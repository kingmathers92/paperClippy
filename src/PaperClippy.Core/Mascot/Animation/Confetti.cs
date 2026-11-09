namespace PaperClippy.Core.Mascot.Animation;

/// <summary>
/// A deterministic confetti burst: each particle's position is a pure function of (index, time), so the
/// renderer needs no particle state and tests can check the physics.
/// </summary>
public static class Confetti
{
    public const int Count = 28;
    public const int ColorCount = 6;
    public const double Duration = 2.4;

    /// <summary>Burst origin in the mascot window's design space (the middle of Gem's face area).</summary>
    public const double OriginX = 95;
    public const double OriginY = 130;

    private const double Gravity = 260;

    public readonly record struct Particle(double X, double Y, double Rotation, double Opacity, int ColorIndex, double Size);

    public static Particle Evaluate(int index, double t)
    {
        t = Math.Max(0, t);

        var angle = (-90 + (Random01(index, 1) - 0.5) * 150) * Math.PI / 180;
        var speed = 90 + Random01(index, 2) * 140;
        var x0 = OriginX + (Random01(index, 3) - 0.5) * 30;
        var spin = (200 + Random01(index, 5) * 400) * (Random01(index, 6) < 0.5 ? -1 : 1);

        var sway = (Math.Sin(t * 6 + index) - Math.Sin(index)) * 4; // zero at t = 0, so particles start exactly at their origin
        var x = x0 + Math.Cos(angle) * speed * t + sway;
        var y = OriginY + Math.Sin(angle) * speed * t + 0.5 * Gravity * t * t;
        var opacity = t < Duration * 0.65 ? 1 : Math.Max(0, 1 - (t - Duration * 0.65) / (Duration * 0.35));

        return new Particle(x, y, Random01(index, 4) * 360 + spin * t, opacity, index % ColorCount, 5 + Random01(index, 7) * 4);
    }

    // A small integer hash -> [0,1). Deterministic across runs and platforms.
    private static double Random01(int index, int salt)
    {
        unchecked
        {
            var h = (uint)(index * 73856093) ^ (uint)(salt * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return h / 4294967296.0;
        }
    }
}
