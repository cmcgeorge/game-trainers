# DarkSpyre — Reverse Engineering Notes

## 1. Game Background

| Field | Value |
|---|---|
| **Title** | DarkSpyre |
| **Developer** | Event Horizon Software |
| **Publisher** | Electronic Arts |
| **Platform** | IBM PC (DOS) |
| **Release** | 1990 |
| **Genre** | Real-time dungeon-crawler RPG |
| **Perspective** | Top-down 2D |
| **Levels** | 50 total, 39 required |

DarkSpyre is a real-time dungeon crawler in which a single character descends through
the levels of a tower. The game never pauses on its own, which is what makes live
memory editing awkward: the number you read off the screen has often already moved by
the time you type it. **P** pauses, which is the window for reading values by eye — the
trainer's automatic locator does not need that window at all.

## 2. Confidence Markers

Every fact below carries its provenance:

- **[File]** — read out of a file the game ships (`CR.DAT`, `OBJ.DAT`, `HISCORE`,
  `darkspyre.txt`). Reproducible offline with `.docs/decode_game_data.py`.
- **[Live]** — observed in a running DOSBox session: read from guest RAM, or written
  into guest RAM and confirmed on the game's own screen.
- **[Manual]** — stated in `darkspyre.txt` (the manual that ships with the game) or the
  Cheatbook walkthrough, and not independently verified here.
- **[Open]** — noticed but not settled. Called out as such rather than guessed at.

## 3. What Ships With the Game

`C:\GAMES\DARKSYPR` holds 78 files. The ones that matter: [File]

| File | Size | What it is |
|---|---|---|
| `DARKSPYR.COM` | 2 KB | Loader; runs the `RUNTIME` stages |
| `RUNTIME.1` / `RUNTIME.2` | 65 KB / 59 KB | MZ executables — the engine |
| `CR.DAT` | 157 KB | Creature table: 86-byte headers, each followed by its sprite blob |
| `OBJ.DAT` | 11 KB | Object table: 162 records plus a name table |
| `WEAPON.DAT` | 5.7 KB | Per-weapon data, keyed by an offset table |
| `WALLS.DAT`, `DOORS.DAT`, `GRAPHIC.DAT`, `FACES.DAT` | — | Tile and sprite art |
| `MAP00.DAT`, `MAP0B…MAP4H.DAT`, `MAPR0…MAPR8.DAT` | 2–4 KB each | 42 tile grids |
| `C`, `E`, `F`, `T`, `V`, `W`, `X` | 3–5 KB each | Overlay code modules (video drivers) |
| `HISCORE` | 237 B | Hall of Champions table |
| `darkspyre.txt` | 30 KB | The manual, including the Amiga control supplement |

### 3.1 The executables are packed [File]

`RUNTIME.1` is an MZ image with a 32-byte header, no relocations, and an entry stub that
decompresses the real image at startup; the stub carries the marker `*FAB*` at file
offset `0xFBF3`. Only fragments of text survive in the file (`cr.dat`, Borland C runtime
strings) and they are interleaved with back-reference tokens, which is the signature of
an LZ-packed executable.

The practical consequence: **static disassembly of the shipped files gets you very
little.** The unpacked image only exists in guest RAM. That is why the work below was
done against a live session rather than in Ghidra.

## 4. Method

1. Copy the game to a scratch directory so the user's own save and high-score files are
   never touched.
2. Run it under DOSBox 0.74-3 with `memsize=16`, `scaler=normal3x`.
3. Start a character with a deliberately unusual name (`ZQXWVU`) so it is easy to find.
4. Dump the emulator's committed regions and search for known values.
5. Confirm each candidate field by **poking it and watching the game's own screen**
   change.

Two details made step 5 practical:

- **Guest RAM is the largest private 16 MB region** of the DOSBox process. Offsets in
  this document are relative to the start of that region. Guest-physical addresses are
  `0x20` lower in this build — the BIOS data area lands at region offset `0x420`, not
  `0x400` — so subtract `0x20` if you are comparing against a DOS memory map. [Live]
- **The rendered screen can be read straight out of the process.** With
  `output=surface` and a 3× scaler, DOSBox keeps a 960×600 32-bit BGRA surface in a
  private region of exactly 2,304,000 bytes. Reading that region and saving it as an
  image gives a screenshot without needing the desktop at all, which is what let the
  poke-and-observe loop run unattended. [Live]

## 5. Character State In Memory

DarkSpyre does not keep one character record. It spreads live state across **three**
structures, and knowing which one to write is the whole game:

### 5.1 Status block — 12 bytes, six 16-bit values [Live]

| Offset | Field |
|---|---|
| +0 | Current hit points |
| +2 | Current spell points |
| +4 | Current encumbrance |
| +6 | Maximum hit points |
| +8 | Maximum spell points |
| +10 | Maximum encumbrance |

This is exactly what the on-screen bars print (`HIT POINTS 039/039`, `SPELL POINTS
039/039`, `ENCUMBRANCE 000/075`). The game rebuilds it from the other two structures
every frame: a value written here is gone on the next tick, so the trainer reads it and
never writes it.

### 5.2 Character record — 12 bytes [Live]

| Offset | Field |
|---|---|
| +0…+5 | Strength, Agility, Endurance, Accuracy, Talent, Power (one byte each) |
| +6 | Maximum hit points (16-bit) |
| +8 | Maximum spell points (16-bit) |
| +10 | Maximum encumbrance (16-bit) |

Writing `+6` and `+8` was confirmed on screen: poking `0x2A` at +6 turned the bar into
`HIT POINTS 040/042`, and poking `0x50` at +8 turned it into `SPELL POINTS 046/080`.
The character then regenerated toward the new ceiling, so the engine genuinely adopts
the value — the manual's "spell points can never exceed 100" is a design statement, not
a clamp. A maximum of 400 was accepted without complaint.

The six attribute bytes matched the character sheet exactly (STR 15, AGI 13, END 11,
ACC 10, TAL 14, PWR 12 for the test character). Writing them takes effect in memory
immediately, but the character sheet is a cached bitmap: it only shows the new numbers
once the game repaints that panel. Maximum encumbrance was **not** observed to be
recomputed from Strength on the spot. [Open]

### 5.3 Player actor — creature-table entry 0 [Live]

The per-level creature table is loaded from `CR.DAT`; entry 0 is the player. Its layout
is the creature header (§6):

| Offset | Field |
|---|---|
| +0x10 | Current hit points (16-bit) |
| +0x12 | Current spell points (16-bit) |
| +0x1D | ASCIIZ name — always `player` for entry 0 |

This is the copy the engine plays out of. Poking `0xC8` at +0x10 raised the on-screen
hit points to 200 and regeneration continued from there; a write to the *status block*
instead was overwritten within one frame. So: **current HP and SP are written here,
maxima and attributes are written to the character record.**

### 5.4 Session addresses [Live]

For the record, in the session these notes were taken from (region base `0x7630000`):

| Structure | Region offset |
|---|---|
| Status block | `0x1E4B9` |
| Character record | `0x21DE0` |
| Player actor | `0x21F0A` |
| `OBJ.DAT` buffer | `0x2370C` |

These are **not** hard-coded anywhere in the trainer. DOS load addresses move with the
environment, and the creature table is rebuilt per level, so the trainer searches by
content instead (§8).

## 6. Creature Table (`CR.DAT`) [File]

Records are variable length: an 86-byte (`0x56`) header followed by that creature's
sprite data. Header fields confirmed by comparing the file against the copy in guest
RAM — everything from +0x1D on is byte-identical, while +0x00…+0x1C is runtime state:

| Offset | Field |
|---|---|
| +0x10 | Current hit points (runtime) |
| +0x12 | Current spell points (runtime) |
| +0x1D | ASCIIZ name |
| +0x3D…+0x3F | Sprite width, height, frame count |
| +0x40…+0x45 | Strength, Agility, Endurance, Accuracy, Talent, Power |
| +0x48 | `0x0A` on creatures that attack at range |
| +0x49 | Projectile kind (only meaningful when +0x48 is `0x0A`) |
| +0x4D | Rises with the depth a creature first appears at |

The file ships **35 creatures plus the player**: slime, giant bee, giant bat, wraith,
mummy, gorilla, shadow warrior, hatchling, scorpius, lizard, centipede, troll, gargoyle,
samurai, hellhound, cyclops, gelatinous cube, saw blade, harpy, crystal knight,
minotaur, stone golem, warrior maiden, evolved slime, manta ray, jester, muskateer,
crustacean, pheonix, djinn, gryphon, creeper, electric ball, beholder, spartan warrior.

Two independent checks say the attribute and ranged-flag decode is right:

- The seven creatures flagged at +0x48 (harpy, jester, pheonix, djinn, gryphon, electric
  ball, beholder) are exactly the ones the walkthrough files under "ranged" — it
  describes beholders, djinn, electric storms and the jester as the monsters that shoot.
- Attribute values read as characters should: the spartan warrior, the end-game melee
  creature, has 25/25/25 in STR/AGI/END and 6/7/0 in ACC/TAL/PWR; the djinn, a caster,
  is 8/8/10 with 16 Talent. Monsters are not held to the player's cap of 20.

Scanning for lowercase ASCIIZ words also turns up short strings inside sprite blobs
(`wp`, `wwp`, `gwv`). They are rejected by requiring plausible sprite dimensions and
attributes, which is what `.docs/decode_game_data.py` does.

## 7. Other File Formats

### 7.1 Object table (`OBJ.DAT`) [File]

`[3-byte header][162 × 57-byte record][name table]`. The name table starts at `0x2415`
and holds 162 ASCIIZ names in record order: `random object`, `bolt`, `spellbook`,
`knock scroll`, … through the 25 runes and `scroll of sight`.

The name table alone settles several things the secondary sources get wrong:

- The game spells three runes differently from the manual: **kano** (manual: KENO),
  **othila** (OTHILIA), **laguz** (the trainer previously carried "Laquz", which is in
  neither).
- There are exactly 25 `… rune` entries and exactly 14 spell sources (a scroll per
  spell, plus the spellbook), which corroborates the manual's rune and spell counts.

The 57-byte records hold small integers — the first three 16-bit fields of `bolt` are
14/30/30 and of `spellbook` 19/100/100 — but the field semantics are not settled, so the
trainer exposes names and table order only. [Open]

### 7.2 High-score table (`HISCORE`) [File]

`[1 byte][current character name, 20 bytes]` then nine entries of
`[name, 20 bytes][rank, 1][level, 1][score, 2 little-endian]`. The shipped table decodes
to Borel 35000, Sprig 25000, Adam 15000, WarMonger 10000, Jessica 5000, Wanda 2500, Morg
2000, Deidra 1000, Kung 500 — descending, which is what confirms the field order.

Useful consequence: the game stores **score as 16 bits and level as one byte**, which is
what the trainer's scan recipes for those two now assume.

### 7.3 Map files (`MAP*.DAT`) [Open]

42 files, each `[0x29][0x31 or 0x32][0x00]` followed by a tile grid — plausibly 41 wide
by 49 or 50 tall, though the files are larger than that product. Tile alphabet: `0xFF`
void, `0x01`–`0x06` walls and wall variants, `0x1E`–`0x23` floor variants.

The live level buffer uses the same alphabet, and a handful of 24-byte windows from it
appear verbatim in `MAP00.DAT` — but the overall match is only about 57% even when
floor-tile variants are ignored, and no other file matches better. So these are
**not** simply "level *n* is `MAPnn.DAT`". They are most likely room or section
templates the generator stitches together, or layouts for particular special levels.
Stated here as unfinished rather than resolved: the previous version of this document
asserted "no fixed maps exist", which the files themselves contradict, but the
relationship is still open.

## 8. How the Trainer Uses This

`Memory/CharacterLocator.cs` finds all three structures with no hard-coded address and
no assumed distance between them — only the internal layout of each, which is a property
of the build. Three stages, each confirming the next:

1. **Player actor** — search for `player\0`, treat each hit as a name field at +0x1D,
   validate the record around it, and read current HP and SP.
2. **Status block** — search the same region for six 16-bit values whose first two equal
   the actor's HP and SP and whose maxima bracket the current values.
3. **Character record** — search for six in-range attribute bytes followed by exactly
   the three maxima the status block just reported.

Cross-checking is what makes it unique. Run against the 16 MB guest-RAM dump from the
session in §5.4, each stage resolves to exactly one address — `0x21F0A`, `0x1E4B9`,
`0x21DE0` — in well under a second. `test/FormatCheck` re-runs the same search over a
synthetic RAM image with decoys (a second creature record, a status block belonging to a
different character, structures straddling page boundaries), and will run it over a real
dump if you pass one: `FormatCheck path\to\dump.bin`.

When the game is sitting in its menus there is no creature table, so the locator returns
nothing and the trainer says so rather than guessing. When you change level the creature
table is rebuilt somewhere else; the poll loop notices the record stops validating and
searches again by itself.

## 9. Character System [Manual]

Six attributes, 1–20, set at character creation by the choices you make in the "Tale of
Champions" story and afterwards only raised by the power runes exchanged on Level 36:

| ID | Attribute | Effect |
|---|---|---|
| 0 | Strength | Hit points and melee damage |
| 1 | Agility | Dodging and movement |
| 2 | Endurance | Hit points and carrying capacity |
| 3 | Accuracy | Chance to hit |
| 4 | Talent | Spell points and magic proficiency |
| 5 | Power | Spell points and spell effect |

`HP = Strength + Endurance + random`, `SP = Talent + Power + random`. The test character
rolled STR 15 / END 11 with 39 hit points and TAL 14 / PWR 12 with 39 spell points, and
started with a maximum encumbrance of 75 — five times Strength. [Live, one sample]

## 10. Combat and Magic [Manual]

Seven weapon proficiency classes (Clubbing, Hurled, Large, Long Edge, Projectile, Short
Edge, Thrusting), ten proficiency levels from None to Expert. Weapons break, so training
several classes matters. Six magic classes (Healing, Sorcery, Wizardry, Conjury, Diviny,
Enchantry), seven proficiency levels from None to Master, 14 spells; each spell's cost is
split half on preparation and half on casting. Armour has 15 protection levels and 7
condition levels.

## 11. Controls [Manual — `darkspyre.txt`]

| Key | Action |
|---|---|
| Keypad 1–9 | Move |
| 1–6 | Trigger the numbered menu action |
| F1–F7 | Cast a prepared spell |
| F8 | Information: score, level, sound status |
| A / W / S | Show attributes / weapon proficiencies / magic proficiencies |
| T | Take the item you are standing on |
| Enter | Toggle a switch you are standing on |
| − / + | Scroll the character sheet up / down |
| P | Pause |
| Esc | Abort the save or restore screen |

The mouse does the rest: click a menu bar to act, click your character to pick up, drag
the grey bar to slide the character sheet.

## 12. Not Reverse-Engineered

- **Inventory** — where carried items live, and in what encoding. This is the biggest
  remaining gap; it would make an item editor possible.
- **Map position** — no teleport feature, because the player's tile coordinates were not
  identified.
- **Score and level in memory** — the on-disk sizes are known (§7.2) but the live
  addresses were not pinned down, so both are still value-scan targets.
- **Save files** — `save.dir` is referenced at runtime; the format was not decoded.
  Saving needs a Raido rune, which the test session never found.
- **Weapon data (`WEAPON.DAT`)** — the offset table is obvious, the payload is not.
- **Proficiency arrays** — a run of small values near the character name looks like the
  weapon and magic proficiency tables, but nothing was confirmed.

## 13. Sources

| Source | What it gave |
|---|---|
| The game's own files | Creature table, object table, high-score format, controls, all counts |
| A live DOSBox session | The three character structures and which one to write |
| `darkspyre.txt` (shipped manual) | Attributes, formulas, proficiency systems, rune meanings, key bindings |
| Cheatbook walkthrough | Monster tactics, level structure, rune effects |
| Wikipedia, MobyGames | Publisher, release date, genre |
