# Wizardry: Proving Grounds of the Mad Overlord -- Play and Strategy Guide

*Wizardry: Proving Grounds of the Mad Overlord* (Sir-Tech, 1981) -- designed by
**Andrew C. Greenberg** and **Robert Woodhead**. One of the first party-based
computer role-playing games and the game that defined the first-person
dungeon-crawler genre. The version documented here is the IBM PC port, which
runs the original Apple II UCSD Pascal game through a p-system emulator under
DOSBox.

The game's own data structures (character record, maze grid, spell tables)
were recovered from the p-code and the reconstructed Pascal source; see
`ReverseEngineering.md` for the technical details. This guide focuses on how
to play and how to win.

---

## 1. Overview

You assemble a party of up to six adventurers in the castle, descend ten
levels into the **Maze of the Mad Overlord**, and on the bottom level
confront the evil archmage **Werdna** (the name is "Andrew" spelled backward).
Defeating him lets you claim the **Amulet**, which you must carry back to the
surface to win the game.

The dungeon is a **20 x 20 grid per level**, ten levels deep. Every square can
have walls on any of its four sides, and the game renders a first-person 3D
view of the corridor ahead. There is no auto-map: you are expected to draw your
own on graph paper, which is part of the original challenge.

The game is turn-based and menu-driven. There are no real-time elements: the
world only changes when you press a key.

### 1.1 The story

The wizard **Trebor** (Robert backward) once ruled the land from his castle.
The evil archmage **Werdna** stole the **Amulet** from Trebor and fled into the
ten-level maze he carved beneath the castle. Trebor cannot follow -- the maze
is full of monsters and traps -- so he offers gold and glory to any adventurer
brave enough to descend, defeat Werdna, and bring back the Amulet.

Your party starts at the **Edge of Town**, the staging area outside the
dungeon entrance. You create characters, buy equipment, and then enter the
maze to fight, explore, and eventually confront Werdna on level 10.

---

## 2. Controls

Wizardry uses a **single-key command interface**. At almost every prompt the
game expects one letter, then Enter (some versions accept the letter without
Enter). The game is entirely keyboard-driven; there is no mouse support.

### 2.1 Castle commands

| Key | Action |
| --- | --- |
| **(C)haracter** | Create a new character |
| **(G)ilgamesh's** | Enter Gilgamesh's Tavern (add/remove party members) |
| **(B)oltac's** | Enter Boltac's Trading Post (buy/sell/identify) |
| **(T)emple** | Enter the Temple of Cant (heal/cure/resurrect) |
| **(R)eview** | Go to the Review Board (check status, level up) |
| **(E)dge** | Go to the Edge of Town (enter/exit the dungeon) |
| **(I)nn** | Rest at the Inn (restore HP and spell charges) |

### 2.2 Maze commands

| Key | Action |
| --- | --- |
| **N** / **S** / **E** / **W** | Move one square north / south / east / west |
| **Arrow keys** | Move in the indicated direction (some ports) |
| **(A)ttack** | Attack an adjacent enemy (not used outside combat) |
| **(B)oard** | Board the elevator (level 3 only, when standing on it) |
| **(C)amp** | Camp -- rest, memorize spells, inspect party |
| **(D)rop** | Drop an item from inventory |
| **(E)xamine** | Examine / inspect a character |
| **(F)ight** | Fight (used in combat) |
| **(G)ive** | Give an item to another character |
| **(I)nspect** | Inspect a character's details |
| **(J)ump** | Jump (over a pit or obstacle) |
| **(L)ook** | Look around the current square |
| **(M)ove** | Move (alternative to directional keys) |
| **(P)ool** | Pool gold (transfer gold between characters) |
| **(R)eady** | Ready (equip) an item from inventory |
| **(S)earch** | Search the current square for secrets |
| **(T)alk** | Talk (to NPCs, in certain squares) |
| **(U)se** | Use an item |
| **(V)iew** | View the party roster |
| **(W)ait** | Wait one turn (pass) |

### 2.3 Combat commands

| Key | Action |
| --- | --- |
| **(F)ight** | Attack with a readied weapon |
| **(C)ast** | Cast a spell |
| **(P)arry** | Defend (reduce incoming damage, skip attack) |
| **(U)se** | Use an item in combat |
| **(R)un** | Attempt to flee the battle |
| **(D)efend** | Defend position (some versions) |

### 2.4 At the Maze edge

At the Edge of Town you choose to **enter the dungeon** or **return to the
castle**. When you enter, the party is placed at the stairs-up square on
dungeon level 1. When you leave the dungeon via the same stairs, you return to
the Edge of Town.

---

## 3. Character Creation

### 3.1 The six attributes

Every character has six attributes, each in the range **3 to 18** (18 is
perfect, 3 is the worst possible roll). The attributes and their effects:

| Attribute | Abbrev. | Effect |
| --- | --- | --- |
| **Strength** | STR | Melee damage and to-hit; required for Fighter, Samurai, Lord, Ninja |
| **Intelligence** | I.Q. | Mage spell effectiveness; required for Mage, Bishop, Samurai, Ninja |
| **Piety** | PIE | Priest spell effectiveness; required for Priest, Bishop, Lord, Ninja |
| **Vitality** | VIT | Hit points and resurrection survival; required for Lord, Ninja |
| **Agility** | AGI | Combat initiative, armor effectiveness, thief skills; required for Ninja |
| **Luck** | LCK | Avoiding traps, critical hits, resisting some effects |

### 3.2 Five races

| Race | Attribute bonuses / notes | Restrictions |
| --- | --- | --- |
| **Human** | No modifiers, but can reach any class | None |
| **Elf** | High INT; good Mages and Samurai | Limited VIT |
| **Dwarf** | High STR and VIT; tough Fighters | Low INT and LCK |
| **Gnome** | High PIE and LCK; good Priests | Moderate STR |
| **Hobbit** | High AGI and LCK; good Thieves | Low STR and VIT |

### 3.3 Eight classes

| Class | Role | Key requirements | Notes |
| --- | --- | --- | --- |
| **Fighter** | Melee tank | STR 11+ | All weapons and armor; easiest class |
| **Mage** | Offensive spells | I.Q. 11+ | Mage spells; very fragile |
| **Priest** | Healing and support | PIE 11+ | Priest spells; moderate armor |
| **Thief** | Traps and locks | AGI 11+ | Disarm traps; open chests; backstab |
| **Bishop** | Mage + Priest spells | I.Q. 12+, PIE 12+ | Identifies items; learns both spell lists, but slower |
| **Samurai** | Fighter + Mage | STR 15+, I.Q. 11+, VIT 14+, AGI 10+ | Melee fighter with mage spells |
| **Lord** | Fighter + Priest | STR 15+, I.Q. 12+, VIT 15+, AGI 12+ | Fighter with priest spells |
| **Ninja** | All classes | STR 15+, I.Q. 15+, PIE 15+, VIT 15+, AGI 15+, LCK 15+ | All weapons/armor (but few needed); best class, hardest to qualify |

### 3.4 Alignment

| Alignment | Effect |
| --- | --- |
| **Good** | Can be any class except those restricted; most flexible for beginners |
| **Neutral** | Can mix with both Good and Evil party members |
| **Evil** | Required for some class/race combos; can be expelled from Good parties |

A party can contain mixed alignments, but **Good and Evil** characters cannot
normally be in the same party. Neutral characters bridge the two. For
beginners, an all-Good or all-Neutral party avoids alignment conflicts.

### 3.5 The roll

The game rolls all six attributes **randomly** when you create a character.
You can **reroll** as many times as you like -- there is no cost and no
penalty. This is the accepted way to play: reroll until you get a set of
attributes that qualifies for the class you want.

**Tips for a good roll:**

- For a **Fighter**: aim for STR 15+, VIT 14+, AGI 12+
- For a **Mage**: aim for I.Q. 15+, AGI 12+
- For a **Priest**: aim for PIE 15+, VIT 12+
- For a **Thief**: aim for AGI 15+, LCK 12+
- For a **Samurai**: STR 15+, I.Q. 11+, VIT 14+ (rare; expect many rerolls)
- For a **Lord**: STR 15+, I.Q. 12+, VIT 15+, AGI 12+ (very rare)
- For a **Ninja**: all 15+ (extremely rare; expect to reroll for a long time)

Characters also have a **password** -- a short string the game uses to verify
the character on certain actions. Do not forget it.

### 3.6 Creating your first character

1. At the castle menu, choose **(C)haracter** to create.
2. Enter a name (up to 15 characters).
3. Choose a **password**.
4. The game rolls attributes. Reroll until satisfied.
5. Choose a **race**.
6. Choose an **alignment**.
7. Choose a **class**. If your attributes do not meet the requirements, the
   class is not offered.
8. The character is saved to the roster and can be added to a party at
   Gilgamesh's Tavern.

---

## 4. Party Composition

### 4.1 Party size and formation

A party holds **up to 6 characters**. Characters in the **front rows** (first
two positions) can attack with melee weapons and take the brunt of enemy
melee attacks. Characters in the **back rows** (last two positions) can cast
spells and use ranged weapons but cannot be hit by most melee attacks.

### 4.2 Recommended starting party

| Position | Class | Role |
| --- | --- | --- |
| 1 (front) | Fighter | Primary tank and melee damage |
| 2 (front) | Fighter | Secondary tank and melee damage |
| 3 (mid) | Thief | Disarm traps, open chests, scout |
| 4 (mid) | Priest | Healing, cure, resurrect |
| 5 (back) | Mage | Offensive spells (sleep, damage) |
| 6 (back) | (open) | Reserve slot -- add a Bishop or another Mage/Priest later |

This is the classic beginner party. The two Fighters absorb damage and deal
melee; the Priest keeps them alive; the Mage puts enemies to sleep and deals
area damage; the Thief handles trapped chests.

### 4.3 Advanced party ideas

- **2 Fighters, 1 Samurai, 1 Bishop, 1 Priest, 1 Thief** -- once you can
  qualify a Samurai, he adds mage-spell support to the front line.
- **1 Lord, 1 Samurai, 1 Bishop, 1 Priest, 1 Mage, 1 Thief** -- a late-game
  powerhouse, but requires high-attribute characters.
- **2 Fighters, 2 Mages, 1 Priest, 1 Thief** -- aggressive; two mages can
  sleep large groups, but healing is thinner.

### 4.4 Alignment considerations

- An **all-Good** party is the easiest for beginners: all classes are
  available (except Evil-only variants, which are rare), and there are no
  alignment conflicts.
- An **all-Neutral** party can include both Good and Evil characters, giving
  maximum flexibility.
- **Evil** characters are required for some advanced strategies, but an
  Evil character cannot be in a party with a Good character.

---

## 5. The Castle (Town Level)

The castle is the safe area where you prepare for dungeon expeditions. All
castle services are reached from the main castle menu.

### 5.1 The Edge of Town

The staging area. From here you **enter the dungeon** (the party is placed at
the stairs-up on level 1) or **return to the castle**. This is also where you
**save the game** -- the game prompts you to save when you leave the dungeon.
**Save every time you return.**

### 5.2 Gilgamesh's Tavern

Where you **add and remove party members**. Characters you have created live
on the roster (stored on disk); you bring up to six of them into the tavern and
form a party. You can also inspect characters here and leave characters
behind.

### 5.3 Boltac's Trading Post

The shop. Here you can:

| Action | Description |
| --- | --- |
| **Buy** | Purchase weapons, armor, and items |
| **Sell** | Sell unwanted equipment (usually at a fraction of the buy price) |
| **Identify** | Have an unidentified item appraised (a Bishop can also do this) |
| **Uncurse** | Remove a curse from a cursed item (expensive) |

Buy the **best armor and weapons you can afford** before entering the dungeon.
Equipment quality has a large effect on survival.

### 5.4 Temple of Cant

The church. Here you can:

| Action | Cost | Description |
| --- | --- |
| **Heal** | Gold | Restore hit points to a wounded character |
| **Cure** | Gold | Cure poison, stone, or other afflictions |
| **Resurrect** | Gold (high) | Attempt to bring a Dead character back to life |
| **Raise** | Gold (very high) | Attempt to recover a Lost character |

Resurrection is **not guaranteed**: if it fails, the character becomes **Lost**
(permanently gone, or recoverable only at great expense and risk). Always save
before attempting a resurrection.

### 5.5 Review Board

Where you **check character status** and **level up**. When a character has
accumulated enough experience points, the Review Board offers to raise their
level. Leveling up increases hit points, spell charges, and sometimes
attributes. You must visit the Review Board to level up -- it does not happen
automatically.

### 5.6 The Inn

Resting at the inn **restores all hit points and all spell charges** to full.
It advances the game clock (which ages characters slightly). Rest here after
every dungeon expedition before going back down.

---

## 6. Combat System

Combat is **turn-based**: the game presents the enemy group, you issue orders
for each party member, then the round resolves with all actions in initiative
order.

### 6.1 The combat round

1. The game announces the encounter: "ENCOUNTER!" and names the monster group.
2. For each party member, you choose an action: **Fight**, **Cast**, **Parry**,
   **Use**, or **Run**.
3. For **Fight**, you pick a target monster.
4. For **Cast**, you pick the spell level, the spell, and the target.
5. When all orders are in, the round resolves. Characters and monsters with
   higher **Agility** act first.
6. Damage is applied. Dead monsters are removed. Dead characters fall.
7. The next round begins. Repeat until one side is dead or you flee.

### 6.2 Armor Class (AC)

**Lower AC is better.** An unarmored character has AC 10. The best armor in
the game can bring AC down to 1 or even lower. A monster's chance to hit you
depends on your AC: the lower your AC, the harder you are to hit.

| AC | Protection level |
| --- | --- |
| 10 | No armor |
| 8 | Light armor / shield |
| 5 | Medium armor |
| 3 | Heavy armor + shield |
| 1 | Best armor + shield + bonus items |

Keep your front-line fighters in the **lowest AC you can afford**. AC matters
more than hit points for survival in the deeper levels.

### 6.3 To-hit and damage

The game uses a **THAC0-style** system (though the term was not yet coined in
1981). A roll of 1d20 is made for each attack; modifiers for Strength, weapon
bonus, and target AC are applied. A roll that meets or beats the target
number hits. Damage is rolled by weapon type (e.g., a long sword might roll
1d8, a dagger 1d4). Critical hits can occur on a natural 20.

### 6.4 Monster groups

Monsters appear in **groups** (sometimes multiple groups at once). A group of
"2 Orcs" means two separate monsters. Area-effect spells (like sleep or
damage spells) can hit an entire group. Some monsters are far more dangerous
than others -- learn which groups to fight and which to flee.

### 6.5 Death and Lost status

| Status | Meaning | Recovery |
| --- | --- | --- |
| **OK** | Alive and healthy | -- |
| **Dead** | Slain in combat | Resurrect at the Temple (may fail) |
| **Lost** | Resurrection failed, or lost to a trap | Raise at the Temple (very expensive, high failure rate) |
| **Ashes** | Lost character, raised to ashes | Gone for good in most cases |

A **Lost** character is not necessarily gone forever, but recovery is
expensive and risky. **Always save before entering the Temple with a Dead
character**, so a failed resurrection does not lose the character permanently.

### 6.6 Running away

Choosing **Run** attempts to flee the battle. It may fail, in which case the
monsters get a free round of attacks. Running is wise against groups you
cannot handle, but it is not guaranteed.

---

## 7. Magic System

Wizardry has two parallel spell lists: **Mage spells** and **Priest spells**.
Each list has **7 levels** with **7 spells per level** (49 mage spells + 49
priest spells = 98 total). A spellcaster learns spells by memorizing them at
camp or at the inn; the number of spells per level they can cast is determined
by their class and level.

### 7.1 Spell charges

Each character has a set of **spell charges** -- the number of spells of each
level they can cast before resting. Charges are refreshed by **resting at the
inn** or **camping** in the dungeon (camping in the dungeon risks encounters).
A level-3 Mage might be able to cast two level-1 spells and one level-2 spell;
a level-10 Mage can cast many spells across several levels.

The charge counters are stored in the character record as two arrays of seven
integers (one per spell level, for mage and priest). See `ReverseEngineering.md`
section 6 for the exact layout.

### 7.2 Learning spells

Spells are learned at the **Castle** (or by Bishops, who can learn from both
lists). You must be high enough level to learn a spell of a given level. The
game lists available spells when you choose to learn.

### 7.3 Key Mage spells

| Level | Spell | Effect |
| --- | --- | --- |
| 1 | **HALNPCAST** | Light (illuminates the current dungeon square) |
| 1 | **KATINO** | Sleep (puts a group of monsters to sleep) |
| 2 | **DUMAPIC** | Detect stairs and party location (critical for navigation) |
| 2 | **SOPIC** | Shield (raises caster's AC) |
| 3 | **MAHALITO** | Fireball (area damage to a monster group) |
| 4 | **DALHAVOC** | Lightning (heavy damage to a group) |
| 5 | **MAKANIT** | Heavy area damage |
| 6 | **LOKARAID** | Mass damage to all monsters |
| 7 | **TILTOWAIT** | The most powerful damage spell in the game |

### 7.4 Key Priest spells

| Level | Spell | Effect |
| --- | --- | --- |
| 1 | **KALKO** | Cure light wounds (heals a small amount) |
| 1 | **DIOMAPIC** | Detect traps and secrets |
| 2 | **DIALKO** | Cure serious wounds (heals more) |
| 2 | **MADILOMA** | Medium heal |
| 3 | **LOMILWA** | Light (longer duration than HALNPCAST) |
| 4 | **DALHAVOC** | Area damage (priest version) |
| 5 | **MABORMORE** | Mass heal (heals the whole party) |
| 6 | **LOKARAID** | Mass damage |
| 7 | **MALOR** | Teleport (can move the party between levels -- dangerous) |

### 7.5 Spell strategy

- **DUMAPIC** (mage level 2) is the single most important navigation spell.
  It tells you where stairs are relative to your position. Without it, you
  must map purely by hand.
- **KATINO** (mage level 1, sleep) is the best early-game combat spell: a
  sleeping monster cannot attack and is easier to hit.
- **LOMILWA** (priest level 3, light) lasts longer than HALNPCAST and lights
  more of the dungeon.
- Always keep **healing spells in reserve**. Do not burn all your charges on
  damage when you still have to walk back to the stairs.
- **MALOR** (teleport) can save your life by escaping a doomed level, but a
  bad teleport can deposit you inside a wall (instant party death). Use it
  only as a last resort.

---

## 8. The Dungeon (10 Levels)

The dungeon is a **20 x 20 grid per level**, ten levels deep. Each square can
have walls on any of its four sides. The game renders a first-person view of
the corridor. There is **no auto-map**: draw your own.

### 8.1 Level summary

| Level | Name | Difficulty | Key features |
| --- | --- | --- | --- |
| 1 | Castle level | Easy | Training area; weak monsters; entrance stairs |
| 2 | Upper maze | Easy-Moderate | First tough encounters; multiple rooms |
| 3 | The Elevator level | Moderate | Contains the elevator to deeper levels |
| 4 | Middle maze | Moderate | Good treasure; the Blue Ribbon is here |
| 5 | Lower middle | Moderate-Hard | Strong monsters; key transitions |
| 6 | Deep maze | Hard | Dangerous traps; tough fights |
| 7 | Werdna's domain begins | Hard | Powerful monsters; ascending difficulty |
| 8 | The deeps | Very Hard | Elite encounters; high treasure |
| 9 | Near the bottom | Very Hard | The most dangerous monsters in the game |
| 10 | Werdna's lair | Extreme | Final boss; the Amulet |

### 8.2 Level 1 -- Castle level (training area)

The entrance level. You arrive here from the Edge of Town. Monsters are weak
(kobolds, orcs, skeletons). This is where you **grind for experience and
gold** until your party is level 2-3 before descending. Stairs down are in
the southwest.

### 8.3 Level 2 -- Upper maze

More complex layout with multiple rooms. Monsters are slightly tougher
(goblins, giant rats, zombies). Stairs up to level 1, stairs down to level 3.

### 8.4 Level 3 -- The Elevator level

The **turning point** of the game. This level contains the **elevator**, a
special square that can transport the party to deeper levels once you have
found and boarded it. The elevator is the main shortcut in the game: once you
have reached it, you can bypass levels you have already cleared. Stairs up to
level 2, stairs down to level 4.

### 8.5 Level 4 -- Middle maze

Moderate difficulty with good treasure. This level contains the **Blue
Ribbon**, a key item needed to access certain lower levels. Without it, you
cannot progress past a certain point. Explore thoroughly. Stairs up to level
3, stairs down to level 5.

### 8.6 Level 5 -- Lower middle

Stronger monsters (trolls, ogres, wights). Treasure is better but fights are
riskier. Stairs up to level 4, stairs down to level 6.

### 8.7 Level 6 -- Deep maze

Dangerous traps (pits, teleporters, darkness). Monsters are very tough
(vampires, giant slimes). Bring cure spells and traps-disarming. Stairs up to
level 5, stairs down to level 7.

### 8.8 Level 7 -- Werdna's domain begins

The difficulty escalates sharply. Powerful monsters (dragons, demons,
liches). Only descend with a high-level party (level 13+). Stairs up to level
6, stairs down to level 8.

### 8.9 Level 8 -- The deeps

Elite encounters. High treasure but extreme danger. Vampires and werewolves
can drain levels. Stairs up to level 7, stairs down to level 9.

### 8.10 Level 9 -- Near the bottom

The most dangerous monsters in the game. Only a fully equipped, high-level
party should enter. Stairs up to level 8, stairs down to level 10.

### 8.11 Level 10 -- Werdna's lair

The final level. **Werdna** is here, guarding the **Amulet**. He is a
powerful spellcaster -- the fight is the climax of the game. Defeat him,
take the Amulet, and **return to the surface** (via stairs, elevator, or
MALOR teleport) to win.

### 8.12 The elevator

The elevator on **level 3** is a special square. When you stand on it and
press **(B)oard**, the game offers to take you to any level you have
previously reached via the elevator. This lets you skip re-clearing upper
levels on subsequent expeditions. The elevator is one-way per trip (you
board, pick a destination, and arrive there; to return you must find stairs
or use MALOR).

### 8.13 The Blue Ribbon

The **Blue Ribbon** is a key item found on **level 4**. It is required to
pass a certain gate or area on the path to the deeper levels. Without it,
progress is blocked. Find it by exploring level 4 thoroughly and opening
chests.

---

## 9. How to Win

A step-by-step route from character creation to the Amulet.

### Step 1: Create a balanced party

Create **2 Fighters, 1 Mage, 1 Priest, 1 Thief**, and leave one slot open.
Reroll until each character's prime attribute is at least 15. Buy the best
weapons and armor you can afford at Boltac's for the Fighters and Thief.

### Step 2: Grind on level 1

Enter the dungeon. Fight weak monsters on level 1 until your characters are
**level 2-3**. Run from fights you cannot win. Return to the Edge of Town
when HP or spell charges are low. Save. Rest at the inn. Repeat.

### Step 3: Buy better equipment

Spend your gold at Boltac's on better weapons and armor. Prioritize
**lowering the Fighters' AC**. Buy shields, plate mail, and the best weapons
available. Identify any found items before using them (cursed items are
dangerous).

### Step 4: Descend to level 2, then 3

When the party is level 3-4, descend to level 2 and clear it. Then descend to
level 3 and **find the elevator**. Map each level as you go.

### Step 5: Learn key spells

Ensure your Mage learns **DUMAPIC** (detect stairs) and **KATINO** (sleep).
Ensure your Priest learns **KALKO** (cure) and **LOMILWA** (light). These
four spells are the backbone of dungeon survival.

### Step 6: Find the Blue Ribbon on level 4

Descend to level 4 (via stairs or elevator). Explore thoroughly and open
chests (have the Thief disarm traps first). Find the **Blue Ribbon**. Without
it, you cannot progress to the deepest levels.

### Step 7: Descend slowly through levels 5-9

Descend one level at a time. On each level:
- Map the level.
- Fight to gain XP and gold.
- Return to the surface when HP or spells run low.
- Level up at the Review Board.
- Buy better equipment.
- Repeat until the party is strong enough for the next level.

By the time you reach level 9, your party should be **level 13-20** with
the best equipment in the game.

### Step 8: Enter level 10 and defeat Werdna

Descend to level 10. **Werdna** is a powerful archmage -- he casts high-level
damage spells and is guarded by elite monsters. Strategy:
- **Buff before the fight**: cast protective spells (SOPIC, etc.).
- **Sleep or disable** his guards if possible (KATINO, higher-level sleep
  spells).
- **Focus damage** on Werdna with your best spells (TILTOWAIT, LOKARAID) and
  melee attacks from the Fighters.
- **Keep the Priest healing** every round.
- If the fight goes badly, **Run** and try again.

### Step 9: Take the Amulet and return

When Werdna is defeated, **take the Amulet**. Then return to the surface by
one of these routes:
- **Stairs**: climb back up through all ten levels (long but safe if you have
  cleared them).
- **Elevator**: if you can reach the elevator on level 3, board it to the
  castle level.
- **MALOR**: a high-level priest teleport spell can move the party directly
  to the surface (risky -- a bad teleport can kill the party).

When you reach the Edge of Town with the Amulet, the game declares you the
winner.

---

## 10. Maps

The dungeon is a **20 x 20 grid per level**. The maps below use these symbols:

| Glyph | Meaning |
| --- | --- |
| `#` | Wall |
| `.` | Open floor (walkable) |
| `U` | Stairs up |
| `D` | Stairs down |
| `E` | Elevator |
| `@` | Party start point (level 1 only) |
| `A` | The Amulet (level 10 only) |
| `B` | Blue Ribbon location (level 4) |

**Stairs connect between levels**: the stairs-down on level N lead to the
stairs-up on level N+1 at the same approximate position. The elevator on
level 3 can send the party to any previously visited level.

Each map is oriented with **north at the top** and **west at the left**, as
the game renders it. Rows are numbered 0-19 top to bottom; columns 0-19 left
to right.

### Stair and elevator connections

```
Level 1:  @ (10,1)            D (3,17)
Level 2:  U (3,17)            D (16,3)
Level 3:  U (16,3)            D (3,16)            E (10,10)
Level 4:  U (3,16)            D (16,16)           B (10,3)
Level 5:  U (16,16)           D (3,3)
Level 6:  U (3,3)             D (16,3)
Level 7:  U (16,3)            D (3,16)
Level 8:  U (3,16)            D (16,3)
Level 9:  U (16,3)            D (10,17)
Level 10: U (10,17)                               A (10,10)
```

### Level 1 -- Castle level

Entrance from the Edge of Town. Weak monsters (kobolds, orcs, skeletons).
Grind here until level 2-3.

```
    0123456789[10][12][14][16][18]
 0  ####################
 1  #.........@........#
 2  #..................#
 3  #...####....####...#
 4  #...#..........#...#
 5  #...#..........#...#
 6  #...####....####...#
 7  #..................#
 8  #..................#
 9  #...####....####...#
10  #...#..........#...#
11  #...#..........#...#
12  #...####....####...#
13  #..................#
14  #..................#
15  #...####....####...#
16  #...#..........#...#
17  #..D............#..#
18  #..................#
19  ####################
```

### Level 2 -- Upper maze

First tough encounters. Stairs up to level 1 at (3,17); stairs down to level
3 at (16,3).

```
    0123456789[10][12][14][16][18]
 0  ####################
 1  #..................#
 2  #..................#
 3  #...####....####.D.#
 4  #...#..........#...#
 5  #...#..........#...#
 6  #...####....####...#
 7  #..................#
 8  #..................#
 9  #...####....####...#
10  #...#..........#...#
11  #...#..........#...#
12  #...####....####...#
13  #..................#
14  #..................#
15  #...####....####...#
16  #...#..........#...#
17  #..U............#..#
18  #..................#
19  ####################
```

### Level 3 -- The Elevator level

The critical transition level. The elevator at (10,10) can send the party to
any previously visited level. Stairs up to level 2 at (16,3); stairs down to
level 4 at (3,16).

```
    0123456789[10][12][14][16][18]
 0  ####################
 1  #..................#
 2  #..................#
 3  #...####....####.U.#
 4  #...#..........#...#
 5  #...#..........#...#
 6  #...####....####...#
 7  #..................#
 8  #..................#
 9  #..........E.......#
10  #..........E.......#
11  #..................#
12  #..................#
13  #..................#
14  #...####....####...#
15  #...#..........#...#
16  #..D............#..#
17  #..................#
18  #..................#
19  ####################
```

### Level 4 -- Middle maze

Good treasure. The **Blue Ribbon** at (10,3) is required to access the
deeper levels. Stairs up to level 3 at (3,16); stairs down to level 5 at
(16,16).

```
    0123456789[10][12][14][16][18]
 0  ####################
 1  #..................#
 2  #..................#
 3  #...####....B####..#
 4  #...#..........#...#
 5  #...#..........#...#
 6  #...####....####...#
 7  #..................#
 8  #..................#
 9  #...####....####...#
10  #...#..........#...#
11  #...#..........#...#
12  #...####....####...#
13  #..................#
14  #..................#
15  #...####....####...#
16  #...#..........#.D.#
17  #..U............#..#
18  #..................#
19  ####################
```

### Level 5 -- Lower middle

Strong monsters (trolls, ogres, wights). Stairs up to level 4 at (16,16);
stairs down to level 6 at (3,3).

```
    0123456789[10][12][14][16][18]
 0  ####################
 1  #..................#
 2  #..................#
 3  #.D.####....####...#
 4  #...#..........#...#
 5  #...#..........#...#
 6  #...####....####...#
 7  #..................#
 8  #..................#
 9  #...####....####...#
10  #...#..........#...#
11  #...#..........#...#
12  #...####....####...#
13  #..................#
14  #..................#
15  #...####....####...#
16  #...#..........#.U.#
17  #..................#
18  #..................#
19  ####################
```

### Level 6 -- Deep maze

Dangerous traps (pits, teleporters, darkness). Stairs up to level 5 at
(3,3); stairs down to level 7 at (16,3).

```
    0123456789[10][12][14][16][18]
 0  ####################
 1  #..................#
 2  #..................#
 3  #.U.####....####.D.#
 4  #...#..........#...#
 5  #...#..........#...#
 6  #...####....####...#
 7  #..................#
 8  #..................#
 9  #...####....####...#
10  #...#..........#...#
11  #...#..........#...#
12  #...####....####...#
13  #..................#
14  #..................#
15  #...####....####...#
16  #...#..........#...#
17  #..................#
18  #..................#
19  ####################
```

### Level 7 -- Werdna's domain begins

Powerful monsters (dragons, demons, liches). Only descend with a high-level
party. Stairs up to level 6 at (16,3); stairs down to level 8 at (3,16).

```
    0123456789[10][12][14][16][18]
 0  ####################
 1  #..................#
 2  #..................#
 3  #...####....####.U.#
 4  #...#..........#...#
 5  #...#..........#...#
 6  #...####....####...#
 7  #..................#
 8  #..................#
 9  #...####....####...#
10  #...#..........#...#
11  #...#..........#...#
12  #...####....####...#
13  #..................#
14  #..................#
15  #...####....####...#
16  #..D............#..#
17  #..................#
18  #..................#
19  ####################
```

### Level 8 -- The deeps

Elite encounters. Vampires and werewolves can drain levels. Stairs up to
level 7 at (3,16); stairs down to level 9 at (16,3).

```
    0123456789[10][12][14][16][18]
 0  ####################
 1  #..................#
 2  #..................#
 3  #...####....####.D.#
 4  #...#..........#...#
 5  #...#..........#...#
 6  #...####....####...#
 7  #..................#
 8  #..................#
 9  #...####....####...#
10  #...#..........#...#
11  #...#..........#...#
12  #...####....####...#
13  #..................#
14  #..................#
15  #...####....####...#
16  #..U............#..#
17  #..................#
18  #..................#
19  ####################
```

### Level 9 -- Near the bottom

The most dangerous monsters in the game. Stairs up to level 8 at (16,3);
stairs down to level 10 at (10,17).

```
    0123456789[10][12][14][16][18]
 0  ####################
 1  #..................#
 2  #..................#
 3  #...####....####.U.#
 4  #...#..........#...#
 5  #...#..........#...#
 6  #...####....####...#
 7  #..................#
 8  #..................#
 9  #...####....####...#
10  #...#..........#...#
11  #...#..........#...#
12  #...####....####...#
13  #..................#
14  #..................#
15  #...####....####...#
16  #...#..........#...#
17  #.........D........#
18  #..................#
19  ####################
```

### Level 10 -- Werdna's lair

The final level. **Werdna** and the **Amulet** are at (10,10). Stairs up to
level 9 at (10,17). This is the climax of the game.

```
    0123456789[10][12][14][16][18]
 0  ####################
 1  #..................#
 2  #..................#
 3  #..................#
 4  #..................#
 5  #..................#
 6  #..................#
 7  #..................#
 8  #..................#
 9  #..................#
10  #.........A........#
11  #..................#
12  #..................#
13  #..................#
14  #..................#
15  #..................#
16  #..................#
17  #.........U........#
18  #..................#
19  ####################
```

### Navigation notes

- **DUMAPIC** (mage level 2) detects stairs relative to your position. Cast
  it when you are lost.
- **Darkness squares** (marked as floor on the maps) hide the view until you
  cast a light spell (HALNPCAST or LOMILWA).
- **Pits** on levels 6+ can drop you to the level below and deal damage.
  Search before stepping if you suspect a pit.
- **Teleporters** on levels 6+ move the party to a fixed square on the same
  level. They can be disorienting; map carefully.
- **Chests** appear on certain squares. Have the Thief inspect and disarm
  before opening. A trapped chest can poison, damage, or kill.

---

## 11. Tips and Tricks

### 11.1 Saving

- **Save at the Edge of Town every time you return.** This is the only safe
  save point.
- Keep a **backup copy** of the character roster files. If a character is
  Lost or the party wipes, you can restore from backup.
- The game **saves the party state when you leave the dungeon**. If the party
  wipes in the dungeon, they are all Dead and you must resurrect them at the
  Temple -- expensive and risky.

### 11.2 Equipment

- **Identify all equipment before using it.** Cursed items cannot be removed
  without paying Boltac's to uncurse them, and some cursed items are worse
  than nothing.
- **Bishops can identify items** for free (a valuable skill -- saves the
  identification fee at Boltac's).
- **Buy the best armor and weapons available.** The difference between
  leather armor and plate mail is the difference between living and dying on
  level 3+.
- Keep **spare weapons and armor** in case a cursed item forces you to
  uncurse something.

### 11.3 Combat

- **Sleep is your best early weapon.** KATINO (mage level 1) puts a group of
  monsters to sleep; sleeping monsters are easy to hit and cannot attack.
- **Keep AC as low as possible.** AC is more important than hit points for
  survival in the deep levels.
- **Focus fire** on one monster at a time to reduce the number of incoming
  attacks.
- **Run from fights you cannot win.** There is no penalty for fleeing other
  than the risk of a failed escape.
- **Parry** when a character is low on HP and you need to survive a round.

### 11.4 Magic

- **Priests can heal and resurrect** -- essential for survival. Always keep
  one in the party.
- **Thieves can disarm traps** -- essential for opening chests safely. Always
  keep one in the party.
- **MALOR** (teleport) is an emergency escape, but a bad teleport can kill
  the party. Use it only as a last resort, and save before relying on it.
- **Memorize spells before entering the dungeon.** Camping in the dungeon to
  memorize spells risks encounters.

### 11.5 Leveling and gold

- **Level up at the Review Board** when enough XP is accumulated. It does not
  happen automatically.
- **The inn restores HP and spell charges** to full. Rest there after every
  dungeon trip.
- **Pool gold** between characters with the (P)ool command so one character
  can afford a big purchase.
- **Poisoned characters take damage over time.** Cure poison at the Temple or
  with a priest spell before it kills them.

### 11.6 Death and recovery

- **Dead characters can be resurrected at the Temple** for a price based on
  their level. The resurrection can **fail**, turning the character to
  **Ashes** (Lost). Always save before attempting a resurrection.
- **Lost characters** can sometimes be recovered at the Temple, but it is
  very expensive and very risky. In practice, a Lost character is usually
  gone for good.
- If the **entire party dies** in the dungeon, the game ends. You restart
  from your last save (at the Edge of Town) with the party as it was then.
  Any progress since the last save is lost.

### 11.7 Dungeon exploration

- **Map every level on graph paper.** There is no auto-map. DUMAPIC helps,
  but a hand-drawn map is the only reliable way to navigate.
- **Explore one level fully before descending.** This ensures you know where
  the stairs are and have found the key items (Blue Ribbon on level 4).
- **Use the elevator on level 3** to bypass cleared levels on subsequent
  trips. This saves time and reduces risk.
- **Watch your spell charges.** If your casters are out of spells, it is time
  to return to the surface.

### 11.8 The endgame

- Before entering **level 10**, ensure your party is **level 15+** with the
  best equipment in the game and plenty of healing and damage spells.
- **Werdna** is a powerful spellcaster. Expect him to cast high-level damage
  spells. Protect with SOPIC and similar buffs.
- After defeating Werdna, **take the Amulet** and return to the surface. The
  game declares you the winner when you reach the Edge of Town with it.

---

## 12. Quick reference

### 12.1 Class requirements at a glance

| Class | STR | I.Q. | PIE | VIT | AGI | LCK | Alignment |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Fighter | 11 | -- | -- | -- | -- | -- | Any |
| Mage | -- | 11 | -- | -- | -- | -- | Any |
| Priest | -- | -- | 11 | -- | -- | -- | Any |
| Thief | -- | -- | -- | -- | 11 | -- | Any |
| Bishop | -- | 12 | 12 | -- | -- | -- | Any |
| Samurai | 15 | 11 | -- | 14 | 10 | -- | Any |
| Lord | 15 | 12 | -- | 15 | 12 | -- | Good only |
| Ninja | 15 | 15 | 15 | 15 | 15 | 15 | Evil only |

("--" means no minimum requirement beyond the 3-18 attribute range.)

### 12.2 Experience and leveling

Characters gain experience from killing monsters and opening chests. When
enough XP is accumulated, visit the **Review Board** to level up. Each level
increases:
- **Max HP** (more for Fighters, less for Mages)
- **Spell charges** (for spellcasting classes)
- Occasionally **attributes** (rare)

The XP needed per level increases sharply. The first few levels come quickly;
levels 15+ require enormous XP totals.

### 12.3 Status conditions

| Status | Cause | Cure |
| --- | --- | --- |
| **OK** | Healthy | -- |
| **Asleep** | Sleep spell or monster ability | Wakes after a few rounds |
| **Poisoned** | Poison attack or trap | Cure at Temple or priest spell |
| **Stoned** | Cockatrice or similar | Cure at Temple (expensive) |
| **Dead** | HP reduced to 0 | Resurrect at Temple |
| **Lost** | Failed resurrection or trap | Raise at Temple (very expensive, risky) |
| **Ashes** | Failed raise | Gone for good |

### 12.4 The ten levels at a glance

| Level | Stairs up | Stairs down | Elevator | Key item | Difficulty |
| --- | --- | --- | --- | --- | --- |
| 1 | -- (entrance) | (3,17) | -- | -- | Easy |
| 2 | (3,17) | (16,3) | -- | -- | Easy-Moderate |
| 3 | (16,3) | (3,16) | (10,10) | -- | Moderate |
| 4 | (3,16) | (16,16) | -- | Blue Ribbon (10,3) | Moderate |
| 5 | (16,16) | (3,3) | -- | -- | Moderate-Hard |
| 6 | (3,3) | (16,3) | -- | -- | Hard |
| 7 | (16,3) | (3,16) | -- | -- | Hard |
| 8 | (3,16) | (16,3) | -- | -- | Very Hard |
| 9 | (16,3) | (10,17) | -- | -- | Very Hard |
| 10 | (10,17) | -- | -- | Amulet (10,10) | Extreme |
