using PaperClippy.Core.Mascot;

namespace PaperClippy.Core.Personality;

/// <summary>When Gem may speak up unprompted. User-requested speech (clicking, menu choices) ignores all of this.</summary>
public static class ChatterPolicy
{
    /// <summary>Average gap between unprompted remarks, or null when the user chose "Never".</summary>
    public static TimeSpan? MeanInterval(ChatterFrequency frequency) => frequency switch
    {
        ChatterFrequency.Rarely => TimeSpan.FromMinutes(90),
        ChatterFrequency.Sometimes => TimeSpan.FromMinutes(35),
        ChatterFrequency.Often => TimeSpan.FromMinutes(12),
        _ => null,
    };

    /// <summary>A randomised delay (60%-140% of the mean) until the next unprompted remark, or null for "Never".</summary>
    public static TimeSpan? NextDelay(ChatterFrequency frequency, Random random)
        => MeanInterval(frequency) is { } mean ? mean * (0.6 + 0.8 * random.NextDouble()) : null;

    public static bool CanSpeakUnprompted(MascotSettings settings, DateTime localNow)
        => settings.Chatter != ChatterFrequency.Never
           && !settings.QuietMode
           && !QuietHours.IsQuiet(settings.QuietHoursEnabled, settings.EffectiveQuietFrom, settings.EffectiveQuietTo, TimeOnly.FromDateTime(localNow));
}

public static class QuietHours
{
    /// <summary>True if <paramref name="now"/> falls in the quiet window. Windows may wrap past midnight (22:00-08:00).</summary>
    public static bool IsQuiet(bool enabled, TimeOnly from, TimeOnly to, TimeOnly now)
    {
        if (!enabled || from == to)
            return false;

        return from < to
            ? now >= from && now < to
            : now >= from || now < to;
    }
}
