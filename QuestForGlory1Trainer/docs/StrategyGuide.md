# Quest for Glory I: So You Want to Be a Hero — Strategy Guide

> Covers the **VGA remake** (1992, Sierra On-Line) which is mechanically identical to the
> original 1989 EGA *Hero's Quest*. All room names, quest steps, and timings apply to both
> versions unless noted.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Controls](#2-controls)
3. [Character Classes](#3-character-classes)
4. [Core Game Mechanics](#4-core-game-mechanics)
5. [Spielburg — Town Guide](#5-spielburg--town-guide)
6. [Valley & Forest Guide](#6-valley--forest-guide)
7. [Main Quest Walkthrough](#7-main-quest-walkthrough)
8. [Class-Specific Quests](#8-class-specific-quests)
9. [Combat Guide](#9-combat-guide)
10. [Magic Spells (Magic-User)](#10-magic-spells-magic-user)
11. [Time & Day Cycle](#11-time--day-cycle)
12. [How to Win](#12-how-to-win)
13. [Maps](#13-maps)
14. [Tips & Secrets](#14-tips--secrets)

---

## 1. Overview

Quest for Glory I is a hybrid adventure/RPG set in the valley of Spielburg. Your hero must:

1. Lift the curse on the valley cast by the witch **Baba Yaga**.
2. Free the Baronet **Barnard von Speilburg** from the brigands.
3. Rescue the Baroness **Elsa von Speilburg** (the brigand leader in disguise).
4. Earn enough points to graduate from hero to **Champion**.

The game is entirely non-violent against humans. Monsters in the forest may attack you;
defeating them yields practice and experience. There is **no fail-state** — you cannot die
permanently, but running out of stamina or health causes you to wake up with Erasmus, costing
you a day.

---

## 2. Controls

### Mouse Controls (VGA version / point-and-click)

| Action | Input |
|---|---|
| Walk | Left-click destination |
| Interact / Look | Right-click object → choose from verb menu |
| Pick up | Right-click → "Take" |
| Talk | Right-click character → "Talk" |
| Fight (combat screen) | Click attack button |

### Keyboard Controls

| Key | Action |
|---|---|
| Arrow keys | Walk (EGA version) / navigate menus |
| **Enter** | Confirm / use |
| **Esc** | Cancel / close screen |
| **Tab** | Cycle through verbs (EGA parser mode) |
| **F1** | Help |
| **F2** | Sound on/off |
| **F3** | Load game |
| **F4** | Save game (up to 12 saves) |
| **F5** | Pause |
| **F6** | Quit |
| **+** / **-** | Increase / decrease game speed |
| **Alt-Enter** | Toggle fullscreen (DOSBox) |
| **Ctrl-F9** | Kill DOSBox process |

### Combat Controls

| Input | Action |
|---|---|
| Click / drag attack button | Swing weapon |
| Parry button | Raise defence |
| Dodge button | Leap aside |
| Flee button | Attempt to run (may fail) |

### Debug Cheat Mode

Type the phrase **`razzle dazzle root beer`** anywhere during play (letters are invisible):

| Hotkey | Effect |
|---|---|
| **Alt-T** | Teleport — enter a room number |
| **Alt-K** | Set stats to a chosen value |
| **Alt-B** | Add money to inventory |

---

## 3. Character Classes

Choose your class at the character-creation screen. Stats are allocated differently but you
can improve any stat through practice.

### Fighter
- Starts with high Strength, Vitality, Weapon Use, Parry.
- Combat is straightforward; can enter the brigand fortress by force.
- Unique quest: prove yourself worthy by defeating the Baron's champion.

### Magic-User (Wizard)
- Starts with high Intelligence and Magic; low physical stats.
- Can cast spells (Fetch, Trigger, Open, Detect Magic, Dazzle, Force Bolt, Calm, Flame Dart).
- Must buy or find spell scrolls; Zara's magic shop sells most.
- Unique quest: prove yourself to Erasmus by winning his magic game.

### Thief
- Starts with high Agility, Stealth, Pick Locks, Climbing, Throwing.
- Can steal, sneak past monsters, and pick pockets.
- Can climb walls and enter locked buildings at night.
- Unique quest: join the Thieves' Guild; complete a theft test.

All classes can ultimately complete the main quest, though the methods differ.

---

## 4. Core Game Mechanics

### Stats

| Stat | Description |
|---|---|
| Strength | Melee damage; max carry weight |
| Intelligence | Spell learning; puzzle hints |
| Agility | Hit chance; dodge; pick locks |
| Vitality | Max Health and Stamina |
| Luck | Random event outcomes; treasure quality |
| Weapon Use | Accuracy in melee |
| Parry | Chance to block incoming attacks |
| Dodge | Chance to avoid missile/area attacks |
| Stealth | Moving silently; sneaking past creatures |
| Pick Locks | Opening locked doors and chests |
| Throwing | Accuracy and range with thrown objects |
| Climbing | Scaling walls, trees, and cliffs |
| Magic | Mana pool; spell casting accuracy |

Stats improve automatically through use. Practice-fighting raises Weapon Use; sneaking around
raises Stealth, etc.

### Health, Stamina, Mana

- **Health** drops when you are wounded. Restore with Healing Potion (Healer's hut), resting, or
  Erana's Peace.
- **Stamina** drops during combat and exhausting actions. Restore with Vigor Potion, Rations, or rest.
- **Mana** depletes when casting spells. Restore slowly over time, or at Erana's Peace.
- Sleeping at the inn restores all three to maximum.

### Weight & Inventory

Carry weight is tracked. Strength governs your maximum. Exceed it and you slow to a crawl.

### Time

See §11 for the full day/night cycle. Many events and NPCs are time-gated (the Healer sleeps at
night; the inn serves breakfast only in the morning, etc.).

---

## 5. Spielburg — Town Guide

The walled town of Spielburg is in the south of the valley. Enter from the south road.

### Town Services

| Location | Hours | Services |
|---|---|---|
| **Dry Grape Inn** | 24 h (common room); mornings for breakfast | Sleep (5 silver), meals, news |
| **Healer's Hut** (Hazel) | Daytime only | Healing Potion, Vigor Potion, Dispel Potion; also buys mushrooms |
| **Weapon Shop** (Issur) | Daytime | Sword, Dagger, Leather Armor, Shield; repairs |
| **Meeps' Curiosity Shoppe** | Daytime | Rope, Beads, Canteen, Oil Lamp, Lock Picks |
| **Magic Shop** (Zara) | Daytime | Spell scrolls, Magic Acorn, Mana-restoring items |
| **Adventurers' Guild** | Anytime | Read the notice board; register kills |
| **Sheriff's Office** | Daytime | Report crimes; get information |

### Key NPCs

| NPC | Location | Importance |
|---|---|---|
| Shameen (innkeeper) | Dry Grape Inn | Safe haven; story background |
| Shema (cook) | Dry Grape Inn kitchen | Gives a favor if you help her |
| Hazel | Healer's hut | Potions; gives info about Baba Yaga |
| Issur | Weapon shop | Equipment |
| Meeps | Curiosity Shoppe | Adventuring supplies |
| Zara | Magic shop | Spells |
| Enry Ogre | Gate | Blocks exit at night; bribe with a 10-silver piece |

---

## 6. Valley & Forest Guide

### Valley Exits from Town

| Direction | Destination |
|---|---|
| South road | Valley entrance (game start) |
| North road | Deep forest; Erana's Peace; brigand area |
| East path | Healer's cottage (outside town) |
| West path | Erasmus's mountain (wizard's house) |
| North-east | Castle Spielburg |

### Forest Encounters

| Creature | Day/Night | Combat | Notes |
|---|---|---|---|
| **Goblin** | Day | Easy | Common; good for practice |
| **Antwerp** (bouncing ball) | Day | Avoid or lure | Stealing its ball triggers the brigand leader quest |
| **Cheetaur** | Day | Hard | Outrun or Dazzle |
| **Troll** | Near bridge | Medium | Must defeat to cross the bridge safely |
| **Ogre** | Day | Very hard | Avoid until high stats |
| **Kobold** | Day/Night | Medium | Guards cave; carries loot |
| **Tentacles** (in mushroom area) | Night | Avoid | Block Erana's Peace path |

### Important Forest Locations

| Location | How to reach | Contents |
|---|---|---|
| **Erana's Peace** | Follow north road, north-east fork | Restore all HP/Stamina/Mana overnight; magic acorn (respawns) |
| **Erasmus's House** | West path, up the mountain | Wizard's class quest; Fenrus puzzle |
| **Healer's Hut (exterior)** | East of town, south of castle | Garden with healing plants |
| **Bear cave** | North-west forest | Cursed Elsa in bear form |
| **Brigand Fortress** | North-east, past the troll bridge | Main quest dungeon |
| **Baba Yaga's Hut** | Far north, past Flying Falls | Must visit twice for main quest |

---

## 7. Main Quest Walkthrough

### Step 1 — Arrive and Orient (Day 1)

1. Arrive in the valley south of Spielburg.
2. Enter town and sleep at the Dry Grape Inn (10 silver per night; you start with 10 silver).
3. Read the **Adventurers' Guild notice board** — lists monsters, bounties, and the valley's plight.
4. Visit the **Sheriff** for background; visit **Hazel** to buy a Healing Potion.

### Step 2 — Find Baba Yaga (Day 2–3)

1. Go north through the forest, past the Flying Falls.
2. Find **Baba Yaga's Hut** (it moves; follow the chicken legs).
3. Speak to Baba Yaga. She gives you a task: **bring her a Mandrake Root**.

### Step 3 — Gather the Mandrake Root (Day 3–5)

- Buy a **Dispel Potion** from Hazel (needed for the root).
- Ask Hazel where to find a Mandrake Root — she points to a specific forest clearing (only at night).
- At night, visit the Mandrake clearing and use the Dispel Potion; pick up the Mandrake Root.

### Step 4 — Return to Baba Yaga (Day 5–6)

1. Bring the Mandrake Root to Baba Yaga.
2. She lifts part of the curse and gives you a **Magic Mirror**.
3. The mirror can reveal the true identity of the brigand chief.

### Step 5 — Rescue the Baronet from the Brigand Fortress (Day 6–10)

1. Cross the **Troll's Bridge** north of town (defeat or bribe the troll).
2. Enter the Brigand Fortress. Each class has a different entry method:
   - **Fighter**: fight through the front gate.
   - **Thief**: sneak in at night through a back entrance.
   - **Magic-user**: use spells to neutralise guards.
3. Navigate the fortress, free **Barnard** from the dungeon.
4. Fight (or avoid) the Brigand Leader.

### Step 6 — Reveal Elsa (Day 10–12)

1. Use the **Magic Mirror** on the brigand leader — reveals she is the Baroness Elsa.
2. Use the **Magic Acorn** from Erana's Peace on Elsa to break the curse (or let Baba Yaga handle it).
3. Return to the castle for the finale.

### Step 7 — Finale

1. Bring Barnard and Elsa to Castle Spielburg.
2. The curse is lifted; the valley celebrates.
3. The game tallies your **Hero Points** (max 500). Reaching a high score earns you the title
   of **Hero** or **Champion** and unlocks the import into Quest for Glory II.

---

## 8. Class-Specific Quests

### Fighter Quest

- Challenge the **Saurus Rex** in the guild arena to demonstrate worth.
- Escort caravan survivors; help the blacksmith with a special commission.
- Reward: a magical sword and a significant boost to Weapon Use.

### Magic-User Quest

- Visit **Erasmus** on the mountain; beat him at **Mage's Maze** (memory puzzle).
- Collect all spell scrolls; practice each spell until it costs only 1 mana.
- Find the **Staircase to the Stars** (hidden room accessible only with Open spell + climbing).

### Thief Quest

- Speak to the **Thieves' Guild** contact (leave Spielburg at night; a figure approaches).
- Complete the initiation theft: steal a **Fairy Ruby** from the Magic Shop during the day.
- Return it to the guild; receive lock-pick tools and the guild password.
- Extra: pick pockets of Enry Ogre at the gate for a silver coin surprise.

---

## 9. Combat Guide

Combat is a real-time window with three buttons:

| Button | Effect |
|---|---|
| **Attack** (drag towards enemy) | Deal damage; depletes Stamina |
| **Parry** | Reduce incoming damage; improves the Parry stat |
| **Dodge** | Avoid attacks; improves Dodge stat |
| **Flee** | Attempt to escape; may fail vs. fast enemies |

### Tips

- Practise on Goblins early to raise Weapon Use and Parry cheaply.
- Always have at least one Healing Potion and one Vigor Potion before venturing far.
- The Troll regenerates health rapidly; deal burst damage and don't let it recover.
- Magic-users should cast **Dazzle** to blind opponents, then finish with **Flame Dart**.
- Thieves can **Throw** daggers from outside melee range to soften enemies.

---

## 10. Magic Spells (Magic-User)

| Spell | Scroll sold by | Effect |
|---|---|---|
| **Fetch** | Zara | Move small objects to you |
| **Open** | Zara | Unlock doors (no Lock Pick needed) |
| **Detect Magic** | Zara | Reveal magical objects (useful for finding hidden items) |
| **Trigger** | Zara | Activate objects or traps at range |
| **Dazzle** | Zara | Blind an enemy for several seconds |
| **Force Bolt** | Zara | Deal damage; knockback |
| **Calm** | Hazel (!) | De-aggro a hostile creature |
| **Flame Dart** | Zara | Fire bolt; most mana-efficient damage |
| **Levitate** | Erasmus (reward) | Float over obstacles; access hidden area |
| **Lightning Ball** | Hidden cave | Area damage; hard to control |

Spells improve with use. Cast Fetch/Open repeatedly to lower their mana cost.

---

## 11. Time & Day Cycle

The game world runs on a **3600-tick day** (150 ticks per in-game hour):

| Time of day | In-game | Events |
|---|---|---|
| Dawn | ~5 AM | Shops begin opening; birds sing |
| Mid-morning | 7–11 AM | All shops open; NPCs active |
| Midday | 11 AM – 2 PM | Monsters most active; best trading hours |
| Mid-afternoon | 2–6 PM | Stamina-draining heat; less foot traffic |
| Sunset | 6–9 PM | Some shops close; forest grows dangerous |
| Night | 9 PM – midnight | Thieves' Guild activity; Mandrake Root location accessible |
| Midnight | Midnight – dawn | Very dark; brigand patrols lighten |

### Day Counter

- The game begins on **Day 1**.
- There is no in-game calendar UI; you must track days by Zara's gossip or the innkeeper.
- At around **Day 14–16**, the game becomes **unwinnable** if key quests are not started
  (depending on version). Finish the main quest well before then.

---

## 12. How to Win

### Victory Conditions

1. **Lift Baba Yaga's curse** (Mandrake Root delivery).
2. **Rescue Baron Barnard** from the brigand dungeon.
3. **Reveal and free Baroness Elsa** (Magic Mirror + Magic Acorn).
4. **Return to Castle Spielburg** for the finale.

### Hero Points (max 500)

Points are awarded for:
- Combat victories (especially first time for each creature type).
- Quest completions.
- Learning spells (Magic-user).
- Guild registrations (Adventurers' Guild, Thieves' Guild).
- Being kind to NPCs (giving food to the Dryad, helping Shameen's family, etc.).
- Collecting rare items.

A score of 500/500 unlocks the Champion title and a special save for import to QFG2.

### Exporting to Quest for Glory II

After the end credits, select **Export Character**. The export file contains your stats, gold,
inventory (some items carry over), and class. Load it in QFG2 at character-creation.

---

## 13. Maps

### 13.1 Spielburg Valley — Overworld

```
     [Erasmus's Mtn]          [Erana's Peace]
           |                        |
   [W Forest]---[N Forest]---[NE Forest]---[Brigand Fortress]
       |              |                          |
   [Valley W]  [Flying Falls]           [Bear Cave]
       |              |
  [Valley Ctr]--[Town Gate]--[Castle Spielburg]
       |              |              |
   [Valley S]   [Town Square]  [Kobold Cave]
                     |
               [Valley Entrance]
                  (start)
```

### 13.2 Spielburg Town

```
     [North Street]
          |
[Weapon Shop]-[Town Square]-[Magic Shop]
     |              |             |
[Inn / Dry Grape]-[Alley]-[Meep's Shop]
          |
  [Town Gate / Guild]
          |
    [South Road]
```

### 13.3 Brigand Fortress

```
  [Entry Gate]---[Courtyard]---[Barracks]
       |               |
  [Guard Tower]  [Dungeon (Barnard)]
                        |
                 [Leader's Chamber]
```

---

## 14. Tips & Secrets

- **Save often** — at least before every new area or combat.
- **Practice stats in town first** — throw rocks at the well, pick the inn's lock at night,
  climb the north wall. You won't get in trouble.
- **The Antwerp** (bouncy ball creature) is harmless but gives significant XP if you lure it
  into a building. Use the rope from Meeps' shop.
- **Erana's Peace** fully restores all three resource bars overnight with no cost — better than
  the inn for survivability.
- **Throwing daggers** at goblins from the inventory screen raises Throwing without entering
  a combat window.
- **Oil the Inn's hinge** to sneak out at night without the creak waking the innkeeper (Thief).
- **Magic Acorn regrows** at Erana's Peace each day — never worry about losing your only one.
- **The Healer pays well for mushrooms** found in the forest. Early gold source.
- **The Thieves' Guild vendor** near the town fountain appears only at night and only if you
  wear dark clothing (leather armor counts).
- **Dispel Potion can reveal the Mandrake Root** — without it the plant is invisible.
- If your character dies, they wake up in **Erasmus's house** — Fenrus (his mouse) lectures
  you, but no permanent penalty beyond losing a day.
