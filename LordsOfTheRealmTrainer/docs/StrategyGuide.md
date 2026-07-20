# Lords of the Realm — Strategy Guide

A practical guide to conquering England & Wales. Grounded in the game's own mechanics as recovered in `ReverseEngineering.md`; the terminology below matches the in-game labels exactly.

**The victory condition:** control the whole map, or become the last lord standing. Everything else — grain, sheep, stone, swords — is a means to fielding armies that take castles and counties faster than your rivals take yours.

---

## 1. The loop you actually play

Each **year** has four **seasons** — **Spring, Summer, Autumn, Winter** — and every season you:

1. **Feed the people.** Set rations. Starving peasants revolt and flee; well-fed peasants breed, immigrate, and stay loyal.
2. **Allocate labor.** Split peasants between grain, cattle, sheep, quarrying, foresting, building, and the armory.
3. **Trade.** Buy what you're short of and sell your surplus at the market.
4. **Build & arm.** Raise castle walls, forge weapons, raise or disband armies.
5. **Make war.** Move armies, invade counties, lay or lift sieges, and talk (or lie) to rivals.

Win the economy and the war takes care of itself. Lose the economy and no army can save you.

---

## 2. Population & happiness — your real currency

Peasants are simultaneously your workforce, your soldiers (via **Conscription**), and your tax base. Two numbers govern them: **Population** and **Happiness**.

- **Happiness up** → immigration (`peasants are flocking into your counties`), births, festivals.
- **Happiness down** → emigration (`hordes of peasants... leaving their lands`), and eventually revolt (`revolts may occur if they are not fed`).

Happiness is pushed by:

| Raises happiness | Lowers happiness |
|---|---|
| Generous rations (Double/Triple food) | Starvation / low rations |
| Low tithe (tax) | High tithe — rivals *and* the Church will publicly shame you (`taxes his subjects too highly`) |
| Ale to drink | Overcrowding beyond castle capacity |
| Winning, festivals, weddings | Disease, war on home soil |

**Rule of thumb:** keep rations at **Normal or better** in every county you intend to hold, and only spike taxes for a season or two when you have happiness to spare. A brief high-tax "harvest" of gold before a war is fine; a permanent high tithe bleeds population.

---

## 3. Farming — the engine

Food comes from four sources; diversify so one bad season doesn't starve you.

- **Grain.** Your staple. Grain fields lose **fertility** if farmed every year — use **Crop rotation** and let fields lie **Fallow** to recover. Watch the `Grain estimates` for *this* vs *future* seasons; a field must be sown a season ahead (`Sows next season`).
- **Sheep.** Produce **wool** (sell it) and **mutton** (eat it). The flock `grows/falls by` each season and needs **lambs** to expand.
- **Cattle.** Produce **dairy** (feeds people directly) and **beef**. Herd needs **calves** to grow. Cattle are the most efficient food-per-worker once established.
- **Ale/Hops.** Grow hops, brew ale. Ale is both a happiness luxury and a rationing option (`Drink ale`).

**Reclaim fields** (turn wasteland into farmland) whenever you have spare **Builders/labor and materials** — more fields is more ceiling. In new/conquered counties, expect to spend a couple of seasons just reclaiming and re-fertilizing before they pay off.

**Seasonal rhythm:** sow in **Spring**, tend in **Summer**, **harvest in Autumn**, survive **Winter**. Winter grows nothing — enter it with full stores.

---

## 4. Construction materials & the market

Three materials drive war: **Stone**, **Timber (Wood)**, and **Iron**. Assign **Quarryers** (stone), **Foresters** (timber), and buy **Iron** (few counties mine it). **Armorers** turn iron into **Weapons**.

**The Market Place** and its six merchants — **Mr. Goldpenny, Jock McTooth, Little Frank, Flemish Bill, Weasel Willy, Bernard Slap** — set **Prices** that move with supply and demand:

- **Sell high, buy low.** Prices swing seasonally. Dump surplus wool/grain when prices peak; stockpile iron when it's cheap.
- Don't sell food you'll need in Winter just because the price is good — famine costs more than any trade profit.
- Early game, a steady wool/dairy export funds your first castles and armies.

---

## 5. Castles — hold what you take

A county is only truly yours when its **castle** can't be stormed. Castle mechanics:

- **Wall height & Capacity.** Taller/thicker walls cost more Stone but survive bombardment longer; capacity caps how many troops (and how much population safety) the castle provides.
- Build with the **blueprint** editor; save good designs (`BLUEPRT1.DAT`) and reuse them.
- **Garrison** counts: spies estimate `a defending garrison of N`. A castle with strong walls and a full garrison can hold out for many seasons.
- **`Extend` / `Demolish`** as your borders shift; don't pour stone into a frontier castle you're about to push past.

**Priority:** wall up your **border** counties first — the ones with the most neighbors (see §8) — and your capital. Interior counties can stay lightly defended.

---

## 6. Armies, sieges & battle

**Raising troops.** Conscript peasants into **Spearsmen** (cheap, anti-cavalry), **Swordsmen** (strong melee), **Crossbows** and **Long bows** (ranged), plus **Knights** and hired **Mercenaries**. Every unit has an ongoing **seasonal upkeep** — a big idle army will bankrupt you. Raise armies *before* a campaign, disband *after*.

**Taking a county:** march in and defeat the field army (`The battle is between…`). Then the castle must be **besieged**.

**Siege engines:**
- **Catapult** — light, early bombardment.
- **Trebuchet** — heavier, smashes walls faster.
- **Battering rams** for gates, **Ladders** for escalade.

**Winning a siege** is a war of morale and supplies:
- Besiegers watch `Morale amongst the siege troops` — if it collapses, `Mutiny is imminent` and the siege breaks. Keep besiegers fed and don't drag sieges into deep winter.
- Defenders watch supplies and their own morale; a starved garrison will `Ask for quarter`.
- As attacker you can **storm** (bloody, fast) once walls crumble, or **starve them out** (slow, cheap) — pick based on your food and how long you can hold the surrounding county.

**Battle tips:** bring ranged units to thin the enemy before contact; spearsmen screen against knights; don't commit a tired, hungry army — `From an initial…` casualty reports punish overreach.

---

## 7. Diplomacy & the AI lords

Your rivals are **The Baron, The Knight, The Bishop, The Countess, The Earl** — each with a personality you'll read in their messages:

- **The Bishop / the Church** frames every war as heresy and will threaten and excommunicate over high taxes (`Attacking me is the same as attacking the Church`). Expect moralizing; he's still beatable.
- **The Baron / aggressive lords** are blunt and expansionist (`How dare you challenge my rule?!`). They respect strength and pounce on weakness.
- Offers of **alliance** and **treaty** are real but fickle — `A wolf in sheep's clothing is still a wolf`. Use alliances to buy time on one border while you crush another, but never trust one to hold when you look weak.
- Watch for telegraphed betrayals: `Treachery is afoot. This very day, I fear # plans your downfall.` and troop-movement "reassurances" (`Some of my men will march through #`) that often precede an attack.

**Diplomatic play:** stay allied with the strongest lord until *you* are the strongest, then turn on the weakest neighbor for easy counties. Send **Flattering** messages to cool a rival you're not ready to fight; **Threatening** ones only when you can back them up.

---

## 8. Opening moves & the map

You start in **one of six** locations. Per the game's own notes:

- **Central starts** give more borders — more routes for **immigrants** *and* **invaders**. High growth, high exposure. Play them aggressively and wall up early.
- **The south-western-most county** gets **harsher weather** (worse harvests) and has **only one neighboring county** for immigration. Safer, slower — play it as a turtle and expand deliberately.

**A solid opening (first 2–3 years):**
1. Set rations to **Normal+**, keep tithe **low** — grow population fast.
2. Balance labor: enough grain to never starve, then push **cattle** (best food efficiency) and a **wool/dairy export** for cash.
3. Assign **Quarryers/Foresters** and start a **castle** on your most-exposed border.
4. Bank Crowns; buy **iron** when cheap and start an **armory**.
5. Only when food is secure and one castle stands, raise your first **army** and take the *weakest* adjacent county.

**Mid-game:** snowball. Each new county is 2 seasons of investment before it pays; take them in a line you can defend, not scattered. Keep one strong mobile army plus garrisons, not many weak ones.

**End-game:** the map tracks your progress with milestones — *half of England*, *three-quarters*, *only two counties remain*. As you near victory the surviving lords gang up; keep a war chest and don't let upkeep or a winter famine undo you at the finish.

---

## 9. The Steward (once you hold 3+ counties)

The **Steward** auto-reallocates farm labor each season and reports counties that are over/understaffed and any **ration changes** (an early-warning for famine). He is a convenience, **not** an autopilot:

- He does **not** reclaim fields, build, or manage production — you still do all of that.
- He does **not** act on his own warnings. When he flags a labor shortage/excess or a ration change, *you* must respond.
- He costs clerk wages (`Stewardship costs`), and if unpaid you'll see `Clerks wages UNPAID`.

Turn him **On** to cut micromanagement across a wide empire; keep watching his reports.

---

## 10. Quick reference — dos & don'ts

**Do**
- Enter Winter with full food stores.
- Diversify food (grain + cattle + sheep + ale).
- Rotate/fallow fields to preserve **fertility**.
- Wall your border counties and capital first.
- Raise armies just-in-time; disband after campaigns.
- Sell surplus at price peaks; buy iron cheap.

**Don't**
- Run a permanent high tithe — population and the Church both punish it.
- Keep a huge idle army — upkeep will bankrupt you.
- Besiege into deep winter without supplies — your own troops mutiny.
- Trust an alliance when you look weak.
- Over-expand into counties you can't feed or defend.

---

*Companion to `ReverseEngineering.md`. For a sandbox that lets you experiment with these systems, see the trainer in `Trainer/` and its `README.md`.*
