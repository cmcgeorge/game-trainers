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
    validated **72-byte** character-record offset table, class/status constants, item-slot
    geometry, and lookup tables. `CharacterRecord.cs` is a typed mutable view over a 72-byte
    buffer with LE accessors (including uint32 for experience), ASCII name handling, the ten
    carried pack slots and the four readied-equipment slots. `CreationFormat.cs` holds the create
    screen's five-value rolled pool — layout, the measured dice, and the arrangement rule;
    `RollOdds.cs` turns that into exact target odds and `RollTally.cs` keeps per-rank session
    statistics. `AttributeBook.cs` describes the five attributes. `SpellBook.cs` / `ItemBook.cs` /
    `MonsterBook.cs` hold the reference tables (16 spells, all 64 item ids of which 41 are obtainable, 43 monsters)
    transcribed from the unpacked EXE — `ItemBook`'s array index **is** the byte the game stores,
    so never reorder it. `MapFormat.cs` holds the 12,648-byte level layout (a 32×32×4 wall grid, a
    32×32 contents array, the description-text tables) plus the wall/square rules and the DGROUP
    deltas the map locator uses; `DungeonMap.cs` is a typed view over one level (walls, event codes,
    the mapped bit, the room list read from the level's own text, reveal-all); `MapBook.cs` holds
    the five levels and reads `DDMAP<n>.DAT`; `PartyPosition.cs` is the level / X / Y / facing block
    — the same four `uint16` live and in the save header. `GameFacts.cs` holds game metadata and the
    locator anchor string. `SaveFile.cs` reads/writes `DDCHARS.DAT` with a one-shot `.bak`,
    including the party position in its header.
  - `Memory/` — `RosterLocator.cs` finds the roster by **dual strategy**: (1) string-anchored scan
    for the 34-byte title string, then a 256 KB window forward for the 15-record pattern; (2)
    fallback structural scan of all readable memory for contiguous 72-byte records matching the
    character shape. It then sweeps ~10 KB for the game's **party working copies**, matched on name
    and class, and hangs them off `LocatedCharacter.Mirrors` — every live edit must be written to
    those too or the game's own save undoes it. Names are not unique in Dark Designs, so a copy is
    attached only when exactly one candidate matches and no other character claims it; anything
    ambiguous is dropped rather than guessed at. `CreationScanner.cs` separately finds the create
    screen's rolled stat pool,
    which is not a roster record and so is invisible to `RosterLocator`: it signature-scans for the
    five captured numbers as a **multiset** (five contiguous uint16 LE that sort equal to the
    captured values sorted), and can read or write the pool. `ItemTableLocator.cs` finds the
    64-entry item table by content (three known names at the 40-byte stride, then "NO ITEM" at
    entry 0) and reads/writes the per-item **potency** word — game-wide data, not per-character,
    so `MainViewModel` keeps the originals and restores them on detach. `MapLocator.cs` finds the
    party-position block and the map buffer of the level the party is on: **primarily from the
    roster** `RosterLocator` already found (both are at constant DGROUP offsets from it — `0xEFC`
    from the array base or `0xEB4` from the file's first record, since the roster scan can anchor on
    either, so both are tried and only the one whose bytes validate is used), with a **structural
    sweep** for the map buffer as a fallback. It takes `IMemorySource.cs` rather than
    `ProcessMemory` so `FormatCheck` can drive it over a synthetic address space. The generic
    process-memory wrapper (`ProcessMemory`/`MemoryRegion`) and `KeyboardSender` come from
    `GameTrainers.Common.Memory` (imported via csproj `<Using>` items), not a local copy.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` (attach/scan/detach, poll loop, party-wide
    actions, save editor, and the attached pid the roller sends keys to), `CharacterViewModel`
    (per-character editable fields, inventory/equipment, freeze, max actions),
    `CharacterRollerViewModel` (the Create tab: lock onto the roll, auto re-roll by tapping `R`,
    suggest the arrangement, write the pool), `NamedValueViewModel` (attribute rows),
    `ItemSlotViewModel` (one item byte — a pack slot or a readied slot — shared by the live and
    save editors; its `DuplicateCommand` goes through `IItemPack`, implemented by
    `CharacterViewModel` for live edits and by `MainViewModel.SavePack` for the save editor),
    `MapsViewModel` (the Maps tab: locate, the level schematic, teleport, reveal, offline `DDMAP`
    browsing), `ReferenceViewModel` (read-only spell/item/monster lists), `ICharacterHost`
    (the read/write channel). Views (`*.xaml`) bind to these; `MapConverters.cs` holds the map
    schematic's value converters.

    `CharacterViewModel.Poll()` is the poll tick: it re-checks every address it holds against an
    identity snapshot taken at scan time (name length + name + class) before trusting or writing to
    it, drops working copies that changed hands, and sets `IsStale` to suppress writes if the
    roster slot itself did. Validate against the snapshot, never against `Record` — `Record` is
    refreshed from the game each tick, so a bad read would otherwise authorise the next write.
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
with `.bak` verification, multi-character saves, inventory and readied-equipment round-trip
(offsets, clamping, full-pack behaviour, the game's ready-slot rules, item ids matching their table
index), item duplication, the item-potency table and item-table geometry, party-mirror staleness
(via a `FakeHost` flat-array stand-in for the address space), out-of-range item bytes, and
reference table counts, and returns exit code 0 (pass) or 1 (fail). When the sample
`DDCHARS.DAT` is present it also asserts the empirically-confirmed values (STR=17, DEX=16,
gold=100, XP=0/1000, magic=0/0). It further covers the creation
roller: pool encode/decode, the plausibility gate, `Arrange`/`MeetsTarget`/`Shortfall`, the roll
distribution, `CreationScanner`'s signature scan, and `CreationFormat.TryParseValues` — plus a
cross-check of `RollOdds.PMeetsTarget` against brute force over all 59,049 possible rolls, and the
specific probabilities quoted in `docs/StrategyGuide.md` so prose and model can't drift. It also
covers the map layer: the section offsets (asserted so they still account for all 12,648 bytes), the
wall classification and passability tables, the direction deltas against their opposites, a
synthetic level decoded square by square (looted squares included), every way a buffer can fail to
be a level (a one-sided wall edit, an out-of-range wall byte, an unvisited square with a code past
`0x3F`, a text run that overruns the block or starts before line 1, an oversized line-length prefix,
a blank buffer), the party position's encode/decode/clamping, the exact byte ranges the teleport and
reveal writes touch — driven through `FakeHost` so the level word and the wall/text sections are
proved untouched — the saved position round-tripping through `DDCHARS.DAT` with its `.bak`, and
`MapLocator` driven over a `FakeMemory` synthetic address space (found from either roster delta,
rejected for an implausible position, a map straddling a chunk seam, one past the first scan window,
one near address zero, one shifted by a record, a region that only reads 13,000 bytes at a time,
re-validation, cancellation). When the game directory is present it additionally parses all five
shipped `DDMAP` files and asserts their wall reciprocity, the stairs-up/down topology, the two item
squares the game hard-codes by coordinate, real room names decoded out of the shipped text (the only
thing that can pin the text-line indexing, since the synthetic fixture is built with the decoder's
own convention), and — the regression a live run caught and 537 synthetic checks did not — that of
the **129** shifted windows over a real level which fool wall reciprocity, exactly one survives the
full check. It runs **546 checks** with the game files present. Add new checks there and keep it exiting 0. Any parser/format
change must keep the assertions green.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon.
Describe which game state a change reads/writes and how it was confirmed against the unpacked EXE
or the sample `DDCHARS.DAT`. No PR template exists.

## Domain Notes

The roster is **15 × 72-byte** slots (only the first few are occupied); occupied slots are validated
by exists flag = 1, name length 1–12, ASCII name starting with a letter, status 1–5, class 1–3, five
uint16 LE attributes in 1..999, level 1–99, and body max > 0. Empty slots have exists flag = 0 (the
game may leave stale data in other fields, so only the flag is checked). Names are plain ASCII. The
file header's first 16 bytes are the four party roster slots and the party position (level, X, Y,
facing); the remaining 128 are round-tripped without interpretation.
`DARKDES.EXE` is LZEXE 0.91 compressed; the unpacked image is a multi-code-segment Borland C build
with a BSS-allocated character buffer.

Record size 72 and the field offsets are settled by disassembly, not inference: the game multiplies
a character index by `0x48` at ~300 sites and by 54 at none, and its own character-sheet printer,
rest/heal code and built-in max-character routine pin each field. Status is at `+0x0F`, class at
`+0x10` and level at `+0x1B` — the sample character reads 1 for all three, which is how an earlier
pass got them in the wrong order. See `docs/ReverseEngineering.md` §4.2.

**There are no item charges.** On `(U)se` the game applies the effect, rolls `random(256)` and
destroys the item unless the item table's *potency* word beats the roll; the same test fires a magic
weapon's special effect in combat. So "recharging" is meaningless per item — the trainer instead
pins potency to 256 in the live item table (global, never saved, restored on detach unless **Keep
on detach** is ticked) and offers a per-slot **Duplicate**. Don't add a charge field to the record;
there isn't one. See `docs/ReverseEngineering.md` §4.4.

`MainViewModel.ResetPotency(bool restore)` clears `_itemTableBase` **unconditionally** — that
address belongs to the process being released, and reusing it after a re-attach would write two
bytes to a stale address in a different process. Only the restore writes and the toggle/originals
reset are conditional. Keep that split if you touch it.

Items are byte ids into a 64-entry table (0 = empty, 1–63 valid); `ItemBook.All` is indexed by that
id, so its order is load-bearing. Each character has four readied slots (`+0x30` right hand, `+0x31`
left hand, `+0x33` armor, `+0x34` ring) and a ten-byte carried pack at `+0x3E`–`+0x47` — the last
ten bytes of the record, which the game addresses as `base + 0x3D + slot` for slot 1–10 (keys A–J).

The game plays out of **party working copies** at a different address and copies them back over the
roster on `(Q)uit and save`, so any live write must go to both — `LocatedCharacter.Mirrors` carries
the copies and `CharacterViewModel.Poke` writes them. Don't "simplify" that away: it was confirmed
live, with the copy sitting at exactly the `0xF3C` delta the disassembly predicts. The poll loop
reads `CharacterViewModel.LiveAddress` (the copy when there is one) because the roster record is
stale during play, which is what the freeze toggles react to.

The layout is **live-verified** (`docs/ReverseEngineering.md` §4.7): items written into pack slots
came back out of the game's own item screen with the right names, and the party status line
independently confirms body cur/max, status, class, and magic as a cur/max pair. Note the item
screen does not repaint on a write — leave and re-enter it.

Each castle level is **12,648 bytes** — a `DDMAP<n>.DAT` the game reads *verbatim* into one buffer at
`DGROUP:0x50F4`, so the file layout and the live layout are the same thing. It is a 32×32×4 wall
grid at `0x0000` indexed `x*128 + y*4 + facing`, a 32×32 contents array at `0x1000` indexed
`x*32 + y` (bit 7 = mapped, bits 0–5 = event code), two 64-entry text-index tables, 2,320 undecoded
bytes, and 127 forty-byte description lines. **X grows east and Y grows south**, facing is
0 N / 1 E / 2 S / 3 W, and facing indexes the wall byte directly — the wall in front of the party is
literally `walls[X][Y][facing]`. Don't transpose the axes: the level-1 item square the game
hard-codes as `(0x1322, 0x1324) == (20, 22)` only lands on an item square under this reading, and
the stairways only line up across levels under it. See `docs/ReverseEngineering.md` §6.

**`LooksLikeMap` has three layers and every one earns its place.** (1) Range checks on wall and
content bytes. (2) **Wall reciprocity** — a square's east wall byte equals its eastern neighbour's
west wall byte, 3,968 of 3,968 interior pairs on every shipped level. (3) **Text-table
consistency** — every run named by `firstLine`/`lineCount` lands inside the 127 lines and every
line's length prefix fits its 40-byte slot.

Do not drop layer 2 or 3 to make something else easier:

- Range checks alone accept an address one 72-byte record off, because a zeroed position block reads
  as a plausible "in town" position and a shifted window of a real map is still all in-range bytes.
- Reciprocity alone accepts **113 different offsets** (measured live). It relates squares a *fixed
  distance apart*, so it survives sliding the whole grid by whole squares, and the buffer is
  preceded by a few hundred zero bytes that a slid window reads as empty map. Only the text tables
  pin the absolute alignment. `FormatCheck` reproduces this with a real level and asserts that
  exactly one shifted window survives the full check.

Don't relax any of it to make locating work in town, either — in town there is no level loaded,
which is the honest answer.

**Square content bytes are 7-bit codes, not 6.** The game decodes `byte - 0x80` and treats anything
over `0x3F` as "nothing here any more", which is how it retires a looted square: an opened chest
becomes the whole byte `0xF7` and a taken item `0xF8`. Both have bit 6 set. A validator that assumes
six-bit codes passes the shipped maps (none has been played) and then rejects the player's own the
first time they open a chest; a six-bit *mask* makes a looted chest read back as a chest. Use
`MapFormat.DecodeEventCode`/`IsPlausibleContentByte` rather than masking by hand.

**The live teleport writes X, Y and facing only, never the level.** The game loads a level's map when
it processes a stairway; moving the level word alone leaves the party on the wrong map. Level changes
belong either to the game (teleport onto a stairway, take a step) or to the save editor, where the
game reloads the matching map itself. `FormatCheck` asserts the 6-byte write range.

The create screen keeps a separate five-value **rolled pool** (5 × uint16 LE, contiguous), not a
roster record — located, sampled and write-tested against the running game. Each value is
`10 + random(5) + random(5)`: a symmetric 10–18 triangle with mean 14, measured over 2,000 values
(chi-square *p* ≈ 0.66). Because the player arranges the five values freely, a per-attribute target
is a question about the pool as a multiset — a roll qualifies exactly when its values sorted
descending dominate the minimums sorted descending, which is what `CreationFormat.Arrange`
implements and `RollOdds` prices. Writing the pool works and the created character keeps the written
values, but the row of numbers already drawn on screen is not repainted; say so rather than implying
the display is in sync. See `docs/ReverseEngineering.md` §5.
