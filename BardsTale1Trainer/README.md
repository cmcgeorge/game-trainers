# Bard's Tale 1 — Live Trainer

A WPF (.NET 8) trainer for the 1987 DOS game **The Bard's Tale: Tales of the Unknown,
Volume I** (the Interplay IBM port, `BARD.EXE`). It attaches to the running game (e.g.
inside DOSBox / DOSBox-X), finds the party's character records in the emulated memory,
and lets you edit every member live — HP, SP, attributes, class, race, level,
experience, gold, equipment, and any raw byte — with freeze ("god mode") toggles and
one-click "max everything" buttons.

> Single-player cheat tool for your own save. Nothing here touches other machines or
> online services.

---

## Quick start

1. **Launch the game** in your emulator and load a party past the title screen (the
   party records only live in memory once a party is created/loaded).
2. **Build & run the trainer:**
   ```powershell
   .\run.ps1
   # builds Release and launches BT1Trainer.exe (it self-elevates via UAC)
   ```
   The app requests administrator rights — reading/writing another process's memory
   needs them, especially if the emulator itself runs elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/DOSBox-X are
   auto-sorted to the top) and click **Attach**. It scans memory and locates the game's
   data segment automatically.
4. **Edit:** select a character on the left. Use the **Character** tab for friendly
   fields, the **Inventory** tab for the 8 item slots, or the **Raw Bytes** tab for the
   full 92-byte record. Edits are written to the game *immediately*.
5. **Cheat fast:** tick **Freeze HP / Freeze SP** for god mode, or hit
   **★ Max EVERYTHING** for one character or the whole party. The **Gold / Experience
   "can't drop"** toggles are one-directional ("no-loss"): the party can still earn
   more, but the value is restored whenever the game tries to lower it (level-drain,
   spending).
6. **Or don't even alt-tab:** the global hotkeys **Ctrl+F1** (god mode on/off),
   **Ctrl+F2** (heal party) and **Ctrl+F3** (max everything) work even while the game
   window has focus.

If the scan finds nothing, make sure a party is actually loaded, then click
**Re-scan**. If several candidates are found, the **Data segment** dropdown lets you
choose (the first match is selected by default).

### Offline .TPW editing

The **Load .TPW character (offline)** button parses one of the game's `*.TPW` saved
characters so you can inspect and edit it without the game running. Edits stay inside
the trainer until you click **💾 Save .TPW file(s)**, which writes each loaded character
back to the file it came from — the previous version is kept beside it as `.bak`.
Attaching to a live game ends file mode (the character list becomes the live party's).

### Party snapshots

**📸 Snapshot…** (sidebar) saves the whole party — live or offline — to a single file of
seven `.TPW`-format blocks, one per slot. **↩ Restore…** loads one back slot-by-slot:
live characters (and their roster-row names) are written to the game immediately, an
offline view stays in the trainer until saved. Use it as an undo for experiments gone
wrong, or to shuttle a party between saves.

### Slot tools

On the **Character** tab, pick another slot in the **Slot tools** dropdown and
**⧉ Copy onto** (duplicate this character over it) or **⇄ Swap with** (reorder the
party). The BT1 record carries no slot index to fix up, but names live out-of-band in
the game's per-slot roster rows — the tools move the name along with the record.

### Memory search & X/Y search (Memory / X-Y tabs)

Some things aren't in the character record at all — notably the party's **dungeon
position and facing**, which live in separate game state. The **🔍 Memory** tab is a
small Cheat-Engine-style scanner (first-scan exact/unknown, then narrow by
increased/decreased/changed) for exactly this, and the **📍 X / Y Search** tab finds a
coordinate pair stored as two adjacent bytes. Candidates are dropped on **Detach**.

### Memory dump & dump diff (Dump tab)

Dumps every committed, readable region of the attached process to a `.bin` file plus a
`.csv` index mapping each region's file offset back to its live address — the same kind
of dump this trainer's format was reverse-engineered from. **Compare two dumps** then
diffs two saved dumps *by live process address* (the region indexes are intersected, so
layout shifts between dumps don't skew the result) and lists every changed byte run —
dump, change exactly one thing in-game, dump again, compare, and poke the addresses in
the Memory tab. Runs separated by ≤4 equal bytes merge into one; results cap at 2,000
runs, and bytes that were unreadable (zero-filled) when dumped are disclosed because
changes falling there may be phantoms.

### Maps tab — live marker & click-to-teleport

A labelled grid for each area (the 30×30 city and all sixteen 22×22 dungeon levels;
there are no bundled map scans). Once the **📍 X / Y Search** has narrowed the party
position to a *single* address, the Maps tab tracks the party with a live marker. Each
map is calibrated once by marking the party's spot from two different positions (the two
anchors define the pixel↔cell transform, including axis direction; persisted to
`%APPDATA%\BT1Trainer\map-calibration.json`). With **🚀 Teleport on click** armed,
clicking a cell writes its X/Y to the position address — the party jumps there.

### Reference tabs

**📖 Spells** lists all 79 spells grouped by art (Magician / Conjurer / Sorcerer /
Wizard) and level, with the 4-letter cast codes; **🎒 Items** lists the game's full
126-item table with a search box; **🐉 Monsters** lists the complete 127-entry bestiary
(extracted verbatim from the running game — see below) grouped by difficulty tier, with
a search box; **🛡 Classes** describes the ten classes and seven races.

---

## How it finds the party

`BARD.EXE` is packed on disk (a self-extracting executable), so a static address won't
do — and its strings only exist in plain form once the game is running and unpacked.
The trainer therefore **signature-scans** the target process for the game's race-name
table (`Human\0Elf\0Dwarf\0Hobbit\0Half-Elf\0Half-Orc\0Gnome\0`, a unique byte string),
then confirms the candidate by checking the item table and class table at their fixed
offsets inside the same 64 KB data segment (DGROUP). Once the segment base is known, the
party array and the on-screen roster name rows are at constant offsets from it.

---

## Reverse-engineered record format

A live character record is **92 bytes** (`0x5C`). The party is a **7-slot array** in the
data segment (slot 0 is the special / summoned-monster slot; the others are members).
On disk, a `.TPW` character file is 109 bytes: a 16-byte NUL-padded **name** followed by
the 92-byte record (byte 0 of the record is `0x01` on disk, `0x00` live). The character's
**name is not in the record** — the game keeps party names only in a separate on-screen
roster table (7 rows × 37 bytes), one row per slot.

All multi-byte fields are little-endian. Offsets are record-relative (add `0x10` for the
`.TPW` file offset).

| Offset | Size | Field |
|-------:|:----:|-------|
| `0x00` | 1 | Disk marker (1 on disk, 0 live) |
| `0x01` | 2 | Status (0 = OK/occupied, 1 = empty slot) |
| `0x05` | 2 | Class (0=Warrior 1=Paladin 2=Rogue 3=Bard 4=Hunter 5=Monk 6=Conjurer 7=Magician 8=Sorcerer 9=Wizard) |
| `0x07` | 5×2 | Attributes **max** (St, IQ, Dx, Cn, Lk), u16 each |
| `0x11` | 5×2 | Attributes **current** (same order) |
| `0x1B` | 2 | Armor class (i16, lower is better) |
| `0x1D` / `0x1F` | 2 / 2 | Hit points current / max (u16) |
| `0x21` / `0x23` | 2 / 2 | Spell points current / max (u16) |
| `0x25` | 8×2 | Inventory: 8 item words. **bit 15** = equipped; low bits = 1-based item id (0 = empty) |
| `0x35` | 4 | Experience (u32) |
| `0x39` | 4 | Gold (u32) |
| `0x3D` / `0x3F` | 2 / 2 | Level / level max (u16) |
| `0x41` | 4 | Spell mastery level (0–7) per art: Magician, Conjurer, Sorcerer, Wizard |
| `0x59` | 1 | Race (0=Human 1=Elf 2=Dwarf 3=Hobbit 4=Half-Elf 5=Half-Orc 6=Gnome) |

The friendly editors cover all of the above; the **Raw Bytes** tab annotates each known
offset and exposes every other byte for hand-editing.

### How the layout was derived & verified

It was reverse-engineered from a DOSBox-X full memory dump cross-checked against the two
sample `.TPW` files in `docs/`. The decoded values are internally consistent and verified
by the `FormatCheck` console project, which asserts (against the bundled `.TPW` files and,
when present, the dump under `testdata/`):

```
CHRISTOPHER  Conjurer  Gnome   L2  HP 26/26  SP 16/16  AC 9   XP 3198  — Dagger + Robes equipped, Conjurer spell level 1
A R HELPER   Warrior   Human   L1  HP 29/29  SP 0      — no spells, no equipment
```

Run the checks any time with:
```powershell
.\run.ps1 -Test -NoRun
```

The item table (126 entries) and spell list (79 spells, 4-letter codes) are extracted
verbatim from the running game's data segment and baked into `Game/ItemBook.cs` and
`Game/Spellbook.cs`.

### The bestiary (and what's still undecoded)

The monster name table also lives in the data segment: NUL-separated names with
inflection markup (`Kobold^^s^`, `Dwar^f^ves^`, `Old M^an^en^` → singular/plural) at
`DS:0x2874`, with a 127-entry table of u16 DS-relative pointers at `DS:0x2F3E` giving
the game's monster-id order. `Game/MonsterBook.cs` carries the raw names verbatim;
`FormatCheck` re-reads the live table from the dump and asserts a byte-for-byte match.
The list is organised in eight 16-id difficulty bands, each carrying its tier's four
enemy caster classes (at the band's end from tier 2 on).

Per-monster *stats* exist as four parallel one-byte-per-monster arrays at `DS:0x19C3`,
`0x1A43`, `0x1AC3` and `0x1B43` (nibble-packed; the first byte of each pair of the
`0x1A43` array tracks the difficulty bands), plus three 26-byte spell-summon records
("Dummy", "Joe the Sword", "Thor": 10 stat bytes + 16-char name) at `DS:0x2168`. Their
encodings haven't been decoded with confidence yet, so the Monsters tab deliberately
omits stats rather than guessing — these offsets are recorded here for future work.
(The on-disk monster data is unreachable statically: `BARD.EXE` is packed and the data
file `47` is compressed.)

---

## Project layout

```
src/BardsTale1Trainer/
  Game/        PartyFormat.cs       field offsets, enums, DGROUP anchors, the slot predicate
               CharacterRecord.cs   typed view over a 92-byte buffer (+ .TPW loader/writer)
               PartySnapshot.cs     7-block .TPW-format party snapshot build/parse
               ItemBook.cs          the game's 126-entry item id→name table
               Spellbook.cs         all 79 spells (art / level / code / name) + class→art map
               MonsterBook.cs       the 127-entry bestiary (verbatim names + markup decoder)
               MapBook.cs           the 17 areas (city + dungeon levels) with grid sizes
               MapCalibration.cs    two-anchor pixel↔cell transform for the Maps tab
               ClassBook.cs         class & race reference text
  Memory/      NativeMethods.cs     P/Invoke (OpenProcess, R/W ProcessMemory, RegisterHotKey, …)
               ProcessMemory.cs     handle wrapper + region enumeration
               PartyLocator.cs      signature scanner -> data-segment base address
               DumpComparer.cs      address-space diff of two saved dumps
               GlobalHotkeys.cs     system-wide Ctrl+F1/F2/F3 (RegisterHotKey + WndProc hook)
               BytePatternScanner.cs, MemorySearcher.cs, MemoryDumper.cs, KeyboardSender.cs
  Mvvm/        ObservableObject, RelayCommand
  ViewModels/  MainViewModel (attach/scan/freeze timer, snapshots, slot tools, hotkeys),
               CharacterViewModel, StatViewModel, ItemSlotViewModel, SpellLevelViewModel,
               HexByteViewModel, MemorySearchViewModel, PairSearchViewModel,
               MemoryDumpViewModel, DumpDiffViewModel, MapReferenceViewModel,
               MonsterReferenceViewModel, SpellReferenceViewModel, ItemReferenceViewModel,
               ReferenceViewModels
  App.xaml, MainWindow.xaml         dark, two-pane UI (party list + editor/reference tabs)
test/FormatCheck/                   headless verification against docs/*.TPW (+ testdata dump)
docs/                               the two sample .TPW characters
testdata/                           (optional) DOSBox-X memory dump + region CSV — not bundled
run.ps1                             build/test/launch helper
```

---

## Notes & caveats

- Tested logic: record parsing, the `.TPW` loader, round-tripping, and the data-segment
  layout are verified by `FormatCheck` against the bundled files. The live attach/scan
  path needs the actual game running to exercise.
- Several fields are best-effort (the exact meaning of the status word, level-max, and
  the undecoded `0x45..0x58` region). The **Raw Bytes** tab is authoritative — verify
  before relying on a friendly label.
- Setting values absurdly high is safe for the trainer but the game's own UI may display
  them oddly — that's cosmetic.
- Saving a `.TPW` keeps the prior file version as `.bak`, but always keep your own
  backups of `*.TPW` files before experimenting.
- The global hotkeys use `RegisterHotKey`; if another app already owns Ctrl+F1/F2/F3 the
  status bar says so and those keys simply won't fire here.
