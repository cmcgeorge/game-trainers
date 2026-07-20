# Lords of the Realm — Reverse-Engineering Notes

*Target:* `Game/LORDS.EXE` (Impressions / Sierra, 1994; GOG "Royal Edition" DOSBox-X release)
*Tools:* Ghidra 12.1.2 (headless), a DOSBox-X full-memory dump (`.bin` + region map `.csv`), and manual binary analysis.
*Scope:* how the game is packaged, where its live state lives in emulated memory, and the data model behind the mechanics. This document is the technical companion to `StrategyGuide.md` and the basis for the trainer in `Trainer/`.

> **Disclaimer.** Everything here was derived from static analysis of the executable and a single RAM snapshot. Offsets that depend on a running match are marked *runtime-dependent*; the trainer therefore finds live values by scanning rather than trusting fixed addresses (see [§7](#7-implications-for-the-trainer)).

---

## 1. Executable overview

| Property | Value |
|---|---|
| File | `LORDS.EXE`, 613,968 bytes |
| Format | MZ real-mode DOS executable |
| Compiler | Borland C++ (1991 runtime; `Borland C++ - Copyright 1991 Borland Intl.`) |
| Overlay system | Borland **FBOV** virtual-code overlays (`FBOV` signature at file `0x47EE0`; `Runtime overlay error`, `No Extended memory for overlays.`, `Using Extended memory for overlays.`) |
| Serial/modem stack | Greenleaf Comm Library (`Copyright (C) 1985..1989 Greenleaf Software Inc.`) — powers the modem 2-player mode |
| Audio | AdLib / Sound Blaster / MT-32 drivers (`*.ADV`), XMI music |

The binary is split into a small **resident** portion (file `0x4000`–`0x47EE0`) and ~91 **overlay** stub segments. Each overlaid routine begins with an `INT 3Fh` (`CD 3F`) Borland overlay trap; the overlay manager pages the real code in from the tail of the EXE on demand. This is why large stretches of code (battle, siege, market, DIVERGANCE) do not appear at fixed resident addresses.

### 1.1 Ghidra segment map (import result)

Ghidra loads the image as 101 real-mode segments `CODE_0 … CODE_100` plus a 16 KB `HEADER`. The **data group (DGROUP)** is the large segment Ghidra labels `462a` (`CODE_95`, ~23 KB), which holds the C globals, string pool, and static tables. Confirmations:

- The state-label pool `"Counties" "Units" "Players" "Figures" "Armies" "Castles" "Prices" "Sieges"` is at Ghidra `462a:3cc2` and at EXE file offset `0x3DF62`.
- Ghidra linear = file offset − `0x4000` (header) + `0x10000` (image base). This mapping was used throughout to translate between the disassembly and the RAM dump.

---

## 2. Locating the game in emulated memory

The `.bin` is a dump of the **DOSBox-X host process**, not of guest RAM directly. The region map (`.csv`) lists dozens of host allocations. The emulated PC's physical RAM is one specific allocation, identified as follows.

### 2.1 Finding guest physical address 0

Exactly one committed `rw-` private region carries a valid **BIOS Data Area**:

- Region size `0x1001000` (16 MB + a 4 KB header), file offset `0x47B9000` in this dump.
- At guest `0x400` the BDA holds the serial-port table `F8 03 F8 02 …` = COM1 `0x3F8`, COM2 `0x2F8` — a reliable fingerprint.
- The Interrupt Vector Table at guest `0`–`0x3FF` contains real BIOS vectors pointing into the upper-memory ROM area. The INT 10h (video) vector points into segment `0xF000` (system BIOS) or `0xC000` (VGA option ROM) depending on whether the video ROM has hooked it yet — both were observed in the same live session — so the check accepts any segment `>= 0xC000`.

From this dump, **guest physical 0 = region base + 0x40**. The trainer does not hardcode `0x40`; it scans the region for the `F8 03 F8 02` BDA fingerprint and sets `guestBase = fingerprint − 0x400`, then sanity-checks the INT 10h IVT vector (segment `>= 0xC000`). This survives DOSBox-X version/ASLR differences and different `memsize` settings.

### 2.2 Runtime segment layout of the game

Cross-referencing the resident string pool between the EXE and the dump:

| Item | Value |
|---|---|
| Program load segment | `0x0823` (PSP at `0x0813`) |
| **DGROUP / DS at runtime** | **`0x3E4D`** → guest linear **`0x3E4D0`** |
| State-label pool | DGROUP + `0x3CC2` (= guest `0x421A1`) |
| Player-name table | DGROUP + `0x5EDA` (18-byte records, `"Player1 "…"Player6 "`) |

So the game's C globals begin around guest linear `0x3E4D0`. The dump was captured at the **setup/menu** screen, so per-match economy tables (the far heap at segment ~`0x93DB`, see §4) were still zero-filled.

---

## 3. The DIVERGANCE table — an authoritative list of game-state arrays

Lords of the Realm's modem 2-player mode keeps both machines in lock-step. When they disagree it invokes a **desync detector** the developers named **DIVERGANCE** (their spelling), which prints:

```
DIVERGANCE    Please note numbers and
the sequence of your previous actions.
Primary player please wait
whilst secondary carries out tests.
```

To detect divergence it checksums a fixed set of memory regions and reports which named region differs. That naming table is the game's own inventory of its authoritative state, in order:

```
Counties · Units · Players · Figures · Armies · (Unused) · Castles ·
(Unused) · Prices · (Unused) · Sieges · (Unused)
```

### 3.1 Region-registration code

The routine that registers these regions lives in an overlay near EXE file `0x77156`. Each entry is built with a sequence like (decoded 8086):

```asm
cmp  word [bdf7], 4        ; branch on a mode/label flag
push 0x0026                ; \
push 0x0240                ;  |  per-array parameters
push 0x93db                ;  |  <-- far SEGMENT of the array data
push 0x0038                ;  |
push 0x0058                ; /
push ds                    ; name pointer (segment = DGROUP)
push 0x3cc2                ; name pointer (offset  = "Counties")
call 0008:2a03             ; RegisterSyncRegion(...)
add  sp, 0x0e
```

The recurring immediate **`0x93DB`** is the far segment where the big per-player arrays are allocated at match start. A second registration helper (`call 0008:2ba5`) handles arrays whose element count is a runtime variable (e.g. armies). Because these segment immediates are relocated by the overlay loader and the arrays are only populated in-match, they are **runtime-dependent** — hence scanning.

### 3.2 The twelve categories, interpreted

| Label | Meaning (from cross-referencing UI text) |
|---|---|
| **Counties** | Per-county economy: fields, fertility, livestock, population, happiness, stores, rations, garrison |
| **Units** | Military unit stocks/records per county (spearsmen, swordsmen, crossbows, longbows, knights) |
| **Players** | Per-lord global state: treasury (Crowns), identity, alliances, difficulty |
| **Figures** | Aggregated report figures (totals shown on the overview: peasants, counties, food, output) |
| **Armies** | Field armies: composition, position, morale, movement |
| **Castles** | Castle designs & construction: wall height, capacity, materials in progress |
| **Prices** | Market prices for the six merchants / tradeable goods |
| **Sieges** | Active siege state: besiegers, defenders, morale, duration |
| **Unused ×4** | Reserved slots in the checksum table |

---

## 4. The economy data model

The playable content was recovered from the UI/label resources (`TEXTCAST.ENG`, `MESSAGES.ENG`, `CASNAMES.ENG`) which the code indexes directly. These labels *are* the field names of the game model.

### 4.1 Resources & goods

- **Treasury:** measured in **Crowns** (`"Your treasury contains"`, `"Treasury of"`, `"Crowns"`). Large quantities are stored as **32-bit little-endian integers** (the config block at guest `0x4438A` shows the `value,00 00` dword pattern, e.g. `0x2580 = 9600`).
- **Food:** Grain, Sheep (→ wool + mutton), Cattle (→ dairy + beef), and Ale (from Hops). Rationing options: `Eat grain`, `Eat sheep`, `Eat cattle`, `Drink ale`, at levels `Quarter / Normal / Double / Triple`.
- **Construction materials:** **Stone** (quarried), **Timber/Wood** (forested), **Iron** (bought/mined) → feed **Weapons** and **Castle** building.
- **Labor pools** (allocatable peasants): `Shepherds`, `Builders`, `Quarryers`, `Foresters`, `Armorers`, plus grain/cattle field workers and *idle*.

The compact column abbreviations found in DGROUP (`Cs By Af St Wd Ar Ir Ha Tx  Av of  S W I A`) are the overview/report headers: **St**=Stone, **Wd**=Wood, **Ir**=Iron, **Ar**=Armaments, **Ha**=Happiness, **Tx**=Tax, with **S W I A** labelling the unit columns.

### 4.2 Counties

The map is England & Wales (`"England and"` + `"Wales"`) plus `Abroad`. County names are an ordered array in `TEXTCAST.ENG`:

> Cornwall, Devon, Somerset, Dorset, Wiltshire, Hampshire, Sussex, Kent, Middlesex, Gloucestershire, Oxfordshire, Buckinghamshire, Cambridgeshire, Northamptonshire, Herefordshire, Staffordshire, Leicestershire, Nottinghamshire, … (the array continues; index 0 in the extracted set = Gloucestershire).

The map itself is `CONQUEST.MAP` / `CONQUES2.MAP` — a **128 × 100 byte tile grid** (12,802 bytes = 2-byte header + 12,800 tiles). Low byte values (`0x04`–`0x0B`) are terrain/fertility classes; higher ranges encode county ownership/features. `CASTLMAP.DAT` holds castle-tile maps and is mirrored into low guest memory at load.

### 4.3 Per-county state (fields recovered from UI)

Fertility & rotation (`Fertility`, `Crop rotation`, `Fallow`, `Field reclaimed`), livestock dynamics (`Flock grows/falls by`, `Herd grows/falls by`, `Need lambs/calves`), production (`Wool produced`, `Dairy produce`, `Harvested grain/hops`), and social state (`Population`, `Happiness`, `Immigration`/`Emigration`, `Conscription`, disease/`starving`).

---

## 4a. The treasury — confirmed live by differential scan

The treasury was pinned and **confirmed against the running game**, including a false lead worth recording because it is a classic trap.

**The trap (a display/report copy).** A tidy 6-record array in static DGROUP (base `+0x2013`, stride `0xE0`) has a field at `+0x40` that read `40` for every lord while the screen showed "40 crowns", and a neighbouring field even matched the on-screen cattle count. It looked exactly like the treasury — but **writing it did nothing**: the on-screen crowns stayed `40` after a refresh. This array is a **"Figures" display/report snapshot** (the DIVERGANCE registration code literally toggles its label between *Players* and *Figures*), not the value the game spends from. Writing a report copy is a no-op.

**The real treasury (found by differential scan).** Every address holding `40` was recorded, the player advanced one season so the treasury became `89`, and the two sets were intersected. Two survivors remained; one was the authoritative store:

| Property | Value |
|---|---|
| **Per-lord treasury array** | 6 × Int32, stride 4, base **DGROUP − 0xC3BD** |
| **Active-player display cache** | Int32 at **DGROUP + 0x8C5F** |
| Human's slot | the array index whose value equals the cache |

At the moment of capture the array read `[96, 98, 89, 96, 95, 97]` — six lords who had each collected a season's income from the shared starting `40`; index 2 (`89`) matched the player's on-screen treasury, and the cache held `89` too. Writing the array slot **changed the spendable gold**; writing the cache made the **displayed** number match. The trainer therefore writes both, and identifies the human as the slot equal to the cache. A poke of `424242` was confirmed on-screen by the player.

Both offsets live in the statically-linked image, so they are fixed for a given `LORDS.EXE`; the trainer re-anchors DGROUP each run by signature (§3) and validates the layout (all six slots plausible, cache matches one) before offering the cheat — otherwise it falls back to the scanner.

**Goods (cattle, grain, wool, …) are per-county**, not a single global: each county record carries its own stores and the market screen shows the kingdom aggregate.

- **Grain** for the managed county was pinned by the differential method (28 → 16 across a season) to a single Int16 at **DGROUP + 0xB1F0**, confirmed by a poke (`1234`) that changed the on-screen number and **persisted across a view change** — the authoritative store, not a redraw copy.
- **Cattle** was pinned once the player made the herd change (7 → 6): three survivors, disambiguated in one shot by writing `100/200/300` to each and asking which the game displayed. The winner was **DGROUP + 0xB202** (grain + 0x12, Int16); the other two were copies that don't drive the display. A first guess (the `7` six bytes after grain) had been a false match — its write did nothing — a reminder that proximity isn't proof.
- The trainer exposes both as "Grain / Cattle (your province)" cheats, each validated before use. **The remaining goods** (sheep, wool, ale, stone, wood, iron) read `0`, so there's nothing to match on yet — each needs its own change-based scan and is left to the Advanced scanner.

Once the player owned several counties, the full **per-county record array** was mapped by correlating five counties' distinct goods values:

- **County records are 0x168 (360) bytes** each; consecutive counties differ by that stride (verified across Dyfed, Powys, Gwynedd, Cheshire, Lancashire at relative indices −2, −1, 0, +6, +7 from the anchor).
- Within a record, goods are Int16 at fixed offsets from the grain field: **grain +0x00, cattle +0x12, sheep +0x1e, wool +0x46** — confirmed identical across every county.
- The anchor county (Gwynedd in the test game) has its grain field at DGROUP `+0xB1F0`, so county *n* is at `0xB1F0 + n*0x168`.

The trainer **enumerates every county record** (index `0…63`, grain field = `0xB1F0 + (index − 24)*0x168`, anchor Gwynedd = index 24) and lists the grain/cattle/sheep/wool of each slot that validates and isn't empty; the user ticks any subset and maxes them in one pass. The county the player currently has open is still read from the viewed-county index at DGROUP `+0xC238` (found by diffing two county views — Dyfed read 22, Gwynedd 24, matching Dyfed = Gwynedd−2) and is flagged "viewing" in the list. Counties are shown by slot number because ownership isn't cleanly mapped (below), not by name.

**Ownership** is *not* a per-record field — a regional-grouping byte (e.g. `+0x8e`, and a parallel region array at DGROUP `+0x4dfb`) spans owned and unowned neighbours alike. The human's actual owned-county list *does* exist as a plain index array (observed `[22,23,24,30,31]` with a terminator) in a separate structure, but its per-player layout wasn't fully mapped, so the trainer uses the safer current-county selector rather than iterating that list. The **kingdom-wide materials** (iron, stone, wood) and the **weapons total** (59450) are global, not per-county, but don't sit in a clean aligned cluster and weren't individually confirmed; weapons in particular never appears as a plain 16/32-bit value, suggesting it's a computed sum.

## 5. Players, opponents & merchants

- **Human/AI slots:** up to 6 (`Player 1..6`, `Modem Player`, `Neutral`). Names default to `"Player1 ".."Player6 "` in the 18-byte DGROUP name table.
- **AI personalities** (with portrait assets `baron.vpx`, `young.vpx`, `bishop.vpx`, `countess.vpx`, `earl.vpx`): **The Baron, The Knight, The Bishop, The Countess, The Earl**. Their diplomacy voice is visible in `MESSAGES.ENG` (the Church/Bishop threatens excommunication; the Baron is bluntly aggressive, etc.).
- **Difficulty** is set independently for **economy** and **warfare**: `Novice / Normal / Expert`.
- **The six merchants** (market prices = the *Prices* array): **Mr. Goldpenny, Jock McTooth, Little Frank, Flemish Bill, Weasel Willy, Bernard Slap**.

---

## 6. Military & siege model

- **Unit types:** Peasants, **Spearsmen**, **Swordsmen**, **Crossbows**, **Long bows**, plus Knights/Yeomen; hired **Mercenaries**. Each has `Men/Upkeep` and a per-season upkeep cost.
- **Army ops:** `Raise army of`, `Total seasonal upkeep of`, `Conscription`, `Disband`, `Foraging in`, `Besieged in`.
- **Siege engines:** `Catapult`, `Trebuchet`, battering rams, scaling **`Ladders`**, plus assets `TOWER.AB8`, `TREBUCHE.AB8`, `BATTERIN.AB8`.
- **Siege state machine:** `Siege has been levied/withstood for`, `Morale amongst the siege troops/defenders`, `Mutiny is imminent`, and the castellan's decision options `Ask for quarter / Attempt escape / Hold fast`. Defenders track a `garrison`, `supplies`, and wall `Capacity`.
- **Castle building:** wall `height`, `Capacity`, `Extend`, `Demolish`, `In ruins`, with named castles from `CASNAMES.ENG` (Senlac, Camelot, Tintagel, Windsor, …). Blueprints are saved to `BLUEPRT1.DAT`.

---

## 6a. Army movement — confirmed live by a four-dump differential (campaign map)

"Unlimited movement" needed a campaign-map army's **per-turn move budget**. It was pinned with four RAM dumps across two moves in the same match (DGROUP `0x3E4D0` throughout):

- **E1** — before / after moving an army **12 spaces**, which *exhausted* its moves (the army then could not move again that turn).
- **E2** — on a **fresh turn** (moves reset), before / after moving the same army **one space south**.

Method mirrors the treasury (§4a): locate guest 0 by the BDA fingerprint in each dump, diff the 640 KB conventional window, and intersect the two experiments. A single move's diff is dominated by **decoys** that must be recognised and discarded:

- The **campaign-map tile grid** redrawing (`~0x29000–0x2C000`) and **overlay code paging in** (thousands of bytes at whatever segment the active overlay occupies) — bulk changes unrelated to state.
- A **transient move-path buffer** at `~0x4B306`: 10-byte records (`x, 0x0002, y, extra, 0x4E20`) written fresh by the move, all-zero before it. Its `+4` field counts the path steps `1…N` — the literal "12 spaces" — so it *looks* movement-related but is just a route log.
- The army's **on-screen position**: the record at `~0x999B0` (a linked node with `100/100` stat pairs), fields `+0x10/+0x22/+0x24`. These track the sprite and are **not** the budget — a classic trap: the 12-space move ran `+0x22` `13→4`, which looked like "moves remaining", but the clean 1-space move ran the same field `9→27`, proving it a coordinate.

The move budget is the one Int16 that satisfies the full four-dump signature — **it depletes when the army moves and resets upward at the start of a turn**:

| Property | Value |
|---|---|
| Move counter | Int16 in the per-match **unit heap**; this session guest `0x9434A` (= DGROUP `+0x55E7A`) |
| E1 (12-space move) | `3 → 0` — exhausted, matching "couldn't move again" |
| E2 before (fresh turn) | resets to `5` |
| E2 (1-space move) | `5 → 4` |

Exactly one address matches at Int16 width; a byte-width sibling at `0x943EE` resets to `12`. Both being per-unit fits the model — an **army stacks units of different speed** (knights outrun foot), each carrying its own per-turn counter, so 2–3 co-exist.

Like the treasury and economy block, this lives in a **per-match heap allocation** (segment `~0x94xx`) re-created at a fresh address every game, so it is **runtime-dependent**, not a fixed offset. The trainer finds it by behaviour, not address: snapshot all Int16 → keep those that **Decreased** after a move → keep those that **Increased** after ending the turn → freeze the survivors above the per-turn max (the "Unit movement" cheat), which keeps the army moving without limit.

---

## 7. Implications for the trainer

1. **Attach, don't patch.** The game runs under DOSBox-X; the trainer opens the `dosbox-x` process and reads/writes guest RAM. No files are modified.
2. **Find guest RAM by fingerprint** (§2.1): 16 MB+4 KB `rw-` region → BDA `F8 03 F8 02` → `guestBase`.
3. **Scan, don't assume.** Because per-match tables are relocated and only populated in-play, the trainer locates values (treasury, food, materials, populations) with a Cheat-Engine-style scanner: *first scan* for a known on-screen number, play a season, *next scan* to narrow, then **freeze** or **set**. Default type is **Int32** (with Int16 available), scanning the 640 KB conventional window `0x00000–0xA0000`.
4. **Reference hints.** The DGROUP anchor (`DS = 0x3E4D`) and the DIVERGANCE category list are shipped as documentation so a user knows *what* to look for and roughly *where* (the game globals cluster just above `0x3E4D0`; the economy heap around segment `0x93DB` once a match starts).

---

## 8. Key addresses & signatures (quick reference)

| What | Where |
|---|---|
| Guest RAM region (host) | `rw-` private, size `0x1001000` |
| BDA fingerprint | `F8 03 F8 02` at `guestBase + 0x400` |
| Guest base in this dump | file `0x47B9000 + 0x40` |
| Runtime DGROUP (DS) | segment `0x3E4D` (linear `0x3E4D0`) |
| State-label pool | DGROUP `+0x3CC2` (`Counties…Sieges`) |
| **Treasury array (Crowns)** | 6×Int32 stride 4 at DGROUP `−0xC3BD` (authoritative, confirmed) |
| **Treasury display cache** | Int32 at DGROUP `+0x8C5F` |
| "Figures" report copy (NOT treasury) | DGROUP `+0x2013`, stride `0xE0` — writing it is a no-op |
| **County record array** | stride 0x168; anchor county grain field at DGROUP `+0xB1F0` |
| **County goods (Int16)** | grain +0x00, cattle +0x12, sheep +0x1e, wool +0x46 (from grain field) |
| **Army move counter (Int16)** | per-match unit heap; found by behaviour (depletes on move, resets each turn). This session guest `0x9434A` = DGROUP `+0x55E7A` (sibling byte at `0x943EE`) |
| Move-path buffer (transient) | 10-byte records at `~0x4B306`; `+4` counts the path steps `1…N` |
| Player-name table | DGROUP `+0x5EDA`, 18-byte records |
| DIVERGANCE register code | EXE file `~0x77156` (overlay) |
| Economy far heap (in-match) | segment `~0x93DB` (relocated) |
| Map grid | `CONQUEST.MAP`, 128×100 tiles |
| Treasury unit / width | Crowns, 32-bit LE int |

*Generated from Ghidra 12.1.2 static analysis + DOSBox-X memory dump inspection.*
