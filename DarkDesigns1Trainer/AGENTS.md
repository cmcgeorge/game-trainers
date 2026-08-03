# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for the 1990 DOS RPG *Dark Designs I:
Grelminar's Staff* by John Carmack (published by Softdisk / Big Blue Disk), running under DOSBox /
DOSBox-X. Windows-only (WPF + Win32 memory APIs); the app manifest requests administrator rights
so it can `Read/WriteProcessMemory` on the emulator.

## Project Structure & Module Organization

Two projects: the WPF app and its test harness, both referencing the shared `GameTrainers.Common`
library.

- `src/DarkDesigns1Trainer/` — the WPF app (`AssemblyName` `DD1Trainer`, `RootNamespace`
  `DarkDesigns1Trainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. `CharacterFormat.cs` holds the
    validated 54-byte character-record offset table, class/status constants, and lookup tables.
    `CharacterRecord.cs` is a typed mutable view over a 54-byte buffer with LE accessors and ASCII
    name handling. `CreationFormat.cs` holds the create screen's five-value rolled pool — layout,
    the measured dice, and the arrangement rule; `RollOdds.cs` turns that into exact target odds
    and `RollTally.cs` keeps per-rank session statistics. `AttributeBook.cs` describes the five
    attributes. `SpellBook.cs` / `ItemBook.cs` / `MonsterBook.cs` hold the reference tables
    (16 spells, 40 items, 43 monsters) transcribed from the unpacked EXE. `GameFacts.cs` holds
    game metadata and the locator anchor
    string. `SaveFile.cs` reads/writes `DDCHARS.DAT` with a one-shot `.bak`.
  - `Memory/` — `RosterLocator.cs` finds the party by **dual strategy**: (1) string-anchored scan
    for the 34-byte title string, then a 256 KB window forward for the 20-record pattern; (2)
    fallback structural scan of all readable memory for contiguous 54-byte records matching the
    character shape. `CreationScanner.cs` separately finds the create screen's rolled stat pool,
    which is not a roster record and so is invisible to `RosterLocator`: it signature-scans for the
    five captured numbers as a **multiset** (five contiguous uint16 LE that sort equal to the
    captured values sorted), and can read or write the pool. The generic process-memory wrapper
    (`ProcessMemory`/`MemoryRegion`) and `KeyboardSender` come from `GameTrainers.Common.Memory`
    (imported via csproj `<Using>` items), not a local copy.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` (attach/scan/detach, poll loop, party-wide
    actions, save editor, and the attached pid the roller sends keys to), `CharacterViewModel`
    (per-character editable fields, freeze, max actions), `CharacterRollerViewModel` (the Create
    tab: lock onto the roll, auto re-roll by tapping `R`, suggest the arrangement, write the pool),
    `NamedValueViewModel` (attribute rows), `ReferenceViewModel` (read-only spell/item/
    monster lists), `ICharacterHost` (the write channel). Views (`*.xaml`) bind to these.
    `ObservableObject`/`RelayCommand` are used from `GameTrainers.Common.Mvvm` — note
    `ObservableObject` exposes `SetField(ref field, value)`.
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.
- `docs/` — committed reverse-engineering notes and strategy guide.
- `.docs/` — RE working notes (git-ignored by `.*/`).

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration
  Debug|Release`, `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\DarkDesigns1Trainer\DarkDesigns1Trainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use
file-scoped namespaces (`namespace DarkDesigns1Trainer.Game;`), XML `<summary>` docs on public
types/members, `sealed` classes by default, `const` hex for offsets, and `// --- section ---`
divider comments. No linter/formatter config is committed; match the surrounding file. Keep all
reverse-engineered constants in the `Game/` layer and follow the read-validate-write pattern so a
shifted layout is never corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` builds a synthetic `DDCHARS.DAT` from the
sample (one character, "CHRISTOPHER", Fighter L1), asserts every decoded field, tests name
round-trip/truncation, empty slot detection, `LooksLikeRecord` validation, save-file round-trip
with `.bak` verification, multi-character saves, and reference table counts, and returns exit code
0 (pass) or 1 (fail). When the sample `DDCHARS.DAT` is present it also asserts the
empirically-confirmed values (STR=17, DEX=16, gold=1000, etc.). It further covers the creation
roller: pool encode/decode, the plausibility gate, `Arrange`/`MeetsTarget`/`Shortfall`, the roll
distribution, `CreationScanner`'s signature scan, and `CreationFormat.TryParseValues` — plus a
cross-check of `RollOdds.PMeetsTarget` against brute force over all 59,049 possible rolls, and the
specific probabilities quoted in `docs/StrategyGuide.md` so prose and model can't drift. Add new
checks there and keep it exiting 0. Any parser/format change must keep the assertions green.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon.
Describe which game state a change reads/writes and how it was confirmed against the unpacked EXE
or the sample `DDCHARS.DAT`. No PR template exists.

## Domain Notes

The roster is 20 × 54-byte slots (only the first few are occupied); occupied slots are validated by
exists flag = 1, name length 1–12, ASCII name starting with a letter, class 1–3, level ≥ 1, five
uint16 LE attributes in 1..999, and body max > 0. Empty slots have exists flag = 0 (the game may
leave stale data in other fields, so only the flag is checked). Names are plain ASCII. The 144-byte
file header is only partially decoded and is round-tripped without
interpretation. The status field encoding is inferred from game strings (fine=1, KO=2, STUNED=3,
STONE=4, DEAD=5) but not confirmed against a character in those states. `DARKDES.EXE` is
LZEXE 0.91 compressed; the unpacked image is a small-model Borland C build with BSS-allocated
character buffer.

The create screen keeps a separate five-value **rolled pool** (5 × uint16 LE, contiguous), not a
roster record — located, sampled and write-tested against the running game. Each value is
`10 + random(5) + random(5)`: a symmetric 10–18 triangle with mean 14, measured over 2,000 values
(chi-square *p* ≈ 0.66). Because the player arranges the five values freely, a per-attribute target
is a question about the pool as a multiset — a roll qualifies exactly when its values sorted
descending dominate the minimums sorted descending, which is what `CreationFormat.Arrange`
implements and `RollOdds` prices. Writing the pool works and the created character keeps the written
values, but the row of numbers already drawn on screen is not repainted; say so rather than implying
the display is in sync. See `docs/ReverseEngineering.md` §5.
