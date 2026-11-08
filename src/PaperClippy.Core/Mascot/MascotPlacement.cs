namespace PaperClippy.Core.Mascot;

/// <summary>
/// The persisted desktop state of the mascot. <see cref="X"/>/<see cref="Y"/> are physical pixels in
/// virtual-screen coordinates; <c>null</c> means "never placed" (use the default corner).
/// </summary>
public sealed record MascotPlacement(double? X = null, double? Y = null, bool Visible = true, double Scale = 1.0)
{
    public const double MinScale = 0.5;
    public const double MaxScale = 3.0;

    /// <summary>Returns a copy that is safe to use even if the stored data was edited or corrupted.</summary>
    public MascotPlacement Sanitized()
    {
        var scale = double.IsFinite(Scale) ? Math.Clamp(Scale, MinScale, MaxScale) : 1.0;
        var hasPosition = X is { } x && Y is { } y && double.IsFinite(x) && double.IsFinite(y);
        return this with
        {
            Scale = scale,
            X = hasPosition ? X : null,
            Y = hasPosition ? Y : null,
        };
    }
}
