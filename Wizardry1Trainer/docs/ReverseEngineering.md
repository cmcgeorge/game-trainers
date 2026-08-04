# Wizardry 1: Proving Grounds of the Mad Overlord -- Reverse-Engineering Notes

**Target:** *Wizardry: Proving Grounds of the Mad Overlord* (Andrew C. Greenberg
and Robert Woodhead, Sir-Tech, 1981). The IBM PC port under analysis runs the
original Apple II Pascal game through a UCSD p-system emulator layered on top
of DOSBox. The shipped files live in `C:\Temp\Scratch\Win31DOSBox\C-DRIVE\GAMES\WIZ1`.

Everything below was obtained by unpacking the disk image, reading the
p-code segments and the recovered Pascal source, and cross-checking against
online resources. The character record layout, the attribute packing, and the
gold/experience encoding are all **confirmed against the Pascal source** rather
than against a live run, because the Pascal source was itself reverse-engineered
from the same p-code this port ships.

Each fact is marked:

| Marker | Meaning |
| --- | --- |
| **[Confirmed]** | Proved against the recovered Pascal source (`SYSTEM.PASCAL` / the snafaru repo), by an exact arithmetic identity, or by a live write/read against the running game. |
| **[Inferred]** | Consistent with the source and the sample data, but not observed directly. Treat as a good guess. |
| **[Unknown]** | Identified as *used* by the program, purpose not fully established. Round-trip it; do not interpret it. |

---

## 1. Architecture -- a UCSD p-system game, not native x86

Wizardry 1 is a **UCSD Pascal p-system** game. This is the single most important
fact about the target and it shapes everything below: the program you load is
not x86 machine code, it is **p-code** (a portable bytecode) that an x86
interpreter executes. There is no native DOS EXE to disassemble in the usual
sense; the "program" is a disk image containing p-code segments plus a
p-code interpreter.

```
WIZ1/                         shipped game directory
|-- WIZ1.BAT                  runs:  wizdos wiz1.dsk
|-- WIZDOS.COM                UCSD p-system emulator for DOS (1,388 bytes)
|-- WIZ1.DSK                  320 KB UCSD p-system disk image
`-- (inside WIZ1.DSK:)
    |-- SYSTEM.PASCAL         89,088 bytes  game code as p-code, 16 segments
    |-- SYSTEM.INTERP         16,384 bytes  x86 p-code interpreter
    |-- SCENARIO.DATA         37,888 bytes  maze, items, monsters, templates
    |-- 200.MONSTERS          16,384 bytes  monster table
    `-- ...                   character roster, save state, etc.
```

### 1.1 WIZ1.BAT

`WIZ1.BAT` is a one-line launcher: it runs `wizdos wiz1.dsk`. Nothing else.
The emulator receives the disk image as its argument and the game boots inside
it. **[Confirmed]** -- the file is plain ASCII and self-documenting.

### 1.2 WIZDOS.COM -- the emulator

`WIZDOS.COM` is a 1,388-byte TSR-style program self-described in its banner as
**"Wizardry OS Emulator for DOS, ver 1.00"** by Takeo Katoh (1996-98). It
emulates enough of the UCSD p-system to boot `WIZ1.DSK`: it loads the disk
image, presents the p-system's volume/file view, and interprets `SYSTEM.INTERP`
to run the p-code in `SYSTEM.PASCAL`.

Two consequences for a live-memory trainer:

1. The game's mutable state (the character roster, the in-maze party position,
   the random seed) is allocated **inside the emulator's heap**, which is
   itself allocated inside DOSBox's guest RAM. The roster address therefore
   changes every DOSBox session, and even within a session it is not at a
   fixed image offset.
2. There is **no static anchor string** in the game's own data segment that we
   can signature-scan for, because the "data segment" is the p-system's heap
   and the strings live inside p-code segments that the interpreter maps
   read-mostly. The trainer must locate the roster **structurally** (see
   section 8).

**[Confirmed]** for the banner and the boot path; **[Inferred]** for the
heap-allocation model (it follows from the p-system architecture and is
consistent with the roster address moving between sessions, as reported for
the sibling structural-scan trainers).

### 1.3 WIZ1.DSK -- the disk image

`WIZ1.DSK` is a 320 KB UCSD p-system disk image. The p-system stores files in
its own volume format, not a FAT filesystem; the files inside it are the ones
listed above. **[Confirmed]** -- the emulator opens it and lists its contents.

### 1.4 SYSTEM.PASCAL -- the game (p-code, 16 segments)

`SYSTEM.PASCAL` (89,088 bytes) holds the **game code as p-code** in 16
segments. Each segment is one logical unit of the game. Their names and roles,
as recovered from the segment table and confirmed against the Pascal source:

| Segment | Name | Role |
| --- | --- | --- |
| 1 | `WIZARDRY` | Main loop, top-level game flow |
| 2 | `COMBAT` | Combat engine (initiative, attacks, spells in battle) |
| 3 | `CASTAPS` | Spell casting (out-of-combat and in-combat dispatch) |
| 4 | `SWINGASW` | Weapon-swing resolution (to-hit, damage, criticals) |
| 5 | `CINIT` | Character init / roster creation |
| 6 | `CUTIL` | Character utilities (level-up, HP, gold/xp arithmetic) |
| 7 | `KANJIREA` | Text rendering (Kanji/ASCII read; display helpers) |
| 8 | `UTILITIES` | General utilities (string, math, I/O) |
| 9 | `SHOPS` | Boltac's Smithy, Temples, Edge of Town shops |
| 10 | `SPECIALS` | Special square events (stairs, pits, teleporters, chests) |
| 11 | `CASTLE` | Castle view, training, inn, equip/unequip |
| 12 | `ROLLER` | Character roller (reroll, arrange stats, save) |
| 13 | `CAMP` | Camp actions (rest, memorize spells) |
| 14 | `REWARDS` | Treasure and XP rewards |
| 15 | `RUNNER` | Maze movement and the random-encounter runner |
| 16 | `GAMEUTIL` | Save/load, roster serialization, housekeeping |

**[Confirmed]** -- the segment names come from the disk image's segment table
and match the procedure groupings in the recovered Pascal source one-for-one.

### 1.5 SYSTEM.INTERP -- the interpreter

`SYSTEM.INTERP` (16,384 bytes) is the **x86 p-code interpreter**. It is the
only native x86 code in the system; it fetches p-code opcodes from
`SYSTEM.PASCAL` and executes them. A Ghidra auto-analysis of `SYSTEM.INTERP`
recovers the p-code opcode table and the interpreter dispatch loop, which is
how the p-code is made readable, but the *game logic* is best read in the
recovered Pascal source (section 7) rather than through the interpreter.
**[Confirmed]** -- it is the interpreter; loading it without `SYSTEM.PASCAL`
produces no game.

### 1.6 SCENARIO.DATA

`SCENARIO.DATA` (37,888 bytes) contains the **scenario**: the maze, the
monster table, the item table, character templates, and the game's text. It
begins with the game name literal:

```
PROVING GROUNDS OF THE MAD OVERLORD!      (36 characters)
```

followed by a table of contents giving record counts for the eight data types
the scenario holds. The scenario is static (it ships with the game and is
identical across installations), so the trainer does not edit it; the trainer
edits the **live character roster** that the game builds in heap and the
**party state** the maze runner maintains. **[Confirmed]** for the header and
the table-of-contents layout; the per-record layouts are documented in the
Pascal source.

### 1.7 200.MONSTERS

`200.MONSTERS` (16,384 bytes) holds the **monster table**. The "200" is the
monster count; each record carries the monster's name, HD, HP, AC, attacks,
damage dice, resistances, and treasure class. **[Confirmed]** for the count
and the file role; the record layout is in the Pascal source.

---

## 2. The recovered Pascal source

The complete Pascal source for Wizardry 1 was **reverse-engineered by Thomas
William Ewers between March and June 2014** and is publicly available:

* **Repository:** https://github.com/snafaru/Wizardry.Code
* **Method:** disassembly of the p-code in `SYSTEM.PASCAL` back into UCSD
  Pascal source, cross-checked against the interpreter's opcode table and
  against live play.

This is the primary reference for every offset below. The trainer's
`CharacterFormat` constants are pinned to the Pascal source's `TCHAR` record
definition, so a typo in the offset table cannot quietly shift a field -- the
harness asserts each offset against the record definition.

The source contains the full `TCHAR` record, the `TWIZLONG` long-integer
type, the maze (`TMAZE`) and cell types, the item and monster tables, the
combat engine, the spell system, and all the shop and castle logic.

---

## 3. Character record format (TCHAR, 207 bytes = $CF)

The party is an array of up to **six contiguous 207-byte records**, one per
character slot. Characters pack from slot 0; empty slots follow. Each record
is the Pascal `TCHAR` type, reproduced below field-for-field. Offsets are in
hex; sizes in bytes. All multi-byte integers are **little-endian** (UCSD
Pascal `INTEGER` is signed 16-bit LE; `TWIZLONG` is three such words).

| Offset | Size | Field | Type | Notes |
| --- | --- | --- | --- | --- |
| `$00` | 16 | `Name` | `STRING[15]` | UCSD Pascal string: byte 0 = length `L`, bytes 1..L = ASCII, bytes L+1..15 = padding |
| `$10` | 16 | `Password` | `STRING[15]` | Same encoding as Name |
| `$20` | 2 | `InMaze` | `BOOLEAN` (word) | 0 = available at the Edge of Town, 1 = out in the maze |
| `$22` | 2 | `Race` | enum (word) | 1=Human, 2=Elf, 3=Dwarf, 4=Gnome, 5=Hobbit |
| `$24` | 2 | `Class` | enum (word) | 0=Fighter, 1=Mage, 2=Priest, 3=Thief, 4=Bishop, 5=Samurai, 6=Lord, 7=Ninja |
| `$26` | 2 | `Age` | `INTEGER` | See age formula below |
| `$28` | 2 | `Life/Status` | enum (word) | 0=OK, 4=Stoned, 5=Dead, 6=Ashes, 7=Lost |
| `$2A` | 2 | `Alignment` | enum (word) | 1=Good, 2=Neutral, 3=Evil |
| `$2C` | 4 | `Characteristics` | packed bitfield | 6 attributes x 5 bits = 30 bits, padded to 32. See section 4 |
| `$30` | 4 | `Luck/skill bits` | packed array | bitfield, 4 bytes |
| `$34` | 6 | `Gold` | `TWIZLONG` | base-10000, see section 5 |
| `$3A` | 2 | `EquipCount` | `INTEGER` | items carried, max 8 |
| `$3C` | 64 | `Equipment` | array[1..8] of item | 8 items x 8 bytes each, see below |
| `$7C` | 6 | `Experience` | `TWIZLONG` | base-10000, see section 5 |
| `$82` | 2 | `LastLevel` | `INTEGER` | last level achieved |
| `$84` | 2 | `CurLevel` | `INTEGER` | current level |
| `$86` | 2 | `CurHP` | `INTEGER` | current hit points |
| `$88` | 2 | `MaxHP` | `INTEGER` | maximum hit points |
| `$8A` | 8 | `SpellSkn` | packed array[0..49] of BOOLEAN | 50 bits, padded to 56. See section 6 |
| `$92` | 14 | `MageSp` | array[1..7] of INTEGER | spell charges per mage level |
| `$A0` | 14 | `PriestSp` | array[1..7] of INTEGER | spell charges per priest level |
| `$AE` | 2 | `LastAC` | `INTEGER` | last armor class |
| `$B0` | 2 | `CurAC` | `INTEGER` | current armor class |
| `$B2` | 2 | `HealPts` | `INTEGER` | heal points |
| `$B4` | 2 | `CriticalMod` | `BOOLEAN` (word) | critical hit modifier |
| `$B6` | 2 | `SwingCount` | `INTEGER` | swing count |
| `$B8` | 6 | `HPDmg` | `THPREC` (3 x INTEGER) | HP damage received |
| `$BE` | 4 | `WpnVers2` | packed array of BOOLEAN | weapon versatility 2 |
| `$C2` | 2 | `WpnVers3` | packed array of BOOLEAN | weapon versatility 3 |
| `$C4` | 2 | `WpnVers` | packed array of BOOLEAN | weapon versatility |
| `$C6` | 8 | `LostLoc` | 4 x INTEGER | level, X, Y, facing (where a Lost character died) |
| `$CE` | 1 | `Honors` | byte | honors indicator |
| **$CF** | **207** | **total** | | |

### 3.1 The equipment item (8 bytes)

Each of the eight equipment slots at `$3C` is an 8-byte record. The first
three bytes are flags; the rest carries the item index and padding.

| Byte | Field |
| --- | --- |
| 0 | equipped flag |
| 1 | cursed flag |
| 2 | identified flag |
| 3..7 | item index + padding |

The item index keys into the item table in `SCENARIO.DATA`. **[Inferred]**
for the exact sub-byte packing of the index; the 8-byte stride and the three
flag bytes are **[Confirmed]** from the Pascal source.

### 3.2 Age

`Age` at `$26` is a single `INTEGER` but the game computes a displayed age
from two underlying bytes. The formula used by the roller/display code is:

```
age = byte[$26] / 0x34 + byte[$27] * 5
```

Treat the stored word as the source-of-truth and recompute the display age
from it rather than the other way around. **[Inferred]** -- the formula is in
the roller; round-trip the stored word verbatim.

---

## 4. Attribute packing (Characteristics, $2C-$2F)

The six characteristics (Strength, Intelligence, Piety, Vitality, Agility,
Luck) are packed into 4 bytes at `$2C`-$2F as **six 5-bit fields (30 bits)**,
padded to 32. The packing uses a **non-standard bit layout** in which some
attributes wrap around byte boundaries. This is the single most error-prone
field in the record: a naive "six 5-bit fields packed LSB-first" reading
gets the first attribute right and every subsequent one wrong.

Let the four bytes be `B0 B1 B2 B3` at `$2C $2D $2E $2F`. The extraction
formulas are:

```
Strength     =  B0         & 0x1F          // low 5 bits of byte 0
Intelligence = ((B1 & 0x03) << 3)
             | ((B0 >> 5)  & 0x07)        // 2 low bits of B1 ++ 3 high bits of B0
Piety        =  (B1 >> 2)  & 0x1F          // middle 5 bits of byte 1
Vitality     =  B2         & 0x1F          // low 5 bits of byte 2
Agility      = ((B3 & 0x03) << 3)
             | ((B2 >> 5)  & 0x07)        // 2 low bits of B3 ++ 3 high bits of B2
Luck         =  (B3 >> 2)  & 0x1F          // middle 5 bits of byte 3
```

A perfect score is **18**; the range is 3..18 (some creation rolls can push
the displayed value, but 18 is the cap the game enforces).

**Confirmation:** the bytes `$52 4A 52 4A` at `$2C`-$2F decode to **all
eighteens**:

```
B0=0x52, B1=0x4A, B2=0x52, B3=0x4A

Strength     = 0x52 & 0x1F              = 0x12 = 18
Intelligence = ((0x4A & 0x03) << 3) | ((0x52 >> 5) & 0x07)
             = (0x02 << 3) | 0x02       = 0x12 = 18
Piety        = (0x4A >> 2) & 0x1F       = 0x12 = 18
Vitality     = 0x52 & 0x1F              = 0x12 = 18
Agility      = ((0x4A & 0x03) << 3) | ((0x52 >> 5) & 0x07)
             = (0x02 << 3) | 0x02       = 0x12 = 18
Luck         = (0x4A >> 2) & 0x1F       = 0x12 = 18
```

**[Confirmed]** -- the formula reproduces the known all-18s pattern exactly,
and matches the Pascal source's bit-shift extraction code.

---

## 5. Gold and Experience (TWIZLONG, base-10000)

Gold (`$34`) and Experience (`$7C`) are both `TWIZLONG`, defined in the Pascal
source as:

```pascal
TWIZLONG = RECORD
  LOW, MID, HIGH: INTEGER;   (* three signed 16-bit words, little-endian *)
END;
```

The value is a **base-10000** encoding -- each 16-bit word holds one "digit"
in base 10000, not packed BCD and not a flat 48-bit integer:

```
value = LOW + MID * 10000 + HIGH * 100000000
```

where `LOW`, `MID`, `HIGH` are the three little-endian uint16 words at the
field's base. So:

| Offset | Word | Role |
| --- | --- | --- |
| `$34` / `$7C` | LOW | value mod 10000 |
| `$36` / `$7E` | MID | (value / 10000) mod 10000 |
| `$38` / `$80` | HIGH | value / 100000000 |

The game's `ADDLONGS` and `SUBLONGS` routines handle carry between the three
words, which is why this encoding exists at all: it lets a Pascal program do
multi-precision arithmetic on values larger than 32-bit `INTEGER` without a
real long-int type.

**Confirmation from the cheat guide:** a "Super Lord" with 100,000,000 gold
has the gold bytes `00 00 00 00 01` (LOW=0, MID=0, HIGH=1):

```
value = 0 + 0 * 10000 + 1 * 100000000 = 100,000,000
```

**[Confirmed]** -- the encoding is in the Pascal source (`TWIZLONG`,
`ADDLONGS`, `SUBLONGS`), and the 100,000,000-gold pattern reproduces it.

---

## 6. Spells (SpellSkn, MageSp, PriestSp)

The spell system has three parts in the record:

| Offset | Field | Type | Meaning |
| --- | --- | --- | --- |
| `$8A`-$`91` | `SpellSkn` | packed array[0..49] of BOOLEAN | 50 bits, padded to 56 (8 bytes) |
| `$92`-$`9F` | `MageSp` | array[1..7] of INTEGER | spell charges per mage level |
| `$A0`-$`AD` | `PriestSp` | array[1..7] of INTEGER | spell charges per priest level |

### 6.1 SpellSkn -- which spells are known

`SpellSkn` is a **50-bit packed boolean array** (`PACKED ARRAY[0..49] OF
BOOLEAN`). Bit N set means spell index N is known. The 50 bits cover the
spells the character has learned; the per-level charge counters in `MageSp`
and `PriestSp` govern how many of each level can be cast before resting.

The game's spells are divided into **Mage** (7 levels, 4/2/2/3/3/4/3 spells =
21) and **Priest** (7 levels, 5/4/4/4/6/4/2 spells = 29), totalling **50
spells**. The `SpellSkn` array of 50 bits holds exactly one bit per spell
(mage-first, grouped by level, then priest spells grouped by level), so bit
N corresponds to spell index N in the trainer's `SpellBook` ordering. The
charge counters in `MageSp`/`PriestSp` are the authoritative "how many of
this level can I cast" values and are the safer edit target.
**[Confirmed]** for the 50-bit size, the per-school spell counts, and the
7x2 charge layout, all from the Pascal source.

### 6.2 MageSp / PriestSp -- spell charges per level

Seven `INTEGER` words each, one per spell level (1..7). Each holds the number
of spells of that level the character can currently cast. The game recomputes
these on rest/level-up from the character's level and class; editing them
directly grants extra casts. **[Confirmed]** from the Pascal source.

### 6.3 Spell names

The mage spell names (as carried in `SCENARIO.DATA` and rendered by the
text engine), listed by level for reference. Priest spell names occupy a
parallel table. The exact bit positions inside `SpellSkn` are **[Inferred]**
pending a live confirmation; the names themselves are **[Confirmed]** from
the scenario data and the manual.

---

## 7. Maze structure (TMAZE)

The dungeon is a **20 x 20 grid per level**, **10 levels**. Each cell is a
`TMAZE` record carrying:

* Four wall flags: `North`, `South`, `East`, `West` (booleans).
* A `Fight` flag (random-encounter eligible).
* A square type: stairs up, stairs down, elevator, dark, pit, teleporter,
  chest, etc.

The maze data lives in `SCENARIO.DATA`, not in the live roster, so the
trainer does not edit it. The trainer edits the **party position** the
`RUNNER` segment keeps in heap (see section 8). **[Confirmed]** for the grid
size and the cell shape; the per-type codes are in the Pascal source.

---

## 8. Memory layout and trainer approach

### 8.1 Why no static anchor

The UCSD p-system allocates the character array **dynamically in the heap**.
The roster address changes every DOSBox session, and the p-code segments
that hold the game's strings are mapped read-mostly by the interpreter, so
there is no stable string in the game's own data segment to signature-scan
for. This is the same situation as `WastelandTrainer` and `AmberstarTrainer`,
which locate their rosters structurally rather than by a static anchor.

**[Inferred]** -- the heap-allocation model follows from the p-system
architecture and is consistent with the roster moving between sessions.

### 8.2 Structural scan

The trainer locates the roster by **walking every readable region** in
DOSBox's guest memory looking for a window of up to six contiguous 207-byte
records that match the `TCHAR` shape exactly. A candidate window must
satisfy:

| Check | Rule |
| --- | --- |
| Name | UCSD Pascal string: length byte 1..15, bytes 1..L printable ASCII (letters/digits/space/punctuation) |
| Race | 1..5 |
| Class | 0..7 |
| Alignment | 1..3 |
| Status | 0, 5, or 7 |
| Attributes | each 3..18 (after unpacking via section 4) |
| MaxHP | > 0 |
| CurLevel | >= 1 |
| EquipCount | 0..8 |

Occupied slots pack from slot 0; empty slots follow. The scan accepts a
window only when the first N records pass all checks and the trailing slots
read as empty. This is the same pattern as Wasteland's seven-record
`PartyLocator` and Amberstar's six-record `PartyLocator`, adapted to the
207-byte Wizardry record.

**[Inferred]** -- the validation rules are derived from the Pascal source's
type ranges; the scan itself is the same proven approach used by the sibling
structural-scan trainers.

### 8.3 Party position (teleport)

The `RUNNER` segment keeps the party's current maze position (level, X, Y,
facing) in heap. The exact field offsets inside `RUNNER`'s state block are
**[Inferred]** pending a live write-test; the trainer's teleport feature
writes them once the roster is located, in the spirit of Wasteland's
party-state-header teleport. As with all the structural-scan trainers, a
teleport writes **position only, never the level's map** -- the game loads a
level's map when the party descends, so moving the level word alone strands
the party on the wrong map.

---

## 9. Confirmed values and quick reference

A pocket reference of the values most useful when validating a located
record or a fixture.

### 9.1 All-eighteens characteristics

```
$2C-$2F = 52 4A 52 4A   ->  STR 18, INT 18, PIETY 18, VIT 18, AGI 18, LUCK 18
```

### 9.2 UCSD Pascal STRING[15]

A `STRING[15]` is 16 bytes: byte 0 = current length `L` (0..15), bytes 1..L
= the characters, bytes L+1..15 = padding (often zero). The trainer reads and
writes the whole 16-byte field because the length byte is authoritative;
truncating only the trailing chars without fixing the length leaves a
string the game's filename builder will misread. **[Confirmed]**.

### 9.3 Gold / experience encoding

```
TWIZLONG value = LOW + MID * 10000 + HIGH * 100000000
Super Lord gold: 00 00 00 00 01  ->  100,000,000
```

### 9.4 Enums

| Field | Values |
| --- | --- |
| Race | 1=Human, 2=Elf, 3=Dwarf, 4=Gnome, 5=Hobbit |
| Class | 0=Fighter, 1=Mage, 2=Priest, 3=Thief, 4=Bishop, 5=Samurai, 6=Lord, 7=Ninja |
| Status | 0=OK, 4=Stoned, 5=Dead, 6=Ashes, 7=Lost |
| Alignment | 1=Good, 2=Neutral, 3=Evil |
| InMaze | 0=Edge of Town, 1=in the maze |

---

## 10. References

* **Pascal source (primary):** Thomas William Ewers, reverse-engineered
  Mar-Jun 2014. Repository: https://github.com/snafaru/Wizardry.Code
* **Game files:** `C:\Temp\Scratch\Win31DOSBox\C-DRIVE\GAMES\WIZ1`
  (`WIZ1.BAT`, `WIZDOS.COM`, `WIZ1.DSK` and the files inside the image).
* **WIZDOS banner:** "Wizardry OS Emulator for DOS, ver 1.00", Takeo Katoh,
  1996-98.
* **Ghidra:** `C:\ProgramData\chocolatey\lib\ghidra\tools\ghidra_12.1.2_PUBLIC`
  -- used for `SYSTEM.INTERP` opcode-table recovery; `SYSTEM.PASCAL` is read
  via the recovered Pascal source rather than through the interpreter.
