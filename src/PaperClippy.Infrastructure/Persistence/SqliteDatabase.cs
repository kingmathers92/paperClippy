using Microsoft.Data.Sqlite;
using PaperClippy.Infrastructure.Diagnostics;

namespace PaperClippy.Infrastructure.Persistence;

/// <summary>
/// Owns the SQLite file: creates it, migrates it, hands out ready-to-use connections, and recovers
/// from a damaged file by quarantining it (never deleting it) and starting fresh.
/// </summary>
public sealed class SqliteDatabase
{
    // SQLITE_CORRUPT and SQLITE_NOTADB: the file is damaged, as opposed to a bug in our own SQL.
    private const int SqliteCorrupt = 11;
    private const int SqliteNotADatabase = 26;

    private readonly string _connectionString;
    private readonly IReadOnlyList<Migration> _migrations;
    private readonly FileLog? _log;

    public SqliteDatabase(string filePath, IReadOnlyList<Migration>? migrations = null, FileLog? log = null)
    {
        FilePath = filePath;
        _migrations = migrations ?? Migrations.All;
        _log = log;

        for (var i = 0; i < _migrations.Count; i++)
        {
            if (_migrations[i].Version != i + 1)
                throw new ArgumentException("Migrations must be numbered 1, 2, 3 … with no gaps.", nameof(migrations));
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = filePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
        }.ToString();
    }

    public string FilePath { get; }

    public int LatestVersion => _migrations.Count;

    /// <summary>Creates/migrates the database, recovering from a damaged file if necessary.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        try
        {
            await OpenCheckAndMigrateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsDamage(ex))
        {
            _log?.Error("Database file is damaged; quarantining it and starting fresh.", ex);
            Quarantine();
            await OpenCheckAndMigrateAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Opens a connection with PaperClippy's standard pragmas applied. Caller disposes it.</summary>
    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using var pragmas = connection.CreateCommand();
            // WAL: readers don't block the writer and a crash mid-write can't corrupt the file.
            pragmas.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL; PRAGMA foreign_keys = ON;";
            await pragmas.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task OpenCheckAndMigrateAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        await using (var check = connection.CreateCommand())
        {
            check.CommandText = "PRAGMA quick_check(1);";
            var result = await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
            if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                throw new DatabaseCorruptException(result ?? "no result");
        }

        var current = await GetUserVersionAsync(connection, cancellationToken).ConfigureAwait(false);
        if (current > LatestVersion)
            throw new DatabaseTooNewException(current, LatestVersion);

        foreach (var migration in _migrations.Where(m => m.Version > current))
        {
            try
            {
                await using var transaction = connection.BeginTransaction();
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                // user_version lives in the database header, so it is rolled back with the schema change.
                command.CommandText = migration.Sql + $"\nPRAGMA user_version = {migration.Version};";
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log?.Error($"Migration {migration.Version} ({migration.Description}) failed.", ex);
                throw new DatabaseMigrationException(migration.Version, ex);
            }

            _log?.Info($"Applied migration {migration.Version}: {migration.Description}");
        }
    }

    private static async Task<int> GetUserVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool IsDamage(Exception ex)
        => ex is DatabaseCorruptException
           || ex is SqliteException { SqliteErrorCode: SqliteCorrupt or SqliteNotADatabase };

    /// <summary>Moves the damaged database (and its WAL/SHM side files) aside so nothing is lost.</summary>
    private void Quarantine()
    {
        // Pooled connections keep the file open (and locked on Windows) until the pool is cleared.
        SqliteConnection.ClearAllPools();

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var source = FilePath + suffix;
            if (!File.Exists(source))
                continue;

            var target = $"{FilePath}.corrupt-{stamp}{suffix}";
            File.Move(source, target, overwrite: true);
            _log?.Warn($"Moved {Path.GetFileName(source)} to {Path.GetFileName(target)}.");
        }
    }
}
