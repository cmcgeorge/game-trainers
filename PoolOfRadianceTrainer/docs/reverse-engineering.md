# Pool of Radiance — Reverse-Engineering Notes

Technical write-up of how *Pool of Radiance* (SSI, 1988 — the first AD&D "Gold Box" game)
stores its party in memory, how that was recovered from DOSBox-X memory dumps, and how the
trainer reads and writes it. Every offset in the character-record table below was confirmed
**two independent ways** — by differential analysis of live memory dumps of a real party, and
against community documentation — and the parser is regression-tested against verbatim bytes
from those dumps (`test/FormatCheck`).

---

## 1. Source material

| Artifact | What it is |
|---|---|
| `.data/dosbox-x-8168-20260708-081948-110.bin` (367 MB) | Full DOSBox-X **process** dump, party **exploring** the Slums (0,4), facing W, 10:50. |
| `.data/dosbox-x-8168-20260708-082313-212.bin` (352 MB) | Full DOSBox-X process dump, party **in combat**, Rhiannon **unconscious**, vs 6 Orcs (5 HP each). |
| `.data/*.csv` | Region index for each dump: `FileOffset, ProcessAddress, Size, Protection, Type`. |
| `.game/` | The game itself: `START.EXE`, `GAME.OVR`, `*.DAX` resource archives, `POOL.CFG`. |

These are dumps of the **DOSBox-X emulator process**, not of the guest (DOS) machine directly.
The emulated PC's RAM lives as a large committed block inside the emulator's address space, so
the game's data structures sit somewhere inside the `.bin` at an offset that changes every run.
The `.csv` maps each file offset to the process virtual address it was captured from, which lets
us report meaningful addresses and diff two dumps *by address* rather than by file offset.

---

## 2. Finding the party in the dump

The six party members are: **Thrender Grone, Bakshi, Rhiannon, Brother Sean, Darkstar, Phineas**.

Gold Box games store a character's name as a **Pascal string** (a length byte followed by up to
15 ASCII bytes), stored **in upper-case** as the game draws them. Searching the dump for the
length-prefixed name (e.g. `0E "THRENDER GRONE"`) locates each record immediately:

```
THRENDER GRONE : @ process address 0x1F1791489D8
BAKSHI         : @ 0x1F179148BE8
RHIANNON       : @ 0x1F179148D98
BROTHER SEAN   : @ 0x1F179148F88
DARKSTAR       : @ 0x1F179149168
PHINEAS        : @ 0x1F1791492C8
```

Crucially, the records sit at the **same process address in both dumps** (only the file offset
differs). The emulated RAM base is stable within a DOSBox session, which is why the trainer can
poll and freeze reliably once it has located a record. Across a DOSBox restart the base moves, so
the trainer never hard-codes an address — it **signature-scans** for the record shape instead
(see §6).

Immediately after each name are the six ability scores, so the anchor is unambiguous. The bulk of
the space *between* consecutive party members is the character's **combat-icon sprite** (bitmap
runs like `3F FF FC …`) and a **linked list of carried-item instances** (each item carries its own
name, e.g. `"Flail Flail"`, `"Banded Mail  Mail"`, `"Two-Handed Sword Sword"`). Because that trailing
data is variable-length, records are **not** at a fixed stride — another reason to locate them by
signature, not stride.

---

## 3. The character record — 0x11D (285) bytes

Each character (and each monster — see §5) is a fixed **285-byte** record. Offsets are relative to
the record start (the name-length byte). Fields the trainer edits are shown in **bold**.

| Offset | Size | Field | Notes |
|-------:|:----:|-------|-------|
| `0x00` | 1 | **name length** | Pascal string length (1–15) |
| `0x01`–`0x0F` | 15 | **name** | ASCII, NUL-padded |
| `0x10` | 1 | **Strength** | |
| `0x11` | 1 | **Intelligence** | |
| `0x12` | 1 | **Wisdom** | |
| `0x13` | 1 | **Dexterity** | |
| `0x14` | 1 | **Constitution** | |
| `0x15` | 1 | **Charisma** | |
| `0x16` | 1 | **Str exceptional %** | 1–100 → 18/01–18/00; 0 = none (fighters only) |
| `0x17`–`0x2B` | 21 | memorized spells | one slot per memorized spell |
| `0x2D` | 1 | **THAC0 base** | stored as `60 − value` (see §4) |
| `0x2E` | 1 | **race** | enum below |
| `0x2F` | 1 | **class** | enum below (incl. multiclass) |
| `0x30`–`0x31` | 2 | **age** | UInt16 LE |
| `0x32` | 1 | **HP maximum** | |
| `0x33`–`0x69` | 55 | known spells | one flag byte per learnable spell (cleric/mage L1–3) |
| `0x6B` | 1 | attack level | |
| `0x6D`–`0x71` | 5 | **saving throws** | para/poison/death, petrify/polymorph, rod/staff/wand, breath, spell |
| `0x72` | 1 | movement base | |
| `0x73` | 1 | level (highest class) | |
| `0x74` | 1 | drained levels | level drain from undead |
| `0x75` | 1 | drained HP | |
| `0x77`–`0x7E` | 8 | **thief skills** | pick pockets, open locks, find/remove traps, move silently, hide, hear, climb, read languages |
| `0x7F`–`0x82` | 4 | effects list pointer | far pointer into guest RAM |
| `0x84` | 1 | NPC flag | |
| `0x85` | 1 | modified flag | set when the character was edited |
| `0x88`–`0x89` | 2 | **copper** | UInt16 |
| `0x8A`–`0x8B` | 2 | **silver** | |
| `0x8C`–`0x8D` | 2 | **electrum** | |
| `0x8E`–`0x8F` | 2 | **gold** | |
| `0x90`–`0x91` | 2 | **platinum** | 1 pp = 5 gp |
| `0x92`–`0x93` | 2 | **gems** | count |
| `0x94`–`0x95` | 2 | **jewelry** | count |
| `0x96`–`0x9D` | 8 | **class levels** | cleric, druid, fighter, paladin, ranger, mage, thief, monk |
| `0x9E` | 1 | **gender** | 0 male / 1 female |
| `0xA0` | 1 | **alignment** | enum below |
| `0xA9` | 1 | AC base | stored `60 − value`; the unarmored 10 baseline |
| `0xAC`–`0xAF` | 4 | **experience** | UInt32 LE — a single total, not per-class |
| `0xB1` | 1 | HP rolled | raw die roll before CON bonus/draining |
| `0xB2`–`0xB4` | 3 | cleric spells/day | L1–3 |
| `0xB5`–`0xB7` | 3 | mage spells/day | L1–3 |
| `0xB8`–`0xB9` | 2 | **XP award** | XP granted for killing this creature (monsters) |
| `0xC7` | 1 | number of items | |
| `0xC8`–`0xCB` | 4 | items list pointer | linked list |
| `0xCC`–`0xFF` | 4×13 | equipped-item pointers | weapon, shield, armor, gauntlets, helm, belt, robe, cloak, boots, ring1, ring2, arrows, bolts |
| `0x102`–`0x103` | 2 | encumbrance | |
| `0x104`–`0x107` | 4 | next-character pointer | the party is a linked list in memory |
| `0x108`–`0x10B` | 4 | combat struct pointer | valid during combat |
| `0x10C` | 1 | **status** | 0 = okay (enum below) |
| `0x110` | 1 | **THAC0 current** | effective, `60 − value` |
| `0x111` | 1 | **AC current** | effective, `60 − value` — what the game shows |
| `0x11B` | 1 | **HP current** | the live current HP |
| `0x11C` | 1 | movement current | |

### Enumerations

- **Race** (`0x2E`): 0 monster · 1 dwarf · 2 elf · 3 gnome · 4 half-elf · 5 halfling · 6 half-orc · 7 human
- **Class** (`0x2F`): 0 cleric · 1 druid · 2 fighter · 3 paladin · 4 ranger · 5 mage · 6 thief · 7 monk · 8 C/F · 9 C/F/M · A C/R · B C/M · C C/T · D F/M · E F/T · F F/M/T · 10 M/T · 11 monster
- **Alignment** (`0xA0`): 0 LG · 1 LN · 2 LE · 3 NG · 4 TN · 5 NE · 6 CG · 7 CN · 8 CE
- **Gender** (`0x9E`): 0 male · 1 female
- **Status** (`0x10C`): 0 okay · 1 animated · 2 tempgone · 3 running · 4 unconscious · 5 dying · 6 dead · 7 stoned · 8 gone

---

## 4. The AC / THAC0 "60 − x" encoding

AD&D uses *descending* Armor Class and THAC0 (lower is better). The engine stores both as
`60 − displayed`, so that internally **higher = better** even though the displayed number
descends. To read a displayed value, compute `60 − storedByte`; to set displayed `X`, write
`60 − X`. This was confirmed empirically:

- Thrender's effective AC byte at `0x111` = `59` → displayed `60 − 59 = 1` (a dwarf fighter in
  banded mail — correct).
- His AC-base byte at `0xA9` = `50` → `60 − 50 = 10` (the naked baseline).
- His current-THAC0 byte at `0x110` = `41` → `60 − 41 = 19` (level-1 fighter — correct).

The record keeps a *base* AC/THAC0 (the 10/20 unarmored baseline at `0xA9`/`0x2D`) and a *current*
AC/THAC0 (the effective value including equipment at `0x111`/`0x110`). The trainer shows the
**effective** value and, when you edit it, writes **both** so an equipment recompute can't quietly
revert your change.

---

## 5. Verification — the decoded sample party

Applying the table above to the "exploring" dump decodes the whole party to values that are
mutually consistent (casters have spell data, ages match racial lifespans, level-1 party shares
~30 XP and pooled starting money):

```
THRENDER GRONE  Male   Dwarf     Fighter               STR 17  HP 11/11  AC 1  THAC0 19  age 52   XP 32
BAKSHI          Male   Half-Elf  Cleric/Fighter/Mage   STR 18/90 HP 7/7  AC 4  THAC0 18  age 48   XP 10
RHIANNON        Female Elf       Fighter/Mage          STR 15  HP 7/7    AC 0  THAC0 20  age 180  XP 14
BROTHER SEAN    Male   Human     Cleric                WIS 17  HP 10/10  AC 2  THAC0 20  age 22   XP 32
DARKSTAR        Female Human     Mage                  INT 18  HP 5/5    AC 7  THAC0 20  age 27   XP 32
PHINEAS         Male   Halfling  Thief                 DEX 18  HP 6/6    AC 4  THAC0 20  age 46   XP 32
```

Notice the details that fall out for free and cross-check the layout: only humans/half-elves are
clerics (Brother Sean, Bakshi); the mage Darkstar is unarmored (AC 7); the dwarf is old (52) and
the elf ancient (180); Bakshi's exceptional strength byte is `0x5A` = 90, i.e. **18/90**.

### The combat dump proves the live fields

Diffing the two dumps *by process address* shows the party header (name, stats, race, class,
money) is byte-identical between exploring and combat — those are persistent. The **live combat
fields** differ exactly as expected:

```
RHIANNON  explore:  HP current (0x11B) = 7,  status (0x10C) = okay
RHIANNON  combat:   HP current (0x11B) = 0,  status (0x10C) = unconscious
```

That single diff — HP 7 → 0 and status okay → **unconscious** — matches the dump's own note
("Rhiannon is unconscious") and nails down `0x11B` (current HP) and `0x10C` (status).

### Monsters share the record

Monsters use the **identical 285-byte record**. In the combat dump the six orcs appear as records
named `"ORC"` with a low Intelligence byte (6), and each reads **HP = 5** at the current-HP field —
matching the dump note ("6 Orcs, each with 5 Hit Points"). Because monsters and characters share a
format, the trainer's combat panel can enumerate and edit enemies exactly like party members.

---

## 5a. Carried items — the `CHRDATAn.ITM` file

Each character's inventory lives in a sibling save file, **`CHRDATAn.ITM`**, as a flat array of
fixed **63-byte (`0x3F`)** item records — no header, so `record count = file size / 63`. The
character record's item-count byte (`0xC7`) and the runtime item/equip pointers (`0xC8`,
`0xCC`–`0xFF`) live in the `.SAV`; those pointers are stale runtime addresses the game rebuilds on
load (exactly like the effects-list head at `0x7F`), so the **persisted** inventory state is just
the count byte plus the `.ITM` records. The layout was confirmed two ways that agree byte-for-byte:
the open-source `coab` `Item.cs` (`StructSize = 0x3F`) and a hex read of real `.ITM` bytes.

| Offset | Size | Field | Notes |
|-------:|:----:|-------|-------|
| `0x00` | 1+41 | name | Pascal string — the game's **cached** render (regenerated from the name-number bytes + hidden-names flag on display) |
| `0x2E` | 1 | item type | see `coab`'s `ItemType` enum (e.g. `0x2F` Sling, `0x3B` Shield, `0x5D` Ring of Protection) |
| `0x2F`–`0x31` | 3 | name-number bytes | index the base/adjective/noun name parts |
| `0x32` | 1 | plus | magical bonus (signed) |
| `0x34` | 1 | **readied** | equipped flag |
| `0x35` | 1 | **hidden-names flag** | **0 = fully identified**; non-zero bits hide name parts (shown as a leading `*`) |
| `0x36` | 1 | cursed | |
| `0x37`–`0x38` | 2 | weight | UInt16 |
| `0x39` | 1 | count | stack size |
| `0x3A`–`0x3B` | 2 | value | UInt16 |
| `0x3C`–`0x3E` | 3 | affects | up to three item effects |

This was verified against `THRENDER GRONE`'s real `CHRDATA1.ITM`: nine records, a plain **Sling**
(type `0x2F`, hidden-names `0`) and an unidentified **Ring of Protection** (type `0x5D`,
hidden-names `6`, value 10000). Setting the hidden-names byte to `0` "identifies" an item — the
name regenerates fully on the next display — and copying one character's `.ITM` records plus its
count byte onto another duplicates the whole inventory. The trainer's **🎒 Inventory** tab does
exactly this (offline, with an automatic backup); the `ItemEntry` parser is regression-tested in
`test/FormatCheck` against verbatim `.ITM` bytes.

## 6. How the trainer uses this

The trainer mirrors the approach a live memory editor must take:

1. **Attach** to the DOSBox process (`OpenProcess`), then enumerate committed regions with
   `VirtualQueryEx`.
2. **Signature-scan** every region for the record shape (`Game/CharacterSignature.cs`): a valid
   Pascal name (length 1–15, an initial letter, printable chars then NUL padding), six ability
   scores in range, a race byte ≤ 7, a class byte ≤ 17, non-zero max HP, and a valid status enum.
   This finds the whole party *and* any in-combat monsters regardless of where DOSBox mapped RAM.
3. **Read/write** fields at the offsets above with `ReadProcessMemory` / `WriteProcessMemory`
   (`Memory/ProcessMemory.cs`), applying the `60 − x` transform for AC/THAC0.
4. **Poll** (~1.5 Hz) to keep the party/enemy HP display live and to re-apply "freeze HP" (god
   mode) by re-writing current HP to max each tick.
5. For anything **not** in the record — the party's map X/Y and facing, the in-combat clock,
   encounter counters — a **Cheat-Engine-style scanner** (`Memory/MemorySearcher.cs`) narrows
   candidates by first-scan/increased/decreased, mirroring the reverse-engineering loop itself.

The record parser (`Game/CharacterRecord.cs`) is regression-tested in `test/FormatCheck` against
the verbatim 285-byte records of Thrender and Rhiannon extracted from the dump, so a future change
that breaks a field is caught headlessly (`dotnet run --project test/FormatCheck`).

---

## 7. The game's file layout (for completeness)

`Run.ps1` and the trainer don't need these, but they frame the memory analysis:

- **`START.EXE`** (≈64 KB) — a DOS `MZ` executable; the loader/front-end.
- **`GAME.OVR`** (≈201 KB) — an **`FBOV`**-signatured **overlay** file (Borland's overlay format).
  The main game code is paged in from here as overlays, which is why the working set is small and
  why the game logic isn't resident as one flat image — a full static disassembly would have to
  follow the overlay table rather than read a single code segment.
- **`*.DAX`** — the resource archives. Their names encode content: `WALLDEF*`/`GEO*` (dungeon wall
  and geometry sets per area 1–8), `PIC*`/`CPIC*`/`TITLE` (pictures), `8X8D*` (8×8 tile fonts),
  `MON*CHA`/`MON*ITM`/`MON*SPC` (monster graphics/items/special per encounter tier), `BODY*`/`HEAD*`
  (portrait/icon parts), `SPRIT*`, `ECL*` (the encounter/"script" data referenced as "ECL Script N"
  in area maps), and `ITEMS`. A `.DAX` begins with a small index (offsets/sizes of its members).
- **`POOL.CFG`** — plain text; records the install path (`C:\POOLRAD\`) and a couple of settings.

Because the interesting data (the party, monsters, item instances) lives in the **emulated RAM**
at runtime, the memory-dump route above is both more direct and more precise than statically
reversing the overlaid `START.EXE`/`GAME.OVR` — the dumps *are* the ground truth, and the
`FormatCheck` harness proves the decode against them.

---

## 8. Provenance & cross-references

The layout was corroborated against the community record documentation shipped with the
**Gold Box Companion** (`formats.zip`, "Character file formats / 01. Pool of Radiance.txt") and the
open-source **`coab`** reimplementation (`Classes/PoolRadPlayer.cs`, `StructSize = 0x11D`). Both list
the same offsets and enums that the dump analysis recovered independently. AC/THAC0's `60 − x`
encoding matches the sister-game code (`DisplayAc = 0x3C − ac`) and is confirmed here on real
Pool of Radiance bytes.

- Gold Box Companion — https://gbc.zorbus.net/  (`formats.zip`, PoR Monster Manual)
- `coab` (Curse of the Azure Bonds reimplementation) — https://github.com/simeonpilgrim/coab
- Stephen S. Lee, "Pool of Radiance — Exhaustive Game Information" — http://www.easydamus.com/PoR.pdf
