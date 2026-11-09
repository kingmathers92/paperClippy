using PaperClippy.Core.Mascot.Animation;
using static PaperClippy.Core.Personality.MessageCategory;

namespace PaperClippy.Core.Personality;

/// <summary>
/// Everything Gem can say, written as plain data so it needs no AI and no network. Humor rules of thumb:
/// teasing Gem's own situation (a paperclip with no hands) is always fair; teasing the user stays light and
/// affectionate. Every category has at least one Warm or Witty line, so the Gentle level never runs dry.
/// </summary>
public static class MessageLibrary
{
    public static IReadOnlyList<GemMessage> All { get; } = Build();

    private static List<GemMessage> Build()
    {
        var list = new List<GemMessage>();
        var counters = new Dictionary<MessageCategory, int>();

        void Add(MessageCategory category, Tone tone, string text, MascotState? mood = null, DayParts when = DayParts.Any)
        {
            counters[category] = counters.GetValueOrDefault(category) + 1;
            var defaultMood = tone switch
            {
                Tone.Sarcastic => MascotState.Smug,
                Tone.Warm => MascotState.Happy,
                _ => MascotState.Idle,
            };
            list.Add(new GemMessage($"{category}.{counters[category]}", category, tone, text, mood ?? defaultMood, when));
        }

        // ---- Startup -------------------------------------------------------------------------------------
        Add(Startup, Tone.Witty, "Well, look who's back! Ready to get things done?");
        Add(Startup, Tone.Warm, "{greeting}! I saved your spot on the desktop.");
        Add(Startup, Tone.Warm, "Hi again! I'm all clipped in and ready.");
        Add(Startup, Tone.Witty, "Powering up. I brought my paperclip A-game.");
        Add(Startup, Tone.Witty, "I'm back! Don't worry, I didn't touch anything. No hands.");
        Add(Startup, Tone.Sarcastic, "Oh good, you're here. The desktop was getting far too peaceful.");
        Add(Startup, Tone.Sarcastic, "Back already? I barely had time to miss you. (I had plenty of time.)");
        Add(Startup, Tone.Warm, "Good morning! Coffee first, conquering the world second.", when: DayParts.Morning);
        Add(Startup, Tone.Witty, "Morning! I've been up for, oh, about four seconds. Let's go.", when: DayParts.Morning);
        Add(Startup, Tone.Warm, "Burning the midnight oil? I respect it. I also worry a little.", when: DayParts.Night);
        Add(Startup, Tone.Sarcastic, "It's the middle of the night. Sure, let's be productive. What could go wrong?", when: DayParts.Night);

        // ---- Idle chatter --------------------------------------------------------------------------------
        Add(Chat, Tone.Witty, "Fun fact: I'm 100% recycled desktop clutter.");
        Add(Chat, Tone.Witty, "I've been holding it together all day. You're welcome.");
        Add(Chat, Tone.Witty, "Paperclips: the original productivity tool. Staples are just show-offs.");
        Add(Chat, Tone.Witty, "If you need me, I'll be here. Clipped to nothing in particular.");
        Add(Chat, Tone.Witty, "Hot take: the best filing system is a pile with confidence.");
        Add(Chat, Tone.Witty, "I contain one bend of wire and infinite enthusiasm.");
        Add(Chat, Tone.Witty, "Somewhere out there, a stapler is jealous of my flexibility.");
        Add(Chat, Tone.Warm, "Sit up straight, drink some water. I believe in you.");
        Add(Chat, Tone.Warm, "Remember: done is better than perfect. Also, hydrate.");
        Add(Chat, Tone.Warm, "You're doing better than you think.");
        Add(Chat, Tone.Warm, "Tiny stretch break? Shoulders down, deep breath. I'll hold the fort.");
        Add(Chat, Tone.Sarcastic, "I'd tell you to take a break, but you look so productive. From here. I can't see your screen. Probably.");
        Add(Chat, Tone.Sarcastic, "I'm not saying your desktop is messy, but I've seen tidier junk drawers.");
        Add(Chat, Tone.Sarcastic, "Wow, a whole minute without clicking me. I'm touched.");
        Add(Chat, Tone.Sarcastic, "Don't mind me. I'll just dangle here, decoratively.");
        Add(Chat, Tone.Sarcastic, "Your tab count is your business. I'm just saying 'wow' quietly.");
        Add(Chat, Tone.Sarcastic, "Another thrilling day of me watching you work. Edge of my seat.");
        Add(Chat, Tone.Sarcastic, "I'd offer advice, but I'm a paperclip. Take that as you will.");
        Add(Chat, Tone.Sarcastic, "Quick five-minute task, you said. That's the world's most common lie.");
        Add(Chat, Tone.Sarcastic, "Your inbox called. It said 'help'.");
        Add(Chat, Tone.Sarcastic, "If procrastination were a sport, you'd at least make regionals.");
        Add(Chat, Tone.Sarcastic, "Sure, I could sit here silently. But where's the fun in that?");
        Add(Chat, Tone.Sarcastic, "Remember, the Save button isn't just decoration. Just saying.");
        Add(Chat, Tone.Warm, "It's very late. Go to sleep soon, okay? Future-you says thanks.", when: DayParts.Night);
        Add(Chat, Tone.Sarcastic, "It's very late. Not that I'm judging. (I'm judging a little.)", when: DayParts.Night);
        Add(Chat, Tone.Warm, "Lunch is a productivity tool too. Just saying.", when: DayParts.Afternoon);
        Add(Chat, Tone.Witty, "Post-lunch slump incoming. Stay brave, stay clipped.", when: DayParts.Afternoon);

        // ---- Tips (only things that exist today) ---------------------------------------------------------
        Add(Tip, Tone.Warm, "Tip: drag me anywhere. I'll remember my favourite spot.");
        Add(Tip, Tone.Witty, "Tip: click me for my little menu. I promise it's not a quiz.");
        Add(Tip, Tone.Warm, "Tip: if I ever go missing, double-click my icon in the tray to bring me back.");
        Add(Tip, Tone.Witty, "Tip: need peace and quiet? Switch on Quiet mode in my menu. I'll sulk silently.");
        Add(Tip, Tone.Warm, "Tip: I can change size and wear glasses or a party hat. Look under 'Change my look'.");
        Add(Tip, Tone.Witty, "Tip: too chatty? Turn me down in my settings. I'll try not to take it personally.");

        // ---- Pokes ---------------------------------------------------------------------------------------
        Add(PokeAnnoyed, Tone.Warm, "Hehe! Okay, that tickles in a good way.");
        Add(PokeAnnoyed, Tone.Witty, "Okay, okay, I'm awake. Mostly.");
        Add(PokeAnnoyed, Tone.Witty, "Poke me again and I start charging by the click.");
        Add(PokeAnnoyed, Tone.Witty, "Is there something specific, or just checking I still work?", mood: MascotState.Confused);
        Add(PokeAnnoyed, Tone.Sarcastic, "Fascinating. Clicking me three times has changed nothing. Try a fourth.");
        Add(PokeAnnoyed, Tone.Sarcastic, "Yes, I'm real. Yes, I'm still here. No, the clicking doesn't help.");
        Add(PokeFurious, Tone.Warm, "Okay, okay! You've got my full attention. What do you need?");
        Add(PokeFurious, Tone.Witty, "If I had arms, I'd cross them right now.");
        Add(PokeFurious, Tone.Witty, "You know I can see you, right?", mood: MascotState.Confused);
        Add(PokeFurious, Tone.Sarcastic, "Fine. Poke away. I'll just stand here being a paperclip.");
        Add(PokeFurious, Tone.Sarcastic, "At this rate I'm unionising. Desktop companions have rights.");
        Add(PokeFurious, Tone.Sarcastic, "Six clicks. Congratulations. Nothing has happened and nothing will.");

        // ---- Coming back / waking ------------------------------------------------------------------------
        Add(ReturnShort, Tone.Warm, "Taking a little break? That's okay. I'll be here.");
        Add(ReturnShort, Tone.Warm, "Welcome back! Hope that was a good breather.");
        Add(ReturnShort, Tone.Witty, "Welcome back! Did you miss me? Don't answer that.");
        Add(ReturnShort, Tone.Sarcastic, "Oh, you're alive. I was about to file a missing-persons report.");
        Add(ReturnLong, Tone.Warm, "That was a long break. I hope it was a good one. Welcome back!");
        Add(ReturnLong, Tone.Witty, "Long break! I assume it was productive. Or at least snacky.");
        Add(ReturnLong, Tone.Witty, "I counted every paperclip in the room while you were gone. Just me, so that was easy.");
        Add(ReturnLong, Tone.Sarcastic, "Oh, there you are. The desktop and I were starting a book club.");
        Add(WakeUp, Tone.Witty, "Mmh! I wasn't sleeping. I was resting my eyes.");
        Add(WakeUp, Tone.Warm, "Five more minutes... okay, I'm up. Hi!");
        Add(WakeUp, Tone.Sarcastic, "Oh sure, wake the paperclip. Very considerate.");
        Add(WakeUp, Tone.Sarcastic, "I was having a lovely dream about a tidy desktop. Thanks for that.");
        Add(WakeUp, Tone.Witty, "Whoa! Who? What? Oh, it's you.", mood: MascotState.Confused);

        // ---- Being dragged -------------------------------------------------------------------------------
        Add(Grabbed, Tone.Warm, "Wheee!");
        Add(Grabbed, Tone.Witty, "Careful, I'm bendy!");
        Add(Grabbed, Tone.Witty, "Put me down gently. I'm vinyl-coated, not invincible.");
        Add(Grabbed, Tone.Sarcastic, "Sure, move me. I'm a very flexible desktop accessory.");
        Add(Grabbed, Tone.Sarcastic, "Do I get a vote on where I sit? No? Great.");

        // ---- Settings feedback ---------------------------------------------------------------------------
        Add(QuietOn, Tone.Warm, "Quiet mode on. I'll keep to myself unless you call.");
        Add(QuietOn, Tone.Witty, "Quiet mode on. I'll be silent as a stapler.");
        Add(QuietOn, Tone.Sarcastic, "Quiet mode. So you're muting the paperclip. Understandable, honestly.");
        Add(QuietOff, Tone.Warm, "I'm back to chatting! Missed you.");
        Add(QuietOff, Tone.Witty, "Quiet mode off. Where was I? Oh right: being delightful.");
        Add(QuietOff, Tone.Sarcastic, "Quiet mode off. Brace yourself.");
        Add(Nap, Tone.Warm, "Nap time. Poke me when you need me.");
        Add(Nap, Tone.Witty, "Nap time! I'll be dreaming of tidy desktops.");
        Add(Nap, Tone.Sarcastic, "Nap time. Wake me when it's interesting.");

        // ---- Reserved for later milestones ---------------------------------------------------------------
        Add(NoTasks, Tone.Witty, "Nothing on your list? That's suspiciously peaceful.");
        Add(NoTasks, Tone.Warm, "A clear list. Enjoy it while it lasts.");
        Add(NoTasks, Tone.Sarcastic, "No tasks at all. Either you're done or you're in denial.");
        Add(NoTasks, Tone.Witty, "Empty to-do list. I'll alert the authorities.");
        Add(TaskCompleted, Tone.Warm, "One less thing to worry about!");
        Add(TaskCompleted, Tone.Witty, "Look at you, ticking boxes like a pro.");
        Add(TaskCompleted, Tone.Sarcastic, "Wow. A finished task. Frame it.");
        Add(TaskCompleted, Tone.Warm, "Nicely done. Take a moment to enjoy that.");
        Add(DeadlineApproaching, Tone.Witty, "Psst... remember that thing you promised yourself you'd finish?");
        Add(DeadlineApproaching, Tone.Warm, "Gentle nudge: a deadline is getting close. You've got this.");
        Add(DeadlineApproaching, Tone.Sarcastic, "Tick tock. Not that I'm counting. (I'm counting.)");
        Add(DeadlineApproaching, Tone.Witty, "A deadline is waving at you. It looks excited.");
        Add(CreatingNote, Tone.Warm, "Need to write something down? I'm listening!");
        Add(CreatingNote, Tone.Witty, "A fresh note! Make it a good one.");
        Add(CreatingNote, Tone.Sarcastic, "Ooh, a new note. Bet it's a masterpiece.");
        Add(CreatingNote, Tone.Warm, "Jot it down before it escapes.");
        Add(FocusStart, Tone.Warm, "I'm keeping quiet. You've got this!");
        Add(FocusStart, Tone.Witty, "Focus mode. I'll be over here, being a statue.");
        Add(FocusStart, Tone.Sarcastic, "Focus time. I'll hold back my best material.");
        Add(FocusStart, Tone.Warm, "Deep breath. One thing at a time.");
        Add(FocusBreak, Tone.Warm, "Excellent work. Time to stretch!");
        Add(FocusBreak, Tone.Witty, "Break time! Hydrate, wiggle, blink at something far away.");
        Add(FocusBreak, Tone.Sarcastic, "Break time. Yes, you may look at your phone. You will anyway.");
        Add(FocusBreak, Tone.Warm, "That was a solid session. Stand up and shake it out.");

        return list;
    }
}
