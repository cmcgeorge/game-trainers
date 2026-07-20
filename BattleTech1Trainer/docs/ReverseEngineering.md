# BattleTech: The Crescent Hawk's Inception — Reverse-Engineering Notes

Teardown notes for the 1988 Westwood Associates / Infocom DOS game **BattleTech: The
Crescent Hawk's Inception**, gathered to build the live-memory trainer in this folder. This
is the single source of truth for every offset, format, and address the trainer relies on.

Each fact is tagged with a confidence level:

- **[Confirmed]** — verified byte-for-byte against the shipped files in `.game/` (mostly
  `BTECH.EXE`) or against verbatim strings the executable carries.
- **[Corroborated]** — consistent across multiple independent external sources (Sarna wiki,
  the UnBattletech decompilation project, community save-hacking guides) but not personally
  re-derived from bytes here.
- **[Unverified]** — a single external source, or a plausible inference; do **not** hard-code
  it without live confirmation.

> **Environment caveat.** The proposal expected Ghidra at `C:\Ghidra\ghidra_12.1.2_PUBLIC`
> and a DOSBox install. Neither is present on this machine, and no `Game0`-style save file
> ships in `.game/`. So the teardown below was done by **static analysis of the shipped files**
> (Python string/hexdump scripts, the `.BLD` cipher crack) plus cross-referencing public
> research — not a live debugger session. Everything tagged **[Confirmed]** is reproducible
> from `.game/BTECH.EXE` alone; the runtime RAM layout (§5) is deliberately left to the value
> scanner rather than guessed.

---

## 1. Target overview

| Property | Value | Source |
| :-- | :-- | :-- |
| Title | `BattleTech: The Crescent Hawk's Inception` | [Confirmed] string @ `0x1EDCE` |
| Version | `Version 1.03` | [Confirmed] string @ `0x1EE11` |
| Copyright | `(C) 1988 Infocom, Inc.`; program by **Westwood Associates**; universe © FASA | [Confirmed] strings @ `0x1EC04`–`0x1ECFF` |
| Main executable | `BTECH.EXE`, 152,429 bytes, real-mode DOS **MZ** (`MZ` magic @ 0) | [Confirmed] |
| Runtime | 16-bit real-mode DOS; runs under DOSBox / DOSBox-X | [Confirmed] |
| C runtime | `MS Run-Time Library - Copyright (c) 1988, Microsoft Corp` | [Confirmed] string @ `0x1E121` |
| Graphics | CGA / 64K EGA, Tandy 1000, 256K EGA/VGA, MCGA | [Confirmed] setup strings @ `0x1EE22` |
| Currency | **C-Bill** (ComStar Bill), labelled `C-Bills:` / `C-bill account:` | [Confirmed] strings @ `0x213C8`, `0x22C53` |

The game is a real-mode DOS binary (unlike `ThePerfectGeneral2Trainer`'s DPMI target), so its
code and static data load into DOSBox's emulated conventional RAM essentially verbatim — which
is what makes the string/table signatures in §3 usable as a runtime detection anchor.

### 1.1 File inventory (`.game/`)

| Extension | Count | Purpose | Confidence |
| :-- | :-- | :-- | :-- |
| `.EXE` | `BTECH.EXE`, `$VERIFY.EXE` | main game; copy-protection/disk verifier | [Confirmed] |
| `.BLD` | ~30 | Town / building / room **scripts** — dialogue, menus, room logic (obfuscated; see §2.1) | [Confirmed] |
| `.MTP` | `MAP1`–`MAP15` | Tile **map** data for fixed locations | [Confirmed] (referenced as `MAP##.MTP` @ `0x1E6B4`) |
| `.CMP` | several | Compressed full-screen **images** (title, stats panel, borders) | [Corroborated] |
| `.ICN` | several | **Icon / sprite** sheets (`BTTLTECH.ICN`, `MAP.ICN`, `DESTRUCT.ICN`, `ANIMATE.ICN`…) | [Confirmed] (names referenced in EXE) |
| `.ANM` | `O0`–`O21` | **Animation** sequences (cutscenes / "outtakes") | [Corroborated] |
| `.SIF` | `WWOODBT.SIF` | Westwood asset/index file loaded at startup | [Confirmed] (referenced @ `0x1EB9D`) |
| — | `DEMOFILE` | Attract-mode / demo input recording | [Confirmed] (referenced @ `0x1ED8E`) |

---

## 2. On-disk data formats

### 2.1 `.BLD` script obfuscation — **[Confirmed], cipher cracked**

The `.BLD` files hold the town/room scripts (dialogue, shop menus, NPC logic). They are lightly
obfuscated. After a 7-byte header, each byte decodes as:

```
plaintext = (0x40 - cipherByte) & 0x7F
```

- `0xA0` decodes to a space (`0x20`).
- Lowercase letters and punctuation decode cleanly; uppercase letters land on symbol code points
  (the cipher folds them out of the printable range), so a decoded dump reads as lowercase text
  with symbol-substituted capitals — still fully legible.
- A handful of control bytes (`0xC6`, `0xEB`, `0xEA`) act as line breaks.

Decoding every `.BLD` (see `decode_bld.py`, output in `decoded_bld.txt`) recovers the full story
and shop text used for the strategy guide: the Kurita invasion, Jason/Jeremiah Youngblood, the
Citadel's destruction, the Star League cache, the retinal-scanner ComStar terminals, and the
three tradable ComStar stocks (**Defiance Industries of Hesperus**, **Nashan Diversified**,
**Baker Pharmaceuticals**). This is content only — no runtime offsets — but it confirms the
economy and quest structure the trainer's guidance text describes.

### 2.2 Save games — **[Corroborated / Unverified]**

- The in-game Load/Save UI exposes **six slots**, labelled `One`–`Six`, and validates a version
  stamp: *"Game saved is invalid. Use only games saved from this version."* **[Confirmed]**
  (strings @ `0x1E6D6`, `0x1E620`).
- The base save name observed in the EXE is `Game0` **[Confirmed]** string @ `0x1E26D`; community
  guides instead cite `BTECH0`–`BTECH5` **[Unverified]** — likely a different release/medium.
- Community save-hacking guides describe a C-Bills field at file offsets `0x05D5/0x05D6`
  (16-bit LE), a 30-byte character record, and a 127-byte mech record **[Unverified]**. **No
  save file ships in `.game/`, so none of this was re-derived here.** The trainer does **not**
  hard-code any save offset; save editing is out of scope for v1 (see §5–§6).

---

## 3. Static data tables inside `BTECH.EXE` — **[Confirmed]**

These are the load-bearing discoveries: fixed tables baked into the executable's data segment.
Because the EXE image is mapped into guest RAM verbatim, they double as **runtime signatures**
(the trainer scans for them to confirm the game is present — see §5.2). Offsets are file offsets
into `BTECH.EXE`.

### 3.1 Weapon / equipment table — **[Confirmed], 17-byte stride**

A contiguous array of fixed-size records. Each record is:

```
+0x00 .. +0x0A   11 bytes   Name, ASCII, NUL-padded (space-padded for a few)
+0x0B .. +0x10    6 bytes   Stat bytes; the last byte is a weapon-class tag
                            (0x01 ≈ small-arms, 0x02 ≈ ballistic personal, 0x03 ≈ 'Mech-scale)
= 17 bytes total
```

The stride of **17** is verified by anchoring on multiple names: `Cudgel` @ `0x20FCB`,
`Knife` @ `0x20FDC`, `Sword` @ `0x20FED`, `VibroBlade` @ `0x20FFE`, `Shortbow` @ `0x2100F`,
`Longbow` @ `0x21020`, `Crossbow` @ `0x21031`, `Pistol` @ `0x21042`, `Rifle` @ `0x21053`,
`MachineGun` @ `0x21064`, `SR Missile` @ `0x21075`, `Inferno` @ `0x21086` — each exactly `0x11`
apart. The 'Mech-scale weapons continue in the **same** array:

`LaserPistl` @ `0x21097`, `LaserRifle`, `Flamer`, `SmallLaser`, `Med Laser`, `LargeLaser`,
`PPC`, `AutoCann/2`, `AutoCann/5`, `AutoCann10`, `AutoCann20`, `MachineGun`, `Flamer`,
`LRMissile5`, `LRMissil10`, `LRMissil15`, `LRMissil20`, `SRMissile2`, `SRMissile4`,
`SRMissile6`, `Kick` (last, @ `0x211EA`).

This is a **genuinely confirmed structure** and is the one the trainer's `Game/WeaponTable.cs`
decoder models and the `FormatCheck` harness regression-tests against a captured slice.

### 3.2 'Mech definition table — **[Confirmed] names; [Partial] layout**

Begins @ `0x1D9C8` with `LOCUST`. Names recovered in order:

`LOCUST`, `WASP`, `STINGER`, `COMMANDO`, `CHAMELEON`, `JENNER`, `spectator` (placeholder),
`URBANMECH`.

Each record starts with an ASCII name followed by a block of stat bytes (tonnage, per-location
armor and internal-structure values, actuator/heat-sink counts, weapon loadout). Unlike the
weapon table the **record stride is not uniform** — the early light 'Mechs (`LOCUST`/`WASP`/
`STINGER`) are packed tighter than the later padded-name records (`COMMANDO`/`CHAMELEON`/
`URBANMECH`, whose names are space-padded to ~15 bytes + NUL). Reconstructing the exact per-field
layout is an **open Ghidra lead** (§6); the trainer therefore treats the 'Mech data as a
read-only reference (names [Confirmed], tonnage/armor/role [Corroborated] from §4) rather than a
live-editable struct.

### 3.3 Inspect-Character screen layout — **[Confirmed]**

The "Inspect Character" panel field labels are a contiguous block @ `0x1E882`:

```
Name  :   Weapon:   Armor :   Health:   Skills:
Skills → Bow & Blade, Pistol, Rifle, Gunnery, Piloting, Tech, Medical   (7 skills)
Inventory flags → MedKit:, Mapper:, Field Surgery Kit:
```

So a character carries **Armor**, **Health**, **seven skills**, and three inventory items. This
corrects the sometimes-cited "3 attributes + different skill set" from the *sequel* — the seven
skills above are what *this* game tracks. **[Confirmed]** byte block:
`4E 61 6D 65 20 20 3A 0D 57 65 61 70 6F 6E 3A 0D 41 72 6D 6F 72 20 3A` ("Name  :\rWeapon:\rArmor :").

### 3.4 Skill proficiency ladder — **[Confirmed]**

Five levels, stored as a small ordinal (0–4), names @ `0x1E15B`:

`Unskilled` (0), `Amateur` (1), `Adequate` (2), `Good` (3), `Excellent` (4).

### 3.5 Other confirmed string tables

- **Compass directions** @ `0x1E186`: North, Northeast, East, Southeast, South, Southwest, West,
  Northwest (an 8-way ordinal — the numpad movement set).
- **Character first names** @ `0x1E1CB`: Jason, Rex, Edward, Russ, Rick, Zeke, Possum, Marco,
  Rusty, Hunter, Hawk.
- **Main menu** @ `0x1E62E`: Return to game · Change game settings · Allocate men in 'Mechs ·
  Inspect Character · Heal Characters · Load Game · Save Game · Show Overhead Map.
- **Tactical-combat menu** @ `0x21B5B`: Walk · Run · Jump · Use Weapons · Kick · Computer ·
  Scan Unit · Next Unit · Flee · Begin Fight.
- **'Mech status panel** (`BTSTATS.CMP`) @ `0x1F374`: Type / Tons / Pilot / Rider / Armament·Loc;
  hit locations Left/Right {Arm,Leg,Torso,Rear Torso}, Center Torso, Head; armor status words
  `Ruined`, `None`, `OK`, `Hit`, `Gone`; actuators Left/Right Leg, Left/Right Arm.

---

## 4. 'Mech roster reference — **[Corroborated]**

Names are [Confirmed] from §3.2; the numeric stats below are [Corroborated] from public sources
(Sarna, TRO 3025) and are surfaced by the trainer as a read-only reference, **not** written to
memory.

| 'Mech | Tons | Armor | Jump | Notable weapons | Role |
| :-- | --: | --: | :-- | :-- | :-- |
| Locust (LCT-1V) | 20 | 64 | — | Med Laser, 2× MG | Fast scout (player start) |
| Wasp (WSP-1A) | 20 | 48 | 180 m | Med Laser, SRM-2 | Recon; fragile |
| Stinger (STG-3R) | 20 | 48 | 180 m | Med Laser, 2× MG | Mobile harasser |
| Commando (COM-2D) | 25 | 64 | — | SRM-6, SRM-4, Med Laser | Missile striker |
| Chameleon (TRA-6) | 50 | — | — | Large + 2 Med + 4 Small Laser, 2× MG | Training 'Mech (biggest arsenal; unmodifiable) |
| Jenner (JR7-D) | 35 | — | jets | 4× Med Laser, SRM-4 | Kurita raider (enemy) |
| UrbanMech (UM-R60) | 30 | 96 | 60 m | AC/10, Small Laser | Slow; the heaviest ballistic in-game |

---

## 5. Runtime (live RAM) state and the trainer approach

### 5.1 What the trainer needs to touch

The player-facing scalars worth cheating are all shown on screen, which is exactly what a value
scanner is good at:

- **C-Bills** — the money total (`C-Bills:` / `C-bill account:`). Walkthroughs reach ~350,000, so
  it is stored **wider than a byte** — scan as **Int32** (fall back to Int16). **[Confirmed]** it
  exists and is displayed; its live address is session-dependent.
- **Health** and **Armor** — per-character bytes on the Inspect screen (small 0–255 values →
  **Byte** scan). **[Confirmed]** they exist; layout is not statically anchorable.
- **Skills** — the seven 0–4 ordinals (§3.4). **[Confirmed]** semantics; **Byte** scan.
- **Stock holdings** — per-stock balances at ComStar terminals (**Int32**). **[Confirmed]** the
  system exists (§2.1); the arbitrage exploit is described in the strategy guide.

### 5.2 Why a value scanner (not a fixed `GameLocator`)

Like `ThePerfectGeneral2Trainer`, this trainer deliberately ships **no hard-coded address**:

1. DOSBox maps the guest's conventional RAM into the emulator's own address space at an offset
   that **changes every session**, so no absolute guest address is stable.
2. The *static* tables in §3 are locatable by signature, but they are **read-only code/data** —
   they are not where the live, mutable C-Bills/health/armor values live. The mutable game state
   sits in dynamically managed structures with no adjacent constant byte-run to anchor to (the
   same problem TPG2 faces).

So the dependable primitive is a **Cheat-Engine-style value scan** over `GameTrainers.Common.
Memory.MemorySearcher` (attach → first scan on the on-screen number → narrow by
increased/decreased/exact → pin → freeze), plus a **signature detector** that scans for the
verbatim §3 tables to tell the user "BattleTech is running in this process" before they scan. The
detector uses `GameTrainers.Common.Memory.BytePatternScanner` against distinctive ASCII the EXE
carries (the title string and the Inspect-field block), which load into guest RAM unchanged.

---

## 6. Open leads (Ghidra TODO)

1. **'Mech record stride (§3.2).** Reconstruct the exact per-field layout of the `0x1D9C8`
   'Mech table (tonnage / armor array / IS array / weapon slots) so the reference tab can show
   real numbers instead of the [Corroborated] table, and so a future locator could edit a live
   'Mech.
2. **Live-state anchor.** Find a constant byte-run adjacent to the C-Bills/character block in a
   live dump so a `GameLocator` (à la the sibling trainers) could replace the manual scan for the
   common cases.
3. **Save-file format.** With a real `Game0`/`BTECH#` save in hand, verify the C-Bills offset and
   character/mech record layouts (§2.2) and add optional offline save editing.
4. **`.MTP` map format.** Decode the tile format (Wayne Piekarski's `battletech-maps` tools are a
   starting point) to render the overworld/dungeon maps in the strategy guide.

---

## 7. Provenance & confidence summary

- **[Confirmed] here from `.game/BTECH.EXE`:** title/version/copyright; the 17-byte weapon table
  (§3.1); 'Mech *names* (§3.2); Inspect-screen fields and the seven skills (§3.3); the five skill
  levels (§3.4); direction/name/menu/combat string tables (§3.5); the `.BLD` cipher (§2.1); the
  six save slots and version validation (§2.2).
- **[Corroborated] from external research:** 'Mech tonnage/armor/role numbers (§4); the ComStar
  stock arbitrage and periodic allowance; the walkthrough, keycard matrix, and map-room planet
  puzzle (see `StrategyGuide.md`).
- **[Unverified] — not used by the trainer:** all specific save-file byte offsets and the
  character/mech save record layouts (§2.2).

Key research sources: Sarna.net BattleTech wiki; the UnBattletech decompilation project
(`velteyn/UnBattletech`); Wayne Piekarski's `battletech-maps`; community CHI save-hack guides;
MobyGames/Wikipedia for production history.
