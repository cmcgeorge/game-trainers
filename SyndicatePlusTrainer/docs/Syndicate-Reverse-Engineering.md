# Syndicate Plus — Reverse-Engineering & Game-Mechanics Reference

*A technical teardown of the DOS release of **Syndicate** (Bullfrog / Electronic Arts, 1993) and
its **American Revolt** data disk, as bundled in the GOG "Syndicate Plus" package.*

This document records what was recovered by statically reverse-engineering the game's
protected-mode executable with **Ghidra 12.1.2** (`C:\ghidra\ghidra_12.1.2_PUBLIC\`), cross-checked
against the in-game strings and the shipped `.\Game\manual.pdf`. It is organised as a *mechanics
reference*: binary internals first, then the game systems those internals implement.

---

# 1. Scope & Method

## 1.1 What was analysed

| Artefact | Path | Size | Role |
|---|---|---|---|
| Main game (Syndicate) | `.\Game\SYNDICAT\MAIN.EXE` | 518,713 B | Primary RE target |
| Sound variants | `.\Game\SYNDICAT\SMAIN.EXE`, `GMAIN.EXE` | ~499 KB | Alternate audio builds |
| Intro players | `.\Game\SYNDICAT\INTRO.EXE`, `SINTRO.EXE`, `GINTRO.EXE` | ~45–48 KB | Cutscene players |
| American Revolt | `.\Game\DATADISK\MAIN.EXE` | 553,529 B | Expansion campaign |
| DOS extender | `.\Game\DOS4GW.EXE` | 231,179 B | Tenberry DOS/4GW 1.x |
| Launcher | `.\Game\SYND.EXE` | 105,981 B | GOG/loader shim |
| Game data | `.\Game\SYNDICAT\DATA\*.dat`, `.\Game\DATA\*.dat` | — | Palettes, maps, missions, text |

All executables share **byte-identical MZ + LE headers**; they differ only in payload. The analysis
below targets `.\Game\SYNDICAT\MAIN.EXE`; the other builds parse identically.

## 1.2 Tooling pipeline

Ghidra 12.1.2 ships **no loader for the LE (Linear Executable) format** used by DOS/4GW — its
`ghidra/app/util/bin/format/lx/LinearExecutable` class is a 557-byte stub with no opinion/loader.
Loaded naively, Ghidra sees only the 16-bit MZ real-mode stub and misses the entire 32-bit game.

The workaround built for this teardown (see `.\re_work\le_loader.py`) reconstructs the flat 32-bit
image before import:

1. Parse the LE header, **object table**, **object page map**, and **fixup tables**.
2. Reassemble the linear image from the file's sequential 4 KB pages.
3. Apply all internal **relocations/fixups** (patching absolute addresses).
4. Emit a flat `.bin` plus a JSON layout descriptor.
5. Import into Ghidra as **raw binary**, `x86:LE:32:default`, base `0x10000`, then auto-analyse.

```bash
# Reconstruct the flat image (applies 10,060 relocations, 0 out-of-bounds)
python le_loader.py Game/SYNDICAT/MAIN.EXE main
# Import + analyse headlessly
analyzeHeadless.bat <proj> SyndicateMAIN -import main.bin \
    -loader BinaryLoader -loader-baseAddr 0x10000 -processor "x86:LE:32:default"
```

Auto-analysis recovered **533 functions** and **776 defined strings** from a clean image.

---

# 2. Executable Structure

## 2.1 Container format

The file is a classic **DOS/4GW bound executable**: a real-mode `MZ` stub whose `e_lfanew`
(`0x3C`) points at an **`LE`** (Linear Executable) header at file offset `0x28B8`.

```
MZ stub ──► LE header @0x28B8 ──► object table ──► page map ──► fixups ──► page data @0x15C00
```

| LE field | Value | Meaning |
|---|---|---|
| Signature | `4C 45` (`"LE"`) | Linear Executable |
| CPU type | `0x02` | Intel 80386 |
| Page size | `0x1000` | 4096 bytes |
| Page count | `0x69` (105) | Total memory pages |
| Entry (obj:off) | obj1 : `0x2D85C` | Program entry point |
| Init stack (obj:off) | obj2 : `0x13E60` | Top of data object |
| Fixup section | `0x12EE9` bytes | 10,060 internal relocations |

## 2.2 Object (segment) layout

Four LE objects define the linear address space. Reconstructed base addresses:

| Obj | Kind | Base VA | Virtual size | File pages | Notes |
|---|---|---|---|---|---|
| 1 | **CODE** (r-x) | `0x10000` | `0x3FDF4` (~262 KB) | 64 | All executable code |
| 2 | **DATA** (rw-) | `0x50000` | `0x13E60` (~82 KB) | 13 | Initialised data + strings; stack at top |
| 3 | **BSS** (rw-) | `0x70000` | `0x00C00` | 0 | Pure zero-fill, no file backing |
| 4 | **DATA** (rw-) | `0x80000` | `0x1C632` (~114 KB) | 28 | Large tables / heap seed |

The Ghidra image therefore spans `0x10000`–`0x9CFFF` (`0x8D000` bytes).

## 2.3 Toolchain fingerprint

The entry point immediately identifies the compiler:

```
0x3D85C:  EB 78                jmp  +0x78              ; skip copyright banner
0x3D85E:  "WATCOM C/C++32 Run-Time system. (c) Copyright by WATCOM ..."
```

The binary is **Watcom C/C++ 32** compiled, linked for DOS/4GW. This matters for RE:

- **Register-based `__watcall`** calling convention (args in `EAX, EDX, EBX, ECX`), not cdecl.
- No dynamic imports — OS services reach DOS via **DPMI / `int 21h`** through the extender, which
  is why the relocation pass found **0 imported fixups** (all 10,060 are internal).
- Watcom's flat memory model: near 32-bit pointers throughout; the "objects" are effectively one
  flat address space, not far-segmented.

---

# 3. Runtime Architecture

## 3.1 Function inventory

Of the 533 recovered functions, the largest are the engine's hot paths. Because Syndicate is built
as a **screen/state machine driven by function-pointer tables**, most large routines show
`calledBy = 0` in direct call analysis — they are dispatched indirectly through jump tables, a
hallmark of the Bullfrog "screen" architecture.

| Address | Size (bytes) | Likely role |
|---|---|---|
| `FUN_0001fbc0` | 11,288 | Primary mission simulation / update loop |
| `FUN_00025a10` | 5,280 | Called 7× — core update service |
| `FUN_00034ad0` | 4,944 | Screen renderer / isometric blit |
| `FUN_00018ff0` | 4,678 | Input / agent command dispatch |
| `FUN_00045f6a`, `FUN_000470f5` | ~4,500 | Menu / UI screen handlers |
| `FUN_00012880` | 3,860 | Entity AI / pathing |

## 3.2 Identified subsystems

Cross-referencing string usage pinned several subsystems to concrete functions:

| Subsystem | Function(s) | Evidence (string / behaviour) |
|---|---|---|
| Fatal-error / memory manager | `FUN_0001abf0` | `"not enough memory to allocate %s"`, `"memory control blocks damaged %s"`, `"%d error(s) occured.exiting."` |
| **Asset decompressor** | `FUN_0001b1a0` | `"ERROR decompressing %s"` — assets are stored compressed |
| File open wrapper | `FUN_0001b210` | `"ERROR opening %s"` |
| Game-data loader | `FUN_00010e60` | opens `data/game%02d.dat` |
| Mission loader | `FUN_000382e0` | `data/miss%02.2d.dat`, `data/miss%01d%02.2d.dat` |
| Save/load | `FUN_00038350/420/540` | `%s/%02.2d.gam`, base path `c:/synd/save` |
| Mission naming | `FUN_00023f10` | indexes the mission code-name table (§6.4) |
| Multiplayer / NetBIOS | `FUN_0002a880…0002b260` | `"netbios"`, `"NET error %s[%d]…"`, `einet.c` |
| FPU guard | `FUN_000404ef` | `"Floating-point support not loaded"` |

## 3.3 Asset & data pipeline

The engine loads numbered, compressed `.dat` blobs. Format strings recovered from the data segment:

```
data/game%02d.dat      data/map%02d.dat       data/hpal%02d.dat     (palettes)
data/miss%02.2d.dat    data/miss%01d%02.2d.dat (per-mission maps)
data/gamefm.dll  data/gamedg.dll  data/syngame.xmi  DATA/SAMPLE.*    (sound: FM/digital drivers, XMI music)
data/m*.dat            (menu/screen graphics: mtitle, mbrief, mdebrief, mresrch, mselect, mmap, mlosa, moption …)
```

Key takeaways for RE of the data files:

- **`.xmi`** = Extended MIDI (Miles/AIL) music; `gamefm.dll`/`gamedg.dll` are the Miles Sound System
  FM and digital drivers.
- Palettes are per-scene (`hpal00`–`hpal…`), consistent with the game's dark, per-mission tints.
- Missions are addressed both as `miss%02d` and `miss%d%02d`, i.e. **base game vs. data-disk
  (American Revolt) numbering** share the loader.

---

# 4. Localisation System

All human-readable game text lives in the **initialised data object** (`0x50000+`) as packed,
null-terminated strings, indexed by **arrays of 32-bit pointers**. Each logical item stores **three
consecutive pointers — English, French, Italian** — matching the manual's translation credits
(French: *Art of Words*; Italian: *C.T.O.*).

```
item_names[i][0] -> English   e.g. 0x54B10 -> "PERSUADERTRON"
item_names[i][1] -> French            0x54B14 -> "PERSUADOTRON"
item_names[i][2] -> Italian           0x54B18 -> "PERSUADERTRON"
```

This triple-pointer layout is how the running game switches language (via `.\Game\DATA\LANGUAGE.DAT`
and the `reset_language.bat`/`lang\` machinery in the GOG wrapper). Recovering the ordering of these
tables directly yields the game's internal enumerations (below).

---

# 5. Reverse-Engineered Data Tables

## 5.1 Item enumeration (mods + weapons + equipment)

Decoded from the item-name pointer table at VA `0x54A38` (English column). This is the **exact
internal item order** used by the equip/mods/research code:

| Index | Item | Category |
|---|---|---|
| 0–2 | **LEGS** V1 / V2 / V3 | Modification |
| 3–5 | **ARMS** V1 / V2 / V3 | Modification |
| 6–8 | **CHEST** V1 / V2 / V3 | Modification |
| 9–11 | **HEART** V1 / V2 / V3 | Modification |
| 12–14 | **EYES** V1 / V2 / V3 | Modification |
| 15–17 | **BRAIN** V1 / V2 / V3 | Modification |
| 18 | **PERSUADERTRON** | Weapon (non-lethal) |
| 19 | **PISTOL** | Weapon |
| 20 | **GAUSS GUN** | Weapon (explosive) |
| 21 | **SHOTGUN** | Weapon |
| 22 | **UZI** | Weapon |
| 23 | **MINI-GUN** | Weapon |
| 24 | **LASER** | Weapon (energy) |
| 25 | **FLAMER** | Weapon |
| 26 | **LONG RANGE** (rifle) | Weapon (sniper) |
| 27 | **SCANNER** | Equipment |
| 28 | **MEDIKIT** | Equipment |
| 29 | **TIME BOMB** | Equipment |
| 30–32 | **ACCESS CARD** / ACCESS CARD1 / ACCESS CARD2 | Equipment |
| 33 | **AUTO MAPPER** | Equipment |
| 34–35 | **ENERGY SHIELD** / SHIELD1 | Equipment |

The three `ACCESS CARD` and two `SHIELD` entries are internal state variants (e.g. card tiers /
shield active-vs-icon), collapsed to single items in the UI.

Each of the 6 body modifications ships in **three versions**, and each item carries a
**multilingual flavour description** in a parallel pointer table (VA `~0x54988`). Sample
English descriptions recovered:

```
CYBERMESH LEGS.  Plasteel core with synthetic muscle fibre. Superb response and balance
                 coupled with high speed.
CYBERMESH ARMS.  Plasteel core with synthetic muscle fibre. Excellent tactile control and
                 weight loading.
METAL CHEST CAGE. All internal organs are shielded by heavy metal casing.
HEART ACCELERATOR AND MONITOR. Heart rate is almost doubled, allowing quicker hormone
                 distribution.
VISION ENHANCER WITH LIMITED ZOOM. Allows near-perfect sight even at night.
```

## 5.2 Territory enumeration

Decoded from the territory-name pointer table at VA `~0x541A0`. **50 territories** in internal
order, beginning at Africa and sweeping the globe (English names from the manual, order from the
binary):

```
 0 Algeria        10 South Africa    20 China          30 Newfoundland    40 Paraguay
 1 Libya          11 Western Europe  21 Far East       31 Kamchatka       41 Brazil
 2 Mauritania     12 Scandinavia     22 Kazakhstan     32 W. Australia    42 Venezuela
 3 Nigeria        13 Central Europe  23 Siberia        33 N. Territories  43 Mexico
 4 Iraq           14 Eastern Europe  24 Urals          34 E. Australia    44 Southern States
 5 Arabia         15 Iran            25 Mongolia       35 New South Wales 45 California
 6 Sudan          16 India           26 Greenland      36 Colombia        46 Colorado
 7 Zaire          17 Pacific Rim     27 NE Territories 37 Peru            47 New England
 8 Kenya          18 Indonesia       28 Yukon          38 Argentina       48 Mid West
 9 Mozambique     19 (…)             29 Alaska         39 Uruguay         49 Rockies / Atlantic Accelerator
```

The map's adjacency (which territory unlocks which) is driven by `data/game%02d.dat` /
`data/map%02d.dat`; the player's home base is **Western Europe** (index 11).

## 5.3 Agent / civilian name pool

The random-name generator draws surnames from an embedded table at VA `~0x539E7`. These are the
**Bullfrog development team's own surnames**, e.g.:

```
AFSHAR ARNOLD BAIRD BALDWIN BLACK BOYD BOYESEN BRAZIER BROWN BUSH CARR CHRISMAS CLINTON COOPER
CORPES DAWSON DONKIN DISKETT DUNNE EDGAR EVANS FAIRLEY FAWCETT FLINT FLOYD GRIFFITHS HARRIS
HASTINGS HERBERT HICKMAN HICKS HILL JAMES JEFFERY JOSEPH JOHNSON JOHNSTON JONES LEWIS LINDSELL
LOCKLEY MARTIN MCENTEE MCLAUGHLIN MOLYNEUX MUNRO MORRIS MUMFORD NIXON PARKER PRATT REID RENNIE
RICE RIPLEY ROBERTSON ROMANO SEAT SIMMONS SNELLING TAYLOR TROWERS WEBLEY WELLESLEY WILD WILLIS
```

(`MOLYNEUX`, `COOPER`, `HILL`, `TROWERS`, `CORPES` etc. map onto the manual's credits page.) Every
agent, guard and civilian is named by concatenating a random surname with a "SPECIAL AGENT" title.

## 5.4 Mission code-names

The mission-title table (VA `0x501A0`, indexed by `FUN_00023f10`) holds the campaign's named
objectives, e.g. `NUK THEM`, `WATCH THE CLOCK`, `DO IT AGAIN`, `ROB A BANK`, `TO THE TOP`,
`COOPER TEAM`. `MULTIPLAYER LEVEL 1…10` form a separate multiplayer map list.

---

# 6. Core Game Mechanics

The systems below are the behaviour those tables and functions implement. Where a rule is stated as
an exact formula it was confirmed from the game data / manual text embedded in the binary.

## 6.1 Agents & the Cryo Chamber

- A Syndicate begins with **8 agents** in cryo storage, the first **4** deployable per mission.
- Agents are **permanent casualties** — a killed agent forfeits *all* weapons and mods and is gone
  forever. Losing all 8 is **game over** (the airship self-destructs — the "Crashing Airship"
  screen).
- The **only** way to grow the roster past 8 is to **persuade enemy agents** (§6.6); persuaded
  enemy agents that survive to the evacuation zone enter the Cryo Chamber with their gear and XP.
- Agents accrue weapon experience across missions, improving handling of that weapon.

## 6.2 IPA system (real-time behaviour drugs)

Each agent has three chemically-driven levels, injected live during a mission via three bars:

| Level | Controls | High setting | Low setting |
|---|---|---|---|
| **I** — Intelligence | Reaction to situations | Self-preserves, evades danger, may abort a risky move | Walks blindly into danger |
| **P** — Perception | Firing precision + threat detection range | Accurate, spots enemies early | Slow to notice, wide shots |
| **A** — Adrenaline | Speed of reaction / movement | Fast but, if I is low, erratic (fires early/wide) | Sluggish |

**Dependency model:** each injection darkens the bar (drug "used up") and pushes the centre line
right, so **future injections must be larger for the same effect**. **Resting** an agent (retarding
the bar left when unthreatened) reduces dependency; the longer an agent is rested, the stronger the
later boost. This is the game's built-in cost for spamming stimulants.

## 6.3 Body modifications (cyborg augmentations)

Six slots, each upgradeable V1→V2→V3 (metal → plasteel → cybermesh). Effects:

| Mod | Effect | Strategic note |
|---|---|---|
| **Legs** | Movement speed | V3 dramatically outruns pursuit |
| **Arms** | Carry capacity without hindrance | Needed to lug heavy weapons (Minigun) |
| **Chest** | Damage resistance to direct hits; **V2/V3 add the self-destruct charge** | Enables `Ctrl-D` self-destruct |
| **Heart** | Overall strength & durability (effective HP) | Raw survivability |
| **Eyes** | Hazard awareness + firing accuracy | Force-multiplies Perception |
| **Brain** | Decision quality **and Persuadertron potency** | Gates recruitment efficiency (§6.6) |

Version gating: **V3 of any mod cannot be researched until that mod's V2 is fully developed.**

## 6.4 Weapons

Nine weapons (item indices 18–26). Roles as documented, ordered by internal index:

| Weapon | Range | Profile / role |
|---|---|---|
| **Persuadertron** | Short | Non-lethal; converts targets to followers (§6.6) |
| **Pistol** | Medium | Free starter; cheap backup, quickly outclassed |
| **Gauss Gun** | Long | 3 rockets; high-explosive, kills tanks & crowds |
| **Shotgun** | Short | Wide spread, high close damage, poor range |
| **Uzi** | Medium | Fast auto, cheap ammo — the workhorse |
| **Mini-Gun** | Med-long | Devastating auto rate; **very heavy** (needs Arms mod) |
| **Laser** | Very long | Piercing energy beam; anti-vehicle & sniping; scarce ammo |
| **Flamer** | Very short | Sticky burning jelly; anti-vehicle / crowd control |
| **Long-Range Rifle** | Very long | Accurate single-shot; assassination / overwatch |

Weapon detail screens expose per-weapon **Cost / Ammo / Range / Shot (per-round cost)**; these are
stored as a parallel stats array indexed by the same item enum. Weapons are **concealed under the
overcoat** until selected — drawing one flags the agent to police. Ammo depletes and can be
**reloaded cheaply** between missions or **grabbed** from any corpse (yours or the enemy's).

## 6.5 Special equipment

| Item | Effect |
|---|---|
| **Scanner** | Aerial overlay: reveals people, vehicles, equipment; **pinpoints mission targets** with an identifier beam |
| **Medikit** | Restores one agent's health, **single use** |
| **Time Bomb** | Timed area explosive; drop with right-click, damages people/vehicles (not structures) |
| **Access Card** | Opens restricted security doors; **disguises the agent as police**, diverting police units |
| **Auto Mapper** | Automap of the mission zone |
| **Energy Shield** | Force field blocking *all* projectiles; **very short duration** (huge power drain) |

## 6.6 The Persuadertron model (recruitment)

The single most important mechanic to reverse cleanly. Civilians are **always** persuadable. To
convert a *defended* target you must have accumulated enough **persuasion points** from followers you
already control. Each follower contributes points by type:

```
Persuasion contributed:   Civilian = 1    Guard = 3    Policeman = 4    Enemy Agent = 32
```

The **cost** (points required) to convert a target depends on the agent's **Brain version**:

| Brain version | Guard | Policeman | Enemy Agent |
|---|---|---|---|
| **None (Brain ø)** | 4 | 8 | 32 |
| **V1** | 2 | 4 | 16 |
| **V2** | 1 | 3 | 11 |
| **V3** | 1 | 2 | **8** |

*(Civilians = always persuaded, at any Brain level.)*

Worked examples (from the embedded rules):

- **Brain ø:** 5 civilians (5 pts) + 1 guard (3 pts) = 8 pts → enough to persuade **1 policeman**.
- **Brain V3:** 1 civilian (1) + 1 guard (3) + 1 police (4) = 8 pts → enough for **1 enemy agent**.

**Consequence:** a **V3 Brain** turns recruitment into a snowball — persuade a handful of civilians,
roll them into guards and police, and use the accumulated points to flip **enemy agents** (who then
each add a massive 32 points). Only persuaded **enemy agents** join the Cryo Chamber; other
persuaded personnel who survive to evac are cashed in as bonus income.

## 6.7 Research & development

- Funds one item at a time (equipment **or** a modification). New designs appear in the Equip/Mods
  lists on completion.
- **Dev-cost ↔ time curve:** baseline is **100% complete in 10 days**. `Funding +` raises cost and
  steepens the curve (down to a **1-day** minimum); `Funding –` reclaims cash and flattens it (up to
  the **10-day** maximum).
- **Version gating:** a mod's **V3 requires its V2 fully developed** first.
- All dev cost is deducted from the running **Budget**.

## 6.8 Economy: budget, tax & population mood

- A single **Budget** carries across the whole campaign; it pays for equipment, mods, research,
  info/map enhancements, and mission overheads.
- After capturing a territory you set its **tax rate**. Higher tax = more income, but the population
  **mood** degrades (…→ Content → unhappy → **rebellious**).
- A territory pushed past *Content* can **rebel**, forcing an extra (paid) mission to re-pacify it,
  and leaving it open to **rival-Syndicate** takeover. Tax is the core risk/return dial: milk
  conquered regions, but **lower tax to cool unrest** before it costs you a territory.
- Income raised is added to Budget **over time**, not instantly.

## 6.9 Mission flow & scoring

```
World Map ─► Mission Brief (buy Info / Map Enhance) ─► Team Selection (Research/Mods/Equip)
          ─► Mission ─► Debriefing ─► (raise tax / choose next territory)
```

The **Debriefing** reports: Mission Status, Agents Used, **New Agents Gained**, Time in Mission,
Enemy Agents Killed, Criminals Killed, Civilians Killed, Police Killed, Guards Killed, **People
Persuaded**, and **Hit Accuracy**. Winning flashes 1–2 adjacent territories as newly available; the
conquered territory recolours to your Syndicate. **Losing** unlocks no new territory and kills any
agents lost in the attempt.

In-mission specials:

- **Panic Mode** (left+right mouse): auto-selects a weapon, sprays fire, and maxes all IPA levels.
- **Self-Destruct** (`Ctrl-D`, needs **Chest V2/V3**): the active agent detonates, clearing the
  surrounding area — a bail-out weapon and a way to end a mission if an unarmed agent reaches the
  target.
- **Weapon grabbing:** an active agent can loot weapons from any corpse; **dead agents drop
  everything**, so recovering downed teammates' gear matters.

---

# 7. Rival Syndicates

Seven AI factions contest the globe. The player is **EuroCorp** (Europe-based). Each has a distinct
behavioural profile (flavour from the manual, encoded per-territory ownership in the map data):

| Syndicate | Region | Behaviour |
|---|---|---|
| **EuroCorp** | Europe (player) | — |
| **The Tao** | Orient | Disciplined, hi-tech, efficient; low civilian wastage |
| **I.I.A.** | North America | Ex-CIA; heavily armed, high collateral |
| **The Castrilos** | South America / Caribbean | Vicious, aggressive |
| **Sphinx Inc.** | Africa / Middle East / Med. | Zealots; fight to the last, hard to kill |
| **Executive Jihad** | Middle East | Few and poorly armed, but kill-hungry |
| **Tasmanian Liberation Consortium** | Australasia | Erratic aim (drunk controllers), not sadistic |

---

# 8. Save-Game Format

Saves are written as `%s/%02.2d.gam` (up to **10 slots**) under a `save` directory (embedded default
path `c:/synd/save`; the GOG build redirects to `.\Game\SYND\SAVE`). Handled by
`FUN_00038350 / FUN_00038420 / FUN_00038540`. A save preserves the campaign globals: budget, owned
territories and their tax/mood, research progress, the Cryo Chamber roster with each agent's mods,
weapons and experience. Multiplayer state is keyed by
`"%01d(player) -- %05d(seed) -- %05d(game turn)"`, confirming a **lockstep, seed-synchronised**
netcode over NetBIOS.

---

# 9. Reproducing This Analysis

```bash
# 1. Flatten the LE image (parses objects/pages/fixups, applies relocations)
python re_work/le_loader.py Game/SYNDICAT/MAIN.EXE main
#    -> main.bin  (0x8D000 bytes, base 0x10000)  +  main.json (layout)

# 2. Headless import + auto-analysis in Ghidra 12.1.2
analyzeHeadless.bat <projDir> SyndicateMAIN -import main.bin \
    -loader BinaryLoader -loader-baseAddr 0x10000 -processor "x86:LE:32:default"

# 3. Extract functions / strings / xrefs (Java GhidraScript — Jython needs PyGhidra)
analyzeHeadless.bat <projDir> SyndicateMAIN -process main.bin -noanalysis \
    -scriptPath re_work/scripts -postScript ExtractInfo.java

# 4. Decode a pointer table (weapons / territories / descriptions)
python re_work/ptrtab.py 0x54A38 108     # item name enum
```

Helper scripts live in `.\re_work\`: `le_loader.py` (LE→flat), `strings.py` (offset strings),
`ptrtab.py` (pointer-table decoder), and `scripts\ExtractInfo.java` (Ghidra extractor).

---

# 10. Live-Memory Trainer (runtime RE)

This appendix documents the runtime memory layout used by the bundled **C# WPF trainer**
(`.\Trainer\`). It was derived by reverse-engineering `MAIN.EXE` in Ghidra *and* correlating three
full DOSBox-X process dumps (Mission Brief, Team Selection, In-Mission) supplied with their region
CSVs.

## 10.1 Finding the game inside DOSBox-X

DOSBox-X stores the emulated guest RAM as one large **private, `rw-` region** (~16 MB) in its
process address space. Within that region the game's LE linear addresses map to the dump at a
**constant offset** (verified across eight anchor strings), i.e. the data segment is contiguous:

```
dump_offset(game_VA) = game_VA + delta          (delta differs per dump/session — ASLR)
```

The trainer does not rely on a fixed address. It **signature-scans** the process for a unique
data-segment anchor and derives every global from it:

| Purpose | Bytes | Game VA |
|---|---|---|
| Primary anchor | `"PERSUADERTRON\0"` | `0x53204` |
| Validation #1 | `"GAUSS GUN\0"` at anchor `+0x3C` | `0x53240` |
| Validation #2 | `"SHOTGUN\0"` at anchor `+0x68` | `0x5326C` |

`game_base = anchor_process_addr - 0x53204`, after which any global is `game_base + VA`.

## 10.2 The campaign structure (player money)

The save routine `FUN_00038420` serialises an array of **8 syndicate records** at VA `0x5E49C`,
**stride `0x417` (1047) bytes**. The current player index is `DAT_00060B16` (VA `0x60B16`, = **0** /
EuroCorp). Confirmed record fields:

| Field | Offset | Type | Meaning |
|---|---|---|---|
| **Budget / money** | `+0x00` | int32 | Player cash — **the trainer target** |
| Time accumulator | `+0x04` | int32 | Sub-day game clock |
| Day | `+0x08` | int16 | 1–365 (wraps → year++) |
| Year | `+0x0A` | int16 | Displayed as the "…AC" year |
| Agent roster | `+0x124` | 8 × `0x28` | Cryo chamber (name, mods, present flag) |

**Money address** = `game_base + 0x5E49C + playerIndex*0x417`.

The value is *authoritative*: the purchase handler `FUN_00035e20` reads it, checks affordability, and
writes back `budget - cost` directly — so overwriting it grants unlimited spendable funds.

```c
cost   = qty * unit_price[item];
budget = *(uint*)(0x5E49C + player*0x417);
if (budget < cost) return FAIL;
*(uint*)(0x5E49C + player*0x417) = budget - cost;   // <-- trainer writes here
```

## 10.3 Cross-check against the supplied dumps

Running the trainer's exact resolution logic over the three dumps (`re_work\validate_trainer.py`):

| Dump | anchor (file off) | player idx | **money** | day / yr |
|---|---|---|---|---|
| Mission Brief | `0x47921B4` | 0 | **30000** | 5 / 85 |
| Team Selection | `0x35351B4` | 0 | **30000** | 7 / 85 |
| In-Mission | `0x33821B4` | 0 | **30000** | 11 / 85 |

Via the region **CSVs**, the money's process VA is `0x1A741FE844C` in all three (same session,
PID 69316) — inside a `0x1001000`-byte private `rw-` block (the guest RAM). The day advancing 5→7→11
confirms the clock fields. Across a *new* game session the guest-RAM base moves (ASLR), which is why
the trainer scans by signature rather than hard-coding the address.

## 10.4 In-mission agent health

Live per-agent health is a **runtime (heap) structure**, not part of the persistent roster (the
roster at record `+0x124` is byte-identical across all supplied dumps, i.e. it does *not* hold live
HP). It was located by **live differential analysis** against the running game:

- With **Agent 3 damaged** and agents 1/2/4 at full health, the health field must read the transition
  `[X,X,X,X]` (undamaged dump) → `[X,X,Y,X]` (live, `Y<X`), *stable* across two live snapshots (unlike
  positions, which keep changing). Scanning for exactly that pattern across strides gave a single
  clean hit.

| Property | Value |
|---|---|
| Field | `int16` **current health** |
| Agent *k* address | `game_base + 0x6C123 + k*0x5C` (k = 0..3) |
| Ped record stride | `0x5C` (92 bytes) |
| Full health | **4096** (`0x1000`) |
| Location | obj2→obj3 heap gap — deterministic (no guest ASLR), mission-scoped |

**Confirmed live:** writing `4096` to Agent 3 (`0x6C1DB`) healed it from `2816 → 4096` and the value
held. Because the ped array is heap-resident, the trainer **self-validates** every slot before
writing (only touches slots reading a sane, alive `0 < hp ≤ 0x2000`), so a shifted layout can never
be corrupted — the feature simply goes inert. God mode re-writes full health to all alive agents
~4×/sec; dead/empty slots (hp = 0) are deliberately left untouched.

No stored pointer to the array exists (it is reached by index arithmetic in the mission loop), so the
address is resolved as `game_base + 0x6C123` — the same `game_base` found by the money signature scan.

## 10.5 In-mission ammo (weapon-object array)

Ammo is **per-weapon**, not per-agent: the player's carried weapons live in a heap array of
**`0x24`-byte records**, ammo (`int16`) at record **`+0xC`**. Located by a **two-round firing
differential** — Player 1's pistol was fired 5, then 5 more, and the field tracked it exactly
`12 → 7 → 2` (the only value that dropped by precisely the shot count and stayed stable between reads).

| Property | Value |
|---|---|
| Record stride | `0x24` (36 bytes) |
| Ammo field | `+0xC`, `int16` (high half `+0xE/+0xF` = 0) |
| Weapon type | `+0x2` byte (pistol `0x01`, flamer `0x05`, …); `+0x3..+0x7` = 0 |
| Position | `+0x8` X, `+0xA` Y (16-bit) |
| Array (this session) | base `0x75678`; grows with the loadout (8 pistols → 12 with 4 flamers) |

**Signature caveat (learned the hard way):** the record's first word (`+0/+1`) is a *per-weapon
dynamic value*, not a constant — pistols happened to read `00 01`, but flamers read `80 04` / `ED 03`.
Keying the scan on `+0/+1` silently excluded flamers. The robust record signature is the invariant
set instead: `+0x3..+0x7 = 0`, `+0xE/+0xF = 0`, a nonzero weapon-type byte at `+0x2`, and a sane
world position at `+0x8/+0xA`. With that, the scan captures **all** weapon types in the contiguous
array (verified live: a squad carrying pistols **and** flamers yields a single 12-record run, and
`FreezeAmmo` tops all 12 — flamers included, `447 → 500`).

The array holds the **player** syndicate's weapons (enemies use their own per-syndicate pool), so
freezing it does not arm the opposition. Because the base/size depend on the loadout and mission, the
trainer **signature-scans** for the longest run of records matching the weapon layout (≥ 4), caches
it, and each tick **tops up** every valid record whose ammo is below the target (never reducing a
weapon that legitimately carries more). Per-record re-validation means a relocated/emptied array is
detected and re-scanned rather than blindly written. **Confirmed live:** `FreezeAmmo` topped Agent 1's
pistol from `2 → 500` and all 8 weapons were refilled.

## 10.6 Heap-address stability

The three heap-resident structures — agent health (`0x6C123`), the entity list, and the weapon array
(`0x75678`) — were all verified to **survive a mission restart at the same VA** (the DOS guest has no
internal ASLR, so allocation is deterministic per build). This is why the trainer can resolve health
by a fixed `game_base`-relative offset, while ammo (whose size varies with the loadout) is located by
signature scan for extra robustness.

## 10.7 In-mission recoil (hit-reaction fields in the ped record)

When an agent is shot it is knocked **backwards** (a stagger/knock-back) independent of the health
loss. The driving fields were located by a **walk-vs-recoil differential** on the same agent's 92-byte
ped record: dump `…105355` (Agent 1 **walking normally**) vs `…105423` (Agent 1 **recoiling from a
shot**), with agents 2–4 standing still in both as a noise baseline.

Diffing all four ped records classifies every changed byte:

| Class | Offsets (from ped base = health addr) | Behaviour |
|---|---|---|
| Health | `+0/+1` (int16) | `4096 → 512` — the damage itself |
| Idle noise | `+64` (byte) | advances for **every** agent each frame (animation/idle timer) |
| Motion | `+6`, `+7`, `+27/+28` X, `+29/+30` Y, `+65`, `+69` | differ from the resting agents in **both** walk and recoil — normal speed/facing/position |
| **Recoil-specific** | **`+3/+4`** (int16), **`+0xA`** (bit `0x08`), **`+0x48`** (byte) | sit at their resting baseline during **both idle and walking**, and only deviate while being knocked back |

The recoil-specific fields are the useful ones: because they read baseline (`0x0000`, bit clear, `0x00`)
during **all** normal play — confirmed against three resting agents *and* the walking shot-agent — the
trainer can force them back to baseline every poll as a **no-op except during a hit**, so it never
disturbs ordinary movement. Observed transition on the shot agent: `+3/+4` `0x0000 → 0x033E`, `+0xA`
`0x04 → 0x0C` (bit `0x08` sets), `+0x48` `0x00 → 0x90`.

The trainer's **No Recoil** toggle (`SuppressRecoilAliveAgents`) resets these three fields on every
alive, sane agent ~4×/sec, self-validating each ped exactly like the health writes. `+3/+4` is the
strongest single candidate (a knock-back impulse/timer); `+0x48` is the least certain but harmless to
clear since it is baseline-zero whenever the agent is not being hit. Reproduce with
`re_work\recoil_diff.py`. *Confidence: fields are directly differential-derived; which one the engine
actually integrates into the backward displacement is pending live-write confirmation.*

### 10.7.1 Static analysis of the hit-reaction flag (Ghidra)

Searching the code object for the flag write (`OR byte ptr [reg+0x0A], 0x08` = `80 /1 0A 08`) yields
exactly **two** sites, in `FUN_00030600` (`0x3061F`) and `FUN_00033fb0` (`0x3401C`). Decompiled, these
are the **entity animation/state update**:

- `FUN_00030600(ped)` reads a **pending-hit event mask at entity `+0x0C`**; if non-zero it clears it and,
  by bit, returns a **hit-reaction animation-state id** (`+0x0C` bit `0x80→0x15`, `0x08→0x14`,
  `0x40→0x12`, `0x01→0x13`, `0x10→0x11`), setting `+0x0A |= 8` on the way out.
- `FUN_00033fb0(ped)` is the per-ped tick: it **clears `+0x0A &= 0xF7` first**, calls `FUN_00030600`,
  and if the returned state differs from the current `+0x19` it adopts it (enters the recoil animation).

So `+0x0A` bit `0x08` is **not a latch** — it is a *"state changed this tick, restart the animation"*
flag that is **cleared and recomputed every update**. The real trigger is the event mask at `+0x0C`; the
recoil itself is a **hit-reaction animation state at `+0x19` (`0x11–0x15`)** and the backward motion is
whatever that state's handler applies.

Two consequences for the trainer:
1. **The `0x88110`/`+0x0A` entity struct is *not* the `0x6C123` array the trainer writes to.** The code's
   ped array `&DAT_00088110` lives in **object 4**, which is **relocated at run time** — so the trainer's
   `game_base + static_VA` shortcut is valid **only for obj1/obj2**. Runtime relocation map (recovered by
   reading relocated code immediates against the live dump):

   | Object | Static base | Runtime base | Reloc |
   |---|---|---|---|
   | obj1 (code) / obj2 (data) | `0x10000` / `0x50000` | same | `0` |
   | obj3 (bss) | `0x70000` | `0x1FB670` | `+0x18B670` |
   | obj4 (data) | `0x80000` | `0x282F70` | `+0x202F70` |

   So `&DAT_00088110` → runtime `0x28B080` (its end pointer `DAT_00060ae0` = `0x28B588`, 14 slots this
   session) — and that array is **empty** in both paused dumps. The live agent state is instead a
   **malloc'd heap block at ≈`0x6C123`** (in the `0x63EE0…0x1FB670` heap gap; no static symbol, no base
   pointer found in the globals). In that heap array `+0x19` reads `0` even while walking and `+0x0C`
   never clears — it lacks the obj4 entity semantics, so its recoil-correlated `+0x0A`/`+3`/`+0x48` are a
   *separate* representation, not the fields `FUN_00030600` drives. **Open question:** whether
   `FUN_00030600`/`FUN_00033fb0` are also invoked (by pointer) on the heap peds, or the heap array is a
   distinct status structure. Resolving it needs a dump captured while the obj4 array is populated, or a
   static trace of the state-`0x11..0x15` movement handler. Decomp saved to
   `re_work\ghidra_out\recoil_decomp.c`.
2. **The obj4 flag caveat does not necessarily apply to the live array.** In the obj4 struct `+0x0A` is
   recomputed each update, so a 4 Hz freeze of *that* flag could not hold — but that struct is dormant
   here (see §10.7.2). On the live heap peds the recoil state (`+3/+4`, `+0x48`) looks like a multi-frame
   decaying value, so a data freeze may in fact work; a code patch (code object is runtime-aligned at
   `game_base + VA`) remains the fallback. Reproduce the disassembly with `re_work\recoil_diff.py` +
   Ghidra `Decompile.java` on `0x3061f,0x3401c`.

### 10.7.2 Third dump — recoil fields confirmed on the live array

A later single dump (`…121105`, same session) caught **agent 0 under fire while agents 1–3 stood calm**,
giving a clean recoil isolation with **no walk-motion confound** (diff the hit agent against three idle
agents in one snapshot). Two things fell out:

- The obj4 entity array (`0x28B080`) was **still empty** even mid-combat — confirming it is not the live
  ped array in this mode; the heap array at `0x6C123` is authoritative and the `FUN_00030600` flag code
  (obj4 struct layout) is a **separate, dormant** path here.
- Cross-referencing the hit agent's non-baseline bytes against the earlier *walking* snapshot, the only
  fields baseline while **both idle and walking** yet active under fire are exactly **`+3/+4`, `+0x0A`
  bit `0x08`, `+0x48`** — the three `SuppressRecoilAliveAgents` already writes. (`+6`, `+0x0B`, `+0x0C`,
  `+0x2F/+0x30`, `+0x41`, `+0x46` are also set while merely walking — general active-state, correctly
  excluded.) Under fire at full health they read `+3/+4 = 0x03F6`, `+0x0A = 0x0C`, `+0x48 = 0xA3`.

**Status:** the No Recoil field set is **confirmed correct** for the live array. Whether zeroing them
*drives* the displacement or merely *reads out* the hit-reaction is the sole remaining unknown — a
**live-test** question, not a static one.

---

*Notes on confidence: binary layout, object map, relocation counts, function inventory, string/
pointer tables and enumerations are **directly reverse-engineered** and reproducible. Numeric balance
values exposed only through the in-game UI (exact weapon Cost/Ammo/Range, tax percentages, XP curves)
live in the compressed `.dat`/stats tables; their **fields and indexing** are established here, and
their behaviour is cross-checked against `.\Game\manual.pdf`, but individual scalar values would
require dynamic tracing or `.dat` decompression to pin exactly.*
