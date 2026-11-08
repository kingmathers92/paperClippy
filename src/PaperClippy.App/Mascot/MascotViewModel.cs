using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PaperClippy.App.Mascot;

/// <summary>
/// State and commands for the mascot window. Deliberately small in Milestone 1; the animation
/// state machine and speech bubbles (Milestone 2) attach here.
/// </summary>
public sealed partial class MascotViewModel : ObservableObject
{
    /// <summary>Window size in device-independent pixels at scale 1.0 (same aspect ratio as the 110x206 artwork).</summary>
    public const double BaseWidth = 80;
    public const double BaseHeight = 150;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowWidth))]
    [NotifyPropertyChangedFor(nameof(WindowHeight))]
    private double _scale = 1.0;

    public double WindowWidth => BaseWidth * Scale;

    public double WindowHeight => BaseHeight * Scale;

    public event EventHandler? HideRequested;

    public event EventHandler? ExitRequested;

    [RelayCommand]
    private void Hide() => HideRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void Exit() => ExitRequested?.Invoke(this, EventArgs.Empty);
}
