using PaperClippy.Core.Mascot;
using PaperClippy.Core.Personality;
using Xunit;

namespace PaperClippy.Core.Tests.Personality;

public class ChatterPolicyTests
{
    private static readonly DateTime Noon = new(2026, 11, 8, 12, 0, 0);

    [Fact]
    public void Never_MeansNever()
    {
        Assert.Null(ChatterPolicy.MeanInterval(ChatterFrequency.Never));
        Assert.Null(ChatterPolicy.NextDelay(ChatterFrequency.Never, new Random(1)));
        Assert.False(ChatterPolicy.CanSpeakUnprompted(new MascotSettings(Chatter: ChatterFrequency.Never), Noon));
    }

    [Fact]
    public void MoreFrequentSettings_MeanShorterGaps()
    {
        var rarely = ChatterPolicy.MeanInterval(ChatterFrequency.Rarely)!.Value;
        var sometimes = ChatterPolicy.MeanInterval(ChatterFrequency.Sometimes)!.Value;
        var often = ChatterPolicy.MeanInterval(ChatterFrequency.Often)!.Value;
        Assert.True(often < sometimes && sometimes < rarely);
    }

    [Fact]
    public void Delays_AreRandomisedWithinSixtyToOneFortyPercent()
    {
        var random = new Random(4);
        var mean = ChatterPolicy.MeanInterval(ChatterFrequency.Sometimes)!.Value;
        var delays = Enumerable.Range(0, 500).Select(_ => ChatterPolicy.NextDelay(ChatterFrequency.Sometimes, random)!.Value).ToList();

        Assert.True(delays.All(d => d >= mean * 0.6 - TimeSpan.FromMilliseconds(1) && d <= mean * 1.4 + TimeSpan.FromMilliseconds(1)));
        Assert.True(delays.Max() - delays.Min() > mean * 0.5, "not all the same");
    }

    [Fact]
    public void QuietMode_SilencesUnpromptedChatter()
    {
        Assert.True(ChatterPolicy.CanSpeakUnprompted(MascotSettings.Default, Noon));
        Assert.False(ChatterPolicy.CanSpeakUnprompted(MascotSettings.Default with { QuietMode = true }, Noon));
    }

    [Fact]
    public void QuietHours_SilenceChatterOnlyInsideTheWindow()
    {
        var settings = MascotSettings.Default with { QuietHoursEnabled = true };
        Assert.False(ChatterPolicy.CanSpeakUnprompted(settings, new DateTime(2026, 11, 8, 23, 0, 0)));
        Assert.True(ChatterPolicy.CanSpeakUnprompted(settings, Noon));
        Assert.True(ChatterPolicy.CanSpeakUnprompted(MascotSettings.Default, new DateTime(2026, 11, 8, 23, 0, 0)), "disabled quiet hours do nothing");
    }

    [Theory]
    [InlineData(23, 0, true)]
    [InlineData(3, 0, true)]
    [InlineData(22, 0, true)]
    [InlineData(7, 59, true)]
    [InlineData(8, 0, false)]
    [InlineData(12, 0, false)]
    [InlineData(21, 59, false)]
    public void QuietHours_CanWrapPastMidnight(int hour, int minute, bool expected)
    {
        Assert.Equal(expected, QuietHours.IsQuiet(true, new TimeOnly(22, 0), new TimeOnly(8, 0), new TimeOnly(hour, minute)));
    }

    [Fact]
    public void QuietHours_WorkWithoutWrapping_AndWithAMidnightStart()
    {
        Assert.True(QuietHours.IsQuiet(true, new TimeOnly(13, 0), new TimeOnly(14, 0), new TimeOnly(13, 30)));
        Assert.False(QuietHours.IsQuiet(true, new TimeOnly(13, 0), new TimeOnly(14, 0), new TimeOnly(14, 0)));
        Assert.True(QuietHours.IsQuiet(true, new TimeOnly(0, 0), new TimeOnly(6, 0), new TimeOnly(0, 0)));
        Assert.True(QuietHours.IsQuiet(true, new TimeOnly(0, 0), new TimeOnly(6, 0), new TimeOnly(5, 59)));
        Assert.False(QuietHours.IsQuiet(true, new TimeOnly(0, 0), new TimeOnly(6, 0), new TimeOnly(6, 0)));
        Assert.False(QuietHours.IsQuiet(true, new TimeOnly(9, 0), new TimeOnly(9, 0), new TimeOnly(9, 0)), "an empty window is no window");
    }
}

public class PokeTrackerTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    [Fact]
    public void SlowClicks_AreNeverPokes()
    {
        var tracker = new PokeTracker();
        for (var i = 0; i < 20; i++)
            Assert.Equal(PokeLevel.None, tracker.Register(S(i * 5)));
    }

    [Fact]
    public void ThirdQuickClick_Annoys_AndSixthEnrages()
    {
        var tracker = new PokeTracker();
        var levels = Enumerable.Range(0, 6).Select(i => tracker.Register(S(i * 0.4))).ToList();

        PokeLevel[] expected = [PokeLevel.None, PokeLevel.None, PokeLevel.Annoyed, PokeLevel.None, PokeLevel.None, PokeLevel.Furious];
        Assert.True(levels.SequenceEqual(expected), string.Join(",", levels));
    }

    [Fact]
    public void AfterAnOutburst_ItStartsFresh()
    {
        var tracker = new PokeTracker();
        for (var i = 0; i < 6; i++)
            tracker.Register(S(i * 0.3));

        Assert.Equal(PokeLevel.None, tracker.Register(S(2.0)));
        Assert.Equal(PokeLevel.None, tracker.Register(S(2.3)));
        Assert.Equal(PokeLevel.Annoyed, tracker.Register(S(2.6)));
    }

    [Fact]
    public void APause_BreaksUpTheBurst()
    {
        var tracker = new PokeTracker();
        tracker.Register(S(0));
        tracker.Register(S(0.5));
        Assert.Equal(PokeLevel.None, tracker.Register(S(5)));
        Assert.Equal(PokeLevel.None, tracker.Register(S(5.4)));
        Assert.Equal(PokeLevel.Annoyed, tracker.Register(S(5.8)));
    }
}
