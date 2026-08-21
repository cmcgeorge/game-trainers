# DarkSpyre — Strategy Guide

## 1. Overview

DarkSpyre is a real-time dungeon-crawler RPG set in a towering spire of 50 randomly
generated levels. You control a single adventurer who must descend through the levels,
collect five power runes, exchange them on Level 36 for divine gifts, and then push
through the final sequence to the bottom. The game runs in real time — monsters move
and attack continuously — so quick thinking and preparedness are essential.

## 2. Controls

| Key | Action |
|---|---|
| **Arrow keys** | Move in four directions |
| **Space** | Attack with equipped weapon |
| **A** | Display attributes |
| **P** | Pause the game (safe for reading values) |
| **F8** | Display current level and score |
| **Number keys** | Select spells / items (context-dependent) |
| **Enter** | Confirm / activate |

### 2.1 Movement

- The character moves one tile per arrow key press.
- Movement is real-time — monsters continue to move while you act.
- Flying monsters and slithering monsters do not trigger weight plates.
- Walking into a monster initiates melee combat.

### 2.2 Combat

- Press **Space** to attack with the equipped weapon in the facing direction.
- Ranged weapons (hurled, projectile) attack at a distance.
- Spells are cast by selecting them and targeting.
- Weapons break randomly — always carry a backup.

### 2.3 Pausing

Press **P** to pause the game. This is essential for:
- Reading attribute values for the trainer's guided scans.
- Taking a breath during intense combat.
- Planning your next move in puzzle rooms.

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
- **Compass** and **Magic Map** are essential for navigating randomly generated
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

### 7.1 Monster Categories

| Category | Monsters | Movement | Attack | Trigger Plates? |
|---|---|---|---|---|
| Ground Melee | Wraith, Crustacean, Samurai, Gargoyle, Crystal Ninja | Walk | Hand-to-hand | Yes |
| Ground Projectile | Jester | Walk | Fireballs + gas | Yes |
| Slither Poison | Slime, Creeper | Slither | Poison on contact | No |
| Flying Melee | Vulture, Manta Ray | Fly | Hand-to-hand | No |
| Flying Projectile | Beholder, Electric Storm, Banshee, Djinn | Fly | Ranged | No |

### 7.2 Combat Tactics by Category

**Ground Melee** (Wraith, Crustacean, Samurai, Gargoyle, Crystal Ninja):
- Keep your distance and use hurled weapons, crossbows, or Fireball.
- These are the most common monsters and the easiest to manage with ranged attacks.
- Use terrain (gates, blocks, walls) to create barriers.

**Ground Projectile** (Jester):
- The most dangerous non-flying monster. It walks AND shoots fireballs (damage in the
  40s) and gas clouds (causes miss/confusion/poison).
- Cast **Hold** or **Freeze** to immobilize it, then kill it quickly.
- Never let a Jester get into a sustained firefight with you.

**Slither Poison** (Slime, Creeper):
- Immune to all projectile attacks — Fireball and hurled weapons will not work.
- Slimes are best avoided entirely.
- Creepers can be meleed safely: attack once, run away, repeat.
- Do not lure them onto weight plates — they will not trigger them.

**Flying Melee** (Vulture, Manta Ray):
- Use hurled weapons and Fireball at range.
- They do not trigger weight plates, so plate-based traps will not stop them.

**Flying Projectile** (Beholder, Electric Storm, Banshee, Djinn):
- Get into hand-to-hand range immediately — this suppresses their projectile attacks.
- Alternatively, cast **Hold** or **Freeze** to pin them, then kill at range.
- Their melee attack is weak, so closing distance is safer than fighting at range.

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
| **Keno** (Opening) | Knock effect | Open gates without spells |
| **Fehu** (Wealth) | Becomes a knock scroll | Alternative to Keno |
| **Isa** (Stagnant) | Poisons you | **Harmful** — avoid or discard |

### 8.3 Rune Strategy

- **Raido runes are the most important item in the game.** There is no autosave, and
  each Raido rune allows exactly one save. Stockpile them whenever you find them.
- **Thurisaz** can skip a level entirely — useful when a randomly generated level is
  particularly nasty.
- **Dagaz** destroys a single monster — save it for Jesters or other high-threat
  monsters you cannot handle.
- The 10 runes with unknown effects (Ansuz, Berkana, Hagalaz, Laquz, Mannaz, Nauthiz,
  Odin, Othilia, Perth, Wunjo) should be experimented with cautiously.

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

### 12.1 Quick Start

1. Launch DarkSpyre in DOSBox.
2. Run the trainer (`.\Run.ps1`).
3. Click **Refresh**, select the DOSBox process, and click **Attach**.
4. Pick a **Guided Scan** recipe from the dropdown.
5. Follow the on-screen instructions to scan and narrow the value.
6. Pin the result and switch to the **Freezes** tab.
7. Edit the **Target** column and tick **Freeze** to hold the value.

### 12.2 Recommended Scan Order

1. **Hit Points** — pin and freeze for invulnerability.
2. **Spell Points** — pin and freeze for unlimited casting.
3. **Attributes** — pin and set to 20 for max stats.
4. **Encumbrance** — pin and set low for unlimited carrying.
5. **Level** — pin to skip ahead (use with caution).
6. **Score** — pin and set high for bragging rights.

### 12.3 Pausing for Scans

Press **P** in-game to pause before reading a value for the scanner. This ensures the
value does not change between reading it on-screen and typing it into the trainer.
After the scan is narrowed and the value is pinned, you can unpause and freeze it.

## 13. Maps

DarkSpyre's dungeons are **randomly generated** — no fixed maps exist. Each playthrough
generates a new layout for every level. However, the following structural rules apply:

- Each level has an **entrance** (where you arrive from the level above) and an
  **exit/gateway** (where you descend to the next level).
- Levels contain **rooms** connected by **corridors**, with doors/gates that may
  require keys or the Knock spell.
- **Weight plates** trigger doors, traps, or puzzles — stepped on by walking creatures
  but not by flying or slithering ones.
- **Blocks and balls** can be pushed onto plates to hold them down — Zap Away can
  relocate them.
- **Items** are scattered on the ground — use the Sight spell to enlarge them for
  easier spotting.
- **Scrolls** are found on the ground or in chests — pick them up to learn spells
  temporarily or permanently (with the spell book).
- **Runes** are found in special locations throughout the levels.

Use the **Compass** spell to find the direction of the exit, and **Magic Map** to
reveal the entire level layout. These are the closest things to a map the game
provides.
