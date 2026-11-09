namespace PaperClippy.Core.Personality;

public enum PokeLevel
{
    None,
    Annoyed,
    Furious,
}

/// <summary>Notices when the user is rapidly clicking Gem and says when it has become a "poke".</summary>
public sealed class PokeTracker
{
    /// <summary>Clicks closer together than this belong to the same burst.</summary>
    public static readonly TimeSpan Window = TimeSpan.FromSeconds(1.6);

    private TimeSpan? _last;
    private int _count;

    /// <summary>Registers a click. Returns Annoyed on the 3rd click of a burst and Furious on the 6th, otherwise None.</summary>
    public PokeLevel Register(TimeSpan now)
    {
        if (_last is not { } last || now - last > Window)
            _count = 0;

        _last = now;
        _count++;

        if (_count == 3)
            return PokeLevel.Annoyed;

        if (_count >= 6)
        {
            _count = 0;
            return PokeLevel.Furious;
        }

        return PokeLevel.None;
    }

    public void Reset()
    {
        _count = 0;
        _last = null;
    }
}
