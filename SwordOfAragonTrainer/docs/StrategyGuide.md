# Sword of Aragon — Play & Strategy Guide

*Strategic Simulations, Inc. (SSI), 1989 — IBM PC / MS-DOS v1.0.*

You are the heir of the Duke of Aladda, murdered by orcs in **April 871 QJ**. You inherit one small city of
1,500 people, an army of 100 soldiers in three or four units, 6,500 gold pieces and your father's ambition:
reunite Aragon and take the imperial throne in Tetrada. (The rule book says 80 soldiers; the shipped
starting-army tables give every class exactly 100 — see [RE.md](RE.md) §5.4.)

The game runs on two levels — a **World Map** where you govern, develop and march, and a **Tactical Battle** hex
map where fights are resolved. You win by conquest.

Numbers in this guide are taken from the shipped executables and save files rather than from the rule book
wherever the two disagree; see [RE.md](RE.md) for how each figure was established.

---

## 1. Getting started

### 1.1 Launching

From the install directory (DOSBox or a real DOS box):

```
SWORD
```

Optional single-letter switches may be appended in any combination:

| Switch | Effect |
|---|---|
| `C` | Force CGA |
| `H` | Force Hercules |
| `T` | Force Tandy 16-colour |
| `E` | Force EGA/VGA |
| `V` | Sound **off** |

e.g. `SWORD VC` = CGA, silent. The program auto-detects video otherwise. A graphics adapter is mandatory —
`Sword of Aragon REQUIRES GRAPHICS!!!!` is the failure message. Under CGA you are additionally asked for
`Mono / Color / RGB` monitor type. A Microsoft- or Logitech-compatible mouse is optional: **left button = ENTER,
right button = ESCAPE**.

### 1.2 Setup screen

| Step | Action |
|---|---|
| 1 | Choose the disk layout: `1-floppy`, `2-floppy`, `3-hard`, and the Program / Data / Save drives. |
| 2 | Press `Z` for **Diff**, then `E` (Easy), `A` (Average — default) or `X` (Expert). This sets **monster** difficulty. |
| 3 | `ENTER`, then `Y` to confirm "Everything Okay?". |
| 4 | Answer the copy-protection question (§1.3). |
| 5 | `N` = New Game, `O` = Old Game, `D` = Demo, `Q` = Quit to DOS. |

### 1.3 Copy protection — the answers

The game shows a city crest and asks you to name it from the poster, then to type the **first word** of one of
that city's four Notebook summary lines. The prompt names the field: `First word of: LOCATION` (or `RESOURCES`,
`ECONOMY`, `RULER`).

The complete answer key, extracted from `SWORD.EXE`:

| City | LOCATION | RESOURCES | ECONOMY | RULER |
|---|---|---|---|---|
| Aladda | NORTHWEST | LUMBER | FARMING | YOU |
| Marinia | NORTHWEST | RIVER | TRAPPING | GARDWELL |
| Brocada | NORTH | GALATION | FISHING | PETROV |
| Sur Nova | FOOTHILLS | FOREST | LOGGING | UNKNOWN |
| Paritan | NORTH | HARBOR | SMUGGLING | PITLAG |
| Nuralia | NORTH | RICH | AGRICULTURE | WILFREED |
| Tentula | SOUTHEAST | LAKE | FISHING | TANTALA |
| Zarnix | JUSTINID | MINERALS | UNKNOWN | GNARDIX |
| Lucedia | SOUTHEAST | GOOD | FARMING | COUNCIL |
| Pudawala | EAST | DALATION | FISHING | EL-IKHOM |
| Sothold | NORTHEAST | EXCELLENT | FARMING | STRUMBERG |
| Estallah | NORTHEAST | DALATION | COMMERCE | LANDRATOZ |
| Tetrada | NORTHEASTERN | BORDER | COMMERCE | LUCINIAN |

**No poster? You do not need it.** You get at least one retry (`ERROR: wrong word--try again` before
`--too bad!`), and each field has only a handful of possible answers. Work down the column the prompt named:

* **LOCATION** — NORTHWEST · NORTH · FOOTHILLS · SOUTHEAST · JUSTINID · EAST · NORTHEAST · NORTHEASTERN
* **RESOURCES** — LUMBER · RIVER · GALATION · FOREST · HARBOR · RICH · LAKE · MINERALS · GOOD · DALATION ·
  EXCELLENT · BORDER
* **ECONOMY** — FARMING · TRAPPING · FISHING · LOGGING · SMUGGLING · AGRICULTURE · UNKNOWN · COMMERCE
* **RULER** — YOU · GARDWELL · PETROV · UNKNOWN · PITLAG · WILFREED · TANTALA · GNARDIX · COUNCIL · EL-IKHOM ·
  STRUMBERG · LANDRATOZ · LUCINIAN

The seven wilderness regions (Tranavan, Gernok, Xafanta, Khalikha, Char, Medeval, Dersh) are never asked about.

### 1.4 New game

Pick a class with its initial letter — `W`arrior, `K`night, `R`anger, `P`riest, `M`age — name your character
(≤ 16 characters), then answer **Yes** to "Do you want the standard units?" until you know the game well.

---

## 2. Controls

Almost every prompt is answered by typing the **first letter** of the option shown at the bottom of the screen.
`ENTER`, `ESCAPE` or `SPACE` leaves a menu; `ESCAPE` aborts a move or a purchase.

### 2.1 Everywhere

| Key | Action |
|---|---|
| Arrow keys / numeric keypad (NumLock on) | Move the cursor |
| `+` / `-` | Faster / slower message display |
| `V` | Toggle sound |
| `ENTER` / `ESC` / `SPACE` | Leave the current menu; `ENTER` also pages long lists |
| Left / right mouse button | `ENTER` / `ESCAPE` |

### 2.2 World Map / Main Menu

| Key | Option | Available |
|---|---|---|
| `N` | **Next** — end the month | always |
| `S` | **Show** — list every unit's level, move, hits, AC, kit | always |
| `M` or `SPACE` | **Move** units | always |
| `I` | **Info** → `C` city list, `D` Chronicle of Deeds | always |
| `Q` | **Quit** (offers a save first) | always |
| `C` | **City** status | cursor on a friendly city |
| `U` | **Unit** menu | cursor on a friendly city |
| `C` | **Camp** | cursor **not** on a city |

The status line names what the cursor is over: `Player City:`, `Vassal City:`, `Ally City:`, `Oppon City:`,
`Under Siege:`, `Defeated City:`, `Undeveloped Hex`, `Unexplored Hex`.

Selecting units to move accepts `1,3-5,7` style lists, `A` for all, or `-3` for "the first three".

### 2.3 City Status screen (`C`)

| Key | Option |
|---|---|
| `D` | **Develop** → `A`gricult, `L`umber, `M`ining, `X` Manufac, `C`ommerce, `S`tructure, `F`ortific |
| `C` | **Conscript** — draft peasants into the recruit pool |
| `T` | **Tax** — set 0–80 % |

The screen shows, per city: Population, Morale, Loyalty, Health, Tax, Store, Trade, Recruit, Income — each with
"total" and "changed since last month". The bottom block is global: **Movement** (extra points to enter a hex
this month), **Attrition** (% of men lost per hex entered), **Wealth**, **Score (of 500)**, **Income**, **Maint**.

### 2.4 Unit menu (`U`)

| Key | Option |
|---|---|
| `M` | **Make** a new unit from recruits |
| `H` | **Hire** a character (`W`/`K`/`R`/`P`/`M`) |
| `R` | **Reinforce** an existing unit with recruits |
| `D` | **Decommission** — return men to the pool and sell their kit |
| `E` | **Equip** — buy/sell armour and weapons |
| `T` | **Train** — buy experience |
| `S` | **Show** — list units |
| `N` | **Name** — rename a unit |

Hard limits enforced by the game: **60 units**, **20 characters**, and `You need a level to Hire more.` — the
number of characters you may hold is gated by your own level. `Unit cannot exceed Commander Level.` caps
training. A trained unit can do nothing else that month, and cannot then be reinforced or re-equipped.

### 2.5 Tactical Battle

| Key | Option |
|---|---|
| `M` or `SPACE` | **Move** (then pick units, then a sub-option) |
| `H` | **Hex** — terrain, foliage, features, defensive bonuses vs Hand/Missile, elevation |
| `L` | **List** — army comparison: number, hits, % effective, gold, victory points, Bless/Prayer bonuses |
| `N` | **Next** turn |
| `Q` | **Quit** the battle (only from turn 7; shows the victory level) |
| `A` | **AutoMV** — hand your turn to the computer: `F`anatic / `A`ttack / `M`oderate / `D`efense |
| `+`/`-`/`V` | Speed and sound |

Move sub-options (availability depends on unit type and movement left):

| Key | Option | Notes |
|---|---|---|
| `S` | **Supply** | Buy missile/spell ammunition mid-battle |
| `A` | **Attack** | Missile fire — the **default**: pick units, move cursor to target, `ENTER`. Hits every unit in the hex. Never fires at Over Long range |
| `C` | **Cast** | Rangers, Priests, Mages only |
| `N` | **Normal** | Follow the cursor; `ESC` backs up one hex |
| `F` | **Force** | +50 % movement this move only; drains Stamina and Morale |
| `E` | **Entrench** | Needs ≥ half normal movement; protects vs Hand and Missile |

Range bands reported by the game: `Short`, `Medium`, `Long`, `Over Long` (no fire). Results include
`Dispersed.` and `Eliminated!`.

---

## 3. The world

### 3.1 Map

The world is a **24 × 24 hex grid**; x runs west → east, y runs north → south. These positions are the actual
values stored in the save files.

```
       0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15 16 17 18 19 20 21 22 23
    +------------------------------------------------------------------------+
  0 | ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ |   Galation Sea
  1 | ~~ ~~ ~~ ~~ ~~  . BR  .  .  .  .  .  .  .  .  .  .  .  . ~~ ~~ ~~ ~~ ~~ |
  2 | ~~ ~~  .  .  .  .  .  .  .  . PA  .  .  .  . NU  .  .  .  .  . ~~ ~~ ~~ |
  3 | ~~  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . ~~ ~~ |
  4 | ~~ MA  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  .  . TT ~~ ~~ |   <- Tetrada: the imperial throne
  5 | ~~  .  .  .  .  .  .  .  .  .  .  .  ^  ^  ^  ^  .  .  .  .  .  . ~~ ~~ |
  6 | ~~  .  .  .  .  .  .  .  .  .  .  .  ^  ^  ^  ^  .  .  .  .  .  . ~~ ~~ |
  7 | ~~  .  .  .  .  . AL  .  .  # TR  #  ^  ^  ^  ^  .  .  .  .  .  . ~~ ~~ |   <- you start here (Aladda)
  8 | ~~  .  .  .  .  .  .  .  .  #  #  ^  ^  ^  ^ GE  .  .  .  .  . ES ~~ ~~ |
  9 | ~~  .  .  .  .  .  .  .  .  .  ^  ^  ^  ^  ^  ^  .  .  .  .  .  . ~~ ~~ |
 10 | ~~  .  .  .  .  .  .  .  .  .  ^  ^  ^  ^  ^  ^  .  .  .  .  .  . ~~ ~~ |
 11 | ~~  .  .  .  .  .  .  .  .  .  ^  ^  ^  ^  ^  ^  .  .  .  . SO  . ~~ ~~ |
 12 | ~~  .  .  . SN  .  .  .  .  .  ^  ^  ^  ^  ^  ^  .  .  .  .  .  . ~~ ~~ |
 13 | ~~  .  .  .  .  .  .  .  .  .  ^  ^  ^  ^  ^ ME  #  .  .  .  .  . ~~ ~~ |
 14 | ~~  .  .  .  .  .  .  .  .  .  ^  ^  ^  ^  ^  ^  #  .  .  .  .  . ~~ ~~ |
 15 | ~~  .  .  .  .  .  .  .  .  . XA  ^  ^  ^  ^  ^  .  .  .  .  .  . ~~ ~~ |
 16 | ~~  .  .  .  .  .  .  .  .  .  ^  ^  ^  ^  ^  ^  .  .  .  .  . PU ~~ ~~ |
 17 | ~~  .  .  .  .  .  .  .  .  .  ^  ^  ^  ^  ^  ^  .  .  .  .  .  . ~~ ~~ |
 18 | ~~  .  .  .  .  .  .  .  .  .  ^  ^  ^ ZA  ^  ^  .  .  .  .  .  . ~~ ~~ |   <- Zarnix holds the Justinid Pass
 19 | ~~  .  .  .  .  .  .  .  .  .  ^  ^  ^  ^  ^  ^  .  .  .  .  .  . ~~ ~~ |
 20 | ~~  .  .  .  .  .  .  .  .  .  ^  ^  ^  ^  ^  ^  .  .  .  . LU  . ~~ ~~ |
 21 | ~~  .  .  .  .  . TE  .  .  .  ^  ^  ^  ^  ^ DE  .  .  .  .  .  . ~~ ~~ |
 22 | ~~  .  .  .  .  .  .  .  .  .  . CH  ^  ^  ^  ^  .  .  .  .  . ~~ ~~ ~~ |
 23 | ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ ~~ |   Great Blue Lake
    +------------------------------------------------------------------------+
```

The 19 city hexes are exact — they are generated from the coordinates in the save files and machine-checked
against them. The `^` mountain spine, `#` forest and `~` water are **schematic**: drawn from the Notebook's
geography, not from the map file (whose per-hex layout is not decoded). Treat the terrain as orientation, not as
movement costs, and use the gazetteer below rather than the map when a coordinate has to be exact.

Reading the terrain:

* The **Luftgar Mountains** run north–south down the middle and split Aragon into **Westrealm** (Aladda,
  Brocada, Paritan, Nuralia, Tentula, Sur Nova, Marinia) and **Eastrealm** (Tetrada, Estallah, Sothold,
  Pudawala, Lucedia).
* The **only** practical crossing is the **Justinid Pass**, held by the orc fortress of **Zarnix** (13,18).
  Taking Zarnix is the strategic hinge of the whole game.
* The **Garroth River** runs the length of the western edge and **cannot be crossed** — the map's west wall.
* The **Galation Sea** is north, the **Dalation Ocean** east, the **Great Blue Lake** south.

### 3.2 Gazetteer

Values are the game's own new-game figures.

| City | Pos | Pop | Ruler | Morale | Loyal | Health | Gold | Character |
|---|---|---|---|---|---|---|---|---|
| **Aladda** | 6,7 | 1,500 | **You** | 75 | 52 | 85 | 150 | Your capital. Lumber, minerals, rich soil. |
| Marinia | 1,4 | 1,200 | Gardwell, Duke | 50 | 30 | 30 | 315 | Poor swampland; sick old ruler; predatory army. Easy first conquest. |
| Brocada | 6,1 | 2,600 | Petrov, General | 50 | 25 | 80 | 7,150 | North coast militia — trains weekly, fights badly. Rich purse. |
| Sur Nova | 4,12 | 3,400 | *none* | 35 | 25 | 60 | 350 | **No army at all.** Hilltop trade route, good resources, monster-plagued. |
| Paritan | 10,2 | 4,450 | Pitlag, Lord | 55 | 50 | 45 | 5,250 | Pirates; the best-run human army in the west. Expansionist — may hit you first. |
| Nuralia | 15,2 | 3,250 | Wilfreed, Duke | 40 | **5** | 60 | 1,200 | Professional army, badly led, loyalty 5 — ripe for vassalage. |
| Tranavan | 10,7 | 150 | Trinangel, Queen | 10 | 25 | 100 | 500 | Elven forest east of you. Fortification 8. |
| Gernok | 15,8 | 750 | Grimlock | 125 | 110 | 20 | 150 | Goblin homeland; source of the northern raids. |
| Xafanta | 10,15 | 850 | Heben Stenthumble | 20 | 10 | 80 | 7,500 | Dwarves of the Lastrul Plateau. Mining 4/20. Wealthy. |
| Khalikha | — | 1,200 | *unknown* | 50 | 10 | 70 | 100 | Steppe nomads — horse archers. No city hex. |
| Tentula | 6,21 | 5,700 | Tantala, Baron | **10** | 25 | 20 | 1,240 | Idle, unhappy, undefended-ish. Big population. |
| Char | 11,22 | 1,250 | *unknown* | 100 | 60 | 40 | 315 | Giants, Titans, Trolls. |
| **Zarnix** | 13,18 | 1,850 | Gnardix | 125 | 125 | 25 | 250 | Orc fortress in the Justinid Pass. **The gate to the east.** |
| Medeval | 15,13 | 750 | *unknown* | 70 | 25 | 80 | 750 | Eastern elves, hostile to all men. Structure 12 / Fort 10. |
| Dersh | 15,21 | 500 | *unknown* | 100 | 70 | 30 | 755 | Titans and Trolls. |
| Lucedia | 20,20 | 7,500 | Council of the Wise and Strong | 25 | 10 | 50 | 7,500 | Priest–knight theocracy; the two factions hate each other. |
| Pudawala | 21,16 | 9,800 | El-Ikhom, Pasha | 25 | 10 | 50 | 12,500 | Free state, resource-rich, fiercely independent. |
| Sothold | 20,11 | 16,500 | Strumberg, Baron | 100 | 30 | 40 | 10,500 | Strong disciplined army — your father served here. |
| Estallah | 21,8 | 12,500 | Landratoz, Earl | 50 | 15 | 70 | 7,500 | Corrupt; mercenary army, well led. Commerce 25/35. |
| **Tetrada** | 21,4 | 31,500 | **Lucinian III** | 25 | 25 | 100 | 15,420 | The imperial throne. Commerce 40/75, Manufacture 25/40, Fort 10. **The win condition.** |

---

## 4. How to win

**Conquest is the only route.** Specifically:

1. Take cities. A conquest needs at least a **Decisive** victory in the tactical battle; anything less leaves the
   city standing.
2. Recover the **three symbols of imperial authority** — the **Amulet of Aladda**, the **Crown of the West** and
   the **Scepter of the East**. The Notebook says the Crown and Scepter were lost when Tetrada fell in 531 QJ, and
   that Justinid XVI sent the Amulet away before Brethon could reach it. They are found through conquest and
   exploration.
3. Take **Tetrada (21,4)** and sit on the throne. Lucinian III and his heir Lucinian IV are the final opposition;
   watch also for Prince Malthorn, Lucinian IV's demented brother.

Your **score is out of 500** and is shown on the City Status screen. Points come from conquests and from victory
margins in battle — the victory ladder is `Marginal → Decisive → Conclusive → *Total*`, mirrored for defeats.

You can also **lose**: the endgame text includes being *deposed by popular demand (because of terrible luck in
battle) — all levels were lost*, dying of wounds, dying of old age (the "Probean Pox"), and assassination. Bad
luck and inactivity both kill.

---

## 5. Playing the World Map

### 5.1 The monthly cycle

`Next` (`N`) ends the month and, in order: runs events, updates production/population/morale/loyalty/health,
pays you taxes, deducts maintenance, restores unit movement, and applies a level to any unit set to **Train**.

Check the City Status screen **every month** before moving: the global block tells you this month's extra
movement cost per hex and this month's attrition percentage.

### 5.2 Winter kills armies

December, January and February impose **attrition** — a percentage of a unit's men lost *per hex entered* when
not in a friendly city. Bad weather can trigger it in other months too.

* Do not campaign in winter. Sit in cities, develop, train.
* If you are caught in the open, **Camp** (`C` off-city, units need ~half their movement left). Camping shields
  you from attrition but the units move more slowly the following month.

### 5.3 Economy

Seven investment categories per city. Five earn money — **Agriculture, Lumber, Mining, Manufacture, Commerce** —
and two do not:

* **Structure** stores agricultural surplus (and buffers famine).
* **Fortification** raises defence and *reduces the chance of humanoid attack*. Cheap insurance on a frontier city.

Each category has `Devel` (what you have built), `Resrc` (the city's natural ceiling) and `Cost` per investment.
Investing is **month-by-month**: press the letter, confirm with `Y`, and the cost leaves your treasury. There is
no deficit spending.

**The key rule:** while `Devel < Resrc`, development is cheap. Once `Devel` reaches `Resrc` the cost rises
sharply. So the profitable play is to fill every city up to its `Resrc` line and then stop.

Aladda's ceilings are Agriculture 8/13, Lumber 4/5, Mining 3/4, Manufacture 4/5, Commerce 4/5, Structure 2/3,
Fortification 1/2 — i.e. **plenty of cheap headroom in agriculture and a little in everything else**. Its
agriculture costs only 60 GP a step, the cheapest thing you can buy anywhere early on.

Development also raises **morale and loyalty**, which matters as much as the money.

### 5.4 Tax, morale, loyalty, health

* Tax is 0–80 %. Aladda starts at **30 %**; every AI city sits at 25 %.
* High tax pushes morale and loyalty down; low tax pushes both up.
* Morale and loyalty are not capped at 100 — a well-run Aladda passes 120 within a few years.
* Recruits accumulate from population growth; **Conscript** buys more at a stated price per head, at a cost in
  population, morale and loyalty. Use it in an emergency, not as policy.

### 5.5 Vassals and allies

Some cities can be **vassalised** instead of occupied. A vassal pays tribute every month and defends itself, but
supplies **no recruits**. That is an excellent deal for a distant, low-value city — and the natural answer for
Nuralia, whose loyalty to its own duke starts at **5**.

An **Ally** simply exists; you learn nothing about it beyond the fact.

Deciding what to do with a conquest:

| Situation | Do this |
|---|---|
| Good resources, near your core, needs recruits | Hold and develop |
| Far away, mediocre, would need a garrison | Vassalise if possible |
| Poor, off the beaten track, source of enemies removed | Loot it and leave |

### 5.6 Building an army

Costs (in gold) come straight from the game's tables. `Maint` is **per figure per month** and every level adds
10 % to a unit's upkeep, so a large veteran army is genuinely expensive.

| Type | Buy | Train | Maint | Notes |
|---|---|---|---|---|
| Infantry | 4 | 2 | 0.3 | Cheapest, toughest per man, levels fastest, slowest |
| Mtd. Infantry | 8 | 3 | 0.5 | Fast, flexible, weaker than foot in a stand-up fight |
| Cavalry | 16 | 4 | 1.0 | Hardest hitter, best movement, dearest, poor missile |
| Bowmen | 12 | 4 | 0.6 | Excellent missile range/damage, fragile, poor melee |
| Horse Bow | 20 | 5 | 0.8 | Fastest, superb missile, levels slowest, very expensive |
| Warrior | 40 | 12 | 1.0 | |
| Knight | 80 | 16 | 2.0 | |
| Ranger | 100 | 20 | 2.5 | |
| Priest | 120 | 25 | 3.0 | |
| Mage | 160 | 30 | 4.0 | |

Equipment (buy / train / maint / min level):

| Slot | Options |
|---|---|
| Armor | Robe 2/0/0 · Leather 8/0/0.2 · Chain 20/1/0.5 · Mail 40/2/1.0 · **Plate 80/3/1.5 (lvl 3)** |
| Shield | Small 2/0/0 · Large 6/1/0.1 · Kite 8/1/0.2 |
| Weapon | Dagger 0 · Mace 2/0/0.1 · Sword 4/1/0.2 · **Halberd 6/2/0.3 (lvl 1)** · **2-Hand 8/2/0.2 (lvl 3)** |
| Pole | Spear 2/1/0.3 · **Pike 4/2/0.4 (lvl 4)** · Lance 10/2/0.6 |
| Missile | Thrown 3/1/0.3 · Javelin 5/2/0.4 · Sling 1/2/0.1 |
| Bow | X-Bow 8/1/0.4 · Short 5/3/0.6 · **Long 15/5/0.8 (lvl 3)** · **Compound 25/8/1.0 (lvl 5)** |
| Horse | Light 50/2/1.5 · Medium 75/3/2.0 · **Heavy 100/4/2.5 (lvl 2)** |
| Barding | Leather 10/0/0.6 · Chain 20/1/0.8 · **Mail 40/2/1.0 (lvl 2)** |

Special melee bonuses: **Spear +2, Lance +2, Halberd +4, Pike +6**. Missile bonuses (adjacent-hex fire):
**Thrown spear +1, Javelin +2**. Priests and Mages have inherent bonuses in both.

Illegal combinations exist and the game silently closes them off: a shield rules out a Two-Handed Sword, Halberd
or Pike; Mail or Plate plus any shield rules out a Long or Compound bow.

**Stacking** is 200 points per hex: foot 2 points each, light horse 4, medium 5, heavy 6. So 100 infantry, 50
light cavalry, or 33 heavy knights.

**Reinforcing dilutes experience.** Adding many raw recruits to a veteran unit drops the whole unit's level.
Prefer to raise a fresh unit and train it, and keep veterans topped up only slightly.

### 5.7 Class choice

| Class | Start lvl | Levels | Discount | Playstyle |
|---|---|---|---|---|
| **Warrior** | 6 | fastest | Infantry **−50 %** | Big cheap infantry armies; the easiest opening. Standard setup gives 5 caster henchmen. |
| **Knight** | 5 | medium | Cavalry **−25 %** (mounted infantry too) | Mobile shock army; strongest single fighter. |
| **Ranger** | 4 | medium | Bowmen & mounted bowmen −25 % | Missile-heavy, mobile, weaker in melee; casts spells. |
| **Priest** | 3 | slowest | — | Heals, buffs army-wide morale, fights adequately. |
| **Mage** | 2 | slowest | — | Avoid melee entirely; becomes the strongest character in the game. |

The discount is real and large: for a Knight player the shipped `1st Cavalry` costs **102 GP instead of 136**,
and its upkeep 3.8 instead of 5.0 per figure per month.

Whatever you pick, keep the army **combined-arms**: infantry to hold, bowmen to grind, cavalry to counter-punch,
and at least one Priest and one Mage.

---

## 6. Tactical battles

A battle is a **24 × 24** hex map. Turns are ~15 minutes of fighting; a battle can last at most **23 turns** and
you may Quit from **turn 7**. Territorial control is judged **around the centre hex (12,12)**.

### 6.1 Defending a city

1. **Entrench infantry** on the walls, with your fighter characters among them.
2. Put **bowmen and casters behind** the infantry line.
3. Keep **cavalry out of the line**, free to counter-attack.
4. As the enemy closes, grind them with massed missile fire — remember fire hits **every unit in the target hex**,
   so shoot at stacks.
5. Slow and demoralise with **Slow, Growth, Exhaust, Fear, Mud**.
6. When their momentum breaks, charge with heavy cavalry and high-level heavy infantry.

### 6.2 Assaulting a city

The hardest fight in the game. Either the garrison comes out or it sits behind the walls.

* If it comes out: form a defensive line, break the sortie, *then* walk into what's left.
* If it stays in: advance steadily, guard your flanks, protect bow units and casters while they weaken and
  demoralise the defenders, then commit heavy units through the gap.
* `Quake` and `Disintegrate` reduce walls and structures — the siege spells.
* **Quit just after your turn begins**, never after moving: the `List` figures only refresh at the start of your
  turn, so quitting later reports stale numbers.

### 6.3 Patrolling

After a conquest, patrol the newly-taken ground rather than idling. Wandering monsters are weaker than city
garrisons, so patrols convert time into experience and loot at low risk — the cheapest way to level an army.

### 6.4 Spells

Available by class and caster level:

| Lvl | Ranger | Priest | Mage |
|---|---|---|---|
| 1 | Grow | Vigor | Light |
| 2 | Dry | Light | Slow |
| 3 | Light | Rally | Confuse |
| 4 | Wither | Xhaust | Fear |
| 5 | Mud | Bless | Mud |
| 6 | Vigor | Heal | Bridge |
| 7 | Rally | Fear | Haste |
| 8 | Xhaust | Prayer | Pyro |
| 9 | Heal | Tower | Quake |
| 10 | Fear | Quake | Teleport |
| 11 | Bridge | Cure | Disint |
| 12 | Tower | Disint | Gate |

| Spell | Effect |
|---|---|
| **Bless** | Defensive bonus to the caster's army for one turn |
| **Bridge** | Creates a crossing over a river hex |
| **Confuse** | Tries to dislodge entrenched enemies from a hex |
| **Cure** | Restores a % of lost hits to **all** units in the caster's hex |
| **Disint** | Damages structures, walls **and every unit** in a hex — no exceptions, including yours |
| **Dry** | Reduces mud in a hex |
| **Fear** | Drops enemy morale in a hex; can disperse units (missile fire cannot) |
| **Gate** | Summons a Troll or Demon to fight for you (0 movement on arrival) |
| **Grow** | Increases vegetation — fails if the hex has none |
| **Haste** | +movement for selected units in the caster's hex. Costs Stamina; can cause hit damage if Stamina goes below zero. Cast **at the start** of movement — the bonus is a % of *current* allowance |
| **Heal** | Restores hits to **one** unit in the caster's hex |
| **Light** | Illuminates a radius and reveals **all** units in lit hexes; blocked hexes stay dark |
| **Mud** | Adds/increases mud (shown as dashed horizontal lines) |
| **Prayer** | Army-wide defensive bonus that persists, decaying 75 % per turn |
| **Pyro** | Multi-hex attack centred on the target; never harms your own or allied units |
| **Quake** | Reduces structures and walls; harms no units |
| **Rally** | Restores morale to all units in the caster's hex |
| **Slow** | Cuts enemy movement in a hex **next** turn |
| **Teleport** | Moves everything in the caster's hex (caster included) to a new destination |
| **Tower** | Builds a fortification in a clear, non-town hex |
| **Vigor** | Restores stamina to all units in the caster's hex |
| **Wither** | Reduces vegetation |
| **Xhaust** | Drains an enemy unit's stamina |

`Confuse`, `Fear`, `Haste`, `Bless`, `Prayer`, `Heal`, `Cure`, `Teleport`, `Pyro`, `Quake`, `Disint`, `Light`,
`Tower` and `Wither` all scale with caster level. Spells can fail (` spell failure.`).

---

## 7. An opening plan

A concrete first two years. Adapt freely.

**April 871 (month 3) — do not move yet.**
* `C` on Aladda. Note: 6,500 GP, tax 30 %, morale 75, loyalty 52, health 85, 1,500 people.
* `D`evelop **Agriculture** repeatedly — 60 GP a step and headroom to 13. Then Lumber (100), Mining (250),
  Manufacture (250), Commerce (200) up toward their `Resrc` ceilings.
* Buy **Structure** to 3 and **Fortification** to 2. Fortification measurably reduces humanoid raids.
* Leave tax at 30 % while morale/loyalty are climbing from development; drop it to 25 % if loyalty stalls.
* `U`nit → `T`rain your starting units with what's left. Trained units sit still that month anyway.

**Months 4–8 (May–September 871) — the western sweep.**
* Explore with your whole force; do not split it yet. Unexplored hexes hide everything.
* First target **Marinia (1,4)** — 1,200 people, sick ruler, morale 50 / loyalty 30, only 315 gold but an easy
  Decisive win, and it teaches you the tactical system cheaply.
* Then **Sur Nova (4,12)**: 3,400 people, genuinely good resources, and **no army**. It is the best value
  conquest in the west.
* **Brocada (6,1)** next — 7,150 gold and a militia that folds. That purse funds everything after it.

**October–November 871 — consolidate.**
* Garrison and develop what you hold. Push each city's five revenue categories to its `Resrc` line.
* `H`ire a Priest early (healing turns a Marginal win into a Decisive one) and a Mage as soon as your level
  allows another character.

**December 871 – February 872 — winter. Do not march.**
* Develop, train, reinforce, re-equip. Watch attrition on the City Status screen; if a force is stranded, `C`amp.

**872 — the north and the pass.**
* **Nuralia (15,2)**: loyalty 5 to its own duke. Try for vassalage; the tribute is free money.
* **Paritan (10,2)**: the best western army and an expansionist. Take it before it takes Brocada or Nuralia.
* **Gernok (15,8)** removes the goblin raids on the north coast permanently, which retires several garrisons.
* Then **Zarnix (13,18)** in the Justinid Pass. It is the gate to the Eastrealm and the single most important
  hex on the map. Come with entrenched infantry, massed bowmen, Quake/Disint for the walls, and cavalry held back.

**873 onward — east.**
Through the pass: Sothold (20,11), Estallah (21,8), Pudawala (21,16), Lucedia (20,20) — all rich, all with real
armies — and finally **Tetrada (21,4)** and Lucinian III.

Rules of thumb that hold throughout:

* **Never move in winter.**
* **Never let a battle end Marginal** if you want the city.
* **Develop before you expand** — 500 GP of development in month 4 pays for itself many times by 873.
* **Patrol between conquests**; free experience.
* **Replace losses with new units**, not by diluting veterans.
* **Save before every battle** (`Q` offers a save and then lets you continue) — the game is happy to kill your
  character outright.

---

## 8. Quick reference

| Fact | Value |
|---|---|
| Start | April 871 QJ, Aladda (6,7), 1,500 people, 100 soldiers, 6,500 GP, tax 30 % |
| World map | 24 × 24 hexes |
| Tactical map | 24 × 24 hexes, centre (12,12) |
| Battle length | max 23 turns; may Quit from turn 7 |
| Stacking | 200 points/hex — foot 2, light horse 4, medium 5, heavy 6 |
| Unit limit | 60 units |
| Character limit | 20, gated by your own level |
| Tax range | 0–80 % |
| Attrition months | December, January, February (plus bad weather) |
| Maximum score | 500 |
| Save letters | A–Y |
| Conquest requires | at least a **Decisive** victory |
| Win | Recover the Amulet, Crown and Scepter; take Tetrada |

### Credits

Design: Kurt Myers & Russell Shilling · Programming: Russell Shilling · Developer: Bret Berry ·
Manual: Larry Hall & Bret Berry · © 1989 Strategic Simulations, Inc.
