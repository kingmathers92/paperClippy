using PaperClippy.Core.Geometry;
using Xunit;

namespace PaperClippy.Core.Tests.Geometry;

public class ScreenPlacementTests
{
    // Primary 1920x1080 monitor with a 40px taskbar; a second monitor to its right;
    // and a third to the LEFT of the primary (negative virtual-screen coordinates).
    private static readonly RectD Primary = new(0, 0, 1920, 1040);
    private static readonly RectD Right = new(1920, 0, 1920, 1080);
    private static readonly RectD Left = new(-1280, 0, 1280, 1024);

    [Fact]
    public void FullyVisibleWindow_IsLeftAlone()
    {
        var window = new RectD(300, 200, 110, 205);
        Assert.Equal(window, ScreenPlacement.EnsureVisible(window, [Primary]));
    }

    [Fact]
    public void WindowPartlyOffTheEdge_ButStillReachable_IsLeftAlone()
    {
        // 100px of a 200px window still on screen: the user can still grab it.
        var window = new RectD(-100, 100, 200, 200);
        Assert.Equal(window, ScreenPlacement.EnsureVisible(window, [Primary]));
    }

    [Fact]
    public void WindowAlmostEntirelyOffScreen_IsPulledBackInside()
    {
        // Only 10px visible: below the 48px reachability threshold.
        var window = new RectD(-190, 100, 200, 200);
        var result = ScreenPlacement.EnsureVisible(window, [Primary]);
        Assert.Equal(new RectD(0, 100, 200, 200), result);
    }

    [Fact]
    public void WindowOnUnpluggedMonitor_MovesToTheRemainingMonitor()
    {
        // It used to live on the right-hand monitor, which is now gone.
        var window = new RectD(2500, 100, 200, 200);
        var result = ScreenPlacement.EnsureVisible(window, [Primary]);
        Assert.Equal(new RectD(1720, 100, 200, 200), result);
    }

    [Fact]
    public void WindowBeyondTheLastMonitor_SnapsToTheNearestOne()
    {
        var window = new RectD(3900, 100, 200, 200);
        var result = ScreenPlacement.EnsureVisible(window, [Primary, Right]);
        Assert.Equal(new RectD(3640, 100, 200, 200), result);
    }

    [Fact]
    public void WindowOnMonitorWithNegativeCoordinates_IsKept()
    {
        var window = new RectD(-1000, 200, 200, 200);
        Assert.Equal(window, ScreenPlacement.EnsureVisible(window, [Left, Primary]));
    }

    [Fact]
    public void WindowAboveAndBelowTheDesktop_IsPulledVertically()
    {
        var below = ScreenPlacement.EnsureVisible(new RectD(400, 5000, 200, 200), [Primary]);
        Assert.Equal(new RectD(400, 840, 200, 200), below);

        var above = ScreenPlacement.EnsureVisible(new RectD(400, -5000, 200, 200), [Primary]);
        Assert.Equal(new RectD(400, 0, 200, 200), above);
    }

    [Fact]
    public void WindowHangingMostlyOffTheBottom_EvenWithFullHorizontalOverlap_IsPulledUp()
    {
        // Fully visible horizontally, but only 20px above the bottom edge (e.g. buried under the taskbar).
        var window = new RectD(400, 1020, 200, 200);
        Assert.Equal(new RectD(400, 840, 200, 200), ScreenPlacement.EnsureVisible(window, [Primary]));
    }

    [Fact]
    public void WindowHangingMostlyOffTheTop_EvenWithFullHorizontalOverlap_IsPulledDown()
    {
        var window = new RectD(400, -180, 200, 200);
        Assert.Equal(new RectD(400, 0, 200, 200), ScreenPlacement.EnsureVisible(window, [Primary]));
    }

    [Fact]
    public void WindowLargerThanTheMonitor_AlignsToTopLeft()
    {
        var window = new RectD(-5000, -5000, 3000, 2000);
        var result = ScreenPlacement.EnsureVisible(window, [Primary]);
        Assert.Equal(new RectD(0, 0, 3000, 2000), result);
    }

    [Fact]
    public void NoMonitors_ReturnsTheWindowUnchanged()
    {
        var window = new RectD(-9999, -9999, 100, 100);
        Assert.Equal(window, ScreenPlacement.EnsureVisible(window, []));
    }

    [Fact]
    public void WindowSmallerThanTheThreshold_IsReachableWhenFullyOnScreen()
    {
        var onScreen = new RectD(100, 100, 20, 20);
        Assert.Equal(onScreen, ScreenPlacement.EnsureVisible(onScreen, [Primary]));

        var offScreen = new RectD(-100, 100, 20, 20);
        Assert.Equal(new RectD(0, 100, 20, 20), ScreenPlacement.EnsureVisible(offScreen, [Primary]));
    }

    [Fact]
    public void ResolveStart_WithNoSavedPosition_UsesTheBottomRightOfThePrimaryMonitor()
    {
        var layout = new MonitorLayout([Primary, Right], Primary);
        var result = ScreenPlacement.ResolveStart(null, null, 110, 205, layout);
        Assert.Equal(new RectD(1786, 811, 110, 205), result);
    }

    [Fact]
    public void ResolveStart_WithSavedPositionOnAMissingMonitor_RecoversOntoAVisibleOne()
    {
        var layout = new MonitorLayout([Primary], Primary);
        var result = ScreenPlacement.ResolveStart(2500, 100, 110, 205, layout);
        Assert.Equal(new RectD(1810, 100, 110, 205), result);
    }

    [Fact]
    public void ResolveStart_WithAValidSavedPosition_RestoresItExactly()
    {
        var layout = new MonitorLayout([Primary, Right], Primary);
        var result = ScreenPlacement.ResolveStart(2200, 300, 110, 205, layout);
        Assert.Equal(new RectD(2200, 300, 110, 205), result);
    }

    [Theory]
    [InlineData(double.NaN, 100.0)]
    [InlineData(100.0, double.PositiveInfinity)]
    public void ResolveStart_WithNonFiniteSavedPosition_FallsBackToTheDefaultCorner(double x, double y)
    {
        var layout = new MonitorLayout([Primary], Primary);
        var result = ScreenPlacement.ResolveStart(x, y, 110, 205, layout);
        Assert.Equal(new RectD(1786, 811, 110, 205), result);
    }

    [Fact]
    public void ResolveStart_WithOnlyHalfAPosition_FallsBackToTheDefaultCorner()
    {
        var layout = new MonitorLayout([Primary], Primary);
        var result = ScreenPlacement.ResolveStart(500, null, 110, 205, layout);
        Assert.Equal(new RectD(1786, 811, 110, 205), result);
    }

    [Fact]
    public void Intersect_ReturnsTheOverlap_OrEmptyWhenApart()
    {
        Assert.Equal(new RectD(50, 50, 50, 50), RectD.Intersect(new RectD(0, 0, 100, 100), new RectD(50, 50, 100, 100)));
        Assert.True(RectD.Intersect(new RectD(0, 0, 10, 10), new RectD(20, 20, 10, 10)).IsEmpty);
        // Touching edges do not count as overlapping.
        Assert.True(RectD.Intersect(new RectD(0, 0, 10, 10), new RectD(10, 0, 10, 10)).IsEmpty);
    }

    [Fact]
    public void DistanceTo_IsZeroInside_AndEuclideanOutside()
    {
        var r = new RectD(0, 0, 100, 100);
        Assert.Equal(0.0, r.DistanceTo(50, 50));
        Assert.Equal(5.0, r.DistanceTo(103, 104));
        Assert.Equal(30.0, r.DistanceTo(-30, 40));
    }
}
