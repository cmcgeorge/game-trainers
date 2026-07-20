# The Perfect General II — Reverse-Engineering Notes

Working notes behind the trainer's offsets and data tables. Every claim is tagged:

- **Confirmed** — verified against the game files or a memory dump with a reproducible byte match.
- **Inferred** — strongly implied by a confirmed fact plus the game's own documentation, but not byte-verified end to end.
- **Candidate** — a lead worth chasing; treat as a hypothesis, not gospel.

Sources used: the shipped game tree in `.game/` (`TPG2.EXE`, `UNITINFO.DOC`, `UPDATE1.DOC`, `PG2HELP.DAT`,
the `SCEN/*.SCN` scenarios, the reference JPEGs), two full DOSBox-X process dumps in `.data/`
(described by `.data/memdump.md`), and the QQP manual / online references.

---

## 1. Platform & toolchain

| Fact | Confidence | Evidence |
|------|-----------|----------|
| Game is a 16-bit **DOS protected-mode** program using a **Borland/Rational DPMI extender** | Confirmed | `.game/` ships `RTM.EXE` (Rational DOS/16M run-time), `DPMI16BI.OVL` (Borland 16-bit DPMI server), and `DPMILOAD`-style startup; `UPDATE1.DOC` states *"TPG2 is a protected mode program and requires a DPMI server."* |
| Main executable is `TPG2.EXE` (~965 KB); `PG2UP1.EXE` is the v1.01 update binary | Confirmed | File tree + `UPDATE1.DOC` version notes |
| Runs under **DOSBox / DOSBox-X**; `.game/dosbox.bat` mounts `TPG2.iso` as CD drive `Y:` then `call tpg2.exe` | Confirmed | `.game/dosbox.bat` |
| Sound stack is the **Miles Sound System / AIL** (`MIDPAK`, `.ADV`/`.XMI`, `PAS16`, `SBAWE32` drivers) | Confirmed | `.game/SOUND/*` driver set |

Because the game is DPMI, its data lives in a **flat 32-bit data segment allocated at run time**, not at a
fixed DOS paragraph. Addresses are therefore **not stable across runs** — the trainer must locate state by
scanning, never by hard-coded address (the repo-wide rule).

---

## 2. DOSBox-X guest-memory model (how the dumps map)

The `.data/*.bin` files are full **DOSBox-X host-process** dumps; the paired `.csv` lists every committed
region as `FileOffset, ProcessAddress, Size, Protection, Type`.

| Fact | Confidence | Evidence |
|------|-----------|----------|
| The emulated guest RAM is a single large **~16 MB `rw-` private region** in the DOSBox-X process | Confirmed | Purchase-screen dump: file offset `0x10E2000` → process address `0x1_5283011000`, size `0x1001000` (16 MB + 4 KB) |
| Guest-linear address = (host address) − (that region's base) | Confirmed | Region arithmetic below |
| The game's DPMI heap sits a couple of MB into guest RAM | Confirmed | The confirmed purchase array lands at guest offset `0x252F4C` (~2.4 MB in) |

**Worked mapping for the confirmed purchase array** (purchase-screen dump):

```
count array at file offset      0x1334F4C
containing region: FileOffset   0x10E2000  ProcessAddr 0x15283011000  Size 0x1001000 (rw- private)
host address  = 0x15283011000 + (0x1334F4C - 0x10E2000) = 0x15283263F4C
guest offset  = 0x1334F4C - 0x10E2000                    = 0x252F4C
```

For a **live** trainer none of these absolute numbers survive a restart; they only prove the structure exists
and let us build a signature/heuristic. The trainer walks committed `rw-` regions of the live DOSBox process
(`ProcessMemory.EnumerateRegions`) and scans them.

---

## 3. Confirmed dynamic structure — the purchased-unit count array

This is the load-bearing memory finding.

`.data/memdump.md` documents the purchase-screen dump `dosbox-x-64984-20260712-114628-106.bin` with a
**known** basket: *Buy Points Remaining 39*, *Units Purchased 36*, and a per-type breakdown.

Searching that dump for the breakdown as a contiguous byte run hit exactly once, at **file offset
`0x1334F4C`**:

```
01334F40  77 06 54 DF 77 07 00 00 00 00 00 00 03 02 01 04
01334F50  03 02 01 04 03 02 01 04 03 00 02 01 00 01 00 00
```

The 16 bytes starting at `0x1334F4C` are `03 02 01 04 03 02 01 04 03 02 01 04 03 00 02 01`.
They map **exactly** onto the purchase-screen list in `memdump.md`, in screen order, with the (un-purchased)
Plane slot reading 0:

| Idx | Byte | Unit type            | memdump count |
|----:|-----:|----------------------|--------------:|
| 0   | 0x03 | Mine                 | 3 |
| 1   | 0x02 | Infantry             | 2 |
| 2   | 0x01 | Machine Gun          | 1 |
| 3   | 0x04 | Engineer             | 4 |
| 4   | 0x03 | Bazooka              | 3 |
| 5   | 0x02 | Armored Car w/MG     | 2 |
| 6   | 0x01 | Armored Car          | 1 |
| 7   | 0x04 | Light Tank           | 4 |
| 8   | 0x03 | Medium Tank          | 3 |
| 9   | 0x02 | Heavy Tank           | 2 |
| 10  | 0x01 | Mobile Artillery     | 1 |
| 11  | 0x04 | Light Artillery      | 4 |
| 12  | 0x03 | Heavy Artillery      | 3 |
| 13  | 0x00 | *(Plane)*            | 0 |
| 14  | 0x02 | Fortification        | 2 |
| 15  | 0x01 | Elephant Tank        | 1 |

**Sum = 36**, matching `Units Purchased: 36` exactly. This is not a coincidence — a 16-value exact match with
the correct total. **Confidence: Confirmed** that this is the running per-type "units bought" tally for the
player during the purchase phase.

Notes / caveats:

- The array is **byte-per-type** (each count 0–255), 16 entries, in the purchase-screen order above.
- It sits inside a zero-padded DPMI heap block; the immediately surrounding bytes are far-pointer soup
  (`77 07`, `DF`, `DE` selectors — classic 16:16 protected-mode pointers). There is **no strong constant
  byte signature** immediately adjacent, so a robust value-independent auto-locator could **not** be derived
  from two static dumps alone (Candidate for future work — see §6).
- Editing this array changes the counts the purchase screen shows; it does **not** by itself refund
  **Buy Points** (see §4), which the engine tracks separately.

### Unit-type ordering (two different orders exist)

The engine uses **two** distinct orderings; keep them straight:

- **Purchase / count-array order** (Confirmed, §3): Mine, Inf, MGun, Eng, Baz, ACw/MG, AC, LTank, MTank,
  HTank, MobArt, LArt, HArt, Plane, Fort, ETank.
- **`UNITINFO.DOC` stat-table order** (Confirmed from the doc): Inf, MGun, Eng, Baz, ACw/MG, AC, LTank,
  MTank, HTank, MobArt, LArt, HArt, Plane, Mine, Fort, ETank.

They differ only in where **Mine** sits (first vs. 14th). The trainer's `PurchaseFormat` uses the count-array
order; `UnitReference` uses the stat-table order.

---

## 4. Buy Points, and structures not yet pinned

| Item | Confidence | Notes |
|------|-----------|-------|
| **Buy Points Remaining** scalar (39 in the sample) | Candidate | Not adjacent to the count array; not uniquely findable in a single static dump (0x27 is far too common). It is a per-player scalar in the scenario/player state block. The trainer finds it live via a **guided differential scan** (enter the number, buy/sell a unit, re-scan) — the reliable way to pin a scalar the layout doesn't otherwise expose. |
| **Placed-unit records** (type, X, Y, HP, facing, side) | Candidate | The round-start dump `…115500-276.bin` contains the placed army, but per-unit record stride/fields were not confirmed from static dumps. Lead: search near the count array's heap block and correlate with unit counts. |
| **Per-type unit-definition (rules) table** in `TPG2.EXE` | Candidate | The `UNITINFO.DOC` stats (cost/move/HP/damage/ranges) are **not** stored as contiguous byte arrays in the dump (searched cost `01 03 05 03 06 05 06 08 0C 0E 09 14 0F 03 02 0F`, HP, move, damage — 0 hits in both dumps). This means the rules live as a **struct-of-records** (fields strided by a record size) or as 16-bit words, so a contiguous-array signature fails. Finding the stride is the next Ghidra task. |

---

## 5. Confirmed game-rules data (from `UNITINFO.DOC`)

`UNITINFO.DOC` is a shipped plain-text reference (WordPerfect-wrapped; readable via a strings pass). The
tables below are transcribed verbatim and are **Confirmed** game rules (they drive the trainer's read-only
Unit Reference tab). Order is the `UNITINFO.DOC` stat-table order.

### 5.1 Unit information

| Unit | Cost | Move | Bombard range | Hit points | Damage | Repairable | AA-fire | Attack style | Defense style |
|------|-----:|-----:|:-------------:|-----------:|:------:|:----------:|:-------:|:------------:|:-------------:|
| INF (Infantry)          |  1 | 1 | — | 3 | 2 | | | Inf | Inf |
| MGUN (Machine Gun)      |  3 | 1 | — | 3 | 4 | | Yes | MG | Inf |
| ENG (Engineer)          |  5 | 2 | — | 4 | 6 | | | Eng | Inf |
| BAZ (Bazooka)           |  3 | 1 | — | 3 | 4 | | | Armor | Inf |
| AC w/MG (Armd Car + MG) |  6 | 11 | — | 3 | 4 | Yes | Yes | MG | AC |
| AC (Armored Car)        |  5 | 11 | — | 3 | 2 | Yes | | Armor | AC |
| LTANK (Light Tank)      |  6 | 7 | — | 6 | 3 | Yes | | Armor | Armor |
| MTANK (Medium Tank)     |  8 | 6 | — | 8 | 4 | Yes | | Armor | Armor |
| HTANK (Heavy Tank)      | 12 | 5 | — | 15 | 6 | Yes | Yes | Armor | Armor |
| MobART (Mobile Arty)    | 14 | 5 | 11 | 6 | 6 | Yes | | Armor | Armor |
| LART (Light Artillery)  |  9 | 0 | 13 | 1 | 6 | | | Armor | Inf |
| HART (Heavy Artillery)  | 20 | 0 | 26 | 1 | 6 | | | Armor | Inf |
| PLANE                   | 15 | 40–60 (round trip) | 20–30 | 1 | 66% kill (ET 50%) | | | Plane | Plane |
| MINE                    |  3 | — | — | — | — | | | — | — |
| FORTIFICATION           |  2 | — | — | — | — | | | — | — |
| ETANK (Elephant Tank)   | 15 | 3 | — | 21 | 9 | Yes | Yes | Armor | Armor |

*(Costs are Confirmed from `UNITINFO.DOC`. The purchase-screen order in §3 differs, as noted.)*

### 5.2 Maximum firing range vs. each defender (`UNITINFO.DOC` "Range to other units")

Defender columns: IN MG EN BZ AM AC LT MT HT MA LA HA PL ET

```
INF     5  5  5  5  1  1  1  1  1  1  5  5  0  1
MGUN    5  5  5  5  2  2  2  0  0  2  5  5  0  0
ENG     5  5  5  5  1  1  1  1  1  1  5  5  0  1
BAZ     8  8  8  8  8  8  6  4  2  6  8  8  0  1
ACw/MG  5  5  5  5  2  2  2  0  0  2  5  5  0  0
AC      6  6  6  6  6  6  3  1  0  3  6  6  0  0
LTANK   8  8  8  8  8  8  6  4  2  6  8  8  0  1
MTANK  10 10 10 10 10 10  8  6  5  8 10 10  0  3
HTANK  13 13 13 13 13 13 11  8  6 11 13 13  0  4
MobART 13 13 13 13 13 13 11  8  6 11 13 13  0  4
LART   13 13 13 13 13 13 11  8  6 11 13 13  0  4
HART   13 13 13 13 13 13 11  8  6 11 13 13  0  4
PLANE   0  0  0  0  0  0  0  0  0  0  0  0  0  0
ETANK  16 16 16 16 16 16 13 10  8 13 16 16  0  5
```

### 5.3 Probability of hitting target (%) by range and attack/defense style

Attack styles collapse to four gun classes: **Armor/Artillery**, **Infantry**, **Engineer**, **Machine Gun**;
each fires differently at an **Armor**, **Armored Car**, or **Infantry** defense style. `*` marks the maximum
normal firing range (rows beyond are altitude/passing-fire bonuses).

```
Range | Armor gun ->        | Infantry gun ->     | Engineer gun ->     | MG gun ->
      | Armor Armd-Car Inf  | Armor Armd-Car Inf  | Armor Armd-Car Inf  | Armor Armd-Car Inf
  1   |  90   90   90       | *20  *20   75       | *50  *50   65       |  45   55   75
  2   |  80   80   80       |  20   20   65       |  25   25   50       | *20  *25   75
  3   |  71   71   71       |  20   20   50       |  12   12   35       |  15   15   75
  4   |  63   63   63       |            35       |            20       |  15   15   75
  5   |  56   56   56       |           *20       |           *10       |          *75
  6   |  50   50   50       |            10       |             5       |           75
  7   |  45   45   45       |             5       |             2       |           75
  8   |  40   40   40       |
  9   |  35   35   35       |
 10   |  32   32   32       |
 11   |  28   28   28       |
 12   |  25   25   25       |
 13   | *22   22   22       |
 14   |  19   19   19       |
 15   |  16   17   17       |
 16   |      *15  *15       |
 17   |       13   13       |
 18   |       11   11       |
```

### 5.4 Assault probabilities (%) — attacker (rows) vs defender (cols)

Only the mobile "overrun"-capable units have non-zero assault odds:

```
        IN MG EN BZ AM AC LT MT HT MA LA HA PL ET
AC      80 80 80 60 45 40 30 20 10 30 80 80  0  5
LTANK   85 85 85 70 55 50 40 30 20 40 85 85  0 10
MTANK   90 90 90 80 65 60 50 40 30 40 90 90  0 20
HTANK   95 95 95 90 75 70 60 50 40 50 95 95  0 30
ETANK   97 97 97 95 85 80 70 60 50 60 97 97  0 40
```
(All other attacker rows are 0.)

---

## 6. Trainer strategy that follows from the RE

1. **Attach** to the DOSBox / DOSBox-X process and enumerate its committed `rw-` regions (the guest RAM sits
   in one ~16 MB block; the scanner simply walks all committed regions, so it is emulator-build agnostic).
2. **Guided value scan** (Cheat-Engine style, via `GameTrainers.Common.Memory.MemorySearcher`) is the reliable
   primitive for scalars the static layout doesn't expose — **Buy Points** during purchase, a unit's **hit
   points** during battle, turn counters, etc. Enter the on-screen number → first scan; change it in-game →
   narrow; freeze or overwrite the survivor.
3. **Freeze table** re-writes pinned addresses every ~150 ms so combat/spending can't move them.
4. **Unit Reference** surfaces the Confirmed §5 tables in-app so the player can pick unit match-ups without
   alt-tabbing to the manual.

### Open leads (Candidate)

- Pin the **Buy Points** scalar to a fixed offset from a DGROUP string anchor (MM1-style) once a unique
  static string near it is identified in `TPG2.EXE` via Ghidra.
- Recover the **unit-definition record stride** so the cost/HP/damage rules table can be located by signature
  (its values are constant across all games — an ideal "game is loaded" gate).
- Decode the **placed-unit record** (round-start dump) to enable per-unit HP/max-out editing during battle.
- Parse the `SCEN/*.SCN` scenario format (each ~82 KB) for buy budgets and VP-region layouts.
