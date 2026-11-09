using PaperClippy.Core.Mascot.Animation;
using PaperClippy.Core.Speech;

namespace PaperClippy.Core.Mascot.Behavior;

/// <summary>Time source. <see cref="Now"/> is monotonic (for animation); <see cref="LocalNow"/> is wall-clock (for greetings, quiet hours).</summary>
public interface IClock
{
    TimeSpan Now { get; }

    DateTime LocalNow { get; }
}

/// <summary>Runs a callback once after a delay, on the UI thread. Dispose the result to cancel.</summary>
public interface IScheduler
{
    IDisposable ScheduleOnce(TimeSpan delay, Action callback);
}

/// <summary>How long since the user last touched the keyboard or mouse anywhere on the system.</summary>
public interface IPresenceSource
{
    TimeSpan GetIdleTime();
}

/// <summary>Things the brain cannot do itself and asks the application shell for.</summary>
public enum HostCommand
{
    Hide,
    Exit,
    OpenSettings,
}

/// <summary>
/// Everything the brain needs from the outside world: somewhere to draw Gem and its bubbles, and a few
/// shell-level actions. Implemented by the WPF layer; faked in tests.
/// </summary>
public interface IGemStage
{
    double Scale { get; }

    void ApplyPose(in Pose pose);

    void ApplyAccessory(Accessory accessory);

    void ShowBubble(BubbleContent content);

    void CloseBubble();

    void SetScale(double scale);

    void Execute(HostCommand command);
}
