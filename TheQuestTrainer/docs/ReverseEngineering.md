# The Quest — reverse-engineering notes

What is in this file: how The Quest keeps a character in memory, how the trainer finds that
character without asking anyone to search for a value, and which parts of the game are *not* in
memory at all because the engine recomputes them every frame.

Everything below was read out of `TheQuest.exe` with Ghidra and then confirmed against a running
game. Where a claim is inferred rather than observed it says so.

---

## 1. The target in one page

| | |
|---|---|
| Game | The Quest — Redshift Ltd., 2006 (PDA), Windows re-release |
| Build examined | **v1.9.10**, GOG edition (`goggame-1219333898`) |
| Executable | `TheQuest.exe`, 3,707,392 bytes |
| Format | PE32, machine `0x014C` (i386) — a 32-bit process even on 64-bit Windows |
| Link stamp | `0x5E57BD07` — 2020-02-27 12:58:47 UTC |
| Preferred image base | `0x00400000` |
| `DllCharacteristics` | `0x8140` = DYNAMICBASE \| NX_COMPAT \| TERMINAL_SERVER_AWARE |
| `SizeOfImage` | `0x0038F000` |
| Observed mapped base | `0x00260000` (**not** the preferred base — see below) |
| Saves | `%USERPROFILE%\Saved Games\The Quest\Save`*n*`.save`, `QuickSave.save` |
| Expansion | `expansions\isle.pak` — *Islands of Ice and Fire* |

**`TheQuest.exe` sets DYNAMICBASE.** Nothing may assume `0x00400000`. The observed base in the
probed session was `0x00260000`, i.e. every RVA in this document must be added to whatever base the
module is actually mapped at. Ghidra addresses in this file are quoted at the *preferred* base
(`0x00400000` + RVA), because that is what the disassembler shows.

Sections, as mapped:

| Name | RVA | Virtual size | Characteristics | |
|---|---|---|---|---|
| `.text` | `0x001000` | `0x2B2506` | `0x60000020` | code |
| `.rdata` | `0x2B4000` | `0x07A3F2` | `0x40000040` | read-only data — **vtables live here** |
| `.data` | `0x32F000` | `0x006C8C` | `0xC0000040` | writable globals — **the engine pointer lives here** |
| `.gfids` | `0x336000` | `0x0002D4` | `0x40000040` | control-flow guard |
| `.tls` | `0x337000` | `0x000009` | `0xC0000040` | |
| `.rsrc` | `0x338000` | `0x041778` | `0x40000040` | |
| `.reloc` | `0x37A000` | `0x014DDC` | `0x42000040` | |

`.data` is only 27 KB. That is the single most useful fact about this binary: almost nothing is a
static global, so almost everything is on the heap — but it also means the handful of pointers that
*are* static are easy to find and easy to validate.

---

## 2. What kind of program this is

The Quest is a C++ game on Redshift's own engine, which the source paths call **Fen**. The
executable is built with assertions that embed their source file, so the module layout is legible
straight out of the string table:

```
..\..\source\Fen\Lua.cpp                     ..\..\source\Quest\Game\SPlayer.cpp
..\..\source\Fen\ResourceSystem\ZipFile.cpp  ..\..\source\Quest\Game\SDungeonWorld.cpp
..\..\source\Fen\Sound\Sound.cpp             ..\..\source\Quest\Game\SEngineManager.cpp
..\..\source\Quest\Display\Driver\D3D9.cpp   ..\..\source\Quest\Objects\SSkills.cpp
..\..\source\Quest\Script\SEngineRun.cpp     ..\..\source\Quest\Objects\SRaces.cpp
..\..\source\Quest\States\SStateInventoryStatusMain.cpp
```

Two scripting languages are linked in, and neither of them holds the character:

- **Lua 5.2.** `_VERSION`, `__pairs`, `__ipairs`, `collectgarbage` options `setmajorinc` /
  `generational` / `isrunning`, and `version mismatch: app. needs %f, Lua core provides %f` are all
  5.2-specific. It is reached through `source\Fen\Lua.cpp` — engine plumbing, not gameplay.
- **The game's own script VM** (`source\Quest\Script\SEngineRun.cpp`), a small C-like language used
  by dialogs and quests. Its diagnostics (`lvalue required`, `unexpected }`, `not an object`,
  `properties aren't supported`) and its command vocabulary (`getgold`, `receivegold`, `removegold`,
  `stealgold`, `findgold`, and template variables `%PCGold%`, `%PCCrimeGold%`) give away that gold
  and crime are ordinary fields on an ordinary object.

Ghidra's RTTI analyser recovers 220 vtable symbols and **every one of them is STL** — the game's own
classes are compiled without RTTI. So there is no `SPlayer::vftable` to search for by name. There is
still a vtable pointer at the head of the character object; it just has no symbol. That turns out to
be enough (§4).

---

## 3. Finding the character: what the disassembly gave up

The whole teardown pivots on one line in the shop code, the function that prints
`You don't have enough gold.`:

```c
// FUN_005cbc40 — "buy this item?"
iVar2 = *(int *)(in_ECX + 0x44);                       // the UI state's pointer to the engine object
if (*(uint *)(iVar2 + 0x3fb8) < *(uint *)(iVar1 + 8))  // player gold < item price
    ...  "You don't have enough gold."
```

So there is a big heap object — call it the **engine object** — with the player's gold at `+0x3FB8`,
and every `SState*` UI class keeps a pointer to it at its own `+0x44`. Grepping the decompiled UI
for the same base immediately produces the rest of the status screen:

```c
FUN_0043e8a0(buf, "%u",                  *(byte *)(engine + 0x3e12));   // Level:
FUN_0043e8a0(buf, "%u (Next level: %u)", *(u32  *)(engine + 0x3e14),    // Experience:
                                         *(u32  *)(engine + 0x3e20));
if (*(short *)(engine + 0x3e0e) == 0) ...                               // dead?
*(int *)(state + 0x6ecc) = (int)*(short *)(engine + 0x4198);            // the Fame bar
FUN_0043e8a0(buf, "%u",                  *(u32  *)(engine + 0x419c));   // Crime:
if (5 < *(uint *)(engine + 0x41a0)) throw out_of_range;                 // Race: — a six-entry table
```

and the attribute screen gives the point counters, one "+" button per attribute:

```c
// FUN_005a5900 — the Attributes panel
if (*(short *)(engine + 0x3fc8) == 0 || *(short *)(engine + 0x3fcc) == 0) hide("+" for Strength);
if (*(short *)(engine + 0x3fc8) == 0 || *(short *)(engine + 0x3fce) == 0) hide("+" for Dexterity);
...                                            0x3fd0, 0x3fd2, 0x3fd4     Endurance … Personality
FUN_0043e8a0(buf, "%u", *(u16 *)(engine + 0x3fc8));                     // Available points:
```

### The engine object is not the character — it *contains* one

The reputation word is produced by a small helper:

```c
// FUN_004ed5b0 — the word next to the Fame bar
short f = *(short *)(this + 0x3d0);
if (f == 100) return "Saint";  if (f > 79) return "Blessed";  ...
```

That reads fame at `this + 0x3D0`, while the status screen reads the same number at
`engine + 0x4198`. The difference is exactly **`0x3DC8`**, so `this` is a sub-object embedded in the
engine object at that offset — the character. Checking the live process confirms it: the dword at
`engine + 0x3DC8` is `module + 0x30AA24`, a pointer into `.rdata`. That is the character class's
**vtable**, and it is the anchor the trainer validates against.

From here on, offsets are quoted **relative to the character record**, i.e. `engine + 0x3DC8 + x`.

### The static pointer

Scanning the live process for pointers to the engine object turns up exactly one inside the mapped
image, at RVA **`0x00335790`** — inside `.data` (`0x32F000 … 0x335C8C`). Ghidra shows it as
`DAT_00735790`, used elsewhere as a display-metrics root, which is consistent: this really is the
process-wide engine singleton, and the character is one of its members.

Two reads and you are there. But an RVA is a promise about *this build*, so the trainer treats it as
a shortcut and not as a dependency.

---

## 4. Finding the character without any address at all

Every character record carries a full copy of the per-level experience table (§5). Its first eight
entries —

```
400, 900, 1500, 2500, 4000, 7000, 11000, 17000
```

— are a 32-byte little-endian pattern. Sweeping a 257 MB live session for it returned **exactly two
hits**, both inside the engine object:

| Hit | Record base | What it is |
|---|---|---|
| `0x041FD0FC` | `0x041FD098` = engine `+0x3DC8` | the live character |
| `0x041F9A24` | `0x041F99C0` = engine `+0x06F0` | a pristine **new-character prototype** |

Both have the same vtable. The prototype is what a new game is stamped from: no name, level 1,
40 health, 40 mana, no gold, and — decisively — **no cached next-level threshold**.

So the trainer runs two chains and one validator:

- **Chain A** — read `module + 0x335790`, add `0x3DC8`, validate. Two reads, no scanning. The slot is
  only read at all when its RVA lands in a **writable, non-executable** section of the *mapped* PE;
  a different build could put code there.
- **Chain B** — sweep the heap for the signature, subtract `0x64` (the table's offset in the record),
  validate. This chain knows no RVAs whatsoever, so it survives a build that moves the static slot.
  It took **954 ms** over the whole live address space.

**Validation is what makes either chain safe:**

1. the address is non-null and 4-aligned;
2. the whole record reads;
3. the first dword is a pointer into the mapped module, landing in a **non-writable** section — a
   vtable cannot live in `.data`;
4. the embedded experience table starts with the signature;
5. the name is a well-formed MSVC `std::string` **and is not empty**;
6. the portrait id is a well-formed `std::string`;
7. level is 1..99, health and mana are sane, every attribute is non-zero, race id is 0..5;
8. the cached next-level threshold is non-zero.

Checks 5 and 8 are what reject the prototype. If more than one record still passes — the game does
not do this, but a future expansion might — the one with the most experience wins, then the lowest
address, so the choice is stable.

### `std::string` as a validator

The name and the portrait id are 32-bit MSVC `std::string`s:

```
+0x00  union { char buf[16]; char* ptr; }
+0x10  size_t size          // characters, excluding the terminator
+0x14  size_t capacity      // 15 while inline, larger once it spills to the heap
```

`"Gerth the Derth"` is 15 characters, so it sits inline with `capacity == 15`.
`"bres_head00_racederth"` is 21, so the union holds a pointer and `capacity == 31`. Requiring
*capacity ≥ 15, size ≤ capacity, inline values NUL-terminated inside the buffer, spilled values
pointing at readable NUL-terminated characters* is a far stronger filter than any range check on a
number, and it costs one extra read.

---

## 5. The character record, field by field

Offsets from the record base. Everything is little-endian.

| Offset | Type | Field | How it was pinned |
|---|---|---|---|
| `+0x000` | `void*` | vtable → `.rdata` (`module + 0x30AA24` in this build) | live process |
| `+0x004` | — | three unknown dwords; `+0x00C` read `0x00FFFF11` | — |
| `+0x010` | `u32` | a second copy of experience | equal to `+0x04C` in every observation; the save writes it too |
| `+0x014` | `std::string` | character name | live process |
| `+0x02C` | `std::string` | portrait resource id, e.g. `bres_head00_racederth` | live process |
| `+0x044` | `u16` | unknown, read `1` | — |
| `+0x046` | `u16` | **current health** | `if (*(short*)(engine + 0x3e0e) == 0)` — the death test |
| `+0x048` | `u16` | **current mana** | live process |
| `+0x04A` | `u16` | **level** (read as a byte) | `"%u"` on the status screen |
| `+0x04C` | `u32` | **experience** | `"%u (Next level: %u)"` |
| `+0x050` | `u32` ×2 | unknown, read `0` | — |
| `+0x058` | `u32` | **experience the next level needs** (cached) | `"%u (Next level: %u)"` |
| `+0x05C` | `u32` ×2 | unknown, read `0` | — |
| `+0x064` | `u32[98]` | **per-level experience table** | value shape; §6 |
| `+0x1EC` | `u32` | unknown, read `0` | — |
| `+0x1F0` | `u32` | **gold** | `if (engine + 0x3fb8 < price)` in the shop |
| `+0x1F4` | `u16[6]` | **base attributes**, ids 1..5; slot 0 unused (read `15`) | live process + the "+" button code |
| `+0x200` | `u16` | **unspent attribute points** | `"%u"` under *Available points:* |
| `+0x202` | `u16[6]` | per-attribute raise allowance, ids 1..5 | the five `0x3fcc … 0x3fd4` tests |
| `+0x20E` | `u8[20]` | **skill display order** — skill ids, primaries first | the skills screen reads `0x3fd6 … 0x3fe9` byte by byte |
| `+0x222` | `u16` | **unspent skill points** | value matched *Available points: 40* |
| `+0x224` | `u16[21]` | skill values **at character creation**; slot 0 unused | all 8 except the race-locked school, which is 0 |
| `+0x24E` | `u16[21]` | **base skill values** — the array training raises | write test; §7 |
| `+0x3D0` | `i16` | **fame**, −100..+100 | the reputation helper reads `this + 0x3d0` |
| `+0x3D2` | `i16` | unknown, read `0` | — |
| `+0x3D4` | `u32` | **crime** (the outstanding bounty) | `"%u"` next to *Crime:* |
| `+0x3D8` | `u32` | **race id** | `if (5 < …) throw out_of_range` over a six-entry table |

The whole record is a few kilobytes; the trainer snapshots `0x400` bytes, which covers every field
above.

The arrays are all "id-indexed with an unused slot 0", and they tile exactly:

```
+0x1F4  attributes[6]        →  +0x200
+0x200  attribute points     →  +0x202
+0x202  raise allowance[6]   →  +0x20E
+0x20E  display order[20]    →  +0x222
+0x222  skill points         →  +0x224
+0x224  starting skills[21]  →  +0x24E
+0x24E  base skills[21]      →  +0x278
```

That tiling is why `Game/QuestLayout.cs` states each offset as arithmetic on the one before it: a
mistyped constant then fails a harness check instead of quietly reading a neighbour.

---

## 6. The experience table

98 `u32` entries at `+0x64`, thresholds for reaching levels 2 … 99:

```
400        900        1 500      2 500      4 000      7 000      11 000     17 000
25 000     40 000     60 000     90 000     130 000    180 000    240 000    320 000
420 000    570 000    730 000    920 000    1 150 000  1 410 000  1 700 000  2 020 000
…
188 550 000  195 170 000  201 950 000  208 890 000  215 990 000
```

It is a *copy per record*, not a global, which is what makes it usable as a structural signature.
The trainer never bakes it in: it reads the table out of the record it found and does its level
arithmetic from that, so a build or expansion that retunes the curve stays correct. Only the first
eight entries are hard-coded, and only as a scan pattern.

The relationship the game maintains, and the one the trainer preserves when it sets a level:

```
experience              ≥ table[level - 2]      (level ≥ 2; level 1 starts at 0)
experienceForNextLevel  = table[level - 1]
```

The threshold at `+0x58` is a **cache**, not a computation. Writing the level alone leaves a level-40
character still needing 4,000 experience, so `TrainerActions.SetLevel` writes all three fields.

---

## 7. Skills: twenty ids, and how they were proved

The record has two 21-entry skill arrays and no names. The names come from the display-order array
at `+0x20E`, which in the probed session read

```
0A 08 09 0B 0D 11 | 01 02 03 04 05 06 07 0C 0E 0F 10 12 13 14
```

— twenty ids, primaries first, then the "Other skills" list. Laying that against the skills screen
**column by column** (the game fills the left column, then the right) pins every id, and the result
matches `SSkills.cpp`'s string table in order:

| id | Skill | Governed by | id | Skill | Governed by |
|---|---|---|---|---|---|
| 1 | Block | Dexterity | 11 | Mind Magic | **Personality** |
| 2 | Light Weapon | Dexterity | 12 | Undead Magic | Intelligence |
| 3 | Heavy Weapon | **Strength** | 13 | Environment Magic | Intelligence |
| 4 | Dual Wield | Dexterity | 14 | Repair | Dexterity |
| 5 | Light Armor | Dexterity | 15 | Appraise | **Personality** |
| 6 | Heavy Armor | **Endurance** | 16 | Alchemy | Intelligence |
| 7 | Accuracy | Dexterity | 17 | Persuasion | **Personality** |
| 8 | Healing Magic | Intelligence | 18 | Lockpick | Dexterity |
| 9 | Protection Magic | Intelligence | 19 | Disarm | Intelligence |
| 10 | Attack Magic | Intelligence | 20 | Stealth | Dexterity |

Heavy Weapon is the only Strength skill; Heavy Armor the only Endurance one. The governing
attributes are the game's own, from the tooltips in `SSkills.cpp`.

Two schools are race-locked, again per the game's own wording:

- **Healing Magic** — *"Cannot be learned by Undead (Rasvim)."*
- **Undead Magic** — *"Can only be learned by Undead (Rasvim)."*

### Base values versus what the screen shows

`+0x24E` holds **base** values. The skills screen shows base plus racial and equipment modifiers, and
the difference is not small. The probed character is a Derth, whose racial ability reads:

```
Derth race
-5 Strength   -5 Dexterity   -5 Endurance   +10 Intelligence
+10 Healing Magic   +10 Mind Magic   +10 Attack Magic
```

which is exactly the gap between the record and the screen:

| | record `+0x24E` | screen |
|---|---|---|
| Attack Magic | 20 | 30 |
| Healing Magic | 21 | 31 |
| Mind Magic | 46 | 56 |
| Environment Magic | 18 | 18 |
| Block | 15 | 15 |

Same story for attributes: all five bases read 23, and the screen showed
`Strength 18 (23) · Intelligence 33 (23)`.

**Undead Magic is the exception worth knowing.** The Derth's record holds base 10 for it, but the
screen shows 0, because a non-Rasvim cannot use the school at all. The trainer honours the same rule
in *Max skills* rather than writing a number the game will refuse to act on.

### The game does not re-clamp a value written from outside

The skills screen states the rule *"The base value of %s cannot be higher than double of the base
value of its governing attribute (%s)"*, and it enforces it when you spend points. Writing the array
directly is not filtered: base skills were set to 30, 46, 47 and 100 with a governing attribute of 23
(cap 46) and every value stuck, survived a tab switch, and was redrawn as written. The trainer's
*Max skills* therefore uses the cap as a *target*, not as a ceiling it enforces on manual edits.

---

## 8. What is *not* in the record

Everything on this list is recomputed by the engine from the record plus equipment plus active
effects. There is nothing to write, and a trainer that claimed otherwise would be lying:

| Shown as | Why it is absent |
|---|---|
| **Maximum health / maximum mana** | Derived from Endurance / Intelligence and level. A live session showing `72/72` and `125/165` contains no `72` or `165` as a stored maximum anywhere in the record. Current health *can* exceed it: writing 500 made the screen read `500/72`. |
| **Damage `2-4`, Armor `1`** | Summed from the wielded weapon and worn armour, scaled by the relevant skill. |
| **Resistances (magic, poison, paralysis, disease)** | Derived — the attribute tooltips say so outright: Endurance affects resistances, Intelligence affects magic and paralysis resistance, Personality affects paralysis resistance. The screen's caps (*Maximum is 80%* for magic, *95%* for poison) are applied at display time. |
| **Outfit** (`Threadbare (6)`) | `FUN_004ed7f0` sums what is worn; `FUN_004ed780` turns the total into a word: `<11` Threadbare, `<21` Shabby, `<41` Plain, `<61` Regular, `<81` Dressy, `<91` Well dressed, `≤95` Fashionable, `>95` Swell. |
| **The displayed attribute and skill numbers** | Base + racial + equipment, as above. |

The reputation ladder, by contrast, *is* a pure function of the stored fame word
(`FUN_004ed5b0`), so the trainer reproduces it exactly:

```
+100 Saint | +80..+99 Blessed | +50..+79 Blameless | +20..+49 Virtuous | +1..+19 Good
   0 Neutral
 -1..-19 Immoral | -20..-49 Corrupt | -50..-79 Evil | -80..-99 Pure evil | -100 Demonic
```

Note the asymmetry at the ends: only exactly ±100 gets the extreme word.

---

## 9. Races

The race id is bounds-checked against a **six**-entry table, and `SRaces.cpp`'s string table is

```
Creature, base_rasvim, base_etherim, base_seiry, base_derth, base_nogur
```

so id 0 is the engine's placeholder for non-player creatures and the five playable races are 1..5.
The probed character reads race id **4** and the game shows *Race: Derth* — which is what fixes the
mapping rather than leaving it as a guess.

| id | Race |
|---|---|
| 0 | Creature (NPCs and monsters) |
| 1 | Rasvim (the undead) |
| 2 | Etherim |
| 3 | Seiry |
| 4 | Derth |
| 5 | Nogur |

Character *class* is a separate matter. Six classes exist in `SClasses.cpp` (fighter, thief, mage,
battlemage, ranger, priest) and the class determines which six skills are "primary", but the status
screen never shows it and no field in the record was confirmed to hold it. It is left undocumented
rather than guessed.

---

## 10. The save file, partially decoded

Saves are `%USERPROFILE%\Saved Games\The Quest\Save`*n*`.save` (about 130 KB each). The trainer does
**not** read or write them — this section exists because the save independently corroborates the
in-memory field order.

```
0x00  char[32]   file name, NUL-padded ("Save0.save")
0x20  ...        header words, including two equal 32-bit timestamps at 0x24 and 0x28
0x40  "ThQS"     magic
0x4C  u16 BE     directory entry count (459 in the probed save)
0x4E  u32 BE     0x00000EA8 — end of the directory area
0x52  entry[]    8 bytes each: u32 BE record id, u32 BE file offset
```

The directory is **big-endian**; record payloads are little-endian. 458 of the 459 entries point at
plausible payloads; the last one does not, and reads as a terminator. Every record id observed was
in the 3000–3999 range.

The character lives in record **3460** in the probed save, and it is a flat serialisation of exactly
the fields §5 lists, in the same order, with the strings written as NUL-terminated text rather than
as `std::string` objects and the constant experience table omitted:

```
… 67 00 00 00 | 00 00 00 00 | 00 00 00 00 | 32 59 00 00 | 63 0B 00 00
"Gerth the Derth\0" "bres_head00_racederth\0"
01 00 | 48 00 | 7D 00 | 05 00 | 00 00 | 63 0B 00 00   ← ?, health 72, mana 125, level 5, pad, exp 2915
00 00 00 00 | 00 00 00 00 | A0 0F 00 00               ← next level 4000
01 0A 00 00 | 00 00 00 00 | 00 00 00 00 | 04 00 00 00 ← gold 2561, race 4
… 17 00 ×5 | 14 00 00 00                             ← attributes 23, points 20
```

A save editor is a different tool and is deliberately out of scope. Note that The Quest autosaves
aggressively, so anything the trainer writes reaches disk on its own.

---

## 11. What was confirmed against the live game

Session: `TheQuest.exe` v1.9.10, PID 40288, module at `0x00260000`, character *Gerth the Derth*
(Derth, level 5).

| Claim | How |
|---|---|
| Module resolution and header parsing | base `0x00260000`, `IsWin32X86`, not a DLL, ASLR on, stamp `0x5E57BD07` matching the documented build |
| Chain A finds the record | `0x041FD098` = engine `0x041F92D0` + `0x3DC8` |
| Chain B finds the same record | same address, 1 surviving candidate, 954 ms |
| The prototype is rejected | the second signature hit at `0x041F99C0` fails on empty name and zero next-level |
| Every read field | name, portrait, race, level 5, exp 2 915, next 4 000, health 72, mana 125, gold 2 561, crime 0, fame 0, 20 attribute points, 40 skill points, all five attributes 23 |
| Every skill id and base value | all twenty matched the skills screen once the Derth's +10 magic bonuses were accounted for |
| The experience table | 98 entries, `400 …  215 990 000` |
| Writes take effect in the game's own UI | health 500 → status screen showed `500/72`; a skill written to 100 → skills screen showed `100` |
| Every write path round-trips | gold, health, mana, crime, fame, a skill, an attribute, skill points and the three-field level write were each set to a test value, read back, and restored to the original — all matched |
| The level write stays consistent | level 12 → experience raised to 60 000 (`table[10]`) and next-level rewritten to 90 000 (`table[11]`) |

The session was left exactly as it was found.

---

## 12. What the trainer deliberately does not do

- **Inventory and equipment.** The item list is a separate object graph that was not traced. Damage,
  armour and outfit all come from it, so leaving it alone also means the trainer never contradicts
  what the status screen computes.
- **Position and teleporting.** The map/coordinate fields were not located, and moving a party
  between levels in a game with `SDungeonWorld`/`SDungeonMap` teardown is the kind of thing that
  desynchronises quietly rather than loudly.
- **Spells and quest flags.** Both are script-driven (`SEngineRun.cpp`); the game's own dialog and
  quest system is the supported route.
- **Save editing.** §10 is far enough to be interesting and nowhere near far enough to be safe.
- **Maximum health and maximum mana.** They do not exist as fields (§8). Raise Endurance or
  Intelligence, or freeze the current value.

---

## 13. Reproducing this

Ghidra refuses a project path containing a dot-prefixed element, so the project was built outside the
repository and the executable copied to a plain path first:

```powershell
Copy-Item "C:\Program Files (x86)\GOG Galaxy\Games\The Quest\TheQuest.exe" C:\GhidraWork\

& "$env:ProgramData\chocolatey\lib\ghidra\tools\ghidra_12.1.2_PUBLIC\support\analyzeHeadless.bat" `
    C:\GhidraWork tq -import C:\GhidraWork\TheQuest.exe `
    -processor "x86:LE:32:default" -cspec windows
```

Analysis takes about five minutes. After that, a small `GhidraScript` that decompiles every function
referencing a given address does all the work; the addresses worth starting from (at the preferred
image base) are:

| Address | String / global |
|---|---|
| `0x006FDAFC` | `You don't have enough gold.` → the gold offset |
| `0x00711994` | `Experience:` → the whole status panel |
| `0x007119EC` | `Crime:` |
| `0x00711AC0` | `%u-%u` → the damage panel and the attribute "+" buttons |
| `0x004ED5B0` | the reputation-word helper → fame at `this + 0x3D0` |
| `0x004ED780` | the wardrobe-word helper |
| `0x00735790` | the engine-object pointer in `.data` |

A useful shortcut for finding more: the game's string table is one contiguous run in `.rdata`, and a
raw file offset converts to a Ghidra address with `VA = fileOffset + 0x1600 + 0x400000`.

`.docs/` and `.data/` are git-ignored (`.*/` in the root `.gitignore`) — RAM dumps, Ghidra projects
and probe scripts live there and are never committed.

---

## 14. Open ends

- `+0x004 … +0x00F` and `+0x044`, `+0x050`, `+0x054`, `+0x05C`, `+0x060`, `+0x1EC`, `+0x3D2` are all
  unidentified. Most read zero.
- `+0x010` mirrors experience in every observation and the save writes it too, but nothing was found
  that reads it. The strings `You have lost %ld experience.` and `%ld experience has been restored.`
  suggest a drain/restore mechanic that would need somewhere to keep the original — that is a guess.
- `+0x1F4`, attribute slot 0, read `15` rather than 0. Harmless, but unexplained.
- The character class field was not identified (§9).
- The save directory's `u32` at `0x4E` is six bytes larger than `0x52 + 458 × 8`; the discrepancy is
  not understood and the last directory entry is bogus. Both facts are consistent with a terminator
  the parser here simply skips.
