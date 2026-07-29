# Amberstar — Reverse-Engineering Notes

## Game Identification

| Field | Value |
|---|---|
| **Title** | Amberstar |
| **Developer** | Thalion Software (Karsten Brück, Peter Thierolf, Gino Fehr, Frank Ussner) |
| **Publisher** | Thalion Software |
| **Release** | 1992 |
| **Original platform** | Atari ST (Motorola 68000, big-endian) |
| **DOS version** | IBM AT build, V1.34 / 22.10.1992 |
| **Executable** | `GAME.EXE` (264,959 bytes, MZ DOS executable) |
| **Emulator** | DOSBox / DOSBox-X |

### EXE Strings (confirmed by binary inspection)

| Offset | String |
|---|---|
| `0x3AB1` | `AMBERSTAR I` |
| `0x3AE7` | `THALION SOFTWARE G.M.B.H` |
| `0x3B6E` | `V1.34 / 22.10.1992` |

### Game directory contents

| File | Size | Notes |
|---|---|---|
| `GAME.EXE` | 264,959 | MZ DOS executable, the game engine |
| `CHARDATA.VGA` | — | Character portrait data |
| `PARTYDAT.SAV` | 5,104 | Compressed/encrypted save file; not directly editable |
| `*.VGA` | various | Graphics, sound, map, and item data files |

## Big-Endian Heritage

Amberstar was originally developed for the Atari ST, which uses the Motorola 68000 CPU
(big-endian). When ported to the PC (Intel x86, little-endian), the character data
structures retained their original big-endian byte order. **All multi-byte values in the
character record are stored big-endian** — the high byte comes first. This is the single
most important detail for parsing the format correctly on PC.

## Data Source

The character record layout was derived from the open-source
[Pyrdacor/Amberstar](https://github.com/Pyrdacor/Amberstar) file specification
(`FileSpecs/CharData.md`), which documents the format from the Atari ST original and the
DOS port. Key offsets were confirmed against the `GAME.EXE` V1.34 IBM AT build by
inspecting the binary and cross-referencing the in-game character screen.

## Character Record Layout (1146 bytes / 0x047A)

Each party member occupies a 1146-byte record. The party is an array of up to 6
contiguous records in memory. NPC-specific fields (interactions, portrait, dialogue)
extend the record beyond 0x047A but are not present for player characters.

### Magic Header

| Offset | Size | Type | Value | Notes |
|---|---|---|---|---|
| `0x0000` | 2 | Word (BE) | `0x00FF` | Always `00 FF`; identifies an Amberstar character record |

### Identity

| Offset | Size | Type | Field | Values |
|---|---|---|---|---|
| `0x0002` | 1 | Byte | Type | 0 = Person (PC/NPC), 1 = Monster |
| `0x0003` | 1 | Byte | Gender | 0 = Male, 1 = Female |
| `0x0004` | 1 | Byte | Race | 0=Human, 1=Elf, 2=Dwarf, 3=Gnome, 4=Halfling, 5=Half-Elf, 6=Half-Orc, 13=Animal |
| `0x0005` | 1 | Byte | Class | 0=None, 1=Warrior, 2=Paladin, 3=Ranger, 4=Thief, 5=Monk, 6=White Mage, 7=Grey Mage, 8=Black Mage, 9=Animal |

### Skills (current + max, each a byte)

| Offset | Size | Field |
|---|---|---|
| `0x0006` | 10 | Current skills: ATK, PAR, SWI, LIS, F-T, D-T, P-L, SEA, RMS, U-M |
| `0x0010` | 10 | Max skills (same order) |

Skill indices: 0=Attack, 1=Parry, 2=Swim, 3=Listen, 4=Find Traps, 5=Disarm Traps,
6=Pick Locks, 7=Search, 8=Read Magic, 9=Use Magic. Each is a byte in range 0..99.

### Magic / Combat

| Offset | Size | Type | Field | Notes |
|---|---|---|---|---|
| `0x001A` | 1 | Byte | Magic Schools flags | bit 1=white, bit 2=grey, bit 3=black, bit 7=special |
| `0x001B` | 1 | Byte | Level | 1..99 |
| `0x001C` | 1 | Byte | Used Hands | equipment slot count |
| `0x001D` | 1 | Byte | Used Fingers | equipment slot count |
| `0x001E` | 1 | Byte | Base Defense | base defence value |
| `0x001F` | 1 | Byte | Base Damage | base damage value |
| `0x0020` | 1 | Byte | Magic Bonus (Weapon) | enchantment bonus |
| `0x0021` | 1 | Byte | Magic Bonus (Armour) | enchantment bonus |

### Item Amounts

| Offset | Size | Field |
|---|---|---|
| `0x0022` | 9 | Equipped item amounts (one byte per equipped slot) |
| `0x002B` | 12 | Inventory item amounts (one byte per inventory slot) |

### Languages / Ailments

| Offset | Size | Type | Field | Notes |
|---|---|---|---|---|
| `0x0037` | 1 | Byte | Languages | bitfield: which languages the character speaks |
| `0x003A` | 1 | Byte | Physical Ailments | bitfield (see below) |
| `0x003B` | 1 | Byte | Mental Ailments | bitfield (see below) |

#### Physical Ailments Bitfield

| Bit | Value | Name |
|---|---|---|
| 0 | `0x01` | Stunned |
| 1 | `0x02` | Poisoned |
| 2 | `0x04` | Petrified |
| 3 | `0x08` | Diseased |
| 4 | `0x10` | Aging |
| 5 | `0x20` | Dead |
| 6 | `0x40` | Ash |
| 7 | `0x80` | Dust |

#### Mental Ailments Bitfield

| Bit | Value | Name |
|---|---|---|
| 0 | `0x01` | Irritated |
| 1 | `0x02` | Mad |
| 2 | `0x04` | Sleeping |
| 3 | `0x08` | Afraid |
| 4 | `0x10` | Blind |
| 5 | `0x20` | Overloaded |

### Attributes (current + max, each a big-endian Word)

| Offset | Size | Field |
|---|---|---|
| `0x0048` | 18 | Current attributes (9 Words, BE): STR, INT, DEX, SPE, CON, CHA, LUC, MAG, AGE |
| `0x005C` | 18 | Max attributes (9 Words, BE, same order) |

Attribute indices: 0=Strength, 1=Intelligence, 2=Dexterity, 3=Speed, 4=Constitution,
5=Charisma, 6=Luck, 7=Anti-Magic, 8=Age. Each is a big-endian Word (2 bytes, high byte
first). Typical range: 1..999.

### Progression

| Offset | Size | Type | Field |
|---|---|---|---|
| `0x0070` | 2 | Word (BE) | Level Attack — attack bonus per level |
| `0x0072` | 2 | Word (BE) | HP per Level |
| `0x0074` | 2 | Word (BE) | SP per Level |
| `0x0076` | 2 | Word (BE) | SLP per Level |

### Vitals (big-endian Words)

| Offset | Size | Type | Field | Notes |
|---|---|---|---|---|
| `0x0086` | 2 | Word (BE) | HP Current | hit points |
| `0x0088` | 2 | Word (BE) | HP Max | |
| `0x008A` | 2 | Word (BE) | SP Current | spell points |
| `0x008C` | 2 | Word (BE) | SP Max | |
| `0x008E` | 2 | Word (BE) | SLP | spell learning points |

### Resources (big-endian Words)

| Offset | Size | Type | Field |
|---|---|---|---|
| `0x0090` | 2 | Word (BE) | Gold |
| `0x0092` | 2 | Word (BE) | Food |

### Equipment Bonuses (big-endian)

| Offset | Size | Type | Field |
|---|---|---|---|
| `0x0094` | 2 | Word (BE) | Bonus Defense |
| `0x0096` | 2 | Word (BE) | Bonus Damage |
| `0x0098` | 2 | Word (BE) | Bonus HP |
| `0x009A` | 2 | Word (BE) | Bonus SP |

### Experience (big-endian Long)

| Offset | Size | Type | Field | Notes |
|---|---|---|---|---|
| `0x00CC` | 4 | Long (BE) | Experience | 4 bytes, high byte first |

### Known Spells (big-endian Longs, bitfields)

| Offset | Size | Type | Field | Notes |
|---|---|---|---|---|
| `0x00D0` | 4 | Long (BE) | White Spells | bit N = spell N+1 known |
| `0x00D4` | 4 | Long (BE) | Grey Spells | |
| `0x00D8` | 4 | Long (BE) | Black Spells | |
| `0x00E8` | 4 | Long (BE) | Special Spells | |

Note: there is a gap between Black (`0x00D8`) and Special (`0x00E8`) — the 12 bytes at
`0x00DC`–`0x00E7` are reserved/unused in the PC record.

### Weight / Name

| Offset | Size | Type | Field | Notes |
|---|---|---|---|---|
| `0x00EC` | 4 | Long (BE) | Weight | carried weight in grams |
| `0x00F0` | 16 | ASCII | Name | null-terminated, 15 chars max |

Names use plain ASCII (not the high-bit-encoded format of some other DOS RPGs like Dragon
Wars). The name field is 16 bytes; the last byte is a null terminator.

### Items (each 40 bytes / 0x28)

| Offset | Count | Field |
|---|---|---|
| `0x0132` | 9 | Equipped items (9 × 40 bytes) |
| `0x029A` | 12 | Inventory items (12 × 40 bytes) |

The party-member record ends at `0x047A` (1146 bytes). NPC records continue beyond this
with interaction tables, portrait data, and dialogue trees.

## Spell Schools

96 spells total, across four schools. Bit N (0-based) in the school's Long bitfield
corresponds to spell N+1.

### White Magic (28 spells, bits 0..27)

Healing 1–5, Salvation, Reincarnation, Conversion of Ashes, Conversion of Dust,
Neutralise Poison, Heal Stun, Heal Sickness, Rejuvenation, De-Petrification, Wake Up,
Calm Panic, Remove Irritation, Heal Blindness, Heal Madness, Stun, Sleep, Fear,
Irritation, Blind, Destroy Undead, Holy Word, Remove Curse, Provide Food.

### Grey Magic (26 spells, bits 0..25)

Light 1–3, Armour Protection 1–3, Weapons Power 1–3, Anti-Magic 1–3, Clairvoyance 1–3,
Invisibility 1–3, Magic Sphere, Magic Compass, Identification, Levitation, Haste,
Mass Haste, Teleport, X-Ray Vision.

### Black Magic (22 spells, bits 0..21)

Beam of Fire, Wall of Fire, Fireball, Fire Storm, Fire Cascade, Waterhole, Waterfall,
Ice Ball, Ice Shower, Hail Storm, Mud Catapult, Falling Rock, Bog, Landslide,
Earthquake, Strong Wind, Storm, Tornado, Thunder, Hurricane, Desintegration, Magic Arrows.

### Special Magic (20 spells, bits 0..19)

Stunned, Poison, Flesh to Stone, Make Ill, Aging, Irritation, Make Mad, Sleep, Panic,
Blinding Flash, Flesh To Stone, Mapshow, Banish Demon, Spellpoints 1, Spellpoints 2,
Weapon Balm, Youth, Pick Lock, Eagle Call, Music.

## Race Table

| ID | Race |
|---|---|
| 0 | Human |
| 1 | Elf |
| 2 | Dwarf |
| 3 | Gnome |
| 4 | Halfling |
| 5 | Half-Elf |
| 6 | Half-Orc |
| 13 | Animal |
| 14 | Monster |

## Class Table

| ID | Class |
|---|---|
| 0 | None |
| 1 | Warrior |
| 2 | Paladin |
| 3 | Ranger |
| 4 | Thief |
| 5 | Monk |
| 6 | White Mage |
| 7 | Grey Mage |
| 8 | Black Mage |
| 9 | Animal |
| 10 | Monster |

## Party Location Strategy

The party roster's address in DOSBox guest memory changes every session (DOSBox
allocates memory dynamically), so it cannot be hard-coded. The trainer locates it by
**structural scan**:

1. Walk every committed, readable memory region in the emulator process.
2. For each position, check whether the next 6 × 1146 bytes look like an Amberstar
   party roster — a window where:
   - Occupied slots pass validation (magic header `00 FF`, type = Person, plausible
     gender/race/class, all 20 skill bytes in 0..99, big-endian attributes in 1..999,
     HP max > 0, name starts with a letter).
   - Empty slots have a non-matching magic header.
   - Occupied slots pack from slot 0 (no empty slot before an occupied one).
3. The first window that matches is the roster.

This is the same approach used by the Wasteland trainer (structural scan of contiguous
records) and is specific enough to avoid false positives because the validation criteria
are strict and the record size (1146 bytes) is distinctive.

## Save File (PARTYDAT.SAV)

The `PARTYDAT.SAV` file is 5,104 bytes and uses an unknown compression/encoding method.
Unlike the flat character records in memory, the save file does not contain recognizable
`00 FF` magic headers or ASCII names at their expected offsets. It may be compressed with
a Thalion-proprietary scheme or encrypted. This trainer edits **live memory only** — the
save file is not touched.

## Ghidra Analysis

Ghidra 12.1.2 PUBLIC (at
`C:\ProgramData\chocolatey\lib\ghidra\tools\ghidra_12.1.2_PUBLIC`) was used to analyze
`GAME.EXE`. The EXE is a standard MZ DOS executable. Key findings:

- The version string `V1.34 / 22.10.1992` at offset `0x3B6E` confirms the build.
- The character record handling routines read and write multi-byte values in big-endian
  order, confirming the Atari ST heritage.
- The party roster is allocated as a contiguous block of 6 × 1146-byte records in
  conventional memory; the game's memory manager assigns its address at load time.

## Online Sources

- [Pyrdacor/Amberstar GitHub repository](https://github.com/Pyrdacor/Amberstar) —
  open-source reimplementation with comprehensive file format specifications
  (`FileSpecs/CharData.md`, `FileSpecs/Spells.md`, etc.)
- [Abandonware DOS](https://www.abandonware-dos.com) — game descriptions, screenshots,
  and the walkthrough used for the strategy guide
- Various community forums and wiki entries for Amberstar lore and gameplay mechanics
