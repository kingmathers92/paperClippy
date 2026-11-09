using CommunityToolkit.Mvvm.ComponentModel;
using PaperClippy.Core.Mascot;

namespace PaperClippy.App.Mascot;

/// <summary>
/// Size state for the mascot window. Behaviour lives in <see cref="PaperClippy.Core.Mascot.Behavior.GemBrain"/>; this just
/// turns the user's size choice into a window size.
/// </summary>
public sealed partial class MascotViewModel : ObservableObject
{
    /// <summary>Window size in device-independent pixels at scale 1.0.</summary>
    public const double BaseWidth = GemLayout.CanvasWidth * GemLayout.DipsPerUnit;
    public const double BaseHeight = GemLayout.CanvasHeight * GemLayout.DipsPerUnit;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowWidth))]
    [NotifyPropertyChangedFor(nameof(WindowHeight))]
    private double _scale = 1.0;

    public double WindowWidth => BaseWidth * Scale;

    public double WindowHeight => BaseHeight * Scale;
}
