using Microsoft.Data.Sqlite;
using PaperClippy.Infrastructure.Persistence;

namespace PaperClippy.Infrastructure.Tests.Support;

/// <summary>A throw-away folder holding a database file; cleaned up (including pooled handles) on dispose.</summary>
public sealed class TempDatabase : IDisposable
{
    public TempDatabase()
    {
        Directory = Path.Combine(Path.GetTempPath(), "paperclippy-tests-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        FilePath = Path.Combine(Directory, "paperclippy.db");
    }

    public string Directory { get; }
    public string FilePath { get; }

    /// <summary>Creates a database object over the same file; call again to simulate an app restart.</summary>
    public SqliteDatabase Open(IReadOnlyList<Migration>? migrations = null) => new(FilePath, migrations);

    public static async Task<long> UserVersionAsync(SqliteDatabase db)
    {
        await using var connection = await db.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return (long)(await command.ExecuteScalarAsync())!;
    }

    public static async Task<bool> TableExistsAsync(SqliteDatabase db, string table)
    {
        await using var connection = await db.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;";
        command.Parameters.AddWithValue("$name", table);
        return (long)(await command.ExecuteScalarAsync())! > 0;
    }

    public void Dispose()
    {
        // Pooled connections keep the file locked on Windows until the pool is cleared.
        SqliteConnection.ClearAllPools();
        try { System.IO.Directory.Delete(Directory, recursive: true); }
        catch (IOException) { /* best effort: temp folder */ }
        catch (UnauthorizedAccessException) { }
    }
}
