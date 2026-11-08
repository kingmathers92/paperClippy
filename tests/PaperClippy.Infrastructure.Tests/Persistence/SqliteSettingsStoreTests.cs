using PaperClippy.Core.Mascot;
using PaperClippy.Infrastructure.Persistence;
using PaperClippy.Infrastructure.Tests.Support;
using Xunit;

namespace PaperClippy.Infrastructure.Tests.Persistence;

public class SqliteSettingsStoreTests
{
    private static async Task<(TempDatabase Temp, SqliteDatabase Db, SqliteSettingsStore Store)> CreateAsync()
    {
        var temp = new TempDatabase();
        var db = temp.Open();
        await db.InitializeAsync();
        return (temp, db, new SqliteSettingsStore(db));
    }

    [Fact]
    public async Task MissingKey_ReadsAsDefault()
    {
        var (temp, _, store) = await CreateAsync();
        using (temp)
        {
            Assert.Null(await store.GetAsync<string>("nope"));
            Assert.Equal(0, await store.GetAsync<int>("nope"));
        }
    }

    [Fact]
    public async Task ComplexValues_RoundTrip()
    {
        var (temp, _, store) = await CreateAsync();
        using (temp)
        {
            var placement = new MascotPlacement(X: -1200.5, Y: 340, Visible: false, Scale: 1.25);
            await store.SetAsync(MascotPlacementRepository.Key, placement);

            Assert.Equal(placement, await store.GetAsync<MascotPlacement>(MascotPlacementRepository.Key));
        }
    }

    [Fact]
    public async Task SettingTheSameKeyAgain_Overwrites()
    {
        var (temp, _, store) = await CreateAsync();
        using (temp)
        {
            await store.SetAsync("theme", "xp");
            await store.SetAsync("theme", "win98");

            Assert.Equal("win98", await store.GetAsync<string>("theme"));
        }
    }

    [Fact]
    public async Task Values_SurviveARestart()
    {
        using var temp = new TempDatabase();
        var first = temp.Open();
        await first.InitializeAsync();
        await new SqliteSettingsStore(first).SetAsync("mascot", new MascotPlacement(X: 10, Y: 20));

        // A new SqliteDatabase over the same file is what the next launch of the app sees.
        var second = temp.Open();
        await second.InitializeAsync();
        var loaded = await new SqliteSettingsStore(second).GetAsync<MascotPlacement>("mascot");

        Assert.Equal(new MascotPlacement(X: 10, Y: 20), loaded);
    }

    [Fact]
    public async Task UnreadableStoredJson_ReadsAsDefault_InsteadOfThrowing()
    {
        var (temp, db, store) = await CreateAsync();
        using (temp)
        {
            await using (var connection = await db.OpenConnectionAsync())
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO settings (key, value, updated_utc) VALUES ('bad', '{ not json', 'now');";
                await command.ExecuteNonQueryAsync();
            }

            Assert.Null(await store.GetAsync<MascotPlacement>("bad"));
        }
    }

    [Fact]
    public async Task ManyConcurrentWrites_AreAllPersisted()
    {
        var (temp, _, store) = await CreateAsync();
        using (temp)
        {
            await Task.WhenAll(Enumerable.Range(0, 25).Select(i => store.SetAsync($"key{i}", i)));

            for (var i = 0; i < 25; i++)
                Assert.Equal(i, await store.GetAsync<int>($"key{i}"));
        }
    }
}
