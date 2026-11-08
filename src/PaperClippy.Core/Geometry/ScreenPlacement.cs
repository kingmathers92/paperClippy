namespace PaperClippy.Core.Geometry;

/// <summary>
/// Pure placement rules for desktop windows. The guarantee we care about: a window the user
/// can no longer reach (monitor unplugged, resolution changed, corrupt saved position) is
/// pulled back onto a visible monitor instead of being lost forever.
/// </summary>
public static class ScreenPlacement
{
    /// <summary>How much of a window must stay on a monitor (in each axis) to count as reachable.</summary>
    public const double DefaultMinVisible = 48;

    /// <summary>Gap kept between the mascot and the screen edge when it is first placed.</summary>
    public const double DefaultMargin = 24;

    /// <summary>
    /// Returns <paramref name="window"/> unchanged if enough of it is visible on some monitor;
    /// otherwise moves it (size preserved) fully inside the nearest monitor's work area.
    /// </summary>
    public static RectD EnsureVisible(RectD window, IReadOnlyList<RectD> workAreas, double minVisible = DefaultMinVisible)
    {
        if (workAreas.Count == 0 || window.IsEmpty)
            return window;

        var needW = Math.Min(minVisible, window.Width);
        var needH = Math.Min(minVisible, window.Height);

        foreach (var area in workAreas)
        {
            var overlap = RectD.Intersect(window, area);
            if (overlap.Width >= needW && overlap.Height >= needH)
                return window;
        }

        var nearest = workAreas[0];
        var best = nearest.DistanceTo(window.CenterX, window.CenterY);
        for (var i = 1; i < workAreas.Count; i++)
        {
            var d = workAreas[i].DistanceTo(window.CenterX, window.CenterY);
            if (d < best)
            {
                best = d;
                nearest = workAreas[i];
            }
        }

        return ClampInside(window, nearest);
    }

    /// <summary>
    /// Where the mascot should appear at startup: the saved position if there is one (made safe
    /// with <see cref="EnsureVisible"/>), otherwise the bottom-right corner of the primary monitor.
    /// </summary>
    public static RectD ResolveStart(double? savedX, double? savedY, double width, double height, MonitorLayout layout)
    {
        if (savedX is { } x && savedY is { } y && double.IsFinite(x) && double.IsFinite(y))
            return EnsureVisible(new RectD(x, y, width, height), layout.WorkAreas);

        return DefaultCorner(width, height, layout.Primary);
    }

    /// <summary>Bottom-right corner of an area, inset by <paramref name="margin"/>.</summary>
    public static RectD DefaultCorner(double width, double height, RectD area, double margin = DefaultMargin)
        => ClampInside(new RectD(area.Right - width - margin, area.Bottom - height - margin, width, height), area);

    private static RectD ClampInside(RectD window, RectD area)
    {
        // A window bigger than the area is aligned to the area's top-left so its title/grab zone is reachable.
        var x = window.Width >= area.Width ? area.X : Math.Clamp(window.X, area.X, area.Right - window.Width);
        var y = window.Height >= area.Height ? area.Y : Math.Clamp(window.Y, area.Y, area.Bottom - window.Height);
        return window.WithPosition(x, y);
    }
}
