# Testing

## Automated

```powershell
dotnet test PaperClippy.sln
```

- `PaperClippy.Core.Tests` — placement geometry (multi-monitor, negative coordinates, unplugged monitors, oversized
  windows, non-finite saved positions), placement sanitizing, repository round-trips; the **animation engine** (blinking,
  looking around, every state's pose, pointer-following, reactions, frame pacing, determinism, sane bounds); **personality**
  (tone mix per attitude, no repeats, time-of-day lines, quiet hours, pokes); **speech** (typewriter, timing, bubble shape,
  placement on any side/monitor); and **GemBrain** on a virtual clock (first run, tour, click menu and every entry,
  chatter pacing over hours, sleep/wake/nap, priorities, hidden = zero scheduled work).
- `PaperClippy.Infrastructure.Tests` — migrations (fresh, idempotent, incremental, rollback on failure, newer-version
  refusal), damaged-file quarantine, settings round-trip and restart persistence, unreadable JSON, concurrent writes.

Status of these suites when Milestone 1 was written is in the README ("Verification status"). The Infrastructure suite
had not been executed against the real SQLite package at that point.

## Manual checklist — Milestone 1 (Windows)

Use a scratch data folder so your real data is untouched:
`$env:PAPERCLIPPY_DATA_DIR = "$env:TEMP\paperclippy-dev"`.

### Check these first — most likely to need a tweak (never run on Windows yet)
1. **Builds.** `dotnet build` is clean. Report/fix any compile errors in `PaperClippy.App` or `Infrastructure` first.
2. **Gem appears** bottom-right of the primary monitor, full colour, no black/white box around it.
3. **Click-through.** Clicking the empty corners of Gem's window reaches the app underneath; clicking the body drags.
4. **Right-click menu** appears and *Hide Gem* / *Exit PaperClippy* both work.
5. **Drag** feels smooth and Gem stays where dropped.
6. **Tray icon** is visible (check the overflow ^ on Windows 11), crisp at 100% and 150%+ scaling, and its menu works.

### Persistence and recovery
7. Drag Gem, exit via tray, relaunch → same spot.
8. Hide Gem, exit, relaunch → stays hidden; tray *Show Gem* brings it back; relaunching the exe while running also shows it.
9. Launch the exe twice → only one Gem, one tray icon.
10. Exit via tray → tray icon disappears immediately (no ghost); `logs\paperclippy.log` contains start/exit lines.
11. Corrupt it: exit, overwrite `data\paperclippy.db` with random bytes, relaunch → Gem still appears, a
    `paperclippy.db.corrupt-<time>` file sits beside a fresh `paperclippy.db`.

### Multi-monitor and DPI
12. Drag to monitor 2, exit, relaunch → on monitor 2.
13. With Gem on monitor 2, unplug it (or disable in Display settings) → Gem moves to the remaining monitor within ~1 s.
14. Drop Gem almost fully off an edge → it snaps back so it can be grabbed.
15. Mixed DPI (e.g. 100% + 150%): drag across the boundary → Gem keeps its apparent size, no flicker/jump; restart keeps place.
16. Change resolution / taskbar position while running → Gem stays reachable.

### Resource use
17. Leave it idle 30 min: Task Manager shows ~0% CPU and stable memory.
18. *Bring Gem to the Corner* works from the tray, including when Gem is hidden or lost.


## Manual checklist - Milestone 2 (Windows)

None of the WPF code from this milestone has been built or run yet. Do these in order; stop and report at the first
thing that fails (a build error message or a one-line description is plenty).

### 0. It builds
`dotnet build PaperClippy.sln` is clean and `dotnet test` passes (Core: 173 tests; Infrastructure: 14).

### 1. Gem draws correctly
1. Launch with a fresh data folder (`$env:PAPERCLIPPY_DATA_DIR`). Gem appears bottom-right, teal, with eyes and a smile,
   **no box or black background around it**, and a soft shadow.
2. Within ~10 seconds Gem blinks and glances left/right. It should look like `assets/mascot/expressions.png`.
3. Hover over Gem's body and move the mouse around: the pupils follow the pointer. Move away: they recentre.

### 2. First run
4. About a second after launch a bubble appears **beside Gem** with a pointer toward it: *"Hi! I'm Gem..."* and three
   buttons. The text types out and Gem's mouth moves while it does.
5. Try each button on separate runs: *Show me around* (4-step tour: Next / Skip / Done), *Keep it quiet* (Gem confirms,
   Quiet mode is on in the tray menu), *Got it* (bubble closes). Relaunch: the intro must **not** come back.

### 3. The click menu
6. Click Gem (left or right button). A cream bubble with a list opens beside Gem; rows highlight **navy** on hover.
7. Try every row: *Tell me something*, *Show me around*, *Change my look...* (Size / Accessory / Show me an expression, with
   working Back rows and a check mark on the current choice), *Gem settings...*, *Quiet mode*, *Take a nap*, *Hide Gem*,
   *Exit PaperClippy*.
8. Clicking Gem again while the menu is open closes it. Leaving the pointer far away for ~6 s closes it.
9. Click Gem 3 times quickly: an "annoyed" remark. 6 times: a "furious" one.

### 4. Dragging
10. Drag Gem: it widens its eyes, opens its mouth and swings with the drag speed; any bubble closes. Drop it: it
    squashes slightly and the position is remembered after a restart. Dropping it nearly off-screen pulls it back.

### 5. Speech
11. Bubbles stay for the duration set in settings, longer for long text; **hovering pauses the countdown**; the × closes
    it; clicking while it is still typing shows the rest at once.
12. Open the bubble near each screen edge / on a second monitor: it flips to a side that fits and stays on Gem's monitor.
13. Sarcastic lines (set *Attitude: Sassy*) put Gem in the smug face.

### 6. Settings dialog (tray -> Gem Settings...)
14. Every control applies **immediately**: size, accessory, animation (Off = no motion at all), attitude, chattiness,
    bubble time, quiet mode, quiet hours (type `22:00` / `08:00`; garbage text reverts), sleep delay, eyes follow mouse.
15. Quiet mode toggled in the menu, the tray and the dialog stays consistent in all three, and survives a restart.

### 7. Sleeping
16. Set *Fall asleep when I'm away* to 2 minutes, leave the PC alone for about 2 minutes: Gem closes its eyes and Zs
    float up. Move the mouse: it wakes, startles and welcomes you back. *Take a nap* sleeps until you click Gem.

### 8. Focus experiment (the one to test first)
Do this with *Don't take keyboard focus* OFF, then ON (tray menu or settings).
17. Open Notepad and type. Click Gem and use the menu. **OFF:** Notepad loses focus (expected). **ON:** Notepad's title
    bar stays active and you can keep typing right after using Gem.
18. With it ON also check: dragging Gem works, bubble buttons (*Got it*, *Next*) work, menu rows work, the × works, and
    nothing needs a second click.
19. Report which of these, if any, misbehave. If all pass, say so and I will make it the default.

### 9. Cost
20. Task Manager: Gem idle should sit near 0% CPU; brief bumps while blinking/talking are normal; *Celebrating* (menu ->
    Change my look -> Show me an expression -> Celebrating) spikes briefly for ~2.5 s; **Hide Gem** drops to 0%.
    Report it if idle stays above ~1-2%.

### 10. Multi-monitor / DPI (carried over from Milestone 1)
21. Drag Gem across monitors with different scaling: no size jump, bubble appears at the right size beside it.
22. Change the resolution or unplug a monitor while a bubble is open: the bubble closes and Gem stays reachable.

### Known limitations in Milestone 2
- Eyes follow the pointer only while it is over Gem's body (transparent areas are click-through by design).
- No sounds yet, and no Start with Windows (see roadmap).
- Text uses Tahoma, falling back to Segoe UI if Tahoma is missing.

## Known limitations to expect in Milestone 1
- (Superseded by Milestone 2.) Gem now has bubbles and animation; the focus option is described above.
