using PaperClippy.Core.Geometry;
using PaperClippy.Core.Speech;
using Xunit;

namespace PaperClippy.Core.Tests.Speech;

public class TypewriterTests
{
    [Fact]
    public void Text_AppearsGradually_AndCompletesAtTheTotalTime()
    {
        const string text = "Hello there, friend!";
        var total = Typewriter.TotalSeconds(text);

        Assert.Equal(0, Typewriter.VisibleLength(text, 0));
        var half = Typewriter.VisibleLength(text, total / 2);
        Assert.True(half > 3 && half < text.Length - 3, $"mid-way shows some text, got {half}");
        Assert.Equal(text.Length, Typewriter.VisibleLength(text, total + 0.01));
    }

    [Fact]
    public void VisibleLength_NeverGoesBackwards()
    {
        const string text = "One. Two, three! Four? Done.";
        var previous = 0;
        for (var t = 0.0; t < Typewriter.TotalSeconds(text) + 0.5; t += 0.01)
        {
            var now = Typewriter.VisibleLength(text, t);
            Assert.True(now >= previous);
            previous = now;
        }
    }

    [Fact]
    public void Punctuation_AddsNaturalPauses()
    {
        Assert.True(Typewriter.TotalSeconds("Stop. Stop. Stop.") > Typewriter.TotalSeconds("Stop  Stop  Stop") + 0.4);
    }

    [Fact]
    public void LongerText_TakesLonger_AndEmptyTextIsInstant()
    {
        Assert.True(Typewriter.TotalSeconds(new string('a', 100)) > Typewriter.TotalSeconds(new string('a', 10)));
        Assert.Equal(0, Typewriter.TotalSeconds(""));
        Assert.Equal(0, Typewriter.VisibleLength("", 5));
    }
}

public class BubbleTimingTests
{
    [Fact]
    public void ShortLines_StayForTheUsersChosenDuration()
    {
        Assert.Equal(TimeSpan.FromSeconds(8), BubbleTiming.DisplayTime("Hi!", 8, interactive: false));
    }

    [Fact]
    public void LongLines_StayLongEnoughToRead()
    {
        var text = new string('x', 180);
        Assert.True(BubbleTiming.DisplayTime(text, 3, interactive: false) >= BubbleTiming.ReadingTime(text));
        Assert.True(BubbleTiming.ReadingTime(text) > TimeSpan.FromSeconds(8));
    }

    [Fact]
    public void BubblesWithChoices_LingerMuchLonger_ButNotForever()
    {
        var plain = BubbleTiming.DisplayTime("Pick one", 8, interactive: false);
        var interactive = BubbleTiming.DisplayTime("Pick one", 8, interactive: true);
        Assert.True(interactive >= TimeSpan.FromSeconds(20) && interactive > plain * 2);
        Assert.True(BubbleTiming.DisplayTime(new string('x', 5000), 30, interactive: true) <= TimeSpan.FromSeconds(90));
    }
}

public class BubbleGeometryTests
{
    private static readonly RectD Body = new(20, 20, 200, 80);

    [Theory]
    [InlineData(PointerSide.Top, "120,6")]
    [InlineData(PointerSide.Bottom, "120,114")]
    [InlineData(PointerSide.Left, "6,60")]
    [InlineData(PointerSide.Right, "234,60")]
    public void PointerTip_StickOutOfTheChosenEdge(PointerSide side, string expectedTip)
    {
        var offset = side is PointerSide.Top or PointerSide.Bottom ? 100 : 40;
        var path = BubbleGeometry.PathData(Body, side, offset, 14, 18, 10);

        Assert.True(path.StartsWith("M ") && path.EndsWith("Z"));
        Assert.Contains($"L {expectedTip} ", path);
        Assert.Equal(4, path.Split('A').Length - 1); // four rounded corners
    }

    [Fact]
    public void PointerBase_IsKeptOffTheRoundedCorners()
    {
        // Ask for an offset right in the corner: the pointer must slide to the straight part of the edge.
        var path = BubbleGeometry.PathData(Body, PointerSide.Top, 0, 14, 18, 10);
        Assert.Contains("L 39,6 ", path); // x = 20 + radius(10) + half-base(9)  -> tip at 39
    }

    [Fact]
    public void Path_UsesInvariantNumbers()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.False(BubbleGeometry.PathData(new RectD(0.5, 0.5, 100.25, 50.75), PointerSide.Left, 20, 14, 18, 10).Contains("0,5"));
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}

public class BubblePlacementTests
{
    private static readonly RectD Screen = new(0, 0, 1920, 1040);
    private static readonly BubbleSpec Spec = new(300, 120);

    private static RectD Gem(double x, double y) => new(x, y, 80, 150);

    private static void AssertInside(RectD outer, RectD inner)
        => Assert.True(inner.X >= outer.X - 0.01 && inner.Y >= outer.Y - 0.01 && inner.Right <= outer.Right + 0.01 && inner.Bottom <= outer.Bottom + 0.01,
            $"{inner} is not inside {outer}");

    [Fact]
    public void GemInTheBottomRightCorner_GetsABubbleToItsLeft_PointingAtItsFace()
    {
        var gem = Gem(1800, 860);
        var layout = BubblePlacement.Compute(gem, Spec, [Screen]);

        Assert.Equal(PointerSide.Right, layout.Side);
        AssertInside(Screen, layout.Window);
        Assert.True(layout.Body.Right < gem.X, "bubble does not cover Gem");
        Assert.True(layout.TipX <= gem.X && layout.TipX > gem.X - 12, "pointer tip touches Gem's edge");
        Assert.True(layout.TipY > gem.Y && layout.TipY < gem.Y + gem.Height * 0.7, "pointing at the face, not the feet");
    }

    [Fact]
    public void GemAgainstTheLeftEdge_GetsABubbleToItsRight()
    {
        var gem = Gem(10, 400);
        var layout = BubblePlacement.Compute(gem, Spec, [Screen]);

        Assert.Equal(PointerSide.Left, layout.Side);
        AssertInside(Screen, layout.Window);
        Assert.True(layout.Body.X > gem.Right);
    }

    [Fact]
    public void WhenThereIsNoRoomBesideGem_TheBubbleGoesAbove()
    {
        var narrow = new RectD(0, 0, 330, 900); // Gem in the middle of a very narrow screen: nothing fits left or right
        var gem = Gem(125, 700);
        var layout = BubblePlacement.Compute(gem, Spec, [narrow]);

        Assert.Equal(PointerSide.Bottom, layout.Side);
        AssertInside(narrow, layout.Window);
        Assert.True(layout.Body.Bottom < gem.Y + gem.Height * 0.2);
    }

    [Fact]
    public void WhenOnlyBelowFits_TheBubbleGoesBelow()
    {
        var narrow = new RectD(0, 0, 330, 900);
        var gem = Gem(125, 20);
        var layout = BubblePlacement.Compute(gem, Spec, [narrow]);

        Assert.Equal(PointerSide.Top, layout.Side);
        AssertInside(narrow, layout.Window);
        Assert.True(layout.Body.Y > gem.Bottom);
    }

    [Fact]
    public void NearTheTopEdge_TheBubbleSlidesDown_ButStillPointsAtTheFace()
    {
        var gem = Gem(1800, 4);
        var layout = BubblePlacement.Compute(gem, Spec, [Screen]);

        AssertInside(Screen, layout.Window);
        Assert.True(Math.Abs(layout.TipY - (gem.Y + gem.Height * 0.38)) < 12, $"tip stays on the face, got {layout.TipY}");
        Assert.True(layout.PointerOffset >= 10 + 9 && layout.PointerOffset <= Spec.BodyHeight - 10 - 9, "pointer stays on the straight part of the edge");
    }

    [Fact]
    public void OnASecondMonitor_TheBubbleStaysOnThatMonitor()
    {
        var second = new RectD(1920, 0, 1920, 1080);
        // Gem hugging the left edge of monitor 2: "left of Gem" would be monitor 1, but the bubble must stay put.
        var gem = Gem(1930, 500);
        var layout = BubblePlacement.Compute(gem, Spec, [Screen, second]);

        AssertInside(second, layout.Window);
        Assert.Equal(PointerSide.Left, layout.Side);
    }

    [Fact]
    public void ABubbleBiggerThanTheScreen_IsPinnedToTheTopLeft_WithoutThrowing()
    {
        var tiny = new RectD(0, 0, 200, 100);
        var layout = BubblePlacement.Compute(Gem(60, 0), Spec, [tiny]);

        Assert.True(layout.Window.X >= 0 && layout.Window.Y >= 0);
    }

    [Fact]
    public void WithoutAnyMonitorInformation_ItStillReturnsASensibleLayout()
    {
        var gem = Gem(500, 500);
        var layout = BubblePlacement.Compute(gem, Spec, []);

        Assert.Equal(PointerSide.Right, layout.Side);
        Assert.True(layout.Body.Right < gem.X);
    }

    [Fact]
    public void TheTip_AlwaysLiesOnTheEdgeItIsAttachedTo()
    {
        foreach (var (x, y) in new[] { (1800.0, 860.0), (10.0, 400.0), (900.0, 5.0), (900.0, 880.0), (1000.0, 500.0) })
        {
            var l = BubblePlacement.Compute(Gem(x, y), Spec, [Screen]);
            var b = l.Body;
            switch (l.Side)
            {
                case PointerSide.Right: Assert.True(Math.Abs(l.TipX - (b.Right + 14)) < 0.01 && l.TipY >= b.Y && l.TipY <= b.Bottom); break;
                case PointerSide.Left: Assert.True(Math.Abs(l.TipX - (b.X - 14)) < 0.01 && l.TipY >= b.Y && l.TipY <= b.Bottom); break;
                case PointerSide.Bottom: Assert.True(Math.Abs(l.TipY - (b.Bottom + 14)) < 0.01 && l.TipX >= b.X && l.TipX <= b.Right); break;
                default: Assert.True(Math.Abs(l.TipY - (b.Y - 14)) < 0.01 && l.TipX >= b.X && l.TipX <= b.Right); break;
            }
            AssertInside(Screen, l.Window);
        }
    }
}

public class BubbleContentTests
{
    [Fact]
    public void MenusAndButtonBubbles_AreInteractive_PlainSpeechIsNot()
    {
        Assert.False(new BubbleContent("Hi").IsInteractive);
        Assert.True(new BubbleContent("Hi", Actions: [new BubbleAction("a", "OK")]).IsInteractive);
        var menu = new BubbleContent("Pick", Choices: [new BubbleChoice("x", "X")]);
        Assert.True(menu.IsMenu && menu.IsInteractive);
    }
}
