using PaperClippy.Core.Geometry;

namespace PaperClippy.Core.Mascot;

/// <summary>
/// Gem's design space. The artwork (the clip itself) is 110 x 206 units; the window around it is larger so
/// overlays (thought bubble, Zzz, question mark, confetti) have room. These numbers are shared by the view and by
/// the speech-bubble placement, which must know where Gem's body is inside its window.
/// </summary>
public static class GemLayout
{
    public const double BodyWidth = 110;
    public const double BodyHeight = 206;

    /// <summary>The body's top-left corner inside the window canvas.</summary>
    public const double BodyLeft = 40;
    public const double BodyTop = 50;

    public const double CanvasWidth = 190;
    public const double CanvasHeight = 266;

    /// <summary>Window DIPs per design unit at scale 1.0.</summary>
    public const double DipsPerUnit = 0.72;

    /// <summary>Pivot for squash/tilt, in body coordinates: the bottom centre, where Gem "stands".</summary>
    public const double PivotX = 55;
    public const double PivotY = 200;

    /// <summary>Where the eyes sit (body coordinates); pointer-following is measured from between them.</summary>
    public const double FaceCenterX = 59;
    public const double FaceCenterY = 98;

    /// <summary>Gem's body rectangle given the window rectangle (both in the same units, e.g. physical pixels).</summary>
    public static RectD BodyRect(RectD window)
    {
        var sx = window.Width / CanvasWidth;
        var sy = window.Height / CanvasHeight;
        return new RectD(window.X + BodyLeft * sx, window.Y + BodyTop * sy, BodyWidth * sx, BodyHeight * sy);
    }
}
