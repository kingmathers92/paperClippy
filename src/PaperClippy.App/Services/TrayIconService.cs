using System;
using PaperClippy.App.Mascot;
using Drawing = System.Drawing;
using WinForms = System.Windows.Forms;

namespace PaperClippy.App.Services;

/// <summary>
/// The system-tray presence. Uses the classic WinForms NotifyIcon (native, no extra dependency).
/// Only items that actually work are listed; more arrive with their features in later milestones.
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    private readonly MascotHost _mascot;
    private readonly Drawing.Icon _icon;
    private readonly WinForms.ContextMenuStrip _menu;
    private readonly WinForms.ToolStripMenuItem _toggleItem;
    private readonly WinForms.NotifyIcon _notifyIcon;
    private bool _disposed;

    public TrayIconService(MascotHost mascot)
    {
        _mascot = mascot;

        WinForms.Application.EnableVisualStyles();
        _icon = GemIcon.Create();

        _toggleItem = new WinForms.ToolStripMenuItem("Hide Gem", null, (_, _) => _mascot.Toggle());
        var resetItem = new WinForms.ToolStripMenuItem("Bring Gem to the Corner", null, (_, _) => _mascot.ResetPosition());
        var exitItem = new WinForms.ToolStripMenuItem("Exit PaperClippy", null, (_, _) => System.Windows.Application.Current.Shutdown());

        _menu = new WinForms.ContextMenuStrip();
        _menu.Items.AddRange(new WinForms.ToolStripItem[]
        {
            _toggleItem,
            resetItem,
            new WinForms.ToolStripSeparator(),
            exitItem,
        });
        _menu.Opening += (_, _) => _toggleItem.Text = _mascot.IsVisible ? "Hide Gem" : "Show Gem";

        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = _icon,
            Text = "PaperClippy",
            ContextMenuStrip = _menu,
            Visible = true,
        };
        _notifyIcon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left)
                _mascot.Toggle();
        };
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        // Hide first, otherwise Windows leaves a ghost icon in the tray until the mouse passes over it.
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _icon.Dispose();
    }
}
