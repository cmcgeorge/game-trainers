# Hillsfar — Play and Strategy Guide

*Hillsfar* (SSI / Westwood Associates, 1989) — a Forgotten Realms AD&D adventure. You play **one**
character, not a party. The city of Hillsfar is ruled by the merchant-mage **Maalthiir**, self-styled
First Lord, and policed by his mercenary **Red Plumes**. Inside the walls there are no weapons, no
spellcasting and no levelling up. What you have instead is your wits, your reflexes and a set of lock
picks.

Everything in this guide either comes from the game's own manual, from the shipped walkthrough, or
was read directly out of the program and confirmed against the running game. Mechanics marked
**(verified)** were established by reverse engineering — see `ReverseEngineering.md`.

---

## 1. How you win

There is no single endgame. **Each of the four classes has three missions**, given by that class's
guild or temple master, and completing the third mission finishes that class's story. The game ships
exactly twelve quest files, `Q1.BIN` through `QC.BIN` — four classes × three missions **(verified:
the game builds those names from the template `Q?.BIN` and the substitution string `123456789ABC`)**.

So "winning" means: pick a class, report to its master, and work the three missions through to the
end. Section 9 has the complete route for all four.

The shape of every mission is the same:

1. **Talk to your master** (Temple of Tempus for clerics, Fighter's / Mage's / Rogue's Guild
   otherwise). He gives you a lead.
2. **Follow the lead** — to a pub for gossip, to a shop owner, to the archery range for a score, to
   the arena for a specific opponent, or out of the city to a location on the overland map.
3. **Search or loot** wherever you are sent: mazes and buildings are full of chests, and one of them
   holds the thing you need.
4. **Return to your master.** Repeat until the mission closes and he rewards you.

Two habits matter more than anything else:

* **Press the search key everywhere.** Several mission steps are literally "search outside the
  guild" or "search the building next door to the Stables". Nothing prompts you.
* **Watch the clock.** Most buildings are shut most of the day (§6.3). A step that seems impossible
  is usually a step you are attempting at 4 am.

---

## 2. Starting play — the title menus

`HILLSFAR.BAT` runs `MAIN.EXE`, which asks two questions before anything else:

```
Input graphics mode, 1:CGA 2:EGA/VGA 3:TANDY
Number of disk drives?   1. One drive  2. Two drives  3. 3.5 inch disk  4. Hard Disk
```

Answer **2** and **4** for a hard-disk EGA/VGA install.

You then reach **CAMP OPTIONS**, which is where you return between every trip:

| Option | What it does |
| --- | --- |
| Ride current character to Hillsfar | Leave camp — this is "play" |
| Load a character to ride to Hillsfar | Load a saved `.HIL` character |
| Generate a Hillsfar character | Roll a new one |
| Save your current Hillsfar character | Write the character to `<name>.HIL` |
| Remove a Hillsfar character from disk | Delete a `.HIL` |
| Load a pre-rolled character from disk | Load one of the four shipped `.PRE` characters |
| Transfer a character | Import from *Pool of Radiance* |
| View current character | The character sheet |
| Quit to Dos | Exit |

**Save before every trip.** There is no in-city save, and dying or being captured costs you
everything you were carrying.

Four pre-rolled characters ship with the game — `CLERIC.PRE`, `FIGHTER.PRE`, `MAGICUSE.PRE`,
`THIEF.PRE`. All four are human, all have Strength 18, and they are a perfectly good way to start
without rolling for an hour. **(verified: all four were generated within a 58-second window on
1989-03-30.)**

### 2.1 Creating a character

**Race** — the game's own list, in its internal order **(verified)**: Dwarf, Elf, Gnome, Half-elf,
Halfling, Human. Non-humans can multi-class; humans cannot but have unlimited advancement.

**Class** — the game recognises eleven legal combinations, and it is worth knowing the full list
because the character sheet abbreviates them **(verified — read out of the class-name table, which is
indexed directly by the internal class bitmask)**:

| | | |
| --- | --- | --- |
| `Cleric` | `Fighter` | `Magic-User` |
| `Thief` | `FTR/TH` (Fighter/Thief) | `MU/TH` (Magic-User/Thief) |
| `FTR/MU` | `FTR/MU/TH` | `CL/FTR` |
| `CL/MU` | `CL/FTR/MU` | |

Every combination of Cleric with Thief is illegal. Multi-class characters keep a **separate level per
class** and split experience between them, but gain all the weapon and equipment benefits of each.

**Alignment** — nine values **(verified)**: Lawful/Neutral/Chaotic × Good/Neutral/Evil. The middle
one displays as **"True Neutral"**. Alignment changes how NPCs treat you.

**Ability scores** run 3–19. Reroll freely — nothing is lost. What each stat actually buys you here:

| Stat | Why it matters in *Hillsfar* |
| --- | --- |
| **Dexterity** | The single best stat in the game. It reduces the **aim drift** at the archery range, and it drives the thief skills. |
| **Constitution** | Hit points, and **natural healing: you regain `1 + clamp(Con − 14, 0, 5)` hit points every 24 game hours** — so Con 14 or below heals 1 a day, Con 19 heals 6. **(verified)** |
| **Strength** | Damage, carrying capacity, and forcing locks and doors. Fighters can roll exceptional Strength, shown as `18(nn)`. **(verified: the percentile is a separate byte and is non-zero only for fighters.)** |
| Intelligence / Wisdom / Charisma | Class prerequisites; Charisma affects NPC encounters. |

**Name** — up to 15 characters **(verified)**. The save filename is built from the first eight, so
give two characters clearly different opening letters.

---

## 3. Controls

### 3.1 In the city (the 3-D walking view)

| Key | Action |
| --- | --- |
| **↑** | Move forward |
| **← / →** | Turn left / right |
| **↓** | Turn around 180° |
| **Space** | **Search / examine** — use this constantly |
| **R** | Recall the last clue you were given |
| **P** | Use a healing potion |
| **S** | Toggle sound |
| **← (backspace) or Esc** | Pause |

### 3.2 Riding

| Key | Action |
| --- | --- |
| **→** | Speed up |
| **←** | Slow down |
| **↑** | Jump |
| **↓** | Duck (birds, arrows) |
| **Space** | Fire the Rod of Blasting, if you have one; also "take the unmarked trail" when a `?` appears |

### 3.3 The arena

You fight with a staff. Directions are the joystick/keypad positions; **hold the fire button with a
direction to attack instead of block**.

| Input | Action |
| --- | --- |
| **←** / **→** | Block left / block right |
| **← + fire** / **→ + fire** | Attack left / attack right |
| **↑** | Special block |
| **↓** | Special attack |

### 3.4 Lock picking

| Key | Action |
| --- | --- |
| **Arrows** | Select a pick |
| **Space** | Flip the selected pick over |
| **Enter** | Try it on the current tumbler |
| **F** | Force the lock |
| **Z** | Use a knock ring |
| **E** | Leave the lock — only before you have tried a pick |

### 3.5 Mazes, sewers and buildings

Arrow keys move. That is all — but see §7.5, because the clock is the real opponent.

---

## 4. The camp and the ride

From camp you get a horse and the overland map. Highlight a route with the arrow keys and press
**Space** to ride it. From some points, including camp, only one route is available.

**Unmarked trails.** Occasionally a **`?`** appears where your horse is. Press **Space** to take it.
These are how you reach the three hidden locations — the Rock Quarry, the Dead Dragon and the
Wizard's Lair — and several missions require them.

**The ride itself** is an obstacle course: hay bales, ditches, holes, puddles, fences, bushes and
tree stumps to jump, plus birds and arrows to duck. Misjudge one and you risk losing the horse. If it
bolts you are offered:

| Option | Notes |
| --- | --- |
| Walk on ahead | May get you to your chosen destination |
| Look for a horse | If you find one you carry on |
| Wait for passerby | Someone takes you to the trading post |
| Start walking back | Return to where you set out from |

Any of these leaves you exposed to robbery, so protect the horse. Slow down (**←**) before obstacles
until you know a route; there is no prize for arriving fast.

---

## 5. The overland map

Camp is in the north-east, Hillsfar city sits in the centre-east behind its walls, and the Moonsea
runs along the eastern edge. Solid lines are roads and marked paths; **dashed lines are the unmarked
trails** that only appear as a `?` while riding.

```
     NW ─────────────────────── N ─────────────────────── NE
      │   ▲▲▲ mountains                                    │
      │        ╲                              ☼ CAMP  ─────┤
      │         ╲                            ╱             │
      │      ┌────╴TRADING POST╶────┐       ╱              │
      │      │        │             │      ╱               │
   W ─┤      │        │        ╔════╧═════╧════╗           ├─ E
      │      │        │        ║   HILLSFAR    ║           │   M
      │  HERMIT'S     │        ║  (walled city)║           │   o
      │   HOUSE ──────┤        ╚════╤══════════╝           │   o
      │      ┊        │             │           ⛵ SHIPWRECK│   n
      │      ┊ (secret path)        │                ┊     │   s
      │  ROCK QUARRY  │             │                ┊     │   e
      │               │        ⌂ HUT │          ☠ DEAD     │   a
      │   ⛬ OLD RUINS─┴──── 🌳 BIG TREE          DRAGON    │
      │      ┊                                             │
      │      ┊ (hidden path)                               │
      │  WIZARD'S LAIR                                     │
     SW ─────────────────────── S ─────────────────────── SE
```

| Location | Reached from | Why you go |
| --- | --- | --- |
| **Camp** | — | Start; save, rest, view the character sheet |
| **Hillsfar** | The main road | Everything in §6–§8 |
| **Trading Post** | Road | The Trader tracks people's movements — a recurring cleric and mage lead |
| **Big Tree** | Road | Maze with chests; a body in one of them (cleric M1, fighter M3) |
| **Hermit's House** | Road | Chests: Holy Scriptures, a Poster, a White Liquid, his Diary |
| **Rock Quarry** | **Secret path from Hermit's House** | A dead woman, a Bonnet, a Rusty Old Pick |
| **Hut** | Road | An Old Man with a clue; a thief lead |
| **Old Ruins** | Road | A Gold Pendant, a bottle of Incense, Ariana |
| **Wizard's Lair** | **Hidden path from the Ruins** | Mage M3 chests |
| **Shipwreck** | Road along the coast | Mage M1 and M3 |
| **Dead Dragon** | **Hidden trail from the Shipwreck** | The Squid's remains, a strange Pick |

**Three locations are only reachable by unmarked trail** — Rock Quarry, Wizard's Lair and Dead
Dragon — and each is required by at least one mission. If a walkthrough step says "take the secret
path", ride the parent location repeatedly until the `?` shows.

---

## 6. Inside the city

You arrive in the **north-east corner**, marked by a flashing arrow — that is the **Stables**, and it
is the only way out of the city again.

### 6.1 The screen

The left column is a 3-D view of what is in front of you, with your status beneath it. The right is
the city map. The message window **swaps between the top and bottom half of the screen** depending on
whether you are in the northern or southern part of the city — that is deliberate, not a glitch.

The status panel shows name, class, level, experience, hit points `cur (max)`, gold, the six
abilities, then two icons with counts — **knock rings** and **healing potions** — and the clock:

```
GOLDTEST
CL: Fighter
LV:  12
EX:  1234567
HP:  200  (200)
GP:  999999
Str:18  Int:19
Wis:19  Dex:19
Con:19  Cha:19
 ○  3        ← knock rings
 ⌾  7        ← healing potions
TIME:  3  pm
```

**(verified: the two consumable counters were confirmed by writing 3 and 7 into memory and reading
the icons back off this panel. Each caps at 99.)**

### 6.2 The clock

**One game hour passes every 122 seconds of real time**, driven off the host clock, and the day rolls
over from hour 24 **(verified)**. That is the constraint the whole city runs on: an in-game day is
about 49 real minutes, and if you need a pub you may have to wait for one.

### 6.3 Opening hours — the most important table in the game

| Building | Hours |
| --- | --- |
| Arena | 8 am – 11 pm |
| Archery range | 8 am – 3 pm |
| Bank | 8 am – 3 pm |
| Book store | 8 am – 3 pm |
| **Castle** | **Never open** |
| Cemetery | 12 am – 7 am |
| Temple of Tempus | Always open |
| Stables | Always open |
| Fighter's Guild | Always open |
| **Haunted Mansion** | **Never open** |
| Healer shops | 8 am – 3 pm |
| **Jail** | **Never open** |
| Mages' Guild | Always open |
| Mages' shops | 8 am – 3 pm |
| Mages' Tower | 8 am – 3 pm |
| **Pubs** | **5 pm – 7 am** |
| Sewers | Always open |
| Rogue's Guild | Always open |

Read that table twice. Three consequences run the whole game:

* **Guilds are open only to their own class** — always open to you, always shut to everyone else.
* **The daytime block (8 am – 3 pm) and the pub block (5 pm – 7 am) barely overlap.** Shops, bank,
  bookstore, archery and the Mages' Tower are morning-and-early-afternoon work; pubs are an evening
  and overnight job. Plan a day around one or the other.
* **"Never open" means break in.** The Castle, Haunted Mansion and Jail are all mission targets. The
  Cemetery is a middle-of-the-night job (12 am – 7 am), and the missions that send you there do not
  say so.
* Two mission steps are timed to the hour: the mage's third mission wants the **Rat's Nest between
  8 pm and 10 pm**, and the thief's third wants you searching **outside the Dragon's Lair at 4 pm**.

### 6.4 Locations

Eighteen locations, which is the game's own list **(verified — read out of its internal name table,
spelling and all)**: Jail, Temple of Tempus, `Cemetary`, Rogue's Guild, Mage's Guild, Fighter's
Guild, Stable, Sewer, Archery, Arena, Mages Tower, Haunted Mansion, Pub, Bank, Book store, Magic
shop, Castle, Healer.

The four named pubs are the **Dragon's Lair**, the **Rat's Nest**, the **Hydra's Den** and the
**Bugbear's Cave**. Missions often name one specifically; when a step just says "any pub", any will
do.

---

## 7. What to do in each building

### 7.1 Your guild or temple

The hub of the game. Options **(verified — the guild menu, read out of the program)**:

* **Rest for a while** — the safe way to pass time and heal. It reports how many hours you slept.
* **Replace some picks** / **Buy a set of picks** — a new set is ten picks in a leather pouch;
  repairs are priced per broken pick.
* **Talk to the Master** — mission progress. This is the one you came for.
* **Leave the guild.**

If the master has nothing to say you get *"The guild master is not in right now"* — usually it means
you have missed a step, not that you should wait.

### 7.2 Pubs

Pubs are where the plot moves. The action list is class-dependent; the full set in the program is:

**Everyone:** Listen to gossip · Buy a drink · Buy a meal · Gamble · Buy the house a round · Leave a
tip · Arm wrestle · Carve initials · Guzzle drinks · Brag · Climb walls · Charm the barmaid · Buy her
a drink · Give gold for info · Complain to her · Leave the pub

**Thief:** Pick pockets · Pick the cellar door · Hide in shadows
**Magic-User:** Sleep everyone · Perform illusions · Start a fire
**Cleric:** Give free healings · Bless the barmaid · Donate to the poor · Chant

**Listen to gossip is the single most important pub action.** It is how you learn about the Haunted
Mansion, the sewers, the Healer, the Arena opponents and the Hut. Repeat it — you often need to
listen several times before the right rumour comes up, and some rumours only appear once a mission
step is active. **R** replays the last clue if you missed it.

Drinks are Pink Lemonade, a Mug of beer, a Glass of wine and a Shot of whiskey; meals are the
Cheapest meal, Today's special and a Gourmet meal. **Guzzling drinks is a real risk** — the game will
happily walk you through light-headed, dizzy and "the room is starting to spin" to passing out and
waking up **with all your gold gone**. Eating soaks up some of the booze. If you only want
information, buy one drink and listen.

Two other ways to lose everything here: overstaying after being thrown out lands you in the **Arena**,
and being escorted out costs **half your gold**. Bank your money first (§7.4).

### 7.3 Tanna's Target Range (archery) — 8 am to 3 pm

Pay a fee, optionally practise free, then rent a weapon and take **ten shots** at targets worth
varying amounts.

| Weapon | Notes |
| --- | --- |
| **Sling** | The only weapon a cleric may use |
| **Dagger** | Heaviest — least wind-affected |
| **Darts** | Faster and lighter than daggers |
| **Arrow** | Fastest |
| **Wand** | Mages only |

Two mechanics decide your score:

* **Aim drift** — the crosshair wanders. **Higher Dexterity means less drift.**
* **Wind** — read the **windmill** and lead your shot. **Lighter weapons drift more in the wind**, so
  the dagger is the most forgiving and the arrow the most demanding.

Practice scores nothing and costs nothing. Use it to learn the windage, then compete.

**Your range level is what several mission steps check** — five of them: the fighter's first mission
needs two levels gained, the mage's second needs level three, the mage's third needs level four, the
thief's third needs four levels gained, and the fighter's third needs level five. The internal level counter is **capped at 15**, and each new level awards experience
scaled by your class **(verified)**.

### 7.4 The Bank — 8 am to 3 pm

Deposit gold, withdraw gold, check the balance. **Use it.** Getting thrown out of a pub, robbed on
the road or captured in a maze all cost you the gold you are carrying, and none of them touch the
bank. The one exception: the mage's second mission requires you to physically **carry 500 gold or
more** into the Dragon's Lair, so withdraw before that step.

### 7.5 Mazes, sewers and buildings — the time limit

Buildings, sewers and hedge mazes hold gold, items and mission objects in chests. The rules:

* A **time limit** counts down at the top of the screen.
* Guards and guardians patrol. **Every touch cuts your remaining time** — they do not damage you
  directly.
* When time runs out, **the next guard to touch you captures you**. You lose everything you have
  collected and may be sentenced to a **fight to the death** in the Arena.
* **Caught inside the Castle, you always end up in the Arena.**
* The exit is a **stairway leading down**. Find it before the clock beats you.

So: loot the near chests first, keep moving, and leave on your own terms. Greed in the last few
seconds is how characters die. Watch for hidden traps.

**Secret rooms.** Four buildings have one, and three missions require them. The pattern is the
same each time: go to the **top-left portion of the maze and look for a passage in one of the
left-hand walls** — that works for the Haunted Mansion and the Mages' Tower. The Temple of Tempus
and the Castle also have secret rooms.

### 7.6 Locks, doors and chests

**With picks (thieves, or any class with a hired NPC thief and a set of picks):** the lock-picking
screen shows the tumblers. Pick them **one at a time, left to right**, choosing the pick that matches
the tumbler and flipping it over (**Space**) if needed. Wrong pick or wrong end and you may **break**
it — broken picks stay visible but unusable until repaired at your guild. Jammed tumblers need
repeated attempts and break picks more often. There is a **time limit**, and running out sets off any
trap almost certainly. **E** leaves the lock, but only before you have tried a pick.

**Without picks**, you get a menu:

```
Leave! Don't try to open this lock
Use physical strength to force it
Pick the lock with a small object
Use a knock ring to open the lock
Use the magical Chime of Opening
```

* **Force it** — Strength. Sets off traps.
* **Small object** — a stick or a scrap of metal. Sets off traps.
* **Knock ring** — opens one lock, then is consumed. Available to every class, bought from the Magic
  shop. Also **Z** on the lock-picking screen.
* **Chime of Opening** — a found magic item that drops **all** the tumblers at once. Save it for a
  lock you cannot afford to fail.

**Hire a rogue whenever one offers.** An NPC thief approaches with *"I am an expert locksmith. I will
assist you in picking locks on your adventure for half of the gold we find."* Half the gold is a
bargain for reliable locks — take it. (He can vanish mid-dungeon: *"Your hired thief seems to have
disappeared!"*)

**Getting into the Jail** — the manual's own hint: pick the first few tumblers normally, then
**force the last tumbler with `F`**.

### 7.7 Shops

* **Magic shop** — buy and sell **knock rings**; talk to the mage. Two missions require visiting it
  **after closing time** (mage M3, thief M3) — the point is to break in.
* **Healer** — buy and sell **healing potions**, or pay for a spell: *"For 500 gold pieces, a 'cure
  critical wounds' spell will be cast upon you."* Talk to the owner; the fighter's third mission
  needs the Healer in the **south** of the city, the thief's second the one in the **south-west**.
* **Book store** — read a book; talk to the owner. A mage M2 step.
* **Watch out for Wak Rathar**, a mage who offers to show you a magic trick. Sometimes you get
  *"there is a pile of gold in front of you!"* — and sometimes *"you find yourself in the Arena."*

---

## 8. The Arena

Sooner or later you end up here — for money, for fame, because a mission needs a specific opponent
beaten, or as a sentence. Anyone may compete, though it suits fighters best. Most bouts run until one
fighter is knocked senseless; **for serious crimes it is to the death.** You fight with a staff.

**Every opponent has a physical tell, and that is the whole fight.** The game teaches four of them
through pub gossip — listen in pubs before a mission sends you to a named opponent. The eight
opponents, from the game's own roster:

| Opponent | Tell (as the game's own gossip gives it) |
| --- | --- |
| **Lefty the left-handed Orc** | Drops his guard just before attacking. **Whichever end of his staff is higher is the end coming at you** — left end up, counter with a quick left. Fights in a pattern of **three left blows then a right**. |
| **The Red Minotaur** | **Twitches his head before each attack — twice when he means to ram you.** Head moves left → he attacks with his right, so hit him with a right to the head. |
| **Ssslader, lizard man of the Vast Swamp** | **Sticks his tongue out in the direction he will attack** — tongue left, left jab. Uses a right-left combo; **tongue out twice means a tail attack**, and hitting him right after the tail leaves him dizzy and open to a couple of free blows. |
| **Morin the knight** | **The feathers on his helm move before he attacks, and the higher end of his staff is the end that lands.** Attacks left for a while, then right for a while, then catches you with **a quick low blow**. |
| **Ottis the Orc** (Thunder Peaks) | Learn the tell by watching — see below. |
| **Taurus the Great**, a mighty minotaur | Required by fighter M3 and mage M3. |
| **Whiplash** | "Watch out for this lizard's tail." |
| **Keller the Dark Knight** | The toughest of the roster. |

For the four the game does not spell out, the method is the same one the gossip describes: **block,
do not attack, for the first several exchanges** and watch for the movement that precedes each blow —
staff end, head twitch, tongue, feathers. Then counter on the opposite side to whichever side he
telegraphs, and learn his repeating pattern. Attacking blind against Taurus or Keller loses.

---

## 9. Complete mission walkthroughs

Steps are in order. "Master" means your own class's guild master (Temple of Tempus for clerics).
Where a step needs a specific hour or a secret path, it is flagged.

### 9.1 Cleric — Temple of Tempus

**Mission One**
1. Find the Temple of Tempus, talk to the Master.
2. Ride to the **Trading Post**, talk to the Trader.
3. Ride to the **Big Tree**; search the maze, open chests — you find a **dead body**.
4. Return to the Temple, report to the Master.
5. Back to the **Trading Post**, talk to the Trader.
6. Ride to the **Hermit's House**; loot until you find the **Holy Scriptures**.
7. Return to the Temple. Reward.

**Mission Two**
1. Rest, then talk to the Master.
2. Enter a **sewer**; a chest holds a **small Thief** — **show pity, do not report him** — for your
   clue.
3. Go to the **Dragon's Lair**, buy a meal and listen to gossip about the **Haunted Mansion**.
4. Break into the **Haunted Mansion**; search chests for a **note**.
5. Ride to the **Hut**; chests yield an **Old Man** with the next clue.
6. Ride to the **Old Ruins**; find a bottle of **Incense**.
7. Return to the Temple. Reward.

**Mission Three**
1. Rest, then **search directly outside the Temple**.
2. Re-enter, talk to the Master, **donate money**.
3. **Mages' Tower** (8 am – 3 pm); find a **Wand with blue runes**.
4. Take the Wand to the Master.
5. **Search outside the Temple** again.
6. **Rat's Nest, between 8 pm and 10 pm** — listen until you meet a **Woman**.
7. Return to the pub, listen again.
8. Temple: talk to the Master.
9. Pub again: listen again.
10. Temple: Master again.
11. **Rock Quarry** — **secret path from the Hermit's House**. Chests: a **dead woman** and a hint.
12. **Haunted Mansion**: a chest holds the missing **Ring**.
13. Return to the Master. Mission complete.

### 9.2 Fighter — Fighter's Guild

**Mission One**
1. Fighter's Guild, talk to the Master.
2. **Archery range**: gain **two levels**.
3. Guild: Master.
4. **Arena**: defeat the **Red Minotaur**.
5. Guild: Master.
6. **Cemetery** (12 am – 7 am): open chests for a clue.
7. Break into the **Jail** — pick the first tumblers, **force the last with `F`** — and find the
   **Documents**.
8. Take them to the Master. Reward.

**Mission Two**
1. Talk to the Master.
2. **Search around the outside of the Great Castle.**
3. **Rat's Nest**: gossip about the **sewers**.
4. Descend into a **sewer**; a chest holds a **Beggar** with an Arena hint.
5. **Arena**: defeat the **Orc**.
6. **Hermit's House**: chests yield a **Poster**.
7. **Rat's Nest**: **buy the Barmaid a beer**.
8. **Haunted Mansion**: search the **top-left** of the maze; a **left-hand wall** hides a **secret
   passage**. In the secret room find **Jared** and help him.
9. Guild: Master. Mission complete.

**Mission Three**
1. Talk to the Master.
2. **Search the building next door to the Stables** — you meet **Hector**.
3. **Cemetery**: chests hold a **Map**.
4. **Big Tree**: chests hold a **dead body**.
5. **Archery range**: reach **level five**.
6. Guild: Master.
7. **Arena**: defeat **Taurus**.
8. **Any Inn**: gossip about the **Healer**.
9. **Healer in the SOUTH** of the city: talk to the owner.
10. **Rock Quarry** (secret path from the Hermit's House): find a **Bonnet**.
11. Guild: Master.
12. **Any pub**: listen for gossip about the **Guild**.
13. **Confront the Master** at the Guild.
14. **Old Ruins**: search until you find **Ariana**.
15. Return to the Master. Mission complete.

### 9.3 Magic-User — Mage's Guild

**Mission One**
1. Mage's Guild, talk to the Master.
2. Ride to the **Trading Post**.
3. Ride to the **Shipwreck**, then take the **hidden trail to the Dead Dragon**; find the **Squid's
   remains**.
4. Return to the city, **Magic shop**: talk to the owner.
5. Return to the **Dead Dragon** for a hint about the **Hydra's Den**.
6. **Hydra's Den**: gossip about the **Trader**.
7. **Trading Post**: talk to the Trader.
8. **Magic shop**: talk to the owner again.
9. Guild: Master. Mission complete.

**Mission Two**
1. Talk to the Master.
2. **Trading Post**: talk to the Trader.
3. **Book store** (8 am – 3 pm): talk to the owner.
4. **Magic shop**: a hint about the archery range.
5. **Archery range**: reach **level three**.
6. **Hydra's Den**: have a beer, gossip about the **Ruins**.
7. **Old Ruins**: unlock chests until you find a **Gold Pendant**.
8. **Hydra's Den**: **charm the Barmaid**.
9. **Carry 500+ gold** to the **Dragon's Lair** and **charm the Barmaid** there.
10. Guild: Master. Mission complete.

**Mission Three**
1. Rest, then talk to the Master.
2. Visit the **Magic shop when it is closed** and find the **Red Liquid**.
3. Guild: Master.
4. **Archery range**: reach **level four**.
5. **Old Ruins**, then the **hidden path to the Wizard's Lair**; open every chest and take everything.
6. **Mages' Tower**: **top-left** of the maze, **left-hand wall** secret passage; open the chests in
   the secret room.
7. **Hermit's House**: chests hold the next clue.
8. **Arena**: beat **Taurus**.
9. **Cemetery** (12 am – 7 am): chests hold a clue.
10. **Shipwreck**: chests hold a clue.
11. **Haunted Mansion**: find the **secret room** and open its chests.
12. Guild: Master. Mission complete.

### 9.4 Thief — Rogue's Guild

**Mission One**
1. Ride to Hillsfar, find the **Rogue's Guild**, talk to the Master.
2. **Magic shop**: talk to the owner.
3. Descend into a **sewer**; chests hold a **Fungus**.
4. Guild: Master.
5. **Hermit's House**: chests hold a **White Liquid**.
6. Guild: Master. Mission complete.

**Mission Two**
1. Wake and talk to the Master.
2. **Any pub**: gossip about a lost **Amulet**.
3. **Sewers**: search chests for a **Torn Note**.
4. **Dragon's Lair**: gossip about the **Hut**.
5. **Hut**: obtain a hint.
6. **Any pub**: listen again.
7. **North-west** part of the city: enter the **sewer** there; chests hold a **Dead Thief**.
8. Guild: Master.
9. **Temple of Tempus**: find the **secret room**, unlock all its chests.
10. **Healer in the SOUTH-WEST**: talk to the owner.
11. **Hermit's House**: uncover his **Diary**.
12. **Rock Quarry** by the **secret route**: find a **Rusty Old Pick**.
13. **The Castle**: enter its **secret room**, find the **Amulet** in a chest. *(Being caught in the
    Castle always sends you to the Arena — go in with time to spare.)*
14. Guild: Master. Mission complete.

**Mission Three**
1. Talk to the Master.
2. **Magic shop while closed**: uncover a **strange Pick**.
3. **Any pub**: gossip about the **Trading Post**.
4. **Trading Post**: talk to the Trader.
5. **Any pub**: gossip about the **Orc** in the Arena.
6. **Arena**: beat the **Orc**.
7. **Bugbear's Cave**: hang around and listen.
8. **Archery range**: improve by at least **four levels**.
9. **Search the area outside the Guild.**
10. **Mages' Tower** secret room: recover the **Book of Arcane Lore**.
11. **At 4 pm, search outside the Dragon's Lair.**
12. Guild: Master.
13. **Any pub**: listen to the latest gossip.
14. **Shipwreck**, then the **hidden trail to the Dead Dragon**: chests hold another **strange Pick**.
15. **Search outside the Dragon's Lair** again.
16. Guild: Master. Mission complete.

---

## 10. Twenty things worth knowing

1. **Save at camp before every ride.** There is no save inside the city.
2. **Bank your gold** before pubs, mazes and rides. Only carried gold is at risk.
3. **Press Space to search constantly** — outside guilds, next to the Stables, outside the Dragon's
   Lair. Several mission steps are nothing but a search in the right place.
4. **`R` recalls the last clue.** Use it rather than guessing.
5. **Pubs open at 5 pm, shops shut at 3 pm.** Split your day.
6. **The Cemetery is 12 am – 7 am only.** Missions that send you there never mention it.
7. **"Never open" means break in** — Castle, Haunted Mansion, Jail.
8. **One game hour = 122 real seconds** (verified). Resting at your guild is the efficient way to
   pass time.
9. **Natural healing is `1 + clamp(Con − 14, 0, 5)` HP per game day** (verified). A high-Con
   character heals meaningfully by resting; a low-Con one should buy potions.
10. **Buy healing potions when you can afford them**, and remember **`P`** uses one. The 500-gold
    *cure critical wounds* spell is the emergency option.
11. **Knock rings are the universal lock solution** and every class can buy them. Carry several
    before a mission that needs a specific chest; **`Z`** uses one.
12. **Save the Chime of Opening** for a lock you cannot afford to fail.
13. **Hire the NPC rogue** every time he offers. Half of found gold is cheap.
14. **Dexterity is the best stat** — less aim drift at the range, better thief skills.
15. **At the range, practise first**, read the windmill, and prefer the **dagger** in high wind
    (heaviest, so least deflected).
16. **In the arena, block and watch before you attack.** Every opponent telegraphs; §8 has four
    tells verbatim from the game.
17. **In mazes, leave early.** Guard contact drains your time, not your hit points, and running out
    means losing everything you picked up.
18. **Secret rooms are in the top-left of the maze, through a left-hand wall.**
19. **Do not guzzle drinks.** Passing out costs all your gold; eating a meal offsets some of it.
20. **Slow down while riding.** Losing the horse risks robbery, and there is no reward for speed.

---

## 11. Files in this directory

| File | What it is |
| --- | --- |
| `MAIN.EXE` | The game — double-packed, see `ReverseEngineering.md` |
| `HILLSFAR.BAT` | Launcher |
| `INSTALLH.BAT` | Original floppy installer |
| `*.CMP` | Compressed graphics (screens, mazes, sprites, the city map) |
| `*.ANM` | Class animations (cleric, fighter, thief, magic-user) |
| `Q1.BIN` … `QC.BIN` | The twelve quest scripts — 4 classes × 3 missions |
| `*.PRE` | The four shipped pre-rolled characters, 188 bytes each |
| `*.HIL` | Saved characters, 188 bytes each — a raw dump of the record, no checksum |
| `PACKWALL.OUT` | City wall / building geometry |
| `SYMBOLS.CMP` | The code-wheel runes (Espruar and Dethek) |
| `hillsfar.txt` | Typed-in manual |
| `hillsfar.sol` | The walkthrough §9 is based on |
