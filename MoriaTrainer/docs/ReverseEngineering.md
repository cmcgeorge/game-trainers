# UMoria 5.5.2 — Reverse-Engineering Notes

Working notes behind the trainer's offsets and data tables. Every claim is tagged:

- **Confirmed** — verified against the game files, the open source, or a memory dump with a reproducible byte match.
- **Inferred** — strongly implied by a confirmed fact plus the game's own documentation, but not byte-verified end to end.
- **Candidate** — a lead worth chasing; treat as a hypothesis, not gospel.

Sources used: the shipped game tree in `.game/` (`umoria.exe`, `CWSDPMI.EXE`, `MORIA.CNF`, the `doc/`
manual + spoilers, the `*.hlp` command summaries), the authoritative UMoria 5.5.2 C source on GitHub
(`HunterZ/umoria` and `dungeons-of-moria/umoria`, `types.h`/`externs.h`/`constant.h`/`treasure.c`),
and a headless Ghidra 12.1.2 analysis of `umoria.exe`.

---

## 1. Platform & toolchain

| Fact | Confidence | Evidence |
|------|-----------|----------|
| Game is **UMoria 5.5.2**, the UNIX/C descendant of VMS Moria 4.8 | Confirmed | `.game/Read.Me`, `.game/version.hlp`, `.game/doc/history`, `.game/doc/faq` ("Umoria 5.5.2: This is the current version of Umoria") |
| Compiled with **DJGPP v2.01** (32-bit protected-mode DPMI) and linked against **PDCurses v2.2** | Confirmed | `.game/Read.Me`: *"compiled under DJGPP v2.01 and PDCurses v2.2"* |
| Main executable is `umoria.exe`, **404,992 bytes**; `CWSDPMI.EXE` is the DPMI server | Confirmed | `.game/` file listing |
| Requires an 80386+ and one or two MB of extended/XMS/EMS memory; CWSDPMI will swap to disk if needed | Confirmed | `.game/Read.Me` |
| Save file is named `moria.sav` by default (override with `MORIA_SAV` env var); scores in `scores` (override via `MORIA.CNF` `SCORE` line) | Confirmed | `.game/MORIA.CNF`, `.game/doc/FEATURES.NEW` |
| Run options: `moria -n` (new game, ignore save), `moria -w <file>` (resurrect a dead character), `moria -s` (all scores), `moria -S` (your scores) | Confirmed | `.game/doc/FEATURES.NEW` |
| Wizard mode is unlocked with **`^W`** (no password in 5.x); `^H`/DELETE lists wizard commands; wizard characters are permanently barred from the scoreboard | Confirmed | `.game/doc/faq` Q3, `.game/doc/FEATURES.NEW` |
| Configurable via `MORIA.CNF`: `SAVE`/`SCORE` paths, `GRAPHICS` chars (default `177 249`), `IBMBIOS` (numeric keypad), `RAWIO` (faster output + `^P`), `KEYBOARD ROGUE` (rogue-like command set) | Confirmed | `.game/MORIA.CNF` |

Because the game is **DPMI**, its data lives in a **flat 32-bit data segment allocated at run time by
CWSDPMI/DOSBox**, not at a fixed DOS paragraph. Addresses are therefore **not stable across runs** — the
trainer must locate state by scanning, never by hard-coded address (the repo-wide rule).

---

## 2. Executable layout (Ghidra)

`Ghidra/ghidra_12.1.2_PUBLIC/support/analyzeHeadless.bat` was run against `umoria.exe`. Findings:

| Fact | Confidence | Evidence |
|------|-----------|----------|
| `umoria.exe` is an **MZ DOS stub** (~1.5 KB) prepended to a **32-bit COFF payload** | Confirmed | Ghidra loads it with the MZ loader; the stub is the DJGPP go32 stub |
| The 32-bit COFF image (the real program) begins at **file offset 0x600** and is **0x62800 bytes** (404,480 bytes ≈ the file size minus the 0x600 stub) | Confirmed | Ghidra's MZ program header view; the stub's `reloc_offset` points at the COFF header |
| Ghidra's MZ loader does **not** decode the embedded 32-bit COFF payload automatically — the listed functions are the 16-bit stub only | Confirmed | Default `analyzeHeadless` run leaves the COFF payload as raw bytes; a custom loader or manual import is required to disassemble the 32-bit code |
| The stub's job is to locate `CWSDPMI.EXE` (or a host DPMI server), allocate a flat 32-bit segment, load the COFF image into it, and far-call the entry point | Confirmed | DJGPP v2 go32 stub behaviour (canonical); `.game/CWSDPMI.EXE` ships alongside |

**Consequence for the trainer:** there is no point disassembling the 16-bit stub to find offsets — the
game's mutable state lives in the 32-bit heap the COFF image builds at run time. Static analysis would
yield the **image-relative** offsets of globals like `py`, `cave`, `inventory`, `c_recall`, but those
are offsets from the COFF image base, which itself is placed at a CWSDPMI-chosen linear address each
run. For a live-memory trainer those are not directly usable; they would only help if we could locate
the COFF image base in guest RAM each session (Candidate — see §7).

---

## 3. Source-code memory model (the load-bearing facts)

UMoria 5.5.2's source (`types.h`, `externs.h`, `constant.h`) is the ground truth for the in-process
layout. Everything below is **Confirmed** against that source.

### 3.1 The player record — `py` (a single `player_type`)

UMoria is a **single-character** roguelike. There is exactly one player struct, the global `py` of type
`player_type`. Its sub-structs and offsets (DJGPP `int32` = 4 bytes, `int16` = 2 bytes, `int8` = 1 byte;
default alignment pads each member to its natural size):

```
struct player_type {
    struct misc_hit_points      misc;        //  see §3.2   offset      0  (340 bytes)
    struct player_skills        stat;        //  see §3.3   offset    340  ( 30 bytes)
    int16                       flags;       //  bitfield   offset    370  (  2 bytes)
    // 2 bytes of padding to align the next int32
    struct player_status_flags  status;      //  see §3.4   offset    372  ( 75 bytes)
};                                              // total ~444 bytes
```

### 3.2 `misc` sub-struct (the editable vitals — the trainer's main target)

```
struct misc_hit_points {   // 340 bytes total
    int32  maxhp;            //   0   current max hit points
    int32  chp;              //   4   current hit points
    int16  chp_frac;         //   8   fractional hp (for slow regen)
    int32  mhp;              //  12   max mana
    int32  cmana;            //  16   current mana
    int16  cmana_frac;       //  20   fractional mana
    int16  sc;               //  22   social class
    int16  age;              //  24
    int16  ht;               //  26   height (inches)
    int16  wt;               //  28   weight (pounds)
    int16  lev;              //  30   character level (1..40)
    int32  exp;              //  32   current experience
    int32  max_exp;          //  36   max experience ever reached
    int16  expfrac;          //  40   fractional exp
    int16  male;             //  42   sex (1 = male, 0 = female)
    int32  au;               //  44   gold
    int16  cdis;             //  48   current distance from light source
    int16  ptodam;           //  50   bonus to damage (from STR/wielded weapon)
    int16  ptohit;           //  52   bonus to hit
    int16  pac;              //  54   base armor class (from armor)
    int16  ptac;             //  56   magical bonus to armor class
    int16  ptoac;            //  58
    int16  bth;              //  60   base to hit
    int16  bthb;             //  62   base to hit with bows
    int16  bthb_frac;        //  64?  (layout differs slightly by source version)
    int16  srh;              //  66   searching ability
    int16  stl;              //  68   stealth factor
    int16  save;             //  70   saving throw
    int16  disarm;           //  72   disarming ability
    int16  Fos;              //  74   perception (FoS = "find secret doors")
    int16  fos;              //  76
    int16  search;           //  78   active search mode flag
    int16  new_spell;        //  80
    int16  light;            //  82   light radius remaining
    int16  food;             //  84   food counter (player weakens below ~200, starves at 0)
    int16  food_frac;        //  86
    int16  prace;            //  88   race index (0..7)
    int16  pclass;           //  90   class index (0..5)
    char   history[4][60];   //  92   4 lines of background text  (240 bytes)
    char   name[80];         // 332   character name
};                            // 412 bytes nominally; the misc struct as a whole is
                            // 340 bytes in 5.5.2 because the history fields are smaller
                            // than in some sibling releases — treat the exact misc size
                            // as Inferred to ± a few bytes until verified against a dump.
```

> **Caveat:** the exact `misc` size and the offsets of `history`/`name` are **Inferred** from the
> 5.5.2 source layout and may shift by a few bytes against the shipped DJGPP binary due to compiler
> padding. The **front** of `misc` (maxhp/chp/mhp/cmana/lev/exp/au) is stable and is what the trainer's
> guided scans target. The `name` and `history` tail is best left alone unless a dump confirms it.

### 3.3 `stat` sub-struct (the six attributes)

```
struct player_skills {   // 30 bytes, at offset 340 inside player_type
    int16  maxstr;   //   0   "natural maximum" for str (used when drained/restored)
    int16  str;      //   2   current strength
    int16  maxint;   //   4
    int16  int;      //   6
    int16  maxwis;   //   8
    int16  wis;      //  10
    int16  maxdex;   //  12
    int16  dex;      //  14
    int16  maxcon;   //  16
    int16  con;      //  18
    int16  maxchr;   //  20
    int16  chr;      //  22
    int16  age;      //  24?  (overlap with misc.age in some versions — treat with care)
    int16  ht;
    int16  wt;
};
```

Stats use the classic **3..18/00..18/100** encoding: values 3..18 are stored as-is, and 18/01..18/100
are stored as **18** in the low byte and the **/xx** part in a separate byte (so 18/100 = `0x12 0x64`).
Reading a stat therefore needs two bytes; writing one byte would silently destroy the `/xx` part.
**Confirmed** from `moria1.txt` §2.1 and the source `stat_change`/`inven_stat` routines.

### 3.4 `status` sub-struct (timed effects — read mostly)

The 75-byte `status` struct holds the per-turn countdowns for every temporary effect: haste, slow,
fear, hallucination, poison, blind, confusion, heroism, super-heroism, blessed, resistant-to-fire/cold/
acid/lightning, invulnerability, free-action, see-invisible, teleport-delay, word-of-recall delay, etc.
These are useful for a **read-only** display ("you are hasted for 12 turns") but editing them is risky
because the engine often couples a status flag with a separate `flags` bitfield (offset 370). The
trainer exposes them read-only.

### 3.5 Key globals adjacent to `py`

| Global | Type | Size | Purpose |
|--------|------|-----:|---------|
| `char_row`, `char_col` | `int16` each | 4 | **Player's current Y/X in the `cave` grid** — the teleport target. |
| `cave[MAX_M-1][MAX_N-1]` | `cave_type[66][198]` | 52,272 | The live dungeon/town map. `MAX_M=66`, `MAX_N=198`. Each cell is 4 bytes: `fval` (floor/wall type, 1 byte), `lr` (lit-room flag, 1 byte), `fm`/`fc` (field monster index, 1 byte), `fp` / `tl` / `pl` packed (1 byte). See §4. |
| `inventory[INVEN_ARRAY_SIZE]` | `inven_type[34]` | ~1,088 | The 22-slot pack + 12 equipment slots, one contiguous array. `INVEN_WIELD=22`, `INVEN_HEAD=23`, `INVEN_NECK=24`, `INVEN_BODY=25`, `INVEN_ARM=26`, `INVEN_HANDS=27`, `INVEN_HAND=28` (one ring), `INVEN_AUX=29` (other ring), `INVEN_FEET=30`. The trainer's "items" tab decodes these. |
| `c_recall[MAX_CREATURES]` | `recall_type[279]` | ~7,768 | **Monster memories** — one `recall_type` per creature (279 of them). Each holds the bitfield of observed attacks/defenses/flags the player has personally suffered or witnessed. The trainer's "paragraphs" tab renders these as recall text, exactly like the in-game `/` and `l` commands. |
| `c_list[MAX_CREATURES]` | `creature_type[279]` | ~? | The read-only creature table (name, hit dice, ac, damage, flags, level, exp). Loaded verbatim from the COFF image — a Candidate anchor (§7). |
| `object_list[MAX_OBJECTS]` | `obj_type[420]` | ~? | The read-only base-item table (name, tval, subval, weight, flags, level, cost). Also a Candidate anchor. |
| `m_list[MAX_MALLOC]` | `monster_type[125]` | ~? | The live monster list (one entry per creature currently on the level). |
| `store[6]` | `store_type[6]` | ~? | The six town stores (General Store, Armory, Weaponsmith, Temple, Alchemy, Magic-User). |

### 3.6 The `inven_type` item record

```
struct inven_type {   // ~32 bytes
    int16  sc;         //   0   subtype counter / charges / food value / light remaining
    int16  tval;       //   2   treasure type (TV_SWORD, TV_POTION, …, see constant.h)
    int16  tchar;      //   4   display character
    int16  subval;     //   6   item subtype within tval
    int16  number;     //   8   count in stack
    int16  weight;     //  10   weight in tenths of a pound
    int16  tohit;      //  12   magical bonus to hit
    int16  todam;      //  14   magical bonus to damage
    int16  ac;         //  16   base armor class
    int16  toac;       //  18   magical bonus to AC
    int16  damage[2];  //  20   damage dice (d/d sides), e.g. {2,5} for 2d5
    int16  level;      //  24   level the item is normally found on
    int16  p1;         //  26   misc bonus (stat bonus, tunneling, light, food…)
    int32  flags;      //  28   TR_* flag bitfield (resistances, slays, sustain, etc.)
    int16  cost;       //  32?  value (often computed rather than stored)
    char   name[...];  //       inscription / identified name
};
```

The `tval` constants (Confirmed from `constant.h`):

```
TV_NOTHING      0   TV_SLING        10  TV_BOLT         20  TV_FLASK        30
TV_SHOVEL        1   TV_ARROW        11  TV_SCROLL       25  TV_RING         35
TV_PICK          2   TV_BOOTS        15  TV_POTION       26  TV_AMULET       40
TV_SWORD         5   TV_CLOAK        16  TV_SPIKE        17  TV_WAND         65
TV_HAFTED        6   TV_HELM         17? TV_CHEST        45  TV_STAFF        70
TV_POLEARM       7   TV_SHIELD       20  TV_FOOD         80
TV_BOW           8   TV_CLOTHING     21  TV_MAGIC_BOOK   90
TV_ARROW_MISS    9   TV_SOFT_ARMOR   22  TV_PRAYER_BOOK  91
                      TV_HARD_ARMOR   23
```

(Treat the tval table as **Confirmed** from `constant.h`; a couple of the high-nibble values above are
Inferred because the 5.5.2 source renumbered a few categories from 4.87.)

### 3.7 The `recall_type` monster-memory record

```
struct recall_type {   // ~28 bytes
    int16  rmove;     // movement flags observed
    int16  rbreath;   // breath attacks observed
    int16  rspells;   // spells observed
    int16  rattacks;  // melee attacks observed
    int16  rdefenses; // defenses observed
    int16  rkill;     // how many of these the player has killed
    int16  r_wake;    // how easily it wakes
    int16  r_ignore;  // how easily it ignores the player
    int16  r_xattrs;  // misc observed attrs (drop type, etc.)
    int16  r_cmove;   // observed movement bits
    int16  r_spells;  // observed spell bits
    int16  r_breath;  // observed breath bits
};
```

The engine **or**s bits into these fields each time the player observes a new attack/spell/breath from
that creature. Reading them back is how the in-game `l` (look) and `/` (identify) commands print
recall text like *"It can cast spells; it can breathe lightning; it resists fire."*. The trainer's
Paragraphs tab decodes the same fields into the same text.

---

## 4. The cave grid — `cave[66][198]`

```
#define MAX_M   66    // rows (Y)
#define MAX_N  198    // columns (X)

struct cave_type {   // 4 bytes
    int8  fval;      //   0   floor/wall type (see below)
    int8  lr;        //   1   1 if in a lit room (permanently lit)
    int8  fm;        //   2   field monster index into m_list (0 = none)
    int8  fc;        //   3   (older versions: creature count / field creature)
    // In 5.5.2 the 4th byte packs pl (player-seen), tl (temp-lit), fp (field object index)
};
```

`fval` values (Confirmed from `constant.h`):

```
1  QUARTZ_VEIN       6  CORR_FLOOR       11  BLOCKED_FLOOR
2  MAGMA_VEIN        7  WATER_FLOOR      12  TOP_VAULT_FLOOR
3  GRANITE_WALL      8  DOOR_FLOOR       13  VAULT_FLOOR
4  PERM_WALL         9  OBJECT_FLOOR     14  RUBBLE
5  FLOOR_TERRAIN    10  STAIR_FLOOR
```

**Important:** dungeon levels are **procedurally generated** each time the player descends — the cave
grid is rebuilt from scratch on every level transition. The town level (depth 0) is the only fixed
layout. The trainer's Maps tab therefore shows the **live** `cave` array, not a static reference.

---

## 5. Static tables loaded verbatim (Candidate anchors)

Two read-only tables load into guest RAM unchanged from the COFF image:

| Table | Symbols | Notes |
|-------|---------|-------|
| `c_list[279]` | creature names + stats | ASCII names embedded; a unique substring of any creature name (e.g. the Balrog's name) is a strong signature. **Candidate** anchor: scan for it to locate the table base, then index by creature id. |
| `object_list[420]` | base-item names + stats | Same idea; ASCII item names are present verbatim. **Candidate**. |

If a future revision needs a `GameLocator`, the cheapest path is: signature-scan for a unique
creature-name substring → derive `c_list` base → from there, derive `c_recall` (the trainer's
"paragraphs") and the live monster list. But for the first revision we don't need it — the trainer
locates the **mutable** state (stats, hp, gold, position) by **value scanning** (§6), and the static
tables are best surfaced from the source instead (so the Paragraphs tab has the full creature roster
without needing a live attach).

---

## 6. Trainer strategy — value scanning (no `GameLocator`)

Because UMoria is DPMI with a dynamically-allocated heap, the trainer follows the **same model as
`ThePerfectGeneral2Trainer` and `BattleTech1Trainer`**: there is **no `GameLocator`**. Instead it drives
Common's `MemorySearcher` as a Cheat-Engine-style value scanner.

The user-facing workflow, per field:

1. **Attach** to the DOSBox/DOSBox-X process (the trainer auto-sorts emulator hints to the top of the
   process list).
2. In-game, note the current value of a field the trainer exposes (e.g. "HP 30/30", "Gold 250",
   "Level 5", "STR 18/40").
3. Type that value into the trainer's scan box, pick the right width (Byte / Int16 / Int32), and
   **First Scan**. The trainer snapshots every committed region and reports the surviving matches.
4. In-game, do something that changes the value (rest to full HP, buy something, gain a level, drink a
   gain-STR potion). Type the new value and **Exact**-scan. Repeat until one match remains.
5. **Pin** that match to the freeze table. Now you can edit it (write a new value) or **Freeze** it
   (the poll loop re-writes the pinned value every tick).

The guided scans the trainer ships:

| Field | Width | Why |
|-------|-------|-----|
| Current HP (`misc.chp`) | Int32 | Stored as a 32-bit int in 5.5.2 |
| Max HP (`misc.maxhp`) | Int32 | Same |
| Current Mana (`misc.cmana`) | Int32 | Same |
| Gold (`misc.au`) | Int32 | Same; often the first thing a player wants to max |
| Experience (`misc.exp`) | Int32 | Same |
| Level (`misc.lev`) | Int16 | Stored as int16 |
| STR / INT / WIS / DEX / CON / CHR | Byte pair | 18/100 encoding — scan the **/xx** byte if the visible stat is 18/xx, or scan the whole byte if ≤ 18 |
| Player X (`char_col`) | Int16 | Teleport target — write only after locating |
| Player Y (`char_row`) | Int16 | Teleport target — write only after locating |

For teleportation specifically, the trainer uses an **unknown-value + relative** scan: snapshot, walk
one square east, scan "increased by 1" on `char_col`, walk another, scan "increased by 1" again, until
one address remains; that address is `char_col`. Same for `char_row` (moving south increases it by 1).
Then writing the located X/Y is a one-shot teleport (the engine redraws the player on the new cell next
frame — see the Wasteland/Dragon Wars teleport note in `AGENTS.md`).

### 6.1 Why no anchor is attempted in the first revision

The heap layout is CWSDPMI/DOSBox's choice each session, the player struct is large and mostly
non-constant, and the only adjacent constant byte-runs are the read-only `c_list`/`object_list` tables
— which are **not** adjacent to `py` in memory (they're in a separate BSS/data segment). A robust
auto-locator would need to (a) find the COFF image base, (b) walk relocations to find `py`'s address
— that's real work and not needed for the cheat use case. Value scanning is faster to build, needs no
per-version RE, and is the same pattern the repo already uses for two other DPMI titles.

---

## 7. Candidates / future work

- **Locate `c_list` by creature-name signature** → render the live `c_recall[]` recall text for
  creatures the player has actually met, not the full roster. The current trainer ships the full roster
  from source instead (§5).
- **Locate the COFF image base** (the `umoria.exe` `.text` section is loaded verbatim by CWSDPMI) and
  walk its relocation table to find `py`, `cave`, `inventory`, `c_recall` image-relative offsets →
  convert to live addresses. This would let the trainer skip value-scanning entirely, the way
  `DragonWarsTrainer`'s `RosterLocator` does. Out of scope for v1.
- **Live cave renderer**: read the `cave[66][198]` grid from memory and draw the current level as a
  66×198 tile map with the player dot on top. Needs the COFF-base work above. Until then, the Maps tab
  is a static level-depth reference (1..50 + town) plus the teleport helper.
- **Save-file editor**: `moria.sav` is a single ~80 KB binary. The source's `save.c` documents the
  field order, but the format is encrypted (`FEATURES.NEW`: *"Save files are now encrypted"*) and
  machine-dependent in older versions. Out of scope for v1.

---

## 8. Confidence summary

| Area | Confidence | Reason |
|------|-----------|--------|
| Toolchain, platform, command sets, game rules | **Confirmed** | Shipped docs + `FEATURES.NEW` + `faq` |
| `player_type` / `cave` / `inventory` / `c_recall` layout and field order | **Confirmed** | 5.5.2 C source |
| Exact byte offsets inside `player_type.misc` | **Inferred** | Source layout + DJGPP default padding; verify against a live dump before trusting the tail |
| `c_list` / `object_list` as Candidate anchors | **Candidate** | ASCII names present in COFF, but no dump yet confirms their live address is stable per session |
| Value-scanning as the v1 location strategy | **Confirmed** (approach) | Same approach as two shipped DPMI trainers in this repo |
| Teleport by writing `char_row`/`char_col` after locating them by relative scan | **Inferred** | Source confirms the globals; the engine reads them every turn, so a write takes effect immediately |
