namespace PaperClippy.Core.Mascot.Animation;

/// <summary>
/// The mascot's behavioural states. The first eight are the required set; <see cref="Grabbed"/> is the
/// "being dragged" reaction and <see cref="Smug"/> is the sarcastic look (half-lidded, one brow up, smirk). Adding a state means adding one case to <see cref="MascotAnimator"/>'s pose
/// function and (optionally) a default duration — nothing else.
/// </summary>
public enum MascotState
{
    Idle,
    Thinking,
    Happy,
    Confused,
    Sleeping,
    Working,
    Celebrating,
    Talking,
    Grabbed,
    Smug,
}

/// <summary>The mouth drawn on Gem's face.</summary>
public enum MouthShape
{
    Smile,
    Grin,
    Neutral,
    Open,
    TalkSmall,
    TalkWide,
    Wobbly,
    Smirk,
}

/// <summary>Short one-shot motions layered on top of whatever state Gem is in.</summary>
public enum Reaction
{
    Bounce,
    Squash,
    Startle,
    Wiggle,
}
