# Sword of Aragon — Reverse-Engineering Notes

**Game:** *Sword of Aragon* — Strategic Simulations, Inc. (SSI), 1989. IBM PC / MS-DOS, version 1.0.
**Sample analysed:** `C:\Temp\Scratch\Win31DOSBox\C-DRIVE\GAMES\SARAGON` (a played hard-drive install: 15 game
saves present, letters A–E, L, M, P, Q, S, T, U, W, X, Y).
**Tools:** Ghidra 12.1.2 headless (`analyzeHeadless` + a `DumpListing` post-script), purpose-written Python
decoders, and the shipped rule book (`saragon.pdf`, text extracted from its Flate streams).

Everything below is derived **statically** from the shipped files. The game was **not executed** during this
work, so anything that would need a running process to prove is labelled **Unconfirmed** and says so. Claims
labelled **Confirmed** are backed by at least two independent agreements — usually an on-disk value reproduced
exactly by arithmetic over a table found elsewhere in the binaries, or a field that matches across all 15 saves.

---

## 1. Contents of the game directory

| File(s) | Size | What it is |
|---|---|---|
| `SWORD.EXE` | 40,353 | Front end: setup screen, copy protection, New/Old game, character creation. Chains to `ARAGON.EXE`. |
| `ARAGON.EXE` | 61,313 | World map / main menu: cities, development, taxes, conscription, units, monthly turn processing. |
| `HEXWAR.EXE` | 56,177 | Tactical battle module (hex map, movement, missile fire, spells, victory levels). |
| `WINDOW.EXE` | 100,499 | Shared graphics/UI library loaded by all three (`window.exe` appears as a literal in each). |
| `BRUN30.EXE` | 70,680 | Microsoft **QuickBASIC 3.00** run-time module. |
| `LOGO.BIN`, `CITY.BIN`, `UNIT.BIN`, `FEATURE.BIN`, `MAGIC.BIN` | 5.4–8.5 K | BSAVE'd sprite/graphics blocks (title art, city icons, unit icons, hex features, spell effects). |
| `INFO`, `BATTLE`, `EVENT`, `SPECIAL`, `RANDOM`, `WEATHER`, `AFTER`, `INVASION`, `GENERIC` | 2–22 K | Token-compressed event/message scripts (§8). |
| `terrain\*.HTZ` (45 files) | 4,616 each | Tactical battle maps: 22 generic terrain templates (one per distinct name in the 32-entry terrain-code table — `Plain` appears 8 times in it, `StreamNS` twice) + 23 named battlefields. |
| `ARAGON.HS?`, `.HR?`, `.HI?`, `.HT?` | — | Game saves, one set per save letter A–Y (§6). |
| `ARAGON.BAK` | 4,646 | Backup of an `ARAGON.HS?` save (same CSV format). |
| `INSTALL.BAT`, `INSTALL.PIF` | — | Floppy → hard-disk installer. |
| `saragon.pdf` | 72,187 | The rule book. |

`INSTALL.BAT` confirms the shipped layout: `copy *.* %1` plus `mkdir %1\terrain` / `copy terrain\*.*`.

---

## 2. Engine: compiled QuickBASIC 3.0, and why the code does not decompile

**Confirmed.** `SWORD.EXE` carries its own provenance as literal text:

```
SWORD OF ARAGON version 1.0
produced using Microsoft QuickBASIC
ver 3.00 (C) Copyright 1982-1987
by Microsoft Corporation
```

All four executables link against the **BRUN30** run-time module rather than embedding it — each contains the
loader's error strings `Must link with BRUN30.LIB`, `Wrong version of runtime module`, `Cannot find BRUN30.EXE`.
The three game modules chain to one another (`Type 'SWORD' to start SWORD OF ARAGON.` appears in both
`ARAGON.EXE` and `HEXWAR.EXE`; `ARAGON.EXE` names `HEXWAR` and vice versa).

Ghidra imports all four cleanly as *Old-style DOS Executable (MZ)* with `x86:LE:16:Real Mode`, but recovers
almost no code: a full-program auto-analysis of `SWORD.EXE` produced only ~350 instructions across a 0x9BA1-byte
image, and no cross-references to any of the interesting string constants. That is expected and is a property of
the target, not of the tool:

* BRUN30-model QuickBASIC 3.0 emits a **statement stream of far calls into the run-time module**, with operands
  supplied as inline data immediately after each call site.
* BRUN30 itself is not present in the image, so the far-call targets are unresolved, the inline argument blocks
  look like code, and flow recovery stops at the first statement.

Ghidra's block map for `SWORD.EXE` is still useful because it fixes the segmentation:

```
CODE_0  1000:0000..1000:591f   (image 0x0000..0x591F)  executable
CODE_1  1592:0000..1592:013f
CODE_2  15a6:0000..15a6:38af   (image 0x5A60..0x930F)  <- string / data pool
CODE_3  1931:0000..1931:0890
```

**Consequence for this project:** the reliable route into this game is its **data**, not its code. Everything in
§3–§8 is recovered from the initialised data the compiler laid down, which turns out to contain the complete
game database in near-source form.

---

## 3. QuickBASIC string constants — descriptors and DGROUP offsets

**Confirmed.** Every string literal in a QB3 image is stored as a **4-byte descriptor immediately followed by the
text**:

```
struct QbLiteral {
    uint16 length;      // character count, excluding any terminator
    uint16 dsOffset;    // run-time DS:offset of the text bytes
    char   text[length];
}
```

Verified on `SWORD.EXE` at file offset `0x5F98`:

```
0x5F98: 1b 00 bc 8e 45 52 52 4f 52 3a 20 77 72 6f 6e 67 ...
        len=27   ptr=0x8EBC  "ERROR: wrong word--too bad!"   (27 chars)
```

Scanning every printable run and keeping those whose preceding 4 bytes decode to `(length == run length,
0x100 ≤ ptr ≤ 0xFFF0)` yields a *single dominant* file→DS delta per executable — i.e. the whole literal pool is
one contiguous, relocation-free block:

| Executable | Consistent literals | `dsOffset` = fileOffset + Δ | Sample anchor |
|---|---|---|---|
| `SWORD.EXE`  | 92 | **+0x2F20** | `"ERROR: wrong word--too bad!"` → `DS:0x8EBC` |
| `ARAGON.EXE` | 189 | **−0x3FE0** | `"*****   C I T Y   S T A T U S    *****"` → `DS:0x90F8` |
| `HEXWAR.EXE` | 118 | **−0x3AA0** | `"Cast a Spell:"` → `DS:0x9466` |

Useful `ARAGON.EXE` anchors (all Confirmed from the image):

| Literal | Length | `DS:offset` |
|---|---|---|
| `*****   C I T Y   S T A T U S    *****` | 38 | `0x90F8` |
| `Population:   ` | 14 | `0x9146` |
| `Recruit:` | 8 | `0x91AE` |
| `Wealth:` | 7 | `0x9298` |

**Why this matters.** A compiled-BASIC program keeps all its *scalar* variables in the same DGROUP as these
literals. Locating one literal in a running DOSBox process therefore fixes `DS:0000` for the whole game
(`dgroupBase = hostAddressOfLiteral − dsOffset`), and further literals at their own expected offsets validate the
hit. The trainer (`Memory/DgroupLocator.cs`) scans for the 38-byte City Status banner and requires **at least two
of the three** other literals to line up — an accepted location is thus a three-of-four match at minimum, and the
match count is reported to the user. That reduces "find the player's gold" from a whole-address-space scan to a
64 KiB window.

**Unconfirmed:** the *DGROUP offsets of the game's variables themselves* (gold, population, …). Those are
assigned by the compiler and are only recoverable from the code stream, which does not disassemble (§2). The
trainer therefore scans **inside** the located DGROUP window rather than reading a hard-coded offset.

---

## 4. Copy protection — the complete answer key

### 4.1 How it works

**Confirmed.** `SWORD.EXE` prints, at file offsets `0x5E7C`–`0x5FD8`:

```
Using the Sword of Aragon poster,
determine the name of this fortress by
matching the screen and poster icons.
From the city description area in the
Duke's Notebook enter the first word of
the summary information for that city.
First word of: ▮
ERROR: wrong word--try again
ERROR: wrong word--too bad!
```

So the challenge is: the game draws a **city/fortress icon** on screen; the poster maps icons to city names; you
look that city up in the *Notebook of the Duke of Aladda* (rule book, pp. 18–23) and type the **first word** of
one of its four summary lines. The field asked for is appended to the `First word of: ` prompt.

Two distinct failure messages exist — `try again` and `too bad!` — so at least one retry is granted before the
program gives up.

### 4.2 The answer table

**Confirmed.** The answer key is stored as plain literals in `SWORD.EXE` at file offsets `0x7250`–`0x7444`: a
header row naming the four fields, then one row per protected city, in the game's own city order.

```
0x7250   LOCATION,RESOURCES,ECONOMY,RULER          <- field names (the prompt suffix)
0x7274   NORTHWEST,LUMBER,FARMING,YOU
0x7294   NORTHWEST,RIVER,TRAPPING,GARDWELL
...
0x741F   NORTHEASTERN,BORDER,COMMERCE,LUCINIAN
```

Cross-referencing each row against the matching Notebook entry in the rule book identifies the city
unambiguously (all 13 agree on all four fields):

| # | City | LOCATION | RESOURCES | ECONOMY | RULER |
|---|---|---|---|---|---|
| 1 | Aladda | `NORTHWEST` | `LUMBER` | `FARMING` | `YOU` |
| 2 | Marinia | `NORTHWEST` | `RIVER` | `TRAPPING` | `GARDWELL` |
| 3 | Brocada | `NORTH` | `GALATION` | `FISHING` | `PETROV` |
| 4 | Sur Nova | `FOOTHILLS` | `FOREST` | `LOGGING` | `UNKNOWN` |
| 5 | Paritan | `NORTH` | `HARBOR` | `SMUGGLING` | `PITLAG` |
| 6 | Nuralia | `NORTH` | `RICH` | `AGRICULTURE` | `WILFREED` |
| 7 | Tentula | `SOUTHEAST` | `LAKE` | `FISHING` | `TANTALA` |
| 8 | Zarnix | `JUSTINID` | `MINERALS` | `UNKNOWN` | `GNARDIX` |
| 9 | Lucedia | `SOUTHEAST` | `GOOD` | `FARMING` | `COUNCIL` |
| 10 | Pudawala | `EAST` | `DALATION` | `FISHING` | `EL-IKHOM` |
| 11 | Sothold | `NORTHEAST` | `EXCELLENT` | `FARMING` | `STRUMBERG` |
| 12 | Estallah | `NORTHEAST` | `DALATION` | `COMMERCE` | `LANDRATOZ` |
| 13 | Tetrada | `NORTHEASTERN` | `BORDER` | `COMMERCE` | `LUCINIAN` |

The seven wilderness regions that also have Notebook entries — Tranavan Forest, Gernok, Xafanta, Khalikha
Plains, Char Hills, Medeval Forest, Dersh Mountains — are **not** in the table and are never asked about.

### 4.3 Answering without the poster

You do not need to identify the icon. The prompt tells you *which field* is wanted, and the candidate set for
each field is tiny — try them in turn (retries are allowed):

* **LOCATION** → `NORTHWEST`, `NORTH`, `FOOTHILLS`, `SOUTHEAST`, `JUSTINID`, `EAST`, `NORTHEAST`, `NORTHEASTERN`
* **RESOURCES** → `LUMBER`, `RIVER`, `GALATION`, `FOREST`, `HARBOR`, `RICH`, `LAKE`, `MINERALS`, `GOOD`,
  `DALATION`, `EXCELLENT`, `BORDER`
* **ECONOMY** → `FARMING`, `TRAPPING`, `FISHING`, `LOGGING`, `SMUGGLING`, `AGRICULTURE`, `UNKNOWN`, `COMMERCE`
* **RULER** → `YOU`, `GARDWELL`, `PETROV`, `UNKNOWN`, `PITLAG`, `WILFREED`, `TANTALA`, `GNARDIX`, `COUNCIL`,
  `EL-IKHOM`, `STRUMBERG`, `LANDRATOZ`, `LUCINIAN`

The trainer's **Copy Protection** tab shows the full table plus these per-field candidate lists.

### 4.4 Patching it out — assessed, not recommended

* A **code patch** (NOP-ing the failing branch) cannot be derived here: the comparison is a BRUN30 run-time
  call and the code stream does not disassemble (§2). Anyone attempting it would need a live debugger.
* A **data patch** is conceivable — the rows are length-prefixed literals, so they could in principle be
  rewritten to a single known word while preserving each row's byte length. This is **Unverified**: it depends
  on how the row is split and whether trailing fields are trimmed, and a wrong guess corrupts the literal pool.

Since the answer key above is complete and certain, neither patch is worth the risk. The trainer does not modify
any game executable.

---

## 5. The embedded game database

`SWORD.EXE` carries the entire new-game database as QuickBASIC `DATA`-statement text — near-source, complete
with the original column alignment. This is the single richest find in the whole target.

### 5.1 Unit and character types

**Confirmed** — `" name", flags1, flags2, buy, train, maint, weight, ???`. `maint` is in **tenths of a gold
piece** per figure per month (`Cavalry` = 10 → the rule book's 1.0); `weight` is negative for anything that
*carries* (a capacity) and positive for anything that is *carried*.

| Code | Name | flags1 | buy | train | maint⁄10 | weight | last |
|---|---|---|---|---|---|---|---|
| 1 | `Infntry` | `0x0001` | 4 | 2 | 3 | −30 | 800 |
| 2 | `Mtd.Inf` | `0x0002` | 8 | 3 | 5 | −25 | 1000 |
| 3 | `Cavalry` | `0x0004` | 16 | 4 | 10 | −20 | 1200 |
| 4 | `Bowmen` | `0x0008` | 12 | 4 | 6 | −35 | 1200 |
| 5 | `Ho.Bow` | `0x0010` | 20 | 5 | 8 | −25 | 1400 |
| 6 | `Warrior` | `0x0002` | 40 | 12 | 10 | −35 | 800 |
| 7 | `Knight` | `0x0004` | 80 | 16 | 20 | −30 | 1000 |
| 8 | `Ranger` | `0x2000` | 100 | 20 | 25 | −30 | 1200 |
| 9 | `Priest` | `0x4000` | 120 | 25 | 30 | −20 | 1400 |
| 10 | `Mage` | `0x8000` | 160 | 30 | 40 | −10 | 1400 |

The same codes 1–10 appear as a parallel abbreviation table (`Infantry, "Inf "` … `Mage, "Mage"`), and are the
values found in the `Type` field of every saved roster record (§6.3). Note the rule book's Appendix I prints
*different* train costs for characters (Warrior 8, Ranger 10, Priest 12, Mage 20); the executable's values
(12/20/25/30) are the ones the game actually uses — proven by the cost arithmetic in §6.4.

### 5.2 Equipment

**Confirmed** — same shape. `flags2` is a bit-mask of legal-combination / allowed-owner constraints
(**Unconfirmed** in detail); `level` is the minimum level required.

| Slot | Idx | Item | flags1 | flags2 | buy | train | maint⁄10 | weight | level |
|---|---|---|---|---|---|---|---|---|---|
| Armor | 1 | `Robe` | 0 | 0 | 2 | 0 | 0 | 1 | 0 |
| | 2 | `Leather` | 0 | `0x8000` | 8 | 0 | 2 | 2 | 0 |
| | 3 | `Chain` | 0 | `0x8000` | 20 | 1 | 5 | 3 | 0 |
| | 4 | `Mail` | `0x0020` | `0xC018` | 40 | 2 | 10 | 4 | 0 |
| | 5 | `Plate` | `0x0040` | `0xE01A` | 80 | 3 | 15 | 6 | **3** |
| Shield | 1 | `Small` | `0x0080` | `0x8000` | 2 | 0 | 0 | 1 | 0 |
| | 2 | `Large` | `0x0100` | `0xE01C` | 6 | 1 | 1 | 3 | 0 |
| | 3 | `Kite` | `0x0100` | `0xC01B` | 8 | 1 | 2 | 4 | 0 |
| Weapon | 1 | `Dagger` | 0 | 0 | 0 | 0 | 0 | 0 | 0 |
| | 2 | `Mace` | 0 | `0x8000` | 2 | 0 | 1 | 0 | 0 |
| | 3 | `Sword` | 0 | `0xC01C` | 4 | 1 | 2 | 1 | 0 |
| | 4 | `Halberd` | `0x0300` | `0xE19C` | 6 | 2 | 3 | 2 | **1** |
| | 5 | `2-Hand` | `0x0200` | `0xE19C` | 8 | 2 | 2 | 2 | **3** |
| Pole | 1 | `Spear` | `0x0400` | `0xE21C` | 2 | 1 | 3 | 1 | 0 |
| | 2 | `Pike` | `0x0700` | `0xE39E` | 4 | 2 | 4 | 4 | **4** |
| | 3 | `Lance` | `0x0100` | `0xE01B` | 10 | 2 | 6 | 2 | 0 |
| Missile | 1 | `Thrown` | `0x0400` | `0xC35C` | 3 | 1 | 3 | 1 | 0 |
| | 2 | `Javelin` | `0x0400` | `0xC47C` | 5 | 2 | 4 | 2 | 0 |
| | 3 | `Sling` | `0x0400` | `0xE47E` | 1 | 2 | 1 | 0 | 0 |
| Bow | 1 | `X-Bow` | 0 | `0xC540` | 8 | 1 | 4 | 2 | 0 |
| | 2 | `Short` | 0 | `0xC567` | 5 | 3 | 6 | 1 | 0 |
| | 3 | `Long` | 0 | `0xE5F7` | 15 | 5 | 8 | 2 | **3** |
| | 4 | `Compnd` | 0 | `0xC5E7` | 25 | 8 | 10 | 3 | **5** |
| Horse | 1 | `Light` | `0x1000` | `0x0009` | 50 | 2 | 15 | −10 | 0 |
| | 2 | `Medium` | `0x0800` | `0xC019` | 75 | 3 | 20 | −20 | 0 |
| | 3 | `Heavy` | 0 | `0xE01B` | 100 | 4 | 25 | −25 | **2** |
| Barding | 1 | `Leather` | 0 | `0x0009` | 10 | 0 | 6 | 5 | 0 |
| | 2 | `Chain` | 0 | `0xD019` | 20 | 1 | 8 | 8 | 0 |
| | 3 | `Mail` | 0 | `0xF81B` | 40 | 2 | 10 | 12 | **2** |

Slot index 0 always means "none".

### 5.3 New-game city table

**Confirmed** for name / population / map position; field-by-field mapping of the four scalar lines is in §6.2.
Each of the 20 city blocks is 11 lines: a header, three scalar lines, then seven economy lines
(`devel, cost, resrc, prod, cap`) for Agriculture, Lumber, Mining, Manufacture, Commerce, Structure,
Fortification.

| # | City | Pos (x,y) | Pop | Morale | Loyalty | Health | Tax % | City gold |
|---|---|---|---|---|---|---|---|---|
| 1 | Aladda | 6,7 | 1,500 | 75 | 52 | 85 | 30 | 150 |
| 2 | Marinia | 1,4 | 1,200 | 50 | 30 | 30 | 25 | 315 |
| 3 | Brocada | 6,1 | 2,600 | 50 | 25 | 80 | 25 | 7,150 |
| 4 | Sur Nova | 4,12 | 3,400 | 35 | 25 | 60 | 25 | 350 |
| 5 | Paritan | 10,2 | 4,450 | 55 | 50 | 45 | 25 | 5,250 |
| 6 | Nuralia | 15,2 | 3,250 | 40 | 5 | 60 | 25 | 1,200 |
| 7 | Tranavan | 10,7 | 150 | 10 | 25 | 100 | 25 | 500 |
| 8 | Gernok | 15,8 | 750 | 125 | 110 | 20 | 25 | 150 |
| 9 | Xafanta | 10,15 | 850 | 20 | 10 | 80 | 25 | 7,500 |
| 10 | Khalikha | — | 1,200 | 50 | 10 | 70 | 25 | 100 |
| 11 | Tentula | 6,21 | 5,700 | 10 | 25 | 20 | 25 | 1,240 |
| 12 | Char | 11,22 | 1,250 | 100 | 60 | 40 | 25 | 315 |
| 13 | Zarnix | 13,18 | 1,850 | 125 | 125 | 25 | 25 | 250 |
| 14 | Medeval | 15,13 | 750 | 70 | 25 | 80 | 25 | 750 |
| 15 | Dersh | 15,21 | 500 | 100 | 70 | 30 | 25 | 755 |
| 16 | Lucedia | 20,20 | 7,500 | 25 | 10 | 50 | 25 | 7,500 |
| 17 | Pudawala | 21,16 | 9,800 | 25 | 10 | 50 | 25 | 12,500 |
| 18 | Sothold | 20,11 | 16,500 | 100 | 30 | 40 | 25 | 10,500 |
| 19 | Estallah | 21,8 | 12,500 | 50 | 15 | 70 | 25 | 7,500 |
| 20 | Tetrada | 21,4 | 31,500 | 25 | 25 | 100 | 25 | 15,420 |

Khalikha's position field is `0` — the Khalikha Plains have no city hex. Populations differ slightly from the
rule book (Brocada 2,600 vs "2,000"; Tentula 5,700 vs "5,000"; Sothold 16,500 vs "15,625") — the executable is
authoritative.

The player's starting purse is a single line: **6,500 GP**, and the game opens in **month 3 (April) of 871 QJ**
with the cursor on Aladda at (6,7).

### 5.4 Starting armies

**Confirmed.** Five blocks, one per player class, each listing henchman characters as `"name", classCode`
followed by starting units as `"name", men, typeCode, armor, shield, weapon, pole, missile, bow, horse, barding`.
The class codes are exactly the §5.1 codes, and every equipment tuple matches the corresponding saved roster
record byte-for-byte (§6.3).

| Player class | Henchmen (class) | Units |
|---|---|---|
| Warrior | Roland (Ranger), Phantas (Priest), Pomar (Priest), Mekilo (Mage), Miscon (Mage) | 1st Spearmen 40, 2nd Javelins 30, 1st Bowmen 30 |
| Knight | Keth (Knight), Roush (Ranger), Palaro (Priest) | 1st Cavalry 20, 1st Mounted 15, 2nd Javelins 30, 1st Bowmen 35 |
| Ranger | Warnok (Warrior), Rhinone (Ranger), Pardek (Priest), Markes (Mage) | 1st Spearmen 30, 2nd Javelins 40, 1st Bowmen 30 |
| Priest | Krill (Knight), Ruthion (Ranger), Milenga (Mage) | 1st Spearmen 40, 2nd Javelins 30, 1st Bowmen 30 |
| Mage | Richerd (Ranger), Perth (Priest), Mandina (Mage) | 1st Spearmen 40, 2nd Javelins 30, 1st Bowmen 30 |

Default character names offered per class: Wintrick (Warrior), Kintreth (Knight), Roddeth (Ranger),
Pentrane (Priest), Millione (Mage).

### 5.5 Enemy army reference table

**Confirmed to exist and be tabular; column meanings Unconfirmed.** 29 rows of the form

```
"Infantry",    40,32,  0, 7,7, 9,1,1,    10,20,1,   10,10,0, 5,8,11,   0, &H102,&H1011
"Priest",      5,100, -5, 3,3, 16,8,0,   60,40,3,   0,0,0,   0,0,0,    5, &H90A,&H1700
"Aragon",      1,200,-10,-8,-8,32,50,50, 50,100,4,  50,100,2,2,5,8,    10,&HA0C,&H1B00
"Dragon",      1,400,-10,-5,-4,32,50,25, 250,150,4, 150,150,1,2,5,8,   20,&HA14,&H1C00
```

The two trailing hex words are highly regular and decode as **`(classCode<<8) | level`** and
**`(opponentId<<8) | variant`**: `&H90A` = Priest (9) at level 10; `&H1700` … `&H1C00` step with the row's
opponent. Notable entries include `Cyclops`, `Minotaur`, `HoBow`, `Aragon` and `Dragon` — the last two are the
end-game set pieces.

### 5.6 Terrain tables

**Confirmed** — `ARAGON.EXE` holds a 32-entry world-terrain name table in four lines of eight, and the names are
exactly the `terrain\*.HTZ` filenames, so a world hex's terrain code selects the tactical map loaded for a battle
fought there:

| 0–7 | `Plain` `Rough` `Hill` `Mountain` `Plateau` `Brush` `CoastN` `Plain` |
|---|---|
| 8–15 | `Brush` `BrshFrst` `Forest` `HillBrsh` `HillFrst` `Plain` `Plain` `Plain` |
| 16–23 | `StreamNS` `StreamNS` `StrmFrst` `HillStrm` `BrookNS` `BrookEW` `Plain` `Plain` |
| 24–31 | `PathNS` `PathEW` `PathStrm` `PathFrst` `HillPath` `Plain` `Plain` `Water` |

The 32 codes use only **22 distinct** names, and each of those is one `terrain\*.HTZ` file. The remaining **23**
`terrain\*.HTZ` files are named battlefields — `ALADDA`, `BROCADA`, `CHAR`, `DERSH`/`DERSH1`,
`ESTALLAH`, `GERNOK`, `LUCEDIA`, `MARINIA`, `MEDEVAL`, `NURALIA`, `PARITAN`, `PUDAWALA`/`PUD1`, `SOTHOLD`,
`SURNOVA`, `TENTULA`, `TETRADA`/`TETRADA2`, `TRANAVAN`, `XAFANTA`, `ZARNIX`/`ZARNIX1` — used when a battle is
fought at that place. 22 + 23 = the 45 files shipped.

`HEXWAR.EXE` holds the per-hex description vocabulary the **Hex** command prints:
`Water Plain Rough Hill Brush Forest Sand Town Fort City Trail Path Road Entrnch Sh.Wall Wall Block Current
Stream Brook River`, plus `Defense Miss:`, ` Hand:`, ` Elev:`.

### 5.7 Other tables recovered

* **Spell menus by class** (`HEXWAR.EXE`), two rows of six each, matching rule-book Appendix II exactly:
  * Ranger: `Grow Dry Light Withr Mud Vigor` / `Rally Xhaus Heal Fear Brdge Tower`
  * Priest: `Vigor Light Rally Xhaus Bless Heal` / `Fear Prayr Tower Quake Cure Disnt`
  * Mage: `Light Slow Confu Fear Mud Brdge` / `Haste Pyro Quake Telpt Disnt Gate`
* **Victory ladder** (`HEXWAR.EXE`): `*Total* / Conclusive / Decisive / Marginal` Victory, and the mirrored
  four Defeats.
* **Development categories** (`ARAGON.EXE`): `Agriculture, Lumber, Mining, Manufacture, Commerce,
  Structure, Fortification`.
* **Months**: `January … December`.
* **Hard limits** (from `ARAGON.EXE` error strings): `You may only have 60 different units.`,
  `You cannot Hire any more leaders.`, `You need a level to Hire more.`,
  `You must use a RATE from 0 to 80 percent.`, `Unit cannot exceed Commander Level.`
* **Score cap**: the City Status screen prints `Score:` … ` (500)`, and the endgame text says
  `out of a possible 500 points.` — **maximum score is 500**.
* **Name generator** fragments: `AshEthOldUll` + `furywardringinia` (character surnames), and the consonant/vowel
  pools `acehinrstS` / `adehilnorst`.

---

## 6. Save-file formats

A saved game is **four files** sharing one letter (A–Y, chosen by the player; `Z` is reserved as scratch):

| File | Size | Format |
|---|---|---|
| `ARAGON.HS<L>` | ~4.6–5.0 K | **Plain ASCII CSV**, CRLF-terminated, `0x1A` at EOF — the kingdom/world state. |
| `ARAGON.HR<L>` | **exactly 8,000** | 80 × 100-byte binary roster records. |
| `ARAGON.HI<L>` | 0.2–1.1 K | Plain text Chronicle of Deeds; `\|` is a line break inside an entry. |
| `ARAGON.HT<L>` | **exactly 4,616** | BSAVE'd 2,304-entry `int16` hex grid (the world map). |

### 6.1 `ARAGON.HS<L>` — kingdom state (CSV)

**Confirmed** — every one of the 15 samples splits into 286 elements on CRLF: 3 header lines + 20 city blocks ×
14 lines (280) + a 2-line trailer = 285 lines, plus a final empty element because the file's last line is also
CRLF-terminated. The parser therefore requires **at least 283** lines (header + blocks) and preserves whatever
follows verbatim.

```
line 0:  yearOffset, month, ?, ?, cursorX, cursorY
line 1:  ?, ?, ?, ?, ?, ?
line 2:  wealth, score, income, maintenance          <- the four PLAYER DATA figures
line 3.. 20 city blocks x 14 lines
line 283: ?, ?, ?
line 284: ?, ?                                       <- 0,0 in the earliest save; (x*100+y)-shaped later
```

* `yearOffset` — years since 871 QJ. `month` — **0-based** (0 = January). The shipped new-game values are
  `0,3` and the Chronicle's first entry is dated *April 871 QJ*, so 3 = April. **Confirmed.**
* `cursorX, cursorY` — world-map cursor. In the earliest save these are `6,7` = Aladda. **Confirmed.**
* `wealth / score / income / maintenance` map one-for-one onto the `PLAYER DATA` block of the City Status screen
  (`Wealth:`, `Score:`, `Income:`, `Maint:`). All four are written with decimals (`702.95`, `247.25`) —
  QuickBASIC single-precision. **Confirmed**: in the earliest save Aladda is the only player city, and its
  income (`523.2`) equals the global income exactly.
* Line 1 and the trailer are **Unconfirmed**. Line 1's last two fields are `2,2` in 12 of 15 saves.

### 6.2 City block (14 lines)

```
0:  "Name", population, income
1:  tribute?, morale, loyalty, health
2:  taxRate, cityGold, trade, ?
3:  recruits, ?, position            (position = x*100 + y; 0 = no city hex)
4:  dPopulation, dMorale, dLoyalty, dHealth      <- "changed since last month" column
5:  ?, ?, dTrade, ?
6:  store?, ?
7:  Agriculture   devel, cost, resrc, prod, cap, ?, prodThisMonth, taxTaken
8:  Lumber        (same shape)
9:  Mining
10: Manufacture
11: Commerce
12: Structure     (prod is always 0 — no direct revenue)
13: Fortification (prod is always 0)
```

**Confirmed:**

* `population`, `income`, `morale`, `loyalty`, `health`, `taxRate`, `recruits`, `position`, and the whole
  line-4 delta row. Proof: the shipped new-game values for Aladda are morale 75 / loyalty 52 / health 85 /
  population 1,500; the earliest save reads 102 / 85 / 82 / 1,501 and its line 4 reads `1,27,33,-3` — exactly the
  four differences.
* `position` = `x*100 + y` for all 20 cities, matching §5.3 and the roster's `X`/`Y` fields (§6.3).
* Each economy line's `taxTaken` = `round(prodThisMonth × taxRate/100)`, and the five revenue categories'
  `taxTaken` sum to the city's `income`. Worked example (Aladda, tax 30 %): 76 + 68 + 76 + 135 + 169 = **524**
  versus a stored income of **523.2**.
* `cityGold` is the AI city's treasury (Tetrada 15,420, Pudawala 12,500 …); it is `0` once you own the city.
* Lines 4–6 are all-zero for cities you do not own — they are the player-city "this month" columns.

**Unconfirmed:** line-1 field 0 (a per-city constant: 0 for Aladda, the wilderness regions, Paritan and Tetrada;
250–1,250 for the other human cities — consistent with vassal tribute, or with initial goods in Store), line-2
field 3, line-5 fields 0/1/3, line 6, and each economy line's `cap` and field 5.

### 6.3 `ARAGON.HR<L>` — roster (binary)

**Confirmed.** 8,000 bytes = **80 records of 100 bytes**, and the split matches the rule book's limits exactly:
**slots 0–19 are characters** (`A maximum of 20 individual characters are allowed`) and **slots 20–79 are units**
(`You may only have 60 different units.`). Occupied slots pack from the start of each range; an empty slot has a
blank name and `Type == 0`.

| Offset | Type | Field | Status |
|---|---|---|---|
| `0x00` | `char[16]` | Name, space-padded | Confirmed |
| `0x10` | MBF single | Experience points | Confirmed (shape); scale plausible |
| `0x14` | `int16` | Type / class, 1–10 (§5.1) | **Confirmed** |
| `0x16` | `int16` | Armor slot, 0–5 | **Confirmed** |
| `0x18` | `int16` | Shield slot, 0–3 | **Confirmed** |
| `0x1A` | `int16` | Weapon slot, 0–5 | **Confirmed** |
| `0x1C` | `int16` | Pole slot, 0–3 | **Confirmed** |
| `0x1E` | `int16` | Missile slot, 0–3 | **Confirmed** |
| `0x20` | `int16` | Bow slot, 0–4 | **Confirmed** |
| `0x22` | `int16` | Horse slot, 0–3 | **Confirmed** |
| `0x24` | `int16` | Barding slot, 0–3 | **Confirmed** |
| `0x26` | `int16` | 0 for every record except the player character (8 or 12) | Unconfirmed |
| `0x28` | `int16` | **Make cost** (GP), derived from equipment | **Confirmed** |
| `0x2A` | `int16` | **Train cost** (GP), derived | **Confirmed** |
| `0x2C` | `int16` | **Maintenance**, tenths GP per figure per month, derived | **Confirmed** |
| `0x2E`, `0x30` | `int16` | 0 in all 1,200 records examined | Unconfirmed |
| `0x32` | `int16` | **Level** | **Confirmed** |
| `0x34` | `int16` | Movement allowance (max) | Confirmed |
| `0x36` | `int16` | 0 in all records | Unconfirmed |
| `0x38` | `int16` | **World map X** | **Confirmed** |
| `0x3A` | `int16` | **World map Y** | **Confirmed** |
| `0x3C` | `int16` | **Men** (figures in the unit; 1 for a character) | **Confirmed** |
| `0x3E` | `int16` | Hits (whole unit) | Confirmed by behaviour |
| `0x40` | `int16` | Armour class vs Hand | Confirmed by behaviour |
| `0x42` | `int16` | Armour class vs Missile | Confirmed by behaviour |
| `0x44` | `int16` | Third armour figure | Unconfirmed |
| `0x46` | `int16` | Movement remaining (equals `0x34` in a freshly-saved month) | Confirmed |
| `0x48` | `int16` | **Stacking size points** | **Confirmed** |
| `0x4A` | `int16` | Missile shots / spell charges per figure | Unconfirmed |
| `0x4C` | `int16` | Hand damage | Confirmed by behaviour |
| `0x4E` | `int16` | Second combat/stamina figure | Unconfirmed |
| `0x50` | `int16` | **Hand special bonus** | **Confirmed** |
| `0x52` | `int16` | Morale / leader range | Unconfirmed |
| `0x54`–`0x5E` | `int16`×6 | Non-zero only for missile/spell users | Unconfirmed |
| `0x60` | `byte` | Level (redundant copy of `0x32`) | **Confirmed** |
| `0x61` | `byte` | Type (redundant copy of `0x14`) | **Confirmed** |
| `0x62`, `0x63` | `byte`×2 | Equipment-derived pair (weight-like) | Unconfirmed |

### 6.4 How the roster layout was proved

The equipment slots, the three cost fields and the size field are not guesses — they reproduce exactly.

*Equipment.* The Knight starting army in `SWORD.EXE` reads
`"1st Cavalry", 20,3, 4,3,2,3,0,0,1,1`. The matching record in `ARAGON.HRA` holds
`Type=3, 4,3,2,3,0,0,1,1` at `0x14`–`0x24`. All four of that army's units and all three of the Warrior army's
units agree, in order, on all eight slots.

*Costs.* Summing the §5.1/§5.2 `buy`, `train` and `maint` columns over each record's own equipment reproduces
`0x28`/`0x2A`/`0x2C` to the gold piece — **including the class purchase discounts**, one of which the rule book
does not describe (see §6.4a):

| Record | Equipment | Σ buy | ×discount | Stored `0x28` |
|---|---|---|---|---|
| `NetDanzr` (player Knight) | Knight + Plate + Kite + Mace + Lance + Heavy horse + Mail barding | 320 | — | **320** ✔ |
| `Keth` (Knight henchman) | Knight + Mail + Kite + Mace + Lance + Heavy + Chain barding | 260 | — | **260** ✔ |
| `Roush` (Ranger) | Ranger + Chain + Sword + Short bow + Medium horse + Chain barding | 224 | — | **224** ✔ |
| `Palaro` (Priest) | Priest + Leather + Mace + Light horse + Leather barding | 190 | — | **190** ✔ |
| `1st Cavalry` | Cavalry + Mail + Kite + Mace + Lance + Light horse + Leather barding | 136 | ×0.75 | **102** ✔ |
| `1st Mounted` | Mtd.Inf + Chain + Large + Sword + Spear + Light + Leather barding | 100 | ×0.75 | **75** ✔ |
| `2nd Javelins` | Infantry + Leather + Large + Sword + Javelin | 27 | — | **27** ✔ |
| `1st Bowmen` | Bowmen + Leather + Mace + Short bow | 27 | — | **27** ✔ |

Train and maintenance agree the same way (`NetDanzr` 28/79, `Keth` 26/72, `Roush` 29/66, `Palaro` 27/54;
`1st Cavalry` 11×0.75 = 8 and 50×0.75 = 38). The 25 % reduction lands on **both** Cavalry *and* Mounted
Infantry for a Knight player — the rule book only mentions cavalry.

*Level.* The rule book says a Knight begins at 5th level and that cavalry & mounted infantry "start at 1st level
if the player character is a knight", other types at 0. `0x32` in that same save reads 5 for the player, 1 for
`1st Cavalry` and `1st Mounted`, and 0 for `2nd Javelins` and `1st Bowmen`. Across the other campaigns it reaches
25 for a long-lived Warrior, and `0x60` always holds the same number as a byte.

*Size.* `0x48` is 2 for every foot unit, 4 for light horse, 5 for medium and 6 for heavy — exactly the rule
book's stacking values (200 points per hex).

*X/Y.* `0x38`/`0x3A` are `6,7` for every record in the earliest save (all forces still in Aladda, whose position
field is `607`), and split into `(11,22)`, `(15,8)`, `(4,12)` groups in a mid-campaign save — i.e. detached
commands.

### 6.4a The class purchase discounts, and the corpus that established them

**Confirmed.** The cost model above was not fitted to the eight Knight records alone. Running it over **every**
occupied roster record in **all 15 shipped saves** reproduces `0x28`, `0x2A`, `0x2C` and `0x48` exactly for
**623 of 623** records, spanning **16** distinct (player class, unit type) combinations:

| Player class | Unit types observed in the corpus |
|---|---|
| Knight (7) | Infantry, Bowmen, Cavalry, Mtd. Infantry, Knight, Priest, Ranger |
| Warrior (6) | Infantry, Mtd. Infantry, Cavalry, Bowmen, H. Bowmen, Warrior, Knight, Priest, Ranger, Mage |

That corpus is what fixes the discount table, including the case the rule book gets wrong:

| Player class | Discounted types | Multiplier | Evidence |
|---|---|---|---|
| Warrior (6) | Infantry **only** | ×0.50 | `Red Dragons` (plate infantry) sums to 96/8/24 and stores **48/4/12**; `1st Defenders` sums to 36/6/14 and stores **18/3/7**. A Warrior campaign's Mtd. Infantry records store their **undiscounted** totals, so the rule book's "infantry" is literal. |
| Knight (7) | Cavalry **and Mtd. Infantry** | ×0.75 | `1st Cavalry` 136 → **102**, `1st Mounted` 100 → **75**. The rule book mentions only cavalry; mounted infantry is discounted too. |
| Ranger (8) | Bowmen and H. Bowmen | ×0.75 | Stated by the rule book; **not** exercised by the corpus (no Ranger-player save is shipped), so it is carried on the rule book's authority alone and is the one discount rule that remains **Unconfirmed**. |
| any | any character (codes 6–10) | ×1.00 | `Groo`, a Warrior player's own record, sums to 176 and stores **176**; `Lightning Riders` (cavalry under a Warrior) stores its full 136. |

Fractional discounted totals **round half away from zero**: the corpus contains 11 → 8 (8.25), 50 → 38 (37.5),
37 → 28 (27.75) and 9 → 7 (6.75), which is also what QuickBASIC's `CINT` would produce for all four.

`test/FormatCheck` re-derives all of this on every run: twelve worked examples as unit checks, then the whole
623-record corpus when the shipped saves are present.

### 6.5 MBF: QuickBASIC 3.0 floats are *not* IEEE 754

**Confirmed and important.** QuickBASIC 3.0 predates Microsoft's move to IEEE, so every single-precision value
— the player's gold above all — is **Microsoft Binary Format**:

```
bytes:  [m0][m1][m2][exp]        (little-endian in file and in RAM)
value = (-1)^sign × (1 + mantissa/2^23) × 2^(exp-129)
        sign     = bit 7 of m2
        mantissa = ((m2 & 0x7F) << 16) | (m1 << 8) | m0
        exp == 0 ⇒ value is 0
```

Two practical consequences:

1. A conventional float scan will **never** find the gold value — the bit pattern is not IEEE.
2. For positive values MBF is nevertheless **monotonic when read as an unsigned little-endian 32-bit integer**
   (exponent occupies the most significant byte, mantissa the rest). So an ordinary *unknown-value Int32* scan
   narrowed by Increased/Decreased **does** work on gold, and that is how the trainer's Wealth guide is written.

### 6.6 `ARAGON.HI<L>` — Chronicle of Deeds

**Confirmed.** Plain text, `|` as an in-entry line break, entries concatenated, `\r\n\x1A` at EOF:

```
April 871 QJ: NetDanzr becomes|ruler of the small city called Aladda|after the death of the child's father.||
April 871 QJ:  NetDanzr avenges the|late Duke of Aladda by destroying the|orcs who slew him.||
```

### 6.7 `ARAGON.HT<L>` and `terrain\*.HTZ` — hex grids

**Confirmed.** Both are QuickBASIC **BSAVE** blocks:

```
offset 0: 0xFD
offset 1: uint16 segment
offset 3: uint16 offset
offset 5: uint16 length      (always 0x1200 = 4608 here)
offset 7: length bytes of payload
offset 4615: 0x1A
```

4,608 bytes = **2,304 `int16`** = a **24 × 24 hex grid with 4 `int16` per hex**, which agrees with the rule
book's "the limits of the 24 x 24 dimensions" and with the observed coordinate ranges (city x ∈ 1…21,
y ∈ 1…22; roster X/Y likewise). Each entry packs two small bytes (terrain/foliage codes in the range 0–3 in the
generic templates; the named city maps use a wider range).

`ARAGON.HT<L>` is the **world** grid: `HTA` and the scratch `HTZ` are byte-identical, saves from the same
campaign differ from each other by 6–140 bytes, and saves from different campaigns by ~374 — i.e. a
predominantly static terrain grid carrying some per-hex mutable state. `terrain\*.HTZ` are the tactical
templates; `ARAGON.HTA` matches none of them (≥2,848 differing bytes), confirming the world/tactical split.

**Unconfirmed:** which of the four `int16` per hex is terrain vs foliage vs feature vs state, and the row/column
ordering. The trainer does not read or write these files.

### 6.8 `*.BIN` — graphics blocks

**Confirmed as BSAVE blocks** with the same 7-byte header (`CITY.BIN` = seg `0x1CD6`, off `0x49DE`,
len `0x2128`). `SWORD.EXE` loads `LOGO.bin`, `CITY.bin` and `UNIT.bin` by name (and prefixes them with a drive
letter when running from floppy). The payloads are attribute-plane sprite data (repeating `ff aa ff aa 66 44`
runs with an incrementing index) — the city crests used by the copy-protection screen live in `CITY.BIN`.
Decoding them needs the drawing routine, which does not disassemble (§2), so they are **not** decoded here.

---

## 7. What varies between the 15 shipped saves

Useful as a sanity corpus for any parser:

| Save | Year/Month | Wealth | Score | Aladda pop | Campaign |
|---|---|---|---|---|---|
| A | 871-04 | 702.95 | 5 | 1,501 | `NetDanzr`, Knight — one month in |
| B | 874-08 | 26,874.26 | 160 | 32,212 | `Groo`, Warrior — 20 characters hired |
| C | 874-09 | 36,965.97 | 170 | 32,471 | `Groo` |
| D | 874-11 | 78,204.84 | 170 | 33,234 | `Groo` |
| E | 875-03 | 213,821.51 | 170 | 34,762 | `Groo` — level 25 |
| L | 872-09 | 14,947.12 | 40 | 26,110 | — |
| M | 874-06 | 47,186.12 | 160 | 31,549 | — |
| P | 873-03 | 63,796.82 | 140 | 27,581 | — |
| Q | 872-04 | 28,029.04 | 25 | 24,302 | — |
| S | 874-05 | 88,888.74 | 160 | 31,329 | — |
| T | 872-02 | 1,216.57 | 15 | 24,061 | — |
| U | 874-00 | 19,065.37 | 150 | 30,598 | — |
| W | 872-09 | 72,562.92 | 90 | 26,110 | — |
| X | 872-03 | — | — | — | — |
| Y | — | — | — | — | — |

(`W` shares `L`'s city state exactly but has a different purse and score — a save made from the same month.)

---

## 8. Token-compressed text files

**Partially decoded.** `GENERIC`, `EVENT`, `RANDOM`, `SPECIAL`, `BATTLE`, `INVASION`, `AFTER`, `WEATHER` and
`INFO` mix plain CRLF-terminated ASCII (coordinate pairs like `37,5`, `30,15`) with paragraph text in which
common letter groups are replaced by single bytes ≥ 0x80:

```
   A surg\xac\xcb immig\xdd\xf9\xa7 \xdfsul\xfe \xcb
   \x94l\x9dg\xac\xcbc\xdf\x9e\xac\xcb popul\x9f\xccn.
```
→ *"A surge in immigration results in ... population."*

The token dictionary is not stored in any of the data files, so it lives in the (non-disassembling) code of
`ARAGON.EXE`. The plain-ASCII skeleton is readable enough to see the structure — each event carries a set of
`value,threshold` pairs followed by its message — but the dictionary itself is **not recovered**. This is the
largest remaining gap and the natural next target for anyone continuing the work.

---

## 9. Summary — Confirmed vs Unconfirmed

**Confirmed**

* Engine is QuickBASIC 3.00 with the BRUN30 run-time; four chained modules plus a shared `WINDOW.EXE`.
* QB3 literal layout (`len`, `dsOffset`, text) and a single constant file→DGROUP delta per executable, with
  concrete anchor offsets for `SWORD`/`ARAGON`/`HEXWAR`.
* The complete copy-protection answer key: 13 cities × 4 fields, cross-checked against the rule book.
* The complete unit/character and equipment tables (buy/train/maint/weight/level/flags).
* The new-game city table: names, positions, populations, morale/loyalty/health, tax, city gold, and seven
  economy categories each.
* Starting armies and henchmen for all five player classes.
* `ARAGON.HS<L>`: 286-line CSV; the four global player figures; the 14-line city block including every field
  listed as Confirmed in §6.2, with the tax arithmetic reproducing stored income.
* `ARAGON.HR<L>`: 80 × 100-byte records, 20 character + 60 unit slots, with name/type/level/equipment/men/X/Y/
  size and the three derived cost fields all reproduced arithmetically.
* `ARAGON.HI<L>` chronicle format; BSAVE framing for `*.BIN`, `*.HT?` and `terrain\*.HTZ`.
* MBF single-precision encoding, and its monotonicity as an unsigned Int32 — the basis of the Wealth scan.
* Hard limits: 60 units, 20 characters, 200 stacking points per hex, 23 tactical turns, tax 0–80 %, score /500.

**Unconfirmed**

* All DGROUP offsets of *variables* (only literals are recoverable statically), hence no hard-coded live
  addresses anywhere in the trainer.
* `ARAGON.HS<L>` header line 1, the 2-line trailer, and the city fields explicitly marked in §6.2.
* Roster offsets `0x26`, `0x2E`, `0x30`, `0x36`, `0x44`, `0x4A`, `0x4E`, `0x52`, `0x54`–`0x5E`, `0x62`–`0x63`.
* The **formulas** behind the equipment-derived combat figures — armour class (`0x40`/`0x42`), hand damage
  (`0x4C`), the hand special bonus (`0x50`) and hits (`0x3E`). Their *meanings* are confirmed by behaviour, but
  nothing here reproduces them arithmetically, so the trainer never writes them.
* The **Ranger** purchase discount (§6.4a): stated by the rule book, but no Ranger-player save is shipped, so it
  is the one row of the discount table the 623-record corpus does not exercise.
* The internal layout of the 4-`int16`-per-hex map entries, and the `*.BIN` sprite encoding.
* The token dictionary for the compressed text files.
* Anything about live process behaviour: the game was never run during this work. The trainer's live tab is
  written to *derive* addresses at run time and to validate before writing, precisely because of that.

---

## 10. What the trainer uses

| Feature | Backed by |
|---|---|
| Kingdom save editor (wealth, score, date, per-city figures, development) | §6.1, §6.2 — CSV, edited in place, all other lines byte-preserved |
| Roster editor (name, type, level, men, XP, equipment, position) | §6.3, §6.4 — writes only Confirmed offsets; recomputes the three derived cost fields from §5.1/§5.2 |
| Chronicle viewer | §6.6 |
| Live value scanner + DGROUP locator | §3, §6.5 |
| Copy-protection answer key | §4 |
| Reference tables (units, equipment, spells, cities, terrain) | §5 |
