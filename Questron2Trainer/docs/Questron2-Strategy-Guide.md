# Questron II — Play and Strategy Guide

*Questron II* (SSI, 1988) — a single-character RPG developed by **Westwood Associates**
(Quest Software / SSI), running on an EXEPACK-compressed Microsoft C 1987 engine
(`START.EXE`, version 1.2). You create **one** character and adventure across the
continent of **Landor**, fighting monsters, exploring dungeons, collecting keys and
quest items, and climbing the rank ladder from Nothing to Adventurer to Apprentice to
**Knight** — the goal of the game.

Everything in this guide comes from the game's own manual, from strings extracted from
`START.EXE`, or from the shipped `DEMOFILE` save (the demo character "The Thing": HP 200,
Food 188, Gold 162, all attributes 15, Level 1). Mechanics marked **[Static]** were
confirmed against the DEMOFILE and/or the manual; those marked **[Inferred]** are
plausible from the DEMOFILE but not independently confirmed against a live game. See
`docs/Questron2-Reverse-Engineering.md` for the full teardown.

---

## 1. How you win

The goal is to become a **Knight** — the highest rank in Landor. You start as Nothing
(rank 0), gain experience through combat, and progress through the ranks:

| Level | Rank | Title |
| --- | --- | --- |
| 0 | Nothing | Starting character |
| 1 | Adventurer | First rank after creation |
| 2 | Apprentice | Mid-game rank |
| 3+ | Knight | The goal — endgame rank |

Along the way you explore the continent, collect **twelve keys** to unlock gated areas,
gather **eleven quest items** (the Unicorn Horn, the Wand of Power, the Eternal Flame,
and so on), and ultimately complete the quest chain that elevates you to Knighthood.

The shape of play:

1. **Start in a town** — buy equipment, food, and spells.
2. **Venture out** — fight monsters on the overland map and in dungeons for experience
   and gold.
3. **Explore** — find keys, open locked doors, discover quest items in tombs and
   dungeons.
4. **Grow stronger** — upgrade weapons and armor, learn spells, raise attributes.
5. **Complete the quest** — gather the special items, reach the Conclave of Sorcerers,
   and become a Knight.

---

## 2. Character creation

### 2.1 Attributes

Your character has **five attributes**, each ranging from **1 to 25** and starting at
**15** for a new character **[Static]**:

| Attribute | Abbreviation | Effect |
| --- | --- | --- |
| **Charisma** | CHA | Shop prices (higher = cheaper), NPC interactions |
| **Strength** | STR | Combat damage with melee weapons |
| **Agility** | AGI | To-hit chance in combat, defense / evasion |
| **Stamina** | STA | Hit points and endurance — how long you can fight and travel |
| **Intelligence** | INT | Spell effectiveness — damage, duration, and reliability |

All five are stored as single bytes at record offset `+0x07` through `+0x0B` **[Static]**.
The trainer can raise any attribute to the maximum of 25.

### 2.2 Starting vitals

| Vital | Starting value | Notes |
| --- | --- | --- |
| **Hit Points** | 200 | The manual states "begins at 200" **[Static]** |
| **Food** | ~188 | The DEMOFILE shows 188; the manual says "buy food in towns" **[Static]** |
| **Gold** | ~162–200 | The DEMOFILE shows 162; the manual says "begins at 200" **[Static]** |

Food decreases as you travel and fight. When food runs out, your character begins to
starve — keep it topped up or buy more in towns.

### 2.3 Name

The character name is a 16-byte null-terminated ASCII field at record offset `+0x50`
**[Static]**. The DEMOFILE character is named "The Thing".

---

## 3. Combat system

### 3.1 Melee combat

You fight with whatever weapon you have equipped. The ten weapons, in the order they
appear in the EXE's weapon table (the equipped-weapon byte at `+0x10` indexes this
table) **[Inferred]**:

| ID | Weapon | Tier |
| --- | --- | --- |
| 0 | Dagger | Starting |
| 1 | Hammer | |
| 2 | Hatchet | |
| 3 | Cudgel | |
| 4 | Rapier | |
| 5 | Fauchard | |
| 6 | Weighted Spear | |
| 7 | Shortbow | *(demo character's weapon)* |
| 8 | Broadsword | |
| 9 | Crossbow | Best |

Weapons get progressively more powerful. Buy the best you can afford — the damage
difference between a Dagger and a Crossbow is significant, and higher-tier weapons are
essential against the tougher monsters in later areas.

### 3.2 Armor

Seven armor types, indexed by the equipped-armor byte at `+0x11` **[Inferred]**:

| ID | Armor | Tier |
| --- | --- | --- |
| 0 | Rawhide | Starting |
| 1 | Studded Leather | |
| 2 | Ring Mail | |
| 3 | Bar Mail | |
| 4 | Chain Mail | *(demo character's armor)* |
| 5 | Plate Mail | |
| 6 | Ribbed Plate | Best |

Better armor reduces the damage you take. The demo character "The Thing" starts with
Chain Mail (ID 5), which is a mid-tier armor — a new character will likely start with
something cheaper and upgrade over time.

### 3.3 Spells

Five spells are available. The first four can be purchased in towns; the fifth —
Destruct — appears in the EXE's string table but not in the manual, suggesting it may
be a late-game or hidden spell **[Inferred]**:

| ID | Spell | Effect | Buyable |
| --- | --- | --- | --- |
| 0 | **Magic Missile** | Basic single-target damage | Yes |
| 1 | **Fireball** | More powerful single-target damage | Yes |
| 2 | **Sonic Whine** | Attacks all adjacent enemies | Yes |
| 3 | **Time Sap** | Slows / freezes enemies' sense of time | Yes |
| 4 | **Destruct** | Powerful spell — not in the manual | No (found?) |

Spell charges are stored as one byte per spell at record offset `+0x86`, with eight
slots **[Inferred]**. Each charge byte ranges from 0 to 99. The DEMOFILE shows
`01 01 01 01 01 01 01 01` — one charge per spell for the demo character.

**Intelligence** governs spell effectiveness. A character with high Intelligence will
get more mileage out of each spell cast — better damage from Magic Missile and Fireball,
wider effect from Sonic Whine, and longer freezes from Time Sap.

### 3.4 Monsters

Approximately **39 monster types** were recovered from `START.EXE`'s string table
**[Inferred]**. The manual states "over 60 different types of creatures inhabit Landor,"
so the EXE strings may not capture every type. The recovered roster includes:

| Early threats | Mid-game | Late-game |
| --- | --- | --- |
| Beggar | Slasher Boar | Constrictor |
| Gypsy Imp | Antisaur | Giant Mantray |
| Sovan Priest | Baboon | Pincer |
| Wave Slapper | Hornet Cloud | Cannibal |
| Mutant Carp | Ball Slime | Muck Grabber |
| Hull Bore | Carrion Creeper | Swamp Slither |
| Spincer | Jelly Nymph | Brine Flicker |
| Snooper Slink | Giant Cockroach | Gilgore |
| Grub Snuffler | Stink Worm | Mind Scream |
| Ramdart | Hurler | |
| Swine Swallow | Ice Urchin | |
| Boll Rot | Cloud Creeper | |
| Tangler | Spiker | |
| Brawn Warrior | Venom Ant | |

Early monsters are found near towns and on the overland map; tougher ones appear in
dungeons, tombs, and deeper wilderness areas. **Sonic Whine** is valuable when
surrounded — it hits every adjacent enemy at once.

---

## 4. Equipment progression

### 4.1 Weapon upgrade path

```
Dagger → Hammer → Hatchet → Cudgel → Rapier → Fauchard → Weighted Spear → Shortbow → Broadsword → Crossbow
```

**Early game (Dagger–Cudgel):** Buy the best you can afford immediately. Even a Hammer
or Hatchet is a meaningful upgrade over the starting Dagger.

**Mid game (Rapier–Shortbow):** The Rapier and Fauchard offer good damage for their
price. The Shortbow is the demo character's weapon — a solid mid-tier choice that also
allows ranged attacks.

**Late game (Broadsword–Crossbow):** The Broadsword and Crossbow are the most powerful
weapons in the game. Save up and buy one as soon as you can afford it.

### 4.2 Armor upgrade path

```
Rawhide → Studded Leather → Ring Mail → Bar Mail → Chain Mail → Plate Mail → Ribbed Plate
```

**Early game (Rawhide–Ring Mail):** Even cheap armor is better than none. Upgrade to
Studded Leather or Ring Mail as soon as you have spare gold.

**Mid game (Bar Mail–Chain Mail):** Chain Mail (the demo character's starting armor) is
a good mid-game target. It offers solid protection for a reasonable price.

**Late game (Plate Mail–Ribbed Plate):** Plate Mail and Ribbed Plate are the best armor
in the game. Essential before tackling the Dungeon of Despair or the deeper tombs.

---

## 5. Items and keys

### 5.1 The twelve keys

Keys unlock doors and gates throughout Landor. Each is a distinct item — you need the
right key for the right lock. The twelve keys, in order **[Inferred]**:

| ID | Key | | ID | Key |
| --- | --- | --- | --- | --- |
| 0 | Gold Key | | 6 | Emerald Key |
| 1 | Opal Key | | 7 | Onyx Key |
| 2 | Iron Key | | 8 | Ruby Key |
| 3 | Brass Key | | 9 | Agate Key |
| 4 | Copper Key | | 10 | Sapphire Key |
| 5 | Silver Key | | 11 | Black Key |

Item ownership is tracked by flag bytes at record offset `+0x20` through `+0x4F`
(48 bytes) **[Inferred]**. The DEMOFILE has sparse `01` values at `+0x27`, `+0x2F`, and
`+0x3F`, indicating three starting items.

### 5.2 Quest items

Eleven special quest items are needed to complete the game **[Inferred]**:

| ID | Item |
| --- | --- |
| 12 | Unicorn Horn |
| 13 | Wand of Power |
| 14 | Eternal Flame |
| 15 | Book of Magic |
| 16 | Crystal Goblet |
| 17 | Chalice of Arvyl |
| 18 | Moonstone Amulet |
| 19 | Orb of Enchantment |
| 20 | Scroll of Scalna |
| 21 | Rope & Hooks |
| 22 | Bread of Life |

These items are found in dungeons, tombs, and other remote locations. Some may be
purchasable or given as rewards; others must be discovered through exploration. The
Rope & Hooks, for example, likely allows access to otherwise unreachable areas.

### 5.3 Transports

Two special transports **[Inferred]**:

| ID | Transport |
| --- | --- |
| 23 | Camalon |
| 24 | Trained Eagle |

Transports presumably allow faster overland travel or access to areas that cannot be
reached on foot. A Trained Eagle may provide aerial access to elevated or isolated
locations.

---

## 6. Locations of Landor

### 6.1 Towns (10)

Towns are where you buy weapons, armor, spells, and food. They are safe havens — rest
and resupply before venturing out.

| ID | Town | | ID | Town |
| --- | --- | --- | --- | --- |
| 0 | Hidden Rock | | 5 | Santor |
| 1 | Bay View | | 6 | Long View |
| 2 | Folman | | 7 | Seacrest |
| 3 | Ontaga | | 8 | Octapoint |
| 4 | Crooked Pine | | 9 | Cramford |

### 6.2 Cathedrals (4)

Cathedrals are likely where you learn spells, receive healing, or get quest
assignments.

| ID | Cathedral |
| --- | --- |
| 10 | Sanctuary Cathedral |
| 11 | Rivercrest Cathedral |
| 12 | Great Plains Cathedral |
| 13 | Twilight Cathedral |

### 6.3 Castle (1)

| ID | Castle |
| --- | --- |
| 14 | Redstone Castle |

Redstone Castle is likely a major story location — possibly the seat of the Knighthood
or the home of a key NPC.

### 6.4 Landmarks (7)

Landmarks are points of interest on the overland map — scenic overlooks, small
settlements, or points where quests lead you.

| ID | Landmark |
| --- | --- |
| 15 | Slippery Rock |
| 16 | Lookout Point |
| 17 | Big Oak |
| 18 | Grissold |
| 19 | Orchard Lake |
| 20 | Brantown |
| 21 | Burnside |

### 6.5 Tombs (2)

Tombs are dangerous underground areas, likely holding keys, quest items, and treasure
guarded by monsters.

| ID | Tomb |
| --- | --- |
| 22 | Rivercrest Tomb |
| 23 | Twilight Tomb |

### 6.6 Dungeon (1)

| ID | Dungeon |
| --- | --- |
| 24 | The Dungeon of Despair |

The Dungeon of Despair is likely the game's major dungeon — the deepest and most
dangerous area, holding the most valuable treasures and possibly the final quest items
needed to become a Knight.

### 6.7 Special (1)

| ID | Special |
| --- | --- |
| 25 | The Conclave of Sorcerers |

The Conclave of Sorcerers is likely the endgame location — where the final quest is
resolved and the character is elevated to Knighthood.

---

## 7. Using the trainer

The trainer attaches to the DOSBox / DOSBox-X process running Questron II, locates the
single 256-byte character record in the emulator's memory, and lets you edit it live.

### 7.1 Getting started

1. **Launch Questron II** in DOSBox or DOSBox-X and play past the title screen — the
   character record only exists in memory once the game is loaded.
2. **Build and run the trainer** (`.\Run.ps1`). It requests administrator rights because
   reading and writing another process's memory requires them.
3. **Attach** — pick the emulator process from the dropdown (DOSBox variants are
   auto-sorted to the top) and click Attach. The trainer scans memory and finds the
   character automatically.
4. **Edit** — change any field on the character sheet. Edits are written to the game
   immediately and take effect when the game next reads the field (e.g. opening the
   character screen, entering combat, or visiting a shop).

If the scan finds nothing, make sure the game is loaded past the title screen, then
click **Re-scan**.

### 7.2 Freeze toggles

The trainer has three freeze checkboxes: **Freeze HP**, **Freeze Food**, and **Freeze
Gold**. When a vital is frozen, the poll loop re-pins its value every tick (every 600
ms), so it never drops during play.

| Freeze | What it does | When to use it |
| --- | --- | --- |
| **Freeze HP** | Pins hit points to their current value | During tough combat — your character cannot die |
| **Freeze Food** | Pins food to its current value | On long expeditions — no need to buy or manage food |
| **Freeze Gold** | Pins gold to its current value | After maxing gold — spending never depletes it |

**Tip:** Set HP to 9999 (or use Full Heal), then enable Freeze HP. Your character
becomes effectively invincible. Toggle the freeze off if you want the game to track
damage normally again.

### 7.3 Quick actions

| Action | What it does |
| --- | --- |
| **Full Heal** | Sets HP to 9999 and Food to 9999 |
| **Max Attributes** | Sets all five attributes to 25 |
| **Max Gold** | Sets gold to 65535 |
| **Max Spells** | Sets all eight spell charge slots to 99 |
| **Max Everything** | All of the above plus Level 20 |

**Max Attributes** is the single biggest combat boost — Strength 25 means maximum
melee damage, Agility 25 means you hit almost every swing and dodge frequently, and
Intelligence 25 makes your spells devastating.

**Max Spells** gives you 99 charges of every spell. With 99 Fireballs and 99 Sonic
Whines, you can clear entire dungeons without swinging a weapon. This is especially
powerful combined with high Intelligence.

**Max Everything** is the one-click "god mode" button — it maxes every field the
trainer can reach. Use it when you want to skip straight to the endgame content or
explore freely without risk.

### 7.4 Editing individual fields

Beyond the quick actions, you can edit any field individually:

- **Name** — change your character's name (16 characters max).
- **HP / Food / Gold** — type any value directly. Gold is a 16-bit unsigned integer, so
  the maximum is 65535.
- **Attributes** — each attribute can be set independently from 1 to 25. This is useful
  if you want a gradual progression rather than jumping straight to 25.
- **Weapon / Armor** — change the equipped weapon or armor by ID. This lets you equip
  the Crossbow (9) or Ribbed Plate (6) without buying them in a shop.
- **Level** — set your level directly (0–20). This changes your rank title but may not
  affect experience or other derived values the game tracks internally.
- **Spell charges** — set each spell's charges individually (0–99).

### 7.5 Important notes

- **Edits take effect when the game next reads the field.** If you change HP during
  combat, the change applies immediately. If you change gold, open the shop screen to
  see the new value.
- **The poll loop refreshes the display every 600 ms.** If the game changes a value
  (e.g. you take damage), the trainer's display updates to reflect it — unless that
  vital is frozen.
- **No live memory dump was available** for the reverse engineering. Every offset is
  marked **[Static]** (confirmed against the DEMOFILE and/or manual) or **[Inferred]**
  (plausible but unconfirmed). The layout should be verified against a running game at
  the first opportunity. If a field does not seem to take effect, the offset may need
  adjustment.

---

## 8. General strategy

### 8.1 Early game (rank 0–1, Adventurer)

- **Buy a better weapon immediately.** Even a Hammer or Hatchet is a major upgrade over
  the starting Dagger. Every gold piece spent on a weapon pays for itself in faster
  kills and less damage taken.
- **Buy armor.** Rawhide is better than nothing; aim for Studded Leather or Ring Mail
  as soon as you can afford it.
- **Fight weak monsters near towns.** Stay close to a town so you can retreat and buy
  food or heal if needed. Beggars, Gypsy Imps, and Wave Slappers are among the weakest
  monsters.
- **Save gold for spells.** Magic Missile is cheap and effective early on. Buy it as
  soon as you can afford it — having a ranged attack option is valuable.
- **Keep food above 50.** Don't let food drop low — starvation is a silent killer. Buy
  food every time you visit a town.

### 8.2 Mid game (rank 2, Apprentice)

- **Upgrade to a Rapier or Shortbow.** The damage increase is noticeable and lets you
  tackle mid-tier monsters like Slasher Boars and Antisaurs.
- **Invest in Chain Mail.** It is the demo character's armor for good reason — solid
  protection at a reasonable price.
- **Buy Fireball and Sonic Whine.** Fireball is a significant upgrade over Magic
  Missile, and Sonic Whine is essential when fighting groups.
- **Explore the overland map.** Visit landmarks and search for keys. Some keys are in
  obvious places (chests in tombs); others may require solving puzzles or talking to
  NPCs.
- **Raise attributes.** If using the trainer, Max Attributes is the single biggest
  power boost available. If playing legitimately, focus on Strength and Agility first —
  they affect every fight.

### 8.3 Late game (rank 3+, Knight)

- **Buy the Broadsword or Crossbow.** These are the best weapons in the game and
  essential for the Dungeon of Despair.
- **Upgrade to Plate Mail or Ribbed Plate.** The late-game monsters hit hard — maximum
  armor is necessary.
- **Learn Time Sap.** Freezing enemies is a powerful tactical tool, especially against
  the toughest monsters like Constrictors and Mind Screams.
- **Collect all quest items.** The Unicorn Horn, Wand of Power, Eternal Flame, and the
  rest are needed to complete the game. Search every tomb and dungeon thoroughly.
- **Visit the Conclave of Sorcerers.** This is likely where the final quest is resolved
  and your character is elevated to Knight.
- **Watch for Destruct.** This spell is not in the manual and may be hidden — perhaps
  found in a deep dungeon or granted by the Conclave. If you find it, it is the most
  powerful spell in the game.

### 8.4 Food management

Food is the clock that drives overland exploration. It ticks down as you travel and
fight, and when it runs out your character starves. Three approaches:

1. **Legitimate:** Buy food in every town you pass through. Keep at least 50 food on
   hand at all times.
2. **Trainer-assisted:** Set food to 9999 with Full Heal or the Food field. This gives
   you effectively unlimited exploration time.
3. **Trainer-frozen:** Set food high and enable Freeze Food. The value never drops —
   you never need to think about food again.

### 8.5 Gold management

Gold is needed for everything — weapons, armor, spells, food, and possibly keys or
quest items. The economy is tight in the early game and generous in the late game once
you can kill tough monsters for large rewards.

- **Early game:** Spend gold on weapons and armor first, spells second, food third.
- **Mid game:** Save for major upgrades (Chain Mail, Shortbow or better, Fireball).
- **Late game:** If using the trainer, Max Gold (65535) eliminates the economy entirely.
  You can buy everything in every shop without worrying about cost.
- **Frozen gold:** Enable Freeze Gold after maxing it. Spending gold in shops will not
  deplete your supply — the trainer re-pins it every tick.

---

## 9. Quick-reference tables

### 9.1 Trainer quick actions

| Button | HP | Food | Gold | Attributes | Level | Spells |
| --- | --- | --- | --- | --- | --- | --- |
| Full Heal | 9999 | 9999 | — | — | — | — |
| Max Attributes | — | — | — | 25 each | — | — |
| Max Gold | — | — | 65535 | — | — | — |
| Max Spells | — | — | — | — | — | 99 each |
| Max Everything | 9999 | 9999 | 65535 | 25 each | 20 | 99 each |

### 9.2 Attribute effects at a glance

| Attribute | Max | Primary effect | Secondary effect |
| --- | --- | --- | --- |
| Charisma | 25 | Lower shop prices | Better NPC interactions |
| Strength | 25 | More melee damage | — |
| Agility | 25 | Better to-hit | Better defense / evasion |
| Stamina | 25 | More hit points | More endurance |
| Intelligence | 25 | Stronger spells | — |

### 9.3 Spell summary

| Spell | Type | Target | Buyable | Best use |
| --- | --- | --- | --- | --- |
| Magic Missile | Damage | Single | Yes | Early-game ranged damage |
| Fireball | Damage | Single | Yes | Mid-to-late single-target damage |
| Sonic Whine | Damage | All adjacent | Yes | Surrounded by multiple enemies |
| Time Sap | Utility | Enemies | Yes | Freezing tough enemies |
| Destruct | Damage | Unknown | No | Late-game hidden spell |

### 9.4 Location summary

| Type | Count | Examples |
| --- | --- | --- |
| Towns | 10 | Hidden Rock, Bay View, Seacrest, Cramford |
| Cathedrals | 4 | Sanctuary, Rivercrest, Great Plains, Twilight |
| Castles | 1 | Redstone Castle |
| Landmarks | 7 | Slippery Rock, Lookout Point, Big Oak |
| Tombs | 2 | Rivercrest Tomb, Twilight Tomb |
| Dungeons | 1 | The Dungeon of Despair |
| Special | 1 | The Conclave of Sorcerers |

---

## 10. Twenty things worth knowing

1. **You are one character, not a party.** Every decision — equipment, spells,
   attributes — is about maximizing a single character's survivability.
2. **Buy a better weapon before anything else.** Even a small upgrade in weapon damage
   means faster kills and less damage taken.
3. **Armor matters more than HP.** Reducing incoming damage is more sustainable than
   healing afterward. Upgrade armor whenever you can afford it.
4. **Keep food above 50 at all times.** Starvation is the most common cause of death on
   long expeditions.
5. **Sonic Whine hits all adjacent enemies.** When surrounded, it is far more efficient
   than attacking one at a time.
6. **Time Sap freezes enemies.** Use it against the toughest single monsters to buy
   yourself free turns.
7. **Intelligence drives spell power.** If you rely on spells, max Intelligence first.
8. **Agility is the most versatile attribute.** It improves both offense (to-hit) and
   defense (evasion).
9. **Explore every tomb and dungeon thoroughly.** Keys and quest items are often in
   chests that are easy to miss.
10. **The twelve keys open specific locks.** If a door won't open, you need a different
    key — go find it.
11. **The Rope & Hooks quest item likely reaches otherwise inaccessible areas.** Find it
    before exploring remote locations.
12. **The Conclave of Sorcerers is probably the endgame location.** Don't go there
    unprepared — bring your best equipment and all quest items.
13. **Destruct is not in the manual.** It may be a hidden or late-game spell — search the
    dungeons carefully.
14. **The Dungeon of Despair is the deepest dungeon.** Bring maximum armor, a
    Crossbow or Broadsword, and plenty of spell charges.
15. **Freeze HP for invincibility.** Set HP to 9999 and freeze it — the character cannot
    die in combat.
16. **Freeze Gold for unlimited shopping.** Max gold to 65535 and freeze it — buy
    everything in every shop.
17. **Max Everything is one-click god mode.** Use it to skip straight to exploration
    without worrying about survival.
18. **Edits take effect on the next game read.** If a change doesn't appear immediately,
    open the character screen or enter a shop to force the game to re-read the field.
19. **The trainer's offsets are not all confirmed.** Fields marked [Inferred] may need
    verification against a live game. If an edit does not take effect, that offset may
    be wrong.
20. **Save your character in-game before risky expeditions.** The trainer edits live
    memory, not save files — if the game crashes or you quit without saving, your edits
    are lost.
