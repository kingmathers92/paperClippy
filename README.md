# PaperClippy

A nostalgic, lightweight Windows desktop companion: a little animated paperclip named **Gem** who lives on your desktop, reacts to you, talks in retro speech bubbles, and (soon) keeps your sticky notes, to-dos, reminders, and focus sessions company.

**Native WPF on .NET 10. Fully offline. No account, no telemetry, no cloud.** All data stays in a local SQLite database.

> **Status: Milestone 2 of 5 — Mascot Experience.**
>
> Gem is alive: it blinks, glances around, follows your mouse with its eyes, bounces, sleeps, and talks in speech bubbles with a personality that is funny and occasionally sarcastic. Clicking Gem opens a little menu.
>
> Sticky notes, to-dos, reminders, the focus timer, and additional themes are **not built yet**. See the [Roadmap](docs/ROADMAP.md).

![Gem's expressions](assets/mascot/expressions.png)

---

## Original Work

PaperClippy is an independent product.

[PaperTodo](https://github.com/snownico0722/PaperTodo) (PolyForm Noncommercial license) was studied only to understand the desktop-paper experience. No PaperTodo code or assets are used.

Gem is original artwork and is not Microsoft's Office Assistant.

---

## Features

### Gem — The Desktop Companion

- Nine animation states: idle, thinking, happy, confused, sleeping, working, celebrating, talking, and grabbed.
- An additional smug expression for Gem's sarcastic personality.
- Natural blinking, occasional double blinks, glancing, and breathing.
- Eye tracking that follows the mouse when it is over Gem.
- Squash-and-stretch reactions when clicked.
- Startles when woken up.
- Swings naturally when dragged.
- Confetti effects when celebrating.
- Animation can be disabled while keeping Gem's expressions.

### Speech Bubbles

- Retro cream-colored cards with a pointer toward Gem.
- Hard offset shadows and stitched inner borders.
- Typewriter text animation.
- Animated mouth movements while Gem speaks.
- Dismiss button, interactive buttons, and timed dismissal.
- Dismissal timer pauses while hovering over a bubble.
- Intelligent positioning: beside Gem first, then above or below when necessary.
- Bubbles stay on the same monitor as Gem.

### Personality Engine

- 107 hand-written dialogue lines.
- No AI, no network connection.
- Three personality modes:
  - **Gentle:** warm and supportive, never sarcastic.
  - **Playful:** default personality, with occasional dry humor.
  - **Sassy:** significantly more sarcastic.
- Dialogue does not repeat back-to-back.
- Some messages depend on the time of day.
- Sarcastic messages are accompanied by Gem's smug expression.

### Customization

- Size options from 50% to 200%.
- Accessories: glasses, party hat, and bow tie.
- Centralized color palette for future customization.
- Animation on/off.
- Personality selection.
- Chattiness and speech bubble duration controls.
- Quiet mode and configurable quiet hours.
- Configurable sleep delay.
- Mouse-following eye tracking.

### Courtesy and Behavior

Gem is designed to be a companion, not a distraction.

- Configurable unsolicited speech: never, rarely, sometimes, or often.
- At least three minutes between unsolicited remarks.
- Never interrupts while a menu is open.
- Respects Quiet mode and quiet hours.
- Falls asleep after inactivity (five minutes by default).
- Welcomes you back when you return.
- Does not run background activity while hidden.

### Desktop Experience

- Transparent, borderless, always-on-top mascot window.
- Drag Gem anywhere on your desktop.
- Position, visibility, and size are saved and restored.
- Automatically repositions Gem when a monitor or resolution changes.
- Supports mixed-DPI multi-monitor setups through Per-Monitor DPI Awareness V2.
- System tray icon:
  - Double-click to show or hide Gem.
  - Show/Hide menu options.
  - Bring Gem to the Corner.
  - Exit.
- Single-instance behavior: launching another copy wakes the existing instance instead of opening another.

### Settings and Focus

The Gem Settings dialog provides controls for visibility, size, accessories, animation, personality, chattiness, bubble duration, Quiet mode, quiet hours, sleep delay, and eye tracking.

An experimental **Don't take keyboard focus** option allows Gem to appear without stealing keyboard focus from the application you're using.

This option is disabled by default and requires further testing on real hardware.

See [Testing](docs/TESTING.md).

### Local Storage and Reliability

- SQLite database with versioned migrations.
- WAL mode enabled.
- Corrupted databases are quarantined using a timestamped `.corrupt-*` filename rather than silently deleted.
- Databases created by newer builds are refused without modification.
- Rolling application log file.
- Unhandled UI exceptions are logged.
- No background animation loops or timers run while idle.

---

## Build and Run

**Requirements:**

- Windows 10 or Windows 11.
- x64 or ARM64.
- [.NET 10 SDK](https://dotnet.microsoft.com/download).

Clone the repository and navigate to its root directory.

Restore dependencies:

```powershell
dotnet restore PaperClippy.sln
```

Build:

```powershell
dotnet build PaperClippy.sln
```

Run tests:

```powershell
dotnet test PaperClippy.sln
```

Launch PaperClippy:

```powershell
dotnet run --project src/PaperClippy.App
```

### Development Data Directory

To experiment without touching your normal application data, use a separate temporary data directory:

```powershell
$env:PAPERCLIPPY_DATA_DIR = "$env:TEMP\paperclippy-dev"
dotnet run --project src/PaperClippy.App
```

This also gives you the first-run experience again.

### Data Location

Normal application data is stored locally at:

```text
%LOCALAPPDATA%\PaperClippy\
```

Files:

```text
data\paperclippy.db
logs\paperclippy.log
```

---

## Verification Status

PaperClippy has been developed and tested in stages. The following distinguishes verified logic from components that still require Windows validation.

| Component                       | Verification                                                                                             |
| ------------------------------- | -------------------------------------------------------------------------------------------------------- |
| `PaperClippy.Core`              | Built with warnings-as-errors. 173 unit tests passed in the original milestone verification.             |
| Animation and personality logic | Tested using virtual clocks, simulated interactions, and mutation testing.                               |
| Speech bubble logic             | Positioning, timing, shape, and behavior tested.                                                         |
| Mascot artwork                  | Animator output rendered and reviewed using `assets/mascot/expressions.png` and `bubble-style.png`.      |
| SQLite behavior                 | SQL and database behavior checked against SQLite 3.45.                                                   |
| `PaperClippy.Infrastructure`    | Previously type-checked against an API-shaped SQLite stub. Actual execution still requires verification. |
| `PaperClippy.App`               | WPF/XAML requires Windows compilation and runtime verification.                                          |

### Current Windows Validation

The first Windows build exposed two XAML issues:

1. Invalid XML comments in `GemView.xaml`.
2. Invalid `Setter.Template` elements in `RetroTokens.xaml`.

Corrections have been prepared, but a successful Windows build and runtime test are still required before declaring the application fully verified.

The original milestone test count was 173 Core tests. Later development also reported 187 passing solution tests, but these results should not be treated as proof that the WPF application successfully builds.

See [Testing](docs/TESTING.md) for manual verification steps and known risks.

---

## Project Structure

```text
PaperClippy/
│
├── src/
│   ├── PaperClippy.Core/
│   │   └── Pure application logic:
│   │       animation, personality, speech, settings, brain
│   │
│   ├── PaperClippy.Infrastructure/
│   │   └── SQLite, migrations, file logging
│   │
│   └── PaperClippy.App/
│       └── WPF application:
│           Gem window, mascot view, speech bubbles,
│           settings dialog, system tray, Win32 interop
│
├── tests/
│   └── xUnit tests for Core and Infrastructure
│
├── assets/
│   └── Gem artwork, SVG sources, expressions and icons
│
└── docs/
    ├── ARCHITECTURE.md
    ├── ROADMAP.md
    ├── MASCOT.md
    └── TESTING.md
```

---

## Roadmap

PaperClippy is being developed in five milestones.

- **Milestone 1:** Desktop foundation — window behavior, positioning, tray integration, single instance, and local persistence.
- **Milestone 2:** Mascot experience — Gem's artwork, animations, personality, speech bubbles, interactions, and settings.
- **Milestone 3:** Sticky notes and to-dos.
- **Milestone 4:** Reminders and focus timer.
- **Milestone 5:** Themes, polish, and final reliability improvements.

See the detailed [Roadmap](docs/ROADMAP.md).

---

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Roadmap](docs/ROADMAP.md)
- [Gem's Identity](docs/MASCOT.md)
- [Testing Guide](docs/TESTING.md)

---

**PaperClippy — A little personality for your desktop.**
