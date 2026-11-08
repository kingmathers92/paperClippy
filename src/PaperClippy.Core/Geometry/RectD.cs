namespace PaperClippy.Core.Geometry;

/// <summary>
/// A plain rectangle in a single coordinate space (PaperClippy uses physical pixels in
/// virtual-screen coordinates for everything that touches monitors). Kept independent of
/// WPF/WinForms types so placement logic is testable anywhere.
/// </summary>
public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public RectD WithPosition(double x, double y) => this with { X = x, Y = y };

    /// <summary>The overlapping region of two rectangles, or <c>default</c> (empty) if they don't overlap.</summary>
    public static RectD Intersect(RectD a, RectD b)
    {
        var x = Math.Max(a.X, b.X);
        var y = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);
        return right > x && bottom > y ? new RectD(x, y, right - x, bottom - y) : default;
    }

    /// <summary>Distance from a point to this rectangle; 0 when the point is inside.</summary>
    public double DistanceTo(double px, double py)
    {
        var dx = Math.Max(Math.Max(X - px, 0), px - Right);
        var dy = Math.Max(Math.Max(Y - py, 0), py - Bottom);
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

/// <summary>The monitors' usable areas (taskbar excluded) plus which one is the primary.</summary>
public sealed record MonitorLayout(IReadOnlyList<RectD> WorkAreas, RectD Primary);
