# Jumpman Lives! — Reverse Engineering Notes

## 1. Game Identification

| Field | Value |
|-------|-------|
| **Title** | Jumpman Lives! |
| **Author** | Dave Sharpless |
| **Publisher** | Apogee Software |
| **Year** | 1991 |
| **Platform** | MS-DOS |
| **Language** | Borland Turbo Pascal 6.0 |
| **Shipped EXE** | `JMAN2.EXE` (136,431 bytes) |
| **Genre** | Platformer / Lode Runner clone |
| **Status** | Shareware (2 playable levels); registered version has 45 |

The game is a tribute to Epyx's 1983 *Jumpman* — collect all the bombs/pellets on each screen while avoiding hazards. It supports VGA, EGA, and CGA graphics modes (auto-detected, or forced with `/VGA`, `/EGA`, `/CGA` command-line switches).

## 2. Source Code

The complete Turbo Pascal 6.0 source code was recovered from archive.org (`jml_src.7z`). It contains 567 files including:

- `JMLIVES!.PAS` — main program, menu, game-mode selection
- `GLOBALS.PAS` — global variable declarations, `GetInput`, video mode detection
- `ROUTINES.PAS` — game loop (`Play`), movement (`MovePlayer`, `Do_Actions`), collision, save/load
- `WRITESU.PAS` — main menu (`Main_Selector`), high scores, ordering info
- `TYPES.INC` — the `player` and `dot` record definitions
- `JMLIVES!.MAP` — Borland linker map (symbol → segment:offset)
- `SC01U.PAS`–`SC45U.PAS` — the 45 level definitions
- `JMLIVES!.EXE`, `JMLIVES!.MAP` — compiled binary and map from the source

The source was the primary reverse-engineering resource — the MAP file gives every global's offset directly, and the source gives every field's type and semantics. The shipped `JMAN2.EXE` is an EXEPACK-compressed build of this same source.

## 3. Executable Format

`JMAN2.EXE` is a standard MZ DOS executable with EXEPACK compression:

- **Header**: 7 paragraphs (112 bytes), 267 pages (136,431 bytes total)
- **Relocations**: zero (EXEPACK relocates internally)
- **CS:IP**: `0xFFF0:0x0100` (decompressor entry)
- **Load image**: starts with EXEPACK stub bytes `B8 CA 4C BA 48 21…` followed by the "Not enough memory" error string

The on-disk data is compressed. Key patterns like PLAYSPEED are found at file offset `0x1FBED` in compressed form with modified surrounding data, and the `keyd` string has null bytes inserted periodically. At runtime in DOSBox, the program unpacks and the data layout matches the MAP file exactly.

## 4. Data Segment Layout

The MAP file (`JMLIVES!.MAP`) gives the data segment base as `0x3E8C` (this segment value changes per session — only the offsets are constant). Every global below is at a fixed offset within this data segment (DGROUP).

### 4.1 Anchor Patterns (static initialised data)

These constant byte runs identify the data segment uniquely in the emulator's memory:

| Symbol | DGROUP Offset | Bytes | Content |
|--------|--------------|-------|---------|
| `PLAYSPEED` | `0x7D26` | 8 | `03 07 0B 0F 11 14 1B 26` — speed table (tick intervals) |
| `jp1` | `0x7D46` | 22 | `02 02 02 02 02 02 00 00 00 FE FE FE FE FE FE FE FE FE FE FE FE` — vertical jump trajectory |
| `ftwo` | `0x7D90` | 6 | `01 2B 03 2A 17 01` — VGA palette entries for pellets |
| `keyd` | `0x7D98` | 129 | `STRING[128]` — scan-code → character mapping table |

**Primary anchor**: `jp1` at `0x7D46` (22 bytes — long enough to be unique in 16 MB of guest RAM).
**Validators**: `PLAYSPEED` at `0x7D26` and `ftwo` at `0x7D90`.

### 4.2 Game State Globals

| Symbol | Offset | Type | Size | Initial | Notes |
|--------|--------|------|------|---------|-------|
| `trainer` | `0x7D2E` | BOOLEAN | 1 | `FALSE` | Set `TRUE` by pressing TAB 4× at main menu → 21 lives instead of 7 |
| `nosnd` | `0x7D2F` | BOOLEAN | 1 | `FALSE` | Set by `/NOSOUND` command-line switch |
| `abstime` | `0x7D36` | LONGINT | 4 | `0` | Global timer tick counter (incremented by `NewTimer` ISR) |
| `current_level` | `0x7D3A` | BYTE | 1 | `1` | Level number (1–45), set by each `sc##_0` function |
| `bonus` | `0x7D3C` | LONGINT | 4 | `0` | Time bonus; set to 1500 (or 0) per level, decrements by 100 every 1100 ticks |
| `maxpl` | `0x7D40` | BYTE | 1 | `1` | Number of active players (1–4) |
| `which_to_play` | `0xD97A` | BYTE | 1 | — | Game mode: 1=Jumpman, 2=Jumpman Jr, 3=Original, 4=All, 5=Random |
| `level_start` | `0xD97E` | BYTE | 1 | — | First level in current game set |
| `level_current` | `0xD97F` | BYTE | 1 | — | Current level index within the set |
| `pl` | `0xD981` | BYTE | 1 | — | Current player index (1–4) |
| `plsx` | `0xD99E` | INTEGER | 2 | — | Player start X (set by `GenDraw`) |
| `plsy` | `0xD9A0` | INTEGER | 2 | — | Player start Y |
| `eomission` | `0xD9A8` | INTEGER | 2 | — | End-of-mission bonus per remaining life (100/250/500/750/0) |
| `max_screens` | `0xD9AA` | INTEGER | 2 | — | Remaining levels in this game session |
| `last_score` | `0x9506` | LONGINT | 4 | — | Previous score for extra-life detection (every 10,000 points) |

### 4.3 The `player` Record (92 bytes)

Defined in `TYPES.INC`. The `p` array is `ARRAY[1..12] OF player` at DGROUP offset `0xCFE6`. The MAP file confirms the size: next symbol `dots` is at `0xD436`, so `0xD436 - 0xCFE6 = 0x450 = 1104 = 12 × 92`.

| Field | Offset | Type | Size | Notes |
|-------|--------|------|------|-------|
| `im` | 0 | INTEGER | 2 | Current sprite frame index |
| `pumps` | 2 | INTEGER | 2 | Animation step counter |
| `jpump` | 4 | INTEGER | 2 | Jump trajectory index |
| `x` | 6 | INTEGER | 2 | Screen X position (pixels) |
| `y` | 8 | INTEGER | 2 | Screen Y position (pixels, 0=top) |
| `left` | 10 | BOOLEAN | 1 | Moving left |
| `right` | 11 | BOOLEAN | 1 | Moving right |
| `up` | 12 | BOOLEAN | 1 | Climbing up |
| `down` | 13 | BOOLEAN | 1 | Climbing down |
| `jump` | 14 | BOOLEAN | 1 | Jumping (directional) |
| `climbing` | 15 | BOOLEAN | 1 | On a ladder |
| `inair` | 16 | BOOLEAN | 1 | Falling |
| `wasmoving` | 17 | BOOLEAN | 1 | Was moving last tick |
| `drtouch` | 18 | BOOLEAN | 1 | Touching a draggable/green block |
| `drtoub` | 19 | BOOLEAN | 1 | draggable block above |
| `jumpup` | 20 | BOOLEAN | 1 | Vertical jump (straight up) |
| `wasdr` | 21 | BOOLEAN | 1 | Was dragging |
| `killed` | 22 | BOOLEAN | 1 | Player killed this life |
| `lrorj` | 23 | BOOLEAN | 1 | Left/right or jump pressed |
| `absb` | 24 | BOOLEAN | 1 | Raw button (jump) input |
| `absl` | 25 | BOOLEAN | 1 | Raw left input |
| `absr` | 26 | BOOLEAN | 1 | Raw right input |
| `absu` | 27 | BOOLEAN | 1 | Raw up input |
| `absd` | 28 | BOOLEAN | 1 | Raw down input |
| `dir` | 29 | BOOLEAN | 1 | Facing right (true) or left |
| `scn` | 30 | STRING[20] | 21 | Screen scan buffer (for collision detection) |
| `pdeath` | 51 | SHORTINT | 1 | Death animation phase (0=alive, 2+=dying, 50=fully dead) |
| `lives` | 52 | SHORTINT | 1 | Remaining lives (starts at 7, or 21 in trainer mode) |
| `ltouch` | 53 | SHORTINT | 1 | Ladder touch indicator (5=not on ladder, 0–4=on ladder) |
| `pels` | 54 | ARRAY[0..25] OF BOOLEAN | 26 | Which pellets/bombs have been collected |
| `speed` | 80 | BYTE | 1 | Current speed (1–8); indexes `PLAYSPEED` |
| `next_speed` | 81 | BYTE | 1 | Speed for next level (set by keys 2–9) |
| `idevice` | 82 | BYTE | 1 | Input device: 0=keyboard, 1=joystick 1, 2=joystick 2 |
| `keys` | 83 | ARRAY[0..4] OF BYTE | 5 | Scan codes for left, right, down, up, button |
| `score` | 88 | LONGINT | 4 | Player score (signed 32-bit) |

### 4.4 Default Key Bindings

Set by `Init_Once` in `ROUTINES.PAS`:

```
keys[0] := 75;  { Left arrow  → absl  (move left) }
keys[1] := 77;  { Right arrow → absr  (move right) }
keys[2] := 72;  { Up arrow    → absd  (climb) }
keys[3] := 80;  { Down arrow  → absu  (climb) }
keys[4] := 57;  { Space       → absb  (jump) }
```

Note: the internal variable names `absu`/`absd` are swapped relative to the scan codes — Up arrow maps to `absd` and Down arrow to `absu`. The net effect is correct: Up arrow moves the character up, Down arrow moves it down. The `Do_Actions` procedure interprets `down=true` as `y := y - 2` (upward on screen) and `down=false` (i.e. `up=true`) as `y := y + 2` (downward), so the end result matches the arrow direction.

Other controls (from the game loop in `Play` and the menu in `Main_Selector`):

| Scan Code | Key | Action | Source |
|-----------|-----|--------|--------|
| 1 | Esc | Pause / quit confirm | `key[1]` in `Play` |
| 14 | Backspace | Skip level (trainer mode only, with S to save) | `key[14]` in `Play` |
| 15 | Tab | Press 4× at main menu for trainer mode (21 lives) | `tcount` in `Main_Selector` |
| 29+56+83 | Ctrl+Alt+Del | Hard exit to DOS | `NewTimer` ISR |
| 32 | D | Screenshot during pause (saves `.SCI` file) | `Pause_It` |
| 57 | Space | Jump (also used as menu select) | `keys[4]` |
| 59 | F1 | Pause game | `key[59]` in `Play` |
| 2–9 | 1–8 | Set game speed (1=fastest, 8=slowest) | `RTime` |

### 4.5 The `keyd` Scan-Code Table

`keyd` at DGROUP `0x7D98` is a `STRING[128]` mapping scan codes to characters for text input:

```
~1234567890-=BTqwertyuiop[]ECasdfghjkl;'`L\zxcvbnm,./R*A C||||||||||NS|||-|||+||||.
```

Position 14 = `B` (Backspace), 15 = `T` (Tab), 28 = `E` (Enter), 29 = `C` (Ctrl), 42 = `L` (Left Shift), 54 = `R` (Right Shift), 56 = `A` (Alt). The menu uses raw scan codes, not this table.

## 5. Game Mechanics

### 5.1 Game Loop

The `Play` procedure (`ROUTINES.PAS:1632`) is the core game loop:

1. For each player 1–4 with lives > 0:
2. Wait for keypress to start the life
3. Reset player state (position, flags, pellets)
4. Inner loop while lives > 0 and not stopped:
   - Wait for `PLAYSPEED[speed]` ticks (`gplay1`)
   - Move sprite, check collisions
   - Collect pellets (100 points each)
   - Check death (falling, hazards)
   - Check level complete (all pellets collected → add bonus to score)
5. On level complete: `Inc(score, bonus)`, play victory sound
6. On all levels complete: convert remaining lives to bonus points

### 5.2 Speed System

`PLAYSPEED` is an 8-element byte array that controls the game's tick rate:

| Speed | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
|-------|---|---|---|---|---|---|---|---|
| Ticks | 3 | 7 | 11 | 15 | 17 | 20 | 27 | 38 |

Lower tick values = faster gameplay (the game waits fewer timer ticks between movement frames). Speed is set with keys 1–8 during play, and persists across levels via `next_speed`.

### 5.3 Bonus System

- Each level sets `bonus` to 1500 (or 0 for three special levels: INVASION, DRAGON SLAYER, GUNFIGHTER)
- `bonus` decrements by 100 every `START_BTICK` (1100) timer ticks via `RTime`
- On level completion: `Inc(p[pl].score, bonus)` — so faster completion = more bonus points
- When bonus reaches 0, it stays at 0 (no negative bonus)

### 5.4 Extra Lives

`Update_Score` checks: `IF (last_score DIV 10000) <> (p[pl].score DIV 10000) THEN Inc(p[pl].lives)`. So every 10,000 points awards an extra life.

### 5.5 Trainer Mode

Pressing TAB 4 times at the main menu sets `trainer := TRUE`, which:
- Starts players with 21 lives instead of 7
- Enables Backspace to skip levels (Backspace+S saves the game, Backspace+Esc quits)
- Prevents the game from zeroing lives/score when quitting

### 5.6 Save Game Format

`SaveGame` writes `jmlives!.sav`:
1. `maxpl` (1 byte)
2. `which_to_play` (1 byte)
3. `level_current` (1 byte)
4. `p[1]` through `p[4]` (4 × 92 = 368 bytes)

Total: 371 bytes. No header, no checksum.

### 5.7 Config File

`Main_Selector` reads `jmlives!.cfg` on startup:
- `p[1]` through `p[4]` (4 × 92 = 368 bytes)
- `maxpl` (1 byte)

Total: 369 bytes. Stores player input device settings and max players.

## 6. Locator Strategy

The trainer follows the string-anchored `GameLocator` pattern used by `RailroadTycoonTrainer` and `AirborneRangerTrainer`:

1. **Sweep** the emulator's memory for the 22-byte `jp1` pattern
2. For each hit, compute `dgroupBase = hit - 0x7D46`
3. **Validate**: read a window from the candidate base and check:
   - `PLAYSPEED` at `base + 0x7D26` (8 bytes)
   - `ftwo` at `base + 0x7D90` (6 bytes)
4. **Plausibility**: `current_level` (1–45), `maxpl` (1–4), `trainer` (0 or 1)
5. On success: read player state from `base + 0xCFE6 + (pl - 1) * 92` (Turbo Pascal arrays are 1-based, so `p[1]` is at offset 0)

No value scanning is required — the anchor pattern uniquely identifies the data segment. If the anchor fails, the user can click Locate again after starting a game (the data segment is not initialized until the main menu loads).

## 7. The 45 Levels

Each level is a separate Pascal unit (`SC01U.PAS`–`SC45U.PAS`). Each `sc##_0` function:
1. Calls `Modify` to set up level callbacks
2. Sets `scrtitle`, `bonus`, and `current_level`
3. Calls `Play` with level-specific data and hazard pointers

Levels are grouped into three sets:

| Set | Levels | Mode | Eomission |
|-----|--------|------|-----------|
| Jumpman | 1–12 | 1 | 100 |
| Jumpman Jr | 13–27 | 2 | 250 |
| Original | 28–45 | 3 | 500 |
| All | 1–45 | 4 | 750 |
| Random | shuffled | 5 | 0 |

Three levels have no time bonus (bonus=0): level 3 (INVASION), level 7 (DRAGON SLAYER), level 11 (GUNFIGHTER).

## 8. What Was Not Reverse-Engineered

- **Map/level data**: Level layouts are compiled as external `.OBJ` files (`scr##d.obj`, `scr##t.obj`) linked into the executable. The level data format was not decoded — it is not needed for the trainer.
- **Sound/music**: The AdLib sound effects and MIDI music are in external `.OBJ` files. Not relevant to game state.
- **Sprite data**: Character and hazard sprites are in external `.OBJ` files.
- **`.SCI` screenshot format**: The pause-screen screenshot feature saves a raw video buffer dump; not decoded.
- **High score file**: High scores are stored in an undocumented format; not needed for the trainer.
