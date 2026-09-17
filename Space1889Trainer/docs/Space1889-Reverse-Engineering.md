# Space: 1889 — reverse-engineering notes

These notes cover the DOS release of *Space: 1889* (Paragon Software, 1990, under licence from Game Designers'
Workshop). They record how the trainer in this folder finds and edits the game. The work combined static analysis
of the unpacked executables (Ghidra 12.1.2 headless, plus a capstone linear sweep where Ghidra missed functions)
with experiments against the running game under DOSBox.

Confidence markers used below:

- **[Confirmed]**: pinned against the running game. Either a value was written to memory and read back on the
  game's own screen, or a known in-game action produced the expected change in memory.
- **[Static]**: unambiguous in the disassembly but not exercised live.
- **[Inferred]**: a best reading that fits the code and the data but was not traced end to end.

Offsets written `+0xNN` are relative to a character record. Offsets written `0xNNNN` are relative to the start of
the 20,198-byte game-state block. `DS:0xNNNN` is an offset into an executable's data group (DGROUP).

---

## 1. The target

| File | Bytes | SHA-1 (first 16 hex) | What it is |
|---|---:|---|---|
| `1889.COM` | 1,602 | `a9c6df216a1dad95` | Launcher ("SUPER"). Allocates the shared memory and chains the programs below |
| `SETUP.EXE` | 27,520 | `634f069cdd900a45` | Configuration menus, copy protection, new game / continue |
| `CG.EXE` | 54,400 | `130375399a6d788b` | Character generator (character pool, careers, parties) |
| `M.EXE` | 153,344 | `3925bc429ed362e9` | The ground game: cities, dungeons, planet maps, ground combat |
| `S.EXE` | 77,312 | `331a0560a2c90453` | Space navigation and ether-flyer combat |
| `DRIVER.EXE` | 45,312 | `1ee6136e0ad835c6` | Sound driver loader |
| `DEF.S` | 20,198 | `f46a68dbcae40740` | New game with the default party (a state-block image) |
| `START.OBJ` | 20,198 | — | Starting ground-object table (only its first 0x3462 bytes mean anything) |
| `ITEMS.DAT` | 12,157 | `4826fa29cda6b00b` | Item table |
| `1889.DAT` | 2,074 | `026eed251512b619` | Copy-protection word list |
| `1889.DCT` | 36,327 | `00834b8f455f200d` | Dictionary for all game text |

The other data files are `A.SYS`/`B.SYS`/`0.SYS` (maps), `A.SET`/`B.SET`, `A.NPC`/`B.NPC`, `A.DES`/`B.DES`,
`0–6.MSG` and `O.MSG`, the `*.DEF`/`*.SPR` tile sheets, the `*.PIC` pictures and archives, `*.LY` vector
layouts, `89CAREER.DAT`, `89CAREER.MOD` and `89CHARS.DAT`, plus sound (`*.VMF`) and AdLib music (`*.ROL`,
`STANDARD.BNK`).

### 1.1 Toolchain

Every executable is compressed with **Microsoft EXEPACK**. The packed image ends in an EXEPACK stub whose header
at `CS:0000` carries the "RB" signature. The files were unpacked with a small Python reimplementation of the
unpacker:

- read the stub header (real CS:IP, SS:SP, destination length);
- rebuild the 16 relocation segments that follow the "Packed file is corrupt" string;
- run the backwards RLE decoder (commands `0xB0` fill and `0xB2` copy, with bit 0 marking the last command).

| Program | Unpacked | Relocations | Real CS:IP | DGROUP (load-relative) |
|---|---:|---:|---|---|
| `M.EXE` | 148,768 | 2,192 | 0000:0000 | 0x1FD5 |
| `S.EXE` | 74,752 | 1,178 | 0000:0000 | 0x104B |
| `CG.EXE` | 53,568 | 185 | 0000:0000 | 0x0B3E |
| `SETUP.EXE` | 27,072 | 91 | 0000:0000 | 0x0594 |
| `DRIVER.EXE` | 44,832 | 50 | 0000:0000 | 0x083A |

All five programs are **Turbo C 2.0** builds. The runtime banner `"Turbo-C - Copyright (c) 1988 Borland Intl."`
sits at `DS:0004` in each, and the startup code begins `mov dx, DGROUP`. `M.EXE` and `S.EXE` also link the
*Covox Sound Development Library* (1989–90). The code is medium model: several code segments share one data
group, so every global has a constant `DS:` offset.

One Ghidra artefact matters for anyone repeating this work. Ghidra renders the state block's near offsets as
pointer arithmetic on unrelated string symbols, for example `(char *)s_THE_ONLY_CURE_FOR_FATIGUE_IS_RES_2fd5_3b26 + 0xc`
where the real meaning is `state[0x3B32]`. A post-processing script rewrote those as `D(0x3b32)[STATE]` before
the decompile was read. Ghidra also missed about a quarter of the `LES BX,[0x5737]` sites in `M.EXE`, so those
functions were read from a capstone disassembly instead.

---

## 2. Method

1. **Unpack and decompile.** Unpack with the reimplemented EXEPACK decoder, run Ghidra headless with a script
   that exports every function's C and assembly, rewrite the offset artefacts, and dump all strings with their
   `DS:` offsets.
2. **Run the game.** DOSBox 0.74 ("DOSBoxW3") with `memsize=16`, `core=normal`, `cycles=fixed 6000` and
   `output=surface`. Guest linear 0 is found through the emulated BIOS data area: `40:0000` holds the COM1 port
   `0x03F8` and `40:0013` holds the memory size, 640 KB. DOSBox pads its guest allocation by 0x20 bytes.
3. **Drive it without a desktop.** The Windows session was disconnected during the work, which ruled out
   `SendInput`.
   - Writing into the BIOS keyboard buffer at `40:001E` does **not** work. The setup program hooks INT 9 and
     reads scancodes into `DS:0x185A` itself.
   - Posting `WM_KEYDOWN`/`WM_KEYUP` to DOSBox's SDL window **does** work. Hold each key about 200 ms, or the
     game's scancode poll misses it.
   - Screenshots were taken by reading DOSBox's 32-bpp software SDL surface straight out of the process: 720×400
     in text mode, 320×200 in game.
4. **Perturb and diff.** Poke a byte, make the game redraw (open the PARTY screen, take a step), and read the
   result off the screen. Where no screen shows the field, diff the block before and after an action (a step, a
   day passing, a save).

---

## 3. The launcher and the program chain

`1889.COM` is a 1,602-byte program that calls itself "SUPER" in its error messages:

1. `mov sp, 0x740`, then INT 21h/4Ah shrinks itself to 0x75 paragraphs.
2. If the INT 33h vector (`0000:00CC`) is empty, meaning no mouse driver, it points it at an `IRET` stub. It
   clears the vector again on exit.
3. INT 21h/48h with `BX = 0x50A` allocates **the game-state block** (20,640 bytes, room for the 20,198 used).
   The segment is stored at `[0x33E]`.
4. INT 21h/48h with `BX = 2` allocates a 32-byte **comms block**, and the first 16 bytes are zeroed.
5. It builds the command tail `" SSSS CCCC\r"`, the two segments in upper-case hex, and EXECs `SETUP.EXE`.
6. It loops on the child's exit code (INT 21h/4Dh):

| Exit code | Next program |
|---|---|
| 1 | quit to DOS (text mode restored) |
| 5 | `M.EXE` |
| 6 | `S.EXE` |
| 7 | `CG.EXE` |
| 8 | `DRIVER.EXE` |
| 11 | `SETUP.EXE` |
| other | "SUPER: Invalid return code N" |

Each program parses the two hex words from its command line and keeps a far pointer to each block in its data
group. The state pointer is at **`M.EXE DS:0x5737`**, **`S.EXE DS:0x2DD5`**, **`CG.EXE DS:0x1DF5`** and
`SETUP.EXE DS:0x1851` [Static]. Live, `M.EXE DS:0x5737` held `0208:0000`, exactly where the block sat
[Confirmed]. Flag bytes in the comms block tell the next program what to do: new game, restore, or entering
space.

Because the launcher owns the allocation, **the block stays at one guest address for the whole session**, while
`M.EXE` and `S.EXE` replace each other around it.

`1889.CFG` holds five ASCII digits, in this order:

| Digit | Meaning |
|---|---|
| 1 | video: 1 VGA, 2 EGA, 3 Tandy, 4 CGA |
| 2 | input: 1 keyboard, 2 mouse, 3 joystick |
| 3 | sound: 1 none, 2 Covox through the PC speaker, 3 Covox through the Sound Master |
| 4 | money shown in: 1 pounds, 2 pennies |
| 5 | current drive: 1 hard disk, 2 floppy |

When the file exists, setup skips the menus. Esc walks back through them.

---

## 4. Copy protection

`SETUP.EXE` loads `1889.DAT` in `FUN_1000_0422`:

```
u16 count (37)
per word:
  char[50] word, each byte XOR 0xFF, NUL-padded (the padding holds leftover garbage)
  u16      N
  N × 4    reference bytes b0 b1 b2 b3
```

`FUN_1000_05b5` picks one word with `rand() % 37` and prints "In the manual, type in the word found on page %d,
paragraph %d, line %d, word %d". The arguments are pushed in an order that is easy to get wrong: **page = b0 &
0x7F, paragraph = b2, line = b1, word = b3**. The live prompt "page 8, paragraph 4, line 2, word 7" matches the
entry `88 02 04 07` = *prestige* [Confirmed].

**In this copy the check has been patched out.** After the `strcmp` the unpacked bytes at `1000:06AE` read
`0B C0 EB 07 B8 FF FF EB 07 EB 05 B8 01 00`: `OR AX,AX; JMP +7; MOV AX,-1; … MOV AX,1`. Turbo C emits `74 07`
(`JZ`) for `if (strcmp(...) == 0)`, so the `EB` is a one-byte crack. The routine always returns 1, and the
caller's failure branch at `1000:11B5` can never run. That branch prints "Your permit to own and operate an Ether
Flyer has been revoked." and exits to DOS, the one-try rule a 1991 review describes. For an unpatched copy, the
complete answer list is:

| Page | Para | Line | Word | Answer |
|---:|---:|---:|---:|---|
| 8 | 1 | 2 | 1 | hotchkiss |
| 8 | 4 | 2 | 7 | prestige |
| 8 | 4 | 3 | 3 | empire |
| 9 | 1 | 3 | 1 | populist |
| 10 | 2 | 10 | 3 | queer |
| 11 | 3 | 9 | 5 | frugal |
| 11 | 4 | 13 | 8 | savage |
| 12 | 3 | 2 | 9 | artisans |
| 12 | 4 | 5 | 1 | drunken |
| 13 | 1 | 4 | 2 | manifest |
| 13 | 2 | 5 | 2 | blanche |
| 14 | 4 | 1 | 4 | epic |
| 18 | 2 | 3 | 8 | physical |
| 27 | 1 | 2 | 8 | gentry |
| 27 | 4 | 2 | 2 | salary |
| 27 | 4 | 4 | 2 | mess |
| 35 | 1 | 5 | 1 | scorching |
| 40 | 3 | 5 | 2 | garbled |
| 42 | 2 | 2 | 6 | fatigue |
| 45 | 4 | 1 | 4 | strategies |
| 46 | 1 | 3 | 7 | alchemists |
| 51 | 1 | 3 | 1 | eject |
| 53 | 1 | 4 | 7 | permeable |
| 53 | 3 | 4 | 10 | caverns |
| 55 | 4 | 13 | 2 | dock |
| 57 | 1 | 3 | 2 | ether |
| 58 | 3 | 10 | 1 | boiler |
| 60 | 1 | 5 | 7 | goose |
| 61 | 1 | 7 | 1 | stratosphere |
| 64 | 2 | 4 | 2 | altitude |
| 68 | 2 | 6 | 1 | liftwood |
| 69 | 3 | 1 | 5 | brutish |
| 71 | 2 | 2 | 11 | ammonia |
| 71 | 3 | 9 | 5 | bungalows |
| 85 | 1 | 11 | 1 | medieval |
| 85 | 3 | 3 | 3 | quadrillion |
| 86 | 2 | 3 | 4 | scenario |

---

## 5. The game-state block (20,198 bytes)

Everything a saved game remembers lives in this one block. **`GAME → SAVE` writes it verbatim to `NAME.SAV`.**
Live, a save written by the game was byte-identical to the block in RAM, 0 differences out of 20,198
[Confirmed]. The shipped `DEF.S` is the same image, made right after starting a new game with the default party.
A RAM dump taken just after the intro matched it except for three bytes of the tick deadline at `0x3B2E`.
`START.OBJ` is **not** a state image: its first 0x3462 bytes are the ground-object table, which `CG.EXE` copies to
`0x0668`, and the rest is leftover x86 code.

How the programs use it [Static]:

- `SETUP.EXE` clears it, or reads a `.SAV` into it for "Continue Saved Game".
- `CG.EXE` "USE THIS PARTY" clears it, copies the five party records in (0x4FB bytes), then reads `START.OBJ`
  into `0x0668`.
- `M.EXE`, on a new game, writes "PARTY ACCT." into slot 5, resets the runtime fields, sets every NPC's hit points
  to 10, and places the party in the London museum (map 113, row 29, column 18).
- `S.EXE` reads and writes the flyer, fuel, combat, day, food and position fields.

| Range | Size | Contents |
|---|---|---|
| `0x0000–0x04FA` | 5 × 0xFF | character records 0–4 |
| `0x04FB–0x05F9` | 0xFF | record 5, **"PARTY ACCT."**: only `+0x00` (the account) and `+0xF2` (the name) are used |
| `0x05FA–0x05FE` | 5 | marching order, slot → character index (`0x5FA` is the leader) |
| `0x05FF–0x0667` | 5 × 21 | "in use" flag per inventory slot |
| `0x0668–0x3AC9` | 0x3462 | ground objects: six u16 counts, then 203 × 11-byte records per planet |
| `0x3ACA–0x3B10` | 0x47 | current area record (copied from `A.SET`/`B.SET`) |
| `0x3B11–0x3B23` | 0x13 | current planet record |
| `0x3B24–0x3B35` | 0x12 | food, clock, day, planet/area/map, return planet |
| `0x3B36–0x3BB2` | 125 | NPC removed/killed bits (1,000 ids) |
| `0x3BB3–0x3CBB` | 265 | never referenced; zero |
| `0x3CBC–0x3EAF` | 500 | NPC conversation progress, one nibble per id |
| `0x3EB0–0x4297` | 1000 | NPC hit points (s8, default 10) |
| `0x4298–0x42A1` | 10 | party position, light flag, previous position |
| `0x42A2–0x42E3` | 0x42 | ether flyer, fuel, space combat, bridge crew, story flags |
| `0x42E4–0x4EE3` | 512 × 6 | map-modification log |
| `0x4EE4` | u16 | map-modification count |

### 5.1 Character record (255 bytes)

| Offset | Type | Meaning | Confidence |
|---|---|---|---|
| `+0x00` | u32 | Wealth in pennies (240d = £1) | [Confirmed]: a poke read back as WEALTH on the PARTY screen and GOLD on the status panel |
| `+0x04` | u32 | **Monthly income**: set to wealth ÷ 30 at a new game, and paid into the party account on every day with `day % 30 == 0` | [Confirmed]: on day 31 the account went 0 → 31,200, the sum of the five incomes |
| `+0x08` | u32 | PERSON'S WEIGHT in lb (generator: 100 + 20 × STR) | [Confirmed] on the PARTY screen's second page |
| `+0x0C` | s8 | Maximum health = STR + END | [Confirmed] |
| `+0x0D` | s8 | Current health (a new game sets it to max − 1) | [Confirmed] |
| `+0x0E` | s8 | FATIGUE LEVEL | [Confirmed]: poke 1 read back |
| `+0x0F` | s8 | MENTAL (insanity) | [Confirmed display; effect Static] |
| `+0x10` | u8 | Dead / empty flag. Death drops all items, zeroes the whole record, then sets this | [Static] |
| `+0x11` | u8 | Owns a horse | [Static] |
| `+0x12` | u8 | Portrait 0–4 | [Static] |
| `+0x13–+0x18` | u8 × 6 | STR, AGI, END, INT, CHA, SOC | [Confirmed] for all five default characters |
| `+0x19–+0x30` | u8 × 24 | Skills, four per attribute in attribute order (below) | [Confirmed] for all five default characters |
| `+0x31–+0x48` | 24 | Never read by `M.EXE` or `S.EXE`; zero | — |
| `+0x49` / `+0x4A` | u8 | First / second career id (`89CAREER.DAT`; 0 is also ARMY (1), so check the name) | [Static] |
| `+0x4B` | u8 | 1-based inventory slot of the armour worn (0 = none) | [Static] |
| `+0x4C` | u8 | 1-based inventory slot of the weapon in hand (0 = FISTS) | [Confirmed]: status panel showed WEAPON: LIGHT REVOLVER |
| `+0x4D–+0xB5` | 21 × 5 | Inventory (below) | [Confirmed] |
| `+0xB6` | char[30] | First career name | [Confirmed] |
| `+0xD4` | char[30] | Second career name | [Static] |
| `+0xF2` | char[12] | Name, NUL-terminated | [Confirmed] |
| `+0xFE` | char | Sex, `'m'` or `'f'` | [Confirmed] |

**Skill order.** Fisticuffs, Throwing, Close Combat, Trimsman · Stealth, Crime, Marksmanship, Mechanics ·
Wilderness Travel, Fieldcraft, Tracking, Swimming · Observation, Engineering, Science, Gunnery · Eloquence,
Theatrics, Bargaining, Linguistics · Riding, Piloting, Leadership, Medicine. This order was confirmed on the PARTY
screen for all five default characters, and cross-checked in code:

- Crime `+0x1E` and Stealth `+0x1D` are read by ROB.
- Tracking `+0x23` is read by HUNT.
- Observation `+0x25` is read by STUDY and VIEW.
- Engineering `+0x26` sets the explosive fuse.
- Bargaining `+0x2B` is read by shop prices.
- Linguistics `+0x2C` is read in conversation.
- Medicine `+0x30` is read by CURE.
- Science `+0x27` is read by `S.EXE`'s course plotting.

**Social class is SOC − 1** into the table Working Class, Tradesman, Middle Class, Gentry, Wealthy Gentry,
Aristocracy. It is not stored separately [Static; matches all five default characters].

**"HEALTH: a/b".** The status panel prints `a = +0x0D` and `b = +0x0C − ⌈(STR + END) ÷ 2⌉`. A character is out
of action when `a ≤ b` (`M.EXE 1000:094C`), and also when fatigue equals STR, AGI or END. [Confirmed]: poking max
health (`+0x0C`) to 12 and health (`+0x0D`) to 5 turned Prof. Wells' "8/4" into "5/7" (STR 5, END 4, so
b = 12 − ⌈9 ÷ 2⌉ = 7). An early live note guessed b = END; that was a
coincidence of the default party's numbers.

**Fatigue** (`M.EXE 1000:0A1A`, once per game day) [Static]:

- Each character able to act rolls (END + 1) d6. If the total is below a threshold, fatigue rises by 1.
- The threshold starts at 4 and is adjusted:

| Condition | Change |
|---|---|
| on Mars | +1 |
| on Mars with ROUGH LIVING CLOTHING in use | −1 |
| on Venus | +2 |
| on Venus with FOUL WEATHER CLOTHING in use | −2 |
| Teotihuacan maps 1 and 7 | +1 |
| carrying nothing | −1 |
| mounted on a horse | −2 |
| overloaded | +(carried − body weight) ÷ 20 |
| out of food | +1 |

- While resting, fatigue falls by 1 per day.
- Venus corrodes every metal firearm in hand (types pistol, rifle, shotgun and machine gun).

**Food** is party-wide (`0x3B24`). Every day each character whose health is above the knock-out line eats 2.

### 5.2 Inventory entry (5 bytes at `+0x4D + slot × 5`, 21 slots)

| +off | Type | Meaning |
|---|---|---|
| +0 | u8 | Item **id** = `ITEMS.DAT` index + 1 (0 = empty). The pack stays packed; DROP shifts later entries down |
| +1 | u8 | Loaded ammunition as a **0-based** `ITEMS.DAT` index (SHOT 68, SHELL 69, GRAPESHOT 70, SHRAPNEL 71). One type per weapon |
| +2 | u16 | ROUNDS in reserve. A reload spends one and refills IN GUN |
| +4 | u8 | IN GUN, shots left before a reload (−1 per shot) |

[Confirmed]: writing `11 44 32 00 06` put a LIGHT REVOLVER (id 17) loaded with SHOT, 50 rounds and 6 in the gun into
Prof. Wells' pack. ITEMS showed it and the second PARTY page listed it. With `+0x4C = 1` and its in-use flag set,
the status panel read "WEAPON: LIGHT REVOLVER".

The **in-use flag** for character *c*, slot *s* is at `0x05FF + c × 21 + s`. USE toggles it. Lamps, clothing and
water breathers only work while it is set. DROP shifts the flags together with the entries and renumbers
`+0x4B`/`+0x4C` [Static]. Carried weight is the sum of each item's `+0x2F` in sixteenths of a pound, divided
by 16.

### 5.3 Location, clock and food

| Offset | Type | Meaning | Confidence |
|---|---|---|---|
| `0x3ACA` | char[16] | Dark-map flags for maps 1–9 of the current area (`'1'` = needs a MINERS HAT, LANTERN or ELECTRIC LAMP in use) | [Static] |
| `0x3ADA + 4m` | u16, u16 | Entry / last position (row, column) of map *m*, saved on leaving it | [Static] |
| `0x3B02` | char[15] | Area name ("LONDON") | [Confirmed] |
| `0x3B11` | u16, u16 | Planet-map return position | [Static] |
| `0x3B15` | char[15] | Planet name | [Static] |
| `0x3B24` | u16 | **FOOD** (0–32,767) | [Confirmed]: 1000 and 2000 read back on the PARTY screen |
| `0x3B26` | u32 | BIOS tick count at new game | [Static] |
| `0x3B2A` | u32 | **DAY** | [Confirmed] |
| `0x3B2E` | u32 | BIOS tick deadline for the next day (runtime only) | [Confirmed]: the only bytes to differ from `DEF.S` |
| `0x3B32` | s8 | Planet: 0 space, 1 Earth, 2 Luna, 3 Mercury, 4 Mars, 5 Venus, 6 a boarded ship (M.EXE uses 10 transiently while reloading) | [Confirmed] (1 in London) |
| `0x3B33` | s8 | Area within the planet (0 = the planet's surface map) | [Confirmed] (1 = London) |
| `0x3B34` | s8 | Map within the area (0 = the area's main map) | [Confirmed] (3 = the museum) |
| `0x3B35` | s8 | Planet to return to after leaving a boarded ship | [Static] |

Area names, index `(planet − 1) × 10 + area` at `M.EXE DS:0x00E2`:

| Planet | Areas |
|---|---|
| Earth | 1 London, 2 New York, 3 Angkor, 4 San Francisco, 5 Egypt, 6 Teotihuacan, 7 Inner Earth |
| Luna | 1 A Cave |
| Mercury | 1 Princess Christiana |
| Mars | 1 Aubochon, 2 Moab, 3 Syrtis Major, 4 Moerus Lacus, 5 Gaaryan, 6 Ausonia, 7 Boreo Syrtis |
| Venus | 1 Thetis Mountains, 2 Venusstadt, 3 Ganis Mountains |
| Ships | 1 Whisperdeath, 2 Bloodrunner, 3 Aphid, 4 Hullcutter, 5 Dauntless, 6 Hamburg, 7 Smallbird |

**Time** runs in real time. On a planet's surface map a day passes every six steps; on other maps it passes every
N BIOS ticks. Only `GAME → PAUSE` stops the clock: live, no bytes changed over eight seconds while paused
[Confirmed]. "PASSAGE:" (days of boat or zeppelin rental) and the party's facing live only in `M.EXE`'s own data
group, so they are **not saved**.

### 5.4 Position and movement

| Offset | Type | Meaning | Confidence |
|---|---|---|---|
| `0x4298` | s16 | Party **row × 2** on the current map (also the ship's row in space) | [Confirmed] |
| `0x429A` | s16 | Party **column × 2** | [Confirmed] |
| `0x429C` | u16 | Current map is lit (1) or dark with no light (0) | [Static] |
| `0x429E` / `0x42A0` | s16 | Row × 2 / column × 2 before the last step | [Confirmed] |

- A step changes the value by 2.
- A step through a door moves the party 3 squares: +6.
- A poke from column 19 to 23, followed by one step west, left the party at column 21 with the view scrolled to
  match [Confirmed].
- The trainer's click-teleport moved the party two squares west, and the move survived a save and load
  [Confirmed].
- The movement keys map to directions through `DS:0x25AF`: ↑ N, ↓ S, ← W, → E, PgUp NE, PgDn SE, Home NW, End SW.

### 5.5 NPC tables and ground objects

- **`0x3B36`**: bit *id* set means NPC *id* has been removed or killed and does not reappear. A new game pre-sets
  id 303.
- **`0x3CBC`**: one nibble per NPC id records how many of its conversation steps have been completed. Odd ids use
  the low nibble.
- **`0x3EB0`**: hit points per NPC, default 10. At 0 or below the NPC dies and drops its loot.
- **`0x0668`**: six u16 counts (planets 1–6), then 203 × 11-byte records per planet. Each record is a u16 map
  code, a 5-byte inventory entry, then u16 row and u16 column. DROP refuses once a planet holds 100 objects ("DON'T
  YOU THINK YOU HAVE LITTERED ENOUGH?").

At the start both Teotihuacan tablets lie on map 163, at (4, 4) and (4, 35).

### 5.6 Ether flyer and space combat

The ship record is 10 bytes. The player's flyer is at `0x42B1` and the current enemy at `0x42BF`, which is copied
from `S.EXE DS:0x130E + ship × 10` [Static]:

| +off | Meaning |
|---|---|
| 0 | Hull size (builder caps at 30; above 2 needs liftwood) |
| 1 | Lift: 0 hydrogen, 1 liftwood (Venus refuses liftwood) |
| 2 | Propeller: 0 Edison, 1 Armstrong, 2 Zeppelin (power ≤ 4), 3 Saurian (needs the PROPELLER item) |
| 3 | Propeller / boiler power level; speed comes from a table indexed by power × 100 ÷ hull |
| 4 | Engine size |
| 5 | Engine × 2, written only when the engine is reduced in the builder [Inferred] |
| 6 | Armour value (incoming hull damage is reduced by armour ÷ 2) |
| 7 / 8 | Top / low gun, as a 0-based `ITEMS.DAT` index of a type 10 or 11 gun (0 = none) |
| 9 | Altitude cap left after hull damage; an underflow destroys the ship [Inferred name] |

| Offset | Meaning |
|---|---|
| `0x42A2` | Owns a built flyer |
| `0x42A3` / `0x42A4` | AMMONIA / GLOW CRYSTAL fuel loaded (the ether port consumes the item; loading needs story bit 3 or 4) |
| `0x42A5` | A space fight or boarding is in progress; `S.EXE` resumes it from `0x42A6–0x42AF` |
| `0x42BB` | u32, value of the current flyer (trade-in credit in the builder) |
| `0x42C9` / `0x42CB` / `0x42CD` | Player component-damage bits, HULL HITS, "W" damage. Repair zeroes all three and adds £100 if any damage bit is set |
| `0x42CF–0x42D4` | The same three fields for the enemy |
| `0x42D5–0x42D9` | Bridge stations → character index (−1 = empty): captain, helmsman, trimsman, top gunner, low gunner |
| `0x42DA–0x42DE` | Enemy crew alive per station |
| `0x42DF` | LINK state |

None of the flyer fields was exercised in a live flight. The trainer labels them as static findings.

### 5.7 Story flags and the map-modification log

`0x42E0` is a u16 of story flags. The code that sets and tests each bit [Static]:

| Bit | Set when | Effect |
|---|---|---|
| 0 | `S.EXE` flies into space rows 2–4, columns 116–118 (Jupiter); set together with bit 10 | When the Earth map loads, `M.EXE` turns (2, 52) into the Inner Earth entrance |
| 1 | VIEW the sarcophagus, Egypt map 158 (event 0) | Gives STONE TABLET and TUTS TREASURES |
| 2 | VIEW the Red Captain's remains, Teotihuacan map 167 (event 2) | Gives FREEMERCHANTS ID, DIARY and SCROLLS OF THE ANCIENTS |
| 3 | Talk to Thomas Edison, NPC 342 on the Whisperdeath (event 3) | Ether ports load ammonia and glow crystals |
| 4 | Kleuht Na Vriss accepts the scrolls (event 1) | Gives THE EMERALD; the Whisperdeath waits over Mars |
| 5 | Both Teotihuacan tablets dropped on their altars, map 163 (13, 17) and (13, 21) | The entrance to map 161 appears at 160 (35, 76); the inner doors of map 164 open |
| 6 | Dig at Egypt (37, 49) | Tomb stairs appear |
| 7 | Dig at Egypt map 157 (9, 9) | Burial chamber opens |
| 8 | Dig at Moab (16, 29) | The lost underground city opens |
| 9 | Dig at London (30, 61) on a day divisible by 30 | GOLD SHIELD (once) |
| 10 | The same `S.EXE` trigger as bit 0, in the same step | Marks the Europa sequence as shown |
| 11 | Dig at London map 115 (15, 11) | RUBY CHALICE (once) |

The **map-modification log** at `0x42E4` holds up to 512 entries of 6 bytes each: u16 map code, u8 row, u8 column,
u8 cell flags, u8 tile. The count is at `0x4EE4`. Every blast, dig or quest change is appended, and the whole log
is replayed onto a map each time it loads. When the log is full, the oldest entry is dropped. A few quest tiles
are instead patched directly on load from the story flags (jump table `M.EXE 1000:2AF8`).

---

## 6. `ITEMS.DAT`

The file is a u16 count (187) followed by 187 × 65-byte records [Static]:

| +off | Type | Meaning |
|---|---|---|
| +0x00 | char[40] | Name |
| +0x28 | u8 | Icon in `ITEMS.PIC` (row = icon ÷ 8, column = icon % 8) |
| +0x29 | u8 | Shop mask: 0x01 Earth, 0x02 Mercury, 0x04 Venus, 0x08 Mars |
| +0x2A | u8 | Type, one of 23 (below) |
| +0x2B | u32 | Price in pennies |
| +0x2F | u32 | Weight in sixteenths of a pound |
| +0x33 | u8 | Unknown (firearms 1–10, artillery 1–6) |
| +0x34 | u8 | Shots per load (IN GUN after a reload) |
| +0x35 | u8 | Reload delay in combat turns |
| +0x36 | u8 | Damage to an NPC's hit points |
| +0x37 | s8 | Minimum STR (to-hit penalty below it) |
| +0x38 | s8 | Wound threshold; for ammunition, the ammunition class |
| +0x39 | u16 | Range in squares |
| +0x3D | u8 | Explosive power (gunpowder 1, dynamite 2, detonite 8) |
| +0x3F | u8 | Melee parry dice; for ammunition, a damage bonus (÷ 2) |
| +0x40 | u8 | Armour: high nibble = piece, low nibble = protection [Inferred] |

The 23 types are Equipment, Tool, Travel Equipment, Explosive, Pistol, Rifle, Shotgun, Melee Weapon, Missile Weapon,
Machine Gun, Martian Artillery, European Artillery, Armor, Geological Invention, Biological Invention, Ammunition,
Jewelry, Manuscript, Talisman, Plant, Animal Hide, Identification and Animal Product.

**Numbering trap:** an inventory entry stores index + 1, but loaded ammunition, flyer guns, the reward tables and
the locked-door table store the 0-based index. Ids 91, 142 and 167–170 are empty records. The ship guns are the
last 17 records (SWEEPER to HALE ROCKET). The trainer's `ItemBook` is baked from this file, and its harness
compares every row with the shipped copy.

`89CAREER.DAT` holds 40 × 49-byte records: name, category (government … criminal), sex restriction, id at `+0x20`,
prerequisites and a 0xFF-terminated skill list. The id equals the index (0–5 Army, 6–11 Navy, 26 Inventor, 31
Detective, 35 Master Criminal, …). `89CAREER.MOD` adds user careers from id 43. The game's own spelling "SEASMAN"
is kept.

---

## 7. Text, pictures and maps

### 7.1 Text: `1889.DCT` and `?.MSG`

`1889.DCT` is a u32 size followed by 4,588 upper-case words, each terminated by `^` and the list ended by `@`. The
message files are:

| File | Contents |
|---|---|
| `0.MSG` | Europa |
| `1.MSG` | Earth |
| `2.MSG` | Luna |
| `3.MSG` | Mercury |
| `4.MSG` | Mars |
| `5.MSG` | Venus |
| `6.MSG` | the ship |
| `O.MSG` | item descriptions, indexed by `ITEMS.DAT` index |

Each message file has this layout:

```
u16 N
N × (u16 word count, u32 offset from file offset 2)
u16 word indices
```

In the game font `&` is drawn as a pound sign. All 685 messages decode with no unknown words.

### 7.2 Pictures: `*.PIC`

```
u16 height, u16 width ÷ 8, u8[16] CGA dither map, u16 unpacked size, u16 packed size, PackBits data
```

The pixel data is 4 bits per pixel, high nibble first, in the default EGA palette. `ALL.PIC`, `ALL.GND`,
`ALL.PEO`, `TITLES.PIC`, `0.PEO` and `0.PLN` are archives: a u32 offset table (−1 = empty) followed by PIC records.
The tile sheets `1.DEF`, `6.DEF` and `1–6.SPR` are 320-pixel-wide PICs of 16 × 16 tiles, 20 per row, numbered
`row × 20 + column`.

### 7.3 Maps: `A.SYS`, `B.SYS`, `0.SYS`

Maps are keyed by **map id = planet × 100 + area × 10 + map**. `A.SYS` covers Earth, Luna and Mercury; `B.SYS`
covers Mars, Venus and the ships. Both files begin with a 1,000-entry u32 offset table. The pub and inn interiors
are the shared maps 992 and 999, present in both files, and are reached as map 2 or 9 of any city. `0.SYS` holds
the 120 × 120 space chart.

```
u8 screens down (× 12 rows), u8 screens across (× 20 columns), char[24] planet name,
u16 packed length, then a 9-bit LSB-first code stream (M.EXE 1000:91E7):
  code < 0xF0          DEF tile code
  0xF0 ≤ code < 0x1E0  SPR tile code − 0xF0
  0x1E0 … 0x1FE        base-31 repeat digits: the next literal is written rep + 3 times
  0x1FF                void cell
```

All 144 distinct maps decode exactly. The trainer's C# decoder reproduces the Python reference render of London
pixel for pixel.

**Tile semantics** (`M.EXE 1000:5AF2`, `1000:5BDA`, `1000:1902`):

- **Walkable on foot:** SPR rows 7–11; DEF rows 7, 10 and 11; DEF row 8 columns 12–15; DEF row 1 columns 6 and 11.
  The squares the party walked in the museum all classify as walkable [Confirmed]. Deep water (SPR tile 57) is
  walkable only when every living character wears a water breather.
- **DEF row 7 is triggers:**
  - column 0: EXIT;
  - columns 1–10: services (1 pawn shop, 2 archaeologist, 3 pub, 4 bank, 5 ether port, 6 market, 7 weapon shop,
    8 alchemist, 9 harbor, 10 inn);
  - columns 11–19: enter map (column − 10) of the current area, or area (column − 10) from a planet map.
- **DEF row 8 columns 0–11 are doors.** On the maps listed in `DS:0x023D` the doors need an item:

| Map | Door opens with |
|---|---|
| 164 | HOBBS LOCKPICKS |
| 153 | KEY |
| 210 | THE EMERALD or PROPELLER |
| 414 | TALISMAN OF MANGLI DESH |
| 436 | GERMAN HQ PASS |
| 437 | SKRILL PASS |
| 446 and 453 | AMULET OF SELDON |
| 454 | PRIEST ROBES |
| 473 | WORM CULT KEY |
| 478 | CASTLE KEY |
| 523 | KEY |

`A.SET`/`B.SET` hold a u16 offset table indexed `planet × 10 + area`. A planet record is 0x13 bytes: the start
position and the name. An area record is 0x47 bytes: a 16-character dark/gold flag string, ten (row, column)
entry positions and the name. `A.NPC`/`B.NPC` hold a 1,000-entry u16 table by map id, then a u16 count and
140-byte records. Each record carries the name, the global NPC id at `+0x19`, row and column at `+0x1D`, type,
weapon, loot, default message and up to eight 11-byte conversation steps: sell information, want an item for
money/item/skill, sell an item, or hand one over. `A.DES`/`B.DES` are 15-byte tile descriptions for VIEW.

The **scripted dig and drop spots** in `M.EXE`'s tables (the dig table at `DS:0x3364` plus special cases):

| Map | Square | Effect |
|---|---|---|
| Egypt 150 | (37, 49) | Tomb stairs |
| Egypt 157 | (9, 9) | Burial chamber |
| Moab 420 | (16, 29) | Lost city |
| Boreo Syrtis 470 | (32, 36) | Worm Cult entrance |
| Syrtis Major 431 | (18, 23) | Lurkers' lair |
| London 115 | (15, 11) | Ruby chalice |
| London 110 | (30, 61) on a day divisible by 30 | Gold shield |
| Teotihuacan 163 | (13, 17) and (13, 21) | DROP the two tablets |
| Princess Christiana 314 | anywhere | 50% chance of AMMONIA |

---

## 8. How the trainer finds the game

`GameLocator` locates the block with no value searching:

1. **Anchor.** Sweep every committed region of at least 1 MiB (in 1 MiB windows with overlap, stepping past
   unreadable pages) for `"PARTY ACCT.\0"`. Each hit gives a candidate block at hit − 0x5ED.
2. **Validate** with `StateFormat.LooksLikeState`:
   - `PARTY ACCT.` is present and terminated;
   - the planet byte is between 0 and 10;
   - every adventurer slot is either empty, or holds 1–11 printable characters terminated inside 12 bytes plus an
     `m`/`f` sex byte;
   - at least one slot is occupied.

   Attributes and skills are deliberately not range-checked, so a maxed-out party still validates. `M.EXE`'s
   data group holds its own copy of "PARTY ACCT." at `DS:0x968`, and that copy fails validation because the bytes
   in front of it are strings.
3. **Corroborate.** Pin guest linear 0 through the BIOS data area. Then find each Turbo C data group by its banner
   at `DS:0004`, identify the program by a string unique to it, and check whether its state pointer resolves to
   the candidate:

| Program | Identity string | State pointer |
|---|---|---|
| `M.EXE` | "PARTY ACCT." at `DS:0x968` | `DS:0x5737` |
| `S.EXE` | "UNCHARTED SPACE" at `DS:0x580` | `DS:0x2DD5` |
| `CG.EXE` | "CHARACTER POOL" at `DS:0x14E3` | `DS:0x1DF5` |

   A witnessed candidate beats an unwitnessed one; otherwise the lowest address wins. An unwitnessed block is still
   accepted, because for a moment between programs nothing points at it, and the status line says so.

Live result: *"Located the game state at guest 0208:0000 in 21–46 ms (confirmed by M.EXE's state pointer; 1 valid
block(s) from 2 anchor hit(s))"* [Confirmed].

**Writes** go only to the bytes a field owns, never the whole block, because the game rewrites its clock and
position continuously. Before every write, `LiveStateTarget` re-checks `LooksLikeState` on the live block, so a
quit to DOS cannot turn an edit into a write into unrelated memory. The trainer re-reads the whole block every
400 ms, applies freezes against that snapshot, then repaints.

**Save files** are the same block, so the Save Editor reuses the same editors over a file buffer. Live, a save
edited this way (wealth 77,777, food 3,333) loaded in the game with GOLD 77777 on the status panel. The one-time
`.SAV.bak` backup does not show in the game's load list [Confirmed].

---

## 9. Verification

`test/FormatCheck` runs **415 checks** with a game folder present, 422 when `--live` finds a running DOSBox, and
fewer with no game folder. They cover:

- format constants and region adjacency;
- the reference books;
- block validation, including maxed and dead characters;
- the cache's write discipline: an unchanged edit sends nothing, and a refused write leaves the cache alone;
- every record field, inventory add/remove/equip with slot renumbering, and refills;
- save files: load errors, backup on first save, never overwriting the backup;
- the locator over a synthetic DOSBox guest:
  - the M.EXE, S.EXE and CG.EXE witnesses, and no witness between programs;
  - a pointer aimed elsewhere, and a decoy copy lower in memory;
  - M.EXE's own string with no block, and an empty guest;
  - no BIOS area, an anchor across the sweep seam, an unreadable page, StillValid after corruption, and
    cancellation;
- synthetic PIC, map, SET and NPC files, and mount-line parsing;
- the view-models over a fake host;
- the cluebook's self-containment;
- a XAML smoke test that builds the real window and walks every tab;
- against the shipped files:
  - `DEF.S` field by field against the figures the PARTY screen showed;
  - `ITEMS.DAT` row by row;
  - `89CAREER.DAT`;
  - all 144 maps, plus London's shops, NPCs and scripted spots.

Live confirmations are marked [Confirmed] above. The ones exercised through the trainer's own code, not raw pokes,
are:

- attach and locate;
- wealth → PARTY screen;
- food → PARTY page 2;
- adding a lantern → the item grid and CARRIED WEIGHT;
- click-teleport onto a floor square, and refusal on a wall;
- save-editor edit → GAME → LOAD.

---

## 10. Not established

- Character bytes `+0x31–+0x48` and block range `0x3BB3–0x3CBB` are never referenced.
- The meanings of the ship damage letters (T, S, R, L, B, E, M, W), and the fields `0x42AF` and `+5` of the ship
  record.
- Item bytes `+0x33` and `+0x3E`.
- The story bits were traced in code, but none was toggled live.
- Moving the party **between** maps is not supported. The engine loads a map when you walk into it, and nothing
  the trainer writes makes it reload one. Teleporting is confined to the current map.
- The tile numbering of the space chart (0.DEF offset by 60) is [Inferred] from the rendered result.

## Sources

- The game's own files and executables (see §1), analysed with Ghidra 12.1.2 and capstone.
- *Space: 1889* manual, reference card and official cluebook (Paragon Software, 1990), scans at mocagh.org.
- *Computer Gaming World*, March 1991 review (copy-protection behaviour).
- The CRPG Addict's *Space: 1889* posts (2015) and the RPGCodex Let's Play (walkthrough cross-checks).
