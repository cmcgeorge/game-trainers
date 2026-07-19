# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for **The Dungeons of Moria (UMoria 5.5.2)**,
the single-character roguelike by Robert Alan Koeneke and James E. Wilson, running under
DOSBox / DOSBox-X. Windows-only (WPF + Win32 memory APIs); the app manifest requests administrator
rights so it can `Read/WriteProcessMemory` on the emulator.

## What makes this trainer different

UMoria 5.5.2 is compiled with DJGPP v2.01 and runs as a 32-bit **protected-mode (DPMI)** program
under `CWSDPMI.EXE`. Its mutable state (the `py` struct, `cave[]`, `inventory[]`, `c_recall[]`)
lives in a flat 32-bit data segment that CWSDPMI/DOSBox allocates at a session-specific linear
address, and — like the sibling DPMI trainers (`ThePerfectGeneral2Trainer`, `BattleTech1Trainer`,
`QuestForGlory1Trainer`, `DarklandsTrainer`) — that heap has **no strong constant static
signature** to anchor a locator to. So there is deliberately **no `GameLocator`**. The dependable
primitive is a Cheat-Engine-style **value scan** via `GameTrainers.Common.Memory.MemorySearcher`,
driven from `MainViewModel`. See `.docs/ReverseEngineering.md` for the full teardown, confidence
tags, and the open Ghidra leads.

The teleport feature is unique among the value-scanner trainers: it locates `char_row` /
`char_col` (two `int16` globals that track cardinal moves) by **relative scanning** (snapshot an
unknown-value Int16 baseline, walk one square in-game, narrow by Increased/Decreased, repeat
until two candidates remain, then disambiguate by which direction moves which one) and writes
them to teleport — a workflow the other DPMI trainers don't need because their games don't expose
a single character position the player can step cardinal directions in.

## Project Structure & Module Organization

Three projects in `MoriaTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/MoriaTrainer/` — the WPF app (`AssemblyName` and `RootNamespace` `MoriaTrainer`), layered
  by concern:
  - `Game/` — pure data layer, no UI or process dependencies.
    - `PlayerFormat.cs` — the **Confirmed** image-relative `player_type` struct offsets (misc /
      stats sub-structs), inventory slot indices, the 3..18/100 stat encoding
      (`DecodeStat`/`EncodeStat`/`FormatStat`), and the cave cell constants (size, fval values).
      Offsets are kept for a future COFF-base locator; the value scanner does not add them to a
      base, it searches for the field by value.
    - `RaceBook.cs` — the 8 playable races (adjustments, infravision, allowed classes).
    - `ClassBook.cs` — the 6 classes (prime stat, hit die, mana basis, skill-gain rates from
      `FEATURES.NEW`).
    - `MonsterBook.cs` — a curated subset of the 279-creature roster (early game, mid-game,
      dragons, liches, and the Balrog at id 23) with recall text.
    - `ItemBook.cs` — the item-category reference (the `tval` byte on each `inven_type`), plus
      the ego weapons (HA/DF/SA/SD/SE/SU/FT/FB), crowns (6), and wearable flags
      (RA/RC/RF/RL/R/FA/SI/PL/TL/SR/RG/FF/BB/HA/DF/ST).
    - `SpellBook.cs` — the 31 mage spells + 31 priest prayers (letter, min level, mana cost,
      book, effect, damage). Letters are stable across the game (a 5.x feature).
    - `LevelBook.cs` — the 51-level descent reference (town + 50 dungeon levels), curated to 21
      depth entries with notable monsters/items/notes per depth. Dungeon layouts are
      procedurally generated, so there are no fixed maps.
    - `ParagraphBook.cs` — renders the recall paragraph for a creature, mirroring what the
      in-game `/` and `l` commands print from `c_recall[]`.
    - `ScanGuide.cs` — 14 guided scan recipes mapping a visible on-screen number to a memory
      field + the right `ScanWidth` + a typical range + a how-to-read hint.
  - `ViewModels/` — hand-rolled MVVM using `GameTrainers.Common.Mvvm` (`ObservableObject` exposes
    `SetField(ref field, value)`; `RelayCommand`).
    - `MainViewModel` — root: attach/detach, background scan `Task` with cancellation, 200 ms
      poll loop (re-writes frozen pins, live-refreshes a small result set), pin/freeze actions,
      the guided-scan buttons. Implements `IScanHost` and `IDisposable`; owns the `Teleport`,
      `Maps`, `Paragraphs`, `Items`, `Reference` child view-models.
    - `TeleportViewModel` — the teleport feature: locates `char_row` / `char_col` by relative
      scanning with its own `MemorySearcher` (Int16 width), then writes them. Has its own poll
      loop for live X/Y. Implements `IScanHost` and `IDisposable`.
    - `ParagraphsViewModel` / `ItemsViewModel` / `MapsViewModel` / `ReferenceViewModel` — the
      read-only reference tabs (no live attach needed). Filter and selection bound to the UI.
    - `ScanValue` — decimal/hex parsing + width-fit + canonicalize helpers (pure, unit-tested).
    - `ScanResultViewModel` — one scan candidate (address + live value).
    - `FrozenValueViewModel` — a pinned address: live value, user target poked on edit, freeze
      re-write; rejects an out-of-width target (read-validate-write).
    - `IScanHost` — the write channel the rows use to reach RAM.
  - `App.xaml` / `MainWindow.xaml` — the WPF UI
    (Character / Freeze / Teleport / Maps / Paragraphs / Items / Reference tabs).
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.

Reverse-engineering notes and the strategy guide live in `.docs/`; the game itself in `.game/`.
Dot-prefixed dirs are git-ignored — never commit them.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration
  Debug|Release`, `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\MoriaTrainer\MoriaTrainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use
file-scoped namespaces (`namespace MoriaTrainer.Game;`), XML `<summary>` docs on public
types/members, `sealed` classes by default, `const` for the counts/widths/offsets, and
`// --- section ---` divider comments. No linter/formatter config is committed; match the
surrounding file. Keep all reverse-engineered constants in the `Game/` layer and follow the
read-validate-write pattern so a shifted layout is never corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` asserts the Confirmed game-knowledge layer (the
stat encoding round-trips, the cave cell constants, the roster/level/item counts, the curated
monster roster including the Balrog, the 31+31 spell letters are unique), the
value-parsing/width-fit helpers, and the frozen-value view-model logic (poke, freeze re-write,
out-of-width rejection). It runs individual `Check(...)` assertions and returns exit code 0
(pass) or 1 (fail). Any parser/format or view-model change must keep the assertions green.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon.
Describe which game state a change reads/writes and how it was confirmed against the source,
the docs, or the live game. No PR template exists.

## Domain Notes

UMoria's heap address changes every DOSBox session, so the trainer never hard-codes addresses —
every field is located by value scanning. The stat encoding is two-layered: stats 3..18 are one
byte; 18/01..18/100 use two bytes (18, then the /xx byte). The guided `str`/`int`/`wis`/`dex`/
`con`/`chr` scans default to Byte width; if your stat is 18/xx, scan the /xx byte (the byte
after 18) — see the recipe notes. Editing the level or experience doesn't auto-grant levels or
recalculate HP/mana — those are derived values the engine computes on rest/level-up. The
teleport writes the raw `char_row` / `char_col` globals; the engine redraws the player on the
new cell next frame, but walking into a wall or monster still triggers the normal collision
logic. The live `c_recall[]`, `inventory[]`, and `cave[]` arrays can be read once a COFF-base
locator is built (Candidate — see `.docs/ReverseEngineering.md` §7); until then the
Paragraphs/Items/Maps tabs ship the static reference data.
