using PaperClippy.Core.Mascot.Animation;
using PaperClippy.Core.Personality;
using Xunit;

namespace PaperClippy.Core.Tests.Personality;

public class PersonalityEngineTests
{
    private static readonly DateTime Afternoon = new(2026, 11, 8, 14, 0, 0);
    private static readonly DateTime Night = new(2026, 11, 8, 23, 30, 0);
    private static readonly DateTime Morning = new(2026, 11, 8, 8, 0, 0);

    private static PersonalityEngine Engine(int seed = 1) => new(new Random(seed));

    [Fact]
    public void Library_HasUniqueIds_AndReadableText()
    {
        var all = MessageLibrary.All;
        Assert.Equal(all.Count, all.Select(m => m.Id).Distinct().Count());
        Assert.True(all.All(m => !string.IsNullOrWhiteSpace(m.Text)));
        Assert.True(all.All(m => m.Text.Length <= 150), "lines must stay short enough for a speech bubble");
        Assert.True(all.Count >= 80, $"a rich library, got {all.Count}");
    }

    [Fact]
    public void EveryCategory_HasEnoughVariety()
    {
        var minimum = new Dictionary<MessageCategory, int>
        {
            [MessageCategory.Startup] = 8, [MessageCategory.Chat] = 12, [MessageCategory.Tip] = 5,
            [MessageCategory.PokeAnnoyed] = 4, [MessageCategory.PokeFurious] = 4,
            [MessageCategory.ReturnShort] = 3, [MessageCategory.ReturnLong] = 3, [MessageCategory.WakeUp] = 4,
            [MessageCategory.Grabbed] = 4, [MessageCategory.QuietOn] = 3, [MessageCategory.QuietOff] = 3, [MessageCategory.Nap] = 3,
            [MessageCategory.NoTasks] = 4, [MessageCategory.TaskCompleted] = 4, [MessageCategory.DeadlineApproaching] = 4,
            [MessageCategory.CreatingNote] = 4, [MessageCategory.FocusStart] = 4, [MessageCategory.FocusBreak] = 4,
        };

        foreach (var category in Enum.GetValues<MessageCategory>())
            Assert.True(MessageLibrary.All.Count(m => m.Category == category) >= minimum[category], $"{category} needs more lines");
    }

    [Fact]
    public void GentleGem_AlwaysHasSomethingToSay_InEveryCategoryAtEveryTimeOfDay()
    {
        var engine = Engine();
        foreach (var category in Enum.GetValues<MessageCategory>())
            foreach (var hour in new[] { 8, 14, 19, 23 })
                Assert.NotNull(engine.Pick(category, PersonalityLevel.Gentle, new DateTime(2026, 11, 8, hour, 0, 0)));
    }

    [Fact]
    public void GentleGem_IsNeverSarcastic()
    {
        var engine = Engine();
        for (var i = 0; i < 3000; i++)
        {
            var category = (MessageCategory)(i % Enum.GetValues<MessageCategory>().Length);
            Assert.NotEqual(Tone.Sarcastic, engine.Pick(category, PersonalityLevel.Gentle, Afternoon)!.Tone);
        }
    }

    private static double SarcasmShare(PersonalityLevel level)
    {
        var engine = Engine(5);
        var picks = Enumerable.Range(0, 4000).Select(_ => engine.Pick(MessageCategory.Chat, level, Afternoon)!).ToList();
        return picks.Count(m => m.Tone == Tone.Sarcastic) / (double)picks.Count;
    }

    [Fact]
    public void PlayfulGem_IsOnlyOccasionallySarcastic_AndSassyGemMuchMoreSo()
    {
        var playful = SarcasmShare(PersonalityLevel.Playful);
        var sassy = SarcasmShare(PersonalityLevel.Sassy);

        Assert.True(playful is > 0.08 and < 0.25, $"playful sarcasm share {playful:P0}");
        Assert.True(sassy > 0.45, $"sassy sarcasm share {sassy:P0}");
        Assert.True(sassy > playful * 2.5, $"sassy ({sassy:P0}) should be far sassier than playful ({playful:P0})");
    }

    [Fact]
    public void Gem_NeverRepeatsItselfWithinAShortWindow()
    {
        var engine = Engine(11);
        var ids = Enumerable.Range(0, 600).Select(_ => engine.Pick(MessageCategory.Chat, PersonalityLevel.Playful, Afternoon)!.Id).ToList();

        for (var i = 0; i < ids.Count; i++)
            for (var back = 1; back <= 10 && i - back >= 0; back++)
                Assert.True(ids[i] != ids[i - back], $"'{ids[i]}' repeated after only {back} lines");
    }

    [Fact]
    public void SmallCategories_CycleThroughEveryLine_WithoutImmediateRepeats()
    {
        var engine = Engine(2);
        var ids = Enumerable.Range(0, 9).Select(_ => engine.Pick(MessageCategory.QuietOn, PersonalityLevel.Sassy, Afternoon)!.Id).ToList();

        for (var i = 1; i < ids.Count; i++)
            Assert.NotEqual(ids[i - 1], ids[i]);
        Assert.Equal(3, ids.Distinct().Count());
    }

    [Fact]
    public void TimeOfDayLines_OnlyAppearAtTheirTime()
    {
        var engine = Engine(3);
        for (var i = 0; i < 300; i++)
        {
            var day = engine.Pick(MessageCategory.Chat, PersonalityLevel.Playful, Afternoon)!;
            Assert.False(day.When == DayParts.Night, "no late-night remarks in the afternoon");
        }

        var seenNightLine = Enumerable.Range(0, 300)
            .Select(_ => engine.Pick(MessageCategory.Chat, PersonalityLevel.Playful, Night)!)
            .Any(m => m.When == DayParts.Night);
        Assert.True(seenNightLine, "night-specific lines do show up at night");
    }

    [Fact]
    public void Greeting_IsFilledInForTheTimeOfDay()
    {
        var message = new GemMessage("t", MessageCategory.Startup, Tone.Warm, "{greeting}! Hi.");
        Assert.Equal("Good morning! Hi.", message.Render(Morning));
        Assert.Equal("Good afternoon! Hi.", message.Render(Afternoon));
        Assert.Equal("Hello, night owl! Hi.", message.Render(Night));
        Assert.True(MessageLibrary.All.All(m => !m.Render(Afternoon).Contains('{')), "no unfilled placeholders");
    }

    [Fact]
    public void SarcasticLines_WearASmugFace_AndWarmOnesSmile()
    {
        Assert.True(MessageLibrary.All.Where(m => m.Tone == Tone.Sarcastic).All(m => m.Mood == MascotState.Smug || m.Mood == MascotState.Confused));
        Assert.True(MessageLibrary.All.Where(m => m.Tone == Tone.Warm).All(m => m.Mood == MascotState.Happy || m.Mood == MascotState.Confused));
    }

    [Fact]
    public void SameSeed_SameConversation()
    {
        var e1 = Engine(42);
        var e2 = Engine(42);
        for (var i = 0; i < 50; i++)
            Assert.Equal(e1.Pick(MessageCategory.Chat, PersonalityLevel.Playful, Afternoon)!.Id, e2.Pick(MessageCategory.Chat, PersonalityLevel.Playful, Afternoon)!.Id);
    }

    [Fact]
    public void PickChatter_MixesQuipsAndTips()
    {
        var engine = Engine(8);
        var categories = Enumerable.Range(0, 200).Select(_ => engine.PickChatter(PersonalityLevel.Playful, Afternoon)!.Category).ToList();
        Assert.True(categories.Contains(MessageCategory.Chat) && categories.Contains(MessageCategory.Tip));
        Assert.True(categories.Count(c => c == MessageCategory.Chat) > categories.Count(c => c == MessageCategory.Tip), "quips outnumber tips");
    }

    [Fact]
    public void UnknownCategoryInACustomLibrary_ReturnsNull()
    {
        var engine = new PersonalityEngine(new Random(1), []);
        Assert.Null(engine.Pick(MessageCategory.Startup, PersonalityLevel.Playful, Afternoon));
    }
}
