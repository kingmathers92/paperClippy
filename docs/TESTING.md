# Testing

## Automated

```powershell
dotnet test PaperClippy.sln
```

- `PaperClippy.Core.Tests` — placement geometry (multi-monitor, negative coordinates, unplugged monitors, oversized
  windows, non-finite saved positions), placement sanitizing, repository round-trips.
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

## Known limitations to expect in Milestone 1
- Clicking Gem activates its window (it can take keyboard focus from the app you were using). A no-activate window is deferred — see roadmap.
- No speech bubbles, animation, notes, to-dos, reminders, timer, themes or Start-with-Windows yet.
