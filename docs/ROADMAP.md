# Roadmap

Legend: ✅ done · 🟡 written, needs first Windows build/run to confirm · ⬜ not started

## Milestone 1 — Foundation
- ✅ Solution layout, build props, SDK pin
- ✅ Core placement logic + tests (executed)
- 🟡 SQLite persistence, migrations, damaged-file recovery, settings store + tests (type-checked only)
- 🟡 WPF app: DI composition, single instance, transparent draggable mascot, position restore, tray icon, logging
- ✅ Original mascot artwork (Gem) and app/tray icon
- ⬜ *Deferred from M1:* automatic pre-migration database backup (lands with the first schema change, Milestone 3)

## Milestone 2 - Mascot experience
- ✅ Animation engine (9 states + Smug, idle life, pointer-following eyes, reactions, confetti) - tested
- ✅ Personality: 107 lines, three attitudes, no-repeat, time-of-day lines, quiet mode and quiet hours - tested
- ✅ Speech-bubble logic: typewriter timing, display time, shape, placement on any side / monitor - tested
- ✅ `GemBrain`: click menu, tour, first-run intro, sleep/wake, chatter pacing, pokes - tested on a virtual clock
- 🟡 WPF layer for all of the above (`GemView`, `BubbleWindow`, `SettingsWindow`, host, tray) - written, never built
- 🟡 Non-activating windows (opt-in) - written; **needs testing before it can become the default**
- ⬜ *Not done from the original plan:* Start with Windows (moves to Milestone 4 with the other Windows integrations);
  the first-run offers "Show me around / Keep it quiet / Got it" instead of "Create my first note / to-do" because
  notes and to-dos do not exist yet (they return to the intro in Milestone 3)
- **Risks to watch:** CPU of animating a layered window (measure idle, celebrating and sleeping); how the focus option
  behaves with real menus and bubbles; Tahoma availability on the target machines (falls back to Segoe UI)

## Milestone 3 — Notes and tasks
- Independent sticky-note windows (colors, resize, pin/always-on-top, capsule, search, archive, image attachments)
- Floating to-do window (priorities, due dates, tags, drag ordering, multi-line paste, progress, history)
- Schema migrations 2+ (with the pre-migration backup above); extract `Notes`/`Todos` projects

## Milestone 4 — Productivity
- Reminders (recurring, snooze, Windows notifications that work while windows are hidden)
- Pomodoro focus timer with session history; mascot reactions
- Start with Windows (carried over from Milestone 2)
- Global hotkeys (new note / new task / show-hide)
- Fuller tray menu from the brief (New Note, New To-Do, Today's Tasks, Focus, Show/Hide All, Settings)

## Milestone 5 — Polish
- Themes: Windows 98 Classic, Windows XP Luna, Warm Paper (switch without restart); design tokens
- Sounds, accessibility pass, animation refinement
- Backup/restore UI, settings window sections, multi-monitor and long-idle soak testing
