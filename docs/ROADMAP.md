# Roadmap

Legend: ✅ done · 🟡 written, needs first Windows build/run to confirm · ⬜ not started

## Milestone 1 — Foundation
- ✅ Solution layout, build props, SDK pin
- ✅ Core placement logic + tests (executed)
- 🟡 SQLite persistence, migrations, damaged-file recovery, settings store + tests (type-checked only)
- 🟡 WPF app: DI composition, single instance, transparent draggable mascot, position restore, tray icon, logging
- ✅ Original mascot artwork (Gem) and app/tray icon
- ⬜ *Deferred from M1:* automatic pre-migration database backup (lands with the first schema change, Milestone 3)

## Milestone 2 — Mascot experience
- Animation state machine (Idle, Thinking, Happy, Confused, Sleeping, Working, Celebrating, Talking), modular
- Speech-bubble component (text, action buttons, dismiss, timed dismissal) as its own transparent window
- Rule-based personality/message library (no AI): no repeats, quiet mode, frequency and personality settings
- First-run greeting with three actions; mascot settings
- Start with Windows
- **Risks to settle early:** CPU cost of animating a layered (`AllowsTransparency`) window — measure; keep frame rate low,
  stop all animation while hidden/asleep. Evaluate `WS_EX_NOACTIVATE` so clicks don't steal focus (test with menus/bubbles).

## Milestone 3 — Notes and tasks
- Independent sticky-note windows (colors, resize, pin/always-on-top, capsule, search, archive, image attachments)
- Floating to-do window (priorities, due dates, tags, drag ordering, multi-line paste, progress, history)
- Schema migrations 2+ (with the pre-migration backup above); extract `Notes`/`Todos` projects

## Milestone 4 — Productivity
- Reminders (recurring, snooze, Windows notifications that work while windows are hidden)
- Pomodoro focus timer with session history; mascot reactions
- Global hotkeys (new note / new task / show-hide)
- Fuller tray menu from the brief (New Note, New To-Do, Today's Tasks, Focus, Show/Hide All, Settings)

## Milestone 5 — Polish
- Themes: Windows 98 Classic, Windows XP Luna, Warm Paper (switch without restart); design tokens
- Sounds, accessibility pass, animation refinement
- Backup/restore UI, settings window sections, multi-monitor and long-idle soak testing
