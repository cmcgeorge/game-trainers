# DarkSpyre — Strategy Guide

## 1. Overview

DarkSpyre is a real-time dungeon-crawler RPG set in a towering spire of 50 levels. You control a single adventurer who must descend through the levels,
collect five power runes, exchange them on Level 36 for divine gifts, and then push
through the final sequence to the bottom. The game runs in real time — monsters move
and attack continuously — so quick thinking and preparedness are essential.

## 2. Controls

Taken from the control supplement in `darkspyre.txt`, the manual that ships with the
game. DarkSpyre is mouse-driven, with a keyboard shortcut for most actions.

| Key | Action |
|---|---|
| **Keypad 1-9** | Move (eight directions, plus stand still) |
| **1-6** | Trigger the numbered action on the menu bars beside your character |
| **F1-F7** | Cast a prepared spell |
| **F8** | Information: score, level location, sound status |
| **A** | Show attributes on the character sheet |
| **W** | Show weapon proficiencies |
| **S** | Show magic proficiencies |
| **T** | Take the item you are standing on |
| **Enter** | Toggle a switch you are standing on |
| **-** / **+** | Scroll the character sheet up / down |
| **P** | Pause; press again to resume |
| **Esc** | Abort the save or restore screen |

### 2.1 Mouse

- **Move**: hold the left button with the pointer at the edge of the play area, in the
  direction you want to walk. The character keeps walking while the button is held.
- **Act**: click an action on a menu bar. The bars change with what you are holding.
- **Pick up**: stand on the item and left-click your character - the cursor becomes the
  item. Click again on the character sheet or inventory to store it, or on your character
  in the play area to drop it.
- **Character sheet**: drag the grey message bar at its top to slide the sheet up and
  down. Three pictures along its bottom switch the sheet between attributes, weapon
  proficiencies and magic proficiencies; the other three are restore, pause and
  information.

### 2.2 Combat

- Attack by moving into a monster, or pick an attack from the menu bars.
- Hurled and projectile weapons attack at a distance; ranged attacks are how you deal
  with anything that hits hard in melee.
- Weapons break randomly - carry a backup, and train more than one class.

### 2.3 Pausing

Press **P** to pause. That is the window for reading a number off the screen, because the
game never stops on its own. The trainer does not need it: the Character tab finds hit
points, spell points, encumbrance and the attributes by itself.

## 3. Character Creation

### 3.1 Attributes

Your character has six attributes, each ranging from 1 to 20:

| Attribute | Affects |
|---|---|
| **Strength (STR)** | Hit points, melee damage |
| **Agility (AGI)** | Dodge chance, movement speed |
| **Endurance (END)** | Hit points, encumbrance capacity |
| **Accuracy (ACC)** | Hit probability in combat |
| **Talent (TAL)** | Spell points, magic proficiency |
| **Power (PWR)** | Spell points, spell effectiveness |

**Hit Points** = Strength + Endurance + random
**Spell Points** = Talent + Power + random (max 100)

### 3.2 Starting Choices

Choose attributes that complement your intended play style:
- **Fighter**: High STR, END, ACC. Use large or long-edge weapons.
- **Ranger**: High AGI, ACC, STR. Use hurled or short-edge weapons.
- **Mage**: High TAL, PWR, END. Invest in magic classes early.

Regardless of build, **Endurance** is critical for survival — it determines both HP
and how much you can carry.

## 4. Weapons

### 4.1 Weapon Types

| Type | Speed | Damage | When to Use |
|---|---|---|---|
| Clubbing | Average | Average | General purpose; trains with shield use |
| Hurled | Fast | Low | Ranged attacks; most common weapon type |
| Large | Slowest | Highest | Heavy damage when you have space |
| Long Edge | Average | Average | Reliable one-handed swords |
| Projectile | Fast | Average | Ranged, but requires bolts (inventory cost) |
| Short Edge | Fastest | Least | Quick attacks, low damage |
| Thrusting | Slow | High | Pole arms; some can be thrown |

### 4.2 Training Tips

- **Train in multiple types** — weapons break randomly, and you may find a great
  weapon of a type you haven't trained.
- **Hurled weapons are the most practical** — they work at range and in melee, and
  hand-to-hand with hurled items still increases hurling proficiency.
- **Attacking with a shield** increases clubbing proficiency, making it a free training
  opportunity.
- **Throwing a thrust weapon** increases hurling proficiency, not thrusting.
- **Projectile weapons are the weakest** — each bolt takes an inventory slot, limiting
  their utility.

### 4.3 Proficiency Levels

| Level | Name |
|---|---|
| 0 | None |
| 1 | Beginner |
| 2 | Neophyte |
| 3 | Novice |
| 4 | Average |
| 5 | Skilled |
| 6 | Stalwart |
| 7 | Adept |
| 8 | Savant |
| 9 | Expert |

## 5. Magic

### 5.1 Magic Classes

Six classes, each with 7 proficiency levels: None → Novice → Average → Skilled →
Sage → Maren → Master.

### 5.2 Spell List

| Spell | Class | SP | When to Use |
|---|---|---|---|
| Liquify | Healing | 10 | Create potions from gemstones — carry empty chalices |
| Knock | Sorcery | 16 | Open gates without finding keys |
| Zap Away | Sorcery | 10 | Clear puzzle blocks from paths |
| Hold | Sorcery | 30 | Stop dangerous monsters (Jesters, Beholders) |
| Fireball | Wizardry | 20 | Primary attack spell; bounces off walls for angles |
| Magic Gas | Wizardry | 20 | Area denial; poison at Skilled+ |
| Abstraka | Conjury | 20 | Invisibility to bypass monsters |
| Disguise | Conjury | 30 | Walk among monsters undetected |
| Magic Wall | Conjury | 30 | Block monster paths; solve puzzles at Sage+ |
| Compass | Diviny | 30 | Find the exit when lost |
| Magic Map | Diviny | 30 | Reveal the entire level layout |
| Sight | Diviny | 10 | Spot small items on the ground |
| Dispel | Enchantry | 36 | Remove enemy magic (unreliable) |
| Freeze | Enchantry | 40 | Stop ALL monsters — emergency button |

### 5.3 Magic Strategy

- **Fireball** is your workhorse attack spell. It deals high damage and bounces off
  walls, allowing you to hit monsters around corners.
- **Freeze** is the ultimate panic button — it stops every monster on the level. Use
  it when overwhelmed.
- **Hold** is more SP-efficient than Freeze when you only need to stop one monster.
- **Compass** and **Magic Map** are essential for navigating unfamiliar
  levels efficiently.
- **Liquify** with an empty chalice creates healing potions — always carry empty
  chalices.
- The **spell book** is found on Level 1. Find it early so you can permanently learn
  spells instead of using one-shot scrolls.
- SP cost is split 50/50 between preparation and casting — you pay half to ready the
  spell and half to cast it.

## 6. Armor

- Armor has 15 protection levels and 7 condition levels.
- Armor degrades with use — monitor condition and repair or replace as needed.
- Armor covers specific body locations; mix and match for balanced protection.
- Higher protection levels significantly reduce incoming damage.

## 7. Monsters

`CR.DAT` ships 35 creatures. The attributes below are read out of the creature records in
that file, in the same six-value order as your own character - and monsters are not held
to your cap of 20. "Tier" is the byte that rises with the depth a creature first appears
at.

### 7.1 The roster

| Creature | Attack | Tier | STR | AGI | END | ACC | TAL | PWR |
|---|---|---|---|---|---|---|---|---|
| Slime | Melee | 1 | 8 | 8 | 8 | 8 | 8 | 8 |
| Giant Bee | Melee | 3 | 7 | 7 | 7 | 7 | 3 | 0 |
| Giant Bat | Melee | 3 | 7 | 7 | 7 | 7 | 3 | 0 |
| Wraith | Melee | 1 | 14 | 12 | 20 | 8 | 10 | 0 |
| Mummy | Melee | 1 | 10 | 8 | 18 | 8 | 5 | 0 |
| Gorilla | Melee | 2 | 18 | 12 | 12 | 3 | 3 | 0 |
| Shadow Warrior | Melee | 2 | 12 | 20 | 17 | 17 | 5 | 0 |
| Hatchling | Melee | 3 | 11 | 9 | 13 | 6 | 0 | 0 |
| Scorpius | Melee | 3 | 11 | 12 | 14 | 10 | 6 | 0 |
| Lizard | Melee | 3 | 13 | 12 | 14 | 6 | 3 | 0 |
| Centipede | Melee | 3 | 12 | 12 | 14 | 3 | 0 | 0 |
| Troll | Melee | 4 | 20 | 8 | 12 | 4 | 0 | 0 |
| Gargoyle | Melee | 4 | 14 | 10 | 14 | 16 | 17 | 0 |
| Samurai | Melee | 4 | 15 | 18 | 18 | 12 | 5 | 0 |
| Hellhound | Melee | 5 | 10 | 14 | 14 | 9 | 5 | 0 |
| Cyclops | Melee | 4 | 13 | 9 | 12 | 10 | 10 | 0 |
| Gelatinous Cube | Melee | 1 | 8 | 8 | 8 | 8 | 8 | 8 |
| Saw Blade | Melee | 3 | 7 | 10 | 10 | 10 | 10 | 0 |
| Harpy | **Ranged** | 6 | 8 | 10 | 11 | 14 | 3 | 0 |
| Crystal Knight | Melee | 5 | 15 | 8 | 20 | 10 | 0 | 0 |
| Minotaur | Melee | 5 | 18 | 13 | 17 | 8 | 8 | 0 |
| Stone Golem | Melee | 6 | 20 | 7 | 18 | 10 | 0 | 0 |
| Warrior Maiden | Melee | 6 | 10 | 16 | 16 | 15 | 12 | 0 |
| Evolved Slime | Melee | 10 | 14 | 8 | 8 | 6 | 6 | 0 |
| Manta Ray | Melee | 7 | 9 | 10 | 9 | 9 | 7 | 0 |
| Jester | **Ranged** | 6 | 8 | 12 | 12 | 15 | 10 | 10 |
| Muskateer | Melee | 8 | 12 | 16 | 13 | 4 | 3 | 0 |
| Crustacean | Melee | 6 | 6 | 2 | 0 | 0 | 6 | 6 |
| Pheonix | **Ranged** | 6 | 10 | 21 | 13 | 13 | 10 | 10 |
| Djinn | **Ranged** | 6 | 8 | 8 | 10 | 12 | 16 | 0 |
| Gryphon | **Ranged** | 6 | 12 | 14 | 16 | 13 | 15 | 0 |
| Creeper | Melee | 1 | 6 | 10 | 12 | 10 | 5 | 0 |
| Electric Ball | **Ranged** | 1 | 7 | 10 | 17 | 14 | 14 | 0 |
| Beholder | **Ranged** | 1 | 11 | 14 | 13 | 14 | 15 | 0 |
| Spartan Warrior | Melee | 10 | 25 | 25 | 25 | 6 | 7 | 0 |

### 7.2 Combat tactics

**Melee creatures** - the bulk of the roster. Keep your distance and use hurled weapons,
crossbows or Fireball, and put gates, blocks and walls between you and them. The ones to
respect: troll and stone golem (20 Strength), spartan warrior (25 across the board), and
samurai and shadow warrior, which are fast as well as strong.

**Ranged creatures** (harpy, jester, pheonix, djinn, gryphon, electric ball, beholder) -
these throw fireballs, bolts or gas. Close to hand-to-hand range, which suppresses their
shooting and is where they are weakest, or pin them with **Hold** or **Freeze** and kill
them from a distance. The jester is the dangerous one: it walks *and* shoots, its
fireballs hit in the 40s, and its smoke clouds make you miss.

**Poison on contact** (slime, creeper) - immune to projectiles, so Fireball and hurled
weapons are wasted on them. They slither under gates and do not trip weight plates.
Slimes are best avoided; creepers can be meleed safely by hitting once and backing off.
Keep an Algit potion or rune for when you are caught.

**Flyers** (manta ray, harpy, gryphon, pheonix and the floating attackers) do not trip
weight plates either, so plate-based traps will not stop them.

### 7.3 General Monster Notes

- Monsters become **exponentially harder** on higher levels.
- Monsters work in **larger groups** on deeper levels.
- When near death, monsters **run away** — chase them down or let them flee.
- Killing monsters increases your score.

## 8. Runes

### 8.1 Power Runes (Required to Win)

Five power runes must be collected and brought to Level 36:

| Rune | Attribute |
|---|---|
| Uraz (Strength) | STR |
| Ehwaz (Agility) | AGI |
| Eihwaz (Accuracy) | ACC |
| Teiwaz (Endurance) | END |
| Inguz (Talent) | TAL |

On Level 36, exchange these runes for gifts from the gods. This is mandatory — you
cannot enter the final levels (38–50) without completing this exchange.

### 8.2 Utility Runes

| Rune | Effect | Priority |
|---|---|---|
| **Raido** (Quest) | Saves the game | **Essential** — stockpile these |
| **Thurisaz** (Gateway) | Go to next level | Useful for skipping difficult levels |
| **Jera** (Sustenance) | Restores HP | Emergency healing |
| **Algit** (Protection) | Cures poison | Carry when facing Slimes/Creepers/Djinns |
| **Sowelu** (Unity) | Cures poison + confusion | Superior to Algit |
| **Dagaz** (Discovery) | Destroys a monster | One-shot kill — save for tough monsters |
| **Gebo** (Alliance) | Magic Map effect | Reveal level layout |
| **Kano** (Opening) | Knock effect | Open gates without spells |
| **Fehu** (Wealth) | Becomes a knock scroll | Alternative to Kano |
| **Isa** (Stagnant) | Poisons you | **Harmful** — avoid or discard |

### 8.3 Rune Strategy

- **Raido runes are the most important item in the game.** There is no autosave, and
  each Raido rune allows exactly one save. Stockpile them whenever you find them.
- **Thurisaz** can skip a level entirely — useful when a level is
  particularly nasty.
- **Dagaz** destroys a single monster — save it for Jesters or other high-threat
  monsters you cannot handle.
- The 10 runes with unknown effects (Ansuz, Berkana, Hagalaz, Laguz, Mannaz, Nauthiz,
  Odin, Othila, Perth, Wunjo) should be experimented with cautiously.

## 9. Level Progression

### 9.1 Early Game (Levels 1–10)

- **Level 1**: Find the spell book. This is your top priority — without it, every
  spell is a one-shot scroll.
- Learn **Fireball** as soon as possible — it is your primary attack spell.
- Train in **hurled weapons** — they work in melee and at range.
- Collect **Raido runes** and save frequently.
- Explore thoroughly — early levels have the best ratio of risk to reward.

### 9.2 Mid Game (Levels 10–35)

- Collect the five **power runes** as you find them. Do not proceed past Level 36
  without all five.
- Train multiple weapon types as backups.
- Invest in **Compass** and **Magic Map** spells to navigate efficiently.
- Stockpile potions, Raido runes, and healing items.
- Monster difficulty ramps up significantly — use terrain and spells creatively.

### 9.3 The Exchange (Level 36)

- Bring all five power runes here.
- Exchange them for gifts from the gods.
- This unlocks the final sequence.

### 9.4 Late Game (Levels 37–50)

- Only 39 of 50 levels are required, but the final sequence (Levels 38–50) is
  mandatory after the exchange.
- Monsters are at their hardest — use **Freeze** liberally.
- Save after every level using Raido runes.
- **Thurisaz** runes can skip non-required levels if you are struggling.

## 10. How to Win

1. Descend from Level 1, finding the spell book on Level 1.
2. Collect all five power runes (Uraz, Ehwaz, Eihwaz, Teiwaz, Inguz) across the middle
   levels.
3. Reach Level 36 and exchange the power runes for divine gifts.
4. Descend through the final levels (38–50).
5. Reach the bottom of the DarkSpyre.

## 11. General Tips

- **Pause with P** whenever you need to think, read values, or plan a route.
- **Save with Raido runes** after every cleared level — there is no autosave.
- **Carry backup weapons** — weapons break randomly, and being unarmed is a death
  sentence.
- **Train multiple weapon types** — you never know what you will find.
- **Use terrain** — gates, blocks, and walls can block monster paths and create safe
  spaces.
- **Conserve SP** — do not waste spells on weak monsters you can melee safely.
- **Watch encumbrance** — carrying too much slows you down and limits what you can
  pick up.
- **Learn monster categories** — the tactics that work on a Wraith will get you killed
  against a Slime or a Jester.
- **Do not skip the power runes** — you cannot win without them.
- **Use the trainer's guided scans** to pin HP, SP, and attributes, then freeze them
  for a safer descent.

## 12. Using the Trainer

### 12.1 Quick start

1. Launch DarkSpyre in DOSBox and get a character into play - the menus have no
   character to find.
2. Run the trainer (`.\Run.ps1`). It picks the emulator process and attaches on its own;
   if it does not, click **Refresh**, choose the process and click **Attach**.
3. The **Character** tab fills in by itself. Hit points, spell points, encumbrance and
   the six attributes are found by searching the emulator for the character - you never
   type an address or hunt for a value.

### 12.2 What to do from there

- **Survive anything**: click **Refill HP & SP**, then tick **Freeze** beside hit points.
  The value is re-written every 200 ms, so nothing can push it down.
- **Cast without counting**: tick **Freeze** beside spell points.
- **Raise the ceiling**: type a new **Maximum** for hit or spell points. The engine
  adopts it and you regenerate up to the new figure - the manual says spell points cap at
  100, but the game itself does not enforce that.
- **Max attributes**: raises all six to 20, the cap the manual states. The character
  sheet keeps showing the old numbers until the game repaints that panel - press **A** in
  game to force it.
- **Carry more**: raise **Maximum encumbrance**. Current weight is read-only; the game
  recomputes it from what you are holding.

### 12.3 The value scanner

The Character tab does not cover score, level number or inventory. For those, use the
**Value Scanner** tab: pick a guided recipe, follow its steps to narrow the candidates,
pin the survivor, then edit or freeze it on the **Freezes** tab.

Press **P** in game before reading a number for a scan - DarkSpyre runs in real time and
the value can move between reading it and typing it. Unpause once the value is pinned.

## 13. Levels and Maps

The game ships 42 tile grids (`MAP00.DAT`, `MAP0B`-`MAP4H`, `MAPR0`-`MAPR8`) built from
the same tile alphabet the live level buffer uses, so levels are not conjured out of
nothing. But no shipped file matches a live level closely enough to say "level *n* is
file *x*": they look more like room or section templates the generator stitches together,
and item, monster and floor-tile placement is certainly randomised per game. See
`docs/ReverseEngineering.md` section 7.3 for the evidence. Treat every level as new.

What is fixed is the structure:

- Each level has an **entrance** where you arrive and a **gateway** where you descend.
  You cannot go back up.
- Rooms are joined by corridors with doors and gates that may need a key or **Knock**.
- **Weight plates** open doors and spring traps. Walking creatures trip them; flying and
  slithering ones do not.
- **Blocks and balls** can be pushed onto plates to hold them down - **Zap Away**
  relocates one that is stuck somewhere useless.
- **Items** lie on the ground; **Sight** enlarges them so you can spot them.
- **Scrolls** teach a spell once, or permanently once you have the spell book from
  Level 1.
- Expect a **Raido** rune on most levels - that is your only save.

**Compass** points at the exit and **Magic Map** reveals the layout; between them they
are the map the game gives you.
