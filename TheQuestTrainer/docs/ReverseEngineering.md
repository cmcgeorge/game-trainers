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
| `+0x320` | vector | **carried items**; §15.1 | the encumbrance sum |
| `+0x334` | `SItem*[14]` ×2 | **equipment**, two weapon sets; §15.5 | the shop's *(equipped)* label |
| `+0x3A4` | `u8` | which weapon set is live | — |
| `+0x3B4` | vector | **diseases** — pointers to shared types; §16.4 | the "are you diseased" helper |
| `+0x3D0` | `i16` | **fame**, −100..+100 | the reputation helper reads `this + 0x3d0` |
| `+0x3D2` | `i16` | unknown, read `0` | — |
| `+0x3D4` | `u32` | **crime** (the outstanding bounty) | `"%u"` next to *Crime:* |
| `+0x3D8` | `u32` | **race id** | `if (5 < …) throw out_of_range` over a six-entry table |
| `+0x3E8` | `SEngine*` | back-pointer to the engine object | the disease-type lookup goes through it |
| `+0x404` | vector[25] | **active effects**, one vector per group; §16.2 | the "strip one source" loop |
| `+0x530` | `u32[]` | effect kind → group; §16.2 | the cure's own indexing |

The trainer snapshots the first `0x400` bytes, which covers every scalar above. The three structures
past that — the item vector, the effect groups and the two tables — are read separately and have
their own layout classes, because they are vectors of heap pointers rather than fields.

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
| **Resistances (magic, poison, paralysis, disease)** | Derived — the attribute tooltips say so outright: Endurance affects resistances, Intelligence affects magic and paralysis resistance, Personality affects paralysis resistance. The screen's caps (*Maximum is 80%* for magic, *95%* for poison) are applied at display time. What *is* stored is the modifier each worn item, race or spell contributes, as an effect in the groups §16.2 describes — but the total on the screen is summed from those, not held anywhere. |
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

- **Moving equipment.** The trainer reads the equipment slots and shows what is worn, but never
  writes them — see §15.5.
- **Teleporting to another map.** Within the map you are standing on it is two writes and the engine
  does the rest (§17); across a map boundary it is not, because the engine reassigns the current map
  only from its own movement code. §17.6 has the experiment.
- **Spells and quest flags.** Both are script-driven (`SEngineRun.cpp`); the game's own dialog and
  quest system is the supported route.
- **Save editing.** §10 is far enough to be interesting and nowhere near far enough to be safe.
- **Maximum health and maximum mana.** They do not exist as fields (§8). Raise Endurance or
  Intelligence, or freeze the current value.
- **Removing an effect that is not an affliction.** The cure takes exactly the three sources the
  game's own cures take (§16.3). Stripping a racial penalty or an item's downside would be one line
  more code and a different tool — the game re-derives both, so it would either be undone or take
  something with it.

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
| `0x0070F29C` | `Poisoned: -%u health per turn, until cured.` → all four conditions at once (§16.1) |
| `0x004EF590` | the cure every "Cure poison" / "Remove curse" ends in |
| `0x004EF1A0` | "strip every effect from this source" → the whole group array |
| `0x004C7570` | the script VM's command table — every command, its arguments and its opcode (§17.1) |
| `0x0055D2E0` | Recall → the world, the current map and the world-absolute tile pair |
| `0x004C6590` | the world-absolute position update → the whole window/local/global conversion (§17.4) |
| `0x00558B20` | the local→window helper → the flag that decides where a map sits in the window |

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
- Most of what §5 once listed as an unidentified gap has since been accounted for: `+0x320`
  onwards is the pack and the equipment (§15), `+0x3B4` is the disease list and `+0x404` onwards the
  effect groups (§16). What is still unread there is `+0x278 … +0x31F`, `+0x3A8`, `+0x3C0 … +0x3CF`
  and `+0x3DC … +0x3E7`; `+0x3A8` and `+0x3C4` are both plainly vectors, and neither was chased.

---

## 15. The item graph

Traced after the rest of this document, which is why the trainer went without an inventory tab for a
while. It turned out to be smaller than expected: the interesting structure is one `std::vector`, one
12-byte object and one shared, read-mostly type.

### 15.1 The pack is a vector inside the character record

The encumbrance warning gives it away in four lines. `FUN_0056bdc0` — the function holding
`You carry way too much - you can't move.` — sums what the character is carrying:

```c
end   = *(void **)(engine + 0x40ec);
begin = *(void **)(engine + 0x40e8);
for (p = begin; p != end; p++)
    load += *(ushort *)(*p + 0x32);        // item -> type -> weight
```

That is an ordinary `std::vector<SItem*>`, and `0x40E8 - 0x3DC8` puts it at **`record + 0x320`** —
inside the `+0x278 … +0x3D0` gap §14 used to list as unidentified. `+0x324` is `end` and `+0x328` is
the capacity.

The number of items is `(end - begin) / 4`. There is no inventory slot count: the game caps what you
carry by weight, not by slots, so an empty pack is `begin == end` and a full one is however many the
player has picked up.

### 15.2 An item is three fields

Consecutive item objects in a live session sit `0x18` apart, which is a 16-byte allocation plus the
usual eight-byte heap header. Only twelve of those bytes are ever touched by the game's own code:

| Offset | | |
|---|---|---|
| `+0x00` | `SItemType*` | the shared type — the whole identity of the item |
| `+0x04` | vector* | the item's own enchantments, or 0 to inherit the type's |
| `+0x08` | `u16` | the one mutable word: condition, charges or a unit count |

`+0x0A` onward is slack. It reads zero in items allocated out of fresh heap pages and holds
leftovers in ones from recycled blocks — in the probed session the four Waters, all of the same type,
held 8, 32, 8 and 48 there — and nothing in the disassembly reads it.

**The type is where everything else lives**, which is why "give the player an item" is a pointer
write and not an allocation. Two Loaves of Bread are two 16-byte objects pointing at one
`base_com_bread`.

### 15.3 The item type

`FUN_00508790` is the item panel — the function printing `Damage: %u-%u`, `Armor: %u`,
`Weight: %u.%u` and `Condition: %u/%u` — and it reads a type through `ECX` and an item through its
first argument. `FUN_00508550` next door builds the displayed name. Between them they account for the
whole object, which is `0x50` bytes:

| Offset | | |
|---|---|---|
| `+0x00` | `SEngine*` | back-pointer to the engine object — see §15.6 |
| `+0x04` | vtable | in the image's read-only data |
| `+0x08` | `char*` | internal id, e.g. `base_shield_smallwooden` |
| `+0x10` | `char*` | resource id, e.g. `bres_helm_helm` |
| `+0x14` | `char*` | **the displayed name**, e.g. `Small Wooden Shield` |
| `+0x28` | vector* | the type's built-in enchantments |
| `+0x32` | `u16` | weight, in hundredths — printed as `w/100 . (w%100)/10` |
| `+0x36` `+0x38` | `u16` | damage, minimum and maximum |
| `+0x3C` | `u16` | enchant storage |
| `+0x3E` | `u16` | **full condition** |
| `+0x45` | `u8` | category, 1..15 |
| `+0x46` | `u8` | sub-type within the category |
| `+0x47` | `u8` | required alignment: 1 good, 2 evil, 0 either |
| `+0x48` | `u8` | flags; bit 1 marks a category-1 weapon as *light* |

The category and sub-type names are not guessed. The executable indexes two tables by them, at
RVAs **`0x2DDAF0`** (category) and **`0x2DDAB0`** (per-category sub-type), and reading those in the
live process prints the game's own vocabulary:

```
 1 Weapon        hand, short sword, long sword, mace, axe, hammer, club, magicstaff,
                 throwing, short bow, long bow, quiver, crossbow, bolt quiver
 2 Heavy armor   Shield, Armored pants, Armor, Helm, Gauntlets, Boots, Cloak, Belt
 3 Light armor   (the same list)
 4 Accessory     Amulet, Ring
 5 Book          Book, Letter, Map
 6 Alchemy equipment   Mortar/pestle
 7 Ingredient    Ingredient
 8 Potion        Potion
 9 Magic         Scroll, Spellbook, Blank scroll, Wand, Empty wand
10 Money         Money
11 Key           Key, Lockpick
12 Repair        Hammer
13 Miscellaneous
14 Comestible    Food, Water
15 Gem           Gem
```

Those are transcribed into `Game/ItemTables.cs` rather than read live, on the same grounds as the
skill names in `GameTables`: the trainer already has everything it needs from the type itself, and a
table of nouns is not worth two more RVAs to depend on.

### 15.4 One word, three meanings

`+0x08` on the item is read as a different thing depending on the type, and the item panel decides
which in a way worth reproducing exactly:

| What the panel prints | When |
|---|---|
| `Condition: %u/%u`, against type `+0x3E` | categories 2, 3, 6, 12; category 1 except sub-types 8, 11 and 13; category 11 sub-type 2 |
| `Contains %u units` | category 1 sub-types 8, 11, 13 — throwing weapons, quivers and bolt quivers |
| `(%u/%u charges)` | category 9 sub-types 4 and 5 — wands |

The wear ladder is the panel's own: under 10 % `broken`, under 30 % `poor`, under 70 % `average`,
under 100 % `good`, and only a full 100 % `perfect`.

A wand's ceiling is not in the type. `FUN_00508430`, the game's own recharge, reads it from the
enchantment:

```c
v = item->enchantments;  if (!v) v = type->enchantments;
if (v is non-empty)  item->charges = *(u16 *)(v->begin[0] + 4);
```

so the trainer's "recharge" ends by writing the same word from the same place.

**The division matters.** The panel computes wear as `condition * 100 / type->maxCondition`. A type
whose category shows a condition but whose maximum is zero would divide by zero the moment the
player looked at it. No shipped type is like that — all 1,084 pass — and `ItemCatalog.CanReplaceWith`
is what keeps it that way when the trainer stamps a type onto an item.

### 15.5 Equipment is two arrays of pointers, and the trainer does not write them

There is no "equipped" flag on an item. The shop's `(wielded)` / `(equipped)` label
(`FUN_005cba60`) decides by *searching* two arrays for the item's pointer, and `FUN_0057b420` gives
their bases exactly:

```c
if (*(char *)(engine + 0x416c) == '\0')  item = *(void **)(engine + 0x40fc + slot * 4);
else                                     item = *(void **)(engine + 0x4134 + slot * 4);
```

Two arrays of fourteen slots and a byte selecting between them, which in record terms is
**`+0x334`**, **`+0x36C`** and **`+0x3A4`**. Slot 0 is unused — the same "id 0 means none" convention
the attribute and skill arrays follow. In the probed session a Helm sat in slot 1, a Small Wooden
Shield in slot 6 and Hard Leather Boots in slot 11.

**Which slot takes which kind of item was not established**, and that is the reason the trainer shows
equipment but never moves it. Equipping by writing a raw pointer would also bypass the paperdoll and
model updates the game does around it, and for the same reason `TrainerActions.ReplaceItem` refuses
an equipped item: retyping in place would leave a body slot holding something the game never put
there. Unequipping in the game first costs one click.

### 15.6 Finding every item type without knowing an address

The game does keep an indexed table — `FUN_00507cc0` resolves a saved item through
`manager->[0xEC][id]` — but reaching that manager means a second static pointer and another chain to
be wrong about on a patch. It is not needed. An item type is recognisable from its own bytes, and the
strongest signal is the one the trainer already has: **`+0x00` is the engine object**, which the
locator found before any of this ran.

So the catalog is a sweep — find every dword in the heap equal to the engine address, then test what
follows it for a module vtable, a category in 1..15, and an id and name that are readable, non-empty,
printable C strings. In the probed session that is one pass and **268 ms** for **1,084 item types**,
across every category from `base_weap_dagger` to the expansion's `isle_repair_hammermaster2`.

The obvious false positive is the module's own `.data` slot at RVA `0x335790`, which holds the engine
pointer too; the vtable check rejects it, and `test/FormatCheck` plants a decoy that differs from a
real type in nothing else so that rule cannot quietly stop being load-bearing.

### 15.7 Confirmed against a live session

Same session as §11 — `TheQuest.exe` v1.9.10, PID 40288, module at `0x00260000`, *Gerth the Derth*.

| Claim | How |
|---|---|
| The pack is at `record + 0x320` | 18 items read, names and weights matching the game's own inventory |
| Weight is summed as the encumbrance check does | 20.6 total across 18 items |
| The meter decodes per category | Fur Boots `2853/3000` "good", Small Wooden Shield `711/1500` "average", Helm `1539/2500` "average"; books, potions and bread show none |
| Equipment is found by searching both arrays | Helm slot 1, Shield slot 6, Boots slot 11, all in set 0 |
| The catalog sweep | 1,084 types in 268 ms; all 1,084 placeable |
| Repair writes the game's own ceiling | Fur Boots `2853 → 3000`, read back "perfect" |
| Retyping an item works | Fur Boots → King's Longsword, condition `40000/40000` |
| An equipped item is refused | the Shield came back with the "unequip it in the game first" refusal |
| An address no longer in the pack is refused | a synthetic pointer was rejected rather than written |
| The session survives it | every item's type and meter compared identical to the pre-test read |

The session was left exactly as it was found. **Not confirmed visually**: the game was minimised
throughout, so no screenshot was taken of its own inventory screen showing a retyped item. Every
field above is read by `FUN_00508790` each time it draws, so this is a gap in the evidence rather
than a doubt about the mechanism — but it is a gap.

### 15.8 Open ends in the item graph

- Item `+0x0A` and `+0x0C` are slack as far as anything in the disassembly is concerned, but that is
  an argument from absence. If a field turns up there, this is where it would be.
- The equipment slot *numbering* (§15.5) is unmapped beyond the three observed. Establishing it is
  what an equip/unequip control would need.
- The item-type manager off `manager->[0xEC]` was found but not walked; the sweep made it
  unnecessary. It would give item type *ids*, which is what a save editor would want.
- Enchantments are read only far enough to find a wand's charge ceiling. The vector's entries have a
  `u16` at `+4` and a byte at `+0x0D` the book code reads as a skill; the rest is untraced.

---

## 16. Conditions: poison, disease, curse and paralysis

Traced last, and the shape is the same idea as the item graph one level up: nothing about a condition
is a flag. Poison, curse and paralysis are **lists of effect objects**, disease is a list of pointers
to shared types, and the character record holds all of it in the region after `+0x400` that §5 stops
at.

### 16.1 One function names all four

`FUN_00538cf0` builds the character screen's condition tooltips, and it does the whole job in one
pass — which makes it the single most useful function in this section, because it shows what the game
considers a condition *and* where each one lives:

```c
iVar5 = *(int *)(in_ECX + 0x44);                      // the engine object

for (p = *(int **)(iVar5 + 0x42e0); p != *(int **)(iVar5 + 0x42e4); p++)
    total += *(short *)(*p + 8);                      // poison, summed
FUN_0043e8a0(…, "Poisoned: -%u health per turn, until cured.", total);

cVar1 = FUN_004ed330(0);                              // any disease at all?
FUN_0043e4b0("Diseased: you'll suffer negative effects until cured.…");

for (p = *(int **)(iVar5 + 0x42d4); p != *(int **)(iVar5 + 0x42d8); p++)
    if (turns < *(int *)(*p + 0xc)) turns = *(int *)(*p + 0xc);
FUN_0043e8a0(…, "Cursed: your attack power has been reduced.\r%i turns left.", turns);

for (p = *(int **)(iVar5 + 0x42c8); p != *(int **)(iVar5 + 0x42cc)) …
FUN_0043e8a0(…, "Paralyzed: you cannot attack or move.\r%i turns left.…", turns);
```

Four conditions, four `std::vector`s, and the icon for each is turned on by the same test the tooltip
uses. There is no fifth: the resource ids in the image are exactly `controls/game/icon-poisoned`,
`icon-diseased` and `icon-cursed`, and paralysis has the panel entry above.

Subtracting `RecordInEngine` (`0x3DC8`) from those engine offsets puts them in the record:

| Engine | Record | |
|---|---|---|
| `+0x417C` | `+0x3B4` | diseases — `std::vector<SDiseaseType*>` |
| `+0x42C8` | `+0x500` | paralysis effects |
| `+0x42D4` | `+0x50C` | curse effects |
| `+0x42E0` | `+0x518` | poison effects |

`FUN_004eddd0` — the "are you poisoned" helper the rest of the game calls, including the one that
refuses to let you rest — reads `this + 0x518` / `+0x51C` and sums `*(short*)(*p + 8)`, which is the
same arithmetic against the record rather than the engine. That is what fixes `ECX` as the record and
not the engine object.

Note what the poison test is: **the sum of the magnitudes must be positive**, not "the list is
non-empty". `ConditionReader` reproduces that, so a poison that nets to nothing reads as no poison
rather than as a poison of zero.

### 16.2 The effect groups are one array, and a table says which is which

The three vectors are 12 bytes apart, and they are not special. `FUN_004ef1a0`, which strips every
effect from one source, gives the whole array away:

```c
piVar3 = (int *)(in_ECX + 0x410);          // group 1's begin
local_4 = 1;
do {
    … erase every entry whose *(char *)(effect + 0x11) == param_1 …
    local_4 = local_4 + 1;
    piVar3 = piVar3 + 3;                   // 12 bytes on
    if (0x18 < local_4) return;            // stop after group 24
} while (true);
```

So there are **25 `std::vector<SEffect*>` in a row at `record + 0x404`**, indices 0..24, and the game
itself only ever walks 1..24 — the same "id-indexed with an unused slot 0" convention the attribute,
skill and equipment arrays follow.

Which group holds which kind of effect is a table, not a constant. `FUN_004ef590` — the function
every "Cure poison", "Remove curse" and "Cure paralysis" ends in — is called as `FUN_004ef590(0x1a)`
for poison and `FUN_004ef590(0x1c)` for paralysis, and starts:

```c
iVar3 = *(int *)(in_ECX + 0x530 + param_1 * 4) * 3 + 0x101;
piVar2 = (int *)(in_ECX + iVar3 * 4);      // &group[table[kind]].begin
```

`(x * 3 + 0x101) * 4` is `x * 12 + 0x404`, so the table at **`record + 0x530`** maps an effect kind
onto a group index — and it abuts the group array exactly, since group 24's vector ends at `0x530`.
The trainer reads that table rather than baking in a group number, because the game does; a harness
check moves poison to another group and expects the reader to follow.

The table read out of the live session, which is also the list of effect kinds the game has:

| kind | group | what it is | kind | group | what it is |
|---|---|---|---|---|---|
| `0x01` | 1 | skill modifier | `0x19` | 24 | serious disease |
| `0x02` | 2 | attribute modifier | `0x1A` | 23 | **poison** |
| `0x05` | 3 | health over time | `0x1B` | 22 | **curse** |
| `0x08` | 4 | unholy health | `0x1C` | 21 | **paralysis** |
| `0x0A` | 5 | mana over time | `0x21` | 12 | disease resistance |
| `0x0B` | 6 | armor | `0x22` | 13 | — |
| `0x0F` | 7 | poison resistance | `0x23` | 14 | — |
| `0x10` | 8 | paralysis resistance | `0x24` | 15 | magic |
| `0x11` | 9 | named resistance | `0x26` | 16 | melee |
| `0x14` | 10 | feather | `0x27` | 17 | magic immunity |
| `0x16` | 11 | magic resistance | `0x2A` | 18 | — |
| `0x12` | — | normal weapon resistance | `0x39` | 19 | — |
| | | | `0x3A` | 20 | outfit |

Entries past `0x3A` are not group numbers — the dwords there read in the millions, so the table ends
where the kinds do.

### 16.3 The effect object

Twenty bytes, and the whole of it is accounted for by one allocation site. `FUN_004ead80`, which is
what a tavern's drink does, builds one field by field:

```c
puVar2 = operator_new(0x14);
*puVar2 = 0;                                    // +0x00
*(undefined2 *)(puVar2 + 4) = 0x213;            // +0x10 = 0x13, +0x11 = 0x02
puVar2[1] = param_2;                            // +0x04
*(short *)(puVar2 + 2) = (short)iVar9;          // +0x08
puVar2[3] = iVar10;                             // +0x0C
*(undefined1 *)((int)puVar2 + 0x12) = 0;        // +0x12
```

| Offset | | |
|---|---|---|
| `+0x00` | ptr | a heap buffer freed alongside the effect; read zero in every effect observed |
| `+0x04` | `u32` | the type key — what a resistance or a disease is looked up by |
| `+0x08` | `i16` | **magnitude**: health per turn for poison, the percentage for a resistance, the modifier for an attribute |
| `+0x0C` | `i32` | **turns remaining**; zero for the ones that last until cured |
| `+0x10` | `u8` | the group it is filed under |
| `+0x11` | `u8` | **the source** — see below |
| `+0x12` | `u8` | the attribute or skill id, for groups 1 and 2 |

`+0x0A..+0x0B` is padding, and the game's own `operator delete` states the size as `0x14`.

**The source byte is the interesting one**, because it is what decides whether a cure may take the
effect away:

| source | granted by | rebuilt when |
|---|---|---|
| 1 | equipment | `FUN_004ece50` strips source 1 and re-applies from the worn slots whenever equipment changes |
| 2, 3, 6 | a spell, a potion, an affliction | nothing — these are what a cure removes |
| 4 | a disease | `FUN_004ef880` strips source 4 and re-applies from the disease list whenever it changes |
| 5 | the character's race | nothing; it is stamped once |

and `FUN_004ef590` removes exactly `{2, 3, 6}`. Everything else is re-derived by the game from
something that still exists, so removing one would either be undone on the next recalculation or take
away something a cure was never meant to touch.

The live session is the clean demonstration. *Gerth the Derth* held, with no potions active:

```
group  1: mag +10 src 5 sub 8    mag +10 src 5 sub 11   mag +10 src 5 sub 10
group  2: mag  -5 src 5 sub 1    mag  -5 src 5 sub 2    mag  -5 src 5 sub 3    mag +10 src 5 sub 4
group 11: mag +130 src 5
group 23: mag  +2  src 6
```

Group 2 is the Derth's `-5 Strength, -5 Dexterity, -5 Endurance, +10 Intelligence` (attribute ids
1, 2, 3, 4) and group 1 is its `+10 Healing Magic, +10 Mind Magic, +10 Attack Magic` (skill ids 8,
11, 10) — the racial block §7 documents, arriving independently through a completely different
structure. Group 11 is magic resistance, which the screen caps and displays as `+30%`. Group 23 is
the poison, the only entry in the record with a curable source.

### 16.4 Disease is a list of shared types

`FUN_004ed330` is "are you diseased", optionally by id: an empty argument returns
`begin != end` and a non-empty one `strcmp`s the id of each element. Curing (`FUN_004ed250`) finds
the element, erases it, and then calls `FUN_004ef880`, which is `FUN_004ef1a0(4)` — strip every
disease-granted effect — followed by re-applying from whatever diseases remain.

The elements are pointers to shared `SDiseaseType` objects out of a table hanging off
`record + 0x3E8` (the record's own back-pointer to the engine), at `+0x28B8`/`+0x28BC` — so the
character never owns them, and emptying the list costs nothing. The type's name is at `+0x08`: it is
the `%s` in `You have been cured of %s.` and in the active-effects list's `%s disease`.

That split is why curing a disease is two separate jobs, and why doing only the first would be a bug:
the list is borrowed pointers, but the penalties the disease granted are ordinary allocated effects
sitting in the groups above, and nothing re-derives them on its own.

### 16.5 What the trainer does, and the one thing it cannot do

`TrainerActions.CureConditions` is `FUN_004ef590` and `FUN_004ef880` written out, minus one thing:

- For poison, curse and paralysis: read the kind table, read the group, erase every entry whose
  source is 2, 3 or 6, compact the pointer array, shorten `end`.
- For disease: write `end = begin` on the list, then strip every source-4 effect from groups 1..24.

**The one thing it cannot do is the `delete`.** The trainer has no safe way to free a block in the
game's heap, so each cured effect leaks its twenty bytes. Nothing is left dangling — nothing is
freed, so no pointer can go stale — and the vector's own buffer is untouched and still the game's to
release; `begin` and the capacity are never written. The cost is twenty bytes per effect removed, in
a game that allocates and frees these constantly.

The survivors are written *before* the vector is shortened, deliberately. In the instant between the
two writes the vector holds one duplicated pointer rather than a short vector with a removed effect
still inside it, so a game reading it mid-cure sees an effect twice for a frame instead of seeing the
poison it was told had gone.

### 16.6 Confirmed against a live session

Same session again — `TheQuest.exe` v1.9.10, PID 40288, module at `0x00260000`, *Gerth the Derth*,
who was poisoned at the time and down to 10 health.

| Claim | How |
|---|---|
| The kind table is where §16.2 says | `record + 0x530` read `0x1A → 23`, `0x1B → 22`, `0x1C → 21`, and 21 further kinds in the same shape |
| The groups tile from `record + 0x404` | groups 1, 2, 11 and 23 non-empty at `+0x410`, `+0x41C`, `+0x488`, `+0x518`, exactly `0x404 + 12n` |
| The effect fields decode | the poison read magnitude 2, duration 0, source 6 — matching the game's own *Poisoned: -2 health per turn, until cured* |
| The source byte separates affliction from anatomy | the eight racial effects all read source 5; the poison alone read a curable source |
| The trainer's reader agrees with the game | `Poisoned — 2 health per turn`, nothing else adverse |
| The cure works | *Cured poison.* — group 23 went from one element to `begin == end`, with `begin` and the capacity unchanged |
| It touches nothing else | the first `0x400` of the record compared byte-for-byte identical afterwards; the pack, all 18 items, and every attribute and skill unchanged |
| The racial modifiers survive it | groups 1 and 2 still held 3 and 4 effects, all source 5 |
| A second cure is a no-op | *Nothing adverse to cure.*, with no writes |
| Read-only refuses it | the refusal came back and the group was unchanged |
| The freeze is the same cure on repeat | one `FreezeWriter.Tick` reported one write and left the character clear |
| The window draws it | driven headless: all four tabs laid out, the Conditions box reading *Poisoned — 2 health per turn* before and *None.* after, the Cure button bound and correctly disabled once clean, no binding errors |

**Not confirmed against a live game**: disease, curse and paralysis. The character was poisoned and
nothing else, and inducing the other three would have meant playing the session rather than observing
it. All three are read and cured by the same code the poison exercised, through structures pinned by
the same disassembly, and `test/FormatCheck` covers each of them — but that is a fixture, not a game.
Disease is the one to be most careful about: it is the only condition whose cure has a second half
(the source-4 strip), and that half has never run against a real disease.

**Not confirmed visually**: the game was minimised throughout, so no screenshot was taken of its own
character screen with the poison icon gone.

### 16.7 Open ends

- The byte at effect `+0x10` matched the group index in every effect observed, so the trainer treats
  it as such and never reads it. The active-effects screen (`FUN_004c02c0`) switches on a byte it
  reads four bytes lower on what looks like the same object; that container was not chased down, and
  it is the one place these notes could not be made to agree.
- Sources 2, 3 and 6 are all "a cure removes this", but what distinguishes them was not established.
  The tavern's drunkenness is source 2 and the observed poison was source 6.
- The remaining effect kinds in §16.2 with no description are named only by their group number;
  nothing needed them.
- `SDiseaseType` is read for its name and nothing else. Its own effect templates — the 16-byte
  entries `FUN_004ea3e0` scans for kind `0x19` to decide "seriously diseased" — are untraced.
- Nothing was found that expires an effect by counting `+0x0C` down, so it is not known whether
  writing a duration of zero would make the game free the object itself. If it does, that would be a
  cure with no leak at all, and it is the first thing to try.

---

## 17. Where the player is standing

Traced last, and the surprise was how little of it is in the character record: none of it. The record
is one member of the engine object, and the position hangs off two *other* objects the engine object
points at.

### 17.1 The chain, and how it was found

The whole thing came out of the script VM's command table. `SEngineRun.cpp`'s vocabulary is
registered in one function (`FUN_004c7570`) as {name, argument count, flags, argument description,
opcode} tuples, and the argument descriptions are unusually generous:

```
movepos        x,y                    opcode 0xA6
getposx                               opcode 0xA7
getposy                               opcode 0xA8
iscurrentmap   mapid                  opcode 0xA9
iscurrentworld worldid                opcode 0xAA
move           mapobjid               opcode 0xA3
```

So the game thinks in terms of a *map id*, a *world id*, and an x/y within a map. The template
variable `%Map%` is substituted from `[[[engine + 0x98] + 0x21CC] + 0x10]`, which pins two of the
three hops before any scanning.

The rest fell out of the Recall spell, `FUN_0055d2e0`:

```c
// this = the engine manager
if ((*(ushort *)(this->0x21CC) + 0x40) >> 10 & 1) -> "Teleport magic is denied on this map."
cellX = *(int *)(this->0x21C8 + 0x90) / 0x15;   // world-absolute tile / 21
cellY = *(int *)(this->0x21C8 + 0x94) / 0x15;
FUN_004c5070(cellX, cellY);                     // find the map at that cell
```

and `FUN_004c5070` — the map lookup — gives away the whole naming scheme:

```c
// this = the world
prefix = this->0xA0;                            // std::string, "base_s"
sprintf(name, "%s%02u%02u", prefix, x, y);      // "base_s0804"
// ... then a linear search of the vector at this->0x74 .. this->0x78
```

**The Quest's outdoor world is a grid of 21x21-tile maps whose ids spell out their cell.** Freymore
is 14x14 of them — 196 maps — plus 43 standalone 35x35 interiors with names instead of cells, 239 in
all.

The chain, then:

| | |
|---|---|
| `engine` | `record - 0x3DC8`, as everywhere else in this document |
| `engine + 0x98` | the **engine manager** (`SEngineManager`), the live game |
| `manager + 0x21C8` | the **world** (`SWorld`); its `+0x00` is the engine object |
| `manager + 0x21CC` | the **map** the player is on; its `+0x00`/`+0x04` are the engine and the world |

Those back-pointers are what the trainer validates against, exactly as `ItemCatalog` validates an
item type: two comparisons, and a pointer that survives them is the object it claims to be.

### 17.2 The world

| Offset | Type | What |
|---|---|---|
| `+0x00` | ptr | the engine object |
| `+0x08` | `std::string` | display name — `Freymore` |
| `+0x20` | `std::string` | resource pack — `base` |
| `+0x38` | `std::string` | id prefix — `base_` |
| `+0x54` | `std::string` | database — `TheQuestBase` |
| `+0x74`/`+0x78` | ptr | `std::vector<SMap*>` begin/end — every map in the world |
| `+0x8C` | ptr | the map the player is on, mirroring `manager + 0x21CC` |
| `+0x90`/`+0x94` | i32 | world-absolute tile position |
| `+0x98` | u32 | game-time stamp of the last position update |
| `+0xA0` | `std::string` | grid prefix — `base_s` |
| `+0xBC` | `std::string` | map picture id — `base_-WORLDMAP-` |

### 17.3 A map

| Offset | Type | What |
|---|---|---|
| `+0x00`/`+0x04` | ptr | the engine object and the world |
| `+0x0C` | `char*` | internal id — `base_s0804`, `base_house7` |
| `+0x10` | `char*` | display name — `Port of Mithria` |
| `+0x2C`/`+0x30` | i32 | width and height in tiles — 21x21 outdoors, 35x35 inside |
| `+0x40` | u16 | flags |

The flags were read off the game's own branches rather than guessed:

| Bit | Mask | Read by | Meaning |
|---|---|---|---|
| 3 | `0x0008` | `FUN_0055d080` | Mark is denied here |
| 7 | `0x0080` | `FUN_00558b20` | the map is laid into the tile window a *border* in |
| 9 | `0x0200` | `FUN_0055d1c0` | Recall may bring the player back here |
| 10 | `0x0400` | `FUN_0055d080`, `FUN_0055d2e0` | Teleport magic is denied here |

Bit 7 is the load-bearing one, and it is set on every outdoor cell and clear on every interior.

### 17.4 The tile window, which is the whole trick

The engine does not address tiles by their place on the map. It keeps one square scratch grid and
loads the map — outdoors, a three-by-three block of maps — into it:

```c
// FUN_004cfa91, in the startup path
engine->0x44E8 = engine->0x66C;             // the border = the configured drawDistance
engine->0x44EC = engine->0x66C * 2 + 0x15;  // the window's side = 2 x border + 21
```

With the default `drawDistance=14` that is a 49x49 window with a 14-tile border. The player's
position — `manager + 0x158C` and `+0x1590` — is an index into **that** grid, and the conversion is
one subtraction whose operand depends on bit 7:

```c
// FUN_00558b20 — the game's own local-to-window helper
if (map->0x40 >= 0)               { win = local; }                  // interiors: the window's origin
else if (map == manager->0x21CC)  { win = engine->0x44E8 + local; }  // outdoor cells: the border
else                              { ... per-slot rects for the eight neighbours ... }
```

and the reverse direction is written out in full by `FUN_004c6590`, which the engine calls whenever
the player moves:

```c
digits = map->0x0C + world->prefix.size();      // "base_s0804" + 6 -> "0804"
world->0x90 = (digits[0]*10 + digits[1] - 0x211) * 0x15 - engine->0x44E8 + winX;
world->0x94 = (digits[2]*10 + digits[3] - 0x211) * 0x15 - engine->0x44E8 + winY;
```

`0x211` is `'0'*10 + '0' + 1`, so **the cell in a map id is one-based** and the map's north-west
corner is at world tile `(column - 1) * 21`. Everything the trainer shows follows from that:

```
local  = window - (map is an outdoor cell ? border : 0)
global = (column - 1) * 21 + local
```

`manager + 0x1570` is the facing, in degrees anticlockwise from north: 0 north, 90 west, 180 south,
270 east. Turning right walks backwards through those four.

### 17.5 Teleporting is two writes

`manager + 0x158C` and `+0x1590` are read by the engine every frame. Writing them moves the player,
the camera, the compass and the automap together, within a frame; nothing has to be nudged
afterwards and nothing else has to be kept in step, because the world-absolute pair at `world + 0x90`
is recomputed from the window position by the code above.

That makes the trainer's teleport smaller than most of its other edits: read the map, convert local
to window, write two dwords.

### 17.6 ...but only within the map you are on

Outdoors the window holds the player's map *and its eight neighbours*, so a coordinate outside the
middle 21x21 is a real, drawn tile of a real neighbouring map. It renders correctly — and the engine
goes on believing the player is on the map they left, because `manager + 0x21CC` is only reassigned
by the engine's own movement code.

This was tried rather than assumed. Writing window X = 5 while standing on `base_s0804` put the
character nine tiles into the forest of `base_s0704`, drawn correctly, with the automap still showing
Port of Mithria and the world-absolute position computed from the wrong cell. Walking a step did not
repair it. So the trainer refuses any target outside the current map's own width and height, and says
why.

### 17.7 The world map picture

`world + 0xBC` names a resource, `base_-WORLDMAP-`, which is `worlds/base/-WORLDMAP-.dds` inside
`data.pak` — the paks are ordinary zip archives. It is a 588x588 DXT1 surface with no mipmaps, and
588 = 14 cells x 21 tiles x **2**, so it is a plan of the whole outdoor world at two pixels a tile,
aligned to tile (0, 0) with no offset. The trainer reads it out of the player's own install (found
from the attached process's own path), decodes the BC1 blocks itself because WPF cannot open DDS, and
draws the position on it. Nothing from the game is redistributed and everything on the tab works
without it.

### 17.8 Confirmed against a live session

Same session as §11 — `TheQuest.exe` v1.9.10, module at `0x00260000`, *Gerth the Derth* standing in
the Port of Mithria:

| | |
|---|---|
| manager | `[engine + 0x98]`, world `Freymore`, pack `base`, grid prefix `base_s` |
| map | `base_s0804` / `Port of Mithria`, 21x21, flags `0x0290` |
| window | side 49, border 14, outdoor flag set |
| position | window (25, 23) -> local (11, 9) -> world tile (158, 72) |
| the engine's own cached pair | `world + 0x90` = 158, `world + 0x94` = 72 — **the same two numbers** |
| atlas | 239 maps, 196 outdoor cells running to column 14, row 14 |
| picture | `worlds/base/-WORLDMAP-.dds`, 588x588, 2 px/tile |

That last row of the position block is the one worth keeping: the trainer derives the world-absolute
tile from the window position and the map id, and the engine maintains its own copy by a different
route. They agree, so neither is the same arithmetic checking itself.

Also confirmed by doing it:

- **Facing.** Pressing "turn right" four times walked `manager + 0x1570` 0 -> 270 -> 180 -> 90 -> 0,
  and a step at each of the four moved the position by -Y, +X, +Y, -X in turn.
- **Teleport.** Writing window (18, 30) moved the character from a cobbled street to a jetty over
  water on the far side of the same map, instantly, with the automap redrawn and an NPC nameplate
  picked up at the new spot. Writing the original pair back restored it exactly, and `world + 0x90`
  came back to 158/72 after one step.
- **Cross-map.** §17.6.

**Not confirmed against a live game: being indoors.** The session was outdoors throughout, and
getting inside would have meant playing it rather than observing it. Everything about an interior
here is read out of the disassembly and the shipped data: the flag branch is `FUN_00558b20`'s own
test, the 43 interiors in Freymore all carry bit 7 clear and all measure 35×35 in the atlas the
running game handed over, and the harness fixture covers the case — but a fixture is not a game. The
thing to watch is the one number that follows from the branch rather than from an observation: an
interior is laid at the window's *origin*, so its local tile is the window index unchanged. If that
is wrong, the readout and every teleport inside a building are out by the draw distance, and the
whole outdoor half of this section is unaffected.

**Not confirmed either: the expansion's world.** *Islands of Ice and Fire* ships its own PDB and its
own `-WORLDMAP-` in `expansions/isle.pak`, and the trainer reads the world's name, pack, grid prefix
and picture id out of the world object precisely so that none of them is baked in — but the session
never left Freymore, so the second world has only ever been read off disk, not out of memory.

### 17.9 Open ends

- The three-by-three block at `manager + 0x21D0` is read for nothing. It is what a cross-map teleport
  would have to maintain, along with `manager + 0x21CC`, `world + 0x8C` and the per-slot rects at
  `manager + 0x1F84`; that is the shape of the work if anyone wants to try.
- `manager + 0x21BC` is the window's own tile array, `WindowSize²` entries of 0x42 bytes. It holds
  what the automap draws and would be the way to render real terrain rather than tinting from the
  world map picture. Untraced.
- The world PDBs in `pdbs/` inside each pak (Palm databases — the game began on Palm OS, type `ThQW`,
  creator `ThQu`) hold the maps offline. Record 0 of `TheQuestBase.pdb` is
  `Freymore\0base\0TheQuestBase\0`, which lines up with `world + 0x08`/`+0x20`/`+0x54`, and the
  tagged records after it are items (`0x15`), map objects (`0x28`), NPCs (`0x2A`) and spells (`0x04`).
  The map records were not identified; the trainer reads the atlas out of the running game instead,
  which is both easier and correct for whatever expansion is loaded.
- `manager + 0x1574` mirrors the facing and is presumably the target angle for the turn animation.
  Reading it mid-turn is how a first attempt at the facing table got the wrong answer.
