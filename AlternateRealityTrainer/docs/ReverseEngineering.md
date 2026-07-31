# Alternate Reality: The City — Reverse-Engineering Notes

**Target:** the IBM PC / MS-DOS conversion of *Alternate Reality: The City*
(Datasoft / Intellicreations, © 1987, 1988 — original concept and program by Philip Price;
PC conversion programmed by Sheng-Chung Liu, Richard Mirsky and Jim Ratcliff; graphic art by
Steve Hofmann — all credits taken verbatim from the title-sequence strings inside `CITY.EXE`).

**Files examined:** `AR.EXE`, `CITY.EXE`, `ARCNAME`, `ARCCD00`–`ARCCD02`, `ARCSP00`–`ARCSP02`,
`ARFONT`, `ARFONTD`, `SIGNX`, `EGA\*.EGA`.

**Tools:** Ghidra 12.1.2 (headless auto-analysis + a scripted string/function/segment dump),
raw byte analysis of the shipped character files, and — decisively — **a live DOSBox-X session**
with read/write process-memory probing of the running game.

Every offset in this document that is marked **[Confirmed]** was verified *twice*: once by matching
the value against the game's own on-screen display, and once against the display template that
`CITY.EXE` itself carries in its data segment (the game stores its status bar as a byte-coded
template whose operands are the literal `DGROUP` addresses of the variables it prints — so the
program tells you where its own fields live). Anything not verified that way is marked
**[Inferred]** and should be treated as a hypothesis.

---

## 1. Executable layout

### 1.1 `AR.EXE` — the launcher (16,652 bytes)

`AR.EXE` is not the game. It is a small Microsoft C program (`C Library - (C)Copyright Microsoft
Corp 1986` at file offset `0x3818`) that:

1. prints the Datasoft / Intellicreations copyright screen (`V 1.0`),
2. asks `1. Color Graphics` / `2. Enhanced Color Graphics` / `Q. Exit`,
3. asks `Do you use a joystick (y/n)?`,
4. rejects EGA-on-CGA (`You can't run the enhanced color graphics with CGA adapter.`), and
5. `exec`s one of the five scenario executables whose names it carries as lower-case stems at
   `0x3846`: `city`, `dungeon`, `arena`, `wilder`, `palace`.

Only `CITY.EXE` ships in this directory; the other four are the unreleased/most-part-unreleased
sequels (`ARENA.EXE`, `WILDER.EXE`, `PALACE.EXE`, `DUNGEON.EXE` are named in `CITY.EXE` too, at
`DGROUP:0x8B2A`, so The City knows how to hand a character over to them).

You can skip the launcher entirely — `CITY.EXE` takes the display mode as `argv[1]`:

```
CITY 0      CGA with a colour monitor
CITY 1      EGA with a colour monitor
```

(that text is the game's own usage message, `DGROUP:0x…` in segment `29dd`, printed when the
argument is missing; a monochrome adapter is refused outright).

### 1.2 `CITY.EXE` — the game (332,160 bytes)

Plain, **unpacked**, relocatable MZ image — no EXEPACK, no PKLITE, no DOS extender. Header:

| Field | Value | Meaning |
| --- | --- | --- |
| `e_cblp` / `e_cp` | `0x0170` / `0x0289` | 649 pages, last page 368 bytes |
| `e_crlc` | `0x0265` | **613** relocation entries |
| `e_cparhdr` | `0x00A0` | 160 paragraphs → header is 2,560 (`0xA00`) bytes |
| `e_lfarlc` | `0x001C` | relocation table starts immediately after the header fields |
| `e_ss:e_sp` | `503E:0384` | stack near the top of the image |
| `e_cs:e_ip` | `0000:0000` | entry point is the very first byte of the load image |

So the load image is `332,160 − 2,560 = 329,600` bytes and **file offset = linear address −
loadBase + 0xA00**.

Ghidra's MZ loader places the image at segment `0x1000` and, from the relocation targets, splits it
into 18 executable segments plus data:

```
CODE_0   1000:0000..d68f   54,928     CODE_9   25ac:0000..167f    5,760
CODE_1   1d69:0000..0431    1,074     CODE_10  2714:0000..2c8f   11,408
CODE_2   1dac:0002..1a2f    6,702     CODE_11  29dd:0000..fdcf   64,976   ← graphics/file names
CODE_3   1f4f:0000..122f    4,656     CODE_12  39ba:0000..35ff   13,824
CODE_4   2072:0000..170f    5,904     CODE_13  3d1a:0000..e24f   57,936   ← DGROUP (see below)
CODE_5   21e3:0000..1c7f    7,296     CODE_14  4b3f:0000..b2df   45,792
CODE_6   23ab:0000..10e4    4,325     CODE_15  566d:0000..084f    2,128
CODE_7   24b9:0005..053f    1,339     CODE_16  56f2:0000..0d6f    3,440
CODE_8   250d:0000..09ef    2,544     CODE_17  57c9:0000..8adf   35,552   ← song/credits vectors
```

The stack lives at `1000+0x503E = 603E:0000`, which is exactly where the `STACK STACK STACK …`
filler string sits (`57c9:8750`) — a useful sanity check that the segment maths is right.

Ghidra's auto-analysis only recovers ~138 functions from the 330 KB image. That is expected for a
1987 large-model 16-bit program with no symbols: the vast majority of control flow is far calls
through relocated pointers that the segmented-x86 analyser cannot follow. **The disassembly was
therefore not the productive route.** Everything below came from the *data*.

---

## 2. `DGROUP` — the key that unlocks everything

Ghidra's segment `3d1a` is the program's C data segment (`DGROUP`). Two independent proofs:

1. The item names stored inside a saved character are **`DGROUP` string offsets**. `ARCCD00`
   contains the word `0x35BE` at record offset `0x17E`; `3d1a:35be` is the literal `Fine`, and the
   item name rendered right after it in the same record reads `Fine⟨tab⟩Silver⟨tab⟩Robe`.
2. In a live session, scanning DOSBox for the literal `Magical⟨tab⟩Flamesword` (`3d1a:c8df`)
   and subtracting `0xC8DF` gives exactly the same base as scanning for the character-roster name
   table and subtracting `0x118C` — two unrelated anchors, one answer.

**`DGROUP` byte *n* is at file offset `0x2DBA0 + n` in `CITY.EXE`.**

### 2.1 The text engine (why the data segment is so informative)

The game has no `printf`. Every message is a byte-coded template terminated by `0xFF`. Literal text
is stored as plain ASCII **with `0x09` (tab) used for the space character** — which is why a naive
`strings` pass over `CITY.EXE` produces `Very⟨tab⟩Thirsty` and `Battle⟨tab⟩Hammer`. The control
bytes recovered so far:

| Byte | Operands | Meaning |
| --- | --- | --- |
| `0xFF` | – | end of message |
| `0x0D` | – | new line |
| `0x09` | – | space |
| `0xA6` | `col`, `row` | position the cursor |
| `0xA5`, `0xA8`, `0x90 nn`, `0x84`, `0x8x` | – | colour / window / panel selection |
| `0xB0` | `word ptr`, `width` | print the **32-bit** variable at `DGROUP:ptr` |
| `0xB1` | `word ptr`, `width` | print the **16-bit** variable at `DGROUP:ptr` |
| `0xB2` | `word ptr`, `width` | print the **8-bit** variable at `DGROUP:ptr` |
| `0xB3` | `word ptr`, `width` | print the **NUL-terminated string** at `DGROUP:ptr` |
| `0xB4`–`0xB7` | `word ptr`, `width` | indexed / table-driven variants of the above |
| `0xB7` | – (in help text) | highlight the next character |

Because the operand of every `0xB0`–`0xB3` is a literal `DGROUP` address, **the program hands you a
symbol table for its own display variables.** Sweeping `CITY.EXE` for those opcodes and keeping the
hits whose operand falls inside the character buffer produces the field map in §4 directly out of
the binary.

### 2.2 Anchors used by the trainer

| Literal | `DGROUP` | Occurrences in a live session |
| --- | --- | --- |
| `Stats STA   CHR   STR   INT   WIS   SKL` | `0x012A` | 1 |
| `Experience⟨tab⟩` | `0x0188` | 1 |
| `Hit⟨tab⟩Points⟨tab⟩:` | `0x01AC` | 1 |
| `Magical⟨tab⟩Flamesword` | `0xC8DF` | 1 |

All four are inside the loaded program image, all four are unique, and their pairwise distances are
fixed, so a three-of-four match is a very strong `DGROUP` identification.

### 2.3 Other things `DGROUP` gives away

* **Roster name table** at `DGROUP:0x118C` — eight slots of 32 bytes, loaded verbatim from
  `ARCNAME`. Recovered from the "Restore which character?" template, which prints
  `1.` `B3 8C 11 20`, `2.` `B3 AC 11 20`, `3.` `B3 CC 11 20`, … (stride `0x20`).
* **A built-in debugger.** `DGROUP:0x0B94` holds `Welcome⟨tab⟩to⟨tab⟩the⟨tab⟩AR⟨tab⟩debugger!`,
  and `DGROUP:0x0C21` a panel that prints ` PSTACKPTR=` plus four record fields. The trigger key was
  not identified.
* **Location artwork names** at `29dd:9b56`: `GUILD`, `SMITHY`, `SHOP`, `INN`, `BANK`, `HEALERA`,
  `HEALERB`, `TAVERN`, `PORTAL`, `CITY`, plus the `CGA\` / `EGA\` directory prefixes.
* **The complete game vocabulary** — 12 armour materials × 4 pieces, 12 weapons, 44 potions, the
  monster roster, the eleven month names (`Rebirth`, `Awakening`, `Winds`, `Rains`, `Sowings`,
  `First Fruits`, `Harvest`, `Final Reaping`, `Darkness`, `Cold Winds`, `Lights`) and every tavern
  song lyric (segment `57c9`).

---

## 3. The character files

Three files make up a saved character. They are **not** compressed, encrypted or checksummed.

| File | Size | Contents |
| --- | --- | --- |
| `ARCNAME` | 256 | Eight 32-byte NUL-padded ASCII slots — the "Restore which character?" menu. |
| `ARCCD`*nn* | 12,288 (`0x3000`) | The character record. Loaded **verbatim** into `DGROUP:0x4EB1`. |
| `ARCSP`*nn* | 952 | A second block, loaded **verbatim** into `DGROUP:0x7EB2` (i.e. `ARCCD` base + `0x3001`). Holds arrays of `DGROUP` string pointers that resolve to tavern-menu and item vocabulary, so it is most likely the per-location / per-encounter state. Not decoded further; the trainer never touches it. |

`nn` is the roster slot (`00`, `01`, `02`, …), matching the `ARCNAME` slot index.

The shipped roster in this directory is `Neuro` (slot 0), `Darwin` (slot 1) and `Shadowmancer`
(slot 2). Darwin is a freshly generated character — every attribute's fractional byte is zero, level
and experience are zero — which makes him a useful "zero sample" when diffing.

**Important:** the character record in memory *is* the working copy, not a snapshot taken at save
time. Scanning a live session for any of the seven attribute cells finds **exactly one** occurrence
in the whole 222 MiB DOSBox process. Writing to it changes the game (see §5).

---

## 4. `ARCCD`*nn* / character record layout

All values little-endian. Offsets are from the start of the record; the `DGROUP` column is what the
game's own display templates name.

### 4.1 Header, clock and identity

| Offset | `DGROUP` | Type | Field | Status |
| --- | --- | --- | --- | --- |
| `0x00` | `0x4EB1` | u32 | `1` in every sample — format/version tag | [Inferred] |
| `0x04` | `0x4EB5` | u16 | shown by the built-in debugger as `PSTACKPTR` | [Inferred] |
| `0x26` | `0x4ED7` | u8 | **Minute** — ticks up and carries into `0x27` | [Confirmed] |
| `0x27` | `0x4ED8` | u8 | **Hour** — `It is ⟨hour⟩00 hours.` / `Hour ⟨hour⟩ of day ⟨day⟩` | [Confirmed] |
| `0x28` | `0x4ED9` | u8 | **Day of month** — `… of day ⟨day⟩` | [Confirmed] |
| `0x29` | `0x4EDA` | u8 | **Month** index into the eleven month names | [Inferred] |
| `0x2A` | `0x4EDB` | u16 | **Year** — `In year ⟨year⟩ since abduction.` | [Confirmed] |
| `0x4C` | `0x4EFD` | char[32] | **Name**, NUL-padded ASCII — `Hello ⟨name⟩`, printed with width 32 | [Confirmed] |

### 4.2 Attributes — seven records, stride 10, starting at `0x6E`

```
+0  value          the number the game displays and uses
+1  maximum        equal to +0 in every sample observed
+2  natural max    equal to +0 in every sample observed
+3  fraction       sub-point progress accumulator (0 in a brand-new character)   [Inferred]
+4..+9  zero in every sample
```

| Index | Offset | `DGROUP` | Attribute | Status |
| --- | --- | --- | --- | --- |
| 0 | `0x6E` | `0x4F1F` | **Strength** | [Confirmed] |
| 1 | `0x78` | `0x4F29` | **Intelligence** | [Confirmed] |
| 2 | `0x82` | `0x4F33` | **Wisdom** | [Confirmed] |
| 3 | `0x8C` | `0x4F3D` | **Skill** | [Confirmed] |
| 4 | `0x96` | `0x4F47` | **Stamina** | [Confirmed] |
| 5 | `0xA0` | `0x4F51` | **Charm** | [Confirmed] |
| 6 | `0xAA` | `0x4F5B` | **Physical Speed** | [Confirmed]¹ |

¹ Speed is at the array's seventh slot and its `DGROUP` address continues the exact `+10` stride of
the six the status bar prints; the status bar itself only has room for six columns. Speed is a
"hidden" stat in the manual, which is consistent.

**Note the storage order is not the display order.** The status bar reads
`STA CHR STR INT WIS SKL`; the record stores `STR INT WIS SKL STA CHR SPD`. Confirming this was the
first hard result of the live session: `Neuro`'s record holds `9, 12, 16, 11, 22, 17, 14` while the
screen showed `STA 22 CHR 17 STR 9 INT 12 WIS 16 SKL 11`.

### 4.3 Level, experience and hit points

| Offset | `DGROUP` | Type | Field | Status |
| --- | --- | --- | --- | --- |
| `0xC1` | `0x4F72` | u8 | **Level** — `Level :⟨n⟩` | [Confirmed] |
| `0xC2` | `0x4F73` | u32 | **Experience** — `Experience ⟨n⟩` | [Confirmed] |
| `0xC6` | `0x4F77` | u32 | **Experience needed for the next level** | [Confirmed]² |
| `0xCA` | `0x4F7B` | u32 | **Hit points (current)** — `Hit Points :⟨n⟩` | [Confirmed] |
| `0xCE` | `0x4F7F` | u32 | **Hit points (maximum)** | [Confirmed]² |

² Not printed anywhere, but proved by a live write: setting experience to 200,000 made the game
level the character from 2 to 3, add +1 to every attribute, add +4 to the value at `0xCE`, and
**recompute `0xC6` to 400,000** — i.e. exactly the behaviour of a "next level at" threshold and a
hit-point maximum. The threshold appears to double each level.

### 4.4 Money and carried goods

Every one of these came out of the game's own inventory panel template at `DGROUP:0x0400`, which
is a run of `⟨cursor⟩ ( ⟨print opcode⟩ ⟨DGROUP ptr⟩ ⟨width⟩ )` groups, and each was then read back
from a live session and matched against the screen.

| Offset | `DGROUP` | Type | Field | Status |
| --- | --- | --- | --- | --- |
| `0xD2` | `0x4F83` | u16 | **Gold** | [Confirmed] |
| `0xD4` | `0x4F85` | u16 | **Silver** | [Confirmed] |
| `0xD6` | `0x4F87` | u16 | **Copper** | [Confirmed] |
| `0xD8` | `0x4F89` | u16 | **Precious Gems** | [Confirmed] |
| `0xDA` | `0x4F8B` | u16 | **Jewelry** | [Confirmed] |
| `0xDE` | `0x4F8F` | u8 | **Food Packets** | [Confirmed] |
| `0xDF` | `0x4F90` | u8 | **Water Flasks** | [Confirmed] |
| `0xE0` | `0x4F91` | u8 | **Crystals** | [Confirmed] |
| `0xE1` | `0x4F92` | u8 | **Keys** | [Confirmed] |
| `0xE2` | `0x4F93` | u8 | **Compass** (0/1) | [Confirmed] |
| `0xE3` | `0x4F94` | u8 | **Watch** (0/1) | [Confirmed] |

The confirmation was clean: `Neuro`'s live record read `03` at `0xDE`, `04` at `0xDF` and `01` at
`0xE2` while the screen showed `Food Packets 3`, `Water Flasks 4` and a compass rose drawn in the
bottom-left panel.

### 4.5 Map position — looked for, not found

The character's position on the 64 × 64 grid was **not** identified. This is a negative result worth
recording so nobody repeats the search blind — but read the scope limit at the end of this section
before treating it as settled, because the first round of searching was narrower than it looked.

What was ruled out, all with the game running:

* **Not a coordinate pair anywhere in the differenced window.** Five snapshots one step apart were
  differenced across a 384 KB window centred on `DGROUP`; no byte or 16-bit field in it moved by ±1
  per step while staying inside 1..64. The only field that moved on *every* step was the clock minute
  at `0x26` — one game minute per square walked.
* **Not a pointer into the map either.** Every 16-bit field stepping by 0/±1/±64 (the deltas a cell
  pointer would show) was tested against the recovered map (§4.6) for a constant base that put every
  snapshot on a walkable square. Only the clock and one oscillating graphics word survived, neither
  of which is a position.
* **Not `0x30`–`0x45` in the character record.** Those two mirrored five-word blocks *do* move as you
  walk, in the right numeric range, which makes them the obvious suspect — but writing a new
  coordinate pair into them (and their mirror) does not move the character, and the game rewrites
  them within seconds.
* A twenty-square walk was also differenced whole-image, constrained to pairs of bytes that both
  land on walkable squares. It produced 148 coincidences and no clear candidate.

**Scope limit — the search was not exhaustive.** Every difference above was taken over a 384 KB
window around `DGROUP`. The emulated machine's RAM is one committed **16 MB** region (`DGROUP` sits
at `+0x35400` inside it), so a position variable in the program's far heap was never in the search
space at all. A later round did difference the whole 16 MB, with a stronger filter — step one square
out, step back, and keep only bytes that changed on the way out *and* returned on the way home. That
run produced 2,996 changed bytes of which 2,927 returned, overwhelmingly the first-person view
bitmap at `DGROUP−0xB4xx` downwards, which of course redraws identically when you return to a square.
Nothing coordinate-shaped survived it: no byte moving ±1 within 1..64, no 16-bit value in 0..4095
moving ±1 or ±64, and no pointer into either map plane (§4.6, §4.7). A longer straight walk that
would have exposed a constant per-step delta could not be completed — the start position outside the
inn on North Main Street draws an encounter every few steps, and an encounter blocks both walking and
turning. So the whole-RAM search is **started, not finished**.

The likely explanation remains that the renderer holds the current square as a **far pointer** in a
register or on the stack rather than as a named global, which is exactly the kind of thing this
program's undisassemblable control flow (§1.2) hides. Anyone picking this up should either walk a
long line somewhere quiet and re-run the constant-delta filter over the full 16 MB, or start from the
map-access code: the planes at §4.6 and §4.7 each sit on a paragraph boundary (guest `0x2E250` and
`0x2F250`, so segments `0x2E25` and `0x2F25` at offset 0), so the instruction that indexes them loads
a square number in `0..4095` — find that, and the position is whatever feeds it.

Because of this the trainer offers **no teleport and no "you are here" marker** — an unverified
write to a guessed address is worse than the feature's absence.

### 4.6 The city street map — 64 × 64, one byte per square

Found by scoring every 4,096-byte window in `CITY.EXE` against the 60 building squares whose
coordinates the shipped `alternate.txt` lists. The correct window scored **92 out of a possible 95** (the score rewarded each building *type* resolving to its own code as well as each square being marked, so it can exceed the 60 squares; the run-time self-check `CityTerrain` applies is the plain square count, which this map passes at 57 of 60);
the runner-up scored 59. It is at **file offset `0x279F0`**, which loads verbatim to
**`DGROUP − 0x61B0`** and is never modified while the game runs.

Row 0 of the array is north 64 and column 0 is east 1, so `index = (64 − north) × 64 + (east − 1)`
and the array reads exactly like a map with north at the top.

Each byte carries a location type in the low nibble and solidity flags in the high bits:

| Low nibble | Meaning | Cells | Cross-check |
| --- | --- | --- | --- |
| 0 | nothing (plain street) | — | |
| 1 | **Inn** | 18 | all 8 known inn squares |
| 2 | **Tavern** | 24 | all 14 known tavern squares |
| 3 | **Bank** | 3 | all 3 known banks — exactly 3 cells in the whole city |
| 4 | **Shop** | 43 | 16 of 17 known shop squares |
| 5 | **Smithy** | 6 | 3 of 4 known smithies |
| 6 | open scenery — the ground you can see past the streets | 313 | [Inferred] |
| 7 | **Healer** | 18 | both known healers |
| 8 | **Guild** | 38 | 11 of 12 known guilds |

| Flag | Meaning | Cells |
| --- | --- | --- |
| `0x40` | building block — solid | 1,343 |
| `0x20` | wall, including the ring around the whole city | 502 |
| `0x60` | both | 113 |
| none | walkable street | 1,675 |

Rendered as text the result is unmistakably a city: a closed boundary ring, a street maze, building
blocks between the streets, two large open areas to the north-west and east, and — the detail that
settles it — a 3 × 3 healer block centred exactly on 30N 30E, matching the hint file.

The trainer reads this from the attached game (or from the player's own `CITY.EXE`) and never ships
it, because it is the game's copyrighted data. It refuses any candidate block that does not explain
at least 80 % of the known building squares, so a bad read can never be drawn as if it were the
city.

### 4.7 The location-name plane — a second 64 × 64 map [Confirmed]

The street map (§4.6) has a sibling immediately before it. Where §4.6 says what a square *is*
(street, wall, building, doorway), this one says what a square is *called*.

| | |
| --- | --- |
| File offset in `CITY.EXE` | `0x269F0` — exactly `0x1000` before the street map |
| Loaded at | **`DGROUP − 0x71B0`**, immediately before the street map at `DGROUP − 0x61B0` |
| Size | 4,096 bytes, one byte per square, same `index = (64 − north) × 64 + (east − 1)` layout |
| Contents | a **location ID**, 99 distinct values in `0x00`–`0x63` |

Confirmed against the running game: the 4,096 bytes at `DGROUP − 0x71B0` match the file byte for
byte, and a sweep of the emulator's whole 16 MB RAM region finds **exactly one** occurrence. Like the
street map it is loaded verbatim and never modified while the game runs.

Rendered as a picture it is unmistakably the same city as §4.6 — the same central block, the same
long vertical corridor — but partitioned into named regions rather than into solid and walkable. One
ID covers 2,059 squares, which is the generic *"a street"*.

**How the game turns an ID into the sentence on your status line.** There is a 28-entry name table at
file offset `0x22336`, each entry a text-engine template of the form
`0xB3 ⟨stem⟩ 0x0E ⟨name⟩ 0xB8 0xFF` — print the string at `DGROUP:⟨stem⟩`, then the name. The stems
are the seven sentence openings stored together at `0x21BE5` and loaded to `DGROUP:0x7416`–`0x7487`:

| Stem | Text |
| --- | --- |
| `0x7416` | `You are at the ` |
| `0x743C` | `You are in the ` |
| `0x7451` | `You are in an ` |
| `0x7465` | `You are in ` |
| `0x7476` | `You are on ` |
| `0x7487` | `You are on the ` |

So entry 0 is stem `0x7416` + `City Square.` → *"You are at the City Square."*, and entry 4 is stem
`0x7476` + `North Main Street.` → *"You are on North Main Street."*, both of which were read straight
off the screen of the running game.

The 28 names, in table order:

`City Square`, `a street`, `Royal Walkway`, `Stadium Way`, `North Main Street`, `South Main Street`,
`Southern Gate`, `Northern Gate`, `Western Gate`, `East Central Avenue`, `West Central Avenue`,
`Street of Lights`, `Stellar Maze`, `Price Commons`, `enclosed area`, `alley`, `a back alley`,
`Gold Alley`, `a cul-de-sac`, `Crimson Row`, `a secret alley`, `a side street`, `Alcove`,
`a secret passage`, `West Xebec Lane`, `East Xebec Lane`, `Griffin Road`, `City Wall`.

**What is still missing.** The plane maps square → name, which is the direction the game needs and
the opposite of the one a trainer needs. The mapping from the 99 location IDs to these 28 name
entries has not been decoded, and neither has the player's own square (§4.5) — so this gives the map
a name for every square, not a way to say which square you are standing on.

### 4.8 Regions deliberately left undecoded

* `0x30`–`0x45` — two parallel five-word blocks that the game rewrites every tick and that revert
  any write within seconds. They track hunger/thirst/fatigue meters (the `Famished` / `Thirsty` /
  `Weary` banners appear as they drain). **Do not write here.**
* `0x100`–`0x1C0` — worn/carried item records: a status word, `DGROUP` string pointers for the
  adjective/colour/material of each garment, then the rendered name as ASCII (e.g.
  `Fine⟨tab⟩Silver⟨tab⟩Robe`, `Simple⟨tab⟩Striped⟨tab⟩Dragonskin⟨tab⟩Robe`).
  Bytes `0x160`–`0x171` are identical in all three shipped characters and look like a fixed
  slot-type table.
* `0x1C0`–`0x2FFF` — zero in all three shipped characters.
* Map position — see §4.5 for the search and what it ruled out.

---

## 5. Live verification (the part that matters)

Method: DOSBox-X 0.83-class build, `machine=svga_s3`, `memsize=16`, running
`CITY.EXE 1` directly; the host process was probed with `ReadProcessMemory` /
`WriteProcessMemory` over every committed, readable, non-guard region (~222 MiB in ~480 regions).

| Experiment | Result |
| --- | --- |
| Scan for `ARCCD00`'s attribute run `09 09 09 7D 00×6 0C 0C 0C` | **exactly one hit**, and the 110 bytes before it reproduce `ARCCD00` offsets `0x00`–`0x6D` byte for byte |
| Scan for `16 16 16 AD` (Stamina + fraction) and `0E 0E 0E 50` (Speed) | one hit each — there is no second, "working" copy of the attributes |
| Scan for `Neuro` | two hits: the record at `+0x4C`, and the `ARCNAME` roster slot at `DGROUP:0x118C` |
| Scan for `Magical⟨tab⟩Flamesword` | one hit; `hit − 0xC8DF` equals `rosterHit − 0x118C` exactly → `DGROUP` base agrees from two anchors |
| Write `63 63 63` at `+0x6E` and `5A 5A 5A` at `+0x78` | status bar redrew as **STR 99, INT 90** at the next encounter |
| Write `40 0D 03 00` at `+0xC2` | **Experience 200000**, level 2 → **3**, every attribute +1, `+0xCE` +4, `+0xC6` recomputed to 400,000 |
| Write `E8 03` / `D0 07` / `60 EA` at `+0xD2` / `+0xD4` / `+0xD6` | values persisted across several minutes of play (the game never overwrote them) |
| Write to `+0x38` / `+0x42` (the `0x30`–`0x45` block) | reverted by the game within seconds — see §4.5 |

The status bar is *not* redrawn on a timer; it repaints when the game next has a reason to
(entering an encounter, changing location). An edit therefore looks like it "did nothing" for a few
seconds and then appears all at once. That is cosmetic — the value is already live.

No game file was modified during any of this: `ARCCD00`–`ARCCD02`, `ARCSP00`–`ARCSP02` and `ARCNAME`
still carry their original timestamps. The game only writes them on an explicit **S**ave.

---

## 6. Copy protection

**There is none in this build.** The original 1985 release was self-booting and disk-keyed; the
1987/88 PC conversion reads everything through ordinary DOS file I/O. `CITY.EXE` still carries the
`Insert Character Disk into drive B`, `Insert Color Graphic Disk into drive A` and
`Please start the game from drive A.` prompts from the floppy era, plus a full set of DOS critical-
error strings (`Disk is protected.`, `Sector not found.`, …), but with all assets in one directory
none of it fires. There is no manual-lookup question anywhere in the display-string table.

---

## 7. Other file formats (identified, not fully decoded)

| File | Size | Notes |
| --- | --- | --- |
| `ARFONT` / `ARFONTD` | 1,024 / 2,048 | 8×8 and 8×16 bitmap fonts (128 glyphs × 8 or 16 rows). |
| `EGA\CITY.EGA`, `EGA\PORTAL.EGA` | 32,000 each | Full-screen EGA images (320×200, 4 planes → 32,000 bytes). |
| `EGA\WALLS.EGA` | 27,360 | The first-person wall/street tile set. |
| `EGA\⟨LOCATION⟩.EGA` | 8.5–11 KB | Interior artwork: `BANK`, `GUILD`, `INN`, `SHOP`, `SMITHY`, `TAVERN`, `HEALERA`, `HEALERB`, `MOUNTAIN`, `SIGN`. |
| `EGA\⟨MON⟩1S/1M.EGA` | 30 B – 4.5 KB | Monster sprites; the `S`/`M` pair is the small/medium (distance) scale, the numeric suffix the variant. Stems map 1:1 to the monster names in `DGROUP` (`RAT`, `MOL` = Brown Mold, `SLI` = Black Slime, `SKE`, `GHO`, `ZOM`, `WRA`, `SPE`, `GRE` = Gremlin, `IMP`, `GOB`, `GNO`… plus `SPSHIPS`/`SPSHIPM`, the abduction spaceship). |
| `SIGNX` | 1,704 | Street-sign text/vector data used with `SIGN.EGA`. |

---

## 8. Reproducing this

```powershell
# 1. Static pass
& "C:\ProgramData\chocolatey\lib\ghidra\tools\ghidra_12.1.2_PUBLIC\support\analyzeHeadless.bat" `
    <projectDir> ARCity -import "<gameDir>\CITY.EXE" -deleteProject

# 2. DGROUP field map straight out of the binary — sweep for print opcodes whose
#    operand lands inside the character buffer at DGROUP:0x4EB1..0x7EB0
#    (0xB0=u32, 0xB1=u16, 0xB2=u8, 0xB3=string; DGROUP byte n is file offset 0x2DBA0+n)

# 3. Live pass
#    Run CITY.EXE 1 under DOSBox-X, resume a character, then locate the record with
#    the anchor scan in §2.2 and read/write it with Read/WriteProcessMemory.
```

The trainer in `AlternateRealityTrainer/` implements step 3 as a one-click auto-locate.

---

## 9. Sources

Reverse engineering above is first-hand. Background and cross-checks came from:

* `alternate.txt` shipped in this directory (location coordinates, potion table, hints).
* [Alternate Reality — The City cluebook](http://eobet.com/alternate-reality/docs/city_cluebook.html)
* [Alternate Reality: The City — Wikipedia](https://en.wikipedia.org/wiki/Alternate_Reality:_The_City)
* [Alternate Reality — The City, C64-Wiki](https://www.c64-wiki.com/wiki/Alternate_Reality_-_The_City)
* [Kroah's Game Reverse Engineering Page — Alternate Reality](http://bringerp.free.fr/RE/Ar/news.php5) (Atari 8-bit/ST originals, not the PC port)
