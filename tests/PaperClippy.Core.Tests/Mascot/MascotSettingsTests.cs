using PaperClippy.Core.Mascot;
using PaperClippy.Core.Personality;
using PaperClippy.Core.Tests.Support;
using Xunit;

namespace PaperClippy.Core.Tests.Mascot;

public class MascotSettingsTests
{
    [Fact]
    public void Defaults_AreFriendlyAndLively()
    {
        var s = MascotSettings.Default;
        Assert.Equal(AnimationIntensity.Normal, s.Animation);
        Assert.Equal(PersonalityLevel.Playful, s.Personality);
        Assert.Equal(ChatterFrequency.Sometimes, s.Chatter);
        Assert.False(s.QuietMode);
        Assert.False(s.KeepFocus, "focus-preserving windows stay opt-in until tested on real hardware");
        Assert.Equal(Accessory.None, s.Accessory);
        Assert.False(s.FirstRunDone);
        Assert.Equal(new TimeOnly(22, 0), s.EffectiveQuietFrom);
        Assert.Equal(new TimeOnly(8, 0), s.EffectiveQuietTo);
    }

    [Fact]
    public void Sanitized_ClampsNumbers_AndRejectsUnknownEnums()
    {
        var wild = new MascotSettings(BubbleSeconds: 999, SleepAfterMinutes: 9999, Animation: (AnimationIntensity)42, Personality: (PersonalityLevel)(-3), Accessory: (Accessory)77).Sanitized();
        Assert.Equal(MascotSettings.MaxBubbleSeconds, wild.BubbleSeconds);
        Assert.Equal(240, wild.SleepAfterMinutes);
        Assert.Equal(AnimationIntensity.Normal, wild.Animation);
        Assert.Equal(PersonalityLevel.Playful, wild.Personality);
        Assert.Equal(Accessory.None, wild.Accessory);

        Assert.Equal(MascotSettings.MinBubbleSeconds, new MascotSettings(BubbleSeconds: -5).Sanitized().BubbleSeconds);
        Assert.Equal(0, new MascotSettings(SleepAfterMinutes: -1).Sanitized().SleepAfterMinutes);
        Assert.Equal(0, new MascotSettings(SleepAfterMinutes: 0).Sanitized().SleepAfterMinutes);
    }

    [Fact]
    public void MidnightIsAValidQuietHoursStart()
    {
        var s = new MascotSettings(QuietFrom: new TimeOnly(0, 0)).Sanitized();
        Assert.Equal(new TimeOnly(0, 0), s.EffectiveQuietFrom);
    }

    [Fact]
    public async Task Settings_RoundTrip_AsReadableJson()
    {
        var store = new InMemorySettingsStore();
        var repo = new MascotSettingsRepository(store);
        var saved = new MascotSettings(
            Animation: AnimationIntensity.Low, Personality: PersonalityLevel.Sassy, Chatter: ChatterFrequency.Often,
            BubbleSeconds: 12, QuietMode: true, QuietHoursEnabled: true,
            QuietFrom: new TimeOnly(0, 0), QuietTo: new TimeOnly(6, 30), SleepAfterMinutes: 0,
            FollowPointer: false, KeepFocus: true, Accessory: Accessory.PartyHat, FirstRunDone: true);

        await repo.SaveAsync(saved);

        var json = store.GetRaw(MascotSettingsRepository.Key)!;
        Assert.Contains("\"Sassy\"", json);
        Assert.Contains("\"PartyHat\"", json);
        Assert.Equal(saved, await new MascotSettingsRepository(store).LoadAsync());
    }

    [Fact]
    public async Task Load_WhenNothingStored_ReturnsDefaults()
    {
        Assert.Equal(MascotSettings.Default, await new MascotSettingsRepository(new InMemorySettingsStore()).LoadAsync());
    }

    [Fact]
    public async Task Load_WithAnUnknownEnumName_FallsBackToDefaults()
    {
        var store = new InMemorySettingsStore();
        store.PutRaw(MascotSettingsRepository.Key, """{"Personality":"Spicy"}""");
        Assert.Equal(MascotSettings.Default, await new MascotSettingsRepository(store).LoadAsync());
    }

    [Fact]
    public async Task Load_WithOutOfRangeNumbers_ClampsThem()
    {
        var store = new InMemorySettingsStore();
        store.PutRaw(MascotSettingsRepository.Key, """{"BubbleSeconds":500,"SleepAfterMinutes":-8}""");
        var loaded = await new MascotSettingsRepository(store).LoadAsync();
        Assert.Equal(MascotSettings.MaxBubbleSeconds, loaded.BubbleSeconds);
        Assert.Equal(0, loaded.SleepAfterMinutes);
    }

    [Fact]
    public async Task Load_FromAnOlderBuildThatKnewFewerSettings_UsesDefaultsForTheRest()
    {
        var store = new InMemorySettingsStore();
        store.PutRaw(MascotSettingsRepository.Key, """{"QuietMode":true}""");
        var loaded = await new MascotSettingsRepository(store).LoadAsync();
        Assert.True(loaded.QuietMode);
        Assert.Equal(PersonalityLevel.Playful, loaded.Personality);
    }
}
