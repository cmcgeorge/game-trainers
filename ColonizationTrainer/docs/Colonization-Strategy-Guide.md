# Sid Meier's Colonization — Strategy Guide

A play guide for **Sid Meier's Colonization** (MicroProse, 1994 — the original DOS game,
`VICEROY.EXE`). Covers the goal, controls, the economy, combat, and the road to independence. Figures
are drawn from the game manual, the in-game Colonizopedia (`.games/PEDIA.TXT`), StrategyWiki, and the
FreeCol reimplementation's docs; anything version-sensitive is noted.

You take one of four European powers to the New World in 1492, build a colonial economy, win the
loyalty of your colonists ("Sons of Liberty"), and eventually **declare independence and defeat the
King's army**. You have until **1850**; if you haven't won your revolution by then the Continental
Congress sues for peace and you lose.

---

## 1. How to win

There is exactly one victory condition: **win the War of Independence.**

1. Grow your colonies and produce **Liberty Bells** (in the Town Hall) until your overall **Sons of
   Liberty (rebel sentiment) reaches ≥ 50 %**.
2. Open the **Game** menu (`Alt-G`) and choose **DECLARE INDEPENDENCE**.
3. Survive and destroy the bulk of the **Royal Expeditionary Force** the King sends across the
   Atlantic. When his army is broken, you win.

**Score** (Colonization Score, `F10`) is the tie-breaker/ranking and rewards an early, total win:

- **+1** per Petty Criminal / Indentured Servant, **+2** per Free Colonist, **+4** per expert or
  converted native.
- **+5** per Founding Father in your Congress.
- **+1** per 1,000 gold.
- **+1** per point of rebel sentiment (max 100).
- **+1** per Liberty Bell produced after foreign intervention during the war.
- **+1** per native settlement destroyed; **− difficulty level**.
- **×2** if you're the first power to declare independence (×1.5 if one beat you, ×1.25 if two did),
  plus a bonus for declaring **before 1780**.

### The King's Royal Expeditionary Force (what you must beat)
The REF scales with difficulty. At **Viceroy** (hardest) it starts around **47 Regulars, 25 Cavalry,
26 Artillery, 14 Men-o-War** and grows during the war; at **Discoverer** it's a fraction of that
(≈15 / 5 / 2 / 2). Lower difficulty isn't just a smaller REF — it also allows **more Tories** in a
colony before you take a production penalty (10 on the easiest level down to 6 on Viceroy).

---

## 2. Controls (DOS version)

### Map & movement
| Key | Action |
|---|---|
| **Arrow keys / numeric keypad (1–9, not 5)** | move the active unit in eight directions |
| **Alt + highlighted letter** | open a pull-down menu (e.g. `Alt-G` = Game) |
| `C` | Center the view on the active unit |
| `Z` / `X` | Zoom in / out (four zoom levels: 120×96 … 15×12) |
| `V` / `M` | View-pieces mode / Move-pieces mode |
| `H` | show Hidden terrain |
| `E` | go to the **Europe** screen |
| `Enter` on a colony tile | open that **colony** |

### Unit orders (the active unit)
| Key | Order |
|---|---|
| `B` | **Build** colony (or Join an existing colony) |
| `F` | **Fortify** (defensive bonus; stops the unit asking for orders) |
| `S` | **Sentry** (skip until something happens nearby) |
| `P` | **Plow** field / **Clear** forest *(Pioneer)* |
| `R` | build **Road** *(Pioneer)* |
| `G` | **Go To** a named place / colony (multi-turn auto-travel) |
| `W` | **Wait** (move this unit later this turn) |
| `Spacebar` | **No orders** this turn (skip) |
| `L` / `U` | Load / Unload most-valuable cargo |
| `O` | dump cargo overboard *(ship)* |
| `Shift-D` | **Disband** the unit |

The **turn ends automatically** once every unit has orders — fortify/sentry the ones you're done with
so they stop asking.

### Reports (function keys)
`F1` context help · `F2` Religious · `F3` **Continental Congress** · `F4` Labor · `F5` Economic ·
`F6` Colony · `F7` Naval · `F8` Foreign Affairs · `F9` Indian · `F10` Score.

### Save / load / independence
All in the **Game** menu (`Alt-G`): Save Game, Load Game, DECLARE INDEPENDENCE, Retire, Exit.
Colonization autosaves to `COLONY09.SAV`; manual saves go to slots `COLONY00`–`COLONY08`. *(That's
where the trainer's save editor reads/writes.)* Bonus: hold **Alt** and type **W, I, N** to toggle
the cheat menu in-game.

---

## 3. The four nations

Pick at the start (and a difficulty from Discoverer → Viceroy). Each power has one signature bonus:

| Nation | Bonus | Free starting unit |
|---|---|---|
| **England** | **More immigration** — colonists arrive on the Europe docks faster (crosses go further). | — |
| **France** | **Better native relations** — alarm rises slowly; peaceful coexistence. | Hardy Pioneer |
| **Spain** | **+50 % attack vs. native settlements** — the conquest power. | Veteran Soldier |
| **Netherlands** | **Better trade** — prices fall slower and recover faster; a bigger starting ship (Merchantman, 4 holds). | Merchantman |

The Dutch are the strongest economy; England snowballs population; France avoids native wars; Spain
plunders. (On Discoverer/Explorer everyone starts with a Veteran Soldier, so Spain's edge shows on
the harder levels.)

---

## 4. The economy: 16 goods and their chains

Sixteen tradeable goods (this is also their index order in the save file):

```
Food  Sugar  Tobacco  Cotton  Furs  Lumber  Ore  Silver
Horses  Rum  Cigars  Cloth  Coats  Trade Goods  Tools  Muskets
```

**Refine raw goods into finished goods** — they sell for far more, and a Custom House can auto-export
them even during the war. Each chain has a building tier and a specialist who doubles output:

| Raw | → Finished | Building (base → shop → **factory**) | Specialist (×2) |
|---|---|---|---|
| Sugar | Rum | Distiller's House → Distillery → **Rum Factory** | Master Distiller |
| Tobacco | Cigars | Tobacconist's House → Shop → **Cigar Factory** | Master Tobacconist |
| Cotton | Cloth | Weaver's House → Shop → **Textile Mill** | Master Weaver |
| Furs | Coats | Fur Trader's House → Shop → **Fur Factory** | Master Fur Trader |
| Ore | Tools | Blacksmith's House → Shop → **Iron Works** | Master Blacksmith |
| Tools | Muskets | Armory → Magazine → **Arsenal** | Master Gunsmith |
| Lumber | **Hammers** (build points) | Carpenter's Shop → Lumber Mill | Master Carpenter |
| — | Liberty Bells | Town Hall (+ Printing Press / Newspaper) | Elder Statesman |
| — | Crosses (immigration) | Church → Cathedral | Firebrand Preacher |
| Food surplus | Horses | Stables | — |

Notes: **Silver** is not processed — mine it (mountains) and sell it raw. **Ore → Tools → Muskets**
(no shortcut). **Trade Goods** and **Horses** are bought in Europe (horses then breed from a food
surplus). The **factory** tier needs the Founding Father **Adam Smith** and yields 50 % more output
per input (put in 20 ore, get 30 tools).

---

## 5. Terrain and yields

Base yields (before roads, rivers, plowing, specialists, or Sons-of-Liberty bonuses):

| Terrain | Food | Other |
|---|---|---|
| Plains | 5 | 2 cotton, 1 ore |
| Grassland | 3 | 3 **tobacco** |
| Prairie | 3 | 3 **cotton** |
| Savannah | 4 | 3 **sugar** |
| Marsh | 3 | 2 tobacco, 2 ore |
| Swamp | 3 | 2 sugar, 2 ore |
| Tundra | 3 | 2 ore |
| Desert | 2 | 1 cotton, 2 ore |
| Hills | 2 | 4 ore |
| Mountains | — | 4 ore, **1 silver** |
| Ocean / Sea Lane | — | 4 fish (needs Docks) |
| Arctic | — | nothing |
| Forests (Boreal … Rain) | 2–3 | 2–3 **furs**, 4–6 **lumber**, + the base crop at reduced rate |

Handy rules: **Plains grow the most food; Grassland = tobacco, Prairie = cotton, Savannah = sugar**.
Food experts add a flat **+2** (they don't strictly double). Rivers boost almost everything; roads and
100 % Sons-of-Liberty add **+2** to lumber/furs. Resource **specials** — Prime Timber (+4 lumber on
Conifer/Tropical), Ore deposit (+2 ore on hills), Beaver (fur), Game (fur + food), Fish, Prime
Tobacco/Cotton/Sugar/Silver — sharply raise a tile's output; scout for them before you settle.

---

## 6. Colonists and experts

Colonists come in a hierarchy: **Petty Criminal → Indentured Servant → Free Colonist → Expert /
Veteran**. A Free Colonist produces the baseline (~3/turn); an **expert doubles output in one field**
(food experts +2). Ways to make experts:

- **On the job** (slow, unreliable).
- **Schools:** Schoolhouse trains basic experts (Farmer, Fisherman, Carpenter, Pioneer, Scout) in 4
  turns; **College** adds Blacksmith/Gunsmith (6 turns); **University** trains the elite — Elder
  Statesman, Firebrand Preacher, Jesuit Missionary (8 turns).
- **Native villages** teach a skill each (send your servants/criminals to learn).
- **Immigration / recruitment** on the Europe docks.

**Important:** the four planter/trapper experts — Master **Sugar**, **Cotton**, **Tobacco** Planter
and Expert **Fur Trapper** — **cannot be recruited in Europe**. You must learn them from Indians (or
on the job) first, after which your own schools can teach them. **William Brewster** (Founding Father)
lets you cherry-pick immigrants and stops criminals/servants arriving.

---

## 7. Founding Fathers

Liberty Bells accrue in the **Continental Congress** (`F3`); each session you pick one candidate to
recruit, and each Father costs more bells than the last. There are **25**, five per category (some —
Washington, Bolívar, Fugger, Jones, Las Casas — only appear after 1600). The trainer can grant these.

**Trade** — **Adam Smith** (unlocks factory-tier buildings) · **Jakob Fugger** (cancels all boycotts,
once) · **Peter Minuit** (native land is free) · **Peter Stuyvesant** (build Custom Houses) · **Jan de
Witt** (trade with foreign colonies; better foreign reports).

**Exploration** — **Ferdinand Magellan** (+1 ship movement; shorter Europe trips) · **Francisco de
Coronado** (all colonies revealed) · **Hernando de Soto** (Lost City Rumors always good; +sight) ·
**Henry Hudson** (fur trappers ×2) · **Sieur de La Salle** (free stockade at pop 3).

**Military** — **Hernán Cortés** (more treasure from conquered villages, shipped free) · **George
Washington** (units auto-promote to Veteran on a win) · **Paul Revere** (last colonist grabs stored
muskets to defend) · **Francis Drake** (privateers +50 %) · **John Paul Jones** (free Frigate).

**Political** — **Thomas Jefferson** (+50 % Liberty Bells) · **Pocahontas** (native tension reset,
alarm halved) · **Thomas Paine** (Liberty Bells +tax-rate %) · **Simón Bolívar** (+20 % Sons of
Liberty everywhere) · **Benjamin Franklin** (Europeans always offer peace).

**Religious** — **William Brewster** (choose immigrants; no criminals/servants) · **William Penn**
(+50 % crosses) · **Jean de Brébeuf** (missionaries act as experts) · **Juan de Sepúlveda** (higher
native conversion) · **Bartolomé de las Casas** (all converts become Free Colonists, once).

---

## 8. Combat

Combat is one roll of **effective attack vs. effective defense**, each scaled by modifiers. Bonuses
that matter:

- **Fortifications (defense):** Stockade **+100 %**, Fort **+150 %**, Fortress **+200 %**.
- **Terrain:** defenders in forest/hills/mountains get a bonus; attackers in open wilderness get a
  **+50 %** ambush bonus. Natives get big terrain bonuses (up to +150 % in mountains).
- **Fortify** order **+50 %** defense.

Losers are **demoted one step at a time**, not instantly killed: **Dragoon → (loses horses) → Soldier
→ (loses muskets) → Colonist → captured/killed**. Winning promotes up the skill ladder (guaranteed to
Veteran with **George Washington**).

**Unit strengths:** Soldier 2 / Veteran 3; Dragoon 3 / Veteran 4; Continental Army 4 / Continental
Cavalry 5; King's Regular 5 / Cavalry 6. **Artillery** is Attack 7 / Defense 5 — devastating attacking
colonies and fortifications, but with a heavy penalty in open terrain, and it **degrades permanently
to Damaged Artillery (5/3) when it loses** (destroyed on a second loss). Buy artillery in Europe (500
gold, +100 each after) or build it at an Armory from Tools. **Dragoons are the workhorse attacker.**

---

## 9. Taxes, the Tea Party, and boycotts

The King periodically demands a **tax increase** (it can climb toward ~75 %). Each demand gives three
options:

1. **Accept** the raise.
2. **Refuse and hold a "party"** — up to ~100 tons of the named good are dumped, that good is
   **boycotted** (can't be bought/sold in Europe), and the tax does **not** rise this time.
3. Refuse but stay open to paying back-taxes later.

Lift a boycott by paying back-taxes = **500 × the good's current price** (expensive), or get **Jakob
Fugger** to wipe every boycott at once. A **Custom House** (needs Peter Stuyvesant) auto-sells goods
and keeps working during the revolution. Tax cuts both ways: **Thomas Paine** turns your tax rate into
bonus Liberty Bell production, so a high tax is not purely bad.

---

## 10. The road to independence

1. **Establish** 1–2 coastal colonies early on good terrain near a river; get a Carpenter working so
   you can build.
2. **Refine and sell** (rum, cigars, cloth, coats) to build a treasury; buy Tools, Muskets, Horses.
3. **Print Liberty Bells** — build the Town Hall up (Printing Press → Newspaper), staff it with Elder
   Statesmen, and grab **Jefferson**, **Paine**, and **Bolívar** to push Sons of Liberty toward
   100 %.
4. **Arm up** before declaring — Veteran Soldiers/Dragoons, Artillery, Fortresses on the coast. Units
   in a **100 % rebel colony** upgrade free to the Continental tier (+1 strength) at declaration; those
   at 50–99 % only *might*.
5. **Declare** (`Alt-G`) once you're ≥ 50 % and ready. During the war your Liberty Bells stop electing
   Fathers and instead court **foreign intervention** — hit another power's requested bell total and
   it sends Continental Army/Cavalry, Artillery and Men-o-War, and bombards Tory cities (+50 % for your
   attacks). Break the King's army and the New World is yours.

Push rebel sentiment as close to **100 %** as you can before declaring: any lingering **Tory** unrest
converts into an **attack bonus for the King**.

---

## 11. The map

The standard map is **58 × 72** tiles including a one-tile border (56 × 70 visible) — you can also pick
the historical **America** map or customize a new world (size, land form, temperature, climate) at the
start. The eight native nations are seeded from a fixed historical dispersal (`.games/TRIBE.TXT`):

- **North / plains:** Sioux, Apache, Iroquois, Cherokee.
- **Caribbean / South:** Arawak, Aztec, Inca, Tupi.

Semi-nomadic tribes (Sioux, Apache, Tupi) roam and teach outdoor skills; the advanced/civilized ones
(Aztec, Inca) hold cities and **treasure** — lucrative but dangerous to raid (Spain's specialty).
Scout villages with a Seasoned Scout for skills, gifts, and Lost City Rumors; keep the peace with
missions unless you mean to conquer.

---

### Quick reference — victory checklist

- [ ] Two or more productive, well-defended colonies.
- [ ] A Custom House exporting finished goods for steady gold.
- [ ] Town Halls printing bells; Jefferson + Paine + Bolívar in Congress.
- [ ] Sons of Liberty near 100 % everywhere.
- [ ] Veteran Dragoons + Artillery garrisoned in coastal Fortresses.
- [ ] Declare before 1780 for the score bonus — then destroy the King's army.
