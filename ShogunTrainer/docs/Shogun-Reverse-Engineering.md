# James Clavell's *Shōgun* (IBM PC / DOS) — Reverse-Engineering Notes

> A Ghidra-based teardown of the DOS game in `Game/`. This document records how the
> game boots, how its data files are structured, how its text is encoded, and how the
> core game mechanics work, as reconstructed from the executables and data files.

---

## 1. Game identity

| | |
|---|---|
| **Title** | *James Clavell's Shōgun* (a.k.a. *Shogun*) |
| **Publisher** | Virgin Games / Mastertronic |
| **IBM PC/DOS conversion** | **Synergistic Software, Inc.** |
| **Original design/code** | "Lee & Mathias" (home-micro versions, 1986) |
| **Year (this build)** | **1987** |
| **Genre** | Real-time arcade-adventure / strategy |
| **Setting** | Feudal Japan, c. 1600 (the world of Clavell's novel) |

These credits are not guesses — they are baked into the game's own text table
(`String0.bin`), which contains the literal strings:

- `" VIRGIN GAMES 1987. BY LEE & MATHIAS."`
- `"ADAPTED BY SYNERGISTIC SOFTWARE, INC."`

and character names drawn straight from the novel: **BLACKTHORNE, MARIKO, TORANAGA,
ISHIDO, OMI, YOSHINAKA**, etc.

**Objective (from the game's own text):** *"TO BECOME SHOGUN YOU MUST … TAKE THE
BUDDHA, SCROLL & MIRROR TO …"* while *"DON'T TAKE TOO LONG!"* — i.e. gather three
sacred relics and deliver them before a time limit, building up a following of
loyal NPCs along the way. Success prints *"WELL DONE! SHOGUN — ALL JAPAN HONORS
YOU."*; failure prints *"YOU HAVE FAILED! YOU MUST DIE! YOUR DEATH IS SLOW AND
PAINFUL."*

---

## 2. Method & tooling

All analysis was done statically (the game was **not executed**).

- **Ghidra 12.1.2** headless analyzer (`analyzeHeadless`) imported the three MZ
  executables with the `x86:LE:16:Real Mode` language and full auto-analysis.
- Custom Ghidra post-scripts (in `C:\ghidra_scripts`) produced:
  - `DecompAll.java` → full decompilation of every function (`SHOGUNT_all.txt`,
    `STARTEXE_all.txt`).
  - `DosSyscalls.java` → a classified map of every `INT 21h` call by AH
    (`*.exe.syscalls.txt`).
  - `StringXrefs.java` / `DumpGlobals.java` → literal/global cross-references.
- Custom **Python decoders** cracked the data-file formats and the game's text
  encoding (see §5–§8).

Raw artifacts live in `C:\ghidra_out\` (e.g. `SHOGUNT_all.txt`,
`SHOGUN_strings_indexed.txt`, `Shogunt.exe.syscalls.txt`).

---

## 3. File inventory

The `Game/` directory is a self-booting DOS game (the `Setup.bat` even `sys`-es a
diskette). Files fall into three groups: **launcher/scripts**, **executables**, and
**data**.

### Scripts / config
| File | Size | Role |
|---|---|---|
| `Autoexec.bat` / `Shogun.bat` | 70 B | Run `START`, then branch on its errorlevel: `errorlevel 2 → shogunt`, `errorlevel 1 → shoguni`. |
| `Setup.bat` | 784 B | First-run disk installer (asks # of drives / hard-disk letter, copies system files). |
| `Input.com` | 36 B | Tiny keypress→errorlevel helper used by `Setup.bat` (see §4). |
| `Setup.bin` | 2 B | Saved display/control config written by `Start.exe` (`00 02`). |

### Executables
| File | Size | Role |
|---|---|---|
| `Start.exe` | 12 778 B | Front-end: detects hardware, asks joystick/keyboard, shows the title screen, writes `Setup.bin`, exits with an errorlevel selecting the graphics build. |
| `Shoguni.exe` | 29 489 B | **The game — IBM/CGA build.** |
| `Shogunt.exe` | 29 953 B | **The game — Tandy build** (16-colour). Functionally identical mechanics; only the graphics driver/assets differ. This is the build that was decompiled. |

### Data
| File | Size | Role (reconstructed) |
|---|---|---|
| `String0.bin` | 4 176 B | **All game text** + a small region grid, as length-prefixed strings in a custom font encoding (§5–§6). |
| `Chrnames.bin` | 400 B | The character/object **name table** — byte-identical to the first 400 bytes of `String0.bin`. |
| `Objects.bin` | 8 960 B | **35 × 256 bytes** — per-location object/scenery layout (tile data). |
| `Screens.bin` | 6 118 B | Screen/room layout data for the 128-screen world map. |
| `Sprites.bin` | 20 608 B | Character/animation **sprite sheet** (Tandy variant). |
| `Spritesm.bin` | 20 480 B | Sprite sheet (second/mono variant). |
| `Charsett.bin` | 24 576 B | Graphical **character-set / tile bank** (one display target). |
| `Charsetx.bin` | 12 288 B | Character-set / tile bank (other display target; exactly half size). |
| `Titlescr.cmp` | 6 047 B | **Compressed title screen** (RLE; decompressed by `Start.exe`'s `DECOMP` routine). |
| `Savegame.bin` | 3 416 B | **Saved game state**: `106 × 32-byte` entity records + a 24-byte trailer (§7). |
| `Dbdata1.bin` | 464 B | Initial-state/placement database (compact ~4-byte entries). |

---

## 4. Boot & launch flow

```
AUTOEXEC.BAT ─► START.EXE ──(errorlevel)──► SHOGUNI.EXE   (IBM / CGA)
                   │                    └──► SHOGUNT.EXE   (Tandy 16-colour)
                   ├─ detects computer type      (GETCOMPTYPE / EGACHECK)
                   ├─ forces a colour video mode  (FORCECOLOR — pokes BIOS
                   │    equipment word at 0040:0010, bits 4-5)
                   ├─ asks "joystick or keyboard?" (GETCONTROLS — J/K key)
                   ├─ loads + RLE-decompresses TITLESCR.CMP
                   │    (LOADTITLEIBM / LOADTITLETANDY → DECOMP)
                   └─ writes the choice to SETUP.BIN     (SAVESETUPFILE)
```

`Start.exe` still contains its linker symbol table, which names its routines
outright: `GETCOMPTYPE`, `EGACHECK`, `FORCECOLOR`, `GETCONTROLS`, `SETMODE`,
`SCANLINES`, `LOADTITLEIBM`, `LOADTITLETANDY`, `DECOMP`, `SAVESETUPFILE`,
`VIDEOENABLED/DISABLE`, plus the prompt strings *"Are you using joystick or
keyboard? (Press the J or K key)"*, *"Please center your joystick"*, and
*"Press the space bar to select colors / Press the Enter key when done"*.

`Setup.bin = 00 02` encodes that saved choice; byte 1 = `2` corresponds to the
`errorlevel 2 → SHOGUNT` (Tandy) branch.

**`Input.com`** (used only by the disk-`Setup.bat`) is a 36-byte program that
loops on `INT 16h` (read key) and exits via `INT 21h/4Ch` with **AL = the key's
ASCII code**, so the batch file can test `if errorlevel`. It accepts `1 2 3`
(drive count) and `c d e` (hard-disk letter, case-insensitive).

---

## 5. Text encoding (the "ASCII−7" font scheme)

The game does not store text as ASCII. It uses a **compact bitmap font** whose glyph
slots are packed so digits and letters are contiguous, and stores text as glyph
indices. The mapping recovered by decoding is:

| Stored byte | Meaning |
|---|---|
| `0x20` | space |
| `0x21`–`0x2F` | punctuation `! " & ' ( ) , - . /` etc. (literal ASCII) |
| `0x30`–`0x39` | digits `0`–`9` (literal ASCII) |
| **`0x3A`–`0x53`** | **letters `A`–`Z`** (i.e. `A`–`Z` shifted **−7** from ASCII, sitting immediately after `9`) |
| `0x01`–`0x1A` | a **word-initial capital** letter, encoded as a 1-based index (`1=A … 26=Z`) |

So `A`–`Z` live at `0x3A`–`0x53` (reusing the ASCII punctuation slots `: ; < = > ? @`
for `A`–`G`), which lets the font drop the gap between `'9'` and `'A'`. Interior
letters use that range; the first letter of a word/name is instead stored as a small
1-based index. Decoding rule:

```python
def glyph(b):
    if 0x3A <= b <= 0x53: return chr(b - 0x3A + ord('A'))   # interior A-Z
    if 0x01 <= b <= 0x1A: return chr(b - 1  + ord('A'))     # word-initial capital
    if b == 0x20:         return ' '
    if 0x30 <= b <= 0x39: return chr(b)                     # digit
    return chr(b)                                           # punctuation, literal
```

**String container:** `String0.bin` is a flat array of **length-prefixed strings**
(Pascal-style). Each record is `[len][len-1 payload bytes]` — the length byte
**counts itself**. Control bytes `0x1B–0x1F` embedded in strings are formatting codes
(line break / wait-for-key / colour). Empty `len == 1` records act as padding/blank
slots. The code addresses strings **by index**.

At run time the loaded text is treated as **several concatenated sub-tables, each
indexed from 0** — which is exactly why the indices in §6 fall into contiguous
category blocks. The engine sets a table-base pointer (`0xD096`) and a per-table
index (`0xD138`), then a resolver (`FUN_1000_078C`) walks the length prefixes to find
the string and a glyph loop (`FUN_1000_40D8` → font blitter `FUN_1000_4B5C`, font at
`0x7C8A`, cell = code·0x28) renders it into the 40-column line buffer. The sub-table
bases: **names `0xBFE8`**, **main messages `0xC120`**, **object names `0xC698`**,
**location names `0xC810`**, **verbs `0xCAB0`**.

For the main-message path the code sets a small **runtime message id** in byte
`0xD11A`. That id maps to the string indices used in this document by a simple linear
offset (confirmed across many call sites):

> **string index = `0xD11A` id + 0x43 (67)**

so e.g. id 1 → "THERE IS NOBODY HERE!", id 0x14 → "YOU DIE HIDEOUSLY", id 0x3B/0x3C →
the win messages. Gendered message pairs are chosen by adding 1 for the male variant,
tested via an entity gender flag (`entity[0x0A] & 0x80`).

---

## 6. Extracted game content (from `String0.bin`)

The string table decodes cleanly and, by itself, exposes most of the design. Indices
below are the record numbers in `SHOGUN_strings_indexed.txt`.

### Character classes (`[0]`–`[13]`)
Male: **CAPTAIN, SERVANT, LORD, PEASANT, BANDIT, PRIEST, ZEN MASTER, SAMURAI**.
Female: **SERVANT, LADY, PEASANT, BANDIT, PRIESTESS**.

### Named characters (`[17]`–`[56]`, ~40 people)
BLACKTHORNE, MARIKO, TORANAGA, ISHIDO, YOTAKA, KIKU, RAKO, AUTUMN MOON, JADE,
MOONLIGHT, WILLOW, PERSIMMON, PINE, KOKU, STONE, NIGHT RAIN, CAMELLIA, ASA, YOKO,
NOVI, KOGAI, YAMAHA, SUZUKI, DANSHICHI, BLOOD, MURAJI, IKEMATSU, SKY DRAGON, NAGA,
LION, HAWK, TIGER, PEREGRINE, OMI, YOSHINAKA, KATANA, WAKIZASHI, TACHI, KOZUKA,
HACHIMAN. (`R.I.P.` is the gravestone label; `YOU` labels the player.)

### The 16 objects (`[184]`–`[199]`, in object-ID order)
`FISH · CHERRIES · SAKI · SHIELD · HELMET · SWORD · CASH · CASH · DIAMOND ·
PRAYER WHEEL · GOLD MASK · BOOK · ROSE · BUDDHA · SCROLL · MIRROR`

Groups: consumables (FISH, CHERRIES, SAKI), combat gear (SHIELD, HELMET, SWORD),
currency (CASH ×2), valuables (DIAMOND, PRAYER WHEEL, GOLD MASK, BOOK, ROSE), and the
three **victory relics** (BUDDHA, SCROLL, MIRROR).

### Personality traits (`[203]`–`[215]`) — 6 axes
Each NPC is described along these axes (shown when you examine them):

| Axis | Low ↔ High |
|---|---|
| Disposition | **HOSTILE** ↔ **PLACID** |
| Fighting skill | **A WEAK FIGHTER** ↔ **A GOOD FIGHTER** |
| Ambition | **UNAMBITIOUS** ↔ **GREEDY** |
| Wealth | **POOR** / **WEALTHY** / **VERY RICH** |
| Resolve | **GULLIBLE** ↔ **RESOLUTE** |
| Intelligence | **A BIT DIM** ↔ **CUNNING** |

Examine text is assembled as *"HE/SHE LOOKS/SEEMS/APPEARS <trait> AND/BUT <trait>"*.

### Follower command menu (`[111]`, `[112]`)
Two-level order menu for your followers:
- **Verbs:** `TAKE · GUARD · ATTACK · BEFRIEND · PROTECT · END`
- **Objects:** `FOOD · WEAPONS · CASH · VALUABLES · THIS AREA`

Confirmations: *"I OBEY," HE/SHE REPLIES*, and orders are queued as `1ST ORDER` /
`2ND ORDER`.

### World map — 35 named locations (`[244]`–`[278]`)
HEAVENS ABOVE · SEVENTH HEAVEN · THE TEMPLE OF THE BUDDHA · THE GATE OF HEAVEN ·
NEAR THE TEMPLE · THE SMALLEST PAGODA · THE VALLEY OF WHISPERS · IN THE VALLEY ·
IN THE SHOGUNS PALACE · THE SHOGUNS PALACE · THE NIGHT BUDDHAS SHRINE · THE PLATEAU
OF THE MOON · THE DOOR TO THE NIGHT · THE MOUNTAINS OF TEARS · PASSAGE OF THE WIND ·
THE DRAGONS MOUTH · THE BLUE CAVES · NEAR TO ENLIGHTENMENT · ZEN MASTERS PALACE ·
TUNNEL OF LOVE · THE GATE OF DAWN · THE PAWNBROKER · NEAR THE RIVER · THE MURKY
RIVER · THE BRIDGE OF DREAMS · NEAR THE BRIDGE · THE WEST PALACE · THE DARK FOREST ·
THE OLD PALACE · THE EAST PALACE · ON THE SHORE · NEAR THE SHORE · THE GARDENS OF
LIFE · THE ROCKY PATH · THE OLD MINE.

(These 35 named regions cover the ~128 individual screens of the world.)

### Event/log verbs (`[286]`–`[309]`)
Third-person messages for the on-screen event ticker: *TAKES / EATS SOME / DRINKS
SOME / GUARDS THIS AREA / ATTACKS / RUNS AWAY! / YIELDS! / DIES! / BEFRIENDS / GIVES
A / GIVES THE / ORDERS / NEWS FROM / PROTECTS / GIVES SOME / **HAS BECOME SHOGUN.***

### System / UI strings
`GAME OR DEMO`, `SAVE OR LOAD`, `SET FILE NAME. SHOGUN 0`, `USE JOYSTICK TO CHOOSE
PERSON.`, `DON'T OVERBURDEN YOURSELF!`, `CHOOSE POCKET.`, `POCKET OR CASH`,
`HOW MUCH? XXX YEN`, `YOUR POCKETS ARE EMPTY!`, `DO YOU WANT TO YIELD? YES NO`,
`YOU DIE HIDEOUSLY. PRESS FIRE!`, `THE BUDDHA SAYS`, `THIS IS THE GRAVE OF`.

---

## 7. Executable architecture (`Shogunt.exe`)

Ghidra recovered **338 functions**. The `INT 21h` map and decompilation give the
skeleton below. (All addresses are `segment:offset`, image segment `0x1000`.)

### Data loading — `FUN_1000_5b58`
Opens/reads/closes **eight** data files (`INT 21h` AH=3D/3F/3E) into fixed memory
buffers at start-up: the two font banks, `Chrnames`, `Objects`, `Screens`, the
sprite sheet, `String0`, and `Dbdata1`. A separate loader `FUN_1000_5d2f` reads a
single file (the save/setup path).

### Timing / animation — `FUN_1000_69c4`
Saves the old `INT 1Ch` vector (AH=35) and installs its own (AH=25). The ISR body
(`FUN_1000_6a11`) drives **sound/music timing and animation cadence** (the main loop
paces to 2 ISR ticks per frame in `FUN_1000_1d20`). The **fail timer** — the
countdown behind *"DON'T TAKE TOO LONG!"* — is a **real wall-clock countdown** held
in word `0xD0A4` and decremented by `FUN_1000_67b9`/`FUN_1000_67e9` using the DOS
time-of-day service (`INT 21h/2Ch`); when it reaches 0 the game is lost. (The
frame-wrap counter `0xD0E4`→`0x11242` is only a running statistic, not the limit.)
`INT 21h/2Ch` is also read at start-up to seed the RNG.

### Input — keyboard or analog joystick (chosen at startup)
The control mode is stored in `0x11240` (set from `Setup.bin`). **Keyboard**
(`INT 16h`, `FUN_1000_524c/5259`) translates arrow/keypad scancodes into an 8-way
direction code plus Space/Enter = *FIRE*, F1 = quit, Ctrl-S = sound toggle.
**Joystick** mode reads the **PC game-port at I/O `0x201` directly** (`in(0x201)`,
via `FUN_1000_6c78`) for the analog stick and fire button (matching *"USE JOYSTICK
TO CHOOSE PERSON"* / *"PRESS FIRE"*). No mouse (`INT 33h`) is used.

### Main loop — `FUN_1000_072e`
Entry `FUN_1000_2a4c` does first-stage setup (installs vectors, video) and hands off
to **`FUN_1000_072e`**, the true bootstrap + endless loop: it runs the 8-file loader,
**procedurally generates each location's terrain grid** (`FUN_1000_235c` via the
RNG), initialises all 40 entities from class templates (`FUN_1000_17c0`), then spins
`while(true)`. Each pass: draw the location (`FUN_1000_5589`), **update every entity**
— player input + NPC AI (`FUN_1000_3bf3`), advance sound (`FUN_1000_6d7a`), tick the
frame/clock counters, redraw the inventory bar, process keys, and render the
event-log line (`FUN_1000_3d32`). (`FUN_1000_4a28` — named in early notes — is not
the top loop; it is the **HUD/status-panel compositor** called from the interaction
sub-loops.) Exit restores the hooked vectors and terminates via `INT 21h/4Ch`.

### System-call profile
`INT 21h` dominates (file I/O, get-time, vector get/set, terminate); `INT 10h`
(video) and `INT 16h` (keyboard) are used sparingly — the game renders graphics by
writing video memory directly rather than through BIOS.

---

## 8. Data structures

### 8.1 Entity table & `Savegame.bin`
The **live entity table** is **40 records × 32 bytes** based at data-segment offset
`0x900` (record *N* = `0x900 + N*0x20`). Each record is one live thing in the world —
the player, an NPC, or an object lying on the ground. Its 32-byte layout is fully
mapped in §9.4.

`Savegame.bin` (3416 bytes) is a **contiguous RAM snapshot** that serialises the
entity table together with the other in-memory tables the game keeps in the same
data segment — the per-location terrain grid (`0xE00`, 40 bytes/row), the 4-byte
event-log ring buffer (`0x1450`), the location-descriptor table (`0x14A0`), and the
block of global state bytes (`0xD0xx`: player index, current area, game clock,
game-over latch, …). The strong 32-byte periodicity seen in the file is the entity
record stride showing through this dump. The `SAVE OR LOAD` / `SET FILE NAME.
SHOGUN 0` UI writes/reads it (`SHOGUN 0`, `SHOGUN 1`, … save slots).

### 8.2 `Dbdata1.bin`
464 bytes of compact records (~4-byte cadence) with a small recurring index set
(`02,03,04,05,06,07,0A,0E,0F`). This is the initial-placement / seed database used to
populate the entity table for a **new** game (as opposed to `Savegame.bin`, which is
a saved state).

### 8.3 `Objects.bin`
`8960 = 35 × 256`: one 256-byte block per named location, holding that location's
tile/scenery layout (the raw data shows tiled border/fill patterns).

### 8.4 `Screens.bin`
Layout data for the 128-screen world (screen→region mapping and per-screen
tile/exit data). A small `15 × 17` region grid is also appended to the tail of
`String0.bin`, likely the schematic overview map.

### 8.5 Graphics banks
- `Charsett.bin` (24 576) and `Charsetx.bin` (12 288) are the graphical
  character-set / tile banks for the two display builds (the halved size of
  `Charsetx` reflects a lower colour depth / single-plane layout). Their dominant
  bytes (`0x22`, `0x55`) are classic CGA/Tandy dither patterns.
- `Sprites.bin` / `Spritesm.bin` (~20 KB each) are the character/animation sprite
  sheets for the two builds.
- `Titlescr.cmp` is the RLE-compressed title image (note the long `0xFF` runs),
  expanded by `Start.exe`'s `DECOMP`.

---

## 9. Game mechanics

You control one character in a living, real-time world of **40 entities** (the
player, NPCs, and ground objects), each a 32-byte record based at `0x900`. NPCs run
their own AI every frame, so the world moves without you — the event ticker reports
them taking, eating, attacking, yielding, dying, befriending and giving. All figures
below are read directly from the `Shogunt.exe` decompilation; addresses are
`FUN_1000_xxxx` and entity byte-offsets are `entity[0x..]`.

### 9.1 Random number generator
State is a single 16-bit word at `0xD098`, seeded from the DOS clock
(`INT 21h/2Ch`) at start-up so each playthrough differs. `FUN_1000_5218` advances it
as a **xor-feedback shift register (LFSR)** — the high byte is XOR-folded with a
shifted tap and the word is rotated right three times, re-injecting the low bits at
bit 15 (it is *not* a multiply LCG). Callers reduce to a range by masking (`& 3`,
`& 7`, `& 0xF`, `& 0x1F`, …). The canonical stat roll `FUN_1000_1932` takes a packed
`base|variance` byte and returns `base + (rand & variance)`.

### 9.2 The six personality traits
Each NPC's personality is **six 4-bit stats packed into `entity[0x08]`,
`entity[0x09]`, `entity[0x0A]`** (two nibbles each), rolled at spawn by
`FUN_1000_1932` from per-class `base|variance` templates. Examine text ("HE
LOOKS/SEEMS/APPEARS … AND/BUT …") is built by comparing each nibble to a midpoint and
selecting the low/high descriptor string:

| # | Axis (low ↔ high) | Drives |
|---|---|---|
| 1 | HOSTILE ↔ PLACID | AI aggression |
| 2 | A WEAK FIGHTER ↔ A GOOD FIGHTER | combat (`entity[9]&0xF`) |
| 3 | UNAMBITIOUS ↔ GREEDY | leader-seeking AI (`entity[0xA]&0xF`) |
| 4 | POOR / WEALTHY / VERY RICH | derived from cash `entity[0xF]` |
| 5 | RESOLUTE ↔ GULLIBLE | recruitment resistance |
| 6 | A BIT DIM ↔ CUNNING | theft / deception |

### 9.3 Entity record — 32 bytes
Reconstructed from the initializer `FUN_1000_17c0`, spawn placer `FUN_1000_1a54`,
animator `FUN_1000_5607`, AI `FUN_1000_4348/41f6`, combat `FUN_1000_0a99/108c/12d0`,
and HP bar `FUN_1000_3301`.

| Off | Field |
|---|---|
| `0x00` | **Class/type** 0–13 (CAPTAIN…PRIESTESS); also the entity's current **location id** for co-location tests (`entity[0]==currentArea`) |
| `0x01` / `0x02` | **X / Y** tile position |
| `0x03` | **action timer / cooldown** (counts down; drives AI re-decisions) |
| `0x04` | **disposition / attention** (bit 0x80 flag; low 7 bits a decaying attention value — raised by gifts) |
| `0x05` | visibility / light-level bitmask |
| `0x06` | secondary "hit" flag (0xFF when knocked/dying) |
| `0x07` | **facing / animation direction** nibble (0–0xE dir, 0xF = idle) |
| `0x08`,`0x09`,`0x0A` | **the six packed trait stats** (2 nibbles each) — see 9.2 |
| `0x0B`–`0x0E` | **inventory** slots 1–4 (object ids 0–15; 0 = empty) |
| `0x0F` | **cash / purse** (YEN) — the amount moved by TAKE/GIVE |
| `0x10` | **HIT POINTS** — spawned at `(rand & 0x7F) + 0x7F` (~127–254); 0 = dead |
| `0x11` | **owner / master** — index of the entity this one belongs to (its recruiter/lord); the recruit-write sites set `[0x11] = recruiter`. Also serves as a home/faction id. |
| `0x12`,`0x13` | AI mode + sub-state (also the two queued order verbs) |
| `0x14` | **primary STATE** (0 normal, 3 dead/grave, 6 defeated, 7 guard/flee, 10 player-yield-prompt) |
| `0x15`,`0x16` | AI target x / target-or-leader index (also the two queued order objects) |
| `0x17` | leader/interaction scratch (read as a master link in the combat/AI passes) |
| `0x18`,`0x19` | move-target X / Y |
| `0x1A` | animation frame/direction latch |
| `0x1B` | flags (0x80 engaged/committed, 0x40 interacted, low nibble = fresh spawn) |
| `0x1C` | order object / order-target (also "rank/power" compared in gift & combat) |
| `0x1D`,`0x1E` | 1st / 2nd order slots (the two-order queue) |
| `0x1F` | render flags |

### 9.4 Combat & befriending share one subsystem
A central design point: **fighting and recruiting run through the same code.** Two
parallel "interaction resolvers" — **`FUN_1000_0a99` (ATTACK)** and
**`FUN_1000_108c` (BEFRIEND)** — have an identical shape: scan for a nearby target,
accumulate a score in `0xD128`, and if `score > 0x0F` call the shared applicator
**`FUN_1000_12d0`**. A separate `FUN_1000_0fef` does continuous **adjacent "chip"
damage**.

Score formulas (from the decompiled expressions):

```
ATTACK  (0x0a99): score = (attacker[8]&0xF) + accuracy + (defenderTerrain>>4)
                        + 2*lineOfSight + (rand & 7)      ; vs threshold 0xD10E
BEFRIEND(0x108c): score = base(0x10 + rand&0xF) + terrain
                        + (attacker[0xA]&0xF)            ; your persuasion nibble
                        - targetResistance(0xD0F4)       ; clamp >= 0; RESOLUTE = high
                        + (rand & 7)                     ; SUCCESS if score > 0x0F
Adjacent dmg (0x0fef): ((attacker[8]&0xF) >> 1) + rand(0..3)  → defender[0x10] (floored 0)
```

So swings/persuasion are **probabilistic score-vs-threshold**, while sustained
contact is an **HP drain**.

**Shared applicator `FUN_1000_12d0`** — on success it sets the target's state
`entity[0x14] = 6` (*subdued*) and, crucially, sets the target's **owner/master link
to the actor** (`target[0x11] = actor`). The winning **margin** in `0xD128` then decides the flavour:

- margin **< 0x12 → YIELD** — the target lives and *joins the actor's following*.
- margin **≥ 0x12 → DIE** (lethal bit 0x80; gravestone, drops items → *"THIS IS THE
  GRAVE OF …"*).
- unreachable/fleeing target → **RUN AWAY** (`FUN_1000_0c94`, state 7).
- if the subdued party is the **player**, state 10 raises *"DO YOU WANT TO YIELD?
  YES NO"*.

Because both resolvers end here, **beating an NPC in a fight and befriending it are
the same act** — a subdue that, on a small margin, conscripts the loser into your
army (`loser[0x11] = you`). Results aren't printed inline: they are pushed onto a
**16-slot, 4-byte event queue at `0x1450`** `[location, actorIdx, verb, targetIdx]`
and rendered by the news ticker `FUN_1000_3d32`, which picks 2nd-person verbs for the
player (*ATTACK/YIELD/DIE*) vs 3rd-person for NPCs (*ATTACKS/YIELDS/DIES*).

### 9.5 Recruitment & the following
The **owner/master link is `entity[0x11]`** = the index of the entity one follows, and the
governing trait is the **RESOLUTE↔GULLIBLE** stat in the **low nibble of
`entity[0x0A]`** (high = resolute = resists). Followers are gained three ways:

- **By force** — win a fight: `FUN_1000_12d0` sets the subdued loser's
  `[0x11] = winner` (see 9.4).
- **By persuasion** — BEFRIEND an NPC through the interaction resolver, gated by the
  target's resolve nibble vs your persuasion + RNG; success announces *"I FOLLOW
  YOU, GREAT ONE."* (prompted by *"WILL YOU FOLLOW HIM/HER? Y OR N"*).
- **Autonomously** — a leaderless NPC seeks a nearby strong master in the AI routine
  `FUN_1000_41f6`, whose gate is `(rand & 3) + (entity[0x0A] & 0xF) > 10` (so
  gullible NPCs drift into a following on their own); on success it writes
  `[0x11] = chosenMaster`.

Un-recruited NPCs say *"I HAVE NO MASTER!"*. **Gifts** (`FUN_1000_34e6`, acceptance
roll `FUN_1000_3753 < 0x15`) sweeten a target concretely: a landed gift **increments
the target's friendliness nibble — the low nibble of `entity[0x09]` (0–15)** — and
that same nibble is read as a *positive* term by the befriend/acceptance rolls, so
gifting an NPC measurably raises your odds of recruiting it later. (`entity[0x09]`
packs two trait axes; its low nibble is the gift-boostable disposition, and both
nibbles also feed the "examine person" trait descriptions.) The selectable-target
list is capped at **7** (`FUN_1000_5e52`) and the world holds **40 entity slots**; a
full inventory/world drives *"SORRY TOO CROWDED!"* and a large party reports
*"YOU HAVE A LARGE FOLLOWING."*.

> Note: the decompiled recruitment/combat routines heavily reuse the same
> entity-offset scratch space (`0x12`–`0x1C`), so several `FUN_` roles were read
> slightly differently across analysis passes; the offsets and gates above are the
> ones that were consistent across independent traces.

### 9.6 Ordering followers
The two-level order menu is drawn by **`FUN_1000_383a`** (level 1) and
**`FUN_1000_669d`** (level 2), navigated with the joystick:

- **Level 1 — verbs**, cycling index 0–5: `0 TAKE · 1 GUARD · 2 ATTACK ·
  3 BEFRIEND · 4 PROTECT · 5 END`. Only TAKE opens the sub-menu; ATTACK/BEFRIEND/
  PROTECT invoke the target picker; END finalises.
- **Level 2 — objects** (index 0–3): the object **category = `index << 5`** →
  `FOOD 0x20 · WEAPONS 0x40 · CASH 0x60 · VALUABLES 0x80`, matching the inventory
  category bits (`slot & 0xE0`) so a tasked follower scans slots `0x0B`–`0x0E` for
  that category. (`THIS AREA` is the area-scoped variant.)

The chosen verb/object pair is written into the follower's **order queue** — verbs
at `entity[0x12]`/`[0x13]`, objects/targets at `entity[0x15]`/`[0x16]` (up to two
orders: *1ST ORDER* / *2ND ORDER*) — and sets its state `entity[0x14]`
(GUARD→3, ATTACK→7/combat, PROTECT→10/follow). The follower acknowledges *"I OBEY."*,
and each tick its queued order is dispatched through a handler table at `0x2200`
(indexed by `entity[0x1B] & 7`). You thus direct a small army to fetch
food/weapons/cash/valuables, guard an area, attack rivals, or protect you.

### 9.7 Giving, taking & economy
- **Give / gift** (`FUN_1000_34e6`) is the player transfer action: you pick from
  *your own* pockets (*CHOOSE POCKET → POCKET OR CASH → HOW MUCH? XXX YEN*, cash
  amount capped at **15 per transfer** via `FUN_1000_36a6`) and hand it to an
  adjacent NPC. A two-stage roll (`FUN_1000_3753`, then a check vs a relationship
  threshold) summing the target's disposition nibble, faction values and RNG picks
  the reaction: refusal (*"I'M NOT INTERESTED!"* / *"LEAVE ME ALONE!"*), grudging
  take (*SHE TAKES / HE SNATCHES YOUR GIFT*), or gracious accept (*"THANK YOU," SHE
  CHIRPS* / *HE BEAMS*). A landed gift **raises the target's friendliness** (see 9.5).
- **Taking from others** is *not* a player pickpocket action — the *CHOOSE POCKET*
  prompts above are for choosing which of *your* pockets to give from. Acquiring an
  NPC's goods is done by **ordering a follower** (TAKE FOOD/WEAPONS/CASH/VALUABLES),
  executed by NPC AI (`FUN_1000_1209`), which moves cash (`entity[0x0F]`) or an
  inventory object (slots `0x0B`–`0x0E`) between entities.
- **Economy**: currency is **YEN** = a single per-entity byte `entity[0x0F]`. An
  object's *category* is the top 3 bits of its id: `0x20` = food, `0x60` = cash/drink,
  and `0xC0` = the **top-caste relics/valuables** (what the victory tally counts,
  §9.9). There is **no fixed price table**; **THE PAWNBROKER** and every valuation
  derive an object's worth dynamically from its class-descriptor nibbles + RNG
  (`FUN_1000_1417`), so prices vary per encounter.

### 9.8 Health, eating & death
`entity[0x10]` is the health pool, spawned at `(rand & 0x7F) + 0x7F` (**127–254**)
and drawn as an 8-segment HP bar (`FUN_1000_3301`, one tile per `0x20` HP). It is a
**pure combat pool**: a full scan of the loop and timer found **no passive hunger or
health drain** — HP falls only to melee damage (`FUN_1000_0fef`:
`dmg = ((attacker[8]&0xF) >> 1) + rand(0..3)`, floored at 0). Eating a food object
(FISH/CHERRIES, category `0x20`) restores **+0x10 HP** (cap `0xFF`); drinking (SAKI,
category `0x60`) tops up a second stat. HP reaching **0 is death** — *"YOU DIE
HIDEOUSLY. PRESS FIRE!"* (`FUN_1000_3301` sets message id `0x14`) — dropping your
goods and leaving a gravestone. Inventory is 4 object slots; a full inventory refuses
with *"DON'T OVERBURDEN YOURSELF!"* (status `0xD0D2 = 2`).

### 9.9 Victory & defeat
The endgame is evaluated in **`FUN_1000_27b8`**, entered whenever the player picks up
an object. It first rebuilds a **per-entity follower-count table at `0x15A0`**; once
the player-leader's following crosses a threshold (`> 0x13` = 19 — surfaced as
*"YOU HAVE A LARGE FOLLOWING."*), the "become-Shogun" challenge opens: a **contest
location `0xD0D0` is chosen from a 4-entry table at `0x3377`** (the palaces) and the
player is moved there, with the reminder *"DON'T TAKE TOO LONG!"*.

**Victory check** (~line 3233): it scans the descriptor table at `0x14A0` and counts
entries at the contest location whose top caste bits are `0xC0` — the marker carried
by the three sacred relics (**BUDDHA `0x0D`, SCROLL `0x0E`, MIRROR `0x0F`**). When the
**count reaches 3**, it prints message ids `0x3B`/`0x3C` → *"WELL DONE! SHOGUN"* /
*"ALL JAPAN HONORS YOU."*, and the leader *"HAS BECOME SHOGUN."* The two framings are
one and the same: **build your following and bring the three relics to the palace.**
(The full objective briefing — *"TO BECOME SHOGUN YOU MUST TAKE THE BUDDHA, SCROLL &
MIRROR TO …"* — is shown by the setup/intro, not `Shogunt.exe`, which is why those
particular strings are never emitted from within the game binary.)

**Defeat** is the **time limit**: a real wall-clock countdown in word `0xD0A4`,
decremented from the DOS time-of-day clock (`FUN_1000_67b9`/`67e9`, `INT 21h/2Ch`).
When it reaches 0 the tally fails and the game prints *"YOU HAVE FAILED! YOU MUST
DIE! YOUR DEATH IS SLOW AND PAINFUL."* Dying to combat (HP 0) ends the run locally
with a gravestone, but the clock is the hard fail behind *"DON'T TAKE TOO LONG!"*.

### 9.10 Save / load (caveat)
The game **loads** external state: `FUN_1000_5b58` reads the 8 asset files and
`FUN_1000_5d2f` reads two entity/definition tables; the *SAVE OR LOAD* / *SET FILE
NAME. SHOGUN 0* UI selects a slot suffix (`SHOGUN 0`, `SHOGUN 1`, …). Notably, the
`INT 21h` map for `Shogunt.exe` contains **open/read/close but no create (`3Ch`) or
write (`40h`)** — so the actual disk *write* of a save is not present in the
recovered code path (it is reduced to unresolved thunks, or handled via disk-swap
overlay logic — the *"insert SHOGUN disk"* prompts sit around these routines). When
written, the persisted state is the entity table (`0x900`, 40×32) plus the world
object/location tables and state globals; the per-entity sprite pointer (bytes 7–9,
segment ~`0x1F00`) is **regenerated on load**, not stored. `Savegame.bin` as shipped
is the initial/starting state (the front-end also offers *GAME OR DEMO*).

---

## 10. How to reproduce

```bash
# Import + auto-analyse (16-bit real mode MZ):
analyzeHeadless C:\ghidra_proj SHOGUN -import Shogunt.exe Shoguni.exe Start.exe \
    -processor "x86:LE:16:Real Mode"

# Decompile everything + classify INT 21h calls:
analyzeHeadless C:\ghidra_proj SHOGUN -process Shogunt.exe -noanalysis \
    -scriptPath C:\ghidra_scripts \
    -postScript DecompAll.java   C:\ghidra_out\SHOGUNT_all.txt \
    -postScript DosSyscalls.java C:\ghidra_out
```

The Python decoders for `String0.bin` / `Chrnames.bin` (font scheme §5) and the
`Savegame.bin` record dumper are in the analysis notes; the decoded, indexed string
table is `C:\ghidra_out\SHOGUN_strings_indexed.txt`.
