using PaperClippy.Infrastructure.Persistence;
using PaperClippy.Infrastructure.Tests.Support;
using Xunit;

namespace PaperClippy.Infrastructure.Tests.Persistence;

public class SqliteDatabaseTests
{
    private static readonly Migration V1 = Migrations.All[0];
    private static readonly Migration V2 = new(2, "extra table", "CREATE TABLE extra (id INTEGER PRIMARY KEY);");

    [Fact]
    public async Task FreshDatabase_IsCreatedAtTheLatestVersion()
    {
        using var temp = new TempDatabase();
        var db = temp.Open();

        await db.InitializeAsync();

        Assert.Equal((long)Migrations.All.Count, await TempDatabase.UserVersionAsync(db));
        Assert.True(await TempDatabase.TableExistsAsync(db, "settings"));
    }

    [Fact]
    public async Task Initialize_CreatesMissingFolders()
    {
        using var temp = new TempDatabase();
        var nested = new SqliteDatabase(Path.Combine(temp.Directory, "a", "b", "c.db"));

        await nested.InitializeAsync();

        Assert.True(File.Exists(nested.FilePath));
    }

    [Fact]
    public async Task Initialize_CanRunTwice_WithoutLosingData()
    {
        using var temp = new TempDatabase();
        var db = temp.Open();
        await db.InitializeAsync();
        await new SqliteSettingsStore(db).SetAsync("keep", "me");

        var restarted = temp.Open();
        await restarted.InitializeAsync();

        Assert.Equal("me", await new SqliteSettingsStore(restarted).GetAsync<string>("keep"));
    }

    [Fact]
    public async Task OnlyPendingMigrationsAreApplied_AndExistingDataSurvives()
    {
        using var temp = new TempDatabase();
        var v1Db = temp.Open([V1]);
        await v1Db.InitializeAsync();
        await new SqliteSettingsStore(v1Db).SetAsync("keep", "me");

        var v2Db = temp.Open([V1, V2]);
        await v2Db.InitializeAsync();

        Assert.Equal(2L, await TempDatabase.UserVersionAsync(v2Db));
        Assert.True(await TempDatabase.TableExistsAsync(v2Db, "extra"));
        Assert.Equal("me", await new SqliteSettingsStore(v2Db).GetAsync<string>("keep"));
    }

    [Fact]
    public async Task FailedMigration_IsRolledBackCompletely()
    {
        using var temp = new TempDatabase();
        await temp.Open([V1]).InitializeAsync();

        // The first statement succeeds, the second fails: nothing from this migration may survive.
        var broken = new Migration(2, "half then fail", "CREATE TABLE half_done (id INTEGER); INSERT INTO no_such_table VALUES (1);");
        var db = temp.Open([V1, broken]);

        var error = await Assert.ThrowsAsync<DatabaseMigrationException>(() => db.InitializeAsync());
        Assert.Equal(2, error.Version);

        var inspector = temp.Open([V1]);
        Assert.Equal(1L, await TempDatabase.UserVersionAsync(inspector));
        Assert.False(await TempDatabase.TableExistsAsync(inspector, "half_done"));
    }

    [Fact]
    public async Task DatabaseFromANewerVersion_IsRefusedAndLeftUntouched()
    {
        using var temp = new TempDatabase();
        await temp.Open([V1, V2]).InitializeAsync();

        var older = temp.Open([V1]);
        var error = await Assert.ThrowsAsync<DatabaseTooNewException>(() => older.InitializeAsync());

        Assert.Equal(2, error.FoundVersion);
        Assert.Equal(1, error.SupportedVersion);
        Assert.Equal(2L, await TempDatabase.UserVersionAsync(temp.Open([V1, V2])));
    }

    [Fact]
    public async Task DamagedFile_IsQuarantinedNotDeleted_AndAFreshDatabaseTakesItsPlace()
    {
        using var temp = new TempDatabase();
        var garbage = new byte[4096];
        Random.Shared.NextBytes(garbage);
        await File.WriteAllBytesAsync(temp.FilePath, garbage);

        var db = temp.Open();
        await db.InitializeAsync();

        var quarantined = System.IO.Directory.GetFiles(temp.Directory, "paperclippy.db.corrupt-*")
            .Where(f => !f.EndsWith("-wal") && !f.EndsWith("-shm"))
            .ToArray();
        Assert.Equal(1, quarantined.Length);
        Assert.Equal(garbage, await File.ReadAllBytesAsync(quarantined[0]));

        // The replacement is a normal, empty, working database.
        var store = new SqliteSettingsStore(db);
        Assert.Null(await store.GetAsync<string>("anything"));
        await store.SetAsync("works", 42);
        Assert.Equal(42, await store.GetAsync<int>("works"));
    }

    [Fact]
    public void MigrationsMustBeNumberedWithoutGaps()
    {
        using var temp = new TempDatabase();
        Assert.Throws<ArgumentException>(() => temp.Open([V1, new Migration(3, "gap", "SELECT 1;")]));
    }
}
