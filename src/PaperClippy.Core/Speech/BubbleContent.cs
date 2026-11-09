namespace PaperClippy.Core.Speech;

/// <summary>Who gets the floor when two things want to speak. Higher replaces lower; equal replaces (newer wins).</summary>
public enum SpeechPriority
{
    /// <summary>Unprompted chatter. Dropped if anything is already on screen.</summary>
    Ambient,

    /// <summary>Reactions to events (waking up, quiet mode toggled).</summary>
    Normal,

    /// <summary>The user asked for it (clicked Gem, picked a menu entry).</summary>
    User,
}

public enum BubbleDismissReason
{
    Timeout,
    Closed,
    Choice,
    Action,
    Replaced,
    Hidden,
}

/// <summary>A small button under the text (e.g. "Show me around").</summary>
public sealed record BubbleAction(string Id, string Label);

/// <summary>One row in a menu-style bubble.</summary>
public sealed record BubbleChoice(string Id, string Label, bool Checked = false);

/// <summary>
/// What a speech bubble should show. A bubble is either "just talking" (text, maybe buttons) or a menu (text plus
/// a list of choices); both share the same retro look.
/// </summary>
public sealed record BubbleContent(
    string Text,
    SpeechPriority Priority = SpeechPriority.Normal,
    IReadOnlyList<BubbleAction>? Actions = null,
    IReadOnlyList<BubbleChoice>? Choices = null,
    TimeSpan? AutoDismiss = null,
    bool Typewriter = true)
{
    public bool IsMenu => Choices is { Count: > 0 };

    public bool IsInteractive => IsMenu || Actions is { Count: > 0 };
}
