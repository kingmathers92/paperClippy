using System.Collections.Generic;
using PaperClippy.Core.Geometry;

namespace PaperClippy.App.Services;

/// <summary>
/// Reports each monitor's usable area (taskbar excluded) in physical pixels, virtual-screen
/// coordinates. The process is per-monitor DPI aware (see app.manifest), so these are real pixels.
/// </summary>
internal sealed class MonitorService
{
    public MonitorLayout GetLayout()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var areas = new List<RectD>(screens.Length);
        foreach (var screen in screens)
            areas.Add(ToRect(screen.WorkingArea));

        var primary = System.Windows.Forms.Screen.PrimaryScreen is { } p
            ? ToRect(p.WorkingArea)
            : areas.Count > 0 ? areas[0] : new RectD(0, 0, 1280, 720);

        if (areas.Count == 0)
            areas.Add(primary);

        return new MonitorLayout(areas, primary);
    }

    private static RectD ToRect(System.Drawing.Rectangle r) => new(r.X, r.Y, r.Width, r.Height);
}
