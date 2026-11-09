namespace PaperClippy.Core.Speech;

/// <summary>
/// "Typing" timing for speech bubbles, with natural pauses at punctuation. The bubble reveals text with
/// <see cref="VisibleLength"/>, and the brain uses <see cref="TotalSeconds"/> to know how long Gem keeps talking.
/// </summary>
public static class Typewriter
{
    public const double DefaultCharsPerSecond = 42;

    private static double Pause(char c) => c switch
    {
        '.' or '!' or '?' or '…' => 0.22,
        ',' or ';' or ':' or '—' => 0.10,
        '\n' => 0.15,
        _ => 0,
    };

    /// <summary>How many characters of <paramref name="text"/> are visible after <paramref name="elapsedSeconds"/>.</summary>
    public static int VisibleLength(string text, double elapsedSeconds, double charsPerSecond = DefaultCharsPerSecond)
    {
        if (elapsedSeconds <= 0 || text.Length == 0)
            return 0;

        var step = 1.0 / charsPerSecond;
        var clock = 0.0;
        for (var i = 0; i < text.Length; i++)
        {
            clock += step;
            if (clock > elapsedSeconds)
                return i;

            clock += Pause(text[i]);
        }

        return text.Length;
    }

    /// <summary>Seconds until the whole text is visible.</summary>
    public static double TotalSeconds(string text, double charsPerSecond = DefaultCharsPerSecond)
    {
        var total = text.Length / charsPerSecond;
        foreach (var c in text)
            total += Pause(c);

        return total;
    }
}
