# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for the 1992 DOS RPG *Amberstar* by
Thalion Software, running under DOSBox / DOSBox-X. Windows-only (WPF + Win32 memory
APIs); the app manifest requests administrator rights so it can `Read/WriteProcessMemory`
on the emulator.

## Project Structure & Module Organization

Three projects in `AmberstarTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/AmberstarTrainer/` — the WPF app (`AssemblyName` `AmberstarTrainer`, `RootNamespace`
  `AmberstarTrainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. `CharacterFormat.cs` holds
    the validated 1146-byte character-record offset table (all multi-byte values are
    big-endian, inherited from the Atari ST origin), the magic-header constant, and the
    skill/attribute/ailment lookup tables. `CharacterRecord.cs` is a typed mutable view
    over a 1146-byte buffer; it handles big-endian Word/Long accessors, the plain-ASCII
    null-terminated name, and the current/max attribute and skill pairs. `SpellBook.cs`
    holds the four spell-school name tables (96 spells total). `RaceBook.cs` and
    `ClassBook.cs` hold the race and class lookup tables.
  - `Memory/` — `PartyLocator.cs` finds the party by **structural scan**: it walks every
    readable region looking for a window of up to six contiguous 1146-byte records that
    match the Amberstar party shape (magic header `00 FF`, type = Person, plausible
    fields, occupied slots packing from slot 0). The generic process-memory wrapper
    (`ProcessMemory`/`MemoryRegion`) comes from `GameTrainers.Common.Memory` (imported via
    csproj `<Using>` items), not a local copy.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` (attach/scan/detach, poll loop,
    party-wide actions), `CharacterViewModel` (per-character editable fields, freeze, max
    actions), `NamedValueViewModel` (attribute/skill rows), `ReferenceViewModel` (spell
    reference tab), `ICharacterHost` (the write channel). Views (`*.xaml`) bind to these.
    `ObservableObject`/`RelayCommand` are used from `GameTrainers.Common.Mvvm` — note
    `ObservableObject` exposes `SetField(ref field, value)`.
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.
- `docs/` — reverse-engineering notes (`RE.md`) and strategy guide (`StrategyGuide.md`).

Ground-truth game files live in the game directory (not committed); dot-prefixed dirs
(`.docs/`, `.data/`, `.game/`) are git-ignored — never commit them.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration
  Debug|Release`, `-Clean`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\AmberstarTrainer\AmberstarTrainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use
file-scoped namespaces (`namespace AmberstarTrainer.Game;`), XML `<summary>` docs on public
types/members, `sealed` classes by default, `const` hex for offsets, and `// --- section ---`
divider comments. No linter/formatter config is committed; match the surrounding file. Keep
all reverse-engineered constants in the `Game/` layer and follow the read-validate-write
pattern so a shifted layout is never corrupted. All multi-byte values in the character
record are **big-endian** — always use the `U16BE`/`U32BE` accessors, never little-endian.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` builds a synthetic 1146-byte record with
known values, asserts every parsed field (identity, attributes, skills, vitals, resources,
spells, ailments), checks the name round-trip and IsOccupied, verifies the spell/race/class
reference tables, and tests set operations. It exits 0 (pass) or 1 (fail). Any parser/format
change must keep these assertions green.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a
semicolon. Describe which game state a change reads/writes and how it was confirmed against
the file spec or live game. No PR template exists.

## Domain Notes

The party is up to six 1146-byte records; occupied slots are validated by the magic header
(`00 FF`), type = Person (0), and a plausible name + HP. Names use plain ASCII with a null
terminator (15 chars max). All multi-byte values are big-endian (Atari ST heritage). The
PARTYDAT.SAV save file uses an unknown compression and is not editable; this trainer edits
live memory only. Setting attributes/skills/vitals to the trainer's "max" caps is safe; the
game UI may render very large numbers oddly (cosmetic).
