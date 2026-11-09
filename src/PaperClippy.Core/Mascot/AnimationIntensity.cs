using System.Text.Json.Serialization;

namespace PaperClippy.Core.Mascot;

/// <summary>How lively Gem is. <see cref="Off"/> keeps expressions but removes all autonomous motion.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnimationIntensity
{
    Off,
    Low,
    Normal,
    High,
}
