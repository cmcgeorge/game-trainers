# Pool of Radiance — Reverse-Engineering Notes

Technical write-up of how *Pool of Radiance* (SSI, 1988 — the first AD&D "Gold Box" game)
stores its party in memory, how that was recovered from DOSBox-X memory dumps, and how the
trainer reads and writes it. Every offset in the character-record table below was confirmed
**two independent ways** — by differential analysis of live memory dumps of a real party, and
against community documentation — and the parser is regression-tested against verbatim bytes
from those dumps (`test/FormatCheck`).

---

## 1. Source material

| Artifact | What it is |
|---|---|
| `.data/dosbox-x-8168-20260708-081948-110.bin` (367 MB) | Full DOSBox-X **process** dump, party **exploring** the Slums (0,4), facing W, 10:50. |
| `.data/dosbox-x-8168-20260708-082313-212.bin` (352 MB) | Full DOSBox-X process dump, party **in combat**, Rhiannon **unconscious**, vs 6 Orcs (5 HP each). |
| `.data/*.csv` | Region index for each dump: `FileOffset, ProcessAddress, Size, Protection, Type`. |
| `.game/` | The game itself: `START.EXE`, `GAME.OVR`, `*.DAX` resource archives, `POOL.CFG`. |

These are dumps of the **DOSBox-X emulator process**, not of the guest (DOS) machine directly.
The emulated PC's RAM lives as a large committed block inside the emulator's address space, so
the game's data structures sit somewhere inside the `.bin` at an offset that changes every run.
The `.csv` maps each file offset to the process virtual address it was captured from, which lets
us report meaningful addresses and diff two dumps *by address* rather than by file offset.

---

## 2. Finding the party in the dump

The six party members are: **Thrender Grone, Bakshi, Rhiannon, Brother Sean, Darkstar, Phineas**.

Gold Box games store a character's name as a **Pascal string** (a length byte followed by up to
15 ASCII bytes), stored **in upper-case** as the game draws them. Searching the dump for the
length-prefixed name (e.g. `0E "THRENDER GRONE"`) locates each record immediately:

```
THRENDER GRONE : @ process address 0x1F1791489D8
BAKSHI         : @ 0x1F179148BE8
RHIANNON       : @ 0x1F179148D98
BROTHER SEAN   : @ 0x1F179148F88
DARKSTAR       : @ 0x1F179149168
PHINEAS        : @ 0x1F1791492C8
```

Crucially, the records sit at the **same process address in both dumps** (only the file offset
differs). The emulated RAM base is stable within a DOSBox session, which is why the trainer can
poll and freeze reliably once it has located a record. Across a DOSBox restart the base moves, so
the trainer never hard-codes an address — it **signature-scans** for the record shape instead
(see §6).

Immediately after each name are the six ability scores, so the anchor is unambiguous. The bulk of
the space *between* consecutive party members is the character's **combat-icon sprite** (bitmap
runs like `3F FF FC …`) and a **linked list of carried-item instances** (each item carries its own
name, e.g. `"Flail Flail"`, `"Banded Mail  Mail"`, `"Two-Handed Sword Sword"`). Because that trailing
data is variable-length, records are **not** at a fixed stride — another reason to locate them by
signature, not stride.

---

## 3. The character record — 0x11D (285) bytes

Each character (and each monster — see §5) is a fixed **285-byte** record. Offsets are relative to
the record start (the name-length byte). Fields the trainer edits are shown in **bold**.

| Offset | Size | Field | Notes |
|-------:|:----:|-------|-------|
| `0x00` | 1 | **name length** | Pascal string length (1–15) |
| `0x01`–`0x0F` | 15 | **name** | ASCII, NUL-padded |
| `0x10` | 1 | **Strength** | |
| `0x11` | 1 | **Intelligence** | |
| `0x12` | 1 | **Wisdom** | |
| `0x13` | 1 | **Dexterity** | |
| `0x14` | 1 | **Constitution** | |
| `0x15` | 1 | **Charisma** | |
| `0x16` | 1 | **Str exceptional %** | 1–100 → 18/01–18/00; 0 = none (fighters only) |
| `0x17`–`0x2B` | 21 | memorized spells | one slot per memorized spell |
| `0x2D` | 1 | **THAC0 base** | stored as `60 − value` (see §4) |
| `0x2E` | 1 | **race** | enum below |
| `0x2F` | 1 | **class** | enum below (incl. multiclass) |
| `0x30`–`0x31` | 2 | **age** | UInt16 LE |
| `0x32` | 1 | **HP maximum** | |
| `0x33`–`0x69` | 55 | known spells | one flag byte per learnable spell (cleric/mage L1–3) |
| `0x6B` | 1 | attack level | |
| `0x6D`–`0x71` | 5 | **saving throws** | para/poison/death, petrify/polymorph, rod/staff/wand, breath, spell |
| `0x72` | 1 | movement base | |
| `0x73` | 1 | level (highest class) | |
| `0x74` | 1 | drained levels | level drain from undead |
| `0x75` | 1 | drained HP | |
| `0x77`–`0x7E` | 8 | **thief skills** | pick pockets, open locks, find/remove traps, move silently, hide, hear, climb, read languages |
| `0x7F`–`0x82` | 4 | effects list pointer | far pointer into guest RAM |
| `0x84` | 1 | NPC flag | |
| `0x85` | 1 | modified flag | set when the character was edited |
| `0x88`–`0x89` | 2 | **copper** | UInt16 |
| `0x8A`–`0x8B` | 2 | **silver** | |
| `0x8C`–`0x8D` | 2 | **electrum** | |
| `0x8E`–`0x8F` | 2 | **gold** | |
| `0x90`–`0x91` | 2 | **platinum** | 1 pp = 5 gp |
| `0x92`–`0x93` | 2 | **gems** | count |
| `0x94`–`0x95` | 2 | **jewelry** | count |
| `0x96`–`0x9D` | 8 | **class levels** | cleric, druid, fighter, paladin, ranger, mage, thief, monk |
| `0x9E` | 1 | **gender** | 0 male / 1 female |
| `0xA0` | 1 | **alignment** | enum below |
| `0xA9` | 1 | AC base | stored `60 − value`; the unarmored 10 baseline |
| `0xAC`–`0xAF` | 4 | **experience** | UInt32 LE — a single total, not per-class |
| `0xB0` | 1 | **class bitmask** | mage `0x01`, cleric `0x02`, thief `0x04`, fighter `0x08`; a multiclass is the union (§3b) |
| `0xB1` | 1 | HP rolled | raw die roll before CON bonus/draining |
| `0xB2`–`0xB4` | 3 | cleric spells/day | L1–3 |
| `0xB5`–`0xB7` | 3 | mage spells/day | L1–3 |
| `0xB8`–`0xB9` | 2 | **XP award** | XP granted for killing this creature (monsters) |
| `0xC7` | 1 | number of items | |
| `0xC8`–`0xCB` | 4 | items list pointer | linked list |
| `0xCC`–`0xFF` | 4×13 | equipped-item pointers | weapon, shield, armor, gauntlets, helm, belt, robe, cloak, boots, ring1, ring2, arrows, bolts |
| `0x102`–`0x103` | 2 | encumbrance | |
| `0x104`–`0x107` | 4 | next-character pointer | the party is a linked list in memory |
| `0x108`–`0x10B` | 4 | combat struct pointer | valid during combat |
| `0x10C` | 1 | **status** | 0 = okay (enum below) |
| `0x110` | 1 | **THAC0 current** | effective, `60 − value` |
| `0x111` | 1 | **AC current** | effective, `60 − value` — what the game shows |
| `0x11B` | 1 | **HP current** | the live current HP |
| `0x11C` | 1 | movement current | |

### Enumerations

- **Race** (`0x2E`): 0 monster · 1 dwarf · 2 elf · 3 gnome · 4 half-elf · 5 halfling · 6 half-orc · 7 human
- **Class** (`0x2F`): 0 cleric · 1 druid · 2 fighter · 3 paladin · 4 ranger · 5 mage · 6 thief · 7 monk · 8 C/F · 9 C/F/M · A C/R · B C/M · C C/T · D F/M · E F/T · F F/M/T · 10 M/T · 11 monster
- **Alignment** (`0xA0`): 0 LG · 1 LN · 2 LE · 3 NG · 4 TN · 5 NE · 6 CG · 7 CN · 8 CE
- **Gender** (`0x9E`): 0 male · 1 female
- **Status** (`0x10C`): 0 okay · 1 animated · 2 tempgone · 3 running · 4 unconscious · 5 dying · 6 dead · 7 stoned · 8 gone

---

## 3a. The known-spell block's ordering (`0x33`–`0x69`)

The 55 bytes at `0x33` are one flag per learnable spell — but *in which order*? The trainer's own
`SpellBook` lists spells school-first (all the cleric spells, then all the magic-user ones), and
reading the block that way decodes the sample party's elf Fighter/Mage as knowing two cleric level-2
spells and a cleric level-3 spell, on a character with no cleric level at all.

The game answers it directly. `START.EXE` carries the spell-name table verbatim at file offset
`0x00E450`, and its sequence is grouped by **spell level first, school second**:

| Flags | Spells |
|---|---|
| `0`–`7` | cleric 1 — Bless, Curse, Cure Light Wounds, Cause Light Wounds, Detect Magic, Protection From Evil, Protection From Good, Resist Cold |
| `8`–`20` | magic-user 1 — Burning Hands … Shield, Shocking Grasp, **Sleep** |
| `21`–`27` | cleric 2 — Find Traps … Spiritual Hammer |
| `28`–`34` | magic-user 2 — Detect Invisibility … Strength |
| `35`–`43` | cleric 3 — Animate Dead … Bestow Curse |
| `44`–`54` | magic-user 3 — Blink … Slow |

8 + 13 + 7 + 7 + 9 + 11 = **55**, exactly the block's length. Under this order Rhiannon's four flags
(indices 10, 17, 18, 20) read as Detect Magic, Read Magic, Shield and **Sleep** — four magic-user
level-1 spells, which is precisely a new mage's starting spell book. Four flags landing inside the
13-wide magic-user level-1 window by chance is a ~0.3% coincidence, so the ordering is settled.

`SpellBook.InRecordOrder` exposes the block order (as distinct from the display order), and
`FormatCheck` pins both the sequence and Rhiannon's four spells.

Note that this is the *known*-spell block; the 21 bytes of **memorized** spells at `0x17` are a
different structure and are not decoded — the trainer only ever snapshots and restores them
verbatim (the spell freeze), or clears them.

---

## 3b. `0xB0` — the class bitmask

The byte between the experience total and the rolled hit points was unidentified until a class-change
feature needed to know what a class change might be leaving behind. Diffing the sample party's
caster against its non-casters turned it up, and it is a **bitmask, one bit per class**:

| Bit | Class | A character holding it |
|---|---|---|
| `0x01` | magic-user | Darkstar (Mage 1) reads `0x01` |
| `0x02` | cleric | Brother Sean (Cleric 1) reads `0x02` |
| `0x04` | thief | Phineas (Thief 1) reads `0x04` |
| `0x08` | fighter | Thrender (Fighter 1) and Altharion (Fighter 5) both read `0x08` |

A multiclass carries the union: Rhiannon the Fighter/Mage reads `0x09`, Bakshi the
Cleric/Fighter/Mage `0x0B`. Checked across **71 character records** found in four save folders — 15
distinct characters covering six class combinations, at levels 1 to 9 — it agrees with the per-class
level bytes at `0x96` every single time, with no exceptions.

So the record states a character's class in three places: the class byte at `0x2F`, the per-class
levels at `0x96`, and this mask. Anything that rewrites one has to rewrite all three, or the record
contradicts itself — which is exactly the bug this found in the party generator and the class-change
feature, both of which were writing the first two and leaving the third describing whoever used to
hold the slot. Whether the engine reads the mask (to decide who gets a Memorize option, say) or
merely keeps it in step is not established; the trainer writes it because a record that disagrees
with itself is not worth shipping either way.

## 3c. Spells per day, solved from real casters

The class spell tables were carried over from the Rule Book as printed in `ClassRaceBook`. Real
saved characters disagree with them, and the characters win. Seven casters at different levels and
Wisdoms determine the rows outright:

| Character | Class & level | Wisdom | Stored `0xB2`/`0xB5` | = class row + Wisdom bonus |
|---|---|---|---|---|
| Brother Sean | Cleric 1 | 17 | 3/0/0 | 1/0/0 + (2,0,0) |
| Bakshi | Cleric 1 (of a C/F/M) | 17 | 3/0/0 | 1/0/0 + (2,0,0) |
| Dirten | Cleric 5 | 16 | 5/5/1 | 3/3/1 + (2,2,0) |
| Alfred | Cleric 6 | 18 | 5/5/3 | 3/3/2 + (2,2,1) |
| Darkstar | Mage 1 | — | 1/0/0 | 1/0/0 |
| Tarry, Carry | Mage 6 | — | 4/2/2 | 4/2/2 |

Two things fall out. A **level-1 cleric gets one spell from its class**, not none as the table
printed — the rest of Brother Sean's three are Wisdom's. And **Wisdom's higher-level bonus spells
wait for the levels that can cast them**: at Wisdom 17 he is owed a 2nd- and a 3rd-level spell too,
and gets neither until he is high enough, which is why his row is 3/0/0 rather than 3/2/1.

The corrected rows are the standard 1e ones — cleric 1, 2, 2/1, 3/2, 3/3/1, 3/3/2 and magic-user 1,
2, 2/1, 3/2, 4/2/1, 4/2/2. `ClassRaceBook.LevelProgression` (what the Rules tab shows) had the
cleric column a level out and the magic-user column wrong at levels 5 and 6; both are fixed, and
`FormatCheck` now checks the displayed table against the computed one so they cannot drift apart
again.

---

## 4. The AC / THAC0 "60 − x" encoding

AD&D uses *descending* Armor Class and THAC0 (lower is better). The engine stores both as
`60 − displayed`, so that internally **higher = better** even though the displayed number
descends. To read a displayed value, compute `60 − storedByte`; to set displayed `X`, write
`60 − X`. This was confirmed empirically:

- Thrender's effective AC byte at `0x111` = `59` → displayed `60 − 59 = 1` (a dwarf fighter in
  banded mail — correct).
- His AC-base byte at `0xA9` = `50` → `60 − 50 = 10` (the naked baseline).
- His current-THAC0 byte at `0x110` = `41` → `60 − 41 = 19` (level-1 fighter — correct).

The record keeps a *base* AC/THAC0 (the 10/20 unarmored baseline at `0xA9`/`0x2D`) and a *current*
AC/THAC0 (the effective value including equipment at `0x111`/`0x110`). The trainer shows the
**effective** value and, when you edit it, writes **both** so an equipment recompute can't quietly
revert your change.

---

## 5. Verification — the decoded sample party

Applying the table above to the "exploring" dump decodes the whole party to values that are
mutually consistent (casters have spell data, ages match racial lifespans, level-1 party shares
~30 XP and pooled starting money):

```
THRENDER GRONE  Male   Dwarf     Fighter               STR 17  HP 11/11  AC 1  THAC0 19  age 52   XP 32
BAKSHI          Male   Half-Elf  Cleric/Fighter/Mage   STR 18/90 HP 7/7  AC 4  THAC0 18  age 48   XP 10
RHIANNON        Female Elf       Fighter/Mage          STR 15  HP 7/7    AC 0  THAC0 20  age 180  XP 14
BROTHER SEAN    Male   Human     Cleric                WIS 17  HP 10/10  AC 2  THAC0 20  age 22   XP 32
DARKSTAR        Female Human     Mage                  INT 18  HP 5/5    AC 7  THAC0 20  age 27   XP 32
PHINEAS         Male   Halfling  Thief                 DEX 18  HP 6/6    AC 4  THAC0 20  age 46   XP 32
```

Notice the details that fall out for free and cross-check the layout: only humans/half-elves are
clerics (Brother Sean, Bakshi); the mage Darkstar is unarmored (AC 7); the dwarf is old (52) and
the elf ancient (180); Bakshi's exceptional strength byte is `0x5A` = 90, i.e. **18/90**.

### The combat dump proves the live fields

Diffing the two dumps *by process address* shows the party header (name, stats, race, class,
money) is byte-identical between exploring and combat — those are persistent. The **live combat
fields** differ exactly as expected:

```
RHIANNON  explore:  HP current (0x11B) = 7,  status (0x10C) = okay
RHIANNON  combat:   HP current (0x11B) = 0,  status (0x10C) = unconscious
```

That single diff — HP 7 → 0 and status okay → **unconscious** — matches the dump's own note
("Rhiannon is unconscious") and nails down `0x11B` (current HP) and `0x10C` (status).

### The record is the character sheet, not the fight

Traced live against a 16-kobold battle (one party member, `FindCombatants` + a byte-differ on every
combatant record and on what its pointers reach):

- **`0x104` chains every combatant.** The party member's next-pointer leads to the first monster and
  on down the encounter, ending in a null — one linked list for both sides.
- **`0x108` is the engine's per-fight block for that creature.** Null outside combat (a reliable
  "are we in a battle" test), non-null for every combatant during one. The blocks are 24 bytes; those
  allocated together sit in a contiguous array. `[0x0A..0x0D]` is a far pointer to the creature's
  current target — it tracked the party member's attacks from kobold to kobold, one per round.
- **The engine rewrites those blocks every round**, including for creatures that are already dead:
  at each round boundary movement `[0x06]` goes back to `0x0C` and the action flags `[0x01]`,
  `[0x04]`, `[0x05]` are restored across the whole array at once.

The consequence matters for cheating: **a death only counts when the engine's damage routine
processes it.** That routine is what takes the creature off the battlefield, leaves the body, and
banks what it carried for the post-battle treasure. Writing HP 0 (and status *dead*, or *gone*) into
the 285-byte record edits the sheet, not the fight — the creature finishes the round, the surviving
monsters' morale check ends the battle in a **surrender**, and a surrender pays no XP and no
treasure. Confirmed in play: forcing the record alone loses the encounter's loot every time.

So the trainer's loot-safe move is to *let the game do the killing* — drop a monster to 1 HP with
AC/THAC0 20 (`CharacterViewModel.WeakenNow`) and let the next party blow land, which runs the real
death path. The instant-kill is kept for escaping a fight, and says so on the button.

### Monsters share the record

Monsters use the **identical 285-byte record**. In the combat dump the six orcs appear as records
named `"ORC"` with a low Intelligence byte (6), and each reads **HP = 5** at the current-HP field —
matching the dump note ("6 Orcs, each with 5 Hit Points"). Because monsters and characters share a
format, the trainer's combat panel can enumerate and edit enemies exactly like party members.

---

## 5a. Carried items — the `CHRDATAn.ITM` file

Each character's inventory lives in a sibling save file, **`CHRDATAn.ITM`**, as a flat array of
fixed **63-byte (`0x3F`)** item records — no header, so `record count = file size / 63`. The
character record's item-count byte (`0xC7`) and the runtime item/equip pointers (`0xC8`,
`0xCC`–`0xFF`) live in the `.SAV`; those pointers are stale runtime addresses the game rebuilds on
load (exactly like the effects-list head at `0x7F`), so the **persisted** inventory state is just
the count byte plus the `.ITM` records. The layout was confirmed two ways that agree byte-for-byte:
the open-source `coab` `Item.cs` (`StructSize = 0x3F`) and a hex read of real `.ITM` bytes.

| Offset | Size | Field | Notes |
|-------:|:----:|-------|-------|
| `0x00` | 1+41 | name | Pascal string — the game's **cached** render (regenerated from the name-number bytes + hidden-names flag on display) |
| `0x2A`–`0x2D` | 4 | **next item** | far pointer (`offset` word, then `segment` word) to the next item this character carries; null on the last |
| `0x2E` | 1 | item type | see `coab`'s `ItemType` enum (e.g. `0x2F` Sling, `0x3B` Shield, `0x5D` Ring of Protection) |
| `0x2F`–`0x31` | 3 | name-number bytes | index the base/adjective/noun name parts |
| `0x32` | 1 | plus | magical bonus (signed) |
| `0x34` | 1 | **readied** | equipped flag |
| `0x35` | 1 | **hidden-names flag** | **0 = fully identified**; non-zero bits hide name parts (shown as a leading `*`) |
| `0x36` | 1 | cursed | |
| `0x37`–`0x38` | 2 | weight | UInt16 |
| `0x39` | 1 | count | stack size |
| `0x3A`–`0x3B` | 2 | value | UInt16 |
| `0x3C`–`0x3E` | 3 | affects | up to three item effects |

This was verified against `THRENDER GRONE`'s real `CHRDATA1.ITM`: nine records, a plain **Sling**
(type `0x2F`, hidden-names `0`) and an unidentified **Ring of Protection** (type `0x5D`,
hidden-names `6`, value 10000). Setting the hidden-names byte to `0` "identifies" an item — the
name regenerates fully on the next display — and copying one character's `.ITM` records plus its
count byte onto another duplicates the whole inventory. The trainer's **🎒 Inventory** tab does
exactly this (offline, with an automatic backup); the `ItemEntry` parser is regression-tested in
`test/FormatCheck` against verbatim `.ITM` bytes.

### Finding a character's items in the *running* game

In memory the same records form a **singly-linked list**: the character record's `0xC8` far pointer
gives the first item, and each item's `0x2A` far pointer gives the next, ending at `0000:0000`. Both
are real-mode `segment:offset` pairs, so the guest address is `segment × 16 + offset`.

Following that list is the only correct way to enumerate an inventory, and the difference is not
academic. A live party member carrying seven items had six of them within 1 KB of its record and the
seventh **over 8 KB away**, interleaved with unrelated allocations; the list order also differs from
address order. Sweeping a range around the character record — the obvious approach, and what this
trainer did first — therefore drops items, picks up freed heap slots that still hold a plausible dead
record (a stale `Jewelry 3` showed up that way), and in any case cannot say which character a swept
record belongs to. Walking the links reproduces the game's own item screen exactly, in its order.

The one thing the links don't give you is where the guest's RAM sits in the emulator process. The
trainer solves that instead of hard-coding it: it signature-scans a megabyte either side of a
character record for item-shaped bytes, and for each hit assumes *that* is where the head pointer
lands, which fixes a candidate guest→host offset. Walking the whole chain with that offset either
resolves every link onto a valid record and terminates, or it doesn't — a wrong offset cannot fake
seven consecutive valid hops. DOSBox maps the emulated RAM as one flat block, so the offset that
survives holds for every character and for the rest of the session.

Note also that `0x2A` sitting *inside* the 63-byte record makes a whole-record copy destructive:
duplicating an item has to preserve the destination slot's own link, or the owner's list gets spliced
onto wherever the source sat in its list.

## 6. How the trainer uses this

The trainer mirrors the approach a live memory editor must take:

1. **Attach** to the DOSBox process (`OpenProcess`), then enumerate committed regions with
   `VirtualQueryEx`.
2. **Signature-scan** every region for the record shape (`Game/CharacterSignature.cs`): a valid
   Pascal name (length 1–15, an initial letter, printable chars then NUL padding), six ability
   scores in range, a race byte ≤ 7, a class byte ≤ 17, non-zero max HP, and a valid status enum.
   This finds the whole party *and* any in-combat monsters regardless of where DOSBox mapped RAM.
3. **Read/write** fields at the offsets above with `ReadProcessMemory` / `WriteProcessMemory`
   (`Memory/ProcessMemory.cs`), applying the `60 − x` transform for AC/THAC0.
4. **Poll** (~1.5 Hz) to keep the party/enemy HP display live and to re-apply "freeze HP" (god
   mode) by re-writing current HP to max each tick.
4a. **Sweep the arena** (~0.8 Hz, `CharacterLocator.FindCombatants`) to keep the combat panel
   honest. Monster records exist only while a battle is on screen and the game builds them fresh —
   at fresh addresses — for every encounter, so an enemy list left over from step 2's one-off scan
   is stale before the first blow lands. Repeating a full ~250 MiB walk on the timer is far too
   slow (≈650 ms), but the arena is always allocated in the same DOS heap as the party, so a window
   of ±512 KiB around the party records finds every combatant in **1–3 ms**. The enemy list is then
   reconciled by address, so selection survives and a slot taken over by a different creature gets
   a fresh view-model rather than the previous occupant's numbers.
   The sweep also drops **look-alike buffers**: the signature can straddle a live record — a stray
   name string a few bytes ahead of a real monster reads as a record of its own, with its combat
   fields landing on zero padding. Since AC/THAC0 are stored as `60 − value`, those zeroes decode
   to AC 60 / THAC0 60, which no creature can have, so `CharacterRecord.LooksLikeLiveCombatant`
   rejects them (both fixture records are in `FormatCheck`).
   The one creature that band would wrongly reject is the trainer's own: Weaken stamps AC 20
   precisely *because* it is off the scale a real creature occupies, so a weakened monster failed
   the plausibility test and dropped out of the sweep — the panel called the battle over while it
   was still being fought, and the standing auto-weaken toggle would have weakened each encounter
   once and then gone quiet. `CharacterRecord.LooksWeakened` exempts it, on the **armour-class pair
   alone** (both bytes reading the stored form of AC 20) rather than by widening the band. The
   temptation is to make the exemption more specific by testing everything Weaken writes — HP 1,
   THAC0 20 — but those are fields the *game* moves: a monster cleric heals its ally, and the engine
   re-derives current THAC0 from the base minus a to-hit adjustment (§8). Either would falsify the
   mark while AC 20 was still stamped, which is the original drop-out again by another route. AC is
   the one field nothing but the trainer touches at that value, and both bytes at exactly 40 is
   discrimination enough — the buffers this guards against decode to AC 60. The stricter all-five
   test still exists as `CharacterRecord.IsWeakened`, but it answers a different question: has the
   auto pass anything left to do to this creature.
5. For anything **not** in the record — the party's map X/Y and facing, the in-combat clock,
   encounter counters — a **Cheat-Engine-style scanner** (`Memory/MemorySearcher.cs`) narrows
   candidates by first-scan/increased/decreased, mirroring the reverse-engineering loop itself.

The record parser (`Game/CharacterRecord.cs`) is regression-tested in `test/FormatCheck` against
the verbatim 285-byte records of Thrender and Rhiannon extracted from the dump, so a future change
that breaks a field is caught headlessly (`dotnet run --project test/FormatCheck`).

### 6a. Writing a *whole character* in (the party generator)

Editing one field is a poke. Replacing a character — what `Game/PartyGenerator.cs` does — means
writing about half the record at once, and the interesting part is which half.

**What it writes**, and what pins each value:

| Field(s) | Value | Anchored by |
|---|---|---|
| name, abilities, STR% | rolled | — |
| race, class, gender, alignment, age | rolled | ages match the sample party (dwarf 52, elf 180) |
| class levels `0x96`+, level `0x73`, attack level `0x6B` | 1 | both sample records carry level 1 in all three |
| HP max/current/rolled | class die at maximum (averaged over a multiclass) + CON bonus | Rhiannon's 7 = (d10 + d4) ÷ 2 at CON 14 |
| THAC0 base `0x2D` / current `0x110` | class row; current = base − STR to-hit | Thrender: base 20, STR 17, current 19 |
| AC base `0xA9` / current `0x111` | 10 / 10 − DEX adjustment | both records store base 10; the unarmored-10-minus-DEX rule is confirmed outright by *Curse of the Azure Bonds*' item-less sample party |
| saving throws `0x6D` | class row; best-of per category for a multiclass | Thrender 14/15/16/17/17 (fighter row); Rhiannon 14/13/11/15/12 (best of fighter and mage, category by category) |
| thief skills `0x77` | level-1 percentages + racial + DEX adjustments, or zeroed | every non-thief record decoded so far is all zeroes |
| movement base `0x72` / current `0x11C` | 12 | both sample records |
| class bitmask `0xB0` | the union of the character's class bits | §3b |
| known spells `0x33` | cleric level 1s flagged for clerics; Sleep, Magic Missile and two more for mages | §3a |
| memorized `0x17`, drain `0x74`–`0x76`, XP `0xAC`, status `0x10C` | cleared | a slot's previous occupant may have been drained, or hold spells the new class cannot cast |

Note the dwarf's saving throws: 14/15/16/17/17 is the plain fighter row with **no racial bonus**, so
the engine applies dwarven magic resistance somewhere other than these five bytes — and the
generator doesn't add one either.

**What it deliberately doesn't write**: the money counters (`0x88`–`0x95`), the item count and list
pointer (`0xC7`–`0xCB`), the thirteen equipped-item pointers (`0xCC`–`0xFF`), the effects pointer
(`0x7F`), encumbrance (`0x102`), the party linked-list pointer (`0x104`), the combat pointer
(`0x108`) and the combat-icon bytes. The pointers are the game's own bookkeeping — clobbering them
loses items or breaks the party list — and the possessions belong to the *slot*, not the person in
it. `RolledCharacter.WrittenRanges` is the explicit list of ranges written, which is also what the
live path pokes into the running game; `FormatCheck` asserts that stamping a generated character
over Thrender's record changes no byte outside them.

Because the identity fields (name, race, class, gender) change in memory at the same moment they
change in the trainer's copy, the poll loop's `IsSameCreatureAs` check still recognises the address
on its next tick — a live replacement doesn't need a re-scan.

### 6b. Changing a character's class, and the level-5 anchor

Writing the class byte at `0x2F` on its own only changes the label. `Game/ClassChange.cs` changes
the class *and* everything the class decides — the per-class levels at `0x96`, the class bitmask at
`0xB0` (§3b — the byte this feature's own diffing turned up), THAC0 at `0x2D`/`0x110`,
the saving throws at `0x6D`, thief skills at `0x77`, the known-spell block at `0x33` and spells per
day at `0xB2`/`0xB5` — while leaving hit points, experience, abilities, Armor Class, the level-drain
bytes, money, items and every pointer alone. The character keeps its level and its hit points; a
level-5 fighter becomes a level-5 magic-user with a fighter's 25 hit points, which no legitimately
played character could be and is precisely what the feature is for.

Doing that needs the class tables *per level*, not just at level 1 — and the two dump records are
both level 1, so nothing in them could distinguish a per-level rule from a constant. The third
fixture closes that gap: **ALTHARION**, the verbatim 285-byte `CHRDATA1.SAV` of a real GOG save, a
level-5 human fighter. It reads:

| Field | Value | What it settles |
|---|---|---|
| THAC0 base `0x2D` | **16** | four better than the level-1 fighter's 20, four levels up — the fighter's line is `21 − level`, not a constant |
| saves `0x6D`–`0x71` | **11/12/13/13/14** | exactly the published fighter level-5/6 row |
| class level `0x98`, level `0x73`, attack level `0x6B` | 5, 5, 5 | all three track the class level together |
| AC base `0xA9` | 10 | the unarmored baseline holds at level 5, so AC is not level-derived |
| movement `0x72` | 12 | unchanged from level 1 |
| thief skills `0x77`–`0x7E` | all zero | a non-thief carries none, at any level |

So the fighter column is measured at two levels and the level-5 saving-throw row is confirmed
outright. The cleric, magic-user and thief columns, and everything above level 5, follow the same
published tables but have no record to check against — `ClassTables` says which is which in its own
docs rather than implying the whole thing was measured.

The strongest test the harness runs on this is a round trip: change the real level-5 fighter to a
magic-user and back, and the record must land on its *own* stored THAC0 base, current THAC0 and five
saving throws again. That only passes if the fighter row, the magic-user row and the
equipment-credit rule (current THAC0 keeps `oldBase − oldCurrent`, since Strength and gear don't
change) are all right.

What the generator **cannot** do is add a party member. In memory the party is a linked list the
engine builds; on disk the member count lives somewhere in `SAVGAM?.DAT`, which is not decoded (the
saves available here all hold a single character, so there is nothing to difference). The feature
therefore rewrites the characters a party already has, and says so when the party it rolled is
larger than the one it found.

---

## 7. The game's file layout (for completeness)

`Run.ps1` and the trainer don't need these, but they frame the memory analysis:

- **`START.EXE`** (≈64 KB) — a DOS `MZ` executable; the loader/front-end.
- **`GAME.OVR`** (≈201 KB) — an **`FBOV`**-signatured **overlay** file (Borland's overlay format).
  The main game code is paged in from here as overlays, which is why the working set is small and
  why the game logic isn't resident as one flat image — a full static disassembly would have to
  follow the overlay table rather than read a single code segment.
- **`*.DAX`** — the resource archives. Their names encode content: `WALLDEF*`/`GEO*` (dungeon wall
  and geometry sets per area 1–8), `PIC*`/`CPIC*`/`TITLE` (pictures), `8X8D*` (8×8 tile fonts),
  `MON*CHA`/`MON*ITM`/`MON*SPC` (monster graphics/items/special per encounter tier), `BODY*`/`HEAD*`
  (portrait/icon parts), `SPRIT*`, `ECL*` (the encounter/"script" data referenced as "ECL Script N"
  in area maps), and `ITEMS`. A `.DAX` begins with a small index (offsets/sizes of its members).
- **`POOL.CFG`** — plain text; records the install path (`C:\POOLRAD\`) and a couple of settings.

Because the interesting data (the party, monsters, item instances) lives in the **emulated RAM**
at runtime, the memory-dump route above is both more direct and more precise than statically
reversing the overlaid `START.EXE`/`GAME.OVR` — the dumps *are* the ground truth, and the
`FormatCheck` harness proves the decode against them.

---

## 7a. Level geometry — `GEO*.DAX` (the walls the Maps tab draws)

The Maps tab used to draw only a grid, because the wall data lived in the `.DAX` archives and nobody
had opened them. They turned out to be straightforward, and `Game/MapTerrainData.cs` is now generated
from them, so the schematic shows the game's real walls rather than a transcription.

**The container.** A `.DAX` file is:

```
UInt16  headerLength                 // bytes of block entries that follow
entry[headerLength / 9]:
    byte    id                       // block id — the map number
    UInt32  offset                   // from the end of the header
    UInt16  unpackedSize
    UInt16  packedSize
byte[]  packed block data
```

`headerLength / 9` blocks; `2 + headerLength + Σ packedSize` accounts for the whole file exactly,
which is what confirmed the field order.

**The packing** is PackBits-style RLE. Read a lead byte `n`: if `n < 0x80`, copy the next `n + 1`
bytes verbatim; otherwise repeat the next single byte `256 - n` times. (`257 - n` also decodes
plausible-looking data — it was the wrong variant, and the tell was that only `256 - n` lands every
one of the 29 GEO blocks on its declared `unpackedSize` exactly.)

**A GEO block** unpacks to 1026 bytes: a `UInt16` length (`0x0400`) then four 256-byte planes, each a
16×16 grid indexed `y * 16 + x` with one byte per square:

| plane | contents |
|-------|----------|
| 0 | high nibble = **north** wall index, low nibble = **east** wall index |
| 1 | high nibble = **south** wall index, low nibble = **west** wall index |
| 2 | per-square backdrop / interior id (not used by the trainer) |
| 3 | two bits per direction — N = bits 0–1, E = 2–3, S = 4–5, W = 6–7. Non-zero = the edge can be **walked through**: a door, an archway, or an illusory wall |

A wall index of 0 means no wall; non-zero indexes that level's `WALLDEF*.DAX` graphic set. Shared
edges are stored on *both* squares and agree about 91% of the time — the rest are genuine one-sided
walls (you see a wall from one side only), so each edge is merged from the two sides.

Nothing marks a door as a door: the door bit says only "passable". What separates a door from an
**illusory wall** is the *graphic* — an illusory wall is passable but drawn with an index that the
same level also uses for solid walls, so it looks like a wall. Classifying passable edges that way
picks out exactly the Slums' known illusory wall at (1, 0) and nothing else.

**Verification.** Rendering all 29 blocks and matching them against this repo's transcribed Slums map
scores **1.000** on GEO2 block 20, and a live scan of the running DOSBox process finds that same
512-byte wall array resident verbatim — the game loads the block into RAM unchanged.

Every other block was identified against the printed maps in the bundled clue book
(`Cluebook.pdf`, which ships with the GOG release), by an automated match rather than by eye: the
page scan is thresholded along each grid line, which yields the same wall/no-wall grid the decoder
produces, and the two are scored by **Matthews correlation over interior edges only** — the outer
border is ink on every map and carries no signal, and plain agreement would let a nearly wall-free
block score well against any sparse map just for being mostly blank. The pipeline reproduces the
known assignments first (Kovel Mansion 1.000, Slums 0.992, Sokal Keep 0.935, Cadorna 0.814), which
is what licenses the rest:

| area | block | score | | area | block | score |
|------|-------|-------|-|------|-------|-------|
| New Phlan (15 rows) | GEO3:0 | — | | Nomad Camp | GEO7:17 | 0.44 |
| Slums | GEO2:20 | 0.99 | | Kobold Caves | GEO8:13 | 0.98 |
| Sokal Keep | GEO4:21 | 0.94 | | Yarash's Pyramid L1 west | GEO7:22 | 0.26 |
| Kuto's Well | GEO8:29 | — | | Yarash's Pyramid L1 east | GEO6:25 | 0.20 † |
| Kuto's Well Catacombs | GEO8:32 | 0.88 | | Yarash's Pyramid L2 | GEO7:23 | 0.81 |
| Podol Plaza | GEO1:18 | — | | Yarash's Pyramid L3 | GEO8:27 | 0.40 |
| Mendor's Library | GEO2:15 | — | | Lizard Man Keep | GEO8:16 | 0.49 |
| Kovel Mansion | GEO3:14 | 1.00 | | Lizard Man Catacombs | GEO8:30 | 0.96 |
| Cadorna Textile House | GEO4:2 | 0.81 | | Buccaneer's Base | GEO6:1 | 0.94 |
| Wealthy Area | GEO1:31 | — | | Outpost of Zhentil Keep | GEO6:28 | 0.98 |
| Temple of Bane | GEO1:24 | 0.99 | | Stojanow Gate | GEO2:9 | 0.97 |
| Valhingen Graveyard | GEO4:10 | 0.57 | | Valjevo SW / NW / NE / SE | GEO5:6 / 5:3 / 5:4 / 5:5 | ~0.5 |
| Inner Tower, upper | GEO5:7 | 0.88 | | Inner Tower, lower | GEO7:26 | 0.50 |

Scores below ~0.6 are still decisive — what matters is the margin over the runner-up, not the
absolute value. The clue book overlays glyphs the game has no notion of (tombstones on the
graveyard, swamp and rubbled walls on the Lizard Man Keep, trees and a stream on the Nomad Camp),
and every one of those adds ink that no decoded block can match; Valhingen scores 0.57 against a
runner-up of 0.098, the Lizard Man Keep 0.49 while winning 2,382 of the 2,401 grid alignments tried.
The four Valjevo quadrants score ~0.5 for the same reason — most of each map is hedge maze, drawn in
a style of its own — and are pinned instead by their edge exits, which form a consistent 2×2: NW
leads east and south, NE west and south, SW east and north, SE west and north.

† **The pyramid and the tower are the weakest links.** Yarash's pyramid is printed as four maps
(level 1 split across the page gutter into a west and an east half, then levels 2 and 3) and the
Inner Tower as two small ones, and their five remaining blocks were settled as a set rather than
individually: level 2 matches at 0.81 outright, and the other four fall out of picking the
assignment that maximises the total. Two independent signals agree on the split — edge matching, and
comparing the clue book's *hatched* impassable squares against the squares the decoder derives as
unreachable, which is what identifies GEO6:25 as the pyramid's east half (its walkable region is the
top seven rows, the rest sealed, exactly as the book draws it; the alternatives score negative).

**Two maps are only as good as that argument**: GEO7:26 as the Inner Tower's lower level and GEO8:27
as pyramid level 3. Their geometry is right — these are the game's own blocks either way — but the
two labels could in principle be swapped with each other. Everything else in the table is anchored
either on a decisive score or on the assignment being forced.

**The Inner Tower is a partial level.** The clue book draws both tower floors as 8×8 maps, and the
upper one matches GEO5:7's columns 1–8, rows 4–11 at 0.878 — the block's remaining squares carry no
walls at all, and it is the only level in the game with no outer border wall. So the schematic for
it shows a small structure adrift in an open field; that is what the block contains.

**Floors.** The game stores no floor terrain, so "impassable" is derived: a square sealed on all four
sides, or cut off from the level's main walkable region, can never be stood on. In New Phlan the
squares that derivation finds east of the sea wall are exactly the ones the clue book prints water
glyphs on, cell for cell, so those are drawn as water and the rest as stone.

---

## 7b. Where the party is standing — and why the wilderness needed its own answer

The Maps tab locates the party by scanning for the coordinates the game prints and narrowing after a
move. Indoors that works on the obvious shape: three adjacent bytes, `[X][Y][Facing]` (Gold Box
facing `0=N 1=E 2=S 3=W`). **In the wilderness it never locked**, and the reason is not subtle: with
the party standing on the square the game labels `26,27`, the byte pair `26,27` occurs **exactly once
in the emulated guest's 16 MB**, inside a static lookup table that does not change when the party
moves. Nor is 26 stored in any other form the scan could recognise — not as a 16-bit word, BCD,
ASCII, or a linear map index `y*stride + x` for any stride from 8 to 128.

**How it was recovered.** Three memory images of the running game were captured with the party on
three known squares — `(26,27)`, then `(27,25)`, then `(25,25)` — read off the game's own status
line (`25,25 W 04:09`). Differencing them narrowed the whole 16 MB to a handful of bytes:

- Exactly one address made the transition `27 → 25` between the first two images. Nothing made the
  transition `26 → 27`, and nothing changed by `+1` that could plausibly be an offset X.
- The address that *did* track X sat two bytes earlier and read `13 → 14 → 12` while the game
  displayed `26 → 27 → 25`. It moves one-for-one with X — it is just **short by a constant 13**.

So the wilderness position is a pair of adjacent little-endian 16-bit words, `[X][Y]`. **Y is the
number on screen; X is not** — the printed X is the stored word plus a bias (13 in the session it was
recovered from). The bias survived a `+1` move and a `−2` move, which is what rules out a scale
factor, but a constant that shows up as 13 for a party in the central band is not something to
hard-code: 13 would make the map's western squares negative, so it is more likely relative to
something than absolute. The trainer therefore **measures the bias per lock** rather than assuming
it — Snapshot records each candidate's implied bias and Narrow keeps only the candidates whose bias
still predicts the new coordinates. That is also what makes the two encodings collapse cleanly: both
shapes are collected in one pass and the wrong one dies at the first Narrow. Live against the running
game, ~9,000 candidates for `(25,25)` narrowed to exactly one — the right address, bias 13 — after a
single move.

One consequence is worth stating because it is easy to get wrong: since the printed X is the stored
word *plus* a bias, a square west of the bias stores a **negative** word. The X word must therefore be
read and written as a signed 16-bit value. Truncating it to a byte — as the first cut of this code
did — silently lands the party 256 columns east of any western target.

**Where it lives.** The pair sits inside the block the game writes to `SAVGAM?.DAT`, which is
resident verbatim: the save file's bytes map to memory at a fixed delta (the block based at guest
`0x3315F` in the session examined), so **X is at save offset `0x187` and Y at `0x189`**, with the day
counter, minute and hour a few words further on (`0x1E1`, `0x18F`, `0x193`). Confirming that also
confirmed the reading — the on-disk save made while the party stood on `26,27` holds `0D 00 1B 00`
there, i.e. stored X 13 and Y 27.

**No facing, and no terrain grid.** Only ten bytes in the entire 13,137-byte state block change
across a three-square move, and none of them is the compass letter the status line prints, so the
overland marker is drawn as a dot rather than an arrow pointing at a guess. The wilderness terrain is
not resident either: a template match of the clue-book map against all 16 MB, at every width from 36
to 48 and one byte per square, finds nothing, and no shipped `.DAX` decodes to an overland grid
(`WILDCOM.DAX` is the wilderness *graphics*, 4bpp, not a map). That is why `Game/WildernessMap.cs` is
transcribed rather than generated, and says so.

---

## 8. Provenance & cross-references

The layout was corroborated against the community record documentation shipped with the
**Gold Box Companion** (`formats.zip`, "Character file formats / 01. Pool of Radiance.txt") and the
open-source **`coab`** reimplementation (`Classes/PoolRadPlayer.cs`, `StructSize = 0x11D`). Both list
the same offsets and enums that the dump analysis recovered independently. AC/THAC0's `60 − x`
encoding matches the sister-game code (`DisplayAc = 0x3C − ac`) and is confirmed here on real
Pool of Radiance bytes.

- Gold Box Companion — https://gbc.zorbus.net/  (`formats.zip`, PoR Monster Manual)
- `coab` (Curse of the Azure Bonds reimplementation) — https://github.com/simeonpilgrim/coab
- Stephen S. Lee, "Pool of Radiance — Exhaustive Game Information" — http://www.easydamus.com/PoR.pdf
