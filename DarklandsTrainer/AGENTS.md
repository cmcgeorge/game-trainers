# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for the 1992 MicroProse open-world DOS RPG
*Darklands*, running under DOSBox / DOSBox-X. Windows-only (WPF + Win32 memory APIs); the app manifest
requests administrator rights so it can `Read/WriteProcessMemory` on the emulator.

## What makes this trainer different

Darklands is a **PKLITE-compressed** Microsoft-C binary (`DARKLAND.EXE`) that a DOS extender relocates
into extended memory, so — unlike a plain real-mode game — its code and static tables are **not** at a
fixed image offset once running, and there is no reliable EXE-string signature to detect it by. Its
*mutable* state (attributes, skills, party Fame, the coin purse) sits in dynamically allocated
structures with no adjacent constant byte-run to anchor to, and its address changes every session. So —
like `ThePerfectGeneral2Trainer` and `BattleTech1Trainer` — there is deliberately **no `GameLocator`**
and **no `GameDetector`**; the dependable primitive is a Cheat-Engine-style **value scan** via
`GameTrainers.Common.Memory.MemorySearcher`, driven from `MainViewModel`, with five guided-scan buttons
(Endurance / Strength / Skill / Fame / Florins). See `.docs/ReverseEngineering.md` for the full
teardown, confidence tags, and open Ghidra leads.

## Project Structure & Module Organization

Three projects in `DarklandsTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/DarklandsTrainer/` — the WPF app (`AssemblyName`/`RootNamespace` both `DarklandsTrainer`),
  layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. Keep all reverse-engineered constants here.
    - `AttributeBook.cs` — the six **Confirmed** primary attributes (Endurance, Strength, Agility,
      Perception, Intelligence, Charisma) plus party-wide Divine Favor, in save/stat-screen order.
    - `SkillBook.cs` — the nineteen **Confirmed** skills, each with its long stat-sheet spelling and
      short combat-dump spelling.
    - `GameFacts.cs` — the three-tier currency and its **Confirmed** ratios (1 fl = 20 gr = 240 pf), the
      `SplitPfennigs` purse-split helper, and the eleven-rung Fame ladder.
    - `SaveFile.cs` — a **read-only** reader for the DEFAULT save, keyed off the offsets Confirmed in
      `SAVES\DEFAULT` (§3). Deliberately never writes saves — the offsets come from a single sample, so
      live edits go through the scanner instead. Exercised by `FormatCheck` against a synthetic fixture.
  - `ViewModels/` — hand-rolled MVVM using `GameTrainers.Common.Mvvm` (`ObservableObject` exposes
    `SetField(ref field, value)`; `RelayCommand`).
    - `MainViewModel` — attach/detach, background scan `Task` with cancellation, 200 ms poll loop
      (re-writes frozen pins, live-refreshes a small result set, detaches if the target exits),
      pin/freeze, the five guided scans. Implements `IScanHost` and `IDisposable`.
    - `ScanValue` — decimal/hex parsing + width-fit helpers (pure, unit-tested).
    - `ScanResultViewModel` — one scan candidate (address + live value).
    - `FrozenValueViewModel` — a pinned address: label, live value, target poked on edit, freeze
      re-write; rejects an out-of-width target (read-validate-write).
    - `IScanHost` — the read/write channel the rows use to reach RAM.
  - `App.xaml` / `MainWindow.xaml` — the WPF UI (Value Scanner / Freezes / Attributes / Skills /
    Reference tabs).
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.

Reverse-engineering notes and the strategy guide live in `.docs/`; the game itself in `.game/`.
Dot-prefixed dirs are git-ignored — never commit them.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration Debug|Release`,
  `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\DarklandsTrainer\DarklandsTrainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use file-scoped
namespaces (`namespace DarklandsTrainer.Game;`), XML `<summary>` docs on public types/members, `sealed`
classes by default, `const` hex for offsets/sizes, and `// --- section ---` divider comments. No
linter/formatter config is committed; match the surrounding file. Keep all reverse-engineered constants
in the `Game/` layer and follow the read-validate-write pattern so a shifted layout is never corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` asserts the Confirmed attribute / skill / currency / Fame
tables, the currency-split maths, and the read-only save reader against a synthetic DEFAULT-shaped
fixture (location, label, party count, portrait codes, name/nickname, current/max attribute blocks),
plus the value-parsing/width-fit helpers and the frozen-value view-model logic (poke, freeze re-write,
out-of-width rejection, write-failure report). It runs individual `Check(...)` assertions and returns
exit code 0 (pass) or 1 (fail). Any parser/format or view-model change must keep the assertions green.
The harness never reads the copyrighted save file — it rebuilds the fixture in code.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon. Describe
which game state a change reads/writes and how it was confirmed against the shipped files or live game.
No PR template exists.

## Domain Notes

The trainer is intentionally scoped to the value scanner and the read-only Confirmed references. A
live-state anchor for a future `GameLocator`, the full save layout, and the exact byte→skill mapping in
the save's skills block are known open leads (`.docs/ReverseEngineering.md` §5–§6) left for Ghidra work
rather than guessed. Attribute and skill values are single bytes; Fame and the coin counts behave as
16-bit words, so the guides preset those widths. The three coins (Florin / Groschen / Pfennig) are
tracked separately in RAM.
