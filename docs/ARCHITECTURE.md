# Architecture

## Projects and dependencies

```
PaperClippy.App ──► PaperClippy.Infrastructure ──► PaperClippy.Core
        └────────────────────────────────────────────────▲
```

| Project | Target | Depends on | Holds |
|---|---|---|---|
| `Core` | net10.0 | nothing | Placement geometry, `ISettingsStore`, mascot placement model. No I/O, no Windows. |
| `Infrastructure` | net10.0 | Core, Microsoft.Data.Sqlite | SQLite database + migrations, settings store, file log, data paths. |
| `App` | net10.0-windows | Core, Infrastructure, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection | WPF windows, tray icon, Win32 interop, composition root. |

**Why not the nine projects in the brief?** `Mascot`, `Notes`, `Todos`, `Reminders` and `Focus` would be empty shells
today. The rule "avoid unnecessary abstractions" wins; each splits out of Core/App when it has real content
(planned in Milestones 2–4). Anything that can be tested without Windows belongs in Core or Infrastructure.

## Decisions

**Physical pixels and Win32 for window placement.** WPF's `Left/Top` are DIPs and misbehave across monitors with
different scaling. The mascot's position is read/written with `GetWindowRect`/`SetWindowPos` in virtual-screen
pixels, and the process is PerMonitorV2-aware (`app.manifest`). Moving onto a monitor with a different DPI changes
the window's pixel size, so placement re-checks with the real size after the move.

**Reachability is a pure function.** `ScreenPlacement.EnsureVisible` guarantees a window with fewer than 48px visible
(either axis) is pulled onto the nearest monitor. It is called after drags, after display changes (debounced 500 ms
from `SystemEvents.DisplaySettingsChanged`), on show, and at startup. It is fully unit-tested, including negative
coordinates (monitor left of primary), unplugged monitors and oversized windows.

**SQLite.** One file, WAL mode. Schema versions live in `PRAGMA user_version`; each migration runs in its own
transaction together with the version bump, so a failure leaves the database untouched (verified against SQLite).
Rules: never edit a released migration; add a new one. A database newer than the build is refused, not "fixed".
A file SQLite reports as corrupt/not-a-database is *moved aside* with a timestamp, never deleted. A failed
migration is deliberately **not** treated as corruption (that would discard user data because of our own bug).

**Settings are JSON in a key/value table.** Typed repositories (e.g. `MascotPlacementRepository`) sanitize what they
load, so hand-edited or damaged values degrade to defaults instead of crashing.

**Saves never block the UI and stay ordered.** The host chains saves on the thread pool; shutdown waits (≤2 s) for the
last one. State is captured in the window's `Closing` event because the native window no longer exists by `OnExit`.

**Tray via WinForms `NotifyIcon`.** It is native, needs no NuGet package and is a well-trodden path. Cost: referencing
WinForms (plus `Screen` for monitor enumeration). WPF + WinForms define clashing type names, so the App project turns
implicit usings off and WinForms use is confined to two files behind aliases. The icon is drawn at runtime at the exact
size the shell requests (and the HICON is freed to avoid a GDI leak).

**Single instance** via a session-local named mutex plus an auto-reset event so a second launch can wake the first.

**Transparency.** `AllowsTransparency` uses a layered window. Fully transparent pixels are click-through; the body has
an alpha-1/255 hit area so dragging works anywhere on the character. Layered windows are software-rendered — fine for
static art, and a known cost to measure when animation arrives in Milestone 2 (see roadmap).

**Not activating the mascot's window** (`WS_EX_NOACTIVATE`, so clicking Gem never steals focus) was considered and
deliberately deferred: it can interfere with WPF popups/context menus and must be tested on a real machine with speech
bubbles in place.

## Threading rules

All `MascotHost`/window/tray members run on the UI thread. Anything arriving on another thread (`SystemEvents`, the
single-instance listener) hops via `Dispatcher.InvokeAsync`. Database calls use `ConfigureAwait(false)` throughout the
library layers so synchronous waits on shutdown can never deadlock.

## Dependencies (and why)

`Microsoft.Data.Sqlite` (storage) · `CommunityToolkit.Mvvm` (observable properties/commands) ·
`Microsoft.Extensions.DependencyInjection` (composition root). Test-only: xUnit. Nothing else.

## License note on the reference project

PaperTodo is under PolyForm Noncommercial 1.0.0 with additional terms. PaperClippy neither copies nor derives from its
code or assets; it was used only to understand the expected experience (independent paper windows, capsules, quiet UX).
