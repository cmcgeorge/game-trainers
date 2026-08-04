# Dark Designs I: Grelminar's Staff — Strategy Guide

## Overview

**Dark Designs I: Grelminar's Staff** is a 1990 DOS role-playing game by **John Carmack**, published by Softdisk / Big Blue Disk. You lead a party of up to four adventurers through a ruined castle to retrieve the eponymous Staff of Grelminar, which holds the power to close rifts in the continuum. The game uses a first-person 3D view with a party status panel, auto-map, and message window.

## Story

A mysterious warlord has appeared in the mountains of the north, massing an army backed by otherplanar creatures — demons, devils, and chaos avatars — that pour out of Mount Delkeina to replace his losses. The major nations squabble and refuse to act, so the borderlands' only defense is its stock of hardy adventurers.

The warlord's reinforcements must be cut off. The gate the monsters use must be closed. The only one with the power to do this — the great wizard Grelminar — died years ago, and his castle has decayed to a ruin populated by evil creatures. You must retrieve Grelminar's staff from his private laboratory on the third floor of the castle and return it to the town of Taprobale.

## Controls

### Town of Taprobale (Main Menu)

| Key | Action |
|---|---|
| **1–4** | Examine a character in the party |
| **A** | Add a character to the party |
| **R** | Remove a character from the party |
| **C** | Create a new character |
| **D** | Delete a character permanently |
| **H** | Heal at the temple |
| **E** | Equipment shop (buy/sell) |
| **L** | Learn spells |
| **G** | Go to Grelminar's Castle |
| **Q** | Quit and save |

### Castle (Dungeon Exploration)

| Key | Action |
|---|---|
| **Up Arrow / I** | Move forward |
| **Down Arrow / K** | Turn around (180°) |
| **Left Arrow / J** | Turn left |
| **Right Arrow / L** | Turn right |
| **S** | Search wall for secret door |
| **Ctrl+S** | Toggle sound on/off |
| **Q** | Save position |
| **1–4** | Inspect individual characters |
| **Esc** | Quit |

### Character Examination

| Key | Action |
|---|---|
| **I** | Items (Use, Trade, Drop, Ready) |
| **S** | Spells (cast known spells) |

### Combat

| Key | Action |
|---|---|
| **F** | Fight |
| **R** | Run away |
| **A** | Attack |
| **S** | Cast Spell |
| **U** | Use item |
| **X** | Exchange weapons (swap left/right hand) |
| **F** | Forward (move to front rank) |
| **B** | Back (fall back to rear rank) |
| **Space** | Pass (skip turn) |
| **A–K** | Select target monster |
| **1–4** | Select target character |
| **Esc** | Go back |
| **Enter** | Start round / confirm |
| **A–H** | Select spell (when casting) |

## Character Creation

### How the roll works

The create screen rolls **five values at once** and lets you place them on the attributes in any
order with the arrow keys and Return; **R** throws the whole set away and rolls a new one. Because
you choose the placement, what matters is the *shape* of the set, not which slot a number came out
in — a roll with an 18 in it is an 18 for whichever attribute you want.

Each value is `10 + random(5) + random(5)` — measured over 2,000 values read out of the running game
(see [Reverse Engineering](ReverseEngineering.md#5-character-creation-the-rolled-stat-pool)). That
gives a symmetric spread over **10–18** with a mean of 14, and the five values total 50–90 (mean 70):

| Value | 10 | 11 | 12 | 13 | 14 | 15 | 16 | 17 | 18 |
|---|---|---|---|---|---|---|---|---|---|
| Chance | 4% | 8% | 12% | 16% | 20% | 16% | 12% | 8% | 4% |

Practical consequences: the game is generous (it never rolls below 10), and roughly **one roll in
five** contains at least one 18 — so re-rolling for a single strong prime attribute is quick. A set
where *every* value is 15 or better is a different matter: about **1 in 98** rolls. That is why the
trainer's Create tab automates the re-rolling.

### Attributes

Five attributes are rolled randomly and assigned by the player:

| Attribute | Effect |
|---|---|
| **Strength** | Damage dealt by melee attacks; important for Fighters and Priests |
| **Dexterity** | Determines combat initiative (who goes first); dodge ability |
| **Constitution** | Determines hit points (Body); resists some spells |
| **Intelligence** | Determines Wizard magic points; resists some spells |
| **Piety** | Determines Priest magic points; affects healing spell effectiveness |

### Classes

| Class | Role | Restrictions |
|---|---|---|
| **Fighter** | Master at arms; high STR, CON, DEX | Can use all weapons and armor |
| **Priest** | Healer and undead-slayer; high PIE | No pointed weapons; limited armor (needs mobility for casting) |
| **Wizard** | Offensive spellcaster; high INT | Very limited armor (needs mobility for casting) |

### Recommended Party

The manual suggests **two Fighters, one Priest, and one Wizard**. This is a solid all-rounder party:

- **Fighter 1 (front rank)**: High STR/CON/DEX — tank and primary damage dealer
- **Fighter 2 (front rank)**: High STR/CON/DEX — secondary tank/damage
- **Priest (back rank)**: High PIE — healing and anti-undead
- **Wizard (back rank)**: High INT — offensive spells (Fireball, Lightning Bolt)

### Creation Tips

- Press **R** to re-roll attributes until at least one is 17+ (put it in the class's prime
  attribute) — nearly half of all rolls contain one, so it rarely takes long
- Fighters: prioritize STR > CON > DEX > PIE > INT
- Priests: prioritize PIE > CON > STR > DEX > INT
- Wizards: prioritize INT > DEX > CON > PIE > STR
- Buy equipment at the shop and **Ready** it before entering the castle

## Spells

### Wizard Spells

| Slot | Spell | Cost | Effect |
|---|---|---|---|
| A | Magic Missile | 50g | Auto-hit bolt; ~short sword damage. Costs 1 MP. |
| B | Speed | 100g | Raises target's Dexterity for the combat. |
| C | Strength | 150g | Raises target's Strength for extra damage. |
| D | Stun | 200g | Target stands motionless for several rounds. |
| E | Lightning Bolt | 250g | Strong single-target; 1–7 damage per caster level. |
| F | Fireball | 300g | Damages entire enemy column; 1–5 damage/level. |
| G | Flame Strike | 350g | Burns all monsters; 1–4 damage/level. |
| H | Death Ray | 400g | Usually kills one monster outright. |

### Priest Spells

| Slot | Spell | Cost | Effect |
|---|---|---|---|
| A | Cure Light Wounds | 50g | Heals several Body points. Scales with level + Piety. |
| B | Dispel Undead | 100g | Destroys weaker undead (skeletons, zombies, ghosts, liches). |
| C | Bless | 150g | 25% of attacks against the blessed character are warded off. Combat duration. |
| D | Cure Serious Wounds | 200g | Heals a few dozen Body points. |
| E | Death's Door | 250g | Revives a KO'd character (0 Body but can act). |
| F | Banishment | 300g | Damage scales with target's evil alignment; demons/devils hit hardest. |
| G | Word of Recall | 350g | Instantly teleports the party back to town. |
| H | Cureall | 400g | Restores a character to maximum Body points. |

**Spell point costs**: Spell A costs 1 MP, Spell B costs 2 MP, … Spell H costs 8 MP. Magic points are restored when returning to town (resting).

## Items

Every figure below is read straight from the item table in the game's own executable: **Dam**age or
effect power, shield **Prot**ection, shop **Price**, and which classes may use it (**F**ighter,
**P**riest, **W**izard). **ID** is the byte the game stores in an inventory slot, which is what the
trainer's dropdowns are indexed by.

You carry **ten items** (item screen keys `A`–`J`) and ready **four** of them at a time: right hand,
left hand, armor, ring. Buying does not equip — `(I)tems → (R)eady` does.

### What can go where

The game sorts every item into a type, and each readied slot accepts only certain types:

| Slot | Accepts |
|---|---|
| Right hand | any weapon or shield (light, medium, or two-handed) |
| Left hand | **light** items only — daggers, short swords, shields |
| Armor | armor |
| Ring | rings |

So you can pair a two-handed weapon with nothing, or a one-handed weapon with a shield or dagger.
Anything else gets *"Wrong type!"*. Wands, potions, scrolls and keys are never readied — they are
carried and used from the item screen.

### Weapons

| ID | Weapon | Hands | Dam | Price | Classes |
|---|---|---|---|---|---|
| 1 | Dagger | light | 3 | 5 | F · W |
| 2 | Staff | two-handed | 5 | 10 | F P W |
| 3 | Mace | medium | 6 | 15 | F P · |
| 4 | Short Sword | light | 7 | 20 | F · · |
| 5 | Long Sword | medium | 9 | 30 | F · · |
| 6 | Battle Axe | two-handed | 10 | 40 | F · · |
| 7 | Two Hand Sword | two-handed | 11 | 50 | F · · |
| 34 | Trident of Pain | two-handed | 15 | 2,010 | F · · |
| 38 | Striking Staff | two-handed | 10 | 2,500 | F P W |
| 30 | Vampiric Sword | medium | 10 | 2,500 | F · · |
| 29 | Mangling Mace | medium | 10 | 3,000 | F P · |
| 40 | Active Axe | two-handed | 10 | 3,500 | F · · |
| 16 | Hell Dagger | light | 15 | 4,000 | F P W |
| 17 | Gravedigger Axe | two-handed | 13 | 5,000 | F · · |
| 35 | Electroblade | medium | 12 | 5,000 | F · · |
| 37 | Boom Blade | medium | 14 | 5,000 | F · · |
| 39 | Bone Basher | two-handed | 13 | 5,000 | F P · |
| 31 | Holy Sword | medium | 12 | 7,000 | F · · |
| 33 | Old Dark Sword | medium | 15 | 32,768 | F · · |

The two best weapons in the game are the **Hell Dagger** (15 damage, and the only top-tier weapon
every class can hold — it is *light*, so it also fits the left hand) and the **Old Dark Sword**
(15 damage). The Old Dark Sword's price is exactly 0x8000, which reads as −32,768 signed; treat it
as "not realistically purchasable" rather than a real number.

Best value early: the **Staff** at 10 gold does 5 damage and every class can use it. The
**Two Hand Sword** at 50 gold is the strongest thing a starting fighter can afford.

### Shields

Shields count as *light*, so they go in the left hand — or the right, if you would rather hit with
one.

| ID | Shield | Prot | Dam | Price | Classes |
|---|---|---|---|---|---|
| 9 | Spiked Shield | 15 | 3 | 35 | F · · |
| 8 | Shield | 30 | 0 | 25 | F P · |
| 54 | Bad Buckler | 30 | 6 | 4,500 | F · · |
| 11 | Magic Shield | 55 | 0 | 2,000 | F · · |

The plain **Shield** is the bargain — more protection than the Spiked Shield for less money. The
**Bad Buckler** matches it for protection and adds 6 damage, which is a real weapon's worth.

### Armor

| ID | Armor | Rating | Price | Classes |
|---|---|---|---|---|
| 10 | Leather Armor | 2 | 20 | F P W |
| 12 | Chain Mail | 4 | 50 | F P · |
| 14 | Plate Mail | 6 | 100 | F P · |
| 15 | Full Plate | 7 | 250 | F · · |
| 13 | Magic Armor | 8 | 3,000 | F P · |

Leather is the only armor a wizard may wear, which is exactly what the manual says about needing
mobility to cast.

### Using an item may destroy it

There are no charges in Dark Designs. When you `(U)se` something, the game applies its effect and
then rolls to see whether the item survives — and the odds are per item type, fixed in the
executable:

| Item | Survives a use |
|---|---|
| Cureall Potion | 99.6% |
| Recall Scroll | 97.7% |
| Extra Healing | 95.7% |
| Healing Potion | **50%** |
| Medusa Skull | 19.5% |
| Wand of Evil | 11.3% |
| Paralyze Wand | 3.9% |
| Keys 1–3 | never — always consumed |

This is worth planning around. A Healing Potion is a coin-flip every time you drink it, so carry
several. The expensive consumables are the *durable* ones: a Cureall Potion at 1,500 gold survives
199 uses out of 200 on average, which makes it far better value than its price suggests, and an
Extra Healing at 500 gold will usually outlast a stack of ordinary potions. The Paralyze Wand, by
contrast, is effectively single-use at 2,000 gold.

Keys are always destroyed, so buy spares — at 10 gold each there is no reason not to.

The same roll governs **magic weapons**: it decides whether their special effect fires on a hit.
Gaze 97.7%, Trident of Pain and Active Axe 78.1%, Old Dark Sword 31.2%, Holy Sword 30.1%,
Gravedigger Axe 25.8%, Vampiric Sword and Electroblade 19.5%, Mangling Mace 17.6%, Boom Blade 9.8%.
That is a real argument for the Trident of Pain at 2,010 gold: same 15 damage as the Old Dark
Sword, and its effect fires two and a half times as often.

### Wands, potions, rings and keys

| ID | Item | Power | Price | Classes | Notes |
|---|---|---|---|---|---|
| 20 | Healing Potion | 11 | 150 | F P W | Restores Body points |
| 21 | Extra Healing | 14 | 500 | F P W | Restores more Body points |
| 22 | Cureall Potion | 18 | 1,500 | F P W | The strongest heal |
| 26 | Recall Scroll | 17 | 1,500 | · P · | Word of Recall effect; priests only |
| 18 | Paralyze Wand | 4 | 2,000 | · · W | Paralyses the target; wizards only |
| 19 | Wand of Evil | 8 | 3,500 | · · W | Wizards only |
| 23 | Medusa Skull | 22 | 7,000 | F P W | Stone gaze effect |
| 24 | Speed Ring | — | 1,000 | F P W | Raises Dexterity (ring slot) |
| 25 | Strength Ring | — | 2,000 | F P W | Raises Strength (ring slot) |
| 60 | Key 1 | — | 10 | F P W | Unlocks door type 1 |
| 61 | Key 2 | — | 10 | F P W | Unlocks door type 2 |
| 62 | Key 3 | — | 10 | F P W | Unlocks door type 3 |
| 63 | The Staff | — | — | F P W | Grelminar's Staff — the quest item |

All three keys cost 10 gold each. Buy them.

### Ids you will not see in a shop

Ids 41–59 (bar 54) are the monsters' own gear — `Hide`, `Thick Hide`, `Scales`, `Plated Hide`,
`Shell`, `Shell & Scales`, `Nip`, `Claw`, `Big Claw`, `Huge Claw`, `Bite`, `Big Bite`, `Huge Bite`,
`Bash`, `Hard Bash`, `Tail`, `Horn`, `Spikes` — plus `Gaze` at 32. They are valid ids a character
*can* hold, but none of them is sold. Ids 27, 28 and 36 are blank table entries.

## Monsters

The castle is inhabited by a progression of increasingly dangerous creatures:

### Level 1 Monsters
Kobold, Kobold Leader, Kobold Priest, Orc Chief, Goblin, Wolf, Skeleton, Zombie, Ghost, Mummy, Pixie, Bugbear, Lizard Man

### Level 2 Monsters
Lich, Lich Fighter, Manticore, Minotaur, Ogre, Ogre Mage, Troll, Evil Fighter, Evil Cleric, Evil Mage, Gargoyle, Ettin, Giant, Basilisk, Medusa

### Level 3+ Monsters
Iron Gargoyle, Golem, Evil Unicorn, Fire Elemental, Air Elemental, Water Elemental, Earth Elemental, Quasit, Hellhound, Ice Demon, Flame Devil, Death Knight, 3 Head Hydra, Demon Lord, Chaos Avatar

**Special monster abilities** (from the EXE strings):
- **Stone Gaze**: Medusa can petrify characters
- **Flaming Breath**: Fire-breathing monsters
- **Paralyzes**: Some monsters can paralyze
- **Drains Life**: Vampiric attacks drain Body points
- **Chaos Avatar**: Reflects spells back at the caster

## Combat Strategy

### General Principles

1. **Dexterity determines initiative** — characters with higher DEX act first
2. **Front rank vs. rear rank** — only front-rank characters can make melee attacks; rear-rank characters are safe from melee but can cast spells or use ranged items
3. **Two weapon slots** — each character has a right hand and left hand; you can use a weapon + shield, two weapons (both attack the same target), or a two-handed weapon
4. **Running is risky** — monsters get a free attack if you fail to escape

### Tactics by Party Composition

**Two Fighters / One Priest / One Wizard** (recommended):
- Round 1: Both Fighters Attack, Priest casts Bless on a Fighter, Wizard casts Fireball or Flame Strike
- Round 2+: Fighters Attack, Priest heals as needed (Cure Light/Serious), Wizard continues offensive spells
- If someone is KO'd: Priest casts Death's Door immediately
- If overwhelmed: Wizard casts Word of Recall to escape to town

**Against undead**: Priest casts Dispel Undead — weaker undead are destroyed instantly
**Against demons**: Priest casts Banishment — damage scales with evil alignment
**Against single tough monsters**: Wizard casts Death Ray
**Against groups**: Wizard casts Fireball (column) or Flame Strike (all)

### Healing Economy

- **In combat**: Cure Light Wounds (1 MP) is most MP-efficient; Cure Serious (4 MP) for emergency healing; Cureall (8 MP) to top off
- **In town**: The temple heals everyone for a gold donation based on how much healing is needed
- **Resting**: Returning to town restores all Magic points (assumed rest)
- **Strategy**: Go into the castle, fight until your Priest is low on MP, return to town to rest and heal, repeat

## Walkthrough

### Step 1: Character Creation and Preparation

1. Start the game. Four pre-made characters are provided.
2. Alternatively, create your own party (recommended for a fresh challenge):
   - Create 2 Fighters, 1 Priest, 1 Wizard
   - Re-roll until each has a 17+ in their prime attribute
3. Buy equipment for each character at the shop
4. **Ready** the equipment (press character number → I → R)
5. Learn spells for your Priest and Wizard

### Step 2: Enter the Castle

1. Press **G** at the town menu to enter Grelminar's Castle
2. You start on the **Ground Level** at **(16, 31)**, outside the castle gate, facing north
3. Walk forward (Up Arrow) 6 times to reach the gate, then once more to enter

### Step 3: Explore the Castle

The castle is five levels. The game numbers them from the top down, so *going down* stairs makes the
level number go *up*:

1. **Top Castle Level** — Grelminar's staff room and his lab. **The Staff is here.**
2. **Mid Castle Level** — The royal floor: throne room, the main treasury, the commander's safe.
3. **Ground Level** — The entrance floor. You start here, outside the gate.
4. **Dungeon Level 1** — The first underground level: the most chests of any level, tougher monsters.
5. **Dungeon Level 2** — The deepest level. The lich's living quarters and the hardest monsters.

Full maps of all five, with every stairway, chest, locked and secret door marked, are under
[Maps](#maps) below.

### Step 4: Find the Staff

The Staff of Grelminar sits on the **Top Castle Level** at **(20, 22)**, in the staff room past his
lab. You need to:

1. From the Ground Level, take the stairs up at **(9, 13)** to the Mid Castle Level
2. Take the stairs up at **(12, 7)** to the Top Castle Level
3. Work east and then south to the staff room in the south-east of the level
4. Search for secret doors (press **S** while facing walls) — parts of the route are hidden behind them
5. Step onto (20, 22) — "You find an item!"

### Step 5: Escape and Win

1. Once you have the Staff, make your way back down through the castle
2. Exit the castle and return to the town of Taprobale
3. The game displays the victory screen:

> **CONGRATULATIONS!**
> You have retrieved Grelminar's staff!
> Your band of adventurers must now head into the heart of the Warlord of the north's domain to use the power of the staff to close the otherplanar portal at the bottom of Mount Delkeina.
>
> Watch for **Dark Designs II: Closing the Gate** — Coming soon!

### Step 6: Using the Staff at the Gate

If you find a gate (the otherplanar portal) in the dungeon levels, use the Staff on it:

> The staff shoots a blinding pulse of energy at the swirling gate, and as the gate swallows itself up the entire mountain begins to shake violently. You get the idea that you had better get out of here RIGHT NOW!!!

## Maps

The castle is five levels, each a **32 x 32 grid** stored in its own `DDMAP<n>.DAT`. **X runs east**
across the level and **Y runs south** down it, both 0-31, and the game numbers the levels from the
top of the castle down - so *going down* stairs makes the level number go *up*:

| Level | Name | File | Role |
|---|---|---|---|
| 1 | Top Castle Level | `DDMAP1.DAT` | Grelminar's lab - **the Staff is here** |
| 2 | Mid Castle Level | `DDMAP2.DAT` | Connector; the main treasury |
| 3 | Ground Level | `DDMAP3.DAT` | Entrance and exit - you start outside the gate at **(16, 31)** |
| 4 | Dungeon Level 1 | `DDMAP4.DAT` | Better treasure, tougher monsters |
| 5 | Dungeon Level 2 | `DDMAP5.DAT` | Deepest and most dangerous |

The stairways line up exactly - the same square on both levels - which makes the whole castle one
column you can walk straight up and down:

```
   Top Castle Level  (1)          (12,  7) down
   Mid Castle Level  (2)  (12,  7) up      (9, 13) down
   Ground Level      (3)   (9, 13) up     (20, 13) down     entrance (16, 31)
   Dungeon Level 1   (4)  (20, 13) up      (5, 18) down
   Dungeon Level 2   (5)   (5, 18) up
```

The game draws its own **auto-map** in the upper-right of the screen, but only for squares you have
already stood on. The maps below are the complete levels, read straight out of the map files; the
trainer's **Maps tab** draws the same thing live, marks where you are, and can teleport you.

### Reading the maps

Y increases downward, so **north is up** on the page, exactly as the game shows it.

| Glyph | Meaning |
|---|---|
| `-` `\|` | Wall |
| `.` `:` | Door - walk straight through |
| `#` | **Locked** door - needs Key 1, 2 or 3 |
| `s` | **Secret** door - press `S` facing it to find it; it looks like a plain wall in game |
| `<` `>` | Stairs up / stairs down |
| `$` | Treasure chest |
| `*` | Item - the two fixed items, including **THE STAFF** at (20, 22) on the Top Castle Level |
| `!` | Ledge - walking here drops you to the level below and hurts |

The secret doors are shown here because this is a guide; the game itself, and the trainer's map,
draw them as ordinary walls until you have searched them out.

### 1. Top Castle Level — `DDMAP1.DAT`

Occupies X 5–26, Y 5–26. Stairs down (12, 7); item (20, 22); 9 chest squares.

```
                1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2
      5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6

   5
       ------                            ------
   6   |     |                           |     |
             ----------------------------
   7   |     :   :  >|                   :     |
                 ----  ------------------
   8   |     |   :     |     :           |     |
       ------..--------##----            ..----
   9       |     |$ $    |   |           | |
                             ----------..
  10       |     |$ $    |   |           | |

  11       |     |$ $    :   |           | |
           ##--------------------
  12       |                     :       | |

  13       |                     :       | |
           ..--------------------
  14       | | | | | | | | | |           | |

  15       | | | | | | | | | |           | |
             --  --  --  --  ----------..
  16       |                 |           | |
             --  --  --
  17       | | | | | | |     |           | |
                             ..----------
  18       | | | | | | |     |           | |
                             ----------..
  19       | | | | | | |     |           | |
           --------------..--..----------
  20       |                 | |         | |
           ss----..--..--..--
  21       |  $|   |   |     | :         | |

  22       |  $|   |   |     | :    *    | |
       ------                            ..----
  23   |     |$|   |   |     | |         |     |
             ----------------------------
  24   |     :                           :     |
             ----------------------------
  25   |     |                           |     |
       ------                            ------
  26

```

### 2. Mid Castle Level — `DDMAP2.DAT`

Occupies X 5–26, Y 5–26. Stairs up (12, 7); stairs down (9, 13); edge (24, 15), (24, 16), (24, 17), (24, 18); 6 chest squares.

```
                1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2
      5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6

   5
       ------                            ------
   6   |     |                           |     |
             ----------------------------
   7   |     | s   |<      #             |     |
                   --------
   8   |     |$|   |$ $    |             |     |
       ----..--                          ..----
   9       |   :   |$ $ $  s             | |
                   --------          ..--..
  10       |   |   s       |         |     |
           ....    ------          --
  11       | | |   |     | |       |       |
               ----              --
  12       | | |         | |     |         |
                               --
  13       | |>|         s |   |           |
             ----------..ss----
  14       | :                 :           |

  15       | :                 :            !
             ----------....----
  16       | :       |       | |            !
                               --
  17       | |       |       |   |          !
                                 --
  18       | |       |       |     |        !
             ----------------      --
  19       | :               |       |     |
                             ------....----
  20       | :                   |         |
             ..--..--..--..--..--
  21       | |   |   |   |   |   |         |

  22       | |   |   |   |   |   |         |
       ----..                    --------------
  23   |     |   |   |   |   |   |       |     |
             ----------------------------
  24   |     :                           :     |
             ----------------------------
  25   |     |                           |     |
       ------                            ------
  26

```

### 3. Ground Level — `DDMAP3.DAT`

Occupies X 0–31, Y 0–31. Stairs up (9, 13); stairs down (20, 13); 5 chest squares.

```
                          1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2 3 3
      0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1

   0

   1
         --------------------------------------------------------
   2     |                                                       |
           ----------------------------------------------------
   3     | |                       | |                         | |
                                 --  --
   4     | |                     |     |                       | |
                                 --  --
   5     | |                       | |                         | |
                 ------            ##              ------
   6     | |     |     |                           |    $|     | |
                       ----------------------------
   7     | |     |     |       |           | s     s    $|     | |

   8     | |     |     |       |           |$|     |     |     | |
                 ----....------            --      ------
   9     | |         | :       :           |         |         | |
                       --..ss--
  10     | |         | | | |   |           |         |         | |
                               ----....--------..----
  11     | |         | | | |     |       |     : |   |         | |

  12     | |         | | | |     |       |     | |   |         | |
                       --                    --
  13     | |         | |<| |     |       |   |>| |   |         | |
                         ..------        ----
  14     | |         | : |       |       |   | | |   |         | |
                       --------
  15     | |         | |       : |       |   | | |   |         | |

  16     | |         | |$      | |       |   | | |   |         | |
                       --------  --....----......
  17     | |         | |       | |       :   :   |             | |

  18     | |         | |       : |       |   |   |             | |
                       --------
  19     | |         | |       : |       |   |   |   |         | |
                           ------        --------
  20     | |         | |$  |     |       |   |   |   |         | |
                     ..----..----
  21     | |         |     |     |       |   |   |   |         | |

  22     | |         |     |     |       |   |   |   |         | |
                 ------..----..----....--..----......----
  23     | |     |     :         :       :         |     |     | |
                       ..--------        --------..
  24     | |     |     |         |       |         |     |     | |
                       ------------....------------
  25     | |     |     |                           |     |     | |
                 ------                            ------
  26     | |                                                   | |

  27     | |                                                   | |

  28     | |                                                   | |
           --------------..--------    --------##--------------
  29     |               | :       |   |       : |               |
         ------------------                    ------------------
  30                       | | | | |   | | | | |
                           --------    --------
  31

```

### 4. Dungeon Level 1 — `DDMAP4.DAT`

Occupies X 0–28, Y 0–31. Stairs up (20, 13); stairs down (5, 18); 14 chest squares.

```
                          1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2
      0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8
                                           ------
   0                                       |     |
                                         --      --
   1                                     |         |
                   ------------------  --  --  --  --    ----
   2               |   : |     | :   | |   | | | |   |   |   |

   3               |   | |     | |  $| |   | | | |   |   |   |
           ------                      --  --  --  --  ----..
   4       |     | |   |         |  $|   |         |   |     |
                   ----          ----    --      --
   5       |     |     |         |         |     |     |     |
                                           --  --    --
   6       |     |     |         |           | |     | :     |
           --  --      ----..----
   7         | |           | |               | |     | |     |

   8         | |           | |               | |     | |    $|
         --                                            ------
   9     | | | |           | |               | |     | |
       --  --  ------------  ----------------  ------
  10   |                                               |
               ------  ------------  --------  ------
  11   |       |     | |           | |       | |     | |
             ------
  12   |     |     | | |           | |       | |     | |
         ----                          ----        --  --
  13   | |   :    $| | |           | | |$  | |<|   |     |
           --                                --
  14   |   | |     | | |           | | |   |       |     |
             ------                        --
  15   |   |         | |           | | |   : |     |     |
       --          --          ----..--ss--        --  --
  16     | |       | | |       |         | | |       | |
       --        --    ------                  ------  ------
  17   |   |     |   | |     | |         | | | |   :     :   |
     --    --  --                              ----      ----
  18 |       | |>    : |     | |         | | | |   :     :   |
     --        --              ----..----      ----      ----
  19 |       |   |   | |     | |$  | |$  | | | |   :     :   |
     ------      --                            ----      ----
  20 |     | |     | | |     | |   | |   | | | |   :     :   |
       --          --                          ----      ----
  21 | |   | |       | |     | |   | |   | | | |   :     :   |
       --              --..--                  ----      ----
  22 |$ $| | |       |     |   |   : :   | | | |   :     :   |
     ----            ------    ----ss----      ----      ----
  23 |     | |                     | |     | | |   #     #   |
     ----            --------------            ------ss------
  24     |   |       |               |     | |     |     |
         ----          ------------ss        ------
  25                 | |           | |     |             |
                                           --------
  26                 | |           | |             |     |
             --------..                            ------
  27         | #       |           | |
                       ------
  28         |$|       |     |     | |

  29         |$|       :    $|     | |
             ----------      ------
  30                   |    $s       |
                             --------
  31                   |    $|
                       ------
```

### 5. Dungeon Level 2 — `DDMAP5.DAT`

Occupies X 0–29, Y 0–26. Stairs up (5, 18); item (9, 1); 8 chest squares.

```
                          1 1 1 1 1 1 1 1 1 1 2 2 2 2 2 2 2 2 2 2
      0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9
             --------------------------
   0         |                     s   |
               --------ss--------  --
   1         | |       |*|       | | | |
     --------..--  ----  ----  --..--
   2 |     :     | |         | |     | |
                 --          --          ----------------------
   3 |     |     |             |     | | |   :     | |     :   |
           ------              ------    --                  --
   4 |     :   |                 |   | | | | |     | |     | | |
                                           --              --
   5 |$ $  |   |                 |   | | |   |$    | |    $|   |
     ------                              ..----          ----..
   6       |   |                 |   | | |     |   | |   |$    |
           ----------      ------              --  --  --
   7                 |     |         | | |       | : : |       |
                     --..--  --------            --  --
   8                 |     | |         | |         : :         |
                               --------  ----------  ----------
   9                 |     | | |         |         | |   | |   |

  10                 |     | | |         |         : |   : |   |
                                                         --
  11                 |     | | |         |         : |   | :   |
                     --..--
  12                   | |   | |         |         | |   | |   |
                                         ----------  ..------..
  13                   | |   | |         |                     |
                         ----            ..----..--  ..------..
  14                   | s     |         |   | |   | |   | |   |
                         ----------------
  15                   | |     |         |   : |   | |   | :   |
                                             --          --
  16                   | |     :         |   | :   | |   : |   |
           ------------##
  17       |             |     |         |   | |  $| |  $| |  $|
             ----------  --------..----------------..----------
  18       |    <|   : |   :                         |
           ------------  ----------------------------
  19                   | |         | |     | |     |
                       ..                              ------
  20                   | |         | |     | |     |   |     |
                               --##----..------..--
  21                   | |     |                   |   |     |
                         ------                    ----
  22                   |       :                   : # #     |
                     --..------                    ----
  23                 |     |   |                   |   |     |
                               --##----..------..--
  24                 |     |       | |     | |     |   |     |
                                                       ------
  25                 |     |       | |     | |     |
                     --------------  ------  ------
  26

```
### Navigation tips

- **Stairs** connect levels. Look for "Going up stairs..." or "Going down stairs..." messages.
- **Secret doors** are found by pressing **S** while facing a wall. The message "No Wall!" means you found one.
- **Locked doors** require the appropriate Key (Key 1, Key 2, or Key 3) - and the key is consumed on
  the roll described under *Using an item may destroy it*, so carry spares.
- **Treasure chests** are found by exploring - "You find a treasure chest!!!"
- **Items** are found by exploring - "You find an item!"
- **Falling off edges**: "You foolishly walk off the edge of the building and crash to the ground 20 feet below, injuring yourself!" - marked `!` on the maps above, and they drop you a level.
- **Encounters** are random as you walk - "ENCOUNTER!" appears when monsters attack.

### What to look for on each level

| Level | Key features |
|---|---|
| Ground Level | Castle entrance at (16, 31), stairs both ways, initial encounters (easier monsters) |
| Mid Castle Level | The main treasury, the army commander's safe, stairs both ways |
| Top Castle Level | Grelminar's staff room (the Staff at (20, 22)), the lich in his lab, secret doors |
| Dungeon Level 1 | The most chests of any level, tough monsters (Trolls, Ogres, Minotaurs) |
| Dungeon Level 2 | The lich's living quarters, hardest monsters (Demons, Death Knights, Demon Lord) |

## Hints

From the manual and playtesting:

1. **Save often** — press **Q** to save. If your whole party dies, the game quits to DOS and you restart from your last save.
2. **Explore the first level, then go up, then go down, then go up** — alternating exploration pattern.
3. **"Enter not the cross / Unless your crew is boss / It will mean life loss / To the curious"** — a riddle warning about a dangerous cross-shaped area; don't explore it until your party is strong.
4. **Return to town regularly** — heal at the temple, restore magic points, sell unwanted loot, buy better equipment.
5. **Keep your Priest's magic points in reserve** for healing during tough fights.
6. **Bless** is excellent for tough fights — 25% of attacks are warded off.
7. **Word of Recall** is your emergency escape — but it costs 7 MP, so save enough.
8. **Buy better equipment** as you accumulate gold — the equipment differences are significant.
9. **Search every wall** for secret doors — the laboratory and treasure rooms are often hidden.

## Scoring

The game tracks experience points and gold. Characters level up when they reach the "Next" experience threshold, gaining more Body Points and Spell Points.

## Sequels

Dark Designs I is the first in a trilogy:
- **Dark Designs I: Grelminar's Staff** (1990) — Retrieve the Staff
- **Dark Designs II: Closing the Gate** — Use the Staff to close the portal at Mount Delkeina
- **Dark Designs III: Retribution** — The finale
