# Autoduel — Strategy Guide & Walkthrough

*Origin Systems, 1985. Set in the Northeastern United States, Fall 2030.*

This guide combines classic play knowledge with the concrete numbers recovered
by reverse-engineering the game (see [reverse-engineering.md](reverse-engineering.md)).
The in-game text strings quoted below (rumors, mission briefings, passwords)
were extracted directly from `AUTODUEL.OVL`, so this walkthrough reflects
**this exact build** of the game.

---

## 1. The goal — how to win

Autoduel has an open "sandbox" career (earn money, prestige, and combat
Division rank by running deliveries and winning arena duels), **and** a hidden
main quest that ends the game with a victory screen. To win you must complete
a chain of FBI courier missions that culminates in busting the crime boss
"Mr. Big". The final screen reads:

> *"Congratulations for completing AUTODUEL … Thanks and happy gaming from the
> folks at Origin Systems."*

You then continue playing with the same driver if you wish.

### The victory chain (rumor → password → delivery)

The quest is a breadcrumb trail. You pick up **rumors** at Joe's Bars and shop
counters; each points to the next town and gives a **password**. The passwords
recovered from the game data, in order, are:

| # | Password | Where you get the job | Where it goes |
|---|---|---|---|
| 1 | *(start)* — "They need a hand at the Harrisburg Arena. Stop by the Scranton Weapon Shop." | rumors bootstrap the chain | — |
| 2 | **SAN ANTONIO ROSE** | Dover Weapon Shop | decoy prize → Manchester Arena |
| 3 | **LITTLE BIG HORN** | Buffalo Salvage Yard | proof of fixed duels → FBI |
| 4 | **GREAT WHITE WHALE** | New York (clone-heart courier) | clone heart → Boston Gold Cross |
| 5 | **RUMPLESTILTSKIN** | FBI, New York (final briefing) | bootleg brain tape from the **Outlaw HQ, Watertown** → back to the **FBI in New York** |

The final briefing (FBI, verbatim from the ROM):

> *"Mr. Big has gotten one of his men into Gold Cross and wants to set up a
> bootleg brain tape operation. … Your mission … is to go to the Outlaw HQ,
> which we know is in Watertown, and pose as their courier. Take the brain tape
> they will give you, and bring it here. It is vital evidence. The password they
> will want is 'Rumplestiltskin'."*

**Critical warning for the finale (also from the ROM):** *"And just to make sure
your heart is in this, you won't have a clone to fall back on."* During the last
run your clone will not save you. If you are killed leaving the Outlaw HQ or in
Joe's Bar as a marked "cop-lover," it is game over — so go in with a strong,
fully repaired car, full ammo, and full health/body armor.

### Following the trail in practice

* Buy a **$5 drink at Joe's Bar** and choose **"Listen for rumors"** in each
  Fortress Town. Rumors rotate; note the ones that name a town + a keyword.
* The keyword *is* the password for the mission you'll be offered there.
* Deliver quest cargo like any courier job — you need a car with a free cargo
  space and, for some jobs, minimum Division/prestige.
* After the final delivery you'll read *"Read tomorrow's paper to see what
  happens…"* then get the completion screen.

---

## 2. Getting started (the first hour)

You begin in **New York, Friday January 1 2030**, with **$2,000**, no car,
Prestige 0, Health 3, and starting skills (Driving/Marksmanship/Mechanic). You
cannot survive on foot, so the first job is a car.

1. **Build a cheap, tough starter car** at an Assembly Plant (New York has
   Magnum Motors; Pittsburgh and Boston also build). A **Subcompact, Light
   chassis, Improved suspension** is the classic starter — cheap, light, and
   nimble. Fit a **Machine Gun front** ($1,000, 150 lb, 1 space) and put your
   remaining money into **armor** (front and back first).
2. Keep **weight under the chassis max** (a Light subcompact caps at **2,070 lb**)
   and leave at least **one cargo space** free so you can take courier jobs.
3. **Charge the battery** ($50 at a Garage) — you can't leave a garage with a
   flat pack.

### Cash bootstrapping

* **Couriering** at the AADA office is the steady income: pick "Courier tasks,"
  accept jobs whose weight/value your car can carry (heavier/valuable loads
  need more armament and experience — the game will refuse *"lightly armed for
  a payload of this value"* or *"someone with a little more experience"*).
* You may carry **up to three payloads at a time** (AADA rule). Combine
  deliveries heading the same direction.
* **Salvage** everything after a duel and sell it at the Salvage Yard
  ("Sell salvaged goods").
* The rumor *"Try Atlantic City if you're ever low on cash"* points at the
  **Casino** (Poker and Blackjack). It's variance, not a plan — but the game
  will let you *"break the bank"* (the money field caps at **$999,999**).

---

## 3. Combat and the arena

Winning duels raises **Prestige** and your **Division** (combat rank), which in
turn unlocks higher-tier arena events and better courier jobs.

* **Arena events** (from the schedule board): **Amateur Night**, **Division 5**,
  **Division 10**, **Division 15**, **Division 20**, **Unlimited**, and periodic
  **City Championships**. Your car must *qualify* for the event's Division.
* **Practice** ($20) to learn the controls and the arena with no risk to your
  record.
* Amateur Night is the safety net: *"come back for amateur night if ya need
  another one"* — you can win a replacement car there if yours is destroyed.
* **Marksmanship** governs your hit chance; **Driving** governs handling/evasion;
  **Mechanic** lowers repair cost and time and is trained at the Garage
  ("Take mechanic lessons $500").

### Weapon reference (exact catalog values from the game data)

| Weapon | Cost | Weight | Dmg/shot | Spaces | Notes |
|---|---:|---:|---:|---:|---|
| Machine gun | $1,000 | 150 | 3 | 1 | Best value; buy ammo cheap |
| Flamethrower | $550 | 465 | 3 | 3 | Short range, heavy |
| Rocket launcher | $1,050 | 215 | 3 | 3 | |
| Recoilless rifle | $1,550 | 315 | 5 | 3 | High damage |
| Anti-tank gun | $2,050 | 615 | 6 | 4 | Heaviest hitter, very heavy |
| Laser | $8,000 | 500 | 2 | 2 | No ammo needed (self-powered) |
| Minedropper | $550 | 165 | 3 | 3 | Rear-facing area denial |
| Spikedropper | $150 | 40 | 5 | 2 | Cheap dropped weapon |
| Smokescreen | $300 | 40 | 5 | 2 | Break enemy targeting |
| Paint sprayer | $400 | 25 | 2 | 1 | Blind opponents |
| Oil jet | $250 | 25 | 3 | 2 | Rear defense |
| Heavy rocket | $200 | 100 | 2 | 1 | One-shot, no reload |

**Build priorities:**
* Front **Machine Gun** early (cheap ammo, 1 space); it carries most of the
  early game.
* Add a **rear dropped weapon** (Oil jet or Spikedropper) so pursuers eat damage.
* When cash allows, a front **Recoilless rifle** or **Anti-tank gun** for the
  Division-15+ events — but watch weight; upgrade to a larger chassis/power
  plant first.
* The **Laser** is the endgame front weapon: expensive but never runs dry, which
  matters on long quest runs where you can't restock mid-mission.

### Armor and survival

* Armor is tracked **per facet**: Front, Back, Left, Right, Underbody. Front and
  Back take the most fire; armor them heaviest.
* A **larger power plant** (Medium/Large) buys speed/acceleration and weight
  budget for armor — balance top speed against total weight vs. the chassis cap.
* **Body armor** (personal) protects you in on-foot pistol duels and muggings
  ("You were just mugged. They took $…"). Keep a few units.

---

## 4. Staying alive between cities — cloning

Health is only **3 points**. Death is permanent **unless you have a clone**:

> *"four-fifths of autoduellists without clones don't make it past a month of
> duelling."*

* At **Gold Cross** (New York, Boston): **Create a clone $5,000**, **Update a
  braintape $3,000**, **Undergo treatment $100–200**.
* Buy a clone as soon as you can afford it, and **update the braintape**
  periodically so a restored clone keeps your latest skills. When you die with a
  clone: *"Your clone is now being activated."*
* Without a clone, death shows *"Since you don't have a clone, the game is now
  over."*
* **Reminder:** the *final* quest run deliberately strips your clone safety net —
  don't attempt it under-equipped.

---

## 5. Travel, road combat, and the map

Roads between Fortress Towns are rated **lightly / moderately / heavily**
"trafficked by outlaws." Heavier roads mean more ambushes (more salvage, more
risk). Check **Road Status** at the AADA office before long hauls.

Fortress Town facilities (from `automap.txt`):

| Facility | Where to find it |
|---|---|
| Assembly Plant (buy cars) | New York, Boston, Pittsburgh |
| Gold Cross (cloning) | New York, Boston |
| Weapon Shop | most towns (not Philadelphia) |
| Garage (repair/charge/store/lessons) | New York, Albany, Harrisburg, Washington, Baltimore(-adjacent), etc. |
| Salvage Yard | Buffalo, Scranton, Boston, Washington, Pittsburgh, Harrisburg |
| Arena | many towns; Championships rotate to larger venues |
| Joe's Bar (drinks, rumors, sell courier tasks) | New York, Boston, Pittsburgh, Philadelphia, … |
| Casino | Atlantic City |
| FBI Office | New York (main quest hub) |
| Origin Systems / Pet Store / Hotel | Manchester / Philadelphia / Baltimore (flavor + a couple of quest stops) |

**Downtown weapons rule:** in New York all vehicle weapons *"must be covered or
unloaded downtown"* and the militia enforces it — don't roll in hot.

### Map (Eastern Division)

```
        Watertown
            |
        Syracuse ------------------ Manchester
       /        \                        \
  Buffalo        Albany --- ... --------- Boston
                   |                        |
                Scranton                Providence
                 /   \                     |
        Pittsburgh   Harrisburg --- New York
             \            \           /
              \        Philadelphia -/
               \        /    \
           Baltimore   Dover  Atlantic City
               |
           Washington
```

---

## 6. Efficient career order (recommended)

1. **New York:** build the Subcompact + MG starter car; charge battery.
2. Run **local courier jobs** (New York ↔ Philadelphia ↔ Atlantic City) to reach
   ~$5,000; salvage roadside outlaws for extra parts.
3. **Buy a clone** ($5,000) at New York Gold Cross the moment you can.
4. Grind **Amateur Night → Division 5 → 10** to raise Prestige/Division; reinvest
   in armor, a rear dropped weapon, then a bigger gun/plant.
5. Start **listening for rumors** at every Joe's Bar; write down town+keyword
   pairs — these seed the FBI quest chain (§1).
6. Work the quest deliveries (SAN ANTONIO ROSE → LITTLE BIG HORN → GREAT WHITE
   WHALE) as they come up, keeping the braintape updated.
7. For the **finale** (RUMPLESTILTSKIN): fully repair, full ammo (or a Laser),
   full health + body armor, **then** take the Watertown Outlaw HQ job and run
   the tape back to the New York FBI. Survive the exit and you win.

---

## 7. Money & cost quick-reference (verified values)

| Thing | Cost |
|---|---|
| Joe's Bar drink | $5 |
| Battery charge (Garage) | $50 |
| Take a car out of storage | $50 |
| Hotel night (Baltimore) | $200 |
| Gold Cross treatment | $100–200 |
| Arena practice | $20 |
| Mechanic lesson | $500 |
| Braintape update | $3,000 |
| Create a clone | $5,000 |
| Machine gun / ammo | $1,000 / cheap per round |
| Laser | $8,000 |
| **Money cap** | **$999,999** |

*Prices for specific cargo pay, car bodies, and armor scale with your car and
Division; the AADA/shop dialogs will quote exact figures in-game.*
