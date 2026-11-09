namespace PaperClippy.Core.Mascot;

/// <summary>
/// Gem's colours in one place, as hex strings (#RRGGBB). The view reads these, so offering other colours
/// later is "add a palette and a setting", not a redraw. Face colours stay fixed so Gem always reads as Gem.
/// </summary>
public sealed record GemPalette(string Name, string Edge, string Body, string Shine)
{
    public static GemPalette Teal { get; } = new("Teal", Edge: "#0D6B64", Body: "#18A99E", Shine: "#A6F2E6");
}
