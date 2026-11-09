namespace PaperClippy.Core.Speech;

public static class BubbleTiming
{
    /// <summary>Roughly how long a person needs to read <paramref name="text"/>.</summary>
    public static TimeSpan ReadingTime(string text) => TimeSpan.FromSeconds(1.0 + text.Length / 18.0);

    /// <summary>
    /// How long a bubble stays up once its text is fully shown. Never shorter than the user's chosen duration or
    /// the reading time; bubbles with buttons or choices linger much longer (the user may still be deciding).
    /// </summary>
    public static TimeSpan DisplayTime(string text, int baseSeconds, bool interactive)
    {
        var baseTime = TimeSpan.FromSeconds(Math.Max(1, baseSeconds));
        var reading = ReadingTime(text);
        var time = baseTime > reading ? baseTime : reading;

        if (interactive)
        {
            var longer = TimeSpan.FromSeconds(Math.Max(20, time.TotalSeconds * 2.5));
            time = longer;
        }

        return time > TimeSpan.FromSeconds(90) ? TimeSpan.FromSeconds(90) : time;
    }
}
