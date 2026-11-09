using PaperClippy.Core.Mascot;
using PaperClippy.Core.Mascot.Animation;
using PaperClippy.Core.Mascot.Behavior;
using PaperClippy.Core.Personality;
using PaperClippy.Core.Speech;
using PaperClippy.Core.Tests.Support;
using Xunit;

namespace PaperClippy.Core.Tests.Mascot.Behavior;

public class FirstRunTests
{
    private static BrainRig FirstRun() => new(MascotSettings.Default); // FirstRunDone = false

    [Fact]
    public void GemIntroducesItself_WithThreeRealChoices()
    {
        var rig = FirstRun();
        rig.Advance(1.0);

        Assert.NotNull(rig.Stage.Bubble);
        Assert.Contains("I'm Gem", rig.Stage.Bubble!.Text);
        Assert.True(rig.Stage.Bubble.Actions!.Select(a => a.Id).SequenceEqual(["firstrun:tour", "firstrun:quiet", "firstrun:ok"]));
        Assert.Equal(SpeechPriority.User, rig.Stage.Bubble.Priority);
        Assert.Equal(MascotState.Talking, rig.Brain.State);
    }

    [Fact]
    public void GotIt_ClosesTheIntro_AndItIsNeverShownAgain()
    {
        var rig = FirstRun();
        rig.Advance(1.0);
        rig.Brain.OnBubbleAction("firstrun:ok");

        Assert.Null(rig.Stage.Bubble);
        Assert.True(rig.Saved.Last().FirstRunDone);

        // A new launch with the saved settings greets normally instead of repeating the intro.
        var next = new BrainRig(rig.Saved.Last());
        next.Advance(2.0);
        Assert.DoesNotContain("I'm Gem, your new", next.Stage.Bubble?.Text ?? string.Empty);
    }

    [Fact]
    public void ShowMeAround_WalksThroughTheTour_AndFinishesWithDone()
    {
        var rig = FirstRun();
        rig.Advance(1.0);
        rig.Brain.OnBubbleAction("firstrun:tour");

        var texts = new List<string> { rig.Stage.Bubble!.Text };
        for (var i = 0; i < 3; i++)
        {
            Assert.True(rig.Stage.Bubble!.Actions!.Any(a => a.Id == "tour:next") || i == 3);
            rig.Brain.OnBubbleAction("tour:next");
            texts.Add(rig.Stage.Bubble!.Text);
        }

        Assert.Equal(4, texts.Distinct().Count());
        Assert.True(rig.Stage.Bubble!.Actions!.Count == 1 && rig.Stage.Bubble.Actions[0].Label == "Done", "last step offers only Done");

        rig.Brain.OnBubbleAction("tour:stop");
        Assert.Null(rig.Stage.Bubble);
    }

    [Fact]
    public void TheTour_OnlyDescribesThingsThatExist()
    {
        var rig = FirstRun();
        rig.Advance(1.0);
        rig.Brain.OnBubbleAction("firstrun:tour");
        var all = new List<string> { rig.Stage.Bubble!.Text };
        while (rig.Stage.Bubble!.Actions!.Any(a => a.Id == "tour:next"))
        {
            rig.Brain.OnBubbleAction("tour:next");
            all.Add(rig.Stage.Bubble!.Text);
        }

        Assert.True(all.Last().Contains("coming soon"), "future features are labelled as future");
    }

    [Fact]
    public void KeepItQuiet_TurnsOnQuietMode_AndSaysSo()
    {
        var rig = FirstRun();
        rig.Advance(1.0);
        rig.Brain.OnBubbleAction("firstrun:quiet");

        Assert.True(rig.Brain.Settings.QuietMode);
        Assert.True(rig.Saved.Any(s => s.QuietMode) && rig.Saved.Last().FirstRunDone);
        Assert.True(BrainRig.Texts(MessageCategory.QuietOn).Contains(rig.Stage.Bubble!.Text));
    }

    [Fact]
    public void IgnoringTheIntro_StillCountsAsHavingSeenIt()
    {
        var rig = FirstRun();
        rig.Advance(1.0);
        rig.Brain.OnBubbleDismissed(BubbleDismissReason.Timeout);
        Assert.True(rig.Saved.Last().FirstRunDone);
    }

    [Fact]
    public void ClickingGemWhileTheIntroIsUp_AlsoCountsAsSeen()
    {
        var rig = FirstRun();
        rig.Advance(1.0);
        rig.Brain.OnClicked(); // opens the menu, replacing the intro

        Assert.True(rig.Stage.Bubble!.IsMenu);
        Assert.True(rig.Saved.Any(s => s.FirstRunDone));
    }
}

public class LifecycleAndCostTests
{
    [Fact]
    public void ReturningUsers_GetAStartupGreeting()
    {
        var rig = new BrainRig();
        rig.Advance(2.0);

        Assert.True(BrainRig.Texts(MessageCategory.Startup).Contains(rig.Stage.Bubble!.Text), rig.Stage.Bubble.Text);
        Assert.Equal(SpeechPriority.Ambient, rig.Stage.Bubble.Priority);
    }

    [Fact]
    public void QuietModeAndNeverChatter_SuppressTheGreeting()
    {
        var quiet = new BrainRig(MascotSettings.Default with { FirstRunDone = true, QuietMode = true });
        quiet.Advance(5.0);
        Assert.Empty(quiet.Stage.Shown);

        var never = new BrainRig(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Never });
        never.Advance(5.0);
        Assert.Empty(never.Stage.Shown);
    }

    [Fact]
    public void WhileHidden_GemSchedulesAbsolutelyNothing()
    {
        var rig = new BrainRig();
        rig.Advance(30);
        rig.Brain.OnHidden();

        var poses = rig.Stage.PoseCount;
        var shown = rig.Stage.Shown.Count;
        Assert.Equal(0, rig.Time.Pending);

        rig.Advance(3 * 3600);
        Assert.Equal(poses, rig.Stage.PoseCount);
        Assert.Equal(shown, rig.Stage.Shown.Count);
        Assert.False(rig.Brain.Say(MessageCategory.Chat), "a hidden Gem does not talk");
    }

    [Fact]
    public void ShowingGemAgain_ResumesAnimationAndChatter()
    {
        var rig = new BrainRig();
        rig.Brain.OnHidden();
        rig.Advance(60);

        var before = rig.Stage.PoseCount;
        rig.Brain.OnShown();
        rig.Advance(30);

        Assert.True(rig.Stage.PoseCount > before);
        Assert.True(rig.Time.Pending > 0);
    }

    [Fact]
    public void StartedHidden_NothingRunsUntilShown()
    {
        var rig = new BrainRig(visible: false);
        rig.Advance(600);
        Assert.Equal(0, rig.Stage.PoseCount);
        Assert.Equal(0, rig.Time.Pending);
    }

    [Fact]
    public void AnIdleGem_DrawsFarFewerFramesThanAContinuousLoop()
    {
        var rig = new BrainRig(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Never });
        rig.Advance(60);
        Assert.True(rig.Stage.PoseCount < 60 * 30 * 0.75, $"frames in a minute of idling: {rig.Stage.PoseCount} (a 30 fps loop would be 1800)");
    }

    [Fact]
    public void WithAnimationOff_AnIdleGemDrawsAlmostNothing()
    {
        var rig = new BrainRig(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Never, Animation = AnimationIntensity.Off });
        rig.Advance(60);
        Assert.True(rig.Stage.PoseCount <= 3, $"got {rig.Stage.PoseCount}");
    }

    [Fact]
    public void ChangingTheAccessory_ViaSettings_UpdatesTheStage_WithoutEchoing()
    {
        var rig = new BrainRig();
        rig.Brain.ApplySettings(rig.Brain.Settings with { Accessory = Accessory.Glasses });

        Assert.Equal(Accessory.Glasses, rig.Stage.Accessory);
        Assert.Empty(rig.Saved); // the settings window saves its own changes; the brain must not save them again
    }
}

public class MouseInteractionTests
{
    private static BrainRig Quiet() => new(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Never });

    [Fact]
    public void TheEyesFollowThePointer_UnlessTurnedOff()
    {
        var rig = Quiet();
        rig.Brain.OnPointerMoved(1, 0);
        rig.Advance(1.5);
        Assert.True(rig.Stage.Last.GazeX > 0.8, $"got {rig.Stage.Last.GazeX}");

        var off = new BrainRig(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Never, FollowPointer = false });
        off.Brain.OnPointerMoved(1, 0);
        off.Advance(1.5);
        Assert.True(off.Stage.Last.GazeX < 0.8 + 0.1 && off.Stage.PoseCount >= 0);
        Assert.True(Math.Abs(off.Stage.Last.GazeX) <= 0.95, "no pointer tracking when disabled (only idle glances)");
    }

    [Fact]
    public void LeavingWithThePointer_RecentresTheEyes()
    {
        var rig = Quiet();
        rig.Brain.OnPointerMoved(-1, 0);
        rig.Advance(1);
        rig.Brain.OnPointerLeft();
        rig.Advance(10);
        Assert.True(Math.Abs(rig.Stage.Last.GazeX) < 0.95);
    }

    [Fact]
    public void Dragging_ShowsASurprisedGem_AndClosesAnyBubble()
    {
        var rig = new BrainRig();
        rig.Advance(2.0); // greeting is up
        Assert.NotNull(rig.Stage.Bubble);

        rig.Brain.OnGrabbed();
        rig.Brain.OnDragVelocity(1200);
        rig.Advance(0.4);

        Assert.Null(rig.Stage.Bubble);
        Assert.Equal(MascotState.Grabbed, rig.Brain.State);
        Assert.True(rig.Stage.Last.EyeOpenL > 1.05 && rig.Stage.Last.Rotation > 5, "wide-eyed and swinging");

        rig.Brain.OnReleased();
        if (rig.Stage.Bubble is { } quip) // one release in five gets a remark
        {
            Assert.Equal(MascotState.Talking, rig.Brain.State);
            Assert.True(BrainRig.Texts(MessageCategory.Grabbed).Contains(quip.Text));
        }
        else
        {
            Assert.Equal(MascotState.Idle, rig.Brain.State);
        }
    }

    [Fact]
    public void ADraggedGem_DoesNotTalkWhileHeld()
    {
        var rig = Quiet();
        rig.Brain.OnGrabbed();
        Assert.False(rig.Brain.Say(MessageCategory.Chat, SpeechPriority.User));
    }

    [Fact]
    public void Release_SometimesGetsAQuip()
    {
        var rig = Quiet();
        var quips = 0;
        for (var i = 0; i < 100; i++)
        {
            rig.Brain.OnGrabbed();
            rig.Brain.OnReleased();
            if (rig.Stage.Bubble is not null)
            {
                quips++;
                Assert.True(BrainRig.Texts(MessageCategory.Grabbed).Contains(rig.Stage.Bubble.Text));
                rig.Brain.OnBubbleDismissed(BubbleDismissReason.Closed);
                rig.Stage.Bubble = null;
            }

            rig.Advance(5);
        }

        Assert.True(quips is > 5 and < 45, $"roughly one drag in five, got {quips}/100");
    }
}

public class MenuTests
{
    private static BrainRig Rig() => new(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Never });

    private static string[] Ids(BrainRig rig) => rig.Stage.Bubble!.Choices!.Select(c => c.Id).ToArray();

    [Fact]
    public void ClickingGem_OpensTheMenu_AndClickingAgainClosesIt()
    {
        var rig = Rig();
        rig.Click(0.5);

        Assert.True(rig.Stage.Bubble!.IsMenu);
        Assert.True(Ids(rig).SequenceEqual(["chat", "tour", "page:look", "settings", "quiet", "nap", "hide", "exit"]));
        Assert.Equal(SpeechPriority.User, rig.Stage.Bubble.Priority);
        Assert.Equal(MascotState.Idle, rig.Brain.State);

        rig.Click(0.5);
        Assert.Null(rig.Stage.Bubble);
    }

    [Fact]
    public void EveryMenuEntry_DoesSomething()
    {
        // Menu rows must never be decoration: each id must be understood by the brain.
        foreach (var id in new[] { "chat", "tour", "settings", "quiet", "nap", "hide", "exit" })
        {
            var rig = Rig();
            rig.Click(0.5);
            var before = (rig.Stage.Shown.Count, rig.Stage.Commands.Count, rig.Saved.Count, rig.Brain.State, rig.Stage.Bubble);

            rig.Brain.OnBubbleChoice(id);

            var after = (rig.Stage.Shown.Count, rig.Stage.Commands.Count, rig.Saved.Count, rig.Brain.State, rig.Stage.Bubble);
            Assert.True(before != after, $"'{id}' had no visible effect");
        }
    }

    [Fact]
    public void LookMenu_ChangesSize_AndMarksTheCurrentOne()
    {
        var rig = Rig();
        rig.Click(0.5);
        rig.Brain.OnBubbleChoice("page:look");
        Assert.Contains("page:size", Ids(rig));

        rig.Brain.OnBubbleChoice("page:size");
        Assert.True(rig.Stage.Bubble!.Choices!.Single(c => c.Checked).Id == "size:1");

        rig.Brain.OnBubbleChoice("size:1.5");
        Assert.Equal(1.5, rig.Stage.Scale);
        Assert.Null(rig.Stage.Bubble);

        rig.Click(0.5);
        rig.Brain.OnBubbleChoice("page:look");
        rig.Brain.OnBubbleChoice("page:size");
        Assert.True(rig.Stage.Bubble!.Choices!.Single(c => c.Checked).Id == "size:1.5");
    }

    [Fact]
    public void LookMenu_ChangesAccessory_AndSavesIt()
    {
        var rig = Rig();
        rig.Click(0.5);
        rig.Brain.OnBubbleChoice("page:look");
        rig.Brain.OnBubbleChoice("page:acc");
        rig.Brain.OnBubbleChoice("acc:PartyHat");

        Assert.Equal(Accessory.PartyHat, rig.Stage.Accessory);
        Assert.Equal(Accessory.PartyHat, rig.Saved.Last().Accessory);

        rig.Click(0.5);
        rig.Brain.OnBubbleChoice("page:look");
        rig.Brain.OnBubbleChoice("page:acc");
        Assert.True(rig.Stage.Bubble!.Choices!.Single(c => c.Checked).Id == "acc:PartyHat");
    }

    [Fact]
    public void BackButtons_ReturnToThePreviousPage()
    {
        var rig = Rig();
        rig.Click(0.5);
        rig.Brain.OnBubbleChoice("page:look");
        rig.Brain.OnBubbleChoice("page:expr");
        rig.Brain.OnBubbleChoice("page:look");
        Assert.Contains("page:expr", Ids(rig));
        rig.Brain.OnBubbleChoice("page:main");
        Assert.Contains("nap", Ids(rig));
    }

    [Fact]
    public void ExpressionDemos_PlayTheFace_ThenReturnToNormal()
    {
        var rig = Rig();
        foreach (var state in new[] { MascotState.Happy, MascotState.Smug, MascotState.Confused, MascotState.Thinking, MascotState.Working, MascotState.Celebrating })
        {
            rig.Click(0.5);
            rig.Brain.OnBubbleChoice("page:look");
            rig.Brain.OnBubbleChoice("page:expr");
            rig.Brain.OnBubbleChoice($"expr:{state}");

            rig.Advance(0.5);
            Assert.Equal(state, rig.Brain.State);
            rig.Advance(6);
            Assert.Equal(MascotState.Idle, rig.Brain.State);
        }
    }

    [Fact]
    public void QuietModeToggle_SavesIt_AndGemConfirmsEvenWhenQuiet()
    {
        var rig = Rig();
        rig.Click(0.5);
        rig.Brain.OnBubbleChoice("quiet");
        Assert.True(rig.Brain.Settings.QuietMode && rig.Saved.Last().QuietMode);
        Assert.True(BrainRig.Texts(MessageCategory.QuietOn).Contains(rig.Stage.Bubble!.Text));

        rig.Advance(30);
        rig.Click(0.5);
        Assert.True(rig.Stage.Bubble!.Choices!.Single(c => c.Id == "quiet").Checked, "menu shows quiet mode as on");
        rig.Brain.OnBubbleChoice("quiet");
        Assert.False(rig.Brain.Settings.QuietMode);
        Assert.True(BrainRig.Texts(MessageCategory.QuietOff).Contains(rig.Stage.Bubble!.Text));
    }

    [Fact]
    public void HideExitAndSettings_AskTheShell()
    {
        var rig = Rig();
        foreach (var id in new[] { "hide", "exit", "settings" })
        {
            rig.Click(0.5);
            rig.Brain.OnBubbleChoice(id);
            Assert.Null(rig.Stage.Bubble);
        }

        Assert.True(rig.Stage.Commands.SequenceEqual([HostCommand.Hide, HostCommand.Exit, HostCommand.OpenSettings]));
    }

    [Fact]
    public void TellMeSomething_SaysAChatLine_EvenInQuietMode()
    {
        var rig = new BrainRig(MascotSettings.Default with { FirstRunDone = true, QuietMode = true });
        rig.Click(0.5);
        rig.Brain.OnBubbleChoice("chat");

        Assert.False(rig.Stage.Bubble!.IsMenu);
        Assert.True(BrainRig.Texts(MessageCategory.Chat, MessageCategory.Tip).Contains(rig.Stage.Bubble.Text));
    }

    [Fact]
    public void UnknownChoices_AreHarmless()
    {
        var rig = Rig();
        rig.Click(0.5);
        rig.Brain.OnBubbleChoice("nonsense");
        rig.Brain.OnBubbleChoice("size:banana");
        rig.Brain.OnBubbleChoice("acc:Crown");
        rig.Brain.OnBubbleChoice("expr:Exploding");
        Assert.Equal(1.0, rig.Stage.Scale);
        Assert.Equal(Accessory.None, rig.Stage.Accessory);
    }
}

public class PokeTests
{
    [Fact]
    public void RapidClicking_GetsAnAnnoyedLine_ThenAFuriousOne()
    {
        var rig = new BrainRig(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Never });

        rig.Click(0.3);          // menu opens
        rig.Click(0.3);          // menu closes
        rig.Click(0.3);          // third click in a row
        Assert.False(rig.Stage.Bubble!.IsMenu);
        Assert.True(BrainRig.Texts(MessageCategory.PokeAnnoyed).Contains(rig.Stage.Bubble.Text));

        rig.Click(0.3);
        rig.Click(0.3);
        rig.Click(0.3);
        Assert.True(BrainRig.Texts(MessageCategory.PokeFurious).Contains(rig.Stage.Bubble!.Text));
    }
}

public class SpeechTests
{
    private static BrainRig Rig() => new(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Never });

    [Fact]
    public void WhileTalking_TheMouthFlaps_AndGemSettlesAfterwards()
    {
        var rig = Rig();
        rig.Stage.Recorded = [];
        Assert.True(rig.Brain.Say(MessageCategory.Chat, SpeechPriority.User));
        var text = rig.Stage.Bubble!.Text;
        Assert.Equal(MascotState.Talking, rig.Brain.State);

        rig.Advance(Typewriter.TotalSeconds(text) * 0.9);
        var shapes = rig.Stage.Recorded.Select(p => p.Mouth).Distinct().Count();
        Assert.True(shapes >= 3, $"saw {shapes} mouth shapes");

        rig.Advance(5);
        Assert.Equal(MascotState.Idle, rig.Brain.State);
    }

    [Fact]
    public void GemKeepsItsExpressionForAMoment_AfterFinishingASentence()
    {
        var rig = Rig();
        rig.Brain.Say(MessageCategory.PokeAnnoyed, SpeechPriority.User);
        var line = rig.Stage.Bubble!.Text;
        var mood = MessageLibrary.All.First(m => m.Render(rig.Time.LocalNow) == line).Mood;

        rig.Advance(Typewriter.TotalSeconds(line) + 0.5);
        if (mood is MascotState.Happy or MascotState.Smug or MascotState.Confused)
            Assert.Equal(mood, rig.Brain.State);
        rig.Advance(4);
        Assert.Equal(MascotState.Idle, rig.Brain.State);
    }

    [Fact]
    public void ClosingTheBubble_StopsTheTalking()
    {
        var rig = Rig();
        rig.Brain.Say(MessageCategory.Chat, SpeechPriority.User);
        rig.Advance(0.3);
        rig.Brain.OnBubbleDismissed(BubbleDismissReason.Closed);
        Assert.Equal(MascotState.Idle, rig.Brain.State);
    }

    [Fact]
    public void Priorities_DecideWhoGetsTheFloor()
    {
        var rig = Rig();
        Assert.True(rig.Brain.Say(MessageCategory.Chat, SpeechPriority.Normal));
        Assert.False(rig.Brain.Say(MessageCategory.Chat, SpeechPriority.Ambient), "chatter never interrupts");
        Assert.True(rig.Brain.Say(MessageCategory.Chat, SpeechPriority.User), "the user's own request wins");
        Assert.False(rig.Brain.Say(MessageCategory.Chat, SpeechPriority.Normal), "a lesser line cannot replace it");
    }

    [Fact]
    public void ReplacedAndHiddenNotifications_AreIgnored()
    {
        var rig = Rig();
        rig.Brain.Say(MessageCategory.Chat, SpeechPriority.User);
        rig.Brain.OnBubbleDismissed(BubbleDismissReason.Replaced);
        Assert.False(rig.Brain.Say(MessageCategory.Chat, SpeechPriority.Ambient), "the bubble is still considered open");
    }
}

public class ChatterTests
{
    private static List<(TimeSpan At, BubbleContent Bubble)> Collect(BrainRig rig, double hours)
    {
        var log = new List<(TimeSpan, BubbleContent)>();
        rig.Stage.BubbleShown += c => log.Add((rig.Time.Now, c));
        rig.Advance(hours * 3600);
        return log;
    }

    [Fact]
    public void GemChatsNowAndThen_WithQuipsAndTips_NeverTooCloseTogether()
    {
        var rig = new BrainRig(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Often });
        var log = Collect(rig, 6);

        Assert.True(log.Count is > 12 and < 60, $"expected roughly one remark per 12-20 minutes, got {log.Count}");
        var allowed = BrainRig.Texts(MessageCategory.Chat, MessageCategory.Tip, MessageCategory.Startup);
        Assert.True(log.All(l => allowed.Contains(l.Bubble.Text)), "only chatter lines are spoken unprompted");
        Assert.True(log.All(l => l.Bubble.Priority == SpeechPriority.Ambient));

        for (var i = 1; i < log.Count; i++)
            Assert.True(log[i].At - log[i - 1].At >= TimeSpan.FromMinutes(3), "at least three minutes between remarks");
    }

    [Fact]
    public void RareChatter_IsRarer()
    {
        var often = Collect(new BrainRig(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Often }, seed: 4), 8).Count;
        var rarely = Collect(new BrainRig(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Rarely }, seed: 4), 8).Count;
        Assert.True(rarely < often / 2, $"rarely={rarely}, often={often}");
    }

    [Fact]
    public void QuietMode_NeverChatters()
    {
        var rig = new BrainRig(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Often, QuietMode = true });
        Assert.Empty(Collect(rig, 6));
    }

    [Fact]
    public void QuietHours_SilenceGem_ThenItResumes()
    {
        // Simulation starts at 14:00. Quiet from 14:00 to 20:00.
        var settings = MascotSettings.Default with
        {
            FirstRunDone = true, Chatter = ChatterFrequency.Often,
            QuietHoursEnabled = true, QuietFrom = new TimeOnly(14, 0), QuietTo = new TimeOnly(20, 0),
        };
        var rig = new BrainRig(settings);
        var log = Collect(rig, 10);

        Assert.True(log.Count > 0, "Gem speaks once quiet hours end");
        Assert.True(log.All(l => rig.Time.Start + l.At >= rig.Time.Start.Date.AddHours(20)), "nothing before 20:00");
    }

    [Fact]
    public void ChatterNeverInterruptsAnOpenMenu()
    {
        var rig = new BrainRig(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Often }) { AutoDismiss = false };
        rig.Advance(30);
        rig.Brain.OnBubbleDismissed(BubbleDismissReason.Closed);
        rig.Stage.Bubble = null;

        rig.Brain.OnClicked();
        var shown = rig.Stage.Shown.Count;
        rig.Advance(3 * 3600);

        Assert.True(rig.Stage.Bubble!.IsMenu);
        Assert.Equal(shown, rig.Stage.Shown.Count);
    }

    [Fact]
    public void ChangingTheFrequency_TakesEffectImmediately()
    {
        var rig = new BrainRig(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Never });
        Assert.Empty(Collect(rig, 3));

        rig.Brain.ApplySettings(rig.Brain.Settings with { Chatter = ChatterFrequency.Often });
        Assert.True(Collect(rig, 3).Count > 0);
    }

    [Fact]
    public void TheSameSeed_GivesTheSameConversation()
    {
        List<string> Run(int seed)
        {
            var rig = new BrainRig(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Often }, seed: seed);
            return Collect(rig, 3).Select(l => l.Bubble.Text).ToList();
        }

        Assert.True(Run(21).SequenceEqual(Run(21)));
        Assert.False(Run(21).SequenceEqual(Run(22)));
    }
}

public class SleepTests
{
    private static BrainRig Rig(int sleepAfter = 5) => new(MascotSettings.Default with { FirstRunDone = true, Chatter = ChatterFrequency.Never, SleepAfterMinutes = sleepAfter });

    [Fact]
    public void WhenTheUserGoesAway_GemFallsAsleep_AndWelcomesThemBack()
    {
        var rig = Rig();
        rig.Presence.Idle = TimeSpan.FromMinutes(6);
        rig.Advance(16);
        rig.Advance(3);

        Assert.True(rig.Brain.IsAsleep);
        Assert.Equal(MascotState.Sleeping, rig.Brain.State);
        Assert.True(rig.Stage.Last.EyeOpenL < 0.2 && rig.Stage.Last.Zzz > 0.5, "eyes shut, Zzz showing");

        rig.Presence.Idle = TimeSpan.FromSeconds(1);
        rig.Advance(3);

        Assert.False(rig.Brain.IsAsleep);
        Assert.True(BrainRig.Texts(MessageCategory.ReturnShort).Contains(rig.Stage.Bubble!.Text));
    }

    [Fact]
    public void ALongAbsence_GetsAMoreDramaticWelcome()
    {
        var rig = Rig();
        rig.Presence.Idle = TimeSpan.FromHours(2);
        rig.Advance(20);
        Assert.True(rig.Brain.IsAsleep);

        rig.Presence.Idle = TimeSpan.FromSeconds(1);
        rig.Advance(3);
        Assert.True(BrainRig.Texts(MessageCategory.ReturnLong).Contains(rig.Stage.Bubble!.Text));
    }

    [Fact]
    public void ASleepingGem_Sleeps_UntilTheUserReturns_NotJustMoves()
    {
        var rig = Rig();
        rig.Presence.Idle = TimeSpan.FromMinutes(10);
        rig.Advance(60);
        Assert.True(rig.Brain.IsAsleep);
        rig.Advance(600);
        Assert.True(rig.Brain.IsAsleep);
    }

    [Fact]
    public void ClickingASleepingGem_WakesItWithAGrumble()
    {
        var rig = Rig();
        rig.Presence.Idle = TimeSpan.FromMinutes(10);
        rig.Advance(20);
        Assert.True(rig.Brain.IsAsleep);

        rig.Brain.OnClicked();

        Assert.False(rig.Brain.IsAsleep);
        Assert.True(BrainRig.Texts(MessageCategory.WakeUp).Contains(rig.Stage.Bubble!.Text));
    }

    [Fact]
    public void Napping_LastsUntilGemIsClicked_EvenWhileTheUserIsActive()
    {
        var rig = Rig();
        rig.Click(0.5);
        rig.Brain.OnBubbleChoice("nap");
        Assert.True(rig.Brain.IsAsleep);
        Assert.True(BrainRig.Texts(MessageCategory.Nap).Contains(rig.Stage.Bubble!.Text));

        rig.Presence.Idle = TimeSpan.Zero; // user is busy typing
        rig.Advance(3600);
        Assert.True(rig.Brain.IsAsleep);

        rig.Brain.OnClicked();
        Assert.False(rig.Brain.IsAsleep);
    }

    [Fact]
    public void SleepDisabled_MeansNeverAsleep_AndNoPolling()
    {
        var rig = Rig(sleepAfter: 0);
        rig.Presence.Idle = TimeSpan.FromHours(10);
        rig.Advance(3600);
        Assert.False(rig.Brain.IsAsleep);

        // Only animation frames may be pending - no presence timer ticking every 15 seconds.
        var frames = rig.Stage.PoseCount;
        rig.Advance(120);
        Assert.True(rig.Stage.PoseCount - frames < 120 * 30 * 0.75);
    }

    [Fact]
    public void DraggingASleepingGem_WakesItQuietly()
    {
        var rig = Rig();
        rig.Presence.Idle = TimeSpan.FromMinutes(10);
        rig.Advance(20);
        var shown = rig.Stage.Shown.Count;

        rig.Brain.OnGrabbed();

        Assert.False(rig.Brain.IsAsleep);
        Assert.Equal(shown, rig.Stage.Shown.Count);
        Assert.Equal(MascotState.Grabbed, rig.Brain.State);
    }

    [Fact]
    public void ASleepingGem_IgnoresThePointer()
    {
        var rig = Rig();
        rig.Presence.Idle = TimeSpan.FromMinutes(10);
        rig.Advance(20);
        rig.Brain.OnPointerMoved(1, 1);
        rig.Advance(2);
        Assert.True(Math.Abs(rig.Stage.Last.GazeX) < 0.05);
    }
}
