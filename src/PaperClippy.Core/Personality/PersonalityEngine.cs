namespace PaperClippy.Core.Personality;

/// <summary>
/// Chooses what Gem says. Works without AI or a network: a tone-weighted random pick from the library that
/// avoids repeating itself. All randomness is injected so behaviour is reproducible in tests.
/// </summary>
public sealed class PersonalityEngine
{
    private const int MaxNoRepeatWindow = 10;

    private readonly IReadOnlyList<GemMessage> _library;
    private readonly Random _random;
    private readonly List<(MessageCategory Category, string Id)> _history = []; // oldest first

    public PersonalityEngine(Random? random = null, IReadOnlyList<GemMessage>? library = null)
    {
        _random = random ?? new Random();
        _library = library ?? MessageLibrary.All;
    }

    /// <summary>How much a tone is wanted at a given personality level (0 = never).</summary>
    public static double Weight(PersonalityLevel level, Tone tone) => (level, tone) switch
    {
        (PersonalityLevel.Gentle, Tone.Warm) => 3.0,
        (PersonalityLevel.Gentle, Tone.Witty) => 1.0,
        (PersonalityLevel.Gentle, Tone.Sarcastic) => 0,

        (PersonalityLevel.Playful, Tone.Warm) => 1.0,
        (PersonalityLevel.Playful, Tone.Witty) => 2.0,
        (PersonalityLevel.Playful, Tone.Sarcastic) => 0.6,

        (PersonalityLevel.Sassy, Tone.Warm) => 0.5,
        (PersonalityLevel.Sassy, Tone.Witty) => 1.5,
        (PersonalityLevel.Sassy, Tone.Sarcastic) => 3.0,

        _ => 1.0,
    };

    /// <summary>A line for <paramref name="category"/>, or null if nothing fits (never the case for the built-in library).</summary>
    public GemMessage? Pick(MessageCategory category, PersonalityLevel level, DateTime localNow)
    {
        var part = DayPartExtensions.Of(localNow);
        var pool = _library
            .Where(m => m.Category == category && (m.When & part) != 0 && Weight(level, m.Tone) > 0)
            .ToList();

        if (pool.Count == 0)
            return null;

        // A line is "fresh" if it was not among this category's last few picks. The window scales with the pool
        // so small categories still rotate through everything instead of getting stuck.
        var window = Math.Clamp(pool.Count / 2, 1, MaxNoRepeatWindow);
        var recent = _history.Where(h => h.Category == category).TakeLast(window).Select(h => h.Id).ToHashSet();
        var fresh = pool.Where(m => !recent.Contains(m.Id)).ToList();

        GemMessage chosen;
        if (fresh.Count > 0)
        {
            chosen = PickFrom(fresh, level);
        }
        else
        {
            // Everything was used lately: take whichever was used longest ago.
            var lastUse = _history.Select((h, i) => (h.Id, i)).ToLookup(x => x.Id, x => x.i);
            chosen = pool.OrderBy(m => lastUse[m.Id].DefaultIfEmpty(-1).Max()).First();
        }

        _history.Add((category, chosen.Id));
        if (_history.Count > 200)
            _history.RemoveRange(0, 100);

        return chosen;
    }

    /// <summary>A line for an unprompted remark: mostly quips, sometimes a tip.</summary>
    public GemMessage? PickChatter(PersonalityLevel level, DateTime localNow)
        => Pick(_random.NextDouble() < 0.7 ? MessageCategory.Chat : MessageCategory.Tip, level, localNow);

    /// <summary>
    /// Picks the TONE first, by the user's personality level, then a line within that tone. Choosing the line
    /// directly would let the library's own mix of tones override the user's setting.
    /// </summary>
    private GemMessage PickFrom(List<GemMessage> fresh, PersonalityLevel level)
    {
        var tones = fresh.Select(m => m.Tone).Distinct().ToList();
        var toneRoll = _random.NextDouble() * tones.Sum(t => Weight(level, t));
        var tone = tones[^1];
        foreach (var candidate in tones)
        {
            toneRoll -= Weight(level, candidate);
            if (toneRoll <= 0)
            {
                tone = candidate;
                break;
            }
        }

        // Lines written for this time of day get a boost so a late-night quip actually shows up at night.
        var inTone = fresh.Where(m => m.Tone == tone).ToList();
        double W(GemMessage m) => m.When == DayParts.Any ? 1.0 : 2.0;
        var roll = _random.NextDouble() * inTone.Sum(W);
        foreach (var message in inTone)
        {
            roll -= W(message);
            if (roll <= 0)
                return message;
        }

        return inTone[^1];
    }
}
