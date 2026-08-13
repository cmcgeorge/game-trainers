# Curse of the Azure Bonds — Reverse-Engineering Notes

How *Curse of the Azure Bonds* (SSI, 1989 — the second AD&D "Gold Box" game) stores a character,
how that layout was recovered, and how the trainer reads and writes it.

The short version: **a `CHRDATAn.SAV` file is a character record, byte for byte.** That is the lever
everything else turns on. The file is 422 bytes, so the record is 422 bytes, and a real saved party
gives six of them to work with — plus a seventh in the `.GUY` file a transferred character is stored
in, and eighty-one more inside the game's own monster archives. Nothing here was recovered by
guessing at a memory dump; the game ships its own ground truth.

Every offset in §3 is confirmed by an assertion that ties it to at least one other offset through a
rule the game had no reason to satisfy unless the layout is right, and all of those assertions run
headlessly in `test/FormatCheck` (305 checks).

---

## 1. Source material

| Artifact | What it is |
|---|---|
| `SAVE\CHRDATA1..6.SAV` | A real saved party — six 422-byte character records. |
| `SAVE\CHRDATA?.FX` | Their effect lists: 9-byte records, one linked list per character. |
| `SAVE\JASON.GUY` | A character saved to disk for transfer — the same 422-byte record. |
| `SAVE\SAVGAMA.DAT` | The 13 KB game-state block (position, clock, quest flags). |
| `MON1..6CHA.DAX` | The bestiary: **81 complete character records**, one per creature, per chapter. |
| `ITEM1..6.DAX` | The item templates: 63-byte item records. |
| `GEO2..6.DAX` | Level geometry — 16 blocks, one per explorable level. |
| `curseazure.pdf` | The game's **Rule Book and Adventure Journal**, bundled with this install. |
| `GAME.OVR`, `START.EXE` | The overlaid executable. Used only for string spelunking. |

The Rule Book matters more than it sounds. It states outright that "New characters begin with 25,000
XP and the corresponding level" and that "Each character begins the game with 300 platinum pieces" —
two specific numbers that a candidate offset either produces or does not.

---

## 2. Establishing the record size

`CHRDATA1.SAV` is 422 bytes. So is every other `.SAV`, and so is `JASON.GUY`. 422 = `0x1A6`.

Two independent confirmations that this really is *the* record rather than a record plus a header:

* Offset `0x00` is a Pascal-string length byte and `0x01..0x0F` the name — the file starts at the
  start of the record, with no header in front of it.
* Every block of every `MON*CHA.DAX` archive unpacks to exactly 422 bytes and begins with the same
  Pascal-string name field. The game stores its monsters in the same structure it stores the party
  in, which is the Gold Box family's defining trait, and it pins the size from a second direction.

---

## 3. The character record — `0x1A6` (422) bytes

Offsets are relative to the record start. Fields the trainer edits are in **bold**.

| Offset | Size | Field | Notes |
|-------:|:----:|-------|-------|
| `0x00` | 1 | **name length** | Pascal string length (1–15) |
| `0x01`–`0x0F` | 15 | **name** | ASCII, NUL-padded — but see §4 |
| `0x10`–`0x1D` | 14 | **ability scores** | Seven **(current, maximum) pairs**: STR, INT, WIS, DEX, CON, CHA, exceptional-STR % |
| `0x1E`–`0x71` | 84 | memorized spells | one byte per spell |
| `0x73` | 1 | **THAC0 base** | stored `60 − value` |
| `0x74` | 1 | **race** | 0 monster · 1 dwarf · 2 elf · 3 gnome · 4 half-elf · 5 halfling · 6 half-orc · 7 human |
| `0x75` | 1 | **class** | 0 cleric · 1 druid · 2 fighter · 3 paladin · 4 ranger · 5 mage · 6 thief · 7 monk · 8 C/F · 9 C/F/M · A C/R · B C/M · C C/T · D F/M · E F/T · F F/M/T · 10 M/T · 11 monster |
| `0x76`–`0x77` | 2 | **age** | UInt16 LE |
| `0x78` | 1 | **HP maximum** | |
| `0x79`–`0xDC` | 100 | known spells | one flag byte per learnable spell |
| `0xDD` | 1 | attack level | |
| `0xDE` | 1 | icon dimensions | |
| `0xDF`–`0xE3` | 5 | **saving throws** | para/poison/death, petrify/polymorph, rod/staff/wand, breath, spell |
| `0xE4` | 1 | movement base | 12 for every human in the sample party |
| `0xE5` | 1 | level (highest class) | |
| `0xE6` | 1 | drained levels | |
| `0xE7` | 1 | drained HP | |
| `0xE8` | 1 | undead level | |
| `0xEA`–`0xF1` | 8 | **thief skills** | pick pockets, open locks, find/remove traps, move silently, hide, hear, climb, read languages |
| `0xF2`–`0xF5` | 4 | effects list pointer | far pointer; head of the `.FX` list |
| `0xF7` | 1 | NPC flag | |
| `0xF8` | 1 | modified flag | 1 for every party member, 0 for the `.GUY` transfer file |
| `0xFB`–`0x108` | 14 | **money** | seven UInt16s: copper, silver, electrum, gold, platinum, gems, jewelry |
| `0x109`–`0x110` | 8 | **class levels** | cleric, druid, fighter, paladin, ranger, mage, thief, monk |
| `0x111` | 1 | **gender** | 0 male / 1 female |
| `0x113` | 1 | **alignment** | 0 LG · 1 LN · 2 LE · 3 NG · 4 TN · 5 NE · 6 CG · 7 CN · 8 CE |
| `0x124` | 1 | AC base | stored `60 − value`; the unarmored 10 baseline |
| `0x127`–`0x12A` | 4 | **experience** | UInt32 LE — per-class share for a multi-class character |
| `0x12C` | 1 | HP rolled | raw dice before the CON bonus |
| `0x12D`–`0x131` | 5 | cleric spells/day | spell levels 1–5 |
| `0x132`–`0x136` | 5 | (unidentified) | zero for every caster in the sample party |
| `0x137`–`0x13B` | 5 | mage spells/day | spell levels 1–5 |
| `0x13C`–`0x13D` | 2 | **XP award** | experience granted for killing this creature (monsters) |
| `0x143` | 1 | marching order | 0–5 in the party; 10 for the un-partied `.GUY` |
| `0x144` | 1 | icon size | 1 small, 2 large |
| `0x145`–`0x14A` | 6 | combat-icon colors | two 4-bit palette indices per byte |
| `0x14B` | 1 | number of items | |
| `0x14C`–`0x14F` | 4 | items list pointer | linked list |
| `0x150`–`0x183` | 4×13 | equipped-item pointers | |
| `0x187`–`0x188` | 2 | encumbrance | carried weight, coins included |
| `0x189`–`0x18C` | 4 | next-character pointer | the party is a linked list |
| `0x18D`–`0x190` | 4 | combat struct pointer | valid during combat |
| `0x195` | 1 | **status** | 0 okay · 1 animated · 2 temp gone · 3 running · 4 unconscious · 5 dying · 6 dead · 7 stoned · 8 gone |
| `0x199` | 1 | **THAC0 current** | effective, `60 − value` |
| `0x19A` | 1 | **AC current** | effective, `60 − value` |
| `0x1A4` | 1 | **HP current** | |
| `0x1A5` | 1 | movement current | |

### How each region was pinned

Nothing above rests on "this byte happens to be 5". Each block is anchored by a relationship:

**Ability scores are pairs, not singles.** Reading `0x10` onwards for the six party members gives
`18 18 17 17 16 16 17 17 17 17 17 17 100 100` — every value duplicated. Fourteen bytes where seven
were expected, each one equal to its neighbour, across six independent characters. That is the
current/maximum pair the game needs so a drain has somewhere to go and a Restoration has something
to restore from. It also explains why the record is so much larger than the sister game's.

**Class levels** are the cleanest anchor in the record. At `0x109` the six characters read:
the cleric has 5 in slot 0, the paladin 5 in slot 3, the mage 5 in slot 5, the fighter/thief 4 in
slot 2 and 5 in slot 6, the fighter/mage 4 in slots 2 and 5. Six characters, six different class
bytes at `0x75`, and every one of them lands its levels in the slots its class byte names.

**Experience** at `0x127` reads 25,000 for each single-class character and 12,500 for each
multi-class one. The Rule Book states both numbers: new characters start with 25,000, and a
non-human multi-class character divides everything it earns by the number of its classes. Running
those totals through the Rule Book's own XP tables reproduces every class level in the paragraph
above — 25,000 buys paladin 5, cleric 5 and mage 5; 12,500 buys fighter 4 and thief 5.

**Hit points.** `0x78` is maximum and `0x1A4` current, equal for an undamaged party. `0x12C` is the
raw die roll: for each single-class character, `HpMax − rolled` is exactly the Constitution bonus
times the level (CON 17 → +3 × 5 = 15; CON 16 → +2 × 5 = 10). Three characters, three exact hits.

**AC and THAC0** use the same `60 − x` encoding as the sister game, and this party proves it twice
over. Nobody in it carries a single item (item count 0, encumbrance exactly 300 = their 300 platinum
coins), so every AC must be the unarmored 10 minus the AD&D 1st-edition Dexterity adjustment — and
it is: DEX 15 → 9, DEX 16 → 8, DEX 17 → 7, for all six. `0x124` decodes to exactly 10 for everyone,
which is the unarmored baseline it should be. THAC0 works the same way: `0x73` gives 16 for a
level-5 fighter-type, 17 for level 4, 18 for a level-5 cleric, 20 for a level-5 mage, and `0x199` is
that base minus the character's Strength bonus to hit — 18/00 → −3, 18/53 → −2, 18/12 → −1, 17 → −1.
Six characters, six exact matches, from two offsets that have to be right together.

**Spells per day.** The cleric reads 5/5/2 at `0x12D`. The Rule Book gives a 5th-level cleric 3/3/1
and Wisdom 17 a bonus of +2/+2/+1. The mage reads 4/2/1 at `0x137`, which is the Rule Book's
5th-level magic-user row exactly; the fighter/mage reads 3/2, its 4th-level row. The five bytes
between the two blocks are zero for every caster, priest and mage alike, so they are left labelled
as unidentified rather than guessed at.

**Thief skills.** Only the fighter/thief has any, and they read 62/69/62/52/43/27/87/27 at `0x0EA` —
a level-5 thief with Dexterity 17, with Climb Walls at 87 where it belongs as the seventh skill. It
is the 87 that fixes the block's alignment: read it one byte earlier and Climb Walls becomes 27,
which no thief has ever had.

**The effects pointer** is confirmed by arithmetic against a different file. The fighter/thief's
record holds `0x6597:000B` at `0x0F2`; his `CHRDATA3.FX` is 27 bytes — three 9-byte records — whose
links are `0x6598:0004` and `0x6598:000D`. Resolving those real-mode pointers gives `0x6597B`,
`0x65984`, `0x6598D`: two hops of exactly 9 bytes each, ending in a null. The two characters with no
`.FX` file on disk hold `0000:0000`. A wrong offset does not produce a correctly-spaced linked list.

**Money.** The seven UInt16s at `0xFB` read all zero except platinum, which is 300 for all six —
the Rule Book's stated starting funds. Encumbrance at `0x187` reads 300 as well, which is what 300
coins weigh when a character carries nothing else, and the item count at `0x14B` is 0, which agrees.

**Icon size** at `0x144` is 1 for both dwarves and 2 for everyone else. Dwarves get the small combat
icon; that is the whole tell, and it is enough.

---

## 4. The name field has a trap in it

The obvious rule for a Pascal string in a fixed field is "text up to the declared length, NULs after
it". The sister game's scanner enforces exactly that, and on Curse it silently loses party members.

`CHRDATA3.SAV` holds length 6 and the bytes `TRAVIS `, with a space still sitting at index 6 — the
player typed a trailing space, the game trimmed the length, and the buffer kept the character. A
signature that demands NULs past the declared length rejects that record, the scan comes back with
five party members instead of six, and nothing anywhere reports an error.

`CharacterSignature` therefore enforces the weaker, true rule: the field is name characters up to a
NUL and nothing but NULs after it. `test/FormatCheck` asserts the real record passes and that a
shifted or zero-filled buffer still fails.

---

## 5. Monsters are the same record

Every `MON<n>CHA.DAX` block unpacks to a 422-byte record in this exact format — 81 of them across
the six chapters. Decoding all 81 with the table above gives:

* current hit points equal to maximum hit points in **all 81**,
* status "okay" in **all 81**,
* current movement equal to base movement in **all 81**,
* Armor Class inside AD&D's range in all 81, and experience awards that match the published Gold Box
  values (troll 525, otyugh 700, ettin 1,950, beholder 12,900, dracolich 13,200).

Eleven records — the named NPCs, and the armed humans the engine equips as it builds an encounter —
carry an uncomputed *current* THAC0 byte and an Armor Class of 10, the unarmored baseline their gear
is applied to at spawn. The bestiary therefore lists the base THAC0, which is sane for all 81.

Because monsters share the format, the combat panel enumerates and edits them exactly like party
members, and `MonsterBook.cs` is generated from these blocks rather than transcribed.

The same is true of items: every block of every `ITEM*.DAX` is an exact multiple of 63 bytes, and
decoding the 58 base items reproduces the AD&D equipment tables outright — plate mail weight 450 and
value 400 gp, chain mail 300 and 75, leather 150 and 5, a two-handed sword 30 gp. That confirms the
63-byte item record and its weight, value and stack-count offsets from the game's own data.

---

## 6. Effects (`CHRDATAn.FX`)

An effect is a 9-byte record: a 5-byte payload `[type][b1][b2][duration][b4]` (duration `0xFF` =
permanent) followed by a 4-byte link. The links are stale runtime pointers the game rebuilds on
load, so only the payloads and the null terminator matter.

The Gold Box effect table applies to Curse unchanged, and the sample party proves it on four
independent races and classes at once:

| Character | Effects | What they are |
|---|---|---|
| Both paladins | `0x08` | protected from evil — the paladin's permanent aura |
| Both dwarves | `0x1A`, `0x2F`, `0x61` | dwarf THAC0 bonus, dwarf giant bonus, dwarf save bonus |
| The elf | `0x6B` | 90% sleep/charm resistance |
| Cleric and mage (human) | none | correct — neither race nor class grants one |

---

## 7. Level geometry — `GEO*.DAX`

The container and the packing are the Gold Box family's:

```
UInt16  headerLength
entry[headerLength / 9]:
    byte    id
    UInt32  offset          // from the end of the header
    UInt16  unpackedSize
    UInt16  packedSize
byte[]  packed block data
```

`2 + headerLength + Σ packedSize` accounts for every archive in the folder exactly. Blocks are
PackBits-style RLE: a lead byte `n < 0x80` copies the next `n + 1` bytes verbatim, otherwise the next
byte repeats `256 − n` times. Under that variant all 16 geometry blocks land on their declared
unpacked size exactly, and every monster block lands on 422.

A GEO block unpacks to 1026 bytes: a `UInt16` length (`0x0400`) then four 256-byte planes, each a
16×16 grid indexed `y * 16 + x`:

| plane | contents |
|-------|----------|
| 0 | high nibble = **north** wall index, low nibble = **east** wall index |
| 1 | high nibble = **south** wall index, low nibble = **west** wall index |
| 2 | per-square backdrop / interior id (not used) |
| 3 | two bits per direction (N = 0–1, E = 2–3, S = 4–5, W = 6–7); non-zero = the edge can be walked through |

Shared edges are stored on both squares, so each edge is merged from the two sides. A passable edge
whose wall index is also used for a solid wall elsewhere in the same level is an illusory wall —
passable, but drawn as stone. Squares sealed on all four sides, or cut off from the level's main
walkable region, can never be stood on and are drawn as impassable.

There are **16 levels**, three each in `GEO2`–`GEO5` and four in `GEO6`.

### Which level is which

This is where Curse differs from its sister, and the difference is worth being straight about.
Pool of Radiance's districts could be named because its clue book prints their maps and the decoded
blocks could be matched against the page scans. Curse's maps live in an Adventurer's Journal that is
**not part of this install** — the bundled `curseazure.pdf` is the Rule Book plus the journal's
*text*, and it prints no maps.

What the install does carry is each chapter's monster roster, and those name the chapters outright:

| Module | Roster includes | Therefore |
|---|---|---|
| 1 | a mixed bag from everywhere; **no geometry at all** | wilderness / random encounters |
| 2 | bar patrons, royal guards, thieves, Fire Knives, fighting dogs, sewer otyughs | **Tilverton** |
| 3 | Red Plumes, Zhentilar, cultists, vegepygmies, Mogion, a Bit o' Moander, Alias, Dragonbait | **Yulash and the Temple of Moander** |
| 4 | Zhentil fighters/mages/clerics, a beholder, a hooded medusa | **Zhentil Keep** |
| 5 | dark elf lords, fighters, mages and clerics, efreeti, **Dracandros**, Akabar bel Akash | **Dracandros's stronghold** |
| 6 | thri-kreen, phase spiders, priests of Bane, **Tyranthraxus** | **Myth Drannor** |

That is corroborated by the endgame text in `GAME.OVR` ("Tyranthraxus is slain this day"; "The
Knights of Myth Drannor rush in") and by the journal's own geography section.

So: the **chapter** each level belongs to is established. The level names *within* a chapter are
descriptive labels, and `MapBook.cs` says so, keeping each level's `GEO<n>:<block>` id beside its
name. The **geometry is exact either way** — and the question a label can't answer is answered
directly instead, by §8.

---

## 8. Identifying the level you are standing on

Every level in Curse is 16×16, so the party's coordinates cannot say which of the sixteen they are
coordinates *in*. Rather than guess, the trainer asks the game.

The game loads a level's wall planes — 512 bytes, planes 0 and 1 — into memory unchanged. So
`MapLocator` reads the levels back out of the install's own `GEO*.DAX` files and sweeps the emulated
RAM for one of those 512-byte arrays. A 512-byte exact match is not something a wrong answer
produces. The sweep is the megabyte around the party records rather than the whole process, because
the level data lives in the same 640 KiB of DOS conventional memory the party does.

This is the same residency the sister game's notes record for its own geometry, applied here as the
primary mechanism rather than as a spot check. It needs the game running to exercise, and the Maps
tab reports plainly when no level matches (between areas, or in a menu) instead of picking one.

---

## 9. Where the party is standing

Position is not in the character record and its address moves every DOSBox session, so it is found
by scan-and-narrow: collect every address that could hold the coordinates you read off the game's
status line, walk a square, and drop every candidate that no longer predicts them.

The expected shape is three adjacent bytes, `[X][Y][Facing]` (Gold Box facing `0=N 1=E 2=S 3=W`).
`PositionLocator` also collects a second shape — a pair of 16-bit words whose X carries a constant
bias, which is how the sister game stores overland position — because looking for it costs one extra
pattern per byte and **cannot** produce a wrong lock: a candidate only survives by continuing to
predict the coordinates through every narrowing step, and the bias is measured per candidate rather
than assumed. Whichever shape is right wins on evidence.

Do it while exploring, never mid-combat, and take a step afterwards to redraw the map.

---

## 10. What is still open

Stated plainly, so nobody mistakes a gap for a finding:

* **`0x132`–`0x136`** — five bytes between the cleric and mage spells-per-day blocks. Zero for every
  caster in the sample party, so there was nothing to decode them against.
* **`0x19B`, `0x19E`, `0x1A0`, `0x1A2`** and the bytes around `0x191` — non-zero and consistent
  across the party, but with no rule available to test a reading against.
* **Gender (`0x111`) and alignment (`0x113`)** read 0 for all six characters, so they are placed by
  structure — immediately after the class-level array, exactly as in the sister game's record, whose
  class-level array this one demonstrably matches. Both paladins reading Lawful Good is consistent
  and is the only positive evidence available.
* **The known-spell block's exact end.** It starts at `0x79`; the last flag seen in the sample party
  is at `0xA7`, and the next identified field is at `0xDD`, so the extent is bounded, not measured.
* **The wilderness position shape** has not been observed in Curse. It is searched for, not relied on.
* **`MapLocator` and the item-list walk need the game running** to exercise. The formats they use are
  confirmed from files; the residency and the guest→host offset are not.

---

## 11. Cross-references

- The game's own **Rule Book and Adventure Journal**, bundled with this install as `curseazure.pdf`.
- `Gold Box Companion` — https://gbc.zorbus.net/
- `coab`, a Curse of the Azure Bonds reimplementation — https://github.com/simeonpilgrim/coab
- The sister trainer's notes, `../PoolOfRadianceTrainer/docs/reverse-engineering.md`, for the Gold Box
  container and RLE formats this shares.
