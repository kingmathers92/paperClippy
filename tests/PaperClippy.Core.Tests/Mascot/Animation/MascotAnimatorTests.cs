using PaperClippy.Core.Mascot;
using PaperClippy.Core.Mascot.Animation;
using Xunit;

namespace PaperClippy.Core.Tests.Mascot.Animation;

public class MascotAnimatorTests
{
    private static TimeSpan S(double seconds) => TimeSpan.FromSeconds(seconds);

    private static MascotAnimator Create(int seed = 7, AnimationIntensity intensity = AnimationIntensity.Normal)
    {
        var animator = new MascotAnimator(new Random(seed));
        animator.SetIntensity(intensity);
        return animator;
    }

    /// <summary>Evaluates at 30 fps for <paramref name="seconds"/> and returns every pose.</summary>
    private static List<Pose> Run(MascotAnimator animator, double from, double seconds)
    {
        var poses = new List<Pose>();
        for (var t = from; t <= from + seconds; t += 1.0 / 30)
            poses.Add(animator.Evaluate(S(t)));
        return poses;
    }

    [Fact]
    public void IdleGem_Blinks()
    {
        var poses = Run(Create(), 0, 40);
        var blinks = 0;
        for (var i = 1; i < poses.Count; i++)
            if (poses[i].EyeOpenL < 0.15 && poses[i - 1].EyeOpenL >= 0.15)
                blinks++;

        Assert.True(blinks >= 4, $"expected several blinks in 40s, saw {blinks}");
        Assert.True(poses[^1].EyeOpenL > 0.9 || poses.Skip(poses.Count - 30).Any(p => p.EyeOpenL > 0.9), "eyes reopen after blinking");
    }

    [Fact]
    public void IdleGem_LooksAround()
    {
        var poses = Run(Create(), 0, 60);
        Assert.True(poses.Max(p => Math.Abs(p.GazeX)) > 0.5, "Gem should glance to the side now and then");
    }

    [Fact]
    public void Sleeping_ClosesTheEyes_AndShowsZzz()
    {
        var animator = Create();
        animator.SetState(MascotState.Sleeping, S(0));
        var p = Run(animator, 0, 3).Last();

        Assert.True(p.EyeOpenL < 0.2 && p.EyeOpenR < 0.2, "eyes closed");
        Assert.True(p.Zzz > 0.9, "Zzz visible");
        Assert.True(p.ZzzPhase is >= 0 and < 1, "Zzz phase is a 0..1 loop");
        Assert.True(p.Rotation > 4, "head droops");
    }

    [Fact]
    public void Sleeping_RunsAtALowFrameRate()
    {
        var animator = Create();
        animator.SetState(MascotState.Sleeping, S(0));
        animator.Evaluate(S(1));
        var delay = animator.NextFrameDelay(S(1));
        Assert.True(delay is { } d && d >= S(0.099), "sleeping animation should not burn CPU: <= 10 fps");
    }

    [Fact]
    public void Celebrating_ThrowsConfetti_ThenReturnsToIdle()
    {
        var animator = Create();
        animator.SetState(MascotState.Celebrating, S(0));

        var early = animator.Evaluate(S(0.5));
        Assert.True(early.ConfettiTime is > 0.45 and < 0.55, "confetti clock follows the state clock");
        Assert.True(Run(animator, 0.5, 1).Min(p => p.OffsetY) < -10, "bounces up");

        var after = animator.Evaluate(S(3.5));
        Assert.Equal(MascotState.Idle, animator.State);
        Assert.True(after.ConfettiTime < 0, "confetti is gone");
    }

    [Fact]
    public void TimedStates_ReturnToIdleOnTheirOwn()
    {
        foreach (var state in new[] { MascotState.Happy, MascotState.Confused, MascotState.Thinking, MascotState.Celebrating, MascotState.Smug })
        {
            var animator = Create();
            animator.SetState(state, S(0));
            animator.Evaluate(S(0.1));
            Assert.Equal(state, animator.State);

            animator.Evaluate(S(10));
            Assert.Equal(MascotState.Idle, animator.State);
        }
    }

    [Fact]
    public void ContinuousStates_StayUntilChanged()
    {
        foreach (var state in new[] { MascotState.Sleeping, MascotState.Working, MascotState.Talking, MascotState.Grabbed })
        {
            var animator = Create();
            animator.SetState(state, S(0));
            animator.Evaluate(S(600));
            Assert.Equal(state, animator.State);
        }
    }

    [Fact]
    public void Thinking_LooksUp_AndShowsAThought()
    {
        var animator = Create();
        animator.SetState(MascotState.Thinking, S(0));
        var p = Run(animator, 0, 1.2).Last();

        Assert.True(p.GazeY < -0.8, "eyes up");
        Assert.True(p.Thought > 0.9, "thought bubble visible");
        Assert.True(p.Rotation < -3, "head tilts back");
    }

    [Fact]
    public void Confused_TiltsItsHead_AndShowsAQuestionMark()
    {
        var animator = Create();
        animator.SetState(MascotState.Confused, S(0));
        var poses = Run(animator, 0, 1.5);

        Assert.True(poses.Max(p => p.QuestionMark) > 0.6, "question mark");
        Assert.True(poses.Last().Rotation > 5, "head tilt");
        Assert.Equal(MouthShape.Wobbly, poses.Last().Mouth);
        Assert.True(poses.Last().BrowLiftL > 0.5 && poses.Last().BrowLiftR < 0, "one brow up, one down");
    }

    [Fact]
    public void Working_ShowsAFocusedFace_AndASpinningIndicator()
    {
        var animator = Create();
        animator.SetState(MascotState.Working, S(0));
        var early = Run(animator, 0, 1).Last();
        var later = Run(animator, 1.1, 1).Last();

        Assert.True(early.Working > 0.9, "indicator visible");
        Assert.True(later.WorkSpin > early.WorkSpin, "indicator spins");
        Assert.True(early.EyeOpenL < 0.8, "narrowed, focused eyes");
    }

    [Fact]
    public void Grabbed_WidensTheEyes_AndSwingsWithDragSpeed()
    {
        var still = Create();
        still.SetState(MascotState.Grabbed, S(0));
        var calm = Run(still, 0, 0.5).Last();

        var fast = Create();
        fast.SetState(MascotState.Grabbed, S(0));
        fast.SetDragVelocity(1500);
        var swinging = Run(fast, 0, 0.5).Last();

        Assert.True(calm.EyeOpenL > 1.05, "wide-eyed");
        Assert.Equal(MouthShape.Open, calm.Mouth);
        Assert.True(swinging.Rotation > calm.Rotation + 8, "tilts in the direction of travel");
    }

    [Fact]
    public void Smug_LooksSarcastic()
    {
        var animator = Create();
        animator.SetState(MascotState.Smug, S(0));
        var p = Run(animator, 0, 1).Last();

        Assert.Equal(MouthShape.Smirk, p.Mouth);
        Assert.True(p.EyeOpenL < 0.75, "half-lidded");
        Assert.True(p.BrowLiftR > 0.6 && p.BrowLiftL < 0.1, "one eyebrow raised");
        Assert.True(p.BrowOpacity > 0.9);
    }

    [Fact]
    public void Talking_AsSmug_KeepsTheSmirkBetweenSyllables()
    {
        var animator = Create();
        animator.SetState(MascotState.Talking, S(0), mood: MascotState.Smug);
        var shapes = Run(animator, 0.3, 3).Select(p => p.Mouth).Distinct().ToList();

        Assert.True(shapes.Contains(MouthShape.Smirk) && shapes.Contains(MouthShape.TalkWide));
        Assert.False(shapes.Contains(MouthShape.Smile), "a sarcastic Gem never gives a sincere smile mid-sentence");
    }

    [Fact]
    public void Talking_FlapsTheMouth()
    {
        var animator = Create();
        animator.SetState(MascotState.Talking, S(0));
        var shapes = Run(animator, 0.3, 2).Select(p => p.Mouth).Distinct().ToList();

        Assert.True(shapes.Count >= 3, $"expected several mouth shapes, saw {string.Join(",", shapes)}");
        Assert.True(shapes.Contains(MouthShape.TalkWide) && shapes.Contains(MouthShape.TalkSmall));
    }

    [Fact]
    public void Talking_CanBorrowAMoodsFace()
    {
        var animator = Create();
        animator.SetState(MascotState.Talking, S(0), mood: MascotState.Happy);
        var p = Run(animator, 0, 1).Last();

        Assert.True(p.BrowOpacity > 0.5 && p.CheekGlow > 0.5, "talking happily keeps the happy cheeks and brows");
    }

    [Fact]
    public void OffIntensity_IsPerfectlyStill_AndSchedulesNothing()
    {
        var animator = Create(intensity: AnimationIntensity.Off);
        var poses = Run(animator, 0, 30);

        Assert.True(poses.All(p => p == Pose.Neutral), "no blinking, looking or breathing");
        Assert.Null(animator.NextFrameDelay(S(30)));
    }

    [Fact]
    public void OffIntensity_StillShowsExpressions_WithoutMotion()
    {
        var animator = Create(intensity: AnimationIntensity.Off);
        animator.SetState(MascotState.Happy, S(0));
        var poses = Run(animator, 0.5, 0.5);

        Assert.True(poses.All(p => p.Mouth == MouthShape.Grin), "expression applied");
        Assert.True(poses.All(p => p.OffsetY == 0 && p.Rotation == 0), "but nothing moves");

        animator.Evaluate(S(1.0));
        var delay = animator.NextFrameDelay(S(1.0));
        Assert.True(delay is { } d && d > S(0.3), "waits for the state to end instead of ticking frames");
    }

    [Fact]
    public void SettledIdle_AsksForFarFewerFramesThanAContinuousLoop()
    {
        var animator = Create();
        double t = 0;
        int frames = 0, longGaps = 0;
        while (t < 60)
        {
            animator.Evaluate(S(t));
            frames++;
            var delay = animator.NextFrameDelay(S(t));
            Assert.NotNull(delay);
            if (delay!.Value > S(0.5))
                longGaps++;
            t += delay.Value.TotalSeconds;
        }

        Assert.True(frames < 60 * 30 * 0.75, $"a continuous 30fps loop would be 1800 frames; got {frames}");
        Assert.True(longGaps >= 8, $"expected many quiet stretches where nothing is scheduled, saw {longGaps}");
    }

    [Fact]
    public void LowIntensity_UsesFewerFramesThanHigh()
    {
        static int Frames(AnimationIntensity intensity)
        {
            var animator = Create(intensity: intensity);
            double t = 0;
            var frames = 0;
            while (t < 120)
            {
                animator.Evaluate(S(t));
                frames++;
                t += animator.NextFrameDelay(S(t))!.Value.TotalSeconds;
            }
            return frames;
        }

        Assert.True(Frames(AnimationIntensity.Low) < Frames(AnimationIntensity.High));
    }

    [Fact]
    public void Eyes_FollowThePointer_AndReturnWhenItLeaves()
    {
        var animator = Create();
        animator.SetState(MascotState.Happy, S(0), Timeout.InfiniteTimeSpan); // a state with no random glancing
        Run(animator, 0, 0.5);

        animator.SetPointer(1, -0.5);
        var following = Run(animator, 0.5, 1).Last();
        Assert.True(following.GazeX > 0.9 && following.GazeY < -0.4, "pupils track the cursor");

        animator.ClearPointer();
        var released = Run(animator, 1.6, 1).Last();
        Assert.True(Math.Abs(released.GazeX) < 0.1 && Math.Abs(released.GazeY) < 0.1, "and recentre afterwards");
    }

    [Fact]
    public void Eyes_IgnoreThePointer_WhileAsleep()
    {
        var animator = Create();
        animator.SetState(MascotState.Sleeping, S(0));
        animator.SetPointer(1, 1);
        var p = Run(animator, 0, 2).Last();
        Assert.True(Math.Abs(p.GazeX) < 0.05);
    }

    [Fact]
    public void Bounce_LiftsGem_ThenFinishes()
    {
        var animator = Create(intensity: AnimationIntensity.Normal);
        animator.SetState(MascotState.Happy, S(0), Timeout.InfiniteTimeSpan);
        Run(animator, 0, 3); // let the happy bounce itself die down
        animator.React(Reaction.Bounce, S(3));

        var during = Run(animator, 3, 0.62);
        Assert.True(during.Min(p => p.OffsetY) < -10, "jumps");

        var after = animator.Evaluate(S(4));
        Assert.True(Math.Abs(after.OffsetY) < 0.5, "lands");
    }

    [Fact]
    public void Wiggle_SwingsBothWays_AndStartleWidensTheEyes()
    {
        var wiggle = Create();
        wiggle.SetState(MascotState.Happy, S(0), Timeout.InfiniteTimeSpan);
        Run(wiggle, 0, 3);
        wiggle.React(Reaction.Wiggle, S(3));
        var rotations = Run(wiggle, 3, 0.9).Select(p => p.Rotation).ToList();
        Assert.True(rotations.Max() > 3 && rotations.Min() < -3, "oscillates");

        var startle = Create();
        startle.SetState(MascotState.Happy, S(0), Timeout.InfiniteTimeSpan);
        Run(startle, 0, 3);
        startle.React(Reaction.Startle, S(3));
        Assert.True(Run(startle, 3, 0.5).Max(p => p.EyeOpenL) > 0.85, "eyes pop wider than the happy squint (0.62)");
    }

    [Fact]
    public void ReactionsAreIgnored_WhenAnimationIsOff()
    {
        var animator = Create(intensity: AnimationIntensity.Off);
        animator.React(Reaction.Bounce, S(0));
        Assert.True(Run(animator, 0, 1).All(p => p.OffsetY == 0));
    }

    [Fact]
    public void ChangingState_BlendsInsteadOfPopping()
    {
        var animator = Create();
        Run(animator, 0, 0.1);
        animator.SetState(MascotState.Sleeping, S(0.1), Timeout.InfiniteTimeSpan);
        var mid = animator.Evaluate(S(0.15));

        Assert.True(mid.EyeOpenL is > 0.2 and < 1.0, $"half-way between awake and asleep, got {mid.EyeOpenL}");
    }

    [Fact]
    public void EveryState_StaysWithinSaneBounds()
    {
        foreach (var state in Enum.GetValues<MascotState>())
        {
            var animator = Create();
            animator.SetPointer(0.4, 0.2);
            animator.SetDragVelocity(900);
            animator.SetState(state, S(0), Timeout.InfiniteTimeSpan, state == MascotState.Talking ? MascotState.Smug : MascotState.Idle);

            foreach (var p in Run(animator, 0, 20))
            {
                var label = $"{state}";
                Assert.True(p.EyeOpenL is >= 0 and <= 1.5 && p.EyeOpenR is >= 0 and <= 1.5, label + " eyes");
                Assert.True(Math.Abs(p.GazeX) <= 1.3 && Math.Abs(p.GazeY) <= 1.3, label + " gaze");
                Assert.True(Math.Abs(p.Rotation) <= 30, label + " rotation");
                Assert.True(Math.Abs(p.OffsetY) <= 35 && Math.Abs(p.OffsetX) <= 35, label + " offset");
                Assert.True(p.ScaleX is > 0.85 and < 1.15 && p.ScaleY is > 0.85 and < 1.15, label + " scale");
                Assert.True(p.Zzz is >= 0 and <= 1 && p.BrowOpacity is >= 0 and <= 1.01 && p.QuestionMark is >= 0 and <= 1.01, label + " overlays");
            }
        }
    }

    [Fact]
    public void SameSeed_GivesTheSameAnimation()
    {
        var a = Run(Create(seed: 99), 0, 20);
        var b = Run(Create(seed: 99), 0, 20);
        Assert.True(a.SequenceEqual(b));

        var c = Run(Create(seed: 100), 0, 20);
        Assert.False(a.SequenceEqual(c));
    }
}

public class ConfettiTests
{
    [Fact]
    public void Particles_StartNearTheOrigin_AndFallUnderGravity()
    {
        for (var i = 0; i < Confetti.Count; i++)
        {
            var start = Confetti.Evaluate(i, 0);
            Assert.True(Math.Abs(start.X - Confetti.OriginX) <= 16 && Math.Abs(start.Y - Confetti.OriginY) < 1, "starts at the burst origin");
            Assert.True(Confetti.Evaluate(i, 2.0).Y > Confetti.Evaluate(i, 0.5).Y - 200, "never flies off forever");
        }

        for (var i = 0; i < Confetti.Count; i++)
            Assert.True(Confetti.Evaluate(i, 0.15).Y < Confetti.OriginY, $"particle {i} shoots upward first");
    }

    [Fact]
    public void Particles_AreDeterministic_AndFadeOut()
    {
        Assert.Equal(Confetti.Evaluate(5, 1.1), Confetti.Evaluate(5, 1.1));
        Assert.Equal(1.0, Confetti.Evaluate(5, 0.5).Opacity);
        Assert.True(Confetti.Evaluate(5, Confetti.Duration).Opacity < 1e-9, "fully faded at the end");
    }

    [Fact]
    public void Particles_UseAllColours_AndVarySize()
    {
        var all = Enumerable.Range(0, Confetti.Count).Select(i => Confetti.Evaluate(i, 0.3)).ToList();
        Assert.Equal(Confetti.ColorCount, all.Select(p => p.ColorIndex).Distinct().Count());
        Assert.True(all.Max(p => p.Size) - all.Min(p => p.Size) > 1.5);
    }
}
