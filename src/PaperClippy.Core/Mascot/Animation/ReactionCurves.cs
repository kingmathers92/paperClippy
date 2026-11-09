namespace PaperClippy.Core.Mascot.Animation;

/// <summary>The shape of each one-shot <see cref="Reaction"/>, applied on top of the current pose.</summary>
internal static class ReactionCurves
{
    public static double Duration(Reaction reaction) => reaction switch
    {
        Reaction.Bounce => 0.62,
        Reaction.Squash => 0.34,
        Reaction.Startle => 0.5,
        Reaction.Wiggle => 0.95,
        _ => 0.5,
    };

    /// <summary><paramref name="t"/> is seconds since the reaction began; <paramref name="motion"/> scales amplitudes.</summary>
    public static Pose Apply(Reaction reaction, double t, double motion, Pose p)
    {
        var d = Duration(reaction);
        if (t < 0 || t >= d)
            return p;

        var u = t / d; // 0..1
        switch (reaction)
        {
            case Reaction.Bounce:
            {
                var arc = Math.Abs(Math.Sin(Math.PI * u));
                var ground = Math.Pow(1 - arc, 3); // squash only when close to the floor
                return p with
                {
                    OffsetY = p.OffsetY - 17 * arc * motion * (1 - 0.35 * u),
                    ScaleY = p.ScaleY * (1 - 0.09 * ground * motion),
                    ScaleX = p.ScaleX * (1 + 0.06 * ground * motion),
                };
            }

            case Reaction.Squash:
            {
                var s = Math.Sin(Math.PI * u);
                return p with
                {
                    ScaleY = p.ScaleY * (1 - 0.15 * s * motion),
                    ScaleX = p.ScaleX * (1 + 0.10 * s * motion),
                };
            }

            case Reaction.Startle:
            {
                var s = Math.Sin(Math.PI * u);
                return p with
                {
                    OffsetY = p.OffsetY - 11 * s * motion,
                    EyeOpenL = p.EyeOpenL + 0.28 * s,
                    EyeOpenR = p.EyeOpenR + 0.28 * s,
                    BrowLiftL = p.BrowLiftL + 0.9 * s,
                    BrowLiftR = p.BrowLiftR + 0.9 * s,
                    BrowOpacity = Math.Max(p.BrowOpacity, s),
                    Mouth = s > 0.35 ? MouthShape.Open : p.Mouth,
                };
            }

            default: // Wiggle
            {
                var decay = Math.Exp(-3.2 * u);
                return p with { Rotation = p.Rotation + 11 * Math.Sin(2 * Math.PI * 3.2 * t) * decay * motion };
            }
        }
    }
}
