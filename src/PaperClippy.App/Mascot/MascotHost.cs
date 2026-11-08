using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using PaperClippy.App.Services;
using PaperClippy.Core.Geometry;
using PaperClippy.Core.Mascot;
using PaperClippy.Infrastructure.Diagnostics;

namespace PaperClippy.App.Mascot;

/// <summary>
/// Owns the mascot's life on the desktop: creates its window, restores where it was, keeps it
/// reachable when monitors change, and saves its state. All members must be called on the UI thread.
/// </summary>
internal sealed class MascotHost : IDisposable
{
    private readonly MascotPlacementRepository _repository;
    private readonly MonitorService _monitors;
    private readonly FileLog _log;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _displayChangeTimer;

    private MascotWindow? _window;
    private MascotViewModel? _viewModel;
    private MascotPlacement _placement = new();
    private Task _saveChain = Task.CompletedTask;
    private bool _disposed;

    public MascotHost(MascotPlacementRepository repository, MonitorService monitors, FileLog log)
    {
        _repository = repository;
        _monitors = monitors;
        _log = log;

        // Display changes arrive in bursts (resolution, DPI, docking); settle for a moment, then react once.
        _displayChangeTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500),
        };
        _displayChangeTimer.Tick += OnDisplayChangeSettled;
    }

    public bool IsVisible => _window?.IsVisible == true;

    /// <summary>Loads saved state, creates the window and shows it (unless the user had hidden it).</summary>
    public async Task StartAsync()
    {
        _placement = await _repository.LoadAsync();

        _viewModel = new MascotViewModel { Scale = _placement.Scale };
        _viewModel.HideRequested += (_, _) => Hide();
        _viewModel.ExitRequested += (_, _) => Application.Current.Shutdown();

        _window = new MascotWindow(_viewModel);
        _window.DragFinished += OnDragFinished;
        _window.Closing += OnWindowClosing;

        // Create the native window first so we can position it BEFORE it ever becomes visible.
        _window.EnsureHandle();
        _window.ApplyDesktopWindowStyle();
        PlaceAtStart();

        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        if (_placement.Visible)
        {
            _window.ContentRendered += OnFirstContentRendered;
            _window.Show();
        }
    }

    public void Show()
    {
        if (_window is null)
            return;

        if (!_window.IsVisible)
            _window.Show();

        KeepOnScreen();
        Persist();
    }

    public void Hide()
    {
        if (_window is null)
            return;

        _window.Hide();
        Persist();
    }

    public void Toggle()
    {
        if (IsVisible)
            Hide();
        else
            Show();
    }

    /// <summary>Puts the mascot back in the bottom-right corner of the primary monitor (and shows it).</summary>
    public void ResetPosition()
    {
        if (_window is null)
            return;

        if (!_window.IsVisible)
            _window.Show();

        if (_window.TryGetPixelRect(out var rect))
        {
            var corner = ScreenPlacement.DefaultCorner(rect.Width, rect.Height, _monitors.GetLayout().Primary);
            _window.MoveTo(corner.X, corner.Y);
        }

        KeepOnScreen();
        Persist();
    }

    private void PlaceAtStart()
    {
        if (_window is null || !_window.TryGetPixelRect(out var size))
            return;

        var layout = _monitors.GetLayout();
        var target = ScreenPlacement.ResolveStart(_placement.X, _placement.Y, size.Width, size.Height, layout);
        _window.MoveTo(target.X, target.Y);

        // Moving onto a monitor with a different DPI changes the window's pixel size; re-check with the real size.
        KeepOnScreen();
    }

    /// <summary>If the mascot has drifted out of reach (monitor change, bad drop), pull it back onto a monitor.</summary>
    private void KeepOnScreen()
    {
        if (_window is null || !_window.TryGetPixelRect(out var rect))
            return;

        var safe = ScreenPlacement.EnsureVisible(rect, _monitors.GetLayout().WorkAreas);
        if (safe != rect)
            _window.MoveTo(safe.X, safe.Y);
    }

    private void OnFirstContentRendered(object? sender, EventArgs e)
    {
        if (_window is not null)
            _window.ContentRendered -= OnFirstContentRendered;

        KeepOnScreen(); // safety net: the first real layout pass is when the final pixel size is known
    }

    private void OnDragFinished(object? sender, EventArgs e)
    {
        KeepOnScreen();
        Persist();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Last moment the native window (and so its position) still exists during shutdown.
        Persist();
    }

    // SystemEvents raises this on its own thread; hop to the UI thread before touching anything.
    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        if (_disposed)
            return;

        _dispatcher.InvokeAsync(() =>
        {
            _displayChangeTimer.Stop();
            _displayChangeTimer.Start();
        });
    }

    private void OnDisplayChangeSettled(object? sender, EventArgs e)
    {
        _displayChangeTimer.Stop();
        _log.Info("Display configuration changed; re-checking mascot position.");
        KeepOnScreen();
        Persist();
    }

    private void Persist()
    {
        if (_disposed || _window is null || _viewModel is null || !_window.TryGetPixelRect(out var rect))
            return;

        _placement = _placement with
        {
            X = rect.X,
            Y = rect.Y,
            Visible = _window.IsVisible,
            Scale = _viewModel.Scale,
        };

        // Saves are chained so they always land in order, and they run off the UI thread.
        var snapshot = _placement;
        _saveChain = _saveChain
            .ContinueWith(_ => SaveAsync(snapshot), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default)
            .Unwrap();
    }

    private async Task SaveAsync(MascotPlacement snapshot)
    {
        try
        {
            await _repository.SaveAsync(snapshot).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Warn("Could not save the mascot's position.", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Persist() already ran from Closing; here we only make sure the last save finished, then unhook.
        try
        {
            _saveChain.Wait(TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            _log.Warn("Timed out waiting for the final save.", ex);
        }

        _disposed = true;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _displayChangeTimer.Stop();
        _displayChangeTimer.Tick -= OnDisplayChangeSettled;
    }
}
