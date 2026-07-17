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
    `0x00` terminator). `AttributeBook`/`SkillBook`/`ItemCatalog`/`MapBook`/`Walkthrough` are reference tables
    (`AttributeBook` describes what each of the seven attributes does — role text grounded in
    `.game\manual.txt`, build notes following `.docs\Wasteland-Strategy-Guide.md` — and its order is
    kept aligned with `CharacterFormat.AttributeNames`, asserted by `FormatCheck`; it feeds the
    attribute tooltips and the References ▸ Attributes sub-tab);
    `ParagraphBook` parses the game's own `paragraphs.txt` at runtime (the copyrighted booklet text
    is never embedded). `AmmoFreeze` is a pure, stateless helper (no process/UI dependency) backing the
    Freeze Ammo toggle — each tick it tops every ammo-bearing item (weapons that fire, clips/shells; see
    `ItemCatalog.IsAmmoItem`) up to `CharacterFormat.MaxAmmo` (99), clearing the jammed-weapon flag
    (the quantity byte's high bit) so a frozen weapon can't stay jammed, and leaving non-ammo items
    untouched; `FormatCheck` covers it. `RollTally` is another pure, testable helper — a running tally
    of per-stat and total averages/ranges backing the create-screen roller's statistics panel (plain
    averages, no fairness verdict). `RollOdds` estimates the odds of hitting a roll target by modelling
    each attribute as fair 3d6 (a model a 1,000-plus-sample live read confirmed); its `PMeetsTarget`
    convolves each attribute's minimum-floored 3d6 distribution so per-stat and total-points targets are
    handled together, exactly (MAXCON is not modelled — its distribution is undocumented). Both are
    `FormatCheck`-covered.
  - `Memory/` — `PartyLocator.cs` finds the party by **structure**, not by an anchor: Wasteland has
    no stable byte-run adjacent to the roster, so it scans for an array of seven contiguous 256-byte
    records where occupied slots pack from slot 0. Each occupied slot must pass a strict validity
    test (kept in step with `CharacterRecord.IsOccupied`): a 2+-char letter-leading NUL-terminated
    name, seven attributes in `1..100`, plausible MAXCON, CON ≤ MAXCON, gender 0/1, nationality
    0..4 — enough to reject stray byte runs that merely look name-like. The sweep keeps the candidate
    with the **most** occupied members so a one-record false positive can't win over the real party.
    The generic process-memory wrapper (`ProcessMemory`/`MemoryRegion`) comes from
    `GameTrainers.Common.Memory` (imported via csproj `<Using>` items), not a local copy.
    `CreationScanner.cs` is the *other* locator: it finds the **temporary create-screen roll buffer**
    (not a roster slot yet) by signature-scanning for the seven contiguous attribute bytes, then
    confirms the full record shape by checking the on-screen MAXCON (u16) and SKP at their record
    offsets relative to the attribute base (`AttrToMaxCon`/`AttrToSkp`, derived from `CharacterFormat`).
    Its `FindInBuffer`/`IsStructural`/`InRange` are pure and `FormatCheck`-covered.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` (attach/scan/detach, poll loop, party-wide
    actions including the Freeze Health/Freeze Ammo toggles, the party-header address it hands to
    the maps VM, and it owns the `CharacterRollerViewModel`), `CharacterViewModel` (per-ranger editable
    fields, CON/health freeze, ammo freeze via `AmmoFreeze`, max actions), `CharacterRollerViewModel`
    (the **Create** tab: locates the create-screen roll via `CreationScanner`, then taps the spacebar
    through `KeyboardSender` to re-roll until each stat — and, when the record shape is confirmed,
    MAXCON — meets its minimum; reads-only, never writes to the game), `MapsViewModel` (reads the party-state
    header for the live X/Y as a read-only "where am I" display — teleport is intentionally not offered
    because the header is a write-only shadow the game never reads back; see the RE notes §5),
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
(not high-bit encoded like Dragon Wars) — encode/decode through `WastelandText`. Skills are a packed
`(id, value)` array read to a `0x00` id terminator; edit skills by id (reuse-or-append into 30 slots).
Inventory is 30 fixed `(id, qty)` slots read by index and kept gap-free by `CompactInventory` after
each edit, so the running game (which reads the list only up to the first empty slot) still sees every
carried item. The live map position lives in the 256-byte party-state header at `rosterBase − 0x100`
(X at header `0x08`, Y at `0x09`); the Maps tab reads it for a live position display but does **not**
write it — teleport was removed after live RE proved the header is a write-only shadow the game never
reads back (§5 of the RE notes; no memory write relocates the party). The weapon/equip byte (`0x1F`) and
unidentified padding are left untouched. Setting values to the trainer's "max" caps is safe; the
game UI may render very large numbers oddly (cosmetic). The two poll-loop freezes each rewrite only
their own field: Freeze Health re-pins the CON u16 (`0x1D`), and Freeze Ammo tops the quantity
byte of ammo-bearing items (weapons that fire, clips/shells) up to 99 inside the 60-byte block at
`0xBD`+ and clears each byte's jammed-weapon high bit — never item ids, non-ammo items, or `0x1F`.

On the Ranger Center create screen, the manual's own instruction is **spacebar to re-roll,
Return to accept**; the roller drives exactly that, tapping `{SPACE}` between memory reads. The
rolled stats aren't a roster record yet, so `CreationScanner` finds them by their seven attribute
bytes (STR..CHR, contiguous, offset `0x0E` in the record) and confirms with MAXCON/SKP at their
record offsets. The roller is strictly read-plus-keystroke (no `WriteProcessMemory`), so a target
that never rolls just exhausts the roll limit — it can't corrupt a character.
