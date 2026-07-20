# Quest for Glory I: So You Want to Be a Hero — Reverse-Engineering Notes

> **Status**: Active research. Addresses are session-dependent (SCI0 heap relocates every DOSBox
> run); use the trainer's value scanner to locate them live. Global variable **indices** are
> engine-fixed, but the mapped DOSBox memory **address** for index *n* must be scanned each session.

---

## 1. Target

| Item | Detail |
|---|---|
| Game | Quest for Glory I: So You Want to Be a Hero (Hero's Quest) |
| Publisher | Sierra On-Line, 1989 (Hero's Quest); re-released as Quest for Glory I, 1992 |
| Engine | Sierra SCI0 (interpreter `SCIV.EXE`, ~90 KB) |
| Resources | `RESOURCE.000`–`004`, `RESOURCE.MAP`, `RESOURCE.CFG` |
| Emulator | DOSBox / DOSBox-X (process name: `dosbox`, `dosbox-x`) |

---

## 2. SCI0 Engine Architecture

Sierra's SCI0 interpreter runs on a stack-based virtual machine. It manages:

- **Scripts**: compiled bytecode for every room, object, and class.
- **Heap segment**: contains all live SCI objects and their property arrays.
- **Global variable array**: a flat array of 16-bit signed integers at a fixed offset within
  the heap segment. Index 0 through ~750 are game-defined; the engine writes some reserved
  entries at the low end.
- **Hunk segment**: large blocks (e.g., picture bitmaps) not relevant to trainer targets.

Because the SCI heap is allocated dynamically each session by the DOS extender/real-mode
allocator inside DOSBox's guest RAM, the absolute address of `global[0]` changes every run.
There is no useful static byte-string adjacent to the mutable stat data to anchor a
`BytePatternScanner` scan; value scanning is the correct approach.

### 2.1 Global Variable Layout (16-bit words, little-endian)

```
Offset in global array  Size    Meaning
──────────────────────  ──────  ──────────────────────────────────────────
g[0]   (word)           2 B     Ego object selector (session-variable — NOT a scan anchor)
g[1]   (word)           2 B     Current room number (see §4)
g[2]   (word)           2 B     Previous room number
g[3]   (word)           2 B     Game-day (1-based, starting at 1)
g[4]   (word)           2 B     Game-clock ticks (0–3599 per day)
```

> **Note**: Global indices 0–4 above are confirmed by cross-referencing the SCI0 interpreter
> source and matching the memory-dump values. Skill/stat globals are stored as properties on
> the Ego object and therefore live at a heap address separate from the global array; they must
> be found by value-scanning for their known numeric values.
>
> **g[0] is session-variable**: dump analysis across three captures (PIDs all 43664,
> spanning ~5 hours of play) showed g[0] = 208 in one session and g[0] = 88 in another.
> An earlier (incorrect) note claimed g[0] = 76 was a compile-time constant; that value
> is 0x4C = ASCII 'L' and matches only text-section false positives.  The locator does
> not filter on g[0].

### 2.2 Time System

```
TICKS_PER_DAY  = 3600   (one full in-game day)
TICKS_PER_HOUR = 150    (24 × 150 = 3600)
```

Time-of-day zones, computed from `g[4]` (ticks within the current day):

| Zone | ID | Tick range | Approx. real-time |
|---|---|---|---|
| Dawn | 0 | 0 – 449 | midnight – ~3 AM |
| Mid-morning | 1 | 450 – 1049 | 3 AM – 7 AM |
| Midday | 2 | 1050 – 1649 | 7 AM – 11 AM |
| Mid-afternoon | 3 | 1650 – 2249 | 11 AM – 3 PM |
| Sunset | 4 | 2250 – 2849 | 3 PM – 7 PM |
| Night | 5 | 2850 – 3299 | 7 PM – 10 PM |
| Midnight | 6 | 3300 – 3599 | 10 PM – midnight |

To write a specific hour *h* (0–23): `ticks = h * 150`.
To write a specific day *d*: write *d* to `g[3]` and an appropriate tick count to `g[4]`.

---

## 3. Character Statistics

Stats are stored as properties of the `Ego` SCI object (not raw global slots). Each property
is a 16-bit word. Because the object layout also moves with the heap, stats must be located
via value scanning.

### 3.1 Stat IDs (SCI0 property indices, used in game scripts)

| ID | Name | Range |
|---|---|---|
| 0 | Strength | 1–200 |
| 1 | Intelligence | 1–200 |
| 2 | Agility | 1–200 |
| 3 | Vitality | 1–200 |
| 4 | Luck | 1–200 |
| 5 | Weapon Use | 0–200 |
| 6 | Parry | 0–200 |
| 7 | Dodge | 0–200 |
| 8 | Stealth | 0–200 |
| 9 | Pick Locks | 0–200 (Thief only) |
| 10 | Throwing | 0–200 |
| 11 | Climbing | 0–200 |
| 12 | Magic | 0–200 (Magic-user class only) |
| 13 | Experience | 0–∞ |
| 14 | Health Points (current) | 0–max |
| 15 | Stamina (current) | 0–max |
| 16 | Mana (current) | 0–max (0 if non-magic user) |

### 3.2 Dump Cross-Reference — Day 1 Midday, Thief

From dump `dosbox-x-43664-20260714-082830-726.bin`:

| Stat | In-game value | Expected scan target |
|---|---|---|
| Strength | 10 | 10 |
| Intelligence | 20 | 20 |
| Agility | 30 | 30 |
| Vitality | 15 | 15 |
| Luck | 15 | 15 |
| Weapon Use | 10 | 10 |
| Parry | 5 | 5 |
| Dodge | 5 | 5 |
| Stealth | 10 | 10 |
| Pick Locks | 10 | 10 |
| Throwing | 5 | 5 |
| Climbing | 5 | 5 |
| Magic | 5 | 5 |
| HP (current) | 13 | 13 |
| Stamina (current) | 23 | 23 |
| Mana (current) | 10 | 10 |

Gold coins: 4 · Silver coins: 10
Weight carried: 24 · Max weight: 45

Stats are stored adjacent in the Ego property block (16-bit words, little-endian).
A scan for `13` (HP current = Int16) at midday day 1 will return many candidates; narrow by
allowing the character to take a hit and scanning "Decreased", then healing back and scanning
"Increased".

---

## 4. Room Numbers (Confirmed / Estimated)

Room scripts in SCI0 are identified by integer IDs. The table below combines confirmed entries
(marked **C**) with estimates from resource-map inspection and game-play observation (marked **E**).
Teleporting to an estimated room that is incorrect may crash the interpreter; always save first.

| Room # | Status | Name / Description |
|---|---|---|
| 1 | E | Spielburg Valley — south road |
| 2 | E | Spielburg Valley — south-west |
| 3 | E | Spielburg Valley — west |
| 4 | E | Spielburg Valley — north-west |
| 5 | E | Forest — far west |
| 10 | C | Spielburg — town gate (entrance from valley) |
| 11 | C | Spielburg — town square |
| 12 | E | Spielburg — north street |
| 13 | E | Spielburg — alley |
| 14 | E | Adventurers' Guild — exterior |
| 15 | C | Adventurers' Guild — interior |
| 20 | C | Dry Grape Inn — exterior |
| 21 | C | Dry Grape Inn — common room |
| 22 | E | Dry Grape Inn — hero's room |
| 30 | E | Meeps' Curiosity Shoppe — exterior |
| 31 | C | Meeps' Curiosity Shoppe — interior |
| 40 | E | Weapon shop — exterior |
| 41 | C | Weapon shop — interior |
| 50 | C | Healer's hut — exterior |
| 51 | C | Healer's hut — interior |
| 60 | E | Magic shop (Zara's) — exterior |
| 61 | C | Magic shop (Zara's) — interior |
| 70 | E | Sheriff's office — exterior |
| 71 | C | Sheriff's office — interior |
| 80 | E | Erasmus's house — forest path |
| 81 | E | Erasmus's house — exterior |
| 82 | E | Erasmus's house — interior |
| 90 | E | Castle Spielburg — drawbridge |
| 91 | E | Castle Spielburg — courtyard |
| 92 | E | Castle Spielburg — great hall |
| 100 | C | Erana's Peace |
| 110 | E | Baba Yaga's hut — exterior |
| 111 | E | Baba Yaga's hut — interior |
| 120 | E | Brigand fortress — approach |
| 121 | E | Brigand fortress — exterior |
| 122 | E | Brigand fortress — barracks |
| 130 | E | Ogre's territory |
| 140 | E | Troll's bridge — approach |
| 141 | E | Troll's bridge — at bridge |
| 150 | E | Antwerp meadow |
| 160 | E | Flying falls |
| 170 | E | Kobold cave entrance |
| 171 | E | Kobold cave — interior |
| 180 | E | Bear cave — exterior |
| 181 | E | Bear cave — interior |
| 190 | E | Cheetaur territory |

---

## 5. Inventory / Currency

Gold and silver coins are stored as word-sized properties within the Ego object or a related
inventory-manager object. They are adjacent in memory and can be found by scanning for the
known coin count.

| Item | Storage | Scan type |
|---|---|---|
| Gold coins | 16-bit word | Int16 |
| Silver coins | 16-bit word | Int16 |
| Weight carried | 16-bit word | Int16 |
| Max weight | 16-bit word (read-only in practice) | Int16 |

---

## 6. SCI0 Debug Mode

The game ships a debug mode accessible at runtime:

1. Type the phrase **`razzle dazzle root beer`** anywhere (the letters are invisible).
2. Then use hotkeys:
   - **ALT-T**: teleport to a room by number
   - **ALT-K**: set stats
   - **ALT-B**: give money

This in-game debug mode is the fastest way to confirm suspected room numbers and validate
that stat writes landed at the right address — set a value in the trainer, then read it back
via ALT-K.

---

## 7. Memory Dump Reference

Dump file: `dosbox-x-43664-20260714-082830-726.bin`
Captured: Day 1, Midday, Spielburg entrance, Thief character

- **CSV companion**: `dosbox-x-43664-20260714-082830-726.csv`
- Full 128 MB DOSBox-X guest RAM snapshot.
- Use `DumpComparer` from `GameTrainers.Common.Memory` to diff snapshots taken before and after
  changing a stat; surviving single-byte differences narrow candidate addresses dramatically.

---

## 8. Ghidra Analysis Notes

Ghidra 12.1.2 was used to inspect `SCIV.EXE` (SCI0 interpreter). Key findings:

- The interpreter allocates a heap block of ~48 KB at startup; the base address in DOSBox
  guest RAM varies each session.
- The global variable array starts at a fixed offset from the heap base (confirmed ~0x14C
  bytes in, but this varies by interpreter version).
- The room-change procedure writes the new room number to global[1] and then calls the room
  init handler; writing global[1] alone between room-change events is sufficient to redirect
  the player on the next room transition, but does not immediately teleport.
- The game clock (global[4]) increments once per interpreter cycle (~60 Hz). Writing a new
  tick value takes effect immediately.
- No static byte-string close to the mutable stat block was found that is session-stable
  and could serve as a `BytePatternScanner` anchor; value scanning remains the correct method.

---

## 9. Scan Workflow Summary

1. Launch DOSBox, load the game, reach a known game state (e.g., open the stats screen).
2. Attach the trainer to the DOSBox process.
3. Use a **Guided Scan** button (HP, Stamina, Magic, Gold, Time, Room) for the value you want.
4. Follow the on-screen instructions to narrow to one candidate.
5. Pin the candidate → it appears in the **Freezes** tab.
6. Use the **Day/Time** tab (requires a pinned game-clock address) or the **Teleport** tab
   (requires a pinned room-number address) for the higher-level editors.

---

*Last updated: July 2026*
