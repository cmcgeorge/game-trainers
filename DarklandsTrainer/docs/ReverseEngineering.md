# Darklands — Reverse-Engineering Notes

Working notes on the internals of **Darklands** (MicroProse, 1992; this copy is the final
retail patch, version **483.07**, dated 26 Feb 1993 per `.game\README.TXT`). These notes back
the offsets and reference tables used by `DarklandsTrainer`. They were recovered from four
sources, and every claim below is tagged with how it was obtained:

| Tag | Meaning |
| --- | --- |
| **[EXE]** | Read out of `DARKLAND.EXE` as an embedded ASCII table (see *String tables*). |
| **[SAVE]** | Observed directly in `.game\SAVES\DEFAULT` (the character-generation template save). |
| **[DOC]** | Stated in the shipped docs (`README.TXT`, `TAC.TXT`) or the printed manual. |
| **[WEB]** | Corroborated against public references (Darklands Wiki, GameFAQs, darklands.net). |
| **[TENT]** | Tentative — structure is visible but the exact per-field mapping is not yet nailed. |

> All byte offsets are hex. Multi-byte integers in the save file are little-endian (Microsoft C,
> 8086 real mode). Attribute/skill values are single unsigned bytes.

---

## 1. Executable packaging

`DARKLAND.EXE` is **1,675,637 bytes** and is **PKLITE-compressed** — the decompressor stub
carries the string `Copyright 1990-91 PKWARE Inc.  All Rights Reserved.` **[EXE]**. The game
proper was built with Microsoft C (`MS Run-Time Library - Copyright (c) 1990, Microsoft Corp`
**[EXE]**) and runs in a DOS-extended environment; it needs ~581 KB conventional + 176 KB EMS
**[DOC]**.

Consequences for static analysis:

- A raw disassembly of the on-disk image sees only the PKLITE stub, not game code. To get real
  code into Ghidra the image must first be **unpacked** in memory (run under DOSBox and dump, or
  use an UNP/PKLITE-extract tool). The Ghidra headless pass in `.data\run_ghidra.bat` is kept for
  completeness (it imports and auto-analyzes the packed image and exports
  `ghidra_strings.txt` / `ghidra_functions.txt`), but the **authoritative** static data below
  comes from the game's *uncompressed* trailing data segment, which PKLITE leaves in the clear and
  which a plain ASCII scan recovers (`.data\Extract-Strings.ps1`).
- Because the code is packed and the run-time image is relocated into extended memory by the DOS
  extender, there is **no stable byte signature at a fixed guest address** to anchor a locator to.
  This is the same situation as `ThePerfectGeneral2Trainer`, `BattleTech1Trainer`, and
  `QuestForGlory1Trainer`, and it is why the trainer uses a **value-scanner** model rather than a
  `GameLocator` (see `AGENTS.md`).

---

## 2. String tables (game vocabulary)

`DARKLAND.EXE` embeds several parallel ASCII tables in its trailing data. They are the game's own
labels and are the single most useful RE artifact here, because they fix the **names and ordering**
of attributes, skills, careers, etc. Extracted set lives in `.data\darkland_exe_strings.txt`.

### 2.1 Attributes  **[EXE]**

Six primary attributes, in this order (this order recurs in the save file and the combat data-dump
table):

1. Endurance
2. Strength
3. Agility
4. Perception
5. Intelligence
6. Charisma

`Divine Favor` follows the six as a seventh, party/character-level value **[EXE]**.

### 2.2 Skills  **[EXE]**

The stat screen lists skills in this order (two spellings of the same list appear — the long form
used on the character sheet and a short form used in tight UI columns):

| # | Character sheet | Combat-dump label |
| --- | --- | --- |
| 1 | Edged Wpns | Edged |
| 2 | Impact Wpns | Impact |
| 3 | Flail Wpns | Flails |
| 4 | Polearm Wpns | Polearms |
| 5 | Thrown Wpns | Thrown |
| 6 | Bow Weapons | (Bow) |
| 7 | Missile Device | Mech. Misl |
| 8 | Alchemy | Alchemy |
| 9 | Religious Trng | Religion |
| 10 | Virtue | Virtue |
| 11 | Speak Common | Speak Common |
| 12 | Speak Latin | Speak Latin |
| 13 | Read & Write | Read/Write |
| 14 | Healing | Healing |
| 15 | Artifice | Artifice |
| 16 | Stealth | Stealth |
| 17 | Streetwise | Streetwise |
| 18 | Riding | Riding |
| 19 | Woodwise | Woodwise |

(The combat-dump table interleaves the weapon skills with `Riding, Woodwise, Streetwise, Healing,
Stealth, Artifice, Alchemy, Virtue, Speak Latin, Speak Common, Read/Write` — same names, combat
ordering.)

### 2.3 Combatant / character data-dump schema  **[EXE]**

The game has a debug routine that prints a character's combat record with these field labels, in
order — this is effectively the in-memory PC/combatant struct field list:

```
Endurance, Max Endur, Strength, Max Str, Agility, Perception, Intell, Charisma, Favor,
PCLevel, StrikeResult, MeleeWeapon, WeaponSpd, WeaponHndPen, WeaponDmg, WeaponMinStr,
WeaponQual, WeaponSkill, MeleeWeapQual, ShieldQual, ShieldType, ArmorStr(0), ArmorStr(1),
Armor(0), Armor(1),
Edged, Impact, Flails, Polearms, Thrown, Mech. Misl, Riding, Woodwise, Streetwise,
Healing, Stealth, Artifice, Alchemy, Virtue, Speak Latin, Speak Common, Read/Write,
PClist(-4) … PClist(+1)
```

Note the **current/max pairing** on Endurance and Strength (`Endurance`/`Max Endur`,
`Strength`/`Max Str`) — the two attributes that take temporary combat/injury damage — and the
armour split into two locations (`Armor(0)`/`Armor(1)` with matching `ArmorStr` minimum-strength
requirements). `PClist(-4..+1)` is the party roster window used for group targeting.

### 2.4 City interaction template variables  **[EXE]**

The city/interaction screens are text templates filled from a variable table:

```
PlaceName, PlaceDesc, Money1..Money5, LeaderName, ChosenOneName..ChosenFiveName,
PlaceAttitude, Number1, Number2, NamedOneName..NamedFiveName, Text1..Text4,
CurrentBell, CityLordName, CityLordTitle, CityLocation, MapLocation, LocName, ItemName
```

City building/service types  **[EXE]**:

```
citySquare, councilHall, imperialMint, cityBarracks, university, marketplace, fortress,
pawnshop, hospital, docks, poorhouse, whorehouse, warehouse, monastery, cathedral, cityChurch
```

Merchant/service kinds  **[EXE]**: `goods merchant, blacksmith, artificier, cathedral,
foreign trader, pharmacist`, plus the trading houses `Medici, Hanse, Fugger`.

### 2.5 Careers / social rank ladders  **[EXE]**

Each character background has a promotion ladder; the embedded ladders include:

- **Soldier:** Recruit → Soldier → Veteran → Captain
- **Noble:** Noble Heir → Knight → Courtier → Manorial Lord
- **Religious:** Priest / Bishop / Friar / Hermit / Oblate / Novice Monk-Nun / Monk-Nun /
  Abbot-Abbess
- **Merchant:** Peddler → Local Trader → Travelling Merchant → Merchant-Proprietor (→ Schulz)
- **Academic:** Student → Clerk → Physician → Professor; **Alchemist → Master Alchemist**
- **Craftsman:** Apprentice → Journeyman → Master Craftsman
- **Low-life:** Laborer, Vagabond, Swindler, Thief, Peasant, Hunter, Bandit

### 2.6 Reputation / Fame tiers  **[EXE]**

`Unknown, Barely Known, Slight Reputation, Modest Reputation, Good Reputation, Slight Heroes,
Modest Heroes, Great Heroes, Famous Heroes, Storied Heroes, Legendary Heroes`. Local reputation
(separate from party Fame) uses `a local hero / respected / unknown / suspected / wanted / hunted`.
Party Fame is a single number shown on the party screen (`PARTY FAME`, `%d`) — the README notes
successful parties reach the low-to-high hundreds **[DOC]**.

### 2.7 Currency  **[EXE][DOC]**

Three denominations, shown as `%dfl, %dgr, %dpf`:

- **Florin** (fl) — gold
- **Groschen** (gr) — silver; **20 groschen = 1 florin**
- **Pfennig** (pf) — copper; **12 pfennigs = 1 groschen**

Alchemy also tracks **Philosopher's Stones** (`%d PhStone`), the universal reagent. The party
purse is printed as `%s holds the purse of %dfl, %dgr, %dpf.`

### 2.8 Time-of-day (canonical hours) and directions  **[EXE]**

Clock is displayed as the monastic canonical hours: `Matins, Prime, Terce, Sexts, Nones, Vespers,
Compline` (plus `CurrentBell`). Facings are the 8-wind compass `North, Northeast, Southeast,
South, Southwest, Northwest` (NE/NW/SE/SW diagonals; the table stores 6 of the 8 as primary).

### 2.9 Alchemy / potion names  **[EXE]**

The alchemy data file `darkland.alc` is loaded by name **[EXE]**. Formula/entity names such as
`Enitharmon, Sarcopteryx, Gorgorex, Rodomont, Calefactor, Vendichisme, Lucifuge, Rofocale,
Venifer, Mantiphage, Bezaleel …` appear in the trailing table — these are the secret/true names
used by the alchemy and summoning systems.

---

## 3. Save-file layout (`SAVES\DEFAULT`, 26,349 bytes)

`DEFAULT` is the character-generation template that **must** exist for new-game creation **[DOC]**.
It contains one fully-formed sample character (**"Gretchen Wilburg"**, nickname **"Gretch"**),
which makes it a Rosetta stone for the in-memory/on-disk character record. Hex captured in
`.data\default_hex_head.txt`.

### 3.1 File header  **[SAVE]**

| Offset | Bytes | Meaning |
| --- | --- | --- |
| `0x000` | ASCIIZ | Current location / city name — `"Rottweil"` in this file |
| `0x015` | ASCIIZ | Save/party label — `"new default"` |
| `0x0F1` | 1 byte | Party member count — `0x04` (four filled slots) |
| `0x0FD` | 4 × 4-byte ASCIIZ | Party portrait codes: `F60`, `F01`, `A00`, `C00` — one per party slot; each maps to a `PICS\<code>STAT.PIC` / `SHORT.PIC` / `SMALL.PIC` portrait set. Empty slots hold `"0"`. |

The portrait codes at `0x0FD` (immediately after a `0x04` party-count byte at `0x0F1` and an
`0xFF` pad run) are the cleanest confirmation of the **4-slot party array** — the four non-empty
codes (`F60`, `F01`, `A00`, `C00`) each correspond to portrait files that exist in `.game\PICS\`.

> The offsets above were re-derived byte-for-byte from `.data\default_hex_head.txt`; earlier drafts
> that cited `0x01B` / `0x0FB` / `0x1B2` were off by a few bytes and are corrected here.

### 3.2 Character record (sample: Gretchen)  **[SAVE][TENT]**

Within the record that begins around `0x1AE`:

| Offset | Field | Value in DEFAULT |
| --- | --- | --- |
| `0x1AE` | Full name (ASCIIZ, ~24-byte field) | `Gretchen Wilburg` |
| `0x1C7` | Nickname (ASCIIZ, ~8-byte field) | `Gretch` |
| `0x1E6` | **Attributes — current** (6 bytes + 1) | `1E 1E 1D 12 1F 20 63` = End 30, Str 30, Agi 29, Per 18, Int 31, Cha 32, (+`63`=99) |
| `0x1ED` | **Attributes — maximum** (6 bytes + 1) | identical block (a fresh character has current == max) |
| `0x1F4` | **Skills** (20 bytes) | `23 14 06 10 04 1E 0D 06 0D 19 28 04 04 03 02 15 0B 0D 04 03` |

The **attribute block is high-confidence**: two identical 6-byte runs (with a trailing `0x63`/99
cap byte) sit exactly where a current+max attribute pair belongs, the six values are all in the
plausible 18–32 range, and they match the six-attribute count and ordering from §2.1. The
**skills block is [TENT]** — the run length and value range match the skill list from §2.2, but the
exact byte→skill mapping needs a second live sample to pin down (open the character sheet, read a
skill, then value-scan for it — the trainer's Guided Scans do exactly this).

> The trainer does **not** ship an offline save editor that writes these offsets, because they are
> derived from a single template sample and a mis-write could corrupt a party. `test\FormatCheck`
> instead validates the **read-only** structure it is confident about (header strings, portrait
> codes, attribute block) against `DEFAULT` so the parser can't silently drift.

---

## 4. Auxiliary data files  **[EXE][DOC]**

Darklands is heavily data-driven. Filenames referenced from the executable and present in `.game\`:

| File(s) | Purpose |
| --- | --- |
| `DARKLAND.ALC` | Alchemy formulae/reagents (`darkland.alc`, `No.alc`) |
| `DARKLAND.CTY` / `.LOC` / `.MAP` | Cities, locations, and the travel map of the Empire |
| `DARKLAND.ENM` / `.FAM` | Enemy definitions; families/factions |
| `DARKLAND.SNT` | Saints (the religion/miracle system) |
| `DARKLAND.MSG` / `MSGFILES` | Interaction text (1.1 MB of templated strings) |
| `level%d.flr / .atv / .enm` | Tactical battlefield: floor layout, enemy **activation records**, enemy roster |
| `tacanim.db`, `TERRAIN.FIL`, `BATTLEGR.IMG` | Tactical animation/terrain/battleground assets |
| `TAC.TXT` | Human-readable dump of the current tactical battle (see §4.1) |

### 4.1 Tactical battle record (`TAC.TXT`)  **[DOC]**

`TAC.TXT` is a debug dump the engine writes for the current battle. It exposes the tactical
parameter block and per-level enemy **activation record**:

```
Tac params:  mode, bfldtype, rseed, ftype/fqual/fnum ×3, terrn, mnth
Activation:  NumActivationSpots, strtx, strty, sprd, rm, AllowReenf,
             type[0..2], num/type[0..2], F, G, Pf, (reinforcement) Rtype/Rsprd/Rx/Ry/Rrate/Rmax/Rdirn
Level:       V, S, R, D, E, FurnRemove[x,y]×5, WallRemove[x,y]×7
```

This is not needed by the trainer but documents how battles are seeded (`rseed`) and populated.

---

## 5. Live-memory model (how the trainer works)

Because of §1 there is no fixed anchor, so the trainer treats DOSBox/DOSBox-X guest RAM as an
opaque address space and uses `GameTrainers.Common.Memory.MemorySearcher` as a Cheat-Engine-style
value scanner:

1. **Attach** to the emulator process (`ProcessMemory.Open`).
2. **First scan** for a value you can read off the character/party screen (a skill level, current
   Endurance, Fame, or the florin count).
3. **Narrow** (Exact / Increased / Decreased / Changed / Unchanged) after making that number move
   in-game — e.g. take a hit so Endurance drops, then scan *Decreased*.
4. **Pin** the survivor and edit / freeze it.

Widths: attribute and skill values are **bytes**; Fame and the florin/groschen counts behave as
**16-bit** words. The Guided Scans in the trainer pre-select the right width and walk the user
through the narrowing steps for Endurance, Strength, a chosen Skill, Fame, and Florins.

### 5.1 Confirmed vs. tentative summary

| Item | Status |
| --- | --- |
| Attribute names + order (6 + Divine Favor) | **Confirmed [EXE][SAVE]** |
| Skill names + order (19) | **Confirmed [EXE]** |
| Currency denominations + ratios | **Confirmed [EXE][DOC]** |
| Careers / reputation / Fame tiers | **Confirmed [EXE]** |
| Party = 4 portrait slots | **Confirmed [SAVE]** |
| Character record: name/nickname/attribute block offsets | **Confirmed in DEFAULT [SAVE]** |
| Skills block byte→skill mapping | **Tentative [TENT]** — confirm via live scan |
| Live guest-RAM addresses | **Not fixed** — discovered per-session by value scan |

---

## 6. Reproducing this analysis

```bat
:: 1. Extract the embedded ASCII tables from the (packed) executable
powershell -File .data\Extract-Strings.ps1 -Path .game\DARKLAND.EXE -Min 5 > .data\darkland_exe_strings.txt

:: 2. Hex-dump the character-generation template save
powershell -File .data\Hex-Dump.ps1 -Path .game\SAVES\DEFAULT -Offset 0 -Length 768 > .data\default_hex_head.txt

:: 3. (Optional) Ghidra headless auto-analysis of the packed image
.data\run_ghidra.bat
```

All three write into `.data\`, which is git-ignored.
