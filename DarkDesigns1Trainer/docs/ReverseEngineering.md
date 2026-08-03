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

## 5. Map files (DDMAP1–5.DAT)

Each map file is 12,648 bytes. The game has five levels:

1. `DDMAP1.DAT` — Top Castle Level
2. `DDMAP2.DAT` — Mid Castle Level
3. `DDMAP3.DAT` — Ground Level
4. `DDMAP4.DAT` — Dungeon Level 1
5. `DDMAP5.DAT` — Dungeon Level 2

The first ~1024 bytes of each file are zeros, followed by small values (0–6) representing tile types. The exact map dimensions and tile encoding are not fully decoded. Factor: 12,648 = 2³ × 3 × 17 × 31.

The map files are not edited by the trainer.

## 6. Locator strategy

The trainer uses a **dual-strategy locator** to find the character roster in DOSBox's emulated memory:

### 6.1 Primary: string-anchored scan

The 34-byte title string `"Dark Designs I : Grelminar's Staff"` lives in the game's code/data segment as plain ASCII. It is unique in the emulated 16 MB of DOSBox guest RAM. Finding it pins a known offset within the program's data segment.

The character buffer (loaded from `DDCHARS.DAT`) is in BSS, which is allocated contiguously after the loaded image. The trainer searches a 256 KB window forward from the anchor string for the 20-record character pattern. This is fast (~50 ms) and reliable.

### 6.2 Fallback: structural scan

If the anchor is not found (e.g., a different build or the string is at an unexpected offset), the trainer falls back to scanning all readable memory for a contiguous block of 54-byte records matching the character pattern:

- Each record is either a **valid character** (exists flag = 1, name length 1–12, ASCII name, class 1–3, level ≥ 1, five attributes in 3–18+) or an **empty slot** (all zeros).
- Occupied slots pack from slot 0.
- At least one slot must be occupied.

This is slower (~2 s for 16 MB) but build-independent.

## 7. Limitations and unfinished work

- **Header**: The 144-byte DDCHARS.DAT header is only partially decoded. The party composition, dungeon level, and position fields could not be confirmed from a single sample.
- **Map format**: The DDMAP files' tile encoding and dimensions are not fully decoded.
- **Status field**: The exact byte values for KO/STUNED/STONE/DEAD are inferred from the game's display strings but not confirmed against a character in those states.
- **Spell knowledge**: The bytes at record offset 0x32–0x35 may contain spell bitfields but are zero in the only sample (a Fighter with no spells).
- **Magic max**: No magic max field was identified; magic appears to be restored to a calculated maximum on rest rather than stored.
- **Live testing**: All field offsets were derived from static analysis of the unpacked EXE and a sample `DDCHARS.DAT`. No live DOSBox write-tests were performed.
