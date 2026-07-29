# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for **BeachHead 2000** (Digital Fusion /
WizardWorks, 2000), the beach-defense arcade game shipped in the Steam "BeachHead Gold Edition"
package. Windows-only (WPF + Win32 memory APIs); the app manifest requests administrator rights
so it can `Read/WriteProcessMemory` on the game process.

## What makes this trainer different

BeachHead 2000 is the repo's **second native 32-bit Windows target** (after
`ImperialismIITrainer`), but unlike Imperialism II it has **no `GameLocator`**: the game's
mutable state (health, ammo, score, current level) lives in heap-allocated memory with no
adjacent constant byte-run to anchor a locator to (confirmed by dumping the `.data` section
and scanning the full process memory). So — like `DarklandsTrainer` and
`ThePerfectGeneral2Trainer` — the dependable primitive is a Cheat-Engine-style **value scan**
via `GameTrainers.Common.Memory.MemorySearcher`, driven from `MainViewModel`, with six
guided-scan buttons (Health / Bullets / Projectiles / Missiles / Score / Level).

The trainer also includes an **offline level-file editor** (like `ColonizationTrainer`'s save
editor): the shipped `Level_00`…`Level_60` files are plain-text scripts that define starting
ammo, time limit, enemy aggression, and unit waves. `LevelFile` parses, edits, and round-trips
them without losing comments, blank lines, or unknown properties.

## Project Structure & Module Organization

Three projects in `BeachHead2000Trainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/BeachHead2000Trainer/` — the WPF app (`AssemblyName`/`RootNamespace` both
  `BeachHead2000Trainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. Keep all reverse-engineered
    constants here.
    - `GameFacts.cs` — **Confirmed** constants: process name (`Bh`), image base
      (`0x00400000`), level count (61: `Level_00`…`Level_60`), aggression range (1–9),
      default health (100), max ammo (999/99/99), the `ObjectTypes` list (8 enemy types),
      the `AggressionAxes` list (4), and the `WeaponInfo`/`EnemyInfo`/`ControlInfo` record
      tables for the Reference tab.
    - `LevelFile.cs` — the level-file parser/editor. `Parse` extracts `Ammo`/`Time`/
      `Aggression`/`Artillery` fields while preserving all raw lines for round-trip.
      `ToText` rewrites only the header fields, leaving comments, `Object`/`ObjectInc`
      blocks, and unknown lines untouched. `Load`/`Save` handle disk I/O (ASCII encoding).
  - `ViewModels/` — hand-rolled MVVM using `GameTrainers.Common.Mvvm` (`ObservableObject`
    exposes `SetField(ref field, value)`; `RelayCommand`).
    - `MainViewModel` — attach/detach, background scan `Task` with cancellation, 200 ms poll
      loop (re-writes frozen pins, live-refreshes a small result set, detaches if the target
      exits), pin/freeze, the six guided scans, and the level-file editor (load/edit/save/max
      ammo). Implements `IScanHost` and `IDisposable`. Process picker targets `Bh`/`BH2000`/
      `beachhead` (not emulator hints — this is a native Windows game).
    - `ScanValue` — decimal/hex parsing + width-fit helpers (pure, unit-tested).
    - `ScanResultViewModel` — one scan candidate (address + live value).
    - `FrozenValueViewModel` — a pinned address: label, live value, target poked on edit,
      freeze re-write; rejects an out-of-width target (read-validate-write).
    - `IScanHost` — the read/write channel the rows use to reach RAM.
  - `App.xaml` / `MainWindow.xaml` — the WPF UI (Value Scanner / Freezes / Level Editor /
    Reference tabs with Weapons/Enemies/Controls sub-tabs).
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.

Dot-prefixed dirs (`.data/` RAM dumps, `.game/` copyrighted assets) are git-ignored — never
commit them.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags:
  `-Configuration Debug|Release`, `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\BeachHead2000Trainer\BeachHead2000Trainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use
file-scoped namespaces (`namespace BeachHead2000Trainer.Game;`), XML `<summary>` docs on
public types/members, `sealed` classes by default, `const` hex for offsets/sizes, and
`// --- section ---` divider comments. No linter/formatter config is committed; match the
surrounding file. Keep all reverse-engineered constants in the `Game/` layer and follow the
read-validate-write pattern so a shifted layout is never corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` asserts the Confirmed game-facts constants
(process name, image base, level count, aggression range, weapon/enemy/control counts), the
level-file parser (parse, field extraction, round-trip with comments/End marker preserved,
edge cases for minimal and empty files), the value-parsing/width-fit helpers (decimal/hex,
width-fit, canonicalization), and the frozen-value view-model logic (poke, freeze re-write,
out-of-width rejection, write-failure report). It runs individual `Check(...)` assertions and
returns exit code 0 (pass) or 1 (fail). Any parser/format or view-model change must keep the
assertions green. The harness never reads the copyrighted level files — it rebuilds the
fixture in code from the Confirmed format observed in the shipped `Level_00`.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a
semicolon. Describe which game state a change reads/writes and how it was confirmed against
the shipped files or live game. No PR template exists.

## Domain Notes

The trainer is intentionally scoped to the value scanner and the level-file editor. The
game's mutable state (health, ammo, score, current level) is heap-allocated with no stable
static anchor, so all live edits go through the scanner. The level files are the only
offline-editable surface — they are plain-text scripts that define starting conditions per
level, and the editor preserves their full structure (comments, Object/ObjectInc blocks,
unknown lines) on round-trip. All guided scans default to Int32; if a scan finds nothing, try
Int16 — some values may be stored as 16-bit words. The game process is `Bh.exe` (not the Steam
launcher); the process picker auto-sorts `bh`/`bh2000`/`beachhead` matches to the top.
