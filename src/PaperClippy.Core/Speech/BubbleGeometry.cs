using System.Globalization;
using System.Text;
using PaperClippy.Core.Geometry;

namespace PaperClippy.Core.Speech;

/// <summary>Which edge of the bubble the pointer sticks out of (pointing at Gem).</summary>
public enum PointerSide
{
    Top,
    Bottom,
    Left,
    Right,
}

/// <summary>Builds the outline of a speech bubble (rounded rectangle plus pointer) as a path-data string.</summary>
public static class BubbleGeometry
{
    /// <summary>
    /// Path data (WPF/SVG compatible, clockwise) for <paramref name="body"/> with a pointer of the given
    /// <paramref name="length"/> sticking out of <paramref name="side"/>, <paramref name="pointerOffset"/> along that edge
    /// from its start (left edge for Top/Bottom, top edge for Left/Right).
    /// </summary>
    public static string PathData(RectD body, PointerSide side, double pointerOffset, double length, double baseWidth, double radius)
    {
        var (x, y, w, h) = (body.X, body.Y, body.Width, body.Height);
        var r = Math.Min(radius, Math.Min(w, h) / 2);
        var half = baseWidth / 2;

        // Keep the pointer's base on the straight part of the edge.
        double Along(double edgeLength) => Math.Clamp(pointerOffset, r + half, Math.Max(r + half, edgeLength - r - half));

        var sb = new StringBuilder();
        void P(string cmd, double px, double py) => sb.Append(cmd).Append(' ').Append(N(px)).Append(',').Append(N(py)).Append(' ');
        void Arc(double px, double py) => sb.Append("A ").Append(N(r)).Append(',').Append(N(r)).Append(" 0 0 1 ").Append(N(px)).Append(',').Append(N(py)).Append(' ');

        P("M", x + r, y);

        // top edge (left -> right)
        if (side == PointerSide.Top)
        {
            var t = x + Along(w);
            P("L", t - half, y);
            P("L", t, y - length);
            P("L", t + half, y);
        }

        P("L", x + w - r, y);
        Arc(x + w, y + r);

        // right edge (top -> bottom)
        if (side == PointerSide.Right)
        {
            var t = y + Along(h);
            P("L", x + w, t - half);
            P("L", x + w + length, t);
            P("L", x + w, t + half);
        }

        P("L", x + w, y + h - r);
        Arc(x + w - r, y + h);

        // bottom edge (right -> left)
        if (side == PointerSide.Bottom)
        {
            var t = x + Along(w);
            P("L", t + half, y + h);
            P("L", t, y + h + length);
            P("L", t - half, y + h);
        }

        P("L", x + r, y + h);
        Arc(x, y + h - r);

        // left edge (bottom -> top)
        if (side == PointerSide.Left)
        {
            var t = y + Along(h);
            P("L", x, t + half);
            P("L", x - length, t);
            P("L", x, t - half);
        }

        P("L", x, y + r);
        Arc(x + r, y);
        sb.Append('Z');
        return sb.ToString();
    }

    private static string N(double v) => Math.Round(v, 2).ToString("0.##", CultureInfo.InvariantCulture);
}
