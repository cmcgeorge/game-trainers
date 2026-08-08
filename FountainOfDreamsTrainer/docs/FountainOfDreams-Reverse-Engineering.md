# Fountain of Dreams (1990) — Reverse-Engineering Notes

Live-memory layout and file format analysis of the 1990 Electronic Arts post-apocalyptic RPG **Fountain of Dreams**, as it runs under **DOSBox**. These notes back the `FountainOfDreamsTrainer`: everything below was recovered from static analysis of save files, the game's internal data files, and strings extracted from the packed executables.

**Note:** No live testing was performed. All addresses and offsets are based on static analysis of `DISK1` saves and the `KEH.EXE` engine.

---

## 1. Executable Analysis

Fountain of Dreams is built on a modified version of the *Wasteland* engine. It ships with two primary executables:

| File | Size (Packed) | Function |
|:---|:---:|:---|
| `FOD.EXE` | ~42 KB | Character creation launcher and profession selector. |
| `KEH.EXE` | ~300 KB | The main game engine. |

Both files are **EXEPACK-compressed** Microsoft C 1988 executables. They use a standard EXEPACK header followed by 0xFF-based RLE encoding for the compressed image. Unpacking these files is required for static analysis of string pools and data segments.

---

## 2. Character Data & Roster

The roster occupies a block in memory (and in the `DISK1` save file) containing up to **3 character records**. Unlike *Wasteland*'s 7-slot party, *Fountain of Dreams* restricts the active party to 3 members.

### Locating the roster

The roster address is expected to be dynamic. The trainer uses a **structural scan** to locate it, searching for a 3-record window where:
- Occupied slots pack from slot 0 (an empty slot never precedes an occupied one).
- At least one slot is occupied.
- Each occupied record passes a validity test:
    - A printable ASCII name starting with a letter (+0x00).
    - 7 attribute bytes in the range `3..20` (+0x18).
    - A plausible current CON (+0x23) and MaxCON (+0x46).

### Character Record Layout (332 bytes / 0x14C)

Confirmed by analysis of the `DISK1` save file and cross-checked against the `ARCHTYPE` template file.

| Offset | Size | Field | Notes / Evidence |
|:---:|:---:|:---|:---|
| `0x00` | 20 | **Name** | Null-terminated ASCII. Followed by variable-length quote text up to `0x13`. |
| `0x14` | 4 | **Cash** | uint32 LE. Confirmed `0`, `25`, `50` in test characters. |
| `0x18` | 1 | **ST** (Strength) | Attribute range 3-20. |
| `0x19` | 1 | **IQ** (Intelligence) | Affects Active skill scaling. |
| `0x1A` | 1 | **DX** (Dexterity) | |
| `0x1B` | 1 | **WP** (Willpower) | |
| `0x1C` | 1 | **AP** (Appeal) | Perception/Sense. Affects Passive skill scaling. |
| `0x1D` | 1 | **CH** (Charisma) | |
| `0x1E` | 1 | **LK** (Luck) | |
| `0x23` | 1 | **CON** | Current Constitution. Confirmed u8. |
| `0x24` | 32 | **Skills** | Variable-length packed skill data. |
| `0x46` | 2 | **MaxCON** | uint16 LE. Profession ranges: 15-25. |
| `0x50` | 1 | **Level** | uint8. Starts at 1. |
| `0x52` | 2 | **Rank** | uint16 LE. Starting values: 6, 7, 8. |
| `0x54` | 4 | **Experience** | uint32 LE. |
| `0x5E` | 2 | **Next Level XP** | uint16 LE. Starting thresholds: 1000, 1500. |
| `0x80` | 162 | **Inventory** | 27 slots × 6 bytes each. `0xFF` marks an empty slot. |
| `0x140`| 12 | **Metadata** | Per-character engine metadata. |

---

## 3. The ARCHTYPE File (Profession Templates)

The `ARCHTYPE` file (2632 bytes) defines the starting stats for the game's professions. It consists of an 8-byte header followed by 7 records of 128 bytes each.

| Profession | Type | CON Range | Primary Attributes / Skills |
|:---|:---:|:---:|:---|
| **Survivalist** | Player | 20-25 | Pharmacy, Mechanics, Stealth, Handgun, Gunsmith |
| **Vigilante** | Player | 20-25 | Demolition, Brawling, Handgun, Gunsmith |
| **Medic** | Player | 15-20 | Medic (L2), Perception, Blades |
| **Hood** | Player | 15-25 | Lockpick (L2), Stealth, Handgun |
| **Mechanic** | Player | 15-25 | Bomb/Alarm Disarm, Mechanic, Demolition, Brawling |
| **Yuppie** | NPC | 10-20 | |
| **Clown** | NPC | 1-20 | |

**Record structure (+0x08 start):**
- `+0x00`: Name (NUL-terminated)
- `+0x14`: Attributes (7 bytes: ST, IQ, DX, WP, AP, CH, LK)
- `+0x20`: Base Skill Levels
- `+0x2C`: Max Skill Levels
- `+0x58`: "Attacks" string followed by combat data
- `+0x70`: CON Min/Max (two uint16 LE)

---

## 4. Skills and Attributes

The attribute order was confirmed by strings in `FOD.EXE` and `KEH.EXE`: `ST, IQ, DX, WP, AP, CH, LK`.

### Active vs. Passive Skills
The manual and engine distinguish between skills that require concentration (Active) and those that work automatically (Passive).

**Active Skills (Scale with IQ over 15):**
1. **Medic** — Wound dressing.
2. **Lockpick** — Opening locks.
3. **Climb** — Scaling surfaces.
4. **Pharmacy** — Drug analysis and poison treatment.
5. **Bomb/Alarm Disarm** — Explosives and security.
6. **Mechanic** — Mechanical repair.
7. **Electronics** — Electronic gear maintenance.
8. **Doctor** — Advanced medical care (curing rabies, radiation).
9. **Brawling** — Unarmed combat proficiency.
10. **Handgun** — Pistol proficiency.

**Passive Skills (Scale with AP over 15):**
1. **Gunsmith** — Repairing/unjamming weapons.
2. **Perception** — Awareness and spotting hidden objects.
3. **Stealth** — Moving silently.
4. **Language** — Foreign language comprehension.
5. **Demolitions** — Knowledge of explosives.
6. **Blades** — Blade weapon proficiency.

---

## 5. Data Files and Strings

Analysis of the game's data files recovered the following reference catalogs:

- **GLOBALS**: Contains names for 90 items, including weapons, armor, and quest items.
- **WEAPONS**: Contains attack verbs (e.g., "hits", "slashes").
- **SERVICES**: List of 24 medical and training services available at Doc Marino's, Doc Brewhoe's, and DeMedici's.
- **KEH.EXE Strings**: Display format strings at `0x10DD0` in the image confirm the encoding of cash (`%lu`), Rank (`%u`), and attributes (`%3s%2u`).

---

## 6. Trainer Implications

- **Structural Scanning:** Since `KEH.EXE` is packed and the BSS layout is not fully mapped, the structural scan is the only reliable way to find the roster.
- **Read-Validate-Write:** The trainer uses the 332-byte record size to ensure writes only touch valid character slots.
- **Calculated Fields:** Max health (CON) and skill bonuses are influenced by attributes (IQ/AP); the trainer surfaces these relationships but edits the base values directly.
- **Save Editing:** The `DISK1` format is a flat dump of the roster followed by game state (3776 bytes total), making it directly editable using the same logic as the live trainer.
