using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PaperClippy.App.Interop;
using PaperClippy.Core.Geometry;

namespace PaperClippy.App.Mascot;

/// <summary>
/// The transparent, borderless, always-on-top window that hosts the mascot. It knows how to
/// report and change its own position in physical pixels; deciding WHERE it should be is the
/// <see cref="MascotHost"/>'s job.
/// </summary>
public partial class MascotWindow : Window
{
    public MascotWindow(MascotViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Raised after the user dragged the mascot to a new place (not for a plain click).</summary>
    public event EventHandler? DragFinished;

    /// <summary>Creates the native window without showing it, so it can be positioned before it appears.</summary>
    public IntPtr EnsureHandle() => new WindowInteropHelper(this).EnsureHandle();

    /// <summary>Keeps the mascot out of Alt+Tab and the taskbar (a tool window), like a desktop widget.</summary>
    public void ApplyDesktopWindowStyle()
    {
        var handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE);
        style = (style | NativeMethods.WS_EX_TOOLWINDOW) & ~NativeMethods.WS_EX_APPWINDOW;
        NativeMethods.SetWindowLongPtr(handle, NativeMethods.GWL_EXSTYLE, style);
    }

    /// <summary>Window bounds in physical pixels (virtual-screen coordinates). False if there is no live window.</summary>
    public bool TryGetPixelRect(out RectD rect)
    {
        rect = default;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !NativeMethods.GetWindowRect(handle, out var r))
            return false;

        rect = new RectD(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top);
        return true;
    }

    /// <summary>Moves the window's top-left corner to a physical-pixel position without resizing or activating it.</summary>
    public void MoveTo(double x, double y)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
            return;

        NativeMethods.SetWindowPos(
            handle,
            IntPtr.Zero,
            (int)Math.Round(x),
            (int)Math.Round(y),
            0,
            0,
            NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
    }

    private void OnBodyMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed)
            return;

        e.Handled = true;
        TryGetPixelRect(out var before);

        try
        {
            // Blocks in the OS move loop until the button is released; DPI changes while crossing monitors are handled for us.
            DragMove();
        }
        catch (InvalidOperationException)
        {
            return; // the button was released before the move loop could start
        }

        if (TryGetPixelRect(out var after) && after != before)
            DragFinished?.Invoke(this, EventArgs.Empty);
    }
}
