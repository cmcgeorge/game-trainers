# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for the 1990 DOS RPG *Fountain of Dreams*
(Electronic Arts), running under DOSBox / DOSBox-X. Windows-only (WPF + Win32 memory APIs); the
app manifest requests administrator rights so it can `Read/WriteProcessMemory` on the emulator.

## Project Structure & Module Organization

Three projects in `FountainOfDreamsTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/FountainOfDreamsTrainer/` — the WPF app (`AssemblyName` `FODTrainer`, `RootNamespace`
  `FountainOfDreamsTrainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. `CharacterFormat.cs` holds the
    validated **332-byte** character-record offset table (name +0x00, cash +0x14, seven
    attributes ST/IQ/DX/WP/AP/CH/LK at +0x18, profession +0x1F, CON +0x23, armor class +0x44,
    MaxCON +0x46, level +0x50, rank +0x52, experience +0x54, next-level XP +0x5E, inventory
    27×6-byte slots at +0x80). `CharacterRecord.cs` is a typed mutable view over a 332-byte
    buffer (little-endian ints, plain ASCII names, inventory read by index/slot). `IsValidRecord`
    is the single shared occupancy test used by both `IsOccupied` and the structural scanner: a
    1..18-char NUL-terminated printable-ASCII name starting with a letter, seven attribute bytes
    each in 1..20, a plausible MaxCON (1..999), a plausible level (1..99), and a profession in
    0..6. `AttributeBook`/`ProfessionBook`/`SkillBook`/`ItemBook`/`GameFacts` are reference tables
    (attribute descriptions from the manual, profession CON ranges and starting attributes from
    the `ARCHTYPE` file, 24 skills with IQ gates, 30+ items by id, game facts for process
    detection and display). The attribute order ST/IQ/DX/WP/AP/CH/LK was confirmed from
    `FOD.EXE`'s character-creation display strings and cross-checked against `ARCHTYPE`.
  - `Memory/` — `PartyLocator.cs` finds the party by **structure**, not by an anchor: Fountain of
    Dreams has no stable byte-run adjacent to the roster, so it scans for an array of three
    contiguous 332-byte records where occupied slots pack from slot 0. Each occupied slot must
    pass `CharacterRecord.IsValidRecord`. `IMemorySource.cs` is the read-only slice interface
    that `PartyLocator` needs, so the locator can be driven from a fixture in `FormatCheck`;
    `ProcessMemorySource` adapts a live `ProcessMemory` to it. The generic process-memory wrapper
    (`ProcessMemory`/`MemoryRegion`) comes from `GameTrainers.Common.Memory` (imported via csproj
    `<Using>` items), not a local copy.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` (attach/scan/detach, poll loop, party-wide
    actions including Freeze Health, party-wide quick actions), `CharacterViewModel` (per-character
    editable fields, CON freeze, max actions, inventory view), `ReferenceViewModel` (read-only
    Attributes/Skills/Professions/Items sub-tabs), `ICharacterHost` (the write channel —
    implemented by `MainViewModel` over live memory), and the row VMs (`NamedValueViewModel`,
    `ItemRowViewModel`). `ObservableObject`/`RelayCommand` are used from
    `GameTrainers.Common.Mvvm` — note `ObservableObject` exposes `SetField(ref field, value)`.
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.

Reverse-engineering notes and a strategy guide are in `docs/`. Dot-prefixed dirs are git-ignored
— never commit them.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration
  Debug|Release`, `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\FountainOfDreamsTrainer\FountainOfDreamsTrainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use
file-scoped namespaces (`namespace FountainOfDreamsTrainer.Game;`), XML `<summary>` docs on public
types/members, `sealed` classes by default, `const` hex for offsets, and `// --- section ---`
divider comments. No linter/formatter config is committed; match the surrounding file. Keep all
reverse-engineered constants in the `Game/` layer and follow the read-validate-write pattern (each
editor mutates the backing record then pokes only the changed byte range) so a shifted or
partially-loaded layout is never corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` builds synthetic character records with `MakeRecord`,
asserts every parsed field, the name round-trip, `IsValidRecord` (valid accepted, empty/short
buffer rejected, bad name/attributes/MaxCON/level/profession rejected), the reference tables
(AttributeBook, ProfessionBook, SkillBook, ItemBook), inventory set/clear/item-count, and
`PartyLocator` driven over a `FakeMemorySource` (3-member and solo rosters found with correct names
and slots, empty memory not found, cancellation honoured). It returns exit code 0 (pass) or 1
(fail). Add new checks there and keep it exiting 0.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon.
Describe which game state a change reads/writes and how it was confirmed against the save file or
game. No PR template exists.

## Domain Notes

The roster is three 332-byte slots; occupied slots pack from 0 and are validated by a 1..18-char
letter-leading NUL-terminated ASCII name, seven attribute bytes in 1..20, a plausible MaxCON
(1..999), a plausible level (1..99), and a profession in 0..6 (the locator and `IsValidRecord`
share these checks; editors clamp to the same ranges so an edit never makes a character
un-locatable). Names are plain ASCII. Inventory is 27 fixed 6-byte slots read by index (first byte
= item ID, 0xFF = empty; remaining 5 bytes are item-specific data). No live memory dump was
available — every offset carries a `[Static]` confidence marker, confirmed against the shipped
`DISK1` save file and cross-checked against the `ARCHTYPE` template, `FOD.EXE` display strings, and
the game manual, but not yet verified against a running game's RAM. There is **no save editor**
and **no teleport** (map position was not identified in static analysis). The skill encoding is
variable-length packed data in the +0x24..+0x43 region; the trainer reads skills for display but
does not write them directly. Setting values to the trainer's "max" caps is safe; the game UI may
render very large numbers oddly (cosmetic). The Freeze Health toggle re-pins current CON to MaxCON
each poll tick, rewriting only the single CON byte at +0x23.
