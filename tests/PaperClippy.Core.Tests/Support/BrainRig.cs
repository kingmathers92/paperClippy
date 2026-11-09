using PaperClippy.Core.Mascot;
using PaperClippy.Core.Mascot.Animation;
using PaperClippy.Core.Mascot.Behavior;
using PaperClippy.Core.Personality;
using PaperClippy.Core.Speech;

namespace PaperClippy.Core.Tests.Support;

/// <summary>A clock and scheduler in one: time only moves when a test says so, and due callbacks run in order.</summary>
public sealed class VirtualTime : IClock, IScheduler
{
    private sealed class Entry : IDisposable
    {
        public TimeSpan Due;
        public long Sequence;
        public Action? Callback;

        public void Dispose() => Callback = null;
    }

    private readonly List<Entry> _entries = [];
    private long _sequence;

    public VirtualTime(DateTime start) => Start = start;

    public DateTime Start { get; }

    public TimeSpan Now { get; private set; }

    public DateTime LocalNow => Start + Now;

    /// <summary>Callbacks still waiting to run.</summary>
    public int Pending => _entries.Count(e => e.Callback is not null);

    public IDisposable ScheduleOnce(TimeSpan delay, Action callback)
    {
        var entry = new Entry { Due = Now + delay, Sequence = _sequence++, Callback = callback };
        _entries.Add(entry);
        return entry;
    }

    public void Advance(TimeSpan span)
    {
        var target = Now + span;
        while (true)
        {
            Entry? next = null;
            foreach (var e in _entries)
            {
                if (e.Callback is null || e.Due > target)
                    continue;
                if (next is null || e.Due < next.Due || (e.Due == next.Due && e.Sequence < next.Sequence))
                    next = e;
            }

            if (next is null)
                break;

            _entries.Remove(next);
            if (next.Due > Now)
                Now = next.Due;

            var callback = next.Callback!;
            next.Callback = null;
            callback();
        }

        Now = target;
        _entries.RemoveAll(e => e.Callback is null);
    }

    public void Advance(double seconds) => Advance(TimeSpan.FromSeconds(seconds));
}

public sealed class FakeStage : IGemStage
{
    public double Scale { get; set; } = 1.0;

    public Accessory Accessory { get; private set; }

    public Pose Last { get; private set; } = Pose.Neutral;

    public int PoseCount { get; private set; }

    /// <summary>When set, every pose is also kept (leave off for long simulations).</summary>
    public List<Pose>? Recorded { get; set; }

    public BubbleContent? Bubble { get; set; }

    public List<BubbleContent> Shown { get; } = [];

    public List<HostCommand> Commands { get; } = [];

    public event Action<BubbleContent>? BubbleShown;

    public void ApplyPose(in Pose pose)
    {
        Last = pose;
        PoseCount++;
        Recorded?.Add(pose);
    }

    public void ApplyAccessory(Accessory accessory) => Accessory = accessory;

    public void ShowBubble(BubbleContent content)
    {
        Bubble = content;
        Shown.Add(content);
        BubbleShown?.Invoke(content);
    }

    public void CloseBubble() => Bubble = null;

    public void SetScale(double scale) => Scale = scale;

    public void Execute(HostCommand command) => Commands.Add(command);
}

public sealed class FakePresence : IPresenceSource
{
    public TimeSpan Idle { get; set; }

    public TimeSpan GetIdleTime() => Idle;
}

/// <summary>Everything wired together the way the app does it, but on virtual time.</summary>
public sealed class BrainRig
{
    public BrainRig(MascotSettings? settings = null, bool visible = true, int seed = 1, DateTime? start = null)
    {
        Time = new VirtualTime(start ?? new DateTime(2026, 11, 8, 14, 0, 0));
        Stage = new FakeStage();
        Presence = new FakePresence();
        Brain = new GemBrain(Stage, Time, Time, Presence, new Random(seed));

        // Like the real bubble window: it dismisses itself once its text has been shown for the requested time.
        Stage.BubbleShown += content =>
        {
            if (!AutoDismiss)
                return;

            var typing = content.Typewriter && !content.IsMenu ? Typewriter.TotalSeconds(content.Text) : 0;
            Time.ScheduleOnce(TimeSpan.FromSeconds(typing) + (content.AutoDismiss ?? TimeSpan.FromSeconds(8)), () =>
            {
                if (!ReferenceEquals(Stage.Bubble, content))
                    return;

                Stage.Bubble = null;
                Brain.OnBubbleDismissed(BubbleDismissReason.Timeout);
            });
        };

        Brain.SettingsChanged += s => Saved.Add(s);
        Brain.Start(settings ?? MascotSettings.Default with { FirstRunDone = true }, visible);
    }

    public VirtualTime Time { get; }

    public FakeStage Stage { get; }

    public FakePresence Presence { get; }

    public GemBrain Brain { get; }

    public List<MascotSettings> Saved { get; } = [];

    public bool AutoDismiss { get; set; } = true;

    public void Advance(double seconds) => Time.Advance(seconds);

    public void Click(double thenWait = 3)
    {
        Brain.OnClicked();
        Time.Advance(thenWait);
    }

    /// <summary>Every rendering of a category's lines, at every time of day (so tests don't depend on the clock).</summary>
    public static HashSet<string> Texts(params MessageCategory[] categories)
    {
        var set = new HashSet<string>();
        foreach (var message in MessageLibrary.All.Where(m => categories.Contains(m.Category)))
            foreach (var hour in new[] { 8, 14, 19, 23 })
                set.Add(message.Render(new DateTime(2026, 11, 8, hour, 0, 0)));
        return set;
    }

}
