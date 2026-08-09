# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for the 1988 SSI DOS RPG
*Questron II*, running under DOSBox / DOSBox-X. Windows-only (WPF + Win32 memory
APIs); the app manifest requests administrator rights so it can
`Read/WriteProcessMemory` on the emulator.

## Project Structure & Module Organization

Three projects in `Questron2Trainer.sln`: the WPF app, its test harness, and the
shared `GameTrainers.Common` library it references.

- `src/Questron2Trainer/` — the WPF app (`AssemblyName` `Q2Trainer`, `RootNamespace`
  `Questron2Trainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. `CharacterFormat.cs`
    holds the 256-byte character-record offset table with **[Static]** and
    **[Inferred]** confidence markers (no live dump was available). `CharacterRecord.cs`
    is a typed mutable view over a 256-byte buffer with LE accessors. `GameFacts.cs`
    holds static game facts (title, publisher, developer, copyright string used as the
    locator anchor, emulator process hints). `SpellBook`/`WeaponBook`/`ArmorBook`/
    `ItemBook`/`MonsterBook`/`LocationBook` are reference tables extracted from
    START.EXE strings and the game manual.
  - `Memory/` — `IMemorySource.cs` is the read-only interface the locator needs
    (exists so the locator can be driven from a fixture in the test harness).
    `CharacterLocator.cs` finds the character by **anchor** (the copyright string
    `"Questron II (C) 1988 S.S.I."` — scans a 256 KB window forward for a valid
    record) with a **structural scan** fallback (sweeps all readable memory for a
    256-byte window passing `IsValidRecord`). The generic process-memory wrapper
    (`ProcessMemory`/`MemoryRegion`) comes from `GameTrainers.Common.Memory`
    (imported via csproj `<Using>` items), not a local copy.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` (attach/scan/detach, poll loop,
    quick actions), `CharacterViewModel` (per-character editable fields, freeze, max
    actions), `NamedValueViewModel` (attribute rows), `ReferenceViewModel` (read-only
    reference tables), `ICharacterHost` (the write channel). Views (`*.xaml`) bind to
    these. `ObservableObject`/`RelayCommand` are used from
    `GameTrainers.Common.Mvvm` — note `ObservableObject` exposes `SetField(ref field,
    value)`.
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.

RE notes and a strategy guide live in `docs/`. Dot-prefixed dirs (`.docs/`, `.data/`,
`.game/`) are git-ignored — never commit them.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags:
  `-Configuration Debug|Release`, `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching
  the GUI.
- `dotnet build src\Questron2Trainer\Questron2Trainer.csproj -c Release` — direct
  build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space
indent. Use file-scoped namespaces (`namespace Questron2Trainer.Game;`), XML
`<summary>` docs on public types/members, `sealed` classes by default, `const` hex
for offsets, and `// --- section ---` divider comments. No linter/formatter config is
committed; match the surrounding file. Keep all reverse-engineered constants in the
`Game/` layer and follow the read-validate-write pattern so a shifted layout is never
corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` runs 100 checks: format constants,
reference tables (spells/weapons/armor/items/monsters/locations), character record
round-trip (encode/decode name, HP, Food, Gold, attributes, level, weapon, armor,
spell charges, clamping), `IsValidRecord` validation (accepts demo, rejects
all-zeros/bad-name/HP=0/attribute>25/level>20/1-char-name/16-byte-no-null), and the
locator driven over a `FakeMemorySource` (character found with correct name/HP/level,
empty memory not found, cancellation honoured). Exits 0 (pass) or 1 (fail). Any
parser/format change must keep the harness green.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a
semicolon. Describe which game state a change reads/writes and how it was confirmed
against the game files or manual. No PR template exists.

## Domain Notes

Questron II is a **single-character RPG** (unlike the party-based Dragon Wars or
Wasteland), so there is exactly one 256-byte character record in memory at a time.
The record layout was reverse-engineered from the shipped `DEMOFILE` save (the demo
character "The Thing": HP 200, Food 188, Gold 162, all attributes 15, Level 1) and
cross-checked against the game manual and strings extracted from `START.EXE`. No live
memory dump was available, so every offset carries a **[Static]** (confirmed against
the DEMOFILE and/or manual) or **[Inferred]** (plausible but unconfirmed) confidence
marker. The layout should be verified against a running game at the first opportunity.

The game engine is `START.EXE`, an EXEPACK-compressed Microsoft C 1987 build by
Westwood Associates / Quest Software / SSI, version 1.2. Five spells (Magic Missile,
Fireball, Sonic Whine, Time Sap, Destruct), ten weapons, seven armor types, twelve
keys, eleven quest items, two transports, ~39 monsters, and ~26 locations were
recovered from the EXE's string table.
