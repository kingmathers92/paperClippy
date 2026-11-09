using PaperClippy.Core.Mascot.Animation;

namespace PaperClippy.Core.Personality;

/// <summary>One line Gem can say. <see cref="Mood"/> is the face Gem makes while saying it.</summary>
public sealed record GemMessage(
    string Id,
    MessageCategory Category,
    Tone Tone,
    string Text,
    MascotState Mood = MascotState.Idle,
    DayParts When = DayParts.Any)
{
    /// <summary>The text with <c>{greeting}</c> filled in for the time of day.</summary>
    public string Render(DateTime localTime) => Text.Replace("{greeting}", Greeting(DayPartExtensions.Of(localTime)), StringComparison.Ordinal);

    public static string Greeting(DayParts part) => part switch
    {
        DayParts.Morning => "Good morning",
        DayParts.Afternoon => "Good afternoon",
        DayParts.Evening => "Good evening",
        _ => "Hello, night owl",
    };
}
