# Darklands — Strategy Guide

A player's guide to **Darklands** (MicroProse, 1992): how it plays, the controls, how to build a
party that survives, and how to reach the game's climactic quest. Darklands is an open-ended
party RPG set in a superstitious, "as-people-believed-it" **Holy Roman Empire around 1400** —
saints work miracles, alchemy is real, and the Devil's servant Baphomet is stirring.

> There is no single victory screen. The game explicitly "can be played forever" **[README]**.
> "Winning" means completing the great challenges — chiefly destroying **Baphomet's Citadel of the
> Apocalypse** — while your party grows in **Fame**.

---

## 1. Getting started

### 1.1 Create the party (up to 5 characters)

You build characters by living through a **life path**: pick a background, then make choices for
each 5-year "term," accumulating attributes, skills, equipment and a little money. Aim to *start
adventuring around age 30–35* — older characters are more skilled but begin closer to age-related
decline.

**Attributes** (see `.docs/ReverseEngineering.md` §2.1): Endurance, Strength, Agility, Perception,
Intelligence, Charisma, plus party-wide **Divine Favor**.

- **Strength & Endurance keep you alive** — don't start either below ~30.
- Keep other attributes ≥ 20.
- The party needs: one **face** (Charisma 30+, speech skills), one **alchemist** (Intelligence
  35+), one strong **healer**, one **artificer**, and everyone able to fight.

**Skills** (19 total): weapon skills (Edged, Impact, Flail, Polearm, Thrown, Bow, Missile Device),
plus Alchemy, Religion, Virtue, Speak Common, Speak Latin, Read & Write, Healing, Artifice,
Stealth, Streetwise, Riding, Woodwise. Spend character-creation points on the skills that are
**hard to train later** (rule of thumb: the further down the list, the harder to raise in play).
Only the **highest** Healing in the party matters, so concentrate it on one person.

### 1.2 First hours in your starting city

- Stay in one city for a while — you may not be ready to leave for months of game time, and that's
  fine.
- **Beat up muggers/thieves in the back alleys at night** to raise weapon skills fast (avoid the
  watch — you're breaking curfew).
- **Do odd jobs** while camping to raise cash; early money is scarce.
- Buy the best weapons/armour you can, and learn a couple of **saints** and **alchemy recipes**.

---

## 2. Controls

All mouse actions have keyboard equivalents. Movement uses the **cursor keys or numeric keypad**
(keypad `7/9/1/3` = diagonals). Most menus show a command letter in **crimson** — press that letter
to run the command immediately. Press **F10** (or hold the **right mouse button**) for the main
menu bar, then arrow + **Enter** to choose.

### 2.1 Global keys

| Key | Action |
| --- | --- |
| **F1–F5** | Show character sheet for party member 1–5 |
| **F6** | Party information screen (Fame, wealth, location, notes, local rep) |
| **F10** | Open the main menu bar |
| **Enter** | Select / confirm |
| **Shift** | Display help / (held) drag a whole item stack when transferring |
| **Alt+0** | Change marching order |
| **Alt+S / Alt+L** | Save game / Load game |
| **Alt+D** | Change difficulty |
| **Alt+C** | Toggle "show changes" (temporary Alchemy/Saint stat modifiers) |
| **Alt+M / Alt+F** | Toggle music / sound effects |
| **Alt+P** | Pause |
| **Alt+Q** | Quit to DOS |
| **Space** | (at startup) skip the opening animation |

Inventory/interaction: **p** drink a potion, **d** drop an item; transferring equipment onto a
character can be done by tapping that character's **number key** as the destination.

### 2.2 Combat (tactical) controls

Combat is a **real-time-with-pause** system: the action runs until it needs an order, or you pause
it, then you issue orders per character.

- Move with cursor keys / keypad; click (or press the crimson letter) to choose **Attack, Move,
  Use item, Cast/Pray, Group move**, etc.
- Numbers **1–5** re-select a specific character — essential when someone climbs stairs/ladders
  ("portals") to another floor and drops off the shared view **[README]**.
- On the largest battlefields (mines, the Templar monastery, Baphomet's Citadel) you'll see
  **"Battlefield save rules are in effect"** — you may **Alt+S** whenever no living enemy remains
  on the current floor **[README]**. Save often there.

---

## 3. The gameplay loop

Darklands cycles between four screens:

1. **Travel map of the Empire** — move between cities/sites; random encounters (bandits, Raubritter
   ambushes, wildlife) can interrupt you. You can save here.
2. **City** — a menu of buildings/services: `citySquare, councilHall, imperialMint, cityBarracks,
   university, marketplace, fortress, pawnshop, hospital, docks, poorhouse, monastery, cathedral,
   cityChurch`, plus merchants (goods merchant, blacksmith/swordsmith/armorer/gunsmith/bowyer,
   artificier, jeweler, clothmaker, pharmacist) and the trading houses **Medici, Hanse, Fugger**.
   Gather rumors, take quests, trade, learn saints, buy reagents.
3. **Camp** (press **C** on the map) — spend a day to **Relax, Regain Strength, Pray for divine
   favor, do Alchemy work, Earn money (odd jobs), Guard the camp, or Train/Study** a skill
   **[EXE]**. Camping is your main healing/training/economy engine.
4. **Tactical combat** — the battlefield described in §2.2.

### 3.1 Money & the economy

Currency is **Florins (fl) / Groschen (gr) / Pfennigs (pf)** — **20 gr = 1 fl**, **12 pf = 1 gr**.
Early on, earn pfennigs with camp odd jobs and by looting defeated thugs. The **#1 long-term income
is mixing and selling potions**, once your alchemist is established. Save toward better armour and a
horse (Riding) for faster map travel.

### 3.2 Religion (saints) & Virtue

Praying to the right **saint** grants miracles (healing, protection, smiting evil). Which saints a
city's church will teach is **randomized per game** and gated by your **Charisma + Latin** and by
**donations**. Keep notes on where each saint is available. High **Virtue** is required to face
certain foes — e.g. *"Only high Virtue can subdue the dragon"* **[EXE]**.

### 3.3 Alchemy

Your alchemist mixes **potions** from reagents plus **Philosopher's Stones**. Key combat potions
include **IronArm** (strength) and healing philters like **Essence of Grace** **[WEB]**. Reagent
availability is randomized per game — note which town sells what (e.g. Manganese for Sunburst).

---

## 4. How to win — the great challenges

The README lists the game's "endgame" achievements: defeat **Raubritters** (robber-knights),
**dragons**, the **three kinds of trouble in mines**, and ultimately **Baphomet** **[README]**.

### 4.1 Build-up quests

- **Raubritter hunts** — once bandits are trivial, take robber-knight bounties from **merchant
  houses**. The same Raubritter is often wanted by several people in several nearby towns; collect
  *all* the bounties before you kill him to maximize payment **[WEB]**.
- **Mines** — large multi-level battlefields (kobolds, and worse). They allow battlefield saves;
  clear each floor of living enemies before saving.
- **Dragons** — require **high Virtue** and strong, varied weaponry.

### 4.2 The main quest: Baphomet's Citadel of the Apocalypse

The main quest is **never handed to you** by a prompt. You start it by chasing **rumors of witches'
covens and gatherings of Satanists** **[WEB]**, which lead you up a chain: disrupt covens → find the
**Sabbat** / Satanist hierarchy → learn the location of the **Citadel of the Apocalypse** → confront
**Baphomet**.

Before entering the Citadel:

- **Save first — you cannot leave once inside** **[WEB]**.
- Stock **healing** and **strength** potions (Essence of Grace, IronArm) in quantity.
- Max **Virtue** and **Alchemy**; equip diverse, high-quality weapons; bring your best saints.
- It is a **long, multi-level battle** — use the battlefield-save rule after clearing each floor,
  and defeat Baphomet to end the great quest.

---

## 5. Maps

### 5.1 The world — Holy Roman Empire, c. 1400

The travel map is a stylized map of the **Empire and its neighbours**, dotted with **free cities,
bishoprics, castles, villages, monasteries and mines**. An **interactive copy ships in `.game\`**:
open **`.game\dkmap1.html`** (it drives `dkmap1.swf`) to browse the labelled overworld.

Broad regional layout (orient yourself by river and mountain):

```
                         (North / Baltic & North Seas)
   Hamburg · Lübeck · Bremen ───────────── Stettin · Danzig
        │                                        │
   Köln · Frankfurt M ──── Erfurt · Leipzig ── Frankfurt O
   (Rhine)     │                │                 (Oder)
   Strassburg  Nürnberg ──── Prag (Bohemia)
        │          │
   Freiburg B · Augsburg · München ──── Wien (Vienna)
        │                                   │
     (Alps / Switzerland)            (Austria / Southeast)
```

*(Names as spelled in-game; note the README's warnings: "Frankfurt M" = Frankfurt am Main vs
"Frankfurt O" = Frankfurt an der Oder, and "Freiberg im Breisgau" on the map is really **Freiburg
B**, distinct from **Freiberg** in the Wettin Lands **[README]**.)* Your start city is one of these
free cities (the shipped template save is set in **Rottweil**).

### 5.2 City layout (schematic)

A city is a **menu of destinations**, not a walkable map. Typical services:

```
            ┌───────────── CITY ─────────────┐
   councilHall (City Lord)          cathedral / cityChurch (saints, donations)
   imperialMint (currency)          university (learning, Latin)
   marketplace (goods)              hospital (healing)
   cityBarracks / fortress          pawnshop (buy/sell)
   docks (river/sea travel)         monastery (religious training)
   inns (rest & SAVE)               back alleys (night: thugs, Streetwise)
            └── trading houses: Medici · Hanse · Fugger ──┘
```

Save at an **inn**; the "back alleys at night" are where you farm weapon skill early (mind the
curfew and the watch).

### 5.3 Tactical battlefield

Battles play out on a **tiled, multi-level** map (floors connected by **portals** — stairs and
ladders). Enemies spawn from **activation records** (start position `strtx/strty`, spread `sprd`,
and optional **reinforcements**; see `.docs/ReverseEngineering.md` §4.1 and the shipped `TAC.TXT`).
Practical notes:

- Hold a **doorway/chokepoint** so foes come one or two at a time.
- Keep the party together (**Group move**); a member who takes a portal alone must be re-selected
  with **1–5** and can confuse group mode until rejoined **[README]**.
- On mine/monastery/Citadel maps, **save after every cleared floor**.

---

## 6. Quick tips

- Keep a **notebook** — there is no quest log; record who gave which quest where, and which towns
  teach which saints / sell which reagents (both randomized per game).
- **Heal via camping**, not by waiting — without a real Healing skill you regain ~2 points/day.
- Recruit/retire at inns; a rejoining character arrives with **knowledge but no equipment**, so
  **cache their gear** before retiring them **[README]**.
- Rescue a party member who suffered an "uncertain fate" (rows of `?`) from a **city-hall dungeon**
  — killing all guards frees them **[README]**.
- Save frequently, especially before entering any large fixed battle.

---

### Sources

Shipped docs (`.game\README.TXT`, `TAC.TXT`, `darklands.pdf`, `.game\dkmap1.html`); the embedded
executable string tables (see `.docs/ReverseEngineering.md`); and public references — Darklands
Wiki (Controls, Attributes, Skills), *Before I Play*, GameFAQs, and darklands.net — for controls
and the main-quest outline.
