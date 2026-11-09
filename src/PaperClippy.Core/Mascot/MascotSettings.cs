using System.Text.Json.Serialization;
using PaperClippy.Core.Personality;

namespace PaperClippy.Core.Mascot;

/// <summary>
/// The user's mascot preferences. Persisted as JSON; <see cref="Sanitized"/> makes any stored value safe.
/// Size and position live in <see cref="MascotPlacement"/>.
/// </summary>
public sealed record MascotSettings(
    AnimationIntensity Animation = AnimationIntensity.Normal,
    PersonalityLevel Personality = PersonalityLevel.Playful,
    ChatterFrequency Chatter = ChatterFrequency.Sometimes,
    int BubbleSeconds = 8,
    bool QuietMode = false,
    bool QuietHoursEnabled = false,
    TimeOnly? QuietFrom = null,
    TimeOnly? QuietTo = null,
    int SleepAfterMinutes = 5,
    bool FollowPointer = true,
    bool KeepFocus = false,
    Accessory Accessory = Accessory.None,
    bool FirstRunDone = false)
{
    public const int MinBubbleSeconds = 3;
    public const int MaxBubbleSeconds = 30;

    public static readonly TimeOnly DefaultQuietFrom = new(22, 0);
    public static readonly TimeOnly DefaultQuietTo = new(8, 0);

    /// <summary>Returns a copy that is safe to use whatever was stored (out-of-range numbers, unknown enum values, unset times).</summary>
    public MascotSettings Sanitized() => this with
    {
        Animation = Enum.IsDefined(Animation) ? Animation : AnimationIntensity.Normal,
        Personality = Enum.IsDefined(Personality) ? Personality : PersonalityLevel.Playful,
        Chatter = Enum.IsDefined(Chatter) ? Chatter : ChatterFrequency.Sometimes,
        Accessory = Enum.IsDefined(Accessory) ? Accessory : Accessory.None,
        BubbleSeconds = Math.Clamp(BubbleSeconds, MinBubbleSeconds, MaxBubbleSeconds),
        SleepAfterMinutes = SleepAfterMinutes <= 0 ? 0 : Math.Clamp(SleepAfterMinutes, 1, 240),
    };

    /// <summary>Start of the quiet-hours window (22:00 unless the user chose another time; midnight is allowed).</summary>
    [JsonIgnore]
    public TimeOnly EffectiveQuietFrom => QuietFrom ?? DefaultQuietFrom;

    /// <summary>End of the quiet-hours window (08:00 unless the user chose another time).</summary>
    [JsonIgnore]
    public TimeOnly EffectiveQuietTo => QuietTo ?? DefaultQuietTo;

    /// <summary>The defaults.</summary>
    public static MascotSettings Default { get; } = new MascotSettings();
}
