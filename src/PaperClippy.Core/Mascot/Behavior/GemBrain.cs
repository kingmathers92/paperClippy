using PaperClippy.Core.Mascot.Animation;
using PaperClippy.Core.Personality;
using PaperClippy.Core.Speech;

namespace PaperClippy.Core.Mascot.Behavior;

/// <summary>
/// Gem's whole behaviour in one UI-free place: it turns what happens (the pointer moves, Gem is clicked or dragged,
/// time passes, the user goes away) into what Gem does (animates, talks, opens its menu, falls asleep).
///
/// It talks to the outside world only through <see cref="IGemStage"/>, <see cref="IClock"/>, <see cref="IScheduler"/>
/// and <see cref="IPresenceSource"/>, so it can be driven by a virtual clock in tests. All members must be called
/// from one thread (the UI thread in the app).
///
/// Cost model: while hidden it schedules nothing at all. While visible it schedules animation frames only when
/// something is moving, a rare chatter timer (minutes apart), and - only if "fall asleep when I'm away" is on - a
/// presence check every 15 seconds.
/// </summary>
public sealed class GemBrain : IDisposable
{
    private static readonly TimeSpan MinAmbientGap = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan PresencePollAwake = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PresencePollAsleep = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan LongAway = TimeSpan.FromMinutes(60);

    private static readonly string[] TourSteps =
    [
        "Hi, I'm Gem! A quick tour: you can drag me anywhere, and I'll remember my spot.",
        "Click me any time for my menu. That's where my settings, sizes and silly hats live.",
        "I chat now and then. Too much? Switch on Quiet mode in my menu, or turn me down in my settings.",
        "That's the tour! Notes, to-dos and reminders are coming soon. Until then, I'll keep your desktop company.",
    ];

    private readonly IGemStage _stage;
    private readonly IClock _clock;
    private readonly IScheduler _scheduler;
    private readonly IPresenceSource _presence;
    private readonly Random _random;
    private readonly MascotAnimator _animator;
    private readonly PersonalityEngine _engine;
    private readonly PokeTracker _pokes = new();

    private MascotSettings _settings = MascotSettings.Default;
    private bool _visible;
    private bool _greeted;
    private bool _asleep;
    private bool _napping;
    private bool _grabbed;
    private bool _firstRunActive;
    private int _tourStep = -1;
    private TimeSpan _awayPeak;
    private TimeSpan? _lastSpoke;
    private TimeSpan _frameDueAt;
    private MascotState _talkMood = MascotState.Idle;
    private BubbleContent? _bubble;

    private IDisposable? _frame;
    private IDisposable? _chatter;
    private IDisposable? _presenceTimer;
    private IDisposable? _talkEnd;
    private IDisposable? _startupTimer;

    public GemBrain(IGemStage stage, IClock clock, IScheduler scheduler, IPresenceSource presence, Random? random = null)
    {
        _stage = stage;
        _clock = clock;
        _scheduler = scheduler;
        _presence = presence;
        _random = random ?? new Random();
        _animator = new MascotAnimator(_random);
        _engine = new PersonalityEngine(_random);
    }

    /// <summary>Raised when the brain itself changes a setting (quiet mode, accessory, first-run done) so the app can save it.</summary>
    public event Action<MascotSettings>? SettingsChanged;

    public MascotSettings Settings => _settings;

    public MascotState State => _animator.State;

    public bool IsAsleep => _asleep;

    // ============================================================== lifecycle

    public void Start(MascotSettings settings, bool visible)
    {
        _settings = settings.Sanitized();
        _animator.SetIntensity(_settings.Animation);
        _stage.ApplyAccessory(_settings.Accessory);

        if (visible)
            OnShown();
    }

    public void OnShown()
    {
        if (_visible)
            return;

        _visible = true;
        Pump();
        SchedulePresence();
        ScheduleChatter();

        if (!_greeted)
        {
            _greeted = true;
            _startupTimer = _scheduler.ScheduleOnce(TimeSpan.FromSeconds(_settings.FirstRunDone ? 1.2 : 0.9), OnStartupTimer);
        }
    }

    /// <summary>Gem is no longer on screen: stop everything. Nothing is scheduled until <see cref="OnShown"/>.</summary>
    public void OnHidden()
    {
        if (!_visible)
            return;

        _visible = false;
        CancelTimers();
        CloseBubbleInternal();
    }

    public void Dispose() => CancelTimers();

    private void CancelTimers()
    {
        foreach (var timer in new[] { _frame, _chatter, _presenceTimer, _talkEnd, _startupTimer })
            timer?.Dispose();

        _frame = _chatter = _presenceTimer = _talkEnd = _startupTimer = null;
    }

    // ============================================================== settings

    /// <summary>Applies settings chosen elsewhere (the settings window). Does not raise <see cref="SettingsChanged"/>.</summary>
    public void ApplySettings(MascotSettings settings)
    {
        _settings = settings.Sanitized();
        ApplyDerivedSettings();
    }

    private void UpdateSettings(MascotSettings settings)
    {
        _settings = settings.Sanitized();
        ApplyDerivedSettings();
        SettingsChanged?.Invoke(_settings);
    }

    private void ApplyDerivedSettings()
    {
        if (_animator.Intensity != _settings.Animation)
            _animator.SetIntensity(_settings.Animation);

        _stage.ApplyAccessory(_settings.Accessory);

        if (!_settings.FollowPointer)
            _animator.ClearPointer();

        if (_visible)
        {
            ScheduleChatter();
            SchedulePresence();
            Pump();
        }
    }

    public void SetQuietMode(bool quiet, bool announce = true)
    {
        if (_settings.QuietMode == quiet)
            return;

        UpdateSettings(_settings with { QuietMode = quiet });
        if (announce)
            Say(quiet ? MessageCategory.QuietOn : MessageCategory.QuietOff, SpeechPriority.User);
    }

    // ============================================================== animation pump

    private void Pump()
    {
        _frame?.Dispose();
        _frame = null;
        if (!_visible)
            return;

        var now = _clock.Now;
        _stage.ApplyPose(_animator.Evaluate(now));

        if (_animator.NextFrameDelay(now) is { } delay)
        {
            _frameDueAt = now + delay;
            _frame = _scheduler.ScheduleOnce(delay, Pump);
        }
    }

    /// <summary>Make sure a frame is drawn soon (an input just changed what Gem should look like).</summary>
    private void Wake(bool force = true)
    {
        if (!_visible)
            return;

        if (!force && _frame is not null && _frameDueAt - _clock.Now <= TimeSpan.FromMilliseconds(100))
            return; // frames are already flowing

        Pump();
    }

    /// <summary>Plays a face on request (menu demos, and later: task completed -> Celebrating).</summary>
    public void RequestExpression(MascotState state, TimeSpan? duration = null)
    {
        if (_asleep || _grabbed)
            return;

        _animator.SetState(state, _clock.Now, duration);
        Wake();
    }

    // ============================================================== pointer & mouse

    public void OnPointerMoved(double x, double y)
    {
        if (!_visible || !_settings.FollowPointer)
            return;

        _animator.SetPointer(x, y);
        Wake(force: false);
    }

    public void OnPointerLeft()
    {
        _animator.ClearPointer();
        Wake();
    }

    public void OnGrabbed()
    {
        if (!_visible)
            return;

        _grabbed = true;
        CloseBubbleInternal();
        if (_asleep)
            WakeUp(byPoke: true, TimeSpan.Zero, speak: false);

        _animator.SetState(MascotState.Grabbed, _clock.Now);
        Wake();
    }

    public void OnDragVelocity(double pixelsPerSecond) => _animator.SetDragVelocity(pixelsPerSecond);

    public void OnReleased()
    {
        if (!_grabbed)
            return;

        _grabbed = false;
        var now = _clock.Now;
        _animator.SetState(MascotState.Idle, now);
        _animator.React(Reaction.Squash, now);

        if (_random.NextDouble() < 0.2)
            Say(MessageCategory.Grabbed, SpeechPriority.Normal);

        Wake();
    }

    public void OnClicked()
    {
        if (!_visible || _grabbed)
            return;

        var now = _clock.Now;
        var poke = _pokes.Register(now);

        if (_asleep)
        {
            WakeUp(byPoke: true, TimeSpan.Zero, speak: true);
            return;
        }

        if (poke != PokeLevel.None)
        {
            CloseBubbleInternal();
            _animator.React(Reaction.Wiggle, now);
            Say(poke == PokeLevel.Annoyed ? MessageCategory.PokeAnnoyed : MessageCategory.PokeFurious, SpeechPriority.User);
            Wake();
            return;
        }

        _animator.React(Reaction.Squash, now);
        if (_bubble is { IsMenu: true })
            CloseBubbleInternal();
        else
            ShowMenu(GemMenu.Main);

        Wake();
    }

    // ============================================================== speech

    /// <summary>Says something from <paramref name="category"/>. Returns false if it was suppressed (hidden, busy with a more important bubble).</summary>
    public bool Say(MessageCategory category, SpeechPriority priority = SpeechPriority.Normal)
    {
        var message = _engine.Pick(category, _settings.Personality, _clock.LocalNow);
        return message is not null && SayMessage(message, priority);
    }

    private bool SayMessage(GemMessage message, SpeechPriority priority)
    {
        if (!_visible || _grabbed)
            return false;

        var text = message.Render(_clock.LocalNow);
        var content = new BubbleContent(text, priority, AutoDismiss: BubbleTiming.DisplayTime(text, _settings.BubbleSeconds, interactive: false));
        return Present(content, message.Mood);
    }

    private bool Present(BubbleContent content, MascotState mood)
    {
        if (!_visible)
            return false;

        if (_bubble is { } current && (content.Priority == SpeechPriority.Ambient || content.Priority < current.Priority))
            return false;

        if (_firstRunActive)
            MarkFirstRunDone(); // the intro is being replaced: the user has seen it

        EndTalking(immediate: true);
        _bubble = content;
        _lastSpoke = _clock.Now;
        _stage.ShowBubble(content);

        if (!_asleep)
        {
            if (!content.IsMenu && content.Typewriter)
                StartTalking(content.Text, mood);
            else
                _animator.SetState(MascotState.Idle, _clock.Now);
        }

        Wake();
        return true;
    }

    private void StartTalking(string text, MascotState mood)
    {
        _talkMood = mood;
        _animator.SetState(MascotState.Talking, _clock.Now, Timeout.InfiniteTimeSpan, mood == MascotState.Talking ? MascotState.Idle : mood);
        _talkEnd = _scheduler.ScheduleOnce(TimeSpan.FromSeconds(Typewriter.TotalSeconds(text) + 0.3), () => EndTalking(immediate: false));
    }

    private void EndTalking(bool immediate)
    {
        _talkEnd?.Dispose();
        _talkEnd = null;

        if (_animator.State != MascotState.Talking)
            return;

        var now = _clock.Now;
        if (!immediate && _talkMood is MascotState.Happy or MascotState.Smug or MascotState.Confused)
            _animator.SetState(_talkMood, now, TimeSpan.FromSeconds(1.4)); // linger on the expression for a moment
        else
            _animator.SetState(MascotState.Idle, now);

        Wake();
    }

    private void CloseBubbleInternal()
    {
        if (_bubble is null && _tourStep < 0 && !_firstRunActive)
            return;

        if (_firstRunActive)
            MarkFirstRunDone();

        _bubble = null;
        _tourStep = -1;
        _stage.CloseBubble();
        EndTalking(immediate: true);
    }

    // ---- events coming back from the bubble UI

    /// <summary>The user pressed a row in a menu bubble.</summary>
    public void OnBubbleChoice(string id)
    {
        if (!_visible)
            return;

        if (id.StartsWith("page:", StringComparison.Ordinal))
        {
            ShowMenu(id[5..]);
            return;
        }

        var colon = id.IndexOf(':');
        var key = colon < 0 ? id : id[..colon];
        var arg = colon < 0 ? string.Empty : id[(colon + 1)..];

        switch (key)
        {
            case "chat":
                CloseBubbleInternal();
                var line = _engine.PickChatter(_settings.Personality, _clock.LocalNow);
                if (line is not null)
                    SayMessage(line, SpeechPriority.User);
                break;

            case "tour":
                ShowTourStep(0);
                break;

            case "settings":
                CloseBubbleInternal();
                _stage.Execute(HostCommand.OpenSettings);
                break;

            case "quiet":
                CloseBubbleInternal();
                SetQuietMode(!_settings.QuietMode);
                break;

            case "nap":
                CloseBubbleInternal();
                Say(MessageCategory.Nap, SpeechPriority.User);
                FallAsleep(nap: true);
                break;

            case "hide":
                CloseBubbleInternal();
                _stage.Execute(HostCommand.Hide);
                break;

            case "exit":
                CloseBubbleInternal();
                _stage.Execute(HostCommand.Exit);
                break;

            case "size" when double.TryParse(arg, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var scale):
                CloseBubbleInternal();
                _stage.SetScale(scale);
                _animator.React(Reaction.Bounce, _clock.Now);
                Wake();
                break;

            case "acc" when Enum.TryParse<Accessory>(arg, out var accessory):
                CloseBubbleInternal();
                UpdateSettings(_settings with { Accessory = accessory });
                _animator.React(Reaction.Bounce, _clock.Now);
                Wake();
                break;

            case "expr" when Enum.TryParse<MascotState>(arg, out var state):
                CloseBubbleInternal();
                RequestExpression(state, TimeSpan.FromSeconds(4));
                break;
        }
    }

    /// <summary>The user pressed a button under a bubble's text.</summary>
    public void OnBubbleAction(string id)
    {
        if (!_visible)
            return;

        switch (id)
        {
            case "tour:next":
                ShowTourStep(_tourStep + 1);
                break;

            case "tour:stop":
                CloseBubbleInternal();
                break;

            case "firstrun:tour":
                MarkFirstRunDone();
                ShowTourStep(0);
                break;

            case "firstrun:quiet":
                MarkFirstRunDone();
                CloseBubbleInternal();
                SetQuietMode(true);
                break;

            case "firstrun:ok":
                MarkFirstRunDone();
                CloseBubbleInternal();
                break;
        }
    }

    /// <summary>The bubble went away without a choice: the timer ran out or the user pressed ×.</summary>
    public void OnBubbleDismissed(BubbleDismissReason reason)
    {
        if (reason is BubbleDismissReason.Replaced or BubbleDismissReason.Hidden)
            return; // we caused those ourselves

        _bubble = null;
        _tourStep = -1;
        if (_firstRunActive)
            MarkFirstRunDone();

        EndTalking(immediate: true);
    }

    // ============================================================== menu, tour, first run

    private void ShowMenu(string page) => Present(GemMenu.Build(page, _settings, _stage.Scale), MascotState.Idle);

    private void ShowTourStep(int index)
    {
        if (index >= TourSteps.Length)
        {
            CloseBubbleInternal();
            return;
        }

        var last = index == TourSteps.Length - 1;
        BubbleAction[] actions = last
            ? [new("tour:stop", "Done")]
            : [new("tour:next", "Next"), new("tour:stop", "Skip")];

        var text = TourSteps[index];
        Present(new BubbleContent(text, SpeechPriority.User, actions, AutoDismiss: BubbleTiming.DisplayTime(text, _settings.BubbleSeconds, interactive: true)), MascotState.Happy);
        _tourStep = index;
    }

    private void OnStartupTimer()
    {
        _startupTimer = null;

        if (!_settings.FirstRunDone)
            ShowFirstRun();
        else if (ChatterPolicy.CanSpeakUnprompted(_settings, _clock.LocalNow))
            Say(MessageCategory.Startup, SpeechPriority.Ambient);
    }

    private void ShowFirstRun()
    {
        const string text = "Hi! I'm Gem, your new desktop paperclip. I live here now. No rent, just good vibes.";
        BubbleAction[] actions = [new("firstrun:tour", "Show me around"), new("firstrun:quiet", "Keep it quiet"), new("firstrun:ok", "Got it")];

        if (Present(new BubbleContent(text, SpeechPriority.User, actions, AutoDismiss: TimeSpan.FromSeconds(40)), MascotState.Happy))
            _firstRunActive = true;
    }

    private void MarkFirstRunDone()
    {
        _firstRunActive = false;
        if (!_settings.FirstRunDone)
            UpdateSettings(_settings with { FirstRunDone = true });
    }

    // ============================================================== unprompted chatter

    private void ScheduleChatter()
    {
        _chatter?.Dispose();
        _chatter = null;

        if (!_visible || ChatterPolicy.NextDelay(_settings.Chatter, _random) is not { } delay)
            return;

        _chatter = _scheduler.ScheduleOnce(delay, OnChatterTick);
    }

    private void OnChatterTick()
    {
        _chatter = null;

        var now = _clock.Now;
        var longEnoughSinceLastWord = _lastSpoke is not { } last || now - last >= MinAmbientGap;

        if (_visible && !_asleep && !_grabbed && _bubble is null && longEnoughSinceLastWord
            && ChatterPolicy.CanSpeakUnprompted(_settings, _clock.LocalNow)
            && _engine.PickChatter(_settings.Personality, _clock.LocalNow) is { } message)
        {
            SayMessage(message, SpeechPriority.Ambient);
        }

        ScheduleChatter();
    }

    // ============================================================== sleeping and presence

    private void FallAsleep(bool nap)
    {
        _asleep = true;
        _napping = nap;
        _animator.SetState(MascotState.Sleeping, _clock.Now, Timeout.InfiniteTimeSpan);
        SchedulePresence();
        Wake();
    }

    private void WakeUp(bool byPoke, TimeSpan away, bool speak)
    {
        _asleep = false;
        _napping = false;

        var now = _clock.Now;
        _animator.SetState(MascotState.Idle, now);
        _animator.React(Reaction.Startle, now);
        SchedulePresence();

        if (speak)
        {
            if (byPoke)
                Say(MessageCategory.WakeUp, SpeechPriority.User);
            else
                Say(away >= LongAway ? MessageCategory.ReturnLong : MessageCategory.ReturnShort, SpeechPriority.Normal);
        }

        Wake();
    }

    private void SchedulePresence()
    {
        _presenceTimer?.Dispose();
        _presenceTimer = null;

        if (!_visible)
            return;

        // Nothing to watch: awake with sleep disabled, or napping (a nap ends only when Gem is clicked).
        var needed = _asleep ? !_napping : _settings.SleepAfterMinutes > 0;
        if (!needed)
            return;

        _presenceTimer = _scheduler.ScheduleOnce(_asleep ? PresencePollAsleep : PresencePollAwake, OnPresenceTick);
    }

    private void OnPresenceTick()
    {
        _presenceTimer = null;
        if (!_visible)
            return;

        var idle = _presence.GetIdleTime();

        if (_asleep)
        {
            if (idle > _awayPeak)
                _awayPeak = idle;

            if (!_napping && idle < TimeSpan.FromSeconds(3))
                WakeUp(byPoke: false, _awayPeak, speak: true);
        }
        else if (_settings.SleepAfterMinutes > 0 && !_grabbed && idle >= TimeSpan.FromMinutes(_settings.SleepAfterMinutes))
        {
            _awayPeak = idle;
            CloseBubbleInternal();
            FallAsleep(nap: false);
            return;
        }

        SchedulePresence();
    }
}
