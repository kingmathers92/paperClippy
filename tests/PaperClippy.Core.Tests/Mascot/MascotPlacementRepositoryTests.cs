using PaperClippy.Core.Mascot;
using PaperClippy.Core.Tests.Support;
using Xunit;

namespace PaperClippy.Core.Tests.Mascot;

public class MascotPlacementRepositoryTests
{
    [Fact]
    public async Task Load_WhenNothingIsStored_ReturnsDefaults()
    {
        var repo = new MascotPlacementRepository(new InMemorySettingsStore());
        Assert.Equal(new MascotPlacement(), await repo.LoadAsync());
    }

    [Fact]
    public async Task SavedPlacement_RoundTripsIncludingNegativeCoordinates()
    {
        var store = new InMemorySettingsStore();
        var saved = new MascotPlacement(X: -1200.5, Y: 340, Visible: false, Scale: 1.25);

        await new MascotPlacementRepository(store).SaveAsync(saved);

        // A brand-new repository over the same store simulates an application restart.
        var loaded = await new MascotPlacementRepository(store).LoadAsync();
        Assert.Equal(saved, loaded);
    }

    [Fact]
    public async Task Load_SanitizesHandEditedValues()
    {
        var store = new InMemorySettingsStore();
        store.PutRaw(MascotPlacementRepository.Key, """{"X":10,"Y":null,"Visible":true,"Scale":42}""");

        var loaded = await new MascotPlacementRepository(store).LoadAsync();

        Assert.Null(loaded.X);
        Assert.Null(loaded.Y);
        Assert.Equal(MascotPlacement.MaxScale, loaded.Scale);
    }

    [Fact]
    public async Task Load_WhenTheStoredValueIsUnreadable_ReturnsDefaults()
    {
        var store = new InMemorySettingsStore();
        store.PutRaw(MascotPlacementRepository.Key, "{ this is not json");

        Assert.Equal(new MascotPlacement(), await new MascotPlacementRepository(store).LoadAsync());
    }
}
