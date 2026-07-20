# Syndicate Plus — Strategy Guide

*A practical, systems-driven guide to conquering the world in **Syndicate** and the **American
Revolt** data disk. Every recommendation here is grounded in the mechanics recovered in
`Syndicate-Reverse-Engineering.md` — the persuasion-point math, the mod/version gating, the tax/mood
economy, and the permanent-death agent pool.*

---

# 1. The One Thing to Understand First

Syndicate is not a shooter with a budget attached — it is an **economy game with a shooter attached**.
You win by turning **conquered territory → tax income → research & mods → stronger agents → more
territory**. Three hard constraints shape every decision:

1. **Agents are permanent.** A dead agent is gone with all its gear and experience. Lose all 8 and
   it's **game over**. You can only *grow* the roster by **persuading enemy agents**.
2. **One research slot.** You develop exactly **one** item at a time, so research order is a
   strategy in itself.
3. **Tax is a risk dial.** High tax funds everything but breeds **rebellion**, which can hand a
   territory to a rival and cost you a re-conquest mission.

Optimise the loop, not the firefight.

---

# 2. The Economic Engine (do this every time you're on the World Map)

**After every won mission, tour your territories and tune tax.**

- Push tax **up** while mood stays **Content or better**. Bigger population = more headroom.
- The instant a territory reads worse than *Content*, **back the tax off** a few points. A rebellion
  costs you a whole extra mission (and risks the territory) — far more than the tax you'd have
  skimmed.
- Income accrues **over time**, so raise taxes **early** on your safest, highest-population regions
  (your Western-European home cluster) and let them compound while you campaign elsewhere.
- Treat your **home base (Western Europe)** as sacred: keep it happy and defended. Rival agents work
  to destabilise your own back yard while you're away.

**Rule of thumb:** aim to always have enough Budget banked to *fully equip and re-mod four agents
from scratch*, so a single bad mission never bankrupts you.

---

# 3. Research Priorities (spend the single slot wisely)

You only research one thing at a time, and **Version 3 of a mod needs its Version 2 finished first**.
Recommended early-to-mid order:

1. **Uzi** — cheap ammo, fast fire, medium range. It is the backbone weapon for the first third of
   the game. Rush it.
2. **Legs V2 → V3** — mobility is survival. Fast agents dictate every engagement, dodge fire, and
   actually reach the evac zone. This is the highest-value mod line.
3. **Brain V2 → V3** — the recruitment multiplier (see §5). A V3 Brain changes what's *possible*,
   not just what's easier. Prioritise it before you plan any capture-heavy mission.
4. **Chest V2** — unlocks **Self-Destruct** (`Ctrl-D`) and adds durability. The panic button that
   turns a doomed agent into a bomb.
5. **Minigun / Laser / Gauss** — heavy firepower for the back half. Don't buy a Minigun before you
   have **Arms** mods, or its weight cripples the carrier.
6. **Arms V2/V3, Heart V2/V3, Eyes V2/V3** — quality-of-life and survivability; fold these in as
   Budget allows.

**Funding tempo:** the dev curve is *100% in 10 days* at base cost. When you're cash-rich and need a
weapon **now**, click `Funding +` to compress it toward the **1-day** minimum. When you're saving
for equipment, click `Funding –` to stretch it toward 10 days and reclaim cash. Never leave the slot
idle — something should always be cooking.

---

# 4. Building an Agent (loadout doctrine)

Each agent starts with a free Pistol. That's a backup, not a plan. A solid mid-game squad of four:

| Role | Primary | Support | Key mods |
|---|---|---|---|
| **Point / rusher** | Uzi | Medikit | Legs V3, Chest V2+, Heart |
| **Heavy** | Minigun or Flamer | — | **Arms V2+** (weight), Chest, Legs |
| **Sniper / overwatch** | Long-Range Rifle or Laser | Scanner | Eyes V3 (accuracy), Legs |
| **Recruiter** | **Persuadertron** | Access Card | **Brain V3**, Legs V3 |

Doctrine notes:

- **Always carry a Scanner** on at least one agent — it pinpoints the mission target with an
  identifier beam and reveals enemies before they reveal you.
- **One Medikit per agent** is single-use; treat it as an emergency, not a healbot.
- **Access Card** disguises the carrier as police and diverts police units — invaluable on missions
  through patrolled city cores or behind security doors.
- **Transfer, don't waste.** Between missions, move surplus gear off the dead/benched and onto active
  agents rather than re-buying. **Reload** partially-used weapons cheaply instead of buying new;
  **Sell** anything the next mission won't need to top up Budget.
- **Grab everything.** Loot weapons off every corpse (yours and theirs). Free ammo and free guns.
  Self-destruct an out-of-ammo agent *early* so survivors can scavenge the wreckage and fight on.

---

# 5. The Persuadertron: Your Win Condition

Recruitment is how you beat the permanent-death constraint and snowball an army. The math (from the
reverse-engineered rules):

- Followers you already control contribute **persuasion points**:
  `Civilian = 1, Guard = 3, Policeman = 4, Enemy Agent = 32`.
- The **cost** to convert a target drops sharply with **Brain version**:

| Brain | Guard | Policeman | Enemy Agent |
|---|---|---|---|
| None | 4 | 8 | 32 |
| V1 | 2 | 4 | 16 |
| V2 | 1 | 3 | 11 |
| **V3** | **1** | **2** | **8** |

**The snowball (with Brain V3):**

1. Walk into a crowd and persuade **civilians** (always free to convert). Gather a conga line.
2. Each civilian is worth 1 point; roll them up until you can flip a **guard** (needs just 1 point at
   V3), then **police** (2 points).
3. Now you have high-value followers. `1 civilian + 1 guard + 1 police = 8 points` → enough to
   **persuade an enemy agent** — who is worth a colossal **32 points** and **joins your Cryo
   Chamber** permanently.
4. One enemy agent alone nearly funds persuading the *next* enemy agent. Flip a whole rival squad and
   walk them home.

**Cash bonus:** any *non-agent* persuaded personnel that survive to the evac zone are **paid out** at
debrief. Herding a crowd to safety is free income; herding enemy agents home is free *soldiers*.

**Golden rule:** never grind capture missions with a low-version Brain. Research **Brain V3 first**,
then go recruiting. The difference between 32 points and 8 points per enemy agent is the difference
between "impossible" and "trivial".

---

# 6. In-Mission Tactics

## 6.1 IPA management (your live tuning knobs)

- **Perception high** almost always. It improves accuracy and makes agents spot threats earlier —
  pure upside in a firefight.
- **Adrenaline** governs reaction/move speed; crank it when you need to sprint or trade fire fast.
  But **high Adrenaline + low Intelligence = erratic** (early, wide shots) — pair it with decent
  Intelligence.
- **Intelligence high** makes agents self-preserve (evade, break off suicidal moves); **low**
  Intelligence makes them walk blindly forward — occasionally what you want when charging a target
  through fire.
- **Watch dependency.** Every injection darkens the bar and shifts the centre line right, so the same
  dose does less next time. **Rest** agents (retard the bars) whenever they're not in danger; a
  rested agent gives a much bigger boost when you spike it later. Don't run the whole mission redlined.
- **Overwatch trick:** before leaving an agent as a stationary lookout, **crank its Levels** — it
  will defend itself and fire on threats while awaiting orders.

## 6.2 Movement, cover & the Scanner

- Lead with the **Scanner** view. Note enemy-agent blips (red), police (blue), civilians (white
  specks) and the flashing **target**. Plan the approach before you commit.
- **Draw weapons late.** An agent with a weapon out is flagged as Syndicate and police converge.
  Select your weapon just before contact; de-select (holster) after, especially near police.
- Use **Group Mode** (the centre icon) to move and equip all four as one when repositioning — but
  remember IPA changes in Group Mode hit **all** agents at once.
- Approach targets from angles with cover and an escape lane toward the **evac zone** — the target(s)
  and any persuaded VIPs must physically reach evac for the mission to count.

## 6.3 Panic & Self-Destruct

- **Panic Mode** (both mouse buttons): instant auto-fire with maxed Levels. Your "oh no" button when
  a firefight collapses — effective but wasteful of ammo and IPA.
- **Self-Destruct** (`Ctrl-D`, requires **Chest V2/V3**): detonate to wipe the area. Two great uses:
  (1) an out-of-ammo agent surrounded by rivals takes them all with it, seeding loot for survivors;
  (2) an unarmed agent that *reaches the target* can complete the mission with the blast. Keep other
  agents well clear.

---

# 7. Protecting the Roster (don't go broke on bodies)

- **Never field agents you can't afford to lose.** Early on you have only 8 lives total.
- Respond to hits by **switching active agent** and returning fire, rather than tanking with one
  agent until it dies. Spread damage; keep everyone alive.
- If a mission is clearly lost, **extract** what you can — the dead stay dead and drop all their
  investment. A retreat with the squad intact beats a wipe.
- **Refill the pool by persuading enemy agents** (§5) faster than you lose your own. A campaign where
  your Cryo Chamber is *growing* is a campaign you're winning.

---

# 8. World-Conquest Strategy

- You expand only into **adjacent, destabilised** territories (they flash on the map after a win).
  Plan a **contiguous sweep** rather than scattered grabs, so your income base stays defensible.
- **Read the enemy before you invade.** Match your loadout to the occupying rival (§9): the Tao and
  I.I.A. demand heavy firepower and good mods; the Jihad and TLC can be taken lighter.
- **Consolidate before you push.** After taking a region, set sustainable tax, confirm your home
  cluster is happy, and bank Budget. A slower, solvent advance beats a fast, bankrupt one.
- **Info & Map Enhance** at the Mission Brief cost Budget but reveal the objective and terrain — buy
  them for hard or unfamiliar territories; skip them on trivial ones to save cash.

---

# 9. Know Your Enemy (rival Syndicates)

| Rival | Threat profile | How to fight them |
|---|---|---|
| **The Tao** | Disciplined, hi-tech, accurate | Out-range them (Laser/Long-Range); don't brawl up close |
| **I.I.A.** | Heavy weapons, high collateral | Expect Miniguns/explosives — use cover, Energy Shield, mobility |
| **The Castrilos** | Vicious, aggressive rushes | Kite with fast Legs; Flamer/Shotgun for the crowds they throw |
| **Sphinx Inc.** | Fanatical, fight to the last | Bring overkill — they don't rout; Gauss/Laser to end fights fast |
| **Executive Jihad** | Few, poorly armed, reckless | Beatable light, but don't get careless against zealots |
| **Tasmanian Lib. Consortium** | Erratic aim, not sadistic | Their fire is wild — press the advantage; punish the misses |

---

# 10. Quick Reference

**Weapon roles (internal order):**

| Weapon | Use it for |
|---|---|
| Persuadertron | Recruiting — your army-builder |
| Pistol | Emergency backup only |
| Uzi | General-purpose workhorse |
| Shotgun | Close-quarters burst damage |
| Minigun | Sustained heavy fire (needs Arms mods) |
| Flamer | Anti-vehicle & crowd control, point-blank |
| Laser | Long-range piercing / anti-vehicle / sniping |
| Long-Range Rifle | Assassination & overwatch |
| Gauss Gun | Explosive alpha vs tanks & clusters (3 rockets) |

**Equipment:** Scanner (always), Medikit (emergency), Access Card (patrolled/locked maps), Time Bomb
(area denial), Auto Mapper (navigation), Energy Shield (short invulnerability — burst through fire).

**Priority checklist each cycle:**

1. Tune tax on every owned territory (up to Content, back off if unrest).
2. Keep the research slot busy — Uzi → Legs → **Brain V3** → Chest → heavy weapons.
3. Re-mod & re-equip the squad; transfer/reload/sell instead of re-buying.
4. Carry a Scanner and a Persuadertron+Brain-V3 recruiter.
5. Keep the roster growing via persuasion; never field your last agents recklessly.

**Hotkeys:** `Esc` quit to menu · `P` pause · `F1` SFX · `F2` music · `Ctrl-D` self-destruct
(Chest V2/V3) · `Space` debrief · Cursor keys pan.

---

*Cross-reference: for the underlying data — item/territory enumerations, the persuasion formula, mod
version gating, save format and engine internals — see `Syndicate-Reverse-Engineering.md`.*
