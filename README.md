# PaperClippy

A nostalgic, lightweight Windows desktop companion: a little animated paperclip named **Gem** who lives on
your desktop and (soon) keeps your sticky notes, to-dos, reminders and focus sessions company.

Native WPF on .NET 10. Fully offline. No account, no telemetry, no cloud. All data stays in a local SQLite file.

> **Status: Milestone 1 of 5 (Foundation).** Gem appears, can be dragged anywhere, remembers where it lives
> (across restarts, multiple monitors and DPI changes), and has a tray icon. Notes, to-dos, speech bubbles,
> animation, reminders, focus timer and themes are **not built yet** — see [docs/ROADMAP.md](docs/ROADMAP.md).

## Original work

PaperClippy is an independent product. [PaperTodo](https://github.com/snownico0722/PaperTodo) (PolyForm
Noncommercial license) was studied only to understand the desktop-paper experience; no PaperTodo code or
assets are used. Gem is original artwork and is not Microsoft's Office Assistant.

## Build and run (Windows 10/11, x64 or ARM64)

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet build PaperClippy.sln
dotnet test  PaperClippy.sln
dotnet run --project src/PaperClippy.App
```

To experiment without touching your real data, point the app at a scratch folder:

```powershell
$env:PAPERCLIPPY_DATA_DIR = "$env:TEMP\paperclippy-dev"
dotnet run --project src/PaperClippy.App
```

Data lives in `%LOCALAPPDATA%\PaperClippy` (`data\paperclippy.db`, `logs\paperclippy.log`).

## What works in Milestone 1

- Transparent, borderless, always-on-top mascot window; drag it anywhere. Right-click for *Hide* / *Exit*.
- Position, visibility and size are saved and restored.
- Never lost off-screen: after a drag, a monitor change or a resolution change, Gem is pulled back to the
  nearest visible monitor. First launch: bottom-right corner of the primary monitor.
- Per-monitor DPI v2; positions are handled in physical pixels, so mixed-DPI multi-monitor setups behave.
- System tray icon: double-click toggles Gem; menu has Show/Hide, *Bring Gem to the Corner*, Exit.
- Single instance: launching again wakes the running copy (and shows Gem if hidden) instead of starting another.
- SQLite storage with versioned migrations (WAL mode). A damaged database file is quarantined (renamed
  `*.corrupt-<time>`, never deleted) and a fresh one is created. A database from a *newer* build is refused untouched.
- Rolling log file; unhandled UI exceptions are logged instead of killing the companion.
- Idle cost: no timers or animation loops run while idle.

## Verification status — please read

This milestone was written in a Linux sandbox with no access to NuGet or Windows. Here is exactly what was and
was not verified:

| Part | State |
|---|---|
| `PaperClippy.Core` (placement geometry, settings contracts, mascot placement) | Built with warnings-as-errors. **31 unit tests executed and passing**; tests were mutation-checked (broke the logic on purpose, confirmed they fail). |
| SQL and SQLite behaviour the persistence layer relies on (transactional `user_version`, upsert, damaged-file error code) | Checked against SQLite 3.45 directly. |
| `SingleInstanceGuard` listener/shutdown logic | Executed (with the named-event line swapped for an in-process one, because Linux has no named events). Windows named-object behaviour is unverified. |
| `PaperClippy.Infrastructure` + its 14 tests | Type-checked against an API-shaped stub of `Microsoft.Data.Sqlite`. **Never executed, never linked against the real package.** |
| `PaperClippy.App` (WPF, tray, Win32 interop) | **Never compiled or run.** XAML/manifest checked for well-formedness; code desk-reviewed. |

So the first `dotnet build` / `dotnet test` on Windows is the real test of Infrastructure and App. If something
fails to compile, it will be small (typo/API-shape level). [docs/TESTING.md](docs/TESTING.md) lists the manual
Windows checks to run, ordered by what is most likely to need adjustment.

## Layout

```
src/PaperClippy.Core            pure logic, zero dependencies (runs/tests anywhere)
src/PaperClippy.Infrastructure  SQLite, migrations, file log (Microsoft.Data.Sqlite)
src/PaperClippy.App             WPF shell: mascot window, tray, Win32 interop, DI composition
tests/                          xUnit tests for Core and Infrastructure
assets/                         Gem artwork (SVG source), icons
docs/                           architecture, roadmap, mascot identity, manual test plan
```

More: [Architecture](docs/ARCHITECTURE.md) · [Roadmap](docs/ROADMAP.md) · [Gem's identity](docs/MASCOT.md) · [Testing](docs/TESTING.md)
