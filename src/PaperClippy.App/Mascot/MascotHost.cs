using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using PaperClippy.App.Services;
using PaperClippy.Core.Geometry;
using PaperClippy.Core.Mascot;
using PaperClippy.Core.Mascot.Animation;
using PaperClippy.Core.Mascot.Behavior;
using PaperClippy.Core.Speech;
using PaperClippy.Infrastructure.Diagnostics;

namespace PaperClippy.App.Mascot;

/// <summary>
/// Owns Gem's life on the desktop: its window, speech bubble and settings dialog, where it lives on the monitors,
/// and saving its state. It is the <see cref="IGemStage"/> the <see cref="GemBrain"/> acts through: the brain decides
/// what should happen, the host makes it happen on screen. All members must be called on the UI thread.
/// </summary>
internal sealed class MascotHost : IGemStage, IDisposable
{
    private readonly MascotPlacementRepository _placements;
    private readonly MascotSettingsRepository _settingsRepository;
    private readonly MonitorService _monitors;
    private readonly FileLog _log;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _displayChangeTimer;

    private MascotWindow? _window;
    private MascotViewModel? _viewModel;
    private BubbleWindow? _bubble;
    private SettingsWindow? _settingsWindow;
    private GemBrain? _brain;
    private MascotPlacement _placement = new();
    private MascotSettings _settings = MascotSettings.Default;
    private Task _saveChain = Task.CompletedTask;
    private bool _disposed;

    public MascotHost(MascotPlacementRepository placements, MascotSettingsRepository settingsRepository, MonitorService monitors, FileLog log)
    {
        _placements = placements;
        _settingsRepository = settingsRepository;
        _monitors = monitors;
        _log = log;

        // Display changes arrive in bursts (resolution, DPI, docking); settle for a moment, then react once.
        _displayChangeTimer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher) { Interval = TimeSpan.FromMilliseconds(500) };
        _displayChangeTimer.Tick += OnDisplayChangeSettled;
    }

    public bool IsVisible => _window?.IsVisible == true;

    public bool QuietMode => _settings.QuietMode;

    public bool KeepFocus => _settings.KeepFocus;

    // ================================================================ startup

    /// <summary>Loads saved state, creates the window and brain, and shows Gem (unless the user had hidden it).</summary>
    public async Task StartAsync()
    {
        _placement = await _placements.LoadAsync();
        _settings = await _settingsRepository.LoadAsync();

        _viewModel = new MascotViewModel { Scale = _placement.Scale };
        _window = new MascotWindow(_viewModel);
        _window.Closing += OnWindowClosing;
        _window.PointerMoved += (x, y) => _brain?.OnPointerMoved(x, y);
        _window.PointerLeft += () => _brain?.OnPointerLeft();
        _window.Clicked += () => _brain?.OnClicked();
        _window.DragStarted += () => _brain?.OnGrabbed();
        _window.DragVelocity += v => _brain?.OnDragVelocity(v);
        _window.DragEnded += OnDragEnded;

        // Create the native window first so we can position it BEFORE it ever becomes visible.
        _window.EnsureHandle();
        _window.ApplyWidgetStyle(_settings.KeepFocus);
        _window.View.ApplyPalette(GemPalette.Teal);

        _brain = new GemBrain(this, new SystemClock(), new DispatcherScheduler(), new WindowsPresenceSource());
        _brain.SettingsChanged += OnBrainSettingsChanged;

        PlaceAtStart();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        if (_placement.Visible)
        {
            _window.ContentRendered += OnFirstContentRendered;
            _window.Show();
        }

        _brain.Start(_settings, _placement.Visible);
    }

    // ================================================================ showing, hiding, moving

    public void Show()
    {
        if (_window is null)
            return;

        if (!_window.IsVisible)
            _window.Show();

        KeepOnScreen();
        Persist();
        _brain?.OnShown();
    }

    public void Hide()
    {
        if (_window is null)
            return;

        _brain?.OnHidden();
        _bubble?.HideBubble();
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

    /// <summary>Puts Gem back in the bottom-right corner of the primary monitor (and shows it).</summary>
    public void ResetPosition()
    {
        if (_window is null)
            return;

        _bubble?.HideBubble();
        Show();

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

    /// <summary>If Gem has drifted out of reach (monitor change, bad drop), pull it back onto a monitor.</summary>
    private void KeepOnScreen()
    {
        if (_window is null || !_window.TryGetPixelRect(out var rect))
            return;

        var safe = ScreenPlacement.EnsureVisible(rect, _monitors.GetLayout().WorkAreas);
        if (safe != rect)
            _window.MoveTo(safe.X, safe.Y);
    }

    private void SetScale(double scale)
    {
        if (_window is null || _viewModel is null)
            return;

        var clamped = Math.Clamp(scale, MascotPlacement.MinScale, MascotPlacement.MaxScale);
        if (Math.Abs(clamped - _viewModel.Scale) < 0.001)
            return;

        _bubble?.HideBubble();

        // Resize around Gem's feet so it grows upward rather than jumping across the screen.
        var hadRect = _window.TryGetPixelRect(out var before);
        _viewModel.Scale = clamped;
        if (hadRect && _window.TryGetPixelRect(out var after))
            _window.MoveTo(before.CenterX - after.Width / 2, before.Bottom - after.Height);

        KeepOnScreen();
        Persist();
    }

    private void OnFirstContentRendered(object? sender, EventArgs e)
    {
        if (_window is not null)
            _window.ContentRendered -= OnFirstContentRendered;

        KeepOnScreen(); // safety net: the first real layout pass is when the final pixel size is known
    }

    private void OnDragEnded()
    {
        KeepOnScreen();
        Persist();
        _brain?.OnReleased();
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
        _log.Info("Display configuration changed; re-checking Gem's position.");
        _bubble?.HideBubble();
        KeepOnScreen();
        Persist();
    }

    // ================================================================ settings

    public void SetQuietMode(bool quiet) => _brain?.SetQuietMode(quiet);

    public void SetKeepFocus(bool keepFocus) => ApplyUserSettings(_settings with { KeepFocus = keepFocus });

    public void OpenSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        var viewModel = new SettingsViewModel(
            _settings,
            IsVisible,
            _viewModel?.Scale ?? 1.0,
            ApplyUserSettings,
            visible => { if (visible) Show(); else Hide(); },
            SetScale);

        _settingsWindow = new SettingsWindow(viewModel);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    /// <summary>The user changed something in the settings dialog or tray: apply it and save it.</summary>
    private void ApplyUserSettings(MascotSettings settings)
    {
        _settings = settings.Sanitized();
        ApplyFocusMode();
        _brain?.ApplySettings(_settings);
        SaveSettings();
    }

    /// <summary>The brain changed a setting itself (quiet mode, accessory, first-run done): save it and refresh the dialog.</summary>
    private void OnBrainSettingsChanged(MascotSettings settings)
    {
        _settings = settings;
        ApplyFocusMode();
        SaveSettings();
        _settingsWindow?.ViewModel.Reload(settings, IsVisible, _viewModel?.Scale ?? 1.0);
    }

    private void ApplyFocusMode()
    {
        _window?.ApplyWidgetStyle(_settings.KeepFocus);
        _bubble?.SetNoActivate(_settings.KeepFocus);
    }

    private void SaveSettings()
    {
        var snapshot = _settings;
        Enqueue(() => _settingsRepository.SaveAsync(snapshot), "Could not save Gem's settings.");
    }

    // ================================================================ speech bubble

    private void ShowBubble(BubbleContent content)
    {
        if (_window is null || !_window.IsVisible || !_window.TryGetPixelRect(out var windowRect))
            return;

        _bubble ??= CreateBubble();

        var dpi = VisualTreeHelper.GetDpi(_window).DpiScaleX;
        var spec = _bubble.Prepare(content, dpi);
        var anchor = GemLayout.BodyRect(windowRect);
        var pad = 40 * dpi;
        _bubble.NearArea = new RectD(anchor.X - pad, anchor.Y - pad, anchor.Width + pad * 2, anchor.Height + pad * 2);

        var layout = BubblePlacement.Compute(anchor, spec, _monitors.GetLayout().WorkAreas, gap: 2 * dpi);
        _bubble.Present(layout);
    }

    private BubbleWindow CreateBubble()
    {
        var bubble = new BubbleWindow();
        bubble.SetNoActivate(_settings.KeepFocus);
        bubble.ChoiceInvoked += id => _brain?.OnBubbleChoice(id);
        bubble.ActionInvoked += id => _brain?.OnBubbleAction(id);
        bubble.Dismissed += reason => _brain?.OnBubbleDismissed(reason);
        return bubble;
    }

    // ================================================================ IGemStage (what the brain asks for)

    double IGemStage.Scale => _viewModel?.Scale ?? 1.0;

    void IGemStage.ApplyPose(in Pose pose) => _window?.View.Apply(pose);

    void IGemStage.ApplyAccessory(Accessory accessory) => _window?.View.SetAccessory(accessory);

    void IGemStage.ShowBubble(BubbleContent content) => ShowBubble(content);

    void IGemStage.CloseBubble() => _bubble?.HideBubble();

    void IGemStage.SetScale(double scale) => SetScale(scale);

    void IGemStage.Execute(HostCommand command)
    {
        switch (command)
        {
            case HostCommand.Hide:
                Hide();
                break;
            case HostCommand.Exit:
                Application.Current.Shutdown();
                break;
            case HostCommand.OpenSettings:
                OpenSettings();
                break;
        }
    }

    // ================================================================ saving

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

        var snapshot = _placement;
        Enqueue(() => _placements.SaveAsync(snapshot), "Could not save Gem's position.");
    }

    /// <summary>Saves are chained so they always land in order, and they run off the UI thread.</summary>
    private void Enqueue(Func<Task> save, string failureMessage)
    {
        _saveChain = _saveChain
            .ContinueWith(_ => RunSave(save, failureMessage), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default)
            .Unwrap();
    }

    private async Task RunSave(Func<Task> save, string failureMessage)
    {
        try
        {
            await save().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Warn(failureMessage, ex);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _brain?.Dispose();

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
