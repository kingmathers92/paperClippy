using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using PaperClippy.App.Interop;

namespace PaperClippy.App.Services;

/// <summary>
/// Draws the tray icon at runtime so the exact size the shell asks for is always crisp (and no binary
/// asset is needed). Geometry mirrors assets/icons/gem-icon.svg: a bold, rotated, face-less gem clip.
/// </summary>
internal static class GemIcon
{
    private static readonly Color Edge = Color.FromArgb(0x0D, 0x6B, 0x64);
    private static readonly Color Body = Color.FromArgb(0x18, 0xA9, 0x9E);
    private static readonly Color Shine = Color.FromArgb(0xC0, 0xA6, 0xF2, 0xE6);

    public static Icon Create()
    {
        var size = System.Windows.Forms.SystemInformation.SmallIconSize;
        var pixels = System.Math.Max(16, System.Math.Min(size.Width, size.Height));

        using var bitmap = new Bitmap(pixels, pixels, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            // SVG viewBox "-65 -17 240 240", then rotate(38 55 103): applied bottom-up in GDI+.
            g.ScaleTransform(pixels / 240f, pixels / 240f);
            g.TranslateTransform(65, 17);
            g.TranslateTransform(55, 103);
            g.RotateTransform(38);
            g.TranslateTransform(-55, -103);

            using var wire = BuildWire();
            Stroke(g, wire, Edge, 21f, 0, 0);
            Stroke(g, wire, Body, 15f, 0, 0);
            Stroke(g, wire, Shine, 3.6f, -2.5f, -3f);
        }

        // Icon.FromHandle does not own the HICON, so clone it and free the original to avoid a GDI leak.
        var handle = bitmap.GetHicon();
        try
        {
            using var borrowed = Icon.FromHandle(handle);
            return (Icon)borrowed.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    private static GraphicsPath BuildWire()
    {
        // Same path as the XAML/SVG: three vertical runs joined by semicircular bends.
        var path = new GraphicsPath();
        path.StartFigure();
        path.AddLine(40, 144, 40, 66);
        path.AddArc(40, 47, 38, 38, 180, 180);   // inner top
        path.AddLine(78, 66, 78, 164);
        path.AddArc(14, 132, 64, 64, 0, 180);    // bottom
        path.AddLine(14, 164, 14, 54);
        path.AddArc(14, 13, 82, 82, 180, 180);   // outer top
        path.AddLine(96, 54, 96, 138);
        return path;
    }

    private static void Stroke(Graphics g, GraphicsPath path, Color color, float width, float dx, float dy)
    {
        using var pen = new Pen(color, width)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round,
        };

        var state = g.Save();
        g.TranslateTransform(dx, dy);
        g.DrawPath(pen, path);
        g.Restore(state);
    }
}
