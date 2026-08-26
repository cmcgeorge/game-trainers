# Knights of Legend (1989) Strategy Guide

## Scope and spoiler policy

This guide covers the original Origin Systems DOS version of **Knights of Legend**, by Todd Porter.

It is written for a first complete playthrough, but it contains story and solution spoilers.

The official manual and the in-game help system are the primary references for controls and game systems.

Names, keywords, quest-giver locations, and enemy encounter details can vary between installations or manual editions.

When a clue conflicts with this guide, trust the text the game gives you and the keywords its NPCs provide.

## Overview and objective

Knights of Legend is a turn-based fantasy role-playing game set in the duchy of **Ashtalarea**, part of the kingdom of Sondar.

The evil sorcerer **Pildar** has captured Duke Fuquan and the knight Seggallion.

The player assembles a party of adventurers and must complete **24 quests** in sequence to rescue Seggallion.

The early quests are local errands in and around the starting town of Brettle.

Those introductory tasks expand into travel across the full duchy, requiring visits to distant towns, forests, swamps, mountains, and dungeons.

The practical definition of victory is to complete all 24 quests, collect the key items each one yields, and confront the forces holding Seggallion.

Do not mistake an apparently completed early quest for the end of the campaign.

The game regularly gates progression behind the next quest-giver's keyword, and a quest accepted out of order may not be completable until the party has the right equipment, skills, or key items.

Keep following the quest chain, talk to every NPC with a high-charisma character, and record every keyword.

## The party

A party may hold up to six active members.

Up to sixteen characters can be saved per disk, allowing a pool to draw from.

The party travels, fights, and rests as a group; individual members can be swapped at town save points.

A diverse party is essential because certain NPCs will not speak to certain races, and certain quests require specific capabilities.

### Why diversity matters

Some quest-givers react differently to different races. A party of all one race may find doors closed that a mixed party opens freely.

At least two characters with **Charisma 80 or higher** are recommended to ensure quests are offered.

At least one **Kelden** is recommended because they are the strongest fighters and can fly, opening tactical options no other race has.

At least two **spell casters** are recommended for tough battles, ideally from different magic orders before joining.

### Racial access

Only male Dwarves and male Kelden are available at character creation. Humans and Elves can be either sex.

## Before leaving Brettle

1. Create a balanced party of four to six characters with diverse races and classes.
2. Reroll stats until each character has strong primary attributes for their role.
3. Buy starting weapons and armor for every character from Ludeman Armorers and the weapon shops.
4. Visit the wizard's tower to purchase basic spells before joining a magic order.
5. Buy horses for every character at the stable; party speed equals the slowest character.
6. Stock up on food at the Lonely Page Pub.
7. Rest and save at the Trollsbane Inn (60 GC per night, safe) before heading into the wilderness.
8. Create at least one dummy Brettle Regular character to generate starting gold if funds are low.

Do not leave Brettle with a party that has no armor, no trained weapon skill, or no food.

The wilderness around Brettle contains goblins, ruffians, and bandits that will punish an unprepared party.

## Full controls reference

### Keyboard controls

| Input | Function |
|---|---|
| Up Arrow | Move north / up |
| Down Arrow | Move south / down |
| Left Arrow | Move west / left |
| Right Arrow | Move east / right |
| `<` | Cycle the icon selection backward |
| `>` | Cycle the icon selection forward |
| `ENTER` | Select the highlighted icon or action |
| `ESC` | Go back / activate the U-Turn icon |
| `Ctrl-Q` | Quit to DOS |
| Number keys | Select menu entries on the table of contents screen |

The keyboard is the faster and more reliable input method for this game.

Arrow keys handle movement, `<` and `>` cycle through the available icons on the current screen, and `ENTER` confirms the selection.

`ESC` serves as the universal back button and is equivalent to selecting the U-Turn icon.

### Mouse controls

| Input | Function |
|---|---|
| Click | Highlight an icon, option, or character |
| Click again | Select the highlighted item |
| Click and hold | Drag or move (context-dependent) |

The mouse interface requires **two clicks** to activate any item: the first click highlights it, the second click selects it.

This double-click system can be difficult to use, especially under DOSBox emulation where mouse sensitivity and timing may not match the original hardware.

If the mouse feels unresponsive or unreliable, switch to the keyboard entirely.

### DOSBox settings for Knights of Legend

The game has **no frame limiter**, so CPU speed directly affects game speed and responsiveness.

Recommended DOSBox configuration:

```ini
[cpu]
cycles=fixed 3000

[mouse]
sensitivity=1.0
```

`cycles=fixed 3000` provides a consistent game speed that matches the original-era hardware expectation.

Higher cycle counts make the game run too fast; lower counts make it sluggish.

`sensitivity=1.0` keeps mouse movement proportional. Lower the value if the cursor overshoots, or raise it slightly if the cursor feels too slow.

If mouse control remains problematic after adjusting sensitivity, use the keyboard exclusively. The game is fully playable without the mouse.

## Character creation

### Races

| Race | Characteristics | Build implication |
|---|---|---|
| **Human** | Versatile, no restrictions, accepted by most NPCs | Flexible for any class and role; the safe default |
| **Elf** | Archers and nature-oriented; access to elven areas | Good for ranged combat and magic; some NPCs favor Elves |
| **Dwarf** | Sturdy, heavy armor proficiency, durable | Strong frontline fighter; only males available |
| **Kelden** | Winged, can fly in combat, strongest fighters | Excellent mobility and combat; only males available; some NPCs hostile to Kelden |

### Classes

There are **33 classes** total, each with unique stat requirements and starting equipment.

Classes range from simple fighters to specialized spell casters and hybrid roles.

The class determines starting stats, equipment, and which skills can be trained.

Experiment with different class combinations to find a party composition that covers combat, magic, and social interaction.

### Primary stats

| Stat | Range | Role |
|---|---|---|
| **Strength** | 0–100 | Melee damage, carrying capacity, physical actions |
| **Quickness** | 0–100 | Action speed in combat, initiative, dodge effectiveness |
| **Size** | 0–100 | Hit points, reach, intimidation; affects which armor fits |
| **Health** | 0–100 | Hit points, fatigue recovery, disease resistance |
| **Foresight** | 0–100 | Reveals enemy actions before they execute in combat |
| **Charisma** | 0–100 | NPC reactions, quest availability, trading |
| **Intellect** | 0–100 | Magic capability, spell learning, training eligibility |

### Rerolling stats

You can reroll stats as many times as desired during character creation.

Take advantage of this: reroll until each character has strong values in their primary stats.

A fighter should have high Strength, Size, and Health. A spell caster should have high Intellect and Foresight. A scout or archer should have high Quickness. A spokesperson should have high Charisma.

Do not accept a mediocre roll when the game allows unlimited retries.

### Recommended starting party

| Role | Race | Priorities | Notes |
|---|---|---|---|
| Frontline fighter | Kelden | High Strength, Size, Health, Quickness | Can fly in combat; strongest melee option |
| Frontline fighter | Dwarf | High Strength, Size, Health | Heavy armor; durable damage dealer |
| Spell caster | Human or Elf | High Intellect, Foresight | Learn basic spells from all orders before joining one |
| Spell caster | Human or Elf | High Intellect, Foresight | Second caster for tough battles |
| Archer / scout | Elf | High Quickness, Foresight | Ranged support; some NPC access Elves prefer |
| Spokesperson / utility | Human | High Charisma (80+), decent all stats | Quest access; flexible backup role |

At least two characters with Charisma 80+ ensures quest-givers will offer quests.

At least one Kelden gives the party a flyer and its strongest fighter.

Two spell casters cover different magic orders and provide backup if one falls in combat.

## Town life

### Brettle: the starting town

Brettle is the eastern starting town and the hub for the first several quests.

| Location | Function | Notes |
|---|---|---|
| **Trollsbane Inn** | Rest and save | 60 GC per night; safe from theft |
| **Broken Keg Inn** | Rest for free | No cost, but thieves may steal items overnight |
| **Ludeman Armorers** | Buy armor | Head armor, torso armor, leg armor, shields |
| **Weapon shops** | Buy weapons | Various melee and ranged weapons |
| **Bow shop** | Buy bows | Self Bow, Elf Bow, Long Bow for archers |
| **Lonely Page Pub** | Buy food | Essential for wilderness travel |
| **Wizard's tower** | Learn magic | Astimiah Eckhart trains spells; buy basics before joining an order |
| **Training barracks** | Train weapons | Hvrad Myth teaches Longsword, Broadsword, Short Spear, Battle Axe (skill 0–30) |
| **Stable** | Buy horses | Improves travel speed; party speed equals slowest character |
| **Temple / Abbey** | Healing | Restore health and cure conditions |

### Choosing an inn

The **Trollsbane Inn** charges 60 GC per character per night but guarantees safety.

The **Broken Keg Inn** is free, but thieves may steal items from the party overnight.

The gold saved at the Broken Keg is not worth losing a hard-won piece of equipment. Use the Trollsbane for saving; the Broken Keg only when gold is critically short and the party carries nothing irreplaceable.

### Rest and save

Rest and save are only available at inns.

Plan wilderness expeditions to return to a town before fatigue and health become critical.

Saving is not available in the wilderness or in dungeons.

### Conversation and keywords

Quests are obtained by talking to NPCs using specific **keywords**.

An NPC will mention a topic; the player must use that keyword with the NPC to receive the quest.

Write down every keyword mentioned in conversation, because the game does not always prompt you to use them immediately.

A character with Charisma 80+ should handle all quest conversations to ensure the quest is offered.

## Wilderness travel

### Terrain and movement

The overworld of Ashtalarea connects towns, forests, swamps, mountains, and quest locations.

Travel speed depends on the slowest party member, which is why horses for every character are essential.

Kelden can fly in combat but not on the overworld map; horses remain necessary for them.

Dwarves can ride horses despite what the Brettle stable claims. If the Brettle stable refuses to sell to a Dwarf, try stables in other towns.

### Food and nutrition

The party consumes food during travel.

Buy food at the Lonely Page Pub in Brettle and at pubs in other towns.

Running out of food in the wilderness causes health loss and can strand the party far from help.

Carry surplus food before any long expedition, especially to remote quest locations like Berthand's Bay, Downing Swamp, or the Mountain of Lorr.

### Weather and encounters

Wilderness travel triggers random encounters with enemies appropriate to the terrain.

Forest areas may yield ghouls, thugs, or ogres. Swamp areas host muck creatures. Mountain areas hold orcs, stone ogres, and trolls.

Encounters can be fled from using the Panic defense, but dropped weapons may be lost.

Travel in areas appropriate to the party's current strength; do not wander into the Darkwood or Downing Mountains before the party is ready for ogres and stone ogres.

## Combat system

### Planning phase

Combat is **turn-based** with a planning phase. All party members' actions are selected before any actions execute.

During the planning phase, select an action for each character: attack, defend, move, rest, or cast.

After all actions are planned, the round executes with characters and enemies acting in order of Quickness.

### Foresight

The **Foresight** stat reveals enemy actions during the planning phase.

A character with high Foresight can see what each enemy intends to do before committing their own action, allowing counter-programming: duck against a high attack, jump against a low attack, or thrust to the area an enemy is moving into.

Foresight is one of the most valuable combat stats. Prioritize it on at least one frontline character.

### Attack types (weapon)

| Attack | Damage | Speed | Defense while attacking | Use when |
|---|---|---|---|---|
| **Berserk** | Highest | Slowest | Stand | You want maximum damage and can absorb the counter |
| **Hack** | High | Moderate | Any | Reliable overhead attack; good default |
| **Thrust** | Low | Fast | Any | Opponent is moving; target the area they will enter |
| **Slash** | Moderate | Faster | Any | Faster than Hack with slightly less power |

### Attack types (unarmed)

| Attack | Use when |
|---|---|
| **Kick** | Disarmed or grappled; targets lower body |
| **Bash** | Close quarters; general unarmed strike |
| **Head Butt** | Very close range; moderate damage |
| **Punch** | Fast fallback; weakest unarmed option |

### Aiming

| Aim | Target area | Notes |
|---|---|---|
| **High Shot** | Head, arms, upper chest | Disables arms; prevents weapon use |
| **Body Shot** | Torso, can hit anywhere | Default; most reliable connection |
| **Low Shot** | Legs, lower torso | Prevents jumping; slows or cripples |

Aiming at specific body parts is a core tactical system:

- **Attack the chest** with Berserk until all body parts are red, then one more hit kills instantly.
- **Attack the arms** to disable an enemy's weapon arm, preventing them from attacking.
- **Attack the legs** to prevent the enemy from jumping, making low attacks guaranteed to connect.

### Defense

| Defense | Protection | Attack capability | Use when |
|---|---|---|---|
| **None** | None | Full | You are confident or desperate |
| **Panic** | Best | None | Fleeing; may drop weapons |
| **Stand** | Moderate | Full | Trading blows with Berserk |
| **Back Up** | Good | Full | Opponent uses middle/body attacks |
| **Dodge** | Good | Reduced | Opponent uses mid-height attacks |
| **Duck** | Good vs high | Reduced | Opponent aims High Shot |
| **Jump** | Good vs low | Reduced | Opponent aims Low Shot |

### Defense tactics

- **Jump** is effective against low attacks.
- **Duck** is effective against high attacks.
- **Back Up** is useful against middle attacks.
- **Panic** provides the best defense but prevents attacking and may cause dropped weapons.
- **Sheath weapons before fleeing** to avoid losing them. Bows cannot be sheathed, so archers should keep their distance or accept the risk.

### Movement in combat

| Movement | Distance | Defense while moving | Notes |
|---|---|---|---|
| **Walk** | 1 space | Some defense | Safe repositioning |
| **Run** | 1 space | No defense | Quick retreat or approach |
| **Sprint** | 2 spaces | No defense | Close distance or escape fast |
| **Fly** | 1 space | Some defense | Kelden only |
| **Fly Faster** | 2 spaces | Reduced | Kelden only |
| **Zoom** | 3 spaces | None | Kelden only; maximum mobility |

Kelden flight is a decisive tactical advantage. A flying Kelden can reposition to flanks, escape melee, or reach archers behind enemy lines.

### Fatigue

Every action in combat costs **energy**. Attacking, defending, moving, and even standing all drain fatigue.

When a character's fatigue reaches zero, they **pass out** and cannot act for the remainder of the combat.

Use the **Rest** icon during combat to recover fatigue. A resting character does not attack or defend but regains energy.

Monitor fatigue across all party members. A party that exhausts itself early in a long fight will be defenseless in the later rounds.

Concentrate attacks on already injured or fatigued enemies to end fights before fatigue becomes critical.

### Archery

Each archer carries **20 arrows per battle**.

Archers cannot sheathe bows, so if they Panic to flee, the bow may be dropped.

Keep archers at the back of the formation and protect them with frontline fighters.

### Picking up dropped weapons

Weapons dropped during combat (by fleeing, disarming, or panic) can be **picked up** during combat.

Use a movement action to move to the square where the weapon lies, then pick it up on the next action.

This is especially important for unique or expensive weapons lost to Panic.

### Body part damage

Combat tracks damage to individual body parts: head, arms, torso, and legs.

A disabled arm prevents weapon use. A damaged leg prevents jumping. A battered torso reduces overall health.

This system works both ways: disable enemy arms to neutralize their attacks, and protect your own party members' body parts by using appropriate defense.

## Magic system

### The six magic orders

There are six magic orders. A character can only **join one** order, and joining locks the character into that order's spell modifications.

| Order | Location | Race restriction | Theme |
|---|---|---|---|
| **White Pearl** | Brettle | Human, Elf | General magic |
| **Blue Gem** | Tegal Forest | Kelder, Dwarf | Earth and protection |
| **Black Onyx** | Shellernoon | Elemental | Elemental forces |
| **Secret Storm** | Poitle Lock | Giant | Storm and power |
| **Red Mist** | Thimblewald | Legendary | Legendary magic |
| **Dark Stone** | Olanthen | Undead | Death and shadow |

### Learning strategy

**Buy basic spells before joining an order.** Basic spells can be purchased from the wizard's tower in Brettle (Astimiah Eckhart) and from magic trainers in other towns.

Once a character joins an order, they can **modify** their spells, but the character's race component is fixed.

The optimal strategy is to **learn spells from all six orders** before joining any one. This gives the character the broadest spell base, which can then be modified within the chosen order.

After joining, the character can refine and modify those spells but cannot learn new spells from other orders.

### Spell components

Each spell is composed of four parts:

1. **Race** — fixed at character creation; cannot be changed
2. **Subclass** — determined by the magic order joined
3. **Effect** — the spell's function (damage, healing, protection, etc.)
4. **Power suffix** — modifies the spell's strength

Understanding these components helps in modifying spells after joining an order. Experiment with different effect and power combinations to optimize spell performance.

### Casting in combat

Spells are cast during the planning phase like any other combat action.

The spell consumes the character's energy (fatigue) and takes effect during the execution phase.

Spell casters should be protected by frontline fighters, as they are vulnerable during the planning and execution of a spell.

## Character advancement

### Weapon training

Weapon skills are trained at specific **weapon master** locations across Ashtalarea. Each master teaches specific weapons up to a maximum skill level.

| Master | Location | Weapons taught | Skill range |
|---|---|---|---|
| **Hvrad Myth** | Fortress of Brettle | Longsword, Broadsword, Short Spear, Battle Axe | 0–30 |
| **Fistan Stockhard** | Tower at 3-way junction north of Brettle | Broad Axe, Hand Axe, Hvy Crossbow, Great Axe | 0–45 |
| **Zachary Bladeshure** | Htron | Scimitar, Greatsword, Shortsword, Bastard Sword | — |
| **Mornag the Merciless** | Htron Training Grounds | Scimitar, Mace, Lt Crossbow, War Hammer | — |
| **Monvin the Elder** | Tegal Forest | Halberd, Morningstar, Flail, Broadsword | — |
| **Nigel Gulliam** | Building along Krell Way | Club, Halberd, Great Hammer, Quarterstaff | — |
| **Kelmore Stratsmoth** | Shellernoon | Long Spear, Morningstar, War Maul, Heavy Maul | — |
| **Rhunholland** | Olanthen | Longsword, Broadsword, Bastard Sword, Greatsword | — |
| **Tyrolliar Cellana** | Klvar Wood | Self Bow, Elf Bow, Long Bow, Dagger | — |

Mornag the Merciless in Htron does **not** welcome Kelden. Train Kelden weapon skills elsewhere.

Training costs gold and requires the character to travel to the master's location.

### Ranks and the arena

North of Brettle is an **arena** where characters can fight for rank promotion.

Rank promotions improve the character's standing and may unlock new abilities or equipment.

Arena fights are dangerous; ensure the character is well-equipped and rested before entering.

### Skill progression

Weapon skills improve through training and through use in combat.

A character trained to skill 30 at Hvrad Myth can continue to Fistan Stockhard for axes up to 45, or to another master for different weapons.

Plan weapon training routes alongside the quest progression, since many masters are in towns the party visits during quests.

## Equipment

### Weapons

Weapons in Knights of Legend have varying damage ranges, weights, and trainable skill associations.

Some weapons are unique quest rewards with special properties:

| Weapon | Damage | Notes |
|---|---|---|
| **Truth Sword** | 4–32 | Very light; no trainable skill; quest reward |
| **Deathblade** | 5–27 | Quest reward; strong damage |

Most weapons are purchased from shops and improved through training with weapon masters.

Match the weapon to the character's trained skill. A character trained in Longsword should wield a Longsword, not a weapon they have no skill in.

### Armor

Armor is purchased from **Ludeman Armorers** in Brettle and from armorers in other towns.

Armor covers three body regions:

- **Head armor** — protects against High Shots to the head
- **Torso armor** — protects against Body Shots and chest attacks
- **Leg armor** — protects against Low Shots

**Shields** provide additional defensive value and are separate from worn armor.

Armor has weight, which contributes to encumbrance. Heavier armor provides better protection but slows the character and increases fatigue costs.

Dwarves excel in heavy armor due to their sturdy build. Kelden may prefer lighter armor to preserve their flight mobility.

### Encumbrance and weight

Every item has a weight value. A character's total carried weight affects their combat performance:

- High encumbrance increases fatigue costs for all actions.
- High encumbrance may reduce movement options.
- Exceeding the carrying capacity (determined by Strength and Size) prevents picking up additional items.

Distribute heavy items across the party. Give the strongest characters the heaviest gear and loot.

The Truth Sword is notable for being very light, making it an excellent choice for a character who is already near their encumbrance limit.

### Armor fitting

Armor must fit the character's body size. A character with a very large or very small Size may not fit standard armor.

Check fit before purchasing. If an armor shop does not have fitting armor, try shops in other towns.

### Customizing figures and shields

The game allows editing character figures and shields **pixel by pixel**.

This is a cosmetic feature that does not affect gameplay but allows personalized party visuals.

## The 24 quests

This walkthrough presents the quests in their intended sequence. Each quest names the quest-giver, their location, the keyword to use, the enemy or destination, and any special notes.

### Quest 1: The Stolen Gavel

- **Quest-giver:** Stephanie, in Brettle
- **Keyword:** `gavel`
- **Objective:** Recover the stolen gavel from ruffians
- **Location:** Tantowyn
- **Notes:** Early combat quest. Ensure the party is equipped and trained before engaging ruffians.

### Quest 2: The Stolen Standard

- **Quest-giver:** Stephen, in Brettle
- **Keyword:** `standard`
- **Objective:** Recover the stolen standard from bandits
- **Location:** North of Brettle
- **Notes:** Bandits are slightly tougher than ruffians. Use the combat planning phase to focus fire on one bandit at a time.

### Quest 3: The Stolen Quill

- **Quest-giver:** Hegissa, in Brettle
- **Keyword:** `knight`
- **Objective:** Recover the stolen quill from ghouls
- **Location:** Klvar Wood
- **Notes:** Ghouls are undead; a spell caster with anti-undead spells is valuable here.

### Quest 4: The Truth Sword

- **Quest-giver:** Mayor Benjamin, in Brettle
- **Keyword:** Combine the letters K + A + M
- **Objective:** Recover the Truth Sword from goblins
- **Location:** South of Brettle
- **Notes:** The Truth Sword (4–32 damage, very light, no trainable skill) is a permanent reward. This is one of the most valuable early-game items.

### Quest 5: The Crown

- **Quest-giver:** Biblik the Sage, in Htron
- **Keyword:** (speak with Biblik)
- **Objective:** Recover the crown
- **Location:** Tegal River
- **Notes:** The party should travel to Htron (west of Brettle) and speak with Biblik the Sage.

### Quest 6: The Parth Oil

- **Quest-giver:** Sam, in Htron
- **Keyword:** `stod`
- **Objective:** Obtain the Parth Oil
- **Location:** Berthand's Bay
- **Notes:** Berthand's Bay is a remote location; carry surplus food and rest before the expedition.

### Quest 7: The Shipwheel

- **Quest-giver:** Pegleg, with pirates west of Stone Island
- **Keyword:** `Nobjor`
- **Objective:** Obtain the Shipwheel
- **Location:** Erwenwald
- **Notes:** Speak with Pegleg among the pirates. The keyword `Nobjor` is essential to progress the conversation.

### Quest 8: The Pirate Hat

- **Quest-giver:** Scotty
- **Keyword:** `map`
- **Objective:** Obtain the Pirate Hat from sylphs
- **Location:** Prazen Point
- **Notes:** Sylphs are elemental creatures. Use appropriate defense against their attack patterns.

### Quest 9: The Iron Chest

- **Quest-giver:** Tulliana Daverland, in Htron
- **Keyword:** `map`
- **Objective:** Obtain the Iron Chest from minotaurs
- **Location:** Ebbwater
- **Notes:** Minotaurs are strong melee fighters. Disable their arms with High Shots to neutralize their attacks.

### Quest 10: The Golden Necklace

- **Quest-giver:** Belinda, in Olanthen
- **Keyword:** (speak with Belinda)
- **Objective:** Obtain the Golden Necklace from orcs
- **Location:** Mountain of Lorr
- **Notes:** Olanthen is in the east. The Mountain of Lorr is a remote, dangerous area. Ensure the party has heavy armor and healing supplies.

### Quest 11: The Wand

- **Quest-giver:** Orofin, in Poitle Lock
- **Keyword:** (speak with Orofin)
- **Objective:** Obtain the Wand from skeletons
- **Location:** Southern river
- **Notes:** Skeletons are undead. Anti-undead spells and blunt weapons (maces, hammers) are effective.

### Quest 12: The Coat of Arms

- **Quest-giver:** Sedfrey, in Poitle Lock
- **Keyword:** `gold`
- **Objective:** Obtain the Coat of Arms from thugs
- **Location:** Tegal Forest
- **Notes:** Thugs are human combatants. Use standard melee tactics and focus fire.

### Quest 13: The Oil of Changeling

- **Quest-giver:** Milinya, in Thimberwald
- **Keyword:** (speak with Milinya)
- **Objective:** Obtain the Oil of Changeling from muck creatures
- **Location:** Downing Swamp
- **Notes:** Muck creatures are swamp-dwelling beasts. The swamp terrain may slow movement; bring horses and surplus food.

### Quest 14: The Cloak

- **Quest-giver:** Trimrose, in Thimberwald
- **Keyword:** `Delmor`
- **Objective:** Obtain the Cloak
- **Location:** Karg Hill / Northwald
- **Notes:** The **Flying Cloak** allows flying, which is a major mobility upgrade for non-Kelden characters. This is a high-priority quest reward.

### Quest 15: The Vial

- **Quest-giver:** Keldinarr, in Thimblewald
- **Keyword:** `vial`
- **Objective:** Obtain the Vial
- **Location:** Windy Run
- **Notes:** Thimblewald and Thimberwald appear to be the same town; check both spellings in NPC dialogue.

### Quest 16: The Millet

- **Quest-giver:** Ballaster, at Krag Keep
- **Keyword:** `scalfeth`
- **Objective:** Obtain the Millet from mist giants
- **Location:** Wesswald
- **Notes:** Mist giants are powerful enemies. The **Courage Coat** is required to face terrible creatures like giants; equip it before this quest.

### Quest 17: The Golden Chalice

- **Quest-giver:** Dunnigen, in Tegal Forest
- **Keyword:** `rhording`
- **Objective:** Obtain the Golden Chalice from ogres
- **Location:** The Darkwood
- **Notes:** Ogres are heavy melee fighters. Use Berserk attacks to the chest, and disable arms with High Shots.

### Quest 18: The Hidden Staff

- **Quest-giver:** Lord Stiveron, at Hobean Keep
- **Keyword:** `inthos`
- **Objective:** Obtain the Hidden Staff from stone ogres
- **Location:** Downing Mountains
- **Notes:** Stone ogres are tougher than regular ogres. Ensure the party has trained weapon skills and upgraded armor.

### Quest 19: The Wristband

- **Quest-giver:** Rodrigard, at Sheller Bridge
- **Keyword:** `bryor`
- **Objective:** Obtain the Wristband from ettins
- **Location:** Sheller Ridge
- **Notes:** Ettins are two-headed giants with strong melee attacks. Use defense tactics to mitigate their damage while focusing on disabling their arms.

### Quest 20: The Djinn Item

- **Quest-giver:** Aurin, at Sheller Bridge
- **Keyword:** `grey`
- **Objective:** Obtain the Djinn Item from djinn
- **Location:** Thanakesh Hills
- **Notes:** Djinn are magical creatures with elemental attacks. Spell casters with protective spells are valuable here.

### Quest 21: The Shade Ring

- **Quest-giver:** Sheller Elite Guard (mention Aurin sent you)
- **Keyword:** (tell them Aurin sent you)
- **Objective:** Obtain the Shade Ring from cliff trolls
- **Location:** Westwash
- **Notes:** Cliff trolls are dangerous. Wear the Courage Coat before engaging trolls. The connection to Aurin from Quest 20 is the key to receiving this quest.

### Quest 22: The Ward

- **Quest-giver:** Lord Norgan, in Shellernoon
- **Keyword:** `silver knot`
- **Objective:** Obtain the Ward from sledge creatures
- **Location:** Sodden Hills
- **Notes:** This quest requires a **party split**. Plan carefully which characters go where, ensuring both groups can survive independently.

### Quest 23: The Statuette

- **Quest-giver:** Denswurth, in Olanthen
- **Keyword:** (speak with Denswurth)
- **Objective:** Obtain the Statuette from trolls
- **Location:** Missip Valley
- **Notes:** Trolls are dangerous creatures. Wear the Courage Coat. Use fire or acid if available, as trolls may regenerate.

### Quest 24: Rescue Seggallion

- **Quest-giver:** Dundle, at the Assembly Building in Olanthen Barrier
- **Keyword:** (speak with Dundle)
- **Objective:** Rescue Seggallion from cyclops
- **Location:** Ghor Hills
- **Notes:** This is the final quest. Cyclops are powerful single giants. Bring the full party with the best equipment, all key items, and plenty of food. Ensure all previous quests are completed before attempting this.

## Key items and rewards

### Quest reward items

| Item | Source quest | Effect |
|---|---|---|
| **Truth Sword** | Quest 4: The Truth Sword | 4–32 damage, very light, no trainable skill |
| **Flying Cloak** | Quest 14: The Cloak | Allows non-Kelden characters to fly |
| **Courage Coat** | Quest 16 (or earlier) | Required to face terrible creatures (trolls, giants) |
| **Speed Boots** | Quest chain | Faster movement on overworld and in combat |
| **Great Shield** | Quest chain | Excellent protection, lightweight |
| **Deathblade** | Quest chain | 5–27 damage; strong melee weapon |
| **Magic Ingots** | Quest chain | Forge custom weapons at a blacksmith |

### Using key items

- **Flying Cloak**: Equip on a non-Kelden character to grant flight in combat. This doubles the party's airborne tactical options.
- **Courage Coat**: Equip before facing trolls, giants, or other "terrible creatures." Without it, the party may be unable to engage certain enemies.
- **Speed Boots**: Equip on the slowest party member to improve overall party travel speed.
- **Great Shield**: Equip on a frontline fighter for excellent protection without a heavy weight penalty.
- **Magic Ingots**: Take to a blacksmith to forge custom weapons. These weapons can be tailored to the party's needs.
- **Truth Sword**: Equip on a character who does not have a trained weapon skill, since it requires no training. Its very light weight makes it ideal for a spell caster or scout who needs a melee fallback without encumbrance.

## Money tips

### No gold transfer between characters

Knights of Legend does not allow direct gold transfer between party members.

This means each character must manage their own finances for purchases.

### The dummy character gold exploit

To generate starting gold for a main party:

1. Create a dummy **Brettle Regular** character. This class starts with 3000 GC.
2. Use the dummy character to buy **plate armor** from Ludeman Armorers.
3. Trade the plate armor to a main party member.
4. Have the main party member sell the plate armor at a shop.
5. Repeat with additional dummy characters as needed.

This converts the dummy character's starting gold into transferable equipment value.

### Inn costs

The Trollsbane Inn charges 60 GC per character per night. For a six-member party, that is 360 GC per rest.

Budget for inn costs when planning extended town stays for training or shopping.

The Broken Keg Inn is free but risks item theft. Use it only when gold is critically short.

## General tips and tricks

### Party composition

- Create a **diverse party** with different races. Some NPCs will not talk to certain races.
- Have at least **two characters with Charisma 80+** to ensure quests are offered.
- Include at least **one Kelden** for its flying ability and combat strength.
- Include at least **two spell casters** for tough battles, ideally from different magic orders.
- A party of six is safer than a party of four; more characters mean more actions per combat round and more targets to distribute damage.

### Travel

- Buy **horses for everyone**. Party speed equals the slowest character, so one unhorsed member slows the entire group.
- **Dwarves can ride horses** despite what the Brettle stable claims. If the Brettle stable refuses, buy horses in other towns.
- Carry **surplus food** on long expeditions. Running out of food in the wilderness is dangerous.
- Rest and save at **inns only**. Plan expeditions to return to a town before rest is needed.

### Combat

- **Jump** is effective against low attacks.
- **Duck** is effective against high attacks.
- **Back Up** is useful against middle attacks.
- **Attack the chest** with Berserk until all body parts are red, then one more hit kills instantly.
- **Attack the arms** to disable an enemy's weapon use.
- **Attack the legs** to prevent the enemy from jumping.
- **Thrust** to the area an opponent is moving toward.
- **Sheath weapons before fleeing** to avoid losing them. Bows cannot be sheathed.
- **Keep bows** for archers; archers should stay at range.
- **Concentrate fire** on already injured or fatigued enemies to reduce the number of active opponents quickly.
- Use a **scout with high Quickness** to distract enemies, drawing attacks away from more vulnerable party members.

### Equipment and key items

- **Wear the Courage Coat** before facing trolls, giants, or other terrible creatures.
- The **Flying Cloak** grants flight to non-Kelden characters; equip it on a spell caster or archer for positioning advantage.
- The **Truth Sword** requires no weapon training; give it to a spell caster as a melee fallback.
- **Magic Ingots** can forge custom weapons; save them for when the party has identified its optimal weapon types.
- **Distribute weight** across the party. Heavy armor and loot on strong characters, light gear on casters and scouts.

### Training and advancement

- Train weapon skills at **weapon masters** before tackling tougher quests.
- Plan training routes alongside quest progression, since many masters are in towns the party visits during quests.
- Visit the **arena** north of Brettle for rank promotion fights when characters are well-equipped.
- Learn **basic spells from all six orders** before joining one order, to maximize the spell base.

### Character customization

- **Reroll stats** as many times as desired during creation. Do not accept mediocre rolls.
- Edit character **figures and shields** pixel by pixel for personalized visuals.
- Create **dummy Brettle Regular** characters to generate gold via the armor trade exploit.

### Magic

- Buy basic spells from **Astimiah Eckhart** in the Brettle wizard's tower before joining any order.
- After joining an order, **modify spells** to optimize their performance.
- The race component of a spell is **fixed** at character creation and cannot be changed.
- Use spell casters to support combat: healing, protection, and anti-undead spells are all valuable.
- Protect spell casters with frontline fighters; they are vulnerable during the casting phase.

## Mouse control issues and DOSBox settings

### The double-click problem

Knights of Legend uses a **two-click** mouse interface: the first click highlights an icon or option, and the second click selects it.

This system was designed for 1989-era mouse hardware and can be difficult to use under DOSBox emulation:

- The first click may not register if the cursor is not precisely positioned.
- The second click may register as a new highlight instead of a selection if the cursor drifts.
- Mouse sensitivity settings in DOSBox can make the cursor too fast or too slow for precise clicking.

### Recommended solution: use the keyboard

The keyboard is the **faster and more reliable** input method for Knights of Legend:

- **Arrow keys** for movement
- **`<` and `>`** to cycle through icons on the current screen
- **`ENTER`** to select the highlighted icon
- **`ESC`** to go back or activate the U-Turn icon
- **`Ctrl-Q`** to quit to DOS
- **Number keys** for menu selection on the table of contents screen

The entire game can be played without the mouse. If the mouse is causing frustration, switch to keyboard-only play.

### DOSBox configuration

The game has **no frame limiter**, so the CPU cycle count directly controls game speed.

Recommended settings in `dosbox.conf`:

```ini
[cpu]
cycles=fixed 3000

[mouse]
sensitivity=1.0
```

- **`cycles=fixed 3000`**: Provides consistent game speed matching original-era hardware. Higher values make the game run too fast; lower values make it sluggish. Adjust up or down in small increments if the game feels wrong.
- **`sensitivity=1.0`**: Default mouse sensitivity. Lower the value (e.g., `0.5`) if the cursor overshoots targets. Raise it slightly (e.g., `1.5`) if the cursor feels too slow.

If using DOSBox-X, the same settings apply. Check both `[cpu]` and `[mouse]` sections.

### No frame limiter

Because the game has no internal frame limiter, setting `cycles=max` or very high fixed values will cause the game to run at excessive speed, making combat planning and menu navigation difficult.

Always use `cycles=fixed 3000` or a similar moderate fixed value for this game.

If the game still runs too fast or too slow, adjust the fixed cycle count in increments of 500 until the speed feels right.

## Quick endgame checklist

Before attempting Quest 24 (Rescue Seggallion), confirm the following:

- All 23 previous quests are completed and their key items are in the party's inventory.
- The party has the **Courage Coat** equipped for facing terrible creatures.
- The party has the **Flying Cloak** for tactical mobility in the final battle.
- All characters have trained weapon skills and the best available weapons and armor.
- The party carries surplus food for the expedition to Ghor Hills.
- All characters have horses for overworld travel speed.
- At least two spell casters are in the active party with full spell loads.
- At least two characters have Charisma 80+ (in case NPC interaction is needed en route).
- The party rested and saved at an inn in Olanthen before departing.
- The Truth Sword, Deathblade, Great Shield, Speed Boots, and Magic Ingots are distributed appropriately among party members.

## Final advice

Knights of Legend rewards preparation, diverse party composition, and careful combat planning.

Talk to every NPC with your highest-charisma character.

Write down every keyword mentioned in conversation.

Reroll stats until each character excels in their role.

Train weapon skills before tackling tougher quests.

Learn spells from all six orders before joining one.

Buy horses for everyone; the slowest character sets the pace.

Wear the Courage Coat before facing trolls and giants.

Use Foresight to read enemy actions and counter them with the right defense.

Attack body parts strategically: arms to disable, legs to prevent jumping, chest to kill.

Sheath weapons before fleeing combat.

Save at inns, carry surplus food, and plan expeditions to return before fatigue becomes critical.

Then complete the 24 quests, collect every key item, and bring Seggallion home.
