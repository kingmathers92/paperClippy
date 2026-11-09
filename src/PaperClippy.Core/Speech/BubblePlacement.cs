using PaperClippy.Core.Geometry;

namespace PaperClippy.Core.Speech;

/// <summary>Size and style of a bubble in the same units as the placement (physical pixels).</summary>
public sealed record BubbleSpec(
    double BodyWidth,
    double BodyHeight,
    double PointerLength = 14,
    double PointerBase = 18,
    double Shadow = 4,
    double CornerRadius = 10);

/// <summary>
/// Where a bubble goes. <see cref="Window"/> is the whole window (body + pointer + shadow); <see cref="Body"/> is the
/// bubble proper; the tip of the pointer is at (<see cref="TipX"/>, <see cref="TipY"/>) on Gem.
/// </summary>
public readonly record struct BubbleLayout(RectD Window, RectD Body, PointerSide Side, double PointerOffset, double TipX, double TipY);

/// <summary>
/// Chooses a side of Gem for the bubble: beside Gem first (left, because Gem usually lives at the bottom-right, then
/// right) so it reads like a conversation and stays clear of Gem's overlays; then above, then below. The first side that
/// fits inside Gem's own monitor wins. If nothing fits, the least-bad side is
/// used and clamped onto the screen.
/// </summary>
public static class BubblePlacement
{
    public static BubbleLayout Compute(RectD anchor, BubbleSpec spec, IReadOnlyList<RectD> workAreas, double gap = 4)
    {
        var area = PickArea(anchor, workAreas);
        PointerSide[] order = [PointerSide.Right, PointerSide.Left, PointerSide.Bottom, PointerSide.Top];

        BubbleLayout? best = null;
        var bestOvershoot = double.MaxValue;

        foreach (var side in order)
        {
            var (layout, overshoot) = Place(anchor, spec, area, side, gap);
            if (overshoot <= 0)
                return layout;

            if (overshoot < bestOvershoot)
            {
                best = layout;
                bestOvershoot = overshoot;
            }
        }

        return best!.Value;
    }

    private static RectD? PickArea(RectD anchor, IReadOnlyList<RectD> areas)
    {
        if (areas.Count == 0)
            return null;

        var cx = anchor.CenterX;
        var cy = anchor.CenterY;
        var nearest = areas[0];
        var best = double.MaxValue;
        foreach (var a in areas)
        {
            var d = a.DistanceTo(cx, cy);
            if (d < best)
            {
                best = d;
                nearest = a;
            }
        }

        return nearest;
    }

    /// <summary>Lays the bubble out on one side. The overshoot is how far the primary axis falls outside the area (0 = fits).</summary>
    private static (BubbleLayout Layout, double Overshoot) Place(RectD anchor, BubbleSpec s, RectD? maybeArea, PointerSide side, double gap)
    {
        var (w, h, len, shadow) = (s.BodyWidth, s.BodyHeight, s.PointerLength, s.Shadow);
        var faceY = anchor.Y + anchor.Height * 0.38;     // point at Gem's face
        var headY = anchor.Y + anchor.Height * 0.03;     // or the top of its head
        var inf = double.PositiveInfinity;
        var area = maybeArea ?? new RectD(-inf, -inf, inf * 2, inf * 2);
        var areaLeft = maybeArea is null ? double.NegativeInfinity : area.X;
        var areaTop = maybeArea is null ? double.NegativeInfinity : area.Y;
        var areaRight = maybeArea is null ? double.PositiveInfinity : area.Right;
        var areaBottom = maybeArea is null ? double.PositiveInfinity : area.Bottom;

        double bodyX, bodyY, windowW, windowH, windowX, windowY, overshoot;

        switch (side)
        {
            case PointerSide.Right: // bubble sits left of Gem, pointer on its right edge
                bodyX = anchor.X - gap - len - w;
                bodyY = ClampAxis(faceY - h / 2, areaTop, areaBottom - h - shadow);
                (windowX, windowY, windowW, windowH) = (bodyX, bodyY, w + len + shadow, h + shadow);
                overshoot = Math.Max(0, areaLeft - bodyX);
                break;

            case PointerSide.Left: // bubble sits right of Gem, pointer on its left edge
                bodyX = anchor.Right + gap + len;
                bodyY = ClampAxis(faceY - h / 2, areaTop, areaBottom - h - shadow);
                (windowX, windowY, windowW, windowH) = (bodyX - len, bodyY, len + w + shadow, h + shadow);
                overshoot = Math.Max(0, bodyX + w + shadow - areaRight);
                break;

            case PointerSide.Bottom: // bubble sits above Gem, pointer on its bottom edge
                bodyY = anchor.Y - gap - len - h + (headY - anchor.Y);
                bodyX = ClampAxis(anchor.CenterX - w / 2, areaLeft, areaRight - w - shadow);
                (windowX, windowY, windowW, windowH) = (bodyX, bodyY, w + shadow, h + len + shadow);
                overshoot = Math.Max(0, areaTop - bodyY);
                break;

            default: // Top: bubble sits below Gem, pointer on its top edge
                bodyY = anchor.Bottom + gap + len;
                bodyX = ClampAxis(anchor.CenterX - w / 2, areaLeft, areaRight - w - shadow);
                (windowX, windowY, windowW, windowH) = (bodyX, bodyY - len, w + shadow, len + h + shadow);
                overshoot = Math.Max(0, bodyY + h + shadow - areaBottom);
                break;
        }

        // A bubble bigger than the area is pinned to the area's top-left so at least its start is readable.
        if (maybeArea is not null)
        {
            if (w + shadow + (side is PointerSide.Left or PointerSide.Right ? len : 0) > area.Width) { bodyX = area.X + (side == PointerSide.Left ? len : 0); windowX = area.X; }
            if (h + shadow + (side is PointerSide.Top or PointerSide.Bottom ? len : 0) > area.Height) { bodyY = area.Y + (side == PointerSide.Top ? len : 0); windowY = area.Y; }
        }

        var body = new RectD(bodyX, bodyY, w, h);
        var half = s.PointerBase / 2;
        double offset, tipX, tipY;

        if (side is PointerSide.Left or PointerSide.Right)
        {
            offset = Math.Clamp(faceY - bodyY, s.CornerRadius + half, Math.Max(s.CornerRadius + half, h - s.CornerRadius - half));
            tipY = bodyY + offset;
            tipX = side == PointerSide.Right ? bodyX + w + len : bodyX - len;
        }
        else
        {
            offset = Math.Clamp(anchor.CenterX - bodyX, s.CornerRadius + half, Math.Max(s.CornerRadius + half, w - s.CornerRadius - half));
            tipX = bodyX + offset;
            tipY = side == PointerSide.Bottom ? bodyY + h + len : bodyY - len;
        }

        return (new BubbleLayout(new RectD(windowX, windowY, windowW, windowH), body, side, offset, tipX, tipY), overshoot);
    }

    private static double ClampAxis(double value, double min, double max)
    {
        if (max < min)
            return min; // not enough room: prefer the start (top/left) staying visible

        return Math.Clamp(value, min, max);
    }
}
