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

The program is a **multi-code-segment** Borland C build — medium or large model, not small: the
disassembly is full of `push cs` / `lcall seg:off` far calls between distinct code segments (the
character-sheet printer lives at load-image offset `0x8410`, and calls out to `0x2043:…` for the
runtime), while string literals are addressed as `CS:offset` far pointers. Data, by contrast, is a
single DGROUP: every global is a constant offset off DS, which is what makes the roster locatable by
a fixed intra-segment relationship. The load segment moves between DOSBox sessions, so no address is
hard-coded — the trainer locates the data at runtime.

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

The character data file is 1,224 bytes = 144-byte header + **15 × 72-byte** character records.

> **Correction.** An earlier pass read this as 20 × 54-byte records. Both decompositions give the
> same 1,080-byte roster, and the sample file has only one character — in slot 0 either way — so
> nothing in it distinguishes them. The disassembly does, decisively: the game multiplies a
> character index by `0x48` (72) at **~300 sites** and by 54 at **none**, and its loader reads
> `0x438` (1,080) bytes in one call into the record-1 slot of an array whose stride is 72. Every
> field below is likewise taken from code that reads or writes it, not inferred from the sample.

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

The header is only partially decoded. The loader reads it as six separate reads — 8, 2, 2, 2, 2 and
0x80 bytes — totalling exactly 144, then reads the 1,080-byte roster in one call. The header is not
edited by the trainer — only the character records are.

### 4.2 Character record (72 bytes = 0x48)

The record is laid out as follows. The **evidence** column names the code that pins each field; the
sample column is the one player-created character in `DDCHARS.DAT` ("CHRISTOPHER", Fighter L1).

```
Offset  Type      Size  Sample     Meaning                     Evidence
------  --------  ----  ---------  --------------------------  -----------------------------
0x00    byte      1     01         Exists flag (1 = present)   cmp byte [rec+0],1 (33 sites)
0x01    byte      1     0B (=11)   Name length
0x02    char[12]  12    CHRISTOPH… Name (null-padded)
0x0E    byte      1     00         Unused — never read
0x0F    byte      1     01         Status (1=fine … 5=DEAD)    cmp byte [rec+0x0F],5 → death
0x10    byte      1     01         Class (1=FTR,2=PRI,3=WIZ)   cmp al,0/1 branch; FTR/PRI/WIZ
0x11    uint16LE  2     17         Strength                    printed after "STR:"
0x13    uint16LE  2     16         Dexterity                   printed after "DEX:"
0x15    uint16LE  2     14         Constitution                printed after "CON:"
0x17    uint16LE  2     14         Intelligence                printed after "INT:"
0x19    uint16LE  2     14         Piety                       printed after "PIE:"
0x1B    uint16LE  2     1          Level                       printed after "LEVEL:"
0x1D    uint32LE  4     0          Experience                  printed after "XP:" (as a long)
0x21    uint32LE  4     1000       Experience for next level   printed after "NEXT:" (as a long)
0x25    uint16LE  2     0          Magic current (spell pts)   rest copies 0x27 → 0x25
0x27    uint16LE  2     0          Magic max                   rest copies 0x27 → 0x25
0x29    uint16LE  2     35         Body current (HP)           heal copies 0x2B → 0x29
0x2B    uint16LE  2     35         Body max                    heal copies 0x2B → 0x29
0x2D    uint16LE  2     100        Gold                        printed after "GOLD:"
0x2F    byte      1     0          Unknown (read, not decoded)
0x30    byte      1     0          Readied: right hand         written after the "Right hand:" prompt
0x31    byte      1     0          Readied: left hand          written after the "Left hand:" prompt
0x32    byte      1     0          Unused — never read
0x33    byte      1     0          Readied: armor              written after the "Armor:" prompt
0x34    byte      1     0          Readied: ring               written after the "Ring:" prompt
0x35    9         9     00…        Unused — never read
0x3E    byte[10]  10    00…        Carried pack, slots A–J     mov al,[rec+0x3D+slot], slot 1–10
```

**Verification**: 15 × 72 + 144 = 1224 = file size. ✓

**How the display fields were pinned.** The character-sheet printer emits its labels in screen
order — `STR:`/`LEVEL:`, `DEX:`/`XP:`, `CON:`/`NEXT:`, `INT:`/`GOLD:`, `PIE:` — and pushes fields in
exactly that order: `0x11, 0x1B, 0x13, [0x1F,0x1D], 0x15, [0x23,0x21], 0x17, 0x2D, 0x19`. The two
bracketed pairs are high-word-then-low-word pushes of a 32-bit value, which is why XP and NEXT are
`uint32` while gold, pushed alone, is a `uint16`. The game's manual describes the same four
right-hand quantities ("Level", "Exp", "Next", "Gold"), in that order.

**The game's own max-character routine** (a debug/cheat path in the EXE) writes 99 to each of
`0x11`–`0x19`, 30 to `0x1B`, 99 to `0x27` and `0x2B`, `0x000F423F` (999,999) across `0x21`, and
10,000 to `0x2D`. That independently confirms level at `0x1B`, the two *max* vitals at `0x27`/`0x2B`,
NEXT as a 32-bit field at `0x21`, and gold as a 16-bit field at `0x2D`. The trainer's "max" targets
are taken from this routine.

**Superseded readings.** The sample character is a level-1 Fighter with status "fine", so `0x0F`,
`0x10` and `0x1B` all read 1 and could not be told apart from it. An earlier pass therefore had
class at `0x0F`, level at `0x10` and status at `0x1B`, and — reading gold and XP one field
too early — reported gold 1000 and XP 100. Under the corrected layout the same bytes read XP 0,
NEXT 1000 and gold 100 for a freshly created character, and Magic 0/0 for a Fighter, all of which
are what a new character should have.

### 4.3 Items

Item bytes are indices into a 40-byte-per-entry table in the data segment whose entry 0 is the
game's own `NO ITEM` placeholder — so **0 means "empty"** and valid ids run 1–63. The pack picker
enforces exactly that: it rejects anything above `0x3F` and beeps at 0.

The 40-byte entry, with offsets **relative to the start of the entry** (the same convention
`ItemBook`'s `EntryOff*` constants use):

```
Offset  Type      Meaning
------  --------  ------------------------------------------------------------------
0x00    byte      Type code — what the ready-equipment screen tests (table below)
0x01    byte      Unidentified
0x02    char[]    Name, ASCII. The padding after a short name holds build garbage, so
                  the name cannot be read by scanning to a NUL — the trainer's names
                  are curated and assert-checked as prefixes of these bytes.
0x12    uint16    Damage, or effect id for a usable item
0x14    uint16    Potency — chance out of 256 of the good outcome (§4.4)
0x18    uint16    Shield protection; 0 for everything else
0x1A    uint16    Price in gold; 0 when not sold
0x1C    uint16    Class bitmask — bit 0 Fighter, 1 Priest, 2 Wizard
```

The prices climb sensibly with the names — Dagger 5, Staff 10, Mace 15, Short Sword 20 — and the
class masks match the manual: the dagger is `101` (no priests: "their beliefs prevent them from
using pointed weapons"), the mace `011`, the wands `100`.

The type code at +0x00 is what the ready-equipment screen tests:

| Type | Meaning | Slots that accept it |
|---|---|---|
| 0 | light / off-hand (daggers, short swords, shields) | right hand, **left hand** |
| 1 | medium one-handed weapon | right hand |
| 2 | two-handed weapon | right hand |
| 3 | usable (wands, potions, scrolls, keys, the quest staff) | — |
| 4 | ring | ring |
| 5 | armor | armor |

The picker's four branches are literally `type <= 2` for the right hand, `type == 0` for the left,
`type == 5` for armor and `type == 4` for the ring; anything else prints `"Wrong type!"`. That
matches the manual ("you can have a large and small weapon, or a weapon and a shield").

Ids 41–59 (bar 54) are monster natural gear — `Hide`, `Scales`, `Claw`, `Bite` and friends — with
price 0 and class mask 0. They are perfectly valid ids to write, but the shop never sells them.
Ids 27, 28 and 36 are blank table slots. Id 63 is `THE  S T A F F`, the quest item.

One entry needs care: **`Gaze` (id 32)** is a monster attack, but unlike the rest of the monster
gear it carries a full class mask of `111`. A class mask alone therefore does not separate player
gear from monster entries — the shop **price** does, with the priceless quest staff as the single
exception. That is the rule `ItemBook.Item.IsPlayerItem` implements, giving 41 obtainable items.

### 4.4 Item charges: there aren't any

Dark Designs has **no charge counters**. Three independent lines of evidence:

- A pack slot is a single byte holding an item id (§4.2). There is no per-slot counter anywhere in
  the 72-byte record, and the bytes the record does not use (`0x0E`, `0x32`, `0x35`–`0x3D`) are
  never read by any code.
- The item screen's strings are `Use,Trd,Drop,Rdy:`, `Cannot act!`, `Use item (A-J):`,
  `No usable power!`, `Wrong class!`, `Trade item (A-J):`, `You are using it!`, `Give to (1-4):`,
  `No free spots!`, `Drop item (A-J):`. Nothing about charges, uses left, or being spent.
- The `(U)se` path settles it. After checking the item is type 3 and legal for the class, it does:

```
    push [entry+0x12]          ; the effect id
    push charIndex
    call apply_effect
    push 256
    lcall random               ; roll = random(256)
    ax = [entry+0x14]          ; potency
    cdq                        ; compared as a signed long
    if (potency > roll) goto keep
    mov byte [rec+0x3D+slot], 0    ; destroy the item
keep:
```

So an item is **destroyed on use unless a random roll goes its way**. Consumption is
probabilistic, not metered.

The word at entry `+0x14` — call it **potency** — is that chance out of 256. The same test appears
in two combat routines, where passing it fires a magic weapon's special effect instead of sparing
the item; that matches the manual's "some magic weapons will occasionally produce special effects".

| Item | Potency | Survives a use |
|---|---|---|
| Cureall Potion | 255 | 99.6% |
| Recall Scroll | 250 | 97.7% |
| Extra Healing | 245 | 95.7% |
| Healing Potion | 128 | 50.0% |
| Medusa Skull | 50 | 19.5% |
| Wand of Evil | 29 | 11.3% |
| Paralyze Wand | 10 | 3.9% |
| Keys 1–3, The Staff | 0 | never |

Magic weapons carry it as a trigger chance: Gaze 250, Trident of Pain and Active Axe 200, Old Dark
Sword 80, Holy Sword 77, Gravedigger Axe 66, Vampiric Sword / Electroblade / Medusa Skull 50,
Mangling Mace 45, Boom Blade 25. Ordinary gear is 0 and never rolls.

Because potency lives in the **item table**, not in a character, there is nothing per-item to
recharge. Setting it to 256 makes the test unconditional — usable items survive every use and magic
weapons trigger every hit — which is what `Memory/ItemTableLocator.cs` patches. That is game-wide
data, so it affects every character and is never written to `DDCHARS.DAT`. The trainer restores the
original values on detach unless "Keep on detach" is ticked, in which case the patch lasts until the
game exits.

Either way the cached table address is dropped on detach: it belongs to the process being released,
and DOSBox reuses similar addresses between runs, so keeping it would let a later re-attach write
two bytes to a stale address in a different process. The remembered *original values* are kept when
the patch is left in place — they are static game data, identical every session, so re-attaching and
unticking a toggle restores the true values rather than re-saving the patched ones.

### 4.5 Roster, party working copies, and save

The loader and saver make the memory layout explicit:

```
DS:0x0424   roster[0..15], 72 bytes each   (slot 0 scratch; the file holds slots 1–15)
DS:0x1316   partySlot[1..4], uint16 each   (which roster slot each party position holds, 0 = none)
DS:0x1360   party[1..4], 72 bytes each     (the working copies the game actually plays out of)
```

On load, for each of the four party positions the game `movmem`s 72 bytes from
`roster[partySlot[i]]` into `party[i]` (or from a blank template at `DS:0x12D0` when the position is
empty). On `(Q)uit and save` it does the reverse — `party[i]` → `roster[partySlot[i]]` — and only
then writes the file.

The consequence for a live trainer is direct: **editing only the roster is undone by the game's own
save** for any character currently in the party. `RosterLocator` therefore sweeps the surrounding
data segment for working copies of each located character, matching on the name bytes and class
(not on the `0xF3C` delta, and not on vitals, which diverge as soon as the character takes a hit),
and `CharacterViewModel` writes every copy it found alongside the roster record.

### 4.6 Empty slots

Unoccupied roster slots in the sample `DDCHARS.DAT` are all zeros. An empty slot has exists
flag = 0x00 and name length = 0x00.

### 4.7 Live confirmation

Everything above was then checked against the running game (DOSBox-X, character "CHRISTOPHER" on
the item screen), which settled the layout by observation rather than inference:

**Reading.** The locator found the roster record and decoded it as L1 Fighter, STR 17 / DEX 16 /
CON 14 / INT 14 / PIE 14, Body 35/35, Magic 0/0, XP 0/1000, Gold 100 — matching the save-file
decode field for field.

**The two-array model.** The content-matched sweep found the party working copy at exactly
`roster + 0xF3C`. That is precisely the DGROUP delta the disassembly predicts (`0x1360 - 0x424`),
reached independently by matching name and class. The party array is real, and the mirroring is
load-bearing rather than defensive.

**Writing.** Item ids 5, 8, 10 and 20 were written into pack slots A–D of both copies and read
back. Re-opening the item screen, the game printed:

```
A  LONG SWORD
B  SHIELD
C  LEATHER ARMOR
D  HEALING POTION
```

— the game's own renderer resolving those ids to the names this table claims, which confirms the
item table's base, stride and 1-based origin at once. The pack offset (`+0x3E`, slot A) is
confirmed by the same write.

**The party status line.** The same screen's roster bar reads
`CHRISTOPHER   35/35   fine   0/0   FTR`, independently confirming four more fields: body
current/max (`+0x29`/`+0x2B`), status (`+0x0F`, printing "fine" for 1), class (`+0x10`, "FTR"), and
**magic as a current/max pair** (`+0x25`/`+0x27`). That last one is decisive against the superseded
layout, which had no magic-max field and read a lone magic value of 5 at `+0x2F` — the game shows
`0/0`, as a Fighter should.

One display caveat, the same one the creation pool has (§5.4): the item list is painted when the
screen opens and is **not** repainted when the bytes change underneath it. A write only becomes
visible after leaving and re-entering the item screen.

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

The character buffer (loaded from `DDCHARS.DAT`) is in BSS, which is allocated contiguously after the loaded image. The trainer searches a 256 KB window forward from the anchor string for the 15-record character pattern. This is fast (~50 ms) and reliable.

### 7.2 Fallback: structural scan

If the anchor is not found (e.g., a different build or the string is at an unexpected offset), the trainer falls back to scanning all readable memory for a contiguous block of 72-byte records matching the character pattern:

- Each record is either a **valid character** (exists flag = 1, name length 1–12, ASCII name, status 1–5, class 1–3, five attributes in 1–999, level 1–99, body max > 0) or an **empty slot** (all zeros).
- Occupied slots pack from slot 0.
- At least one slot must be occupied.

This is slower (~2 s for 16 MB) but build-independent.

### 7.3 Party working copies

Whichever strategy found the roster, the locator then sweeps ~10 KB forward for 72-byte blocks that
carry the same name bytes and class as a located character but sit outside the roster array. Those
are the game's party working copies (§4.5). Matching on content rather than on the `0x1360 - 0x424`
delta keeps the trainer's "never hard-code an address" rule intact, and a build that laid the data
segment out differently would simply find no mirrors and fall back to roster-only edits.

## 8. Limitations and unfinished work

- **Header**: The 144-byte DDCHARS.DAT header is only partially decoded. The loader's six reads give its field boundaries (8/2/2/2/2/128) but not their meanings.
- **Map format**: The DDMAP files' tile encoding and dimensions are not fully decoded.
- **Status field**: The value 5 = DEAD is pinned by the game's own `status == 5` check, and the five status strings (" fine", "  KO", "STUNED", "STONE", " DEAD") give the ordering, but 2–4 have not been observed on a live character.
- **Spell knowledge**: No spell-knowledge field was found. The 9 unused bytes at 0x35–0x3D are the only plausible home, and no code reads them via the record base — so if spells are stored per character, it is somewhere this sweep did not reach.
- **Record offset 0x2F**: read by the game in 18 places but not identified; left alone.
- **Item name padding**: names in the item table are not reliably NUL-terminated — the bytes after a short name hold build garbage (`DAGGER` is followed by `LOADMONLIS`). The trainer's names were curated and then verified by assertion to be genuine prefixes of the table bytes rather than parsed at runtime.
- **Live testing**: done — see §4.7. What remains untested live is the *writing* of individual non-item fields (attributes, gold, level and so on); those are pinned by the disassembly and by the party display line, but only the pack bytes have been round-tripped through the game's own renderer.
- **Creation pool neighbours**: The uint16 that brackets the pool at `-0x02` and `+0x0A` (`0x31FE` in both places) is unidentified, and the cursor semantics at `+0x0E` are only partly worked out — Return advances it once and thereafter appears to swap the offered value with the attribute under the cursor. Neither is needed by the trainer, which only reads and writes the five rolled values.
