# War of the Lance — Strategy Guide

A player's guide to **War of the Lance** (SSI, 1989), the *AD&D Dragonlance* strategic wargame set on
the continent of **Ansalon** on the world of Krynn. You command either the evil **Highlord**
dragonarmies (based in Neraka) or the good **Whitestone** alliance, over a war that runs **6 game
years / 30 turns** (5 turns a year: `MAR/APR`, `MAY/JUN`, `JUL/AUG`, `SEP/OCT`, `WINTER`).

Sourced from the bundled manual (`.game/warlance.txt`) and the reverse-engineered data tables (see
`ReverseEngineering.md`).

---

## 1. How to win

There are **two paths to victory** for either side:

1. **Points victory.** You score for making alliances, conquering nations, and destroying/capturing
   enemy troops and champions. On the Victory screen, **Highlord points are negative, Whitestone
   points are positive**, so a net score of zero is a tie. Whoever is ahead when the 30 turns end
   wins.
2. **Sudden (conquest) victory** — ends the game immediately:
   - **Whitestone wins** by capturing the **capital of Neraka** *and* the **tower to the north-west**
     (the Highlord stronghold near the Khalkis Mountains).
   - **Highlord wins** by conquering the **four Knight countries** — **Solanthus, Caergoth, Gunthar,
     and Northern Ergoth** — *plus* the **Clerist Tower** near Palanthus.

You may play Highlord-vs-Whitestone against another human, or command Whitestone against the
computer. If the Highlord side is the computer, the manual recommends the **SCENARIO** start so both
sides begin with allies and troops.

---

## 2. Getting started (opening menu)

After the copy-protection question and setup screens, the **OPENING MENU** lets you configure:

| Option | Choices | Effect |
| --- | --- | --- |
| **A) Highlord** | Human / Computer | Who plays the evil side |
| **B) Game selection** | Scenario / Campaign | Campaign = war's opening (only Neraka allied to Highlord, Whitestone has no allies). Scenario = deeper into the war, both sides have allies and some nations are already conquered |
| **C) Level of play** | 1–5 | Combat weighting: 1 = big Whitestone advantage, 5 = favors Highlord, **3 = balanced** |
| **D) Strength-HL** | 1–5 | Highlord starting strength: 1 = 60 %, 2 = 80 %, 3 = 100 %, 4 = 120 %, 5 = 120 % |
| **E) Strength-WS** | 1–5 | Whitestone starting strength (same scale) |
| **F) Replacements-HL** | 1–5 | How many replacements Highlord gets for losses |
| **G) Replacements-WS** | 1–5 | Whitestone replacement rate |
| **H) Alliance level** | 1–5 | Higher = neutral nations ally more readily |
| **I) Play game** | — | Start |

New players: pick **Whitestone vs. Computer**, **Scenario**, **Level 3**, default strengths.

---

## 3. Controls

The DOS version is driven by a **map cursor + a bottom menu bar**. Move the cursor, then pick an
action word from the menu bar with **`<SPACE>`** (the original also supported a joystick).

**Cursor movement** uses the numeric-keypad-style diamond (or the arrow/cursor keys):

```
    U  I  O          7  8  9        (up-left / up / up-right)
    J     K          4     6        (left    /    / right)
    N  M  ,          1  2  3        (dn-left / dn / dn-right)
```

- **`<SPACE>`** — select the highlighted menu item / map square (joystick: fire button).
- **`Delay(#)`** menu item — response speed, 1 (fastest) … 9 (slowest).
- **`Joy/Key`** menu item — toggle joystick vs. keyboard input.

Under DOSBox, use your emulator's cycles setting if the game runs too fast/slow, and its own
key-mapper if the keypad diamond is awkward on a laptop.

---

## 4. The turn sequence

Each of the 30 turns runs these phases in order (Highlord's political phases, then Whitestone's, then
the shared resolution):

```
Message → Quest → Champion → Reinforcements/Replacements → Subversion → Diplomatic   (Highlord)
        → Quest → Champion → Reinforcements/Replacements → Subversion → Diplomatic   (Whitestone)
Country Status → Victory Display → Initiative → Recovery
Player 1 Movement → Player 1 Combat → Player 2 Movement → Player 2 Combat
```

- **Message** — nations conquered last turn; war news, quest results, magic found.
- **Quest** — your champions are always questing for magic artifacts. You react to champions who are
  detected, **wounded** (`REST PARTY / WITHDRAW / SEEK AID`), **captured** (`ESCAPE / REMAIN /
  RESCUE`), or resting (`REST / SEEK AID / REJOIN`). Trade-off: faster healing/rescue = higher risk of
  capture or death and more quest delay.
- **Reinforcements/Replacements** — lost units are partially rebuilt (rate set by menu options F/G).
- **Subversion** — attempt to undermine enemy alliances.
- **Diplomatic** — the heart of the game (see §5).
- **Initiative** — the side that wins it moves/fights first **and gets +25 % operation points** that
  turn.
- **Recovery** — units regain operation points and shed fatigue (see §6).
- **Movement → Combat** — you maneuver, then resolve the battles you designated (see §7).

---

## 5. Diplomacy — how the war is really won

Most of Ansalon starts **neutral**. Alliances swing the balance far more than early battles.

Diplomatic menu bar: **`COUNTRY  DIPLOMAT  TRANSFER  MAP  EXIT  WAR`**

- **Country** — pick a target nation.
- **Diplomat** — cycle to the diplomat you want to send.
- **Transfer** — send that diplomat to the selected country to court an alliance.
- **War** *(Highlord only)* — declare war on a country; it immediately allies with one side, and that
  side may deploy its units at once.

When a nation joins you, you **deploy its units** anywhere inside its borders (`MOVE UNIT / EXIT`).
Fleets at **Maelstrom** auto-deploy and can't move until your movement phase.

**Tips:**
- As **Whitestone**, race to pull the isolated good nations together before the dragonarmies strike;
  prioritize the four Knight countries and the Clerist Tower (they're also Highlord's win condition —
  deny them).
- As **Highlord**, use **War** aggressively to force weak neutrals in, then concentrate on the Knight
  countries + Clerist Tower for the sudden victory.
- A higher **Alliance level** (menu option H) makes neutrals swing faster — usually toward whoever is
  winning, so keep your visible momentum up.

---

## 6. Movement, terrain & fatigue

**Operation Points (OP)** = squares a unit can move per turn. Movement is bounded by OP, terrain, and
enemy **Zones of Control (ZOC — the 8 squares around an enemy unit)**.

Movement menu: **`CURSOR  GET  RECON  LAST  QUAD  MAP  MENU`** → **`GET`** a unit → **`MOVE  EXIT
ATTACK  NEXT  ITEM  (UN)LOAD  PATROL`**.

- **Recon** estimates enemy strength in a square before you commit.
- **Map** toggles the **tactical** and **strategic** views.
- **Next** cycles your units — handy to review status.

**Movement cost & restrictions:**
- Normal move = **1 OP/square**; **forest = 2 OP** (except elf & kender units = 1).
- Air units = **1 OP any terrain**; **wizards move at 0 OP cost (unlimited)** and gain no fatigue.
- Moving between two enemy ZOCs costs **+3 OP**.
- You **cannot** enter a neutral country, stack with enemies, or leave the map. (Some map regions
  belong to no nation and both armies may cross them freely — no country name shows on the terrain
  line there.)
- **Mountains**: only **dwarf, ogre, and wizard** ground units may enter.
- Ground units can't enter sea/coast/river/swamp (wizards may pass through but not stop; any ground
  unit can cross a river *aboard a fleet*).

**Stacking:** most terrain allows 2 infantry/cavalry, plus up to 2 flyers plus leaders/wizards, for
**≤ 10 units/square**; cities & ports allow 3 infantry/cavalry.

**Fatigue (0 rested … 24 exhausted)** cuts combat strength **4 % per point**, and a unit entering
combat with **0 OP** fights at **−20 %**. Recovery is faster in cities/ports/fortresses/towers/dwarven
forts and for nations with fleets; slower when you keep moving, sit in an enemy ZOC, or are far from
your capital — and there is **no fatigue recovery in winter**. Don't attack with exhausted troops.

---

## 7. Combat

Naval combat resolves first, then land/air.

- **Naval** — fleets fight when adjacent, or within 4 squares if one has **Patrol** on. Menu:
  `CONTINUE COMBAT / WITHDRAW`; fog can end a battle early. You can fight past a blockade and land
  troops the same turn.
- **Land/Air** — the **attacker** chooses `RETREAT / LIGHT / HEAVY / ABORT / MAP`:
  - **Light** = low losses both sides (harassment / whittling down).
  - **Heavy** = full commitment, greatest losses both sides.
  - The **defender** then chooses `RETREAT / STAND / COUNTERATTACK`. **Always STAND when defending a
    fortified square** (city/port/tower/fort) to keep the terrain bonus; retreat halves your losses
    but cedes ground; counterattack can hurt the enemy badly but forfeits the defender bonus.
- **Dragons** — any army with dragons picks **`DRAGON FEAR`** (fly over to paralyze the enemy;
  minimal risk to the dragons) or **`ATTACK`** (front-line them for maximum enemy losses, risking the
  dragons). The wise commander often uses **Dragon Fear** and keeps the dragons safe.

When a defender is destroyed the attacker advances a stack into the square (not into mountains) and
may sometimes attack again.

**Unit quality** is a 1–7 combat-effectiveness rating (higher = better); it's shown on the unit
summary alongside fatigue, OP, and fortification/patrol status.

---

## 8. Maps

The game provides a **tactical** map (zoomed, for moving individual units) and a **strategic** map
(the whole continent) — toggle with the **`Map`** menu item; **`Quad`** jumps the cursor to a chosen
quadrant. National borders matter enormously: you cannot cross neutral territory, so alliances
literally open the roads.

**Ansalon at a glance** (from the nation table; not a literal grid):

```
                          N. ERGOTH ── NORDMAAR
   PALANTHUS ─ SOLANTHUS ─ VINGAARD ─ THROTYL
   (Clerist Tower)         │             │
   CAERGOTH ── GUNTHAR     KERN ──── NERAKA ★ (Highlord capital + NW tower)
        │                   │           │
   QUALINESTI            BLODE ──── SANCTION ── KHUR
        │                                        │
   THORBARDIN (dwarves)   LEMISH ── SILVANESTI  GOODLUND
        │                            (elves)      │
     TARSIS ──── KAOLYN            SANCTION      HYLO (kender)
        │
   ~~ Maelstrom (fleets) ~~   KOTHAS / MITHAS (minotaurs, fleets)   ZHAKAR (dwarves)
```

> This is a **relational sketch** of where the key nations and objectives sit, not the exact 2-D grid
> — the shipped `WL.MAP` holds the true terrain grid (21 terrain types, see the RE notes), which needs
> the game's own map viewer or a live capture to render faithfully.

**Objective landmarks to memorize:**
- **Highlord**: NERAKA capital + the NW tower are *your* seat — defend them; your win = the 4 Knight
  countries (**Solanthus, Caergoth, Gunthar, N. Ergoth**) + the **Clerist Tower** near Palanthus.
- **Whitestone**: those same Knight countries + Clerist Tower are your shield; your win = take
  **Neraka + the NW tower**.
- **Special terrain**: **Maelstrom** stops fleets automatically; **mountains** block all but dwarf/
  ogre/wizard; forests slow everyone but elves/kender.

---

## 9. Unit reference (from the manual appendix)

**Base number** is a unit's full strength; **quality** is its 1–7 combat rating. Selected entries:

| Nation | Units | Base | Quality |
| --- | --- | --- | --- |
| Neraka (Highlord core) | 8 Mercenary Infantry / 2 Mercenary Cavalry | 200 / 150 | 3 |
| Neraka | Red ×2, Blue ×2, Green, Black, White dragons | — | 3–5 |
| Highlord (special) | 9 Baaz Draconian / 10 Kapak Draconian | 200 / 150 | 3 / 4 |
| Highlord (special) | 2 Soth Undead Infantry | 120 | 7 |
| Whitestone (special) | 3 Tower Infantry; Gold/Bronze/Silver/Copper/Brass dragons | 140 / — | 4 / 3 |
| Silvanesti (elves) | 6 Elf Infantry / 4 Griffon / 4 Elf Fleet | 180 / 240 / 20 | 3 / 1 |
| Qualinesti (elves) | 4 Elf Infantry / 4 Pegasus | 180 / 84 | 3 / 1 |
| Thorbardin / Kaolyn / Zhakar (dwarves) | Dwarf Infantry (+ Griffons at Zhakar) | 180 / 240 | 3 / 1 |
| Kothas / Mithas (minotaurs) | 4 Minotaur Infantry / 4 Minotaur Fleet | 110 / 20 | 6–7 / 5 |
| Knight countries (Caergoth, Gunthar, N. Ergoth, Solanthus) | Elite Human Infantry / Cavalry | 140 / 130 | 5 |
| Blode / Kern | Ogre Infantry | 130 | 4 |
| Goodlund / Hylo (kender) | Kender Infantry | 130 | 2 |

The full table is in the manual appendix. The **highest base number is 240 (Griffon)** — the ceiling
the trainer's "Max all" uses.

---

## 10. Quick strategy summary

- **Diplomacy wins wars here.** Grab neutrals early; the four Knight countries + Clerist Tower are the
  pivot for *both* victory conditions.
- **Fight from good terrain.** Defend fortified squares with **STAND**; make the enemy pay OP and
  fatigue to reach you.
- **Manage fatigue & OP.** Never attack exhausted or 0-OP; use the **Recovery** phase and hold cities.
- **Use flyers and wizards for reach** (wizards move free and never tire); **use Dragon Fear** to
  cripple enemies without risking your dragons.
- **Watch initiative** — winning it is a +25 % OP swing for the whole army that turn.
- **Play the clock.** If you can't force a sudden victory, keep the point score on your side of zero
  when turn 30 arrives.
