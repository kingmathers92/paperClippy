namespace PaperClippy.Core.Mascot.Animation;

/// <summary>
/// Gem's animation brain: a state machine plus an "idle life" layer (blinking, looking around, breathing),
/// pointer-following eyes and one-shot reactions, all reduced to a <see cref="Pose"/> per frame.
///
/// It is a pure function of time and injected randomness: it never reads a clock and never touches a UI,
/// so it can be driven by a real timer, a fake one in tests, or not at all while the mascot is hidden.
/// <see cref="NextFrameDelay"/> tells the driver when it next needs to be asked for a frame - or that it can
/// sleep until then - which is what keeps an idle mascot essentially free.
/// </summary>
public sealed class MascotAnimator
{
    private const double BlendSeconds = 0.18;

    private readonly Random _random;
    private Profile _profile = ProfileFor(AnimationIntensity.Normal);

    private MascotState _state = MascotState.Idle;
    private MascotState _mood = MascotState.Idle;
    private double _stateStart;
    private double? _stateEnd;

    private double _blendStart = double.NegativeInfinity;
    private Pose _blendFrom = Pose.Neutral;
    private Pose _last = Pose.Neutral;
    private double _lastEval = double.NaN;

    // Idle layer.
    private double _nextBlink = double.NaN;
    private double _blinkStart = double.NegativeInfinity;
    private bool _doubleBlink;
    private double _nextLook;
    private double _lookEnd = double.NegativeInfinity;
    private double _lookStart = double.NegativeInfinity;
    private double _lookX;
    private double _lookY;
    private double _nextBreath;
    private double _breathStart = double.NegativeInfinity;

    // Pointer-following eyes.
    private bool _pointerActive;
    private double _pointerX;
    private double _pointerY;
    private double _gazeX;
    private double _gazeY;
    private double _pointerWeight;

    private double _dragVelocity;
    private readonly List<(Reaction Kind, double Start)> _reactions = [];

    public MascotAnimator(Random? random = null) => _random = random ?? new Random();

    public MascotState State => _state;

    public AnimationIntensity Intensity { get; private set; } = AnimationIntensity.Normal;

    public void SetIntensity(AnimationIntensity intensity)
    {
        Intensity = intensity;
        _profile = ProfileFor(intensity);
        _nextBlink = double.NaN; // reschedule idle events at the new pace
        _reactions.Clear();
    }

    /// <summary>Switches state. <paramref name="duration"/> null = the state's default; <see cref="Timeout.InfiniteTimeSpan"/> = until changed.</summary>
    public void SetState(MascotState state, TimeSpan now, TimeSpan? duration = null, MascotState mood = MascotState.Idle)
    {
        var t = now.TotalSeconds;
        if (state == MascotState.Idle && _state == MascotState.Idle)
            return; // already idle; re-triggering any other state restarts it (e.g. "Happy" twice)

        if (_profile.Blend)
        {
            _blendFrom = _last;
            _blendStart = t;
        }

        _state = state;
        _mood = state == MascotState.Talking ? mood : MascotState.Idle;
        _stateStart = t;

        var span = duration ?? DefaultDuration(state);
        _stateEnd = span is { } s && s != Timeout.InfiniteTimeSpan ? t + s.TotalSeconds : null;
    }

    public void ReturnToIdle(TimeSpan now) => SetState(MascotState.Idle, now);

    /// <summary>Plays a one-shot reaction on top of the current state (ignored when animation is Off).</summary>
    public void React(Reaction reaction, TimeSpan now)
    {
        if (_profile.Reactions)
            _reactions.Add((reaction, now.TotalSeconds));
    }

    /// <summary>Tells the eyes where the pointer is: -1..1 on each axis relative to Gem's face.</summary>
    public void SetPointer(double x, double y)
    {
        _pointerActive = true;
        _pointerX = Math.Clamp(x, -1, 1);
        _pointerY = Math.Clamp(y, -1, 1);
    }

    public void ClearPointer() => _pointerActive = false;

    /// <summary>Horizontal speed (pixels/second) while being dragged; makes Gem swing like it's dangling.</summary>
    public void SetDragVelocity(double pixelsPerSecond) => _dragVelocity = pixelsPerSecond;

    public Pose Evaluate(TimeSpan now)
    {
        var t = now.TotalSeconds;
        var dt = double.IsNaN(_lastEval) ? 0 : Math.Clamp(t - _lastEval, 0, 0.25);
        _lastEval = t;

        if (_stateEnd is { } end && t >= end)
            SetState(MascotState.Idle, now);

        ScheduleIdleEvents(t);

        var pose = StatePose(_state, t - _stateStart, _profile.Motion);
        pose = ApplyIdleLayer(pose, t);
        pose = ApplyPointer(pose, dt);

        for (var i = _reactions.Count - 1; i >= 0; i--)
        {
            var (kind, start) = _reactions[i];
            if (t - start >= ReactionCurves.Duration(kind))
                _reactions.RemoveAt(i);
            else
                pose = ReactionCurves.Apply(kind, t - start, _profile.Motion, pose);
        }

        if (_profile.Blend && t - _blendStart < BlendSeconds)
            pose = Pose.Lerp(_blendFrom, pose, Smooth((t - _blendStart) / BlendSeconds));

        _last = pose;
        return pose;
    }

    /// <summary>
    /// Call after <see cref="Evaluate"/>: how long until the next frame is needed. A short delay means "still
    /// animating"; a long one means "settled, wake me for the next blink/look/state change"; null means nothing
    /// is scheduled at all and the current pose can be left on screen.
    /// </summary>
    public TimeSpan? NextFrameDelay(TimeSpan now)
    {
        var t = now.TotalSeconds;

        if (IsAnimatingNow(t))
            return TimeSpan.FromSeconds(1.0 / FramesPerSecond());

        var wake = double.PositiveInfinity;
        if (_stateEnd is { } end)
            wake = Math.Min(wake, end);

        if (_profile.Idle && !double.IsNaN(_nextBlink))
            wake = Math.Min(wake, Math.Min(_nextBlink, Math.Min(_nextLook, _nextBreath)));

        return double.IsInfinity(wake) ? null : TimeSpan.FromSeconds(Math.Max(0.005, wake - t));
    }

    // ---------------------------------------------------------------- state poses

    private static TimeSpan? DefaultDuration(MascotState state) => state switch
    {
        MascotState.Happy => TimeSpan.FromSeconds(1.6),
        MascotState.Confused => TimeSpan.FromSeconds(2.6),
        MascotState.Thinking => TimeSpan.FromSeconds(3.5),
        MascotState.Celebrating => TimeSpan.FromSeconds(2.6),
        MascotState.Smug => TimeSpan.FromSeconds(2.6),
        _ => null,
    };

    private Pose StatePose(MascotState state, double t, double m)
    {
        var p = Pose.Neutral;
        switch (state)
        {
            case MascotState.Thinking:
            {
                var r = Smooth(t / 0.35);
                return p with
                {
                    GazeX = 0.55 * r, GazeY = -r, Rotation = -5 * r,
                    EyeOpenL = 1 - 0.05 * r, EyeOpenR = 1 - 0.05 * r,
                    BrowLiftR = 0.9 * r, BrowLiftL = 0.15 * r, BrowTiltR = 0.2 * r, BrowOpacity = r,
                    Mouth = MouthShape.Neutral,
                    Thought = r, ThoughtPhase = Frac(t / 1.8),
                };
            }

            case MascotState.Happy:
            {
                var r = Smooth(t / 0.15);
                var arc = Math.Abs(Math.Sin(Math.PI * t / 0.5));
                var decay = t < 1.0 ? 1 : Math.Max(0, 1 - (t - 1.0) / 0.4);
                return p with
                {
                    EyeOpenL = 1 - 0.38 * r, EyeOpenR = 1 - 0.38 * r,
                    BrowLiftL = 0.6 * r, BrowLiftR = 0.6 * r, BrowOpacity = r,
                    CheekGlow = r, Mouth = MouthShape.Grin,
                    OffsetY = -13 * arc * decay * m,
                    ScaleY = 1 - 0.05 * Math.Pow(1 - arc, 3) * decay * m,
                    Rotation = 3 * Math.Sin(2 * Math.PI * 2 * t) * decay * m,
                };
            }

            case MascotState.Confused:
            {
                var r = Smooth(t / 0.3);
                return p with
                {
                    Rotation = (9 + 2.5 * Math.Sin(2 * Math.PI * t / 1.2) * m) * r,
                    GazeX = Math.Sin(2 * Math.PI * t / 1.5) * 0.8 * Math.Min(m, 1) * r,
                    BrowLiftL = 0.9 * r, BrowTiltL = 0.45 * r, BrowLiftR = -0.5 * r, BrowTiltR = -0.3 * r, BrowOpacity = r,
                    Mouth = MouthShape.Wobbly,
                    QuestionMark = r * (0.75 + 0.25 * Math.Sin(2 * Math.PI * t / 0.9)),
                    ThoughtPhase = Frac(t / 0.9),
                };
            }

            case MascotState.Sleeping:
            {
                var r = Smooth(t / 0.8);
                var breath = Math.Sin(2 * Math.PI * t / 3.4);
                return p with
                {
                    EyeOpenL = 1 - 0.95 * r, EyeOpenR = 1 - 0.95 * r,
                    Mouth = MouthShape.Neutral, CheekGlow = 0.3 * r,
                    Rotation = 6 * r + 1.2 * breath * m,
                    ScaleY = 1 + 0.02 * breath * m, ScaleX = 1 - 0.01 * breath * m,
                    Zzz = r, ZzzPhase = Frac(t / 3.2),
                };
            }

            case MascotState.Working:
            {
                var r = Smooth(t / 0.3);
                return p with
                {
                    EyeOpenL = 1 - 0.28 * r, EyeOpenR = 1 - 0.28 * r,
                    GazeX = Math.Sin(t * 1.1) * 0.75 * Math.Min(m, 1) * r, GazeY = 0.55 * r,
                    BrowLiftL = -0.35 * r, BrowLiftR = -0.35 * r, BrowTiltL = -0.3 * r, BrowTiltR = -0.3 * r, BrowOpacity = r,
                    Mouth = MouthShape.Neutral,
                    OffsetY = 1.3 * Math.Sin(2 * Math.PI * 2.8 * t) * m * r,
                    Working = r, WorkSpin = m > 0 ? t * 110 : 0,
                };
            }

            case MascotState.Celebrating:
            {
                var r = Smooth(t / 0.15);
                var arc = Math.Abs(Math.Sin(Math.PI * t / 0.55));
                var decay = t < 2.0 ? 1 : Math.Max(0, 1 - (t - 2.0) / 0.5);
                return p with
                {
                    EyeOpenL = 1 - 0.45 * r, EyeOpenR = 1 - 0.45 * r,
                    BrowLiftL = 0.85 * r, BrowLiftR = 0.85 * r, BrowOpacity = r,
                    CheekGlow = r, Mouth = MouthShape.Grin,
                    OffsetY = -22 * arc * decay * m,
                    ScaleY = 1 - 0.10 * Math.Pow(1 - arc, 3) * decay * m,
                    Rotation = 8 * Math.Sin(2 * Math.PI * t / 1.1) * decay * m,
                    ConfettiTime = t < Confetti.Duration ? t : -1,
                };
            }

            case MascotState.Smug:
            {
                var r = Smooth(t / 0.25);
                return p with
                {
                    EyeOpenL = 1 - 0.38 * r, EyeOpenR = 1 - 0.38 * r,
                    GazeX = 0.5 * r, GazeY = 0.1 * r, Rotation = -3 * r,
                    BrowLiftR = 0.9 * r, BrowLiftL = -0.2 * r, BrowTiltL = -0.3 * r, BrowOpacity = r,
                    CheekGlow = 0.2 * r, Mouth = MouthShape.Smirk,
                };
            }

            case MascotState.Talking:
            {
                var b = _mood == MascotState.Idle ? p : StatePose(_mood, t, m);
                var open = Math.Abs(Math.Sin(2 * Math.PI * 3.7 * t)) * 0.6 + Math.Abs(Math.Sin(2 * Math.PI * 5.3 * t + 1.0)) * 0.4;
                var mouth = m <= 0 ? MouthShape.TalkSmall
                    : open < 0.22 ? (_mood == MascotState.Happy ? MouthShape.Grin : _mood == MascotState.Smug ? MouthShape.Smirk : MouthShape.Smile)
                    : open < 0.6 ? MouthShape.TalkSmall
                    : MouthShape.TalkWide;
                return b with
                {
                    Mouth = mouth,
                    OffsetY = b.OffsetY * 0.35 + 1.4 * Math.Sin(2 * Math.PI * 2.1 * t) * m,
                    Rotation = b.Rotation * 0.5 + 1.6 * Math.Sin(2 * Math.PI * 1.3 * t) * m,
                    ScaleX = 1 + (b.ScaleX - 1) * 0.35,
                    ScaleY = 1 + (b.ScaleY - 1) * 0.35,
                    ConfettiTime = -1,
                };
            }

            case MascotState.Grabbed:
                return p with
                {
                    EyeOpenL = 1.18, EyeOpenR = 1.18,
                    BrowLiftL = 0.9, BrowLiftR = 0.9, BrowOpacity = 1,
                    Mouth = MouthShape.Open,
                    OffsetY = -4,
                    ScaleY = 1.05, ScaleX = 0.96,
                    Rotation = Math.Clamp(_dragVelocity * 0.012, -25, 25) + 2 * Math.Sin(2 * Math.PI * 3 * t) * m,
                };

            default:
                return p;
        }
    }

    // ---------------------------------------------------------------- idle life

    private void ScheduleIdleEvents(double t)
    {
        if (!_profile.Idle || !double.IsNaN(_nextBlink))
            return;

        var k = _profile.EventScale;
        _nextBlink = t + Between(1.2, 3.2) * k;
        _nextLook = t + Between(3.0, 6.0) * k;
        _nextBreath = t + Between(2.0, 5.0) * k;
    }

    private Pose ApplyIdleLayer(Pose p, double t)
    {
        if (!_profile.Idle || double.IsNaN(_nextBlink))
            return p;

        var k = _profile.EventScale;
        var asleepOrGrabbed = _state is MascotState.Sleeping or MascotState.Grabbed;
        var calm = _state is MascotState.Idle or MascotState.Talking;

        if (!asleepOrGrabbed && t >= _nextBlink)
        {
            _blinkStart = t;
            _doubleBlink = _random.NextDouble() < 0.15;
            _nextBlink = t + Between(2.8, 6.5) * k;
        }
        else if (asleepOrGrabbed && t >= _nextBlink)
        {
            _nextBlink = t + Between(2.8, 6.5) * k; // keep the schedule moving without blinking
        }

        if (calm && t >= _nextLook)
        {
            _lookStart = t;
            _lookEnd = t + 1.5;
            (_lookX, _lookY) = LookTargets[_random.Next(LookTargets.Length)];
            _nextLook = t + Between(4.5, 10.0) * k;
        }
        else if (!calm && t >= _nextLook)
        {
            _nextLook = t + Between(4.5, 10.0) * k;
        }

        if (calm && t >= _nextBreath)
        {
            _breathStart = t;
            _nextBreath = t + Between(5.0, 11.0) * k;
        }
        else if (!calm && t >= _nextBreath)
        {
            _nextBreath = t + Between(5.0, 11.0) * k;
        }

        if (!asleepOrGrabbed)
        {
            var open = BlinkOpenness(t - _blinkStart);
            p = p with { EyeOpenL = p.EyeOpenL * open, EyeOpenR = p.EyeOpenR * open };
        }

        if (calm && t < _lookEnd)
        {
            var u = t - _lookStart;
            var env = Smooth(Math.Min(u / 0.25, 1)) * Smooth(Math.Min((_lookEnd - t) / 0.3, 1));
            p = p with { GazeX = p.GazeX + _lookX * env, GazeY = p.GazeY + _lookY * env, Rotation = p.Rotation + _lookX * 2.5 * env };
        }

        if (calm && t - _breathStart is >= 0 and < 2.0)
        {
            var s = Math.Sin(Math.PI * (t - _breathStart) / 2.0);
            p = p with { ScaleY = p.ScaleY * (1 + 0.018 * s * _profile.Motion), ScaleX = p.ScaleX * (1 - 0.008 * s * _profile.Motion) };
        }

        return p;
    }

    private static readonly (double X, double Y)[] LookTargets =
    [
        (-0.9, 0.0), (0.9, 0.0), (0.7, -0.8), (-0.7, -0.8), (0.0, 0.6), (-0.8, 0.4), (0.8, 0.4),
    ];

    /// <summary>1 = open, 0 = closed, for the time since a blink began.</summary>
    private double BlinkOpenness(double u)
    {
        if (u < 0)
            return 1;

        static double Single(double x) =>
            x < 0 ? 1
            : x < 0.05 ? 1 - Smooth(x / 0.05)
            : x < 0.08 ? 0
            : x < 0.17 ? Smooth((x - 0.08) / 0.09)
            : 1;

        return _doubleBlink ? Math.Min(Single(u), Single(u - 0.24)) : Single(u);
    }

    // ---------------------------------------------------------------- pointer gaze

    private Pose ApplyPointer(Pose p, double dt)
    {
        var allowed = _pointerActive && _profile.Idle
                      && _state is MascotState.Idle or MascotState.Talking or MascotState.Happy or MascotState.Working;

        var targetX = allowed ? _pointerX : 0;
        var targetY = allowed ? _pointerY : 0;
        var k = 1 - Math.Exp(-dt * 14);

        _gazeX += (targetX - _gazeX) * k;
        _gazeY += (targetY - _gazeY) * k;
        _pointerWeight += ((allowed ? 1 : 0) - _pointerWeight) * k;

        if (_pointerWeight < 0.002)
            return p;

        return p with
        {
            GazeX = p.GazeX + (_gazeX - p.GazeX) * _pointerWeight,
            GazeY = p.GazeY + (_gazeY - p.GazeY) * _pointerWeight,
        };
    }

    private bool PointerSettling()
    {
        var allowed = _pointerActive && _profile.Idle
                      && _state is MascotState.Idle or MascotState.Talking or MascotState.Happy or MascotState.Working;
        var tx = allowed ? _pointerX : 0;
        var ty = allowed ? _pointerY : 0;
        var tw = allowed ? 1 : 0;
        return Math.Abs(_gazeX - tx) > 0.01 || Math.Abs(_gazeY - ty) > 0.01 || Math.Abs(_pointerWeight - tw) > 0.01;
    }

    // ---------------------------------------------------------------- frame pacing

    private bool IsAnimatingNow(double t)
    {
        if (_profile.Blend && t - _blendStart < BlendSeconds)
            return true;

        if (_reactions.Count > 0)
            return true;

        if (_profile.Motion > 0 && _state != MascotState.Idle)
            return true;

        if (!_profile.Idle)
            return false;

        if (PointerSettling())
            return true;

        return t - _blinkStart < (_doubleBlink ? 0.44 : 0.17)
               || t < _lookEnd
               || t - _breathStart < 2.0;
    }

    private double FramesPerSecond() => _state switch
    {
        MascotState.Sleeping => Math.Min(_profile.Fps, 10),
        MascotState.Working => Math.Min(_profile.Fps, 24),
        _ => _profile.Fps,
    };

    // ---------------------------------------------------------------- helpers

    private readonly record struct Profile(double Fps, double Motion, double EventScale, bool Idle, bool Reactions, bool Blend);

    private static Profile ProfileFor(AnimationIntensity intensity) => intensity switch
    {
        AnimationIntensity.Off => new Profile(1, 0, 1, false, false, false),
        AnimationIntensity.Low => new Profile(15, 0.6, 1.6, true, true, true),
        AnimationIntensity.High => new Profile(40, 1.25, 0.75, true, true, true),
        _ => new Profile(30, 1, 1, true, true, true),
    };

    private double Between(double min, double max) => min + _random.NextDouble() * (max - min);

    private static double Smooth(double x)
    {
        x = Math.Clamp(x, 0, 1);
        return x * x * (3 - 2 * x);
    }

    private static double Frac(double x) => x - Math.Floor(x);
}
