using PaperClippy.Core.Speech;

namespace PaperClippy.Core.Mascot.Behavior;

/// <summary>
/// The little menu that opens when Gem is clicked, as data. Pages are navigated with choice ids of the form
/// <c>page:name</c>; everything else is an action id handled by <see cref="GemBrain"/>.
/// </summary>
public static class GemMenu
{
    public const string Main = "main";
    public const string Look = "look";
    public const string Size = "size";
    public const string AccessoryPage = "acc";
    public const string Expressions = "expr";

    public static readonly (string Label, double Scale)[] Sizes = [("Small", 0.75), ("Normal", 1.0), ("Large", 1.5), ("Huge", 2.0)];

    public static BubbleContent Build(string page, MascotSettings settings, double scale)
    {
        var (text, choices) = page switch
        {
            Look => ("How should I look?", new List<BubbleChoice>
            {
                new("page:size", "Size…"),
                new("page:acc", "Accessory…"),
                new("page:expr", "Show me an expression…"),
                new("page:main", "← Back"),
            }),
            Size => ("How big should I be?", SizeChoices(scale)),
            AccessoryPage => ("What shall I wear?", AccessoryChoices(settings.Accessory)),
            Expressions => ("Which face would you like to see?", new List<BubbleChoice>
            {
                new("expr:Happy", "Happy"),
                new("expr:Smug", "Smug"),
                new("expr:Confused", "Confused"),
                new("expr:Thinking", "Thinking"),
                new("expr:Working", "Hard at work"),
                new("expr:Sleeping", "Sleepy"),
                new("expr:Celebrating", "Celebrating"),
                new("page:look", "← Back"),
            }),
            _ => ("What would you like to do?", new List<BubbleChoice>
            {
                new("chat", "Tell me something"),
                new("tour", "Show me around"),
                new("page:look", "Change my look…"),
                new("settings", "Gem settings…"),
                new("quiet", "Quiet mode", settings.QuietMode),
                new("nap", "Take a nap"),
                new("hide", "Hide Gem"),
                new("exit", "Exit PaperClippy"),
            }),
        };

        return new BubbleContent(text, SpeechPriority.User, Choices: choices, AutoDismiss: TimeSpan.FromSeconds(30), Typewriter: false);
    }

    private static List<BubbleChoice> SizeChoices(double scale)
    {
        var list = Sizes.Select(s => new BubbleChoice($"size:{s.Scale:0.##}", s.Label, Math.Abs(s.Scale - scale) < 0.01)).ToList();
        list.Add(new BubbleChoice("page:look", "← Back"));
        return list;
    }

    private static List<BubbleChoice> AccessoryChoices(Accessory current)
    {
        var list = new List<BubbleChoice>
        {
            new("acc:None", "Nothing", current == Accessory.None),
            new("acc:Glasses", "Glasses", current == Accessory.Glasses),
            new("acc:PartyHat", "Party hat", current == Accessory.PartyHat),
            new("acc:BowTie", "Bow tie", current == Accessory.BowTie),
            new("page:look", "← Back"),
        };
        return list;
    }
}
