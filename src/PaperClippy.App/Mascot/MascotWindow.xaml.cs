using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PaperClippy.App.Interop;
using PaperClippy.Core.Geometry;
using PaperClippy.Core.Mascot;

namespace PaperClippy.App.Mascot;

/// <summary>
/// The transparent, borderless, always-on-top window that hosts Gem. It reports what the mouse does (hover position,
/// click, drag) and can report/change its own position in physical pixels; deciding what those mean is the brain's job
/// and deciding where Gem lives is the <see cref="MascotHost"/>'s.
/// </summary>
public partial class MascotWindow : Window
{
    private const double GazeReachX = 90;   // design units from the face at which the eyes look fully sideways
    private const double GazeReachY = 110;

    private bool _leftDown;
    private Point _downPoint;

    public MascotWindow(MascotViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public GemView View => Gem;

    /// <summary>Pointer position relative to Gem's face, each axis -1..1, while the mouse is over Gem.</summary>
    public event Action<double, double>? PointerMoved;

    public event Action? PointerLeft;

    /// <summary>A press-and-release without moving: a click (left or right button).</summary>
    public event Action? Clicked;

    public event Action? DragStarted;

    /// <summary>Horizontal speed in pixels/second, sampled while dragging.</summary>
    public event Action<double>? DragVelocity;

    public event Action? DragEnded;

    /// <summary>Creates the native window without showing it, so it can be positioned before it appears.</summary>
    public IntPtr EnsureHandle() => new WindowInteropHelper(this).EnsureHandle();

    /// <summary>Tool window (no taskbar button / Alt+Tab entry), optionally one that never steals keyboard focus.</summary>
    public void ApplyWidgetStyle(bool noActivate) => NativeMethods.SetDesktopWidgetStyle(new WindowInteropHelper(this).Handle, noActivate);

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

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _leftDown = true;
        _downPoint = e.GetPosition(this);
        Surface.CaptureMouse(); // keep receiving moves even if a fast flick leaves the window
        e.Handled = true;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_leftDown && e.LeftButton == MouseButtonState.Pressed)
        {
            var p = e.GetPosition(this);
            if (Math.Abs(p.X - _downPoint.X) >= SystemParameters.MinimumHorizontalDragDistance
                || Math.Abs(p.Y - _downPoint.Y) >= SystemParameters.MinimumVerticalDragDistance)
            {
                BeginDrag();
            }

            return;
        }

        // Not pressed: report where the pointer is relative to Gem's face so the eyes can follow it.
        var inDesign = e.GetPosition(Gem.DesignCanvas);
        var gx = (inDesign.X - (GemLayout.BodyLeft + GemLayout.FaceCenterX)) / GazeReachX;
        var gy = (inDesign.Y - (GemLayout.BodyTop + GemLayout.FaceCenterY)) / GazeReachY;
        PointerMoved?.Invoke(Math.Clamp(gx, -1, 1), Math.Clamp(gy, -1, 1));
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_leftDown)
            return;

        _leftDown = false;
        Surface.ReleaseMouseCapture();
        e.Handled = true;
        Clicked?.Invoke();
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Clicked?.Invoke(); // the menu is the same for either button
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (!_leftDown)
            PointerLeft?.Invoke();
    }

    private void BeginDrag()
    {
        _leftDown = false;
        Surface.ReleaseMouseCapture();
        DragStarted?.Invoke();

        // Sample the window's horizontal speed so Gem can swing like it's dangling. Timers keep firing inside the OS move loop.
        double lastX = double.NaN;
        var last = DateTime.UtcNow;
        double smoothed = 0;
        var sampler = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(40) };
        sampler.Tick += (_, _) =>
        {
            if (!TryGetPixelRect(out var rect))
                return;

            var now = DateTime.UtcNow;
            if (!double.IsNaN(lastX))
            {
                var dt = Math.Max(0.001, (now - last).TotalSeconds);
                smoothed += ((rect.X - lastX) / dt - smoothed) * 0.4;
                DragVelocity?.Invoke(smoothed);
            }

            lastX = rect.X;
            last = now;
        };
        sampler.Start();

        try
        {
            // Blocks in the OS move loop until the button is released; DPI changes while crossing monitors are handled for us.
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // the button was released before the move loop could start
        }
        finally
        {
            sampler.Stop();
        }

        DragVelocity?.Invoke(0);
        DragEnded?.Invoke();
    }
}
