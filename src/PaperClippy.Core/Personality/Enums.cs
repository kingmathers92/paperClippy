using System.Text.Json.Serialization;

namespace PaperClippy.Core.Personality;

/// <summary>How much attitude Gem has. Chosen by the user.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PersonalityLevel
{
    /// <summary>Warm and encouraging, a little silly. Never sarcastic.</summary>
    Gentle,

    /// <summary>The default: funny and chatty, with the occasional dry, sarcastic aside.</summary>
    Playful,

    /// <summary>Sarcasm is the main course.</summary>
    Sassy,
}

/// <summary>The flavour of a single line of dialogue.</summary>
public enum Tone
{
    Warm,
    Witty,
    Sarcastic,
}

/// <summary>How often Gem speaks up unprompted.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ChatterFrequency
{
    Never,
    Rarely,
    Sometimes,
    Often,
}

/// <summary>The situations Gem has something to say about. The last six are written but not triggered until their features exist.</summary>
public enum MessageCategory
{
    Startup,
    Chat,
    Tip,
    PokeAnnoyed,
    PokeFurious,
    ReturnShort,
    ReturnLong,
    WakeUp,
    Grabbed,
    QuietOn,
    QuietOff,
    Nap,

    // Reserved for later milestones (tasks, notes, focus). Content is ready; nothing triggers them yet.
    NoTasks,
    TaskCompleted,
    DeadlineApproaching,
    CreatingNote,
    FocusStart,
    FocusBreak,
}

[Flags]
public enum DayParts
{
    None = 0,
    Morning = 1,    // 05:00-11:59
    Afternoon = 2,  // 12:00-16:59
    Evening = 4,    // 17:00-21:59
    Night = 8,      // 22:00-04:59
    Any = Morning | Afternoon | Evening | Night,
}

public static class DayPartExtensions
{
    public static DayParts Of(DateTime localTime) => localTime.Hour switch
    {
        >= 5 and < 12 => DayParts.Morning,
        >= 12 and < 17 => DayParts.Afternoon,
        >= 17 and < 22 => DayParts.Evening,
        _ => DayParts.Night,
    };
}
