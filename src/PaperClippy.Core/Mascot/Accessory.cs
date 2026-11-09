using System.Text.Json.Serialization;

namespace PaperClippy.Core.Mascot;

/// <summary>Optional things Gem can wear. New accessories = one enum case + one shape in the view.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Accessory
{
    None,
    Glasses,
    PartyHat,
    BowTie,
}
