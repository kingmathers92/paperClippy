namespace PaperClippy.Core.Mascot.Animation;

/// <summary>
/// Everything the renderer needs to draw one frame of Gem, as plain numbers. The animator produces these;
/// the WPF view only maps them onto transforms and visibilities. Lengths are in design units (Gem's body is
/// 110 x 206), angles in degrees.
/// </summary>
public readonly record struct Pose(
    double EyeOpenL,        // 0 closed .. 1 open .. ~1.2 wide
    double EyeOpenR,
    double GazeX,           // -1 left .. 1 right (pupil offset)
    double GazeY,           // -1 up .. 1 down
    MouthShape Mouth,
    double BrowLiftL,       // -1 lowered .. 1 raised
    double BrowLiftR,
    double BrowTiltL,       // -1 .. 1; positive = inner end raised (worried), negative = inner end lowered (focused/cross)
    double BrowTiltR,
    double BrowOpacity,     // brows only appear when expressing something
    double CheekGlow,       // 0 .. 1
    double OffsetX,         // body translation
    double OffsetY,         // negative = up
    double Rotation,        // about the feet
    double ScaleX,          // squash & stretch, about the feet
    double ScaleY,
    double QuestionMark,    // overlay opacities / phases
    double Thought,
    double ThoughtPhase,
    double Zzz,
    double ZzzPhase,
    double Working,
    double WorkSpin,
    double ConfettiTime)    // seconds since the burst began; negative = no confetti
{
    public static Pose Neutral { get; } = new(
        EyeOpenL: 1, EyeOpenR: 1, GazeX: 0, GazeY: 0, Mouth: MouthShape.Smile,
        BrowLiftL: 0, BrowLiftR: 0, BrowTiltL: 0, BrowTiltR: 0, BrowOpacity: 0,
        CheekGlow: 0, OffsetX: 0, OffsetY: 0, Rotation: 0, ScaleX: 1, ScaleY: 1,
        QuestionMark: 0, Thought: 0, ThoughtPhase: 0, Zzz: 0, ZzzPhase: 0, Working: 0, WorkSpin: 0, ConfettiTime: -1);

    /// <summary>Smoothly mixes two poses (used to ease between states). Discrete fields switch at the midpoint.</summary>
    public static Pose Lerp(in Pose a, in Pose b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        static double L(double x, double y, double k) => x + (y - x) * k;
        return new Pose(
            L(a.EyeOpenL, b.EyeOpenL, t), L(a.EyeOpenR, b.EyeOpenR, t),
            L(a.GazeX, b.GazeX, t), L(a.GazeY, b.GazeY, t),
            t < 0.5 ? a.Mouth : b.Mouth,
            L(a.BrowLiftL, b.BrowLiftL, t), L(a.BrowLiftR, b.BrowLiftR, t),
            L(a.BrowTiltL, b.BrowTiltL, t), L(a.BrowTiltR, b.BrowTiltR, t),
            L(a.BrowOpacity, b.BrowOpacity, t),
            L(a.CheekGlow, b.CheekGlow, t),
            L(a.OffsetX, b.OffsetX, t), L(a.OffsetY, b.OffsetY, t), L(a.Rotation, b.Rotation, t),
            L(a.ScaleX, b.ScaleX, t), L(a.ScaleY, b.ScaleY, t),
            L(a.QuestionMark, b.QuestionMark, t), L(a.Thought, b.Thought, t), b.ThoughtPhase,
            L(a.Zzz, b.Zzz, t), b.ZzzPhase,
            L(a.Working, b.Working, t), b.WorkSpin,
            b.ConfettiTime);
    }
}
