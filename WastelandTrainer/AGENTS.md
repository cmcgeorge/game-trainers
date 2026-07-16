# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for the 1988 DOS RPG *Wasteland* (Interplay /
Electronic Arts), running under DOSBox / DOSBox-X. Windows-only (WPF + Win32 memory APIs); the app
manifest requests administrator rights so it can `Read/WriteProcessMemory` on the emulator.

## Project Structure & Module Organization

Three projects in `WastelandTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/WastelandTrainer/` — the WPF app (`AssemblyName` `WLTrainer`, `RootNamespace`
  `WastelandTrainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. `CharacterFormat.cs` holds the
    validated **256-byte** character-record offset table and the party-state-header offsets;
    `CharacterRecord.cs` is a typed mutable view over a 256-byte buffer (little-endian ints, plain
    ASCII names via `WastelandText`, and the packed `(id, value)` skill/inventory arrays read to a
    `0x00` terminator). `SkillBook`/`ItemCatalog`/`MapBook`/`Walkthrough` are reference tables;
    `ParagraphBook` parses the game's own `paragraphs.txt` at runtime (the copyrighted booklet text
    is never embedded). `AmmoFreeze` is a pure, stateless helper (no process/UI dependency) backing the
    Freeze Ammo toggle — each tick it tops every ammo-bearing item (weapons that fire, clips/shells; see
    `ItemCatalog.IsAmmoItem`) up to `CharacterFormat.MaxAmmo` (99), preserving the jammed-weapon flag
    (the quantity byte's high bit) and leaving non-ammo items untouched; `FormatCheck` covers it.
  - `Memory/` — `PartyLocator.cs` finds the party by **structure**, not by an anchor: Wasteland has
    no stable byte-run adjacent to the roster, so it scans for an array of seven contiguous 256-byte
    records where occupied slots pack from slot 0. Each occupied slot must pass a strict validity
    test (kept in step with `CharacterRecord.IsOccupied`): a 2+-char letter-leading NUL-terminated
    name, seven attributes in `1..100`, plausible MAXCON, CON ≤ MAXCON, gender 0/1, nationality
    0..4 — enough to reject stray byte runs that merely look name-like. The sweep keeps the candidate
    with the **most** occupied members so a one-record false positive can't win over the real party.
    The generic process-memory wrapper (`ProcessMemory`/`MemoryRegion`) comes from
    `GameTrainers.Common.Memory` (imported via csproj `<Using>` items), not a local copy.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` (attach/scan/detach, poll loop, party-wide
    actions including the Freeze Health/Freeze Ammo toggles, and the party-header address it hands to
    the maps VM), `CharacterViewModel` (per-ranger editable fields, CON/health freeze, ammo freeze via
    `AmmoFreeze`, max actions), `MapsViewModel` (reads the party-state
    header for the live map/X/Y and teleports by writing the two position bytes),
    `ReferenceViewModel` (skills/items/paragraphs/strategy), and the row VMs
    (`NamedValueViewModel`, `SkillRowViewModel`, `ItemRowViewModel`) plus `ICharacterHost` (the
    write channel). Views (`*.xaml`) bind to these. `ObservableObject`/`RelayCommand` are used from
    `GameTrainers.Common.Mvvm` — note `ObservableObject` exposes `SetField(ref field, value)`.
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.

Ground-truth memory dumps live in `.data/` (with `memdump.md`, `roster.b64`, and the extract/scan
scripts); the game itself is in `.game/`; teardown and strategy notes are in `.docs/`. Dot-prefixed
dirs are git-ignored — never commit them (the multi-hundred-MB `*.bin` dumps especially).

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration
  Debug|Release`, `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\WastelandTrainer\WastelandTrainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly (`--live <pid>` runs the
  structural locator against a running emulator instead of the embedded fixture).

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use
file-scoped namespaces (`namespace WastelandTrainer.Game;`), XML `<summary>` docs on public
types/members, `sealed` classes by default, `const` hex for offsets, and `// --- section ---`
divider comments. No linter/formatter config is committed; match the surrounding file. Keep all
reverse-engineered constants in the `Game/` layer and follow the read-validate-write pattern (each
editor mutates the backing record then pokes only the changed byte range) so a shifted or
partially-loaded layout is never corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` decodes a base64-embedded 7-record roster captured
from a real dump (the default party: Hell Razor, Angela Deth, Thrasher, Snake Vargas + three empty
slots), asserts every parsed field against `.data/memdump.md`, plus the packed skill/inventory
decoding, the name round-trip, `IsOccupied`, and the reference tables, and returns exit code 0
(pass) or 1 (fail). It runs individual `Check(...)` assertions, not isolated tests — add new checks
there and keep it exiting 0. Any parser/format change must keep the sample-party assertions green.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon.
Describe which game state a change reads/writes and how it was confirmed against the live game or
dumps. No PR template exists.

## Domain Notes

The roster is seven 256-byte slots; occupied slots pack from 0 and are validated by a 2+-char
letter-leading ASCII name, seven attribute bytes in `1..100`, a plausible MAXCON, CON ≤ MAXCON,
gender 0/1 and nationality 0..4 (the locator and `IsOccupied` share these checks; editors clamp to
the same ranges so an edit never makes a ranger un-locatable). Names are **plain ASCII**
(not high-bit encoded like Dragon Wars) — encode/decode through `WastelandText`. Skills and
inventory are packed `(id, value)` arrays read to a `0x00` id terminator; edit skills by id
(reuse-or-append into 30 slots). The live map position and 12-byte map name live in the 256-byte
party-state header at `rosterBase − 0x100` (X at header `0x08`, Y at `0x09`); teleport writes only
those two bytes and only moves the party within the current map. The weapon/equip byte (`0x1F`) and
unidentified padding are left untouched. Setting values to the trainer's "max" caps is safe; the
game UI may render very large numbers oddly (cosmetic). The two poll-loop freezes each rewrite only
their own field: Freeze Health re-pins the CON u16 (`0x1D`), and Freeze Ammo tops the quantity
byte of ammo-bearing items (weapons that fire, clips/shells) up to 99 inside the 60-byte block at
`0xBD`+, preserving each byte's jammed-weapon high bit — never item ids, non-ammo items, or `0x1F`.
