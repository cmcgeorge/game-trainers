# Mines of Titan — Reverse-Engineering Notes

Working notes behind the trainer's offsets. Everything here was obtained from the
game's own files under `.game/` (executable, overlays, data files, and the
bundled manuals in `.game/docs/`), a Ghidra 12.1.2 headless disassembly of
`TITAN.EXE`, and a byte-level analysis of `SAVEGAME.DAT`. Confidence is marked per
field: **Confirmed** (observed directly and cross-checked), **Strong** (observed in
one place, self-consistent), **Inferred** (deduced, not byte-verified).

---

## 1. The game

- **Title:** *Mines of Titan* — a science-fiction RPG. You lead a party across
  Saturn's moon Titan to discover why contact with the city of **Proscenium** was lost.
- **Developer / publisher:** *"Game design by Westwood Associates"*,
  *"Programming by Westwood Associates"*, *"Copyright 1989 Infocom, Inc."*
  (all three strings are in `TITAN.EXE`). It is the PC port of Westwood's earlier
  *Mars Saga*, and shares that engine's character model.
- **Release:** file timestamps on every game file are **1989-09-05**.

---

## 2. Executable & toolchain

- **`TITAN.EXE`** — 108,192 bytes, MS-DOS **MZ** real-mode executable.
- **Linker / overlay manager:** *PLINK86* (Phoenix Software Associates), confirmed by
  the string block `PLINK86 Overlay Loader: stack underflow…` and
  `Copyright (C) 1984, 1985, 1986 by Phoenix Software Associates Ltd.` The loader
  swaps `.OVL` overlays in and out of a fixed memory pool (`No room in memory pool %d…`).
- **C runtime:** Microsoft C 5.x — `MS Run-Time Library - Copyright (c) 1988, Microsoft Corp`,
  with the classic `R6000` … `R6009` runtime-error table.
- **Segmentation (from Ghidra):** the image loads as a long run of small code
  segments `CODE_0`…`CODE_21` (resident) followed by overlay-target segments
  `CODE_22u`…`CODE_56u` (marked non-executable/uninitialised until an overlay is paged
  in), then a **`DATA`** segment (`2c3c:2210`–`2c3c:e86f`, ~50 KB) that holds the static
  tables (file-name list, item names, message text). A separate `HEADER` block holds the
  MZ relocation header.

### Overlays (`.OVL`, resident loader pages these in)

| Overlay | Size | Role (inferred from name + strings) |
|---|---|---|
| `CITY.OVL` | 17,904 | City / establishment navigation |
| `TOWN.OVL` | 50,832 | Town interiors, shops, dialogue (largest overlay) |
| `SURF.OVL` | 14,304 | Surface (outdoor Titan) travel |
| `MINE.OVL` | 18,256 | Mine / tunnel exploration |
| `COMPUTER.OVL` | 9,264 | Computer-terminal / hacking mini-game |
| `GAMBLE.OVL` | 12,096 | Casino gambling |
| `BATTLE.OVL` | 3,104 | Combat control / turn resolution |
| `CBTD3D.OVL` | 46,368 | 3-D combat display / rendering |
| `WIN.OVL` | 2,240 | End-game / victory sequence |

### Data files

| File | Size | Role |
|---|---|---|
| `DISK1.DAT` | 82,485 | Game data (graphics/definitions) |
| `DISK2.DAT` | 133,567 | Game data |
| `DISKS.DAT` | 73,021 | Shared / index data |
| `DISK1MAP.DAT` | 4,610 | Map data |
| `DISK2MAP.DAT` | 1,538 | Map data |
| `SAVEGAME.DAT` | 4,190 | Saved games (see §5) |

Boot flow also mentions `PATH.EXE` and a self-reference to `TITAN.EXE`; the loader
opens the `.DAT`/`.OVL` files by name from the current directory (or drives A:/B: on the
original two-floppy layout).

---

## 3. Character model (from the manuals)

Six **primary attributes**, each shown 0–15 on an in-game bar; a character's total
across all six is capped (66 is the value the maxed save reaches — see §5):

| # | Attribute | Effect |
|---|---|---|
| 0 | **Might** | Strength; what weapons the character can carry. |
| 1 | **Agility** | *Most important* — number of combat moves per turn and ranged accuracy. |
| 2 | **Stamina** | Physical punishment tolerated before Might/Agility start dropping. |
| 3 | **Wisdom** | Perception / intuition; edge in some situations. |
| 4 | **Education** | Caps how far academic skills (e.g. Medical) can be trained. |
| 5 | **Charisma** | Looks/charm; helps avoid confrontations. |

- **Health** is *derived* — the average of Might, Agility and Stamina. It is the bar you
  watch in combat; zero Health = permanent death ("dead is dead"). Because it is
  computed from the three physicals, the trainer maxes it by maxing those attributes
  rather than by writing Health directly.
- **Sex** (Male/Female — no gameplay effect) and **Age** (older = more starting
  experience but less Might/Stamina) are stored per character.

**Skills** (raised with credits + experience at Development Centres, Universities,
Combat Training Centres, Computer Centres; a few, like Gambling, rise through use).
The 16 named in the Player's Guide:

`Administration, Arc Gun, Automatic Weapons, Battle Armor, Blade, Cudgel, Gambling,
Golum, Handgun, Medical, Melee, Mining, Programming, Rifle, Street, Throwing`.

The stored skill block is wider than 16 bytes (see §5); the extra slots are treated as
reserved/unnamed by the trainer.

---

## 4. Item table (static, in `DATA`)

`TITAN.EXE` carries the full item-name table as fixed **10-byte**, space-padded ASCII
records. It begins at file offset **0x116E4** (a blank "(none)" entry) and ends with the
sentinel **`End list`** at **0x11E27** — about 150 entries. Order (weapons → armor →
healing → misc → creature attacks):

- **Melee/thrown/guns:** `Fists, Model 10, Uzi, Aslt rifle, Auto carb, Pulse lzr,
  Part beam, Buzz gun, .22 pistol, 9mm pistol, .357 rvlvr, .45 calibr, .44 magnum,
  Mazer, Phazer, Synapse bm, Target rfl, Sport rfl, Sniper rfl, Carbine, Magnum rfl,
  Lzr crbne, Blaster, Reaver rfl, Barb dart, Throw knif, Shuriken, Molotov, Hand gren,
  Shock sphr, Mind melt, Blow gun, Bow, Comp bow, Crossbow, Gren lnchr, Acid jet,
  Flichet, Pckt knife, Switch bld, Combat bld, Short swrd, Light bld, Energy bld,
  Render, Rubber hos, Bat, Night stik, Blackjack, Lead pipe, Pulse prod, Marrow bat,
  Arc gun, Freeze gun, Synapser, Chem gun, Flm throwr, Stun gun, Paralizer, Mind blast`
- **Armor:** `Flak jackt, Vac suit, Reflect, Mesh armor, Hydro armr, Bttl armor,
  Golum armr, Lt armor, Med armor, Hvy armor`
- **Healing / utility:** `Bandage, Injection, Compress, Heal salve, Melder, Med kit A,
  Med kit B, Med kit C, Mind mend, Wallet, Watch, Tools, Rifle sigt`
- **Creature natural attacks (enemies):** `Tentacal, Mandible, Fangs, Pincers, Spine,
  Gas bomb, Death dust, Spittle, Magma, Synapser, Shock, Mining lzr, Dynamite, Slime,
  Mucus`

The `Vac suit` is required to survive the surface; higher combat skills unlock the more
powerful arms further down the list.

---

## 5. `SAVEGAME.DAT` layout — **the trainer's ground truth**

`SAVEGAME.DAT` is 4,190 bytes. The live game loads this same structure into the
emulator's guest RAM, so the offsets below drive both offline inspection and the
live-memory trainer.

### 5.1 File header (0x000–0x101) — *Confirmed*

Six save-slot **name labels**, 22 (0x16) bytes apart: `1` @0x03, `2` @0x19, `3` @0x2F,
`4` @0x45, `5` @0x5B, and `Auto` (the autosave) @0x71. The five numbered slots + autosave
match the game's Save/Load menu.

### 5.2 Save slots — *Confirmed*

Six party snapshots of **676 bytes (0x2A4)** each, the first at **0x102**. Each begins
with the ASCII magic **`IJKM`** (`49 4A 4B 4D`). Verified: the six anchors sit at
`0x102, 0x3A6, 0x64A, 0x8EE, 0xB92, 0xE36` — exactly 0x2A4 apart.

Per-slot layout, relative to its `IJKM` anchor:

| Offset | Size | Field | Confidence |
|---|---|---|---|
| +0x00 | 4 | `IJKM` magic | Confirmed |
| +0x04 | 1 | Party member count (observed `03`) | Inferred |
| +0x05 | 5 | Marching order / member slots (`FF` = empty) | Inferred |
| +0x0A | 12 | Party-level stat block (values 0x10–0x16) | Uncertain |
| +0x16 | 4 | unknown (`00 38 00 00`) | Uncertain |
| +0x1A | 86·N | **Character records**, packed from slot 0 | Confirmed |

Character records are **86 bytes (0x56)** each and pack contiguously from `+0x1A`.
Verified across slots: names land at anchor+0x1A, +0x1A+0x56, +0x1A+0xAC …
(e.g. slot 3 holds *Tom Jetland*, *Peter*, *John* at exactly 0x56 spacing).

### 5.3 Character record (86 bytes) — anchored on the name field

Offsets are relative to the record start (= the name field). Decoded from the first
record of a fully-trained ("cheated") save where every attribute/skill byte reads
`0F` (15) and credits read 100,000:

| Offset | Size | Field | Confidence |
|---|---|---|---|
| +0x00 | 16 | **Name** — ASCII, `00`-padded (plain ASCII, *not* high-bit encoded) | Confirmed |
| +0x10 | 1 | **Sex** — ASCII `'M'`/`'F'` | Confirmed |
| +0x11 | 1 | **Age** — byte (observed 0x16 = 22) | Confirmed |
| +0x12 | 6 | **Attributes** — Might, Agility, Stamina, Wisdom, Education, Charisma (order Inferred) | Strong |
| +0x18 | 27 | **Skills** — one byte each (first 16 named in §3; rest reserved) | Strong |
| +0x33 | 1 | reserved / terminator (`00`) | Observed |
| +0x34 | 20 | Derived / vitals block — partially decoded (a word here reads 66 = attribute total) | Partial |
| +0x48 | 4 | **Credits** — `uint32` little-endian (observed `A0 86 01 00` = 100,000) | Confirmed |
| +0x4C | 10 | misc (experience?/flags) | Unknown |
| = 0x56 | | total record size | Confirmed |

The contiguous `0F` run spanning **+0x12…+0x32** (33 bytes) is what pins the
attribute(6)+skill(27) split. An empty record has a `00`/non-printable first name byte.

### 5.4 Confirmed anchor values (used by the trainer's scanner)

- **Slot magic:** `49 4A 4B 4D` (`IJKM`).
- **Name → record base:** the name is at slot anchor **+0x1A**; records then stride by
  **0x56**.
- **Credits:** record **+0x48**, `uint32` LE.

---

## 6. How the trainer uses this

The trainer attaches to the DOSBox/DOSBox-X process and finds the party two ways
(mirroring the Dragon Wars trainer's dual strategy):

1. **Anchor scan** — search guest RAM for `IJKM`; the character array starts at
   anchor+0x1A. Fast and exact when a game has been loaded/saved.
2. **Structural scan** — a fallback that walks memory for an 86-byte window shaped like a
   valid record: a printable ASCII name, `'M'`/`'F'` sex byte, and attribute/skill bytes in
   range. This finds a freshly-created party that has never touched `SAVEGAME.DAT`.

Every write follows the repo's **read-validate-write** rule: a record is only edited once
it validates as a real character, and only the changed field's bytes are poked back, so a
shifted or misidentified layout can never scribble over unrelated RAM.

### Confidence caveats

- Attribute **order** within the +0x12 block and the exact **skill index → name** mapping
  are inferred from the manual's ordering, not from disassembled display code. The
  "Max attributes / Max skills" actions set the whole block to 15 and so are correct
  regardless of ordering; per-field edits assume the documented order.
- The +0x34 vitals block is only partially decoded; the trainer does not write into it
  (Health is maxed via the physical attributes instead).
