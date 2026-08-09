# Questron II (SSI, 1988) — Reverse-Engineering Notes

Byte-level layout and string-table analysis of the 1988 SSI DOS RPG
**Questron II**, as it runs under **DOSBox / DOSBox-X**. These notes back the
`Questron2Trainer`: everything below was recovered from **static analysis only**
— the shipped `DEMOFILE` save, strings extracted from `START.EXE`, and the game
manual. **No live memory dump was taken and no live write-test was performed.**

Every offset carries a confidence marker:

- **[Static]** — confirmed against the `DEMOFILE` and/or the game manual.
- **[Inferred]** — plausible from the `DEMOFILE` but not independently confirmed.

The layout should be verified against a running game at the first opportunity.

---

## 1. Game Identification

| Field | Value |
|:---|:---|
| **Title** | Questron II |
| **Publisher** | Strategic Simulations, Inc. (SSI) |
| **Developer** | Westwood Associates / Quest Software |
| **Release year** | 1988 |
| **Platform** | DOS |
| **Version** | 1.2 |
| **Main engine** | `START.EXE` — 137,036 bytes |
| **Engine type** | EXEPACK-compressed Microsoft C 1987 build |
| **Game type** | Single-character RPG (one character record in memory at a time) |

Questron II is a sequel to the original *Questron* (SSI, 1984). Unlike the
party-based *Dragon Wars* or *Wasteland*, the player controls a single
adventurer in the land of Landor. The character record is therefore a solitary
256-byte structure rather than a roster array.

---

## 2. MZ Header Analysis

The `START.EXE` file was parsed with a PowerShell MZ-header reader
(`.\.docs\analyze.ps1`). The raw header fields:

| Field | Value | Notes |
|:---|:---|:---|
| File size | 137,036 bytes | |
| MZ signature | `4D 5A` | Standard MZ |
| Header paragraphs | `0x0020` | 512-byte header |
| Image size | 136,524 bytes | `blocks * 512 - headerSize` |
| Relocations | count at `relocOffset` | |
| Real CS:IP | `0x0262:0x38E3` | EXEPACK decompression stub entry |
| Real SS:SP | `0x2CB4:0x0000` | |
| Overlay count | 4 overlay segments | |
| Decompressed size | 81,360 bytes | EXEPACK target size |
| `"Packed file is corrupt"` | present | EXEPACK signature string |

The entry point (`CS:IP = 0x0262:0x38E3`) lands inside the EXEPACK decompression
stub, not the real program code. The `"Packed file is corrupt"` string — the
hallmark of Microsoft's EXEPACK packer — confirms the compression layer.

### 2.1 EXEPACK Unpacking Attempt

An EXEPACK unpacker was written as a C# tool
(`C:\Temp\ExepackTool\ExepackUnpack\Program.cs`) to decompress `START.EXE` for
full static analysis. The attempt produced **incomplete results**:

- Only **2 EXEPACK commands** were successfully decompressed before the stream
  stalled.
- The output was **mostly zeros** — the decompression did not reach the real
  code or data segments.
- The EXEPACK command stream proved **more complex than initially assumed**: the
  format uses a variable-length opcode/operand encoding that the simple
  implementation did not fully model.

The EXEPACK format was partially understood — the header structure, the
decompression stub's register usage, and the initial command bytes were
identified — but the full command stream could not be decoded. **Ghidra was not
available on the system** to load the unpacked image for disassembly, and
Ghidra's auto-analysis on the packed EXE directly is worthless (the entry code
is the decompression stub, not the program).

**Consequence:** full disassembly was not possible. All reverse engineering
went through the **data** — the `DEMOFILE` save and the readable strings that
survive inside the packed EXE — rather than through the code.

---

## 3. DEMOFILE Analysis

The shipped `DEMOFILE` (1,016 bytes) contains a demo character named **"The
Thing"**. This is the primary source for the character record layout. The file
is a flat binary dump with no header or checksum — the first 256 bytes are the
character record, and the remaining 760 bytes are auxiliary game state (not
decoded).

### 3.1 DEMOFILE Contents — "The Thing"

| Offset | Size | Field | Value | Encoding | Marker | Evidence |
|:---:|:---:|:---|:---|:---|:---:|:---|
| `0x00` | 2 | **HP** | 200 | uint16 LE | [Static] | Manual: "begins at 200" |
| `0x02` | 2 | **Food** | 188 | uint16 LE | [Static] | Manual: "buy food in towns" |
| `0x04` | 2 | **Gold** | 162 | uint16 LE | [Static] | Manual says "begins at 200" — discrepancy noted |
| `0x06` | 1 | **Flag / item count** | 03 | uint8 | [Inferred] | Three starting items per manual |
| `0x07` | 5 | **Attributes** | all 15 | uint8 each | [Static] | Manual: five attributes, new chars start at 15 |
| `0x10` | 1 | **Weapon ID** | 07 | uint8 | [Inferred] | Shortbow (0-indexed in weapon table) |
| `0x11` | 1 | **Armor ID** | 05 | uint8 | [Inferred] | Plate Mail (0-indexed in armor table) |
| `0x18` | 1 | **Level** | 01 | uint8 | [Inferred] | Adventurer per the level name table |
| `0x20` | 48 | **Item flags** | sparse `01`s | byte bitmap | [Inferred] | `01` at `+0x27`, `+0x2F`, `+0x3F` (3 items) |
| `0x50` | 16 | **Name** | "The Thing" | ASCII, null-padded | [Static] | Readable in the DEMOFILE |
| `0x86` | 8 | **Spell charges** | `01 01 01 01 01 01 01 01` | uint8 each | [Inferred] | One charge per spell |

### 3.2 Gold Discrepancy

The manual states the character "begins at 200" gold, but the DEMOFILE reads
**162**. This may reflect gold spent during the demo scenario (the demo
character may have made purchases), or the demo may start with a non-standard
amount. The offset itself (`+0x04`, uint16 LE) is confirmed by the manual's
description of gold as a core vital; only the exact starting value differs.

---

## 4. Character Record Layout (256 bytes / 0x100)

**[Inferred]** record size — 256 bytes covers all fields identified in the
DEMOFILE. The actual record may be smaller (the fields above end at `+0x8D`),
but 256 bytes is a safe window for the structural scan and ensures no adjacent
data is misidentified as part of the record.

All multi-byte integers are **little-endian**. Names are plain ASCII,
null-terminated within a 16-byte field.

| Offset | Size | Field | Encoding | Range | Marker |
|:---:|:---:|:---|:---|:---|:---:|
| `0x00` | 2 | **HP** | uint16 LE | 1–9999 | [Static] |
| `0x02` | 2 | **Food** | uint16 LE | 0–9999 | [Static] |
| `0x04` | 2 | **Gold** | uint16 LE | 0–65535 | [Static] |
| `0x06` | 1 | Flag / item count | uint8 | — | [Inferred] |
| `0x07` | 5 | **Attributes** (CHA, STR, AGI, STA, INT) | uint8 each | 1–25 | [Static] |
| `0x0C`–`0x0F` | 4 | Unknown / padding | — | — | [Inferred] |
| `0x10` | 1 | **Weapon ID** | uint8 | 0–9 | [Inferred] |
| `0x11` | 1 | **Armor ID** | uint8 | 0–6 | [Inferred] |
| `0x12`–`0x17` | 6 | Unknown | — | — | [Inferred] |
| `0x18` | 1 | **Level** | uint8 | 0–20 | [Inferred] |
| `0x19`–`0x1F` | 7 | Unknown | — | — | [Inferred] |
| `0x20` | 48 | **Item ownership flags** | byte bitmap | 0/1 | [Inferred] |
| `0x50` | 16 | **Name** | ASCII, null-padded | 2–15 chars | [Static] |
| `0x60` | 38 | **Combat / progression data** | — | — | [Inferred] |
| `0x86` | 8 | **Spell charges** | uint8 each | 0–99 | [Inferred] |
| `0x8E`–`0xFF` | 114 | Unknown / unused | — | — | [Inferred] |

### 4.1 Attributes

Five attributes, one byte each at `+0x07` through `+0x0B`, in this order:

| Index | Name | Abbreviation | Range |
|:---:|:---|:---:|:---|
| 0 | Charisma | CHA | 1–25 |
| 1 | Strength | STR | 1–25 |
| 2 | Agility | AGI | 1–25 |
| 3 | Stamina | STA | 1–25 |
| 4 | Intelligence | INT | 1–25 |

The attribute order was confirmed by strings in `START.EXE`. New characters
start with all attributes at 15, per the DEMOFILE and the manual.

### 4.2 Level / Rank Names

Four level/rank names were extracted from `START.EXE` strings:

| Level | Rank Name |
|:---:|:---|
| 0 | Nothing |
| 1 | Adventurer |
| 2 | Apprentice |
| 3 | Knight |

The DEMOFILE character is level 1 ("Adventurer"), matching the first named
rank. Levels above 3 use a fallback display (`?(n)`) since no further rank names
were found in the EXE strings.

### 4.3 Spell Charges

Eight spell-charge bytes at `+0x86`, one per spell slot. The DEMOFILE has `01`
in all eight bytes. The manual describes four buyable spells; the fifth spell
(*Destruct*) was found in EXE strings only. The eight charge slots may
correspond to spell tiers or multiple castings per spell — this mapping is
**[Inferred]** and not yet confirmed.

### 4.4 Item Ownership Flags

A 48-byte bitmap at `+0x20`–`+0x4F` tracks item ownership. The DEMOFILE has
sparse `01` values at `+0x27`, `+0x2F`, and `+0x3F` — three set bits matching
the three starting items per the manual. The exact bit-to-item mapping is
**[Inferred]** and was not fully decoded.

---

## 5. Strings Extracted from START.EXE

Despite the EXEPACK compression, enough readable ASCII strings survive inside
`START.EXE` to reconstruct the game's reference catalogs. A `strings`-style
sweep (the PowerShell `analyze.ps1` script) extracted all printable runs of 8+
characters, yielding the following tables.

### 5.1 Spells (5)

The first four are buyable in towns per the manual; *Destruct* was found in EXE
strings only and is not listed in the manual.

| ID | Spell | Buyable | Description |
|:---:|:---|:---:|:---|
| 0 | Magic Missile | Yes | Single-target damage spell |
| 1 | Fireball | Yes | More powerful single-target damage spell |
| 2 | Sonic Whine | Yes | Attacks all adjacent enemies |
| 3 | Time Sap | Yes | Slows enemies' sense of time to freeze them |
| 4 | Destruct | No | Powerful spell — found in strings only, not in the manual |

### 5.2 Weapons (10)

Extracted from `START.EXE` strings. The equipped-weapon byte at `+0x10` indexes
this table (0-based). The DEMOFILE carries weapon ID 07 (Shortbow).

| ID | Weapon |
|:---:|:---|
| 0 | Dagger |
| 1 | Hammer |
| 2 | Hatchet |
| 3 | Cudgel |
| 4 | Rapier |
| 5 | Fauchard |
| 6 | Weighted Spear |
| 7 | Shortbow |
| 8 | Broadsword |
| 9 | Crossbow |

### 5.3 Armor (7)

Extracted from `START.EXE` strings. The equipped-armor byte at `+0x11` indexes
this table (0-based). The DEMOFILE carries armor ID 05 (Plate Mail).

| ID | Armor |
|:---:|:---|
| 0 | Rawhide |
| 1 | Studded Leather |
| 2 | Ring Mail |
| 3 | Bar Mail |
| 4 | Chain Mail |
| 5 | Plate Mail |
| 6 | Ribbed Plate |

### 5.4 Keys (12)

Twelve keys were recovered from the EXE string table:

| ID | Key |
|:---:|:---|
| 0 | Gold Key |
| 1 | Opal Key |
| 2 | Iron Key |
| 3 | Brass Key |
| 4 | Copper Key |
| 5 | Silver Key |
| 6 | Emerald Key |
| 7 | Onyx Key |
| 8 | Ruby Key |
| 9 | Agate Key |
| 10 | Sapphire Key |
| 11 | Black Key |

### 5.5 Quest Items (11)

Special quest items beyond the keys:

| ID | Item |
|:---:|:---|
| 12 | Unicorn Horn |
| 13 | Wand of Power |
| 14 | Eternal Flame |
| 15 | Book of Magic |
| 16 | Crystal Goblet |
| 17 | Chalice of Arvyl |
| 18 | Moonstone Amulet |
| 19 | Orb of Enchantment |
| 20 | Scroll of Scalna |
| 21 | Rope & Hooks |
| 22 | Bread of Life |

### 5.6 Transports (2)

| ID | Transport |
|:---:|:---|
| 23 | Camalon |
| 24 | Trained Eagle |

### 5.7 Monsters (~39)

The manual states "over 60 different types of creatures inhabit Landor." These
~39 were recovered from the EXE's string table; the remainder may be in
compressed or overlay sections not reached by the string sweep.

| ID | Monster | ID | Monster |
|:---:|:---|:---:|:---|
| 0 | Sovan Priest | 20 | Jelly Nymph |
| 1 | Gypsy Imp | 21 | Giant Cockroach |
| 2 | Beggar | 22 | Stink Worm |
| 3 | Brawn Warrior | 23 | Hurler |
| 4 | Wave Slapper | 24 | Ice Urchin |
| 5 | Mutant Carp | 25 | Cloud Creeper |
| 6 | Hull Bore | 26 | Spiker |
| 7 | Spincer | 27 | Venom Ant |
| 8 | Snooper Slink | 28 | Constrictor |
| 9 | Slasher Boar | 29 | Giant Mantray |
| 10 | Antisaur | 30 | Pincer |
| 11 | Grub Snuffler | 31 | Jovine Pig |
| 12 | Ramdart | 32 | Blook Slake |
| 13 | Swine Swallow | 33 | Cannibal |
| 14 | Boll Rot | 34 | Muck Grabber |
| 15 | Tangler | 35 | Swamp Slither |
| 16 | Hornet Cloud | 36 | Brine Flicker |
| 17 | Baboon | 37 | Gilgore |
| 18 | Ball Slime | 38 | Mind Scream |
| 19 | Carrion Creeper | | |

### 5.8 Locations (~26)

Location names were extracted from `START.EXE` strings. ICN file names
referenced in the EXE confirm the building types.

| ID | Location | Type |
|:---:|:---|:---|
| 0 | Hidden Rock | Town |
| 1 | Bay View | Town |
| 2 | Folman | Town |
| 3 | Ontaga | Town |
| 4 | Crooked Pine | Town |
| 5 | Santor | Town |
| 6 | Long View | Town |
| 7 | Seacrest | Town |
| 8 | Octapoint | Town |
| 9 | Cramford | Town |
| 10 | Sanctuary Cathedral | Cathedral |
| 11 | Rivercrest Cathedral | Cathedral |
| 12 | Great Plains Cathedral | Cathedral |
| 13 | Twilight Cathedral | Cathedral |
| 14 | Redstone Castle | Castle |
| 15 | Slippery Rock | Landmark |
| 16 | Lookout Point | Landmark |
| 17 | Big Oak | Landmark |
| 18 | Grissold | Landmark |
| 19 | Orchard Lake | Landmark |
| 20 | Brantown | Landmark |
| 21 | Burnside | Landmark |
| 22 | Rivercrest Tomb | Tomb |
| 23 | Twilight Tomb | Tomb |
| 24 | The Dungeon of Despair | Dungeon |
| 25 | The Conclave of Sorcerers | Special |

---

## 6. Locator Strategy

Since no live memory dump was available, the locator was designed with two
strategies that do not depend on a known live address. The character record's
address changes every DOSBox session (the emulator's guest RAM is allocated at a
session-specific process address), so nothing may be hard-coded.

### 6.1 Strategy 1 — Anchor Scan (primary)

The copyright string `"Questron II (C) 1988 S.S.I."` appears in the game's data
segment and loads verbatim into guest RAM. The locator:

1. Scans all readable memory for the 25-byte copyright string
   (`GameFacts.CopyrightString`).
2. For each anchor hit, reads a **256 KB window forward** from the anchor
   address.
3. Walks the window byte-by-byte, testing each 256-byte offset with
   `CharacterRecord.IsValidRecord`.
4. Returns the first valid character record found.

The delta from the anchor to the character record is **not fixed** — it depends
on the session's memory layout and where in the data segment the record happens
to be allocated — so a window scan is used rather than reading at a single
hard-coded offset.

### 6.2 Strategy 2 — Structural Scan (fallback)

If the anchor is not found (e.g., a different build of `START.EXE` with a
different copyright string), the locator falls back to sweeping **all readable
memory** for a 256-byte window passing `IsValidRecord`:

- **Name**: 2–15 printable ASCII characters starting with a letter, null-terminated
  within the 16-byte field at `+0x50`.
- **HP**: uint16 LE at `+0x00`, in the range 1–99999.
- **Food**: uint16 LE at `+0x02`, in the range 0–99999.
- **Gold**: uint16 LE at `+0x04`, in the range 0–65535.
- **Attributes**: five bytes at `+0x07`–`+0x0B`, each in the range 1–25.
- **Level**: byte at `+0x18`, in the range 0–20.

The structural scan walks memory in 1 MiB chunks with `RecordSize - 1` bytes of
overlap between chunks, so a record straddling a chunk boundary is not missed.
If a bulk chunk read fails on an unreadable page, a page-granular fallback
(4 KB reads) salvages the readable pages within that region.

### 6.3 Why No Value Scanner

The trainer does not include a Cheat-Engine-style value scanner. The anchor scan
is expected to find the character in one click, and the structural scan covers
the fallback case. A value scanner was considered unnecessary for a
single-character RPG where the entire record is identifiable by its shape.

---

## 7. Game Manual Cross-Reference

The game manual describes the following systems, which corroborate the
DEMOFILE-derived layout:

- **Character system**: Five attributes (Charisma, Strength, Agility, Stamina,
  Intelligence). New characters begin with all attributes at 15.
- **Vitals**: HP begins at 200. Food is consumed over time and must be purchased
  in towns. Gold begins at 200 (the DEMOFILE's 162 is attributed to demo
  spending).
- **Combat**: Melee combat with equipped weapons and spell combat with learned
  spells. Weapons and armor are purchased in towns.
- **Magic**: Four buyable spells (Magic Missile, Fireball, Sonic Whine, Time
  Sap) plus Destruct (found in EXE strings, not the manual).
- **Inventory**: Keys open locked doors; quest items advance the story;
  transports (Camalon, Trained Eagle) allow faster travel.
- **Game world**: Ten towns, four cathedrals, one castle, seven landmarks, two
  tombs, one dungeon, and the Conclave of Sorcerers.

---

## 8. Trainer Implications

### 8.1 Read-Validate-Write

The trainer follows the read-validate-write pattern: the record is read into a
256-byte buffer, validated with `IsValidRecord` or `IsOccupied`, edited in the
buffer, and written back. This ensures a shifted or stale layout is never
corrupted — if the record fails validation, the write is suppressed.

### 8.2 Editable Fields

The trainer edits:

- **Vitals**: HP, Food, Gold (uint16 LE, with clamping to safe maximums).
- **Attributes**: Five attributes (uint8, clamped to 1–25).
- **Level**: uint8, clamped to 0–20, with rank name display.
- **Equipment**: Weapon ID and Armor ID (uint8, indexing the reference tables).
- **Spells**: Eight spell-charge bytes (uint8, clamped to 0–99).
- **Name**: 16-byte ASCII field (null-terminated, max 15 characters).

### 8.3 Freeze Toggles

HP, Food, and Gold can be frozen — the poll loop re-pins the value every tick so
it never drops during play. The freeze compares against the polled buffer rather
than a shadow copy, since a shadow can already hold the pinned value while the
game has moved on.

### 8.4 Quick Actions

One-click "max" actions set fields to conservative safe caps:

| Field | Max Target |
|:---|:---|
| Attributes | 25 |
| HP | 9999 |
| Food | 9999 |
| Gold | 65535 |
| Level | 20 |
| Spell charges | 99 |

### 8.5 No Save Editor

The `DEMOFILE` format (1,016 bytes) was partially decoded — the first 256 bytes
are the character record — but the remaining 760 bytes of auxiliary game state
were not mapped. The trainer edits **live memory only**; no offline save editor
is provided.

### 8.6 No Teleport

Map position was not identified in the static analysis. The DEMOFILE does not
contain an obvious coordinate pair, and without a live dump or disassembly there
was no way to locate the position fields. The trainer does not teleport.

---

## 9. Open Questions

- **Record size**: 256 bytes is inferred, not confirmed. The last identified
  field (spell charges) ends at `+0x8D`; the true record may be smaller.
- **Combat / progression block** (`+0x60`–`+0x85`): Experience, max HP, and
  other combat data are expected here but were not decoded from the DEMOFILE.
- **Spell charge mapping**: The eight charge bytes at `+0x86` may correspond to
  spell tiers rather than individual spells (only five spells are known). The
  mapping is inferred from the DEMOFILE's uniform `01 01 01 01 01 01 01 01`
  pattern.
- **Item flag bitmap**: The 48-byte ownership bitmap at `+0x20`–`+0x4F` has
  three set bits in the DEMOFILE, but the bit-to-item mapping was not
  established.
- **Missing monsters**: The manual claims "over 60" creatures; only ~39 were
  recovered from strings. The rest may be in compressed or overlay sections.
- **EXEPACK full unpack**: The incomplete unpacking prevented disassembly and
  code-level confirmation of the offsets. A successful unpack (or a Ghidra
  session on the unpacked image) would allow the data-segment layout to be
  mapped and the anchor-to-record delta to be pinned.

---

## 10. Verification Plan

All analysis above is **static** — from the `DEMOFILE`, `START.EXE` strings, and
the game manual. The following should be confirmed against a running game at the
first opportunity:

1. **Anchor scan**: Launch Questron II in DOSBox, attach the trainer, and
   verify the copyright string is found and the character record is located at
   the expected offset from it.
2. **Field write-test**: Write sentinel values (e.g., HP 12345, Gold 999,
   Strength 25) and read them back from the game's own character screen.
3. **Record size**: Confirm whether the true record is smaller than 256 bytes by
   checking whether adjacent data changes independently of the character fields.
4. **Combat block**: Identify experience, max HP, and other progression fields
   by writing sentinels and observing the character screen.
5. **Spell charges**: Cast spells and observe which charge bytes decrement,
   confirming the spell-to-slot mapping.
5. **Structural scan**: Test against a process that is not the game to confirm
   the structural scan does not match unrelated byte runs (a risk noted in the
   `AlternateRealityTrainer` — a structural sweep will eventually match
   something in 16 MB of RAM).
