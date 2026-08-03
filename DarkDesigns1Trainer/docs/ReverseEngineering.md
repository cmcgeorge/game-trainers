# Dark Designs I: Grelminar's Staff — Reverse Engineering Notes

## 1. Game identification

| Field | Value |
|---|---|
| **Title** | Dark Designs I: Grelminar's Staff |
| **Author** | John Carmack |
| **Publisher** | Softdisk / Big Blue Disk (Fun 'N Games) |
| **Year** | 1990 |
| **Platform** | IBM PC (DOS, CGA/BGI) |
| **Language** | C (Borland/Turbo C, BGI graphics) |
| **Executable** | `DARKDES.EXE` (76,112 bytes, LZEXE 0.91 compressed) |
| **Data files** | `DDCHARS.DAT` (1,224 bytes), `DDMAP1–5.DAT` (12,648 bytes each) |
| **Graphics driver** | `CGA.BGI` (Borland Graphics Interface, CGA 2.00, Mar 21 1988) |

## 2. Executable decompression

`DARKDES.EXE` is compressed with **LZEXE 0.91**, identified by the `LZ91` signature at file offset `0x1C`. The compressed file is 76,112 bytes; the uncompressed image is 137,616 bytes with 855 relocations.

A Python 3 port of the `samrussell/unpacklzexe` decompressor was used to decompress it. One bug was encountered and fixed during the port: the back-reference copy loop used `outdata[bx + i]` instead of `outdata[bx]`, which is incorrect for overlapping LZSS copies (the source and destination windows overlap, so each byte must be read from the growing output buffer at the current source position, not a pre-computed offset).

### 2.1 Unpacked MZ header

| Field | Value |
|---|---|
| Header size | 0xD8 paragraphs (3,456 bytes) |
| Relocations | 855 |
| Initial CS:IP | 0x0000:0x67F9 |
| Initial SS:SP | 0x29DB:??? |
| Load image size | 137,616 bytes (0x219D0) |

SS (0x29DB paragraphs = 0x29DB0 bytes) is beyond the load image end (0x219D0), indicating BSS/uninitialized data allocation. The character buffer loaded from `DDCHARS.DAT` lives in this BSS region.

### 2.2 Memory model

The program appears to be a **small-model** Borland C build (CS = DS = SS = DGROUP). All global variables have a constant offset within the single data segment. The load segment moves between DOSBox sessions, so no address is hard-coded — the trainer locates the data at runtime.

## 3. String analysis

Full string extraction from the unpacked EXE revealed all game text, including:

- **Title string**: `"Dark Designs I : Grelminar's Staff"` at unpacked file offset `0x9D76` (load image offset `0x8FF6`). This is 34 bytes of plain ASCII and is unique in DOSBox guest RAM — the trainer's primary locator anchor.
- **Party display header**: `"# NAME          BODY  STATUS MAGIC CLAU"` at `0x9DD4`.
- **Attribute labels**: Strength, Dexterity, Constitution, Intelligence, Piety.
- **Class selection**: `"(F)ighter, (P)riest, or (W)izard"`.
- **Status values**: `"fine"`, `"KO"`, `"STUNED"`, `"STONE"`, `"DEAD"`.
- **Character display**: `STR:`, `DEX:`, `CON:`, `INT:`, `PIE:`, `LEVEL:`, `NEXT:`, `GOLD:`.
- **Equipment slots**: `Right hand:`, `Left hand:`, `Armor:`, `Ring:`.
- **Town menu**: `(A)dd`, `(R)emove`, `(C)reate`, `(D)elete`, `(H)eal`, `(E)quipment`, `(L)earn spells`, `(G)relminar's castle`, `(Q)uit and save`.
- **Level names**: `Top Castle Level`, `Mid Castle Level`, `Ground Level`, `Dungeon Level 1`, `Dungeon Level 2`.
- **File references**: `DDMAP1.DAT`, `DDCHARS.DAT`.
- **BGI driver**: `BGI Device Driver (CGA) 2.00 - Mar 21 1988`.
- **Complete spell lists** (8 priest + 8 wizard with gold costs).
- **Complete item list** (~40 items: weapons, armor, shields, wands, potions, rings, scrolls, keys).
- **Complete monster list** (43 monsters from Kobold to Chaos Avatar).
- **Combat strings**, win/loss messages, story text, victory screen.

## 4. DDCHARS.DAT format

The character data file is 1,224 bytes = 144-byte header + 20 × 54-byte character records.

### 4.1 Header (144 bytes = 0x90)

```
Offset  Bytes     Value (sample)    Inferred meaning
------  --------  ----------------  --------------------
0x00    1         01                Active flag (game in progress)
0x01    7         00…               Unknown / padding
0x08    2         03 00 (=3)        Unknown (party-related?)
0x0A    2         10 00 (=16)       Unknown
0x0C    2         1F 00 (=31)       Unknown
0x0E    2         00 00             Unknown
0x10    2         00 00             Unknown
0x12    20        E7 03 × 10 (=999) Unknown (max values or XP thresholds?)
0x26    2         01 00 (=1)        Unknown
0x28    2         E7 03 (=999)      Unknown
0x2A    2         00 00             Unknown
0x2C    2         E7 03 (=999)      Unknown
0x2E    2         E7 03 (=999)      Unknown
0x30    8         00…               Unknown
0x38    2         63 00 (=99)       Unknown
0x3A    6         00…               Unknown
0x40    2         02 00 (=2)        Unknown
0x42    2         00 00             Unknown
0x44    2         08 00 (=8)        Unknown
0x46    4         00…               Unknown
0x4A    2         01 00 (=1)        Unknown
0x4C    68        00…               Unknown / padding
```

The header is only partially decoded. The repeated 999 (0x03E8) values may represent maximum body/magic points or next-level experience thresholds for character slots. The header is not edited by the trainer — only the character records are.

### 4.2 Character record (54 bytes = 0x36)

Decoded from a player-created character "CHRISTOPHER" (Fighter, Level 1):

```
Offset  Type      Size  Value (sample)  Meaning                    Status
------  --------  ----  ---------------  -------------------------- -------
0x00    byte      1     01               Exists flag (1=present)    [Confirmed]
0x01    byte      1     0B (=11)         Name length                [Confirmed]
0x02    char[12]  12    CHRISTOPHER\0    Name (null-padded)         [Confirmed]
0x0E    byte      1     00               Unknown / padding          [Unknown]
0x0F    byte      1     01               Class (1=Fighter           [Confirmed]
                                        2=Priest, 3=Wizard)
0x10    byte      1     01               Level                      [Confirmed]
0x11    uint16LE  2     11 00 (=17)      Strength                   [Confirmed]
0x13    uint16LE  2     10 00 (=16)      Dexterity                  [Confirmed]
0x15    uint16LE  2     0E 00 (=14)      Constitution               [Confirmed]
0x17    uint16LE  2     0E 00 (=14)      Intelligence               [Confirmed]
0x19    uint16LE  2     0E 00 (=14)      Piety                      [Confirmed]
0x1B    uint16LE  2     01 00 (=1)       Status (1=fine?)           [Inferred]
0x1D    4         4     00…              Unknown                    [Unknown]
0x21    uint16LE  2     E8 03 (=1000)    Gold                       [Confirmed]
0x23    6         6     00…              Unknown                    [Unknown]
0x29    uint16LE  2     23 00 (=35)      Body current (HP)          [Confirmed]
0x2B    uint16LE  2     23 00 (=35)      Body max (HP max)          [Confirmed]
0x2D    uint16LE  2     64 00 (=100)     Experience                 [Confirmed]
0x2F    uint16LE  2     05 00 (=5)       Magic current (MP)         [Confirmed]
0x31    5         5     00…              Unknown (magic max?        [Unknown]
                                        spells? items?)
```

**Verification**: 20 × 54 + 144 = 1224 = file size. ✓

**Field details**:
- **Name**: ASCII, null-padded to 12 bytes. Name length byte at offset 0x01 gives the actual character count.
- **Class**: 1 = Fighter, 2 = Priest, 3 = Wizard (from the class selection string and character display).
- **Attributes**: Stored as uint16LE (not bytes), giving a range of 0–65535. The game rolls 3–18 during character creation. Values confirmed by matching against the character creation screen (STR=17, DEX=16, CON=14, INT=14, PIE=14 for the sample character).
- **Gold**: uint16LE, max 65535. The sample character has 1000 gold.
- **Body (HP)**: Current and max as separate uint16LE fields. The sample character has 35/35.
- **Experience**: uint16LE. The sample character has 100 XP.
- **Magic (MP)**: Current as uint16LE. The sample character (a Fighter) has 5 MP. Magic is restored when returning to town (resting).
- **Status**: Inferred at offset 0x1B. The game displays "fine", "KO", "STUNED", "STONE", "DEAD". The value 1 likely maps to "fine". Without a character in a non-fine state, the exact encoding is unconfirmed.

**Unknown fields**: The 4 bytes at 0x1D, 6 bytes at 0x23, and 5 bytes at 0x31 are zero in the single sample. They may contain magic max, spell knowledge bitfields, item references, or other state. They are round-tripped by the save editor without interpretation.

### 4.3 Empty slots

Slots 1–19 in the sample `DDCHARS.DAT` are all zeros (54 bytes of 0x00). An empty slot has exists flag = 0x00 and name length = 0x00.

## 5. Character creation: the rolled stat pool

Everything in this section was recovered from a **running game** (DOSBox-X), not from static
analysis, and each claim below was confirmed by observation.

### 5.1 The create screen

Town menu → `(C)reate a character`. The game rolls five values, prints them in a row, and prompts:

```
Strength    :??   14        14 18 14 12 16
Dexterity   :??
Constitution:??
Intelligence:??
Piety       :??

Arrange stats using the arrow keys and return, or hit (R) to get new rolls.
```

The five numbers on the right are the rolled pool; the number in the middle column is the value
currently being offered to the attribute the cursor is on. `R` discards the set and rolls again.
Because the player places the values freely, a rolled set is best treated as a **multiset**: any
value can end up on any attribute.

### 5.2 Locating the pool

Found by differential scan rather than by disassembly. The title-string anchor sits at guest offset
`0x11267` inside DOSBox-X's 16 MB guest-RAM allocation, so the game is in ordinary conventional
memory. Snapshotting that whole region, tapping `R`, and re-snapshotting — five times — leaves
exactly **one** location in the entire 16 MB that holds five contiguous uint16 LE values in the
attribute range *and* changes on every re-roll:

| | |
|---|---|
| Pool address (this session) | anchor + `0x2335D` |
| Layout | 5 × uint16 LE, contiguous |
| Confirmed against the screen | first snapshot read `14 18 14 12 16` — exactly the displayed row |

Neighbouring fields, from a hex dump around the pool:

```
Offset  Type      Value (sample)   Meaning                                     Status
------  --------  ---------------  ------------------------------------------  ---------
-0x02   uint16    FE 31            Unidentified (same value as +0x0A)          [Unknown]
+0x00   uint16    rolled[0]        First rolled value                          [Confirmed]
+0x02   uint16    rolled[1]                                                    [Confirmed]
+0x04   uint16    rolled[2]                                                    [Confirmed]
+0x06   uint16    rolled[3]                                                    [Confirmed]
+0x08   uint16    rolled[4]        Fifth rolled value                          [Confirmed]
+0x0A   uint16    FE 31            Unidentified (same value as -0x02)          [Unknown]
+0x0C   uint16    14               The value currently offered to the cursor's [Confirmed]
                                   attribute; re-read from the pool live
+0x0E   uint16    1 → 2            Cursor index, advanced by Return            [Inferred]
+0x16   —         —                Start of the scratch character record being [Inferred]
                                   built: its Strength lands at +0x27, i.e.
                                   record offset 0x11 — the same attribute
                                   offsets a roster record uses
```

The pool is *not* a roster record, so `RosterLocator` cannot see it; `Memory/CreationScanner.cs`
locates it separately. The offsets above are **not** hard-coded — the trainer signature-scans for
the captured values, as everywhere else in this project.

### 5.3 The dice

Measured, not guessed: 400 automated re-rolls were driven through the running game and the pool read
back after each, giving 2,000 values.

| Value | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 |
|---|---|---|---|---|---|---|---|---|---|
| Observed | 76 | 158 | 245 | 330 | 399 | 308 | 250 | 171 | 63 |
| Observed % | 3.80 | 7.90 | 12.25 | 16.50 | 19.95 | 15.40 | 12.50 | 8.55 | 3.15 |
| **2d5+8 %** | **4** | **8** | **12** | **16** | **20** | **16** | **12** | **8** | **4** |

Observed mean 13.988; range exactly 10–18 with nothing outside it. Against the hypothesis "sum of
two uniform 0–4 draws plus 10" the fit is chi-square 5.88 on 8 d.f. (*p* ≈ 0.66) — i.e. an
essentially perfect match. In Borland C terms the roll is:

```c
stat = 10 + random(5) + random(5);
```

The five positions are independent and identically distributed (per-position means 13.84–14.07 over
400 rolls), and the five-value total averaged 69.94 against a predicted 70.

`Game/RollOdds.cs` builds on this to answer "what are the odds of hitting this target on one roll?"
exactly, by enumerating the 1,287 sorted five-value combinations with multinomial weights. The
`FormatCheck` harness cross-checks that against brute force over all 59,049 ordered outcomes.

### 5.4 Writing the pool

The pool is **writable, and the game honours it**. Confirmed by writing `18 18 18 18 18` over the
array mid-screen and then arranging the character: Dexterity was assigned 18, a value that was not
in the roll the game had produced.

One display caveat, also confirmed: the row of five numbers is painted once, when the roll happens,
and is **not** repainted when the array changes underneath it — so after a write the screen keeps
showing the old numbers. The *offered* value in the middle column is re-read from the array live, so
it does reflect a write. The trainer says so in the UI rather than pretending the screen is in sync.

### 5.5 Locator strategy

`CreationScanner` matches the five captured numbers as a **multiset** — five contiguous uint16 LE
values which, once sorted, equal the captured values sorted. Matching the set rather than the
sequence means the player can type the numbers in any order. That trade is not free — the signature
now accepts every permutation of the captured values, as many as 5! = 120 byte patterns where an
exact-sequence signature accepts one, so it collides by chance correspondingly more often. It is
nonetheless far too specific to matter here: scanning the whole emulator process for a captured roll
returned **exactly one** address on every attempt tested. The view model still narrows any ambiguity
by re-rolling and keeping the candidate that actually changes, mirroring the Wasteland trainer's
roller.

## 6. Map files (DDMAP1–5.DAT)

Each map file is 12,648 bytes. The game has five levels:

1. `DDMAP1.DAT` — Top Castle Level
2. `DDMAP2.DAT` — Mid Castle Level
3. `DDMAP3.DAT` — Ground Level
4. `DDMAP4.DAT` — Dungeon Level 1
5. `DDMAP5.DAT` — Dungeon Level 2

The first ~1024 bytes of each file are zeros, followed by small values (0–6) representing tile types. The exact map dimensions and tile encoding are not fully decoded. Factor: 12,648 = 2³ × 3 × 17 × 31.

The map files are not edited by the trainer.

## 7. Locator strategy

The trainer uses a **dual-strategy locator** to find the character roster in DOSBox's emulated memory:

### 7.1 Primary: string-anchored scan

The 34-byte title string `"Dark Designs I : Grelminar's Staff"` lives in the game's code/data segment as plain ASCII. It is unique in the emulated 16 MB of DOSBox guest RAM. Finding it pins a known offset within the program's data segment.

The character buffer (loaded from `DDCHARS.DAT`) is in BSS, which is allocated contiguously after the loaded image. The trainer searches a 256 KB window forward from the anchor string for the 20-record character pattern. This is fast (~50 ms) and reliable.

### 7.2 Fallback: structural scan

If the anchor is not found (e.g., a different build or the string is at an unexpected offset), the trainer falls back to scanning all readable memory for a contiguous block of 54-byte records matching the character pattern:

- Each record is either a **valid character** (exists flag = 1, name length 1–12, ASCII name, class 1–3, level ≥ 1, five attributes in 3–18+) or an **empty slot** (all zeros).
- Occupied slots pack from slot 0.
- At least one slot must be occupied.

This is slower (~2 s for 16 MB) but build-independent.

## 8. Limitations and unfinished work

- **Header**: The 144-byte DDCHARS.DAT header is only partially decoded. The party composition, dungeon level, and position fields could not be confirmed from a single sample.
- **Map format**: The DDMAP files' tile encoding and dimensions are not fully decoded.
- **Status field**: The exact byte values for KO/STUNED/STONE/DEAD are inferred from the game's display strings but not confirmed against a character in those states.
- **Spell knowledge**: The bytes at record offset 0x32–0x35 may contain spell bitfields but are zero in the only sample (a Fighter with no spells).
- **Magic max**: No magic max field was identified; magic appears to be restored to a calculated maximum on rest rather than stored.
- **Live testing**: The *record* field offsets (section 4) are still derived from static analysis of the unpacked EXE and a sample `DDCHARS.DAT`; they have not been write-tested against a running game. The *creation pool* (section 5) is the exception — it was located, sampled and write-tested live, and the write was confirmed to reach the created character.
- **Creation pool neighbours**: The uint16 that brackets the pool at `-0x02` and `+0x0A` (`0x31FE` in both places) is unidentified, and the cursor semantics at `+0x0E` are only partly worked out — Return advances it once and thereafter appears to swap the offered value with the attribute under the cursor. Neither is needed by the trainer, which only reads and writes the five rolled values.
