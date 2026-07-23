# Railroad Tycoon — how to play & how to win

A player's guide to Sid Meier's Railroad Tycoon (MicroProse, 1990): the controls, the mechanics that
actually move money, and the strategy that takes you from a clerk with one bond to President of the
United States. Sourced from the game manual and Technical Supplement, a clean-room remake of the
game's formulas, and hands-on play of our copy. (Values are for the **original 1990** game.)

---

## 1. Getting started

At launch you choose graphics, sound, and input, then set up the game:

1. **Select area** — Eastern (Northeast) US, Western US, England, or Continental Europe.
2. **Difficulty** — Investor, Financier, Mogul, or Tycoon (this sets game length and the AI's aggression).
3. **Reality levels** — three independent toggles you can leave easy or make hard (see §5).
4. The world generates, a newspaper announces your railroad, and you must **identify a locomotive**
   (the copy-protection quiz — match the picture to the roster in §6; a wrong answer handicaps the
   whole game).

You begin on **1 January** of the map's start year with **$1,000,000** in the bank (a $500,000 4% bond
plus $500,000 of stock sold to investors) and an empty map.

## 2. Controls

Railroad Tycoon plays with mouse or keyboard (you pick at startup). The essentials:

**General**
- **Select / confirm:** `RETURN` or left mouse button.
- **Open a menu:** its first-letter key, or right mouse button. **`ESC`** exits a menu.
- **Move the cursor / Construction Box:** the numeric keypad (or left button). Hold **`Shift`** + keypad
  to lay track in a direction — **N=8, NE=9, E=6, SE=3, S=2, SW=1, W=4, NW=7**.

**Function keys**

| Key | Action |
|---|---|
| `F1` | Regional display |
| `F2` | Area display (centres on cursor) |
| `F3` | Local display (centres on cursor) |
| `F4` | Detail display (centres on cursor) |
| `F5` | Income statement |
| `F6` | Train income report |
| `F7` | Build a new train (needs an Engine Shop) |
| `F8` | Build a station |
| `F9` | Call the broker (stock market) |
| `F10` | Survey elevations (for grades/tunnels) |

**Other shortcuts:** `Shift+D` double track · `Shift+S` single track · `I` get information · `S`
override a signal · `C` centre the map · `Tab` train roster · `Alt+Q` quit. (Some shortcuts only appear
in keyboard-only mode.)

## 3. The core loop — how money is made

You make money by **hauling cargo between stations**. Everything else serves that.

**Stations and their catchment.** A station collects cargo (and demand) from the tiles around it — its
own square plus a radius:

| Type | Cost | Radius | Coverage | Maintenance/yr |
|---|---|---|---|---|
| Signal Tower | $25,000 | 0 | — | $1,000 |
| Depot | $50,000 | 1 | 3×3 | $2,000 |
| Station | $100,000 | 2 | 5×5 | $3,000 |
| Terminal | $200,000 | 3 | 7×7 | $4,000 |

Stations can't overlap, and must stay **≥5 tiles** from a competitor's. You may have up to **32
stations** (and up to 96 stations + depots + signal towers combined) and **32 trains**.

**Track.** Lay it with `Shift`+keypad; the base unit is 2 tiles. Double track (`Shift+D`) lets trains
pass; single track (`Shift+S`) is cheaper. Bridges: **stone** is safest, **iron** riskier, **wood**
cheapest but can't be doubled. Tunnels cost **$20,000/mile** (≤9 long, terrain height ≥80). **Climate**
changes right-of-way cost, cargo supply, and bond rates — lay track in good climate when you can.

**Trains and cargo.** A train pulls up to **8 cars** ($5,000 each). Cargo has supply and demand per
station: passengers and mail are fast, light and lucrative; bulk freight is heavier and pays on
distance. **Conversion chains** create demand: e.g. **coal → steel** (steel mill) → **manufactured
goods** (factory); **livestock → food** (meat packing); **wood → paper** (paper mill). Deliver a raw
good to a station that demands it and it's consumed for cash; the product then supplies onward.

**Station improvements** you'll want early:

| Improvement | Cost | Why |
|---|---|---|
| **Engine Shop** | $100,000 | **Required to build or upgrade trains** (`F7`) |
| Maintenance Shop | $25,000 | −75% maintenance for that fiscal period |
| Post Office | $50,000 | more mail revenue |
| Restaurant / Hotel | $25,000 / $100,000 | more passenger revenue |
| Switching Yard / Cold / Goods Storage | $50,000 / $25,000 / $25,000 | routing and anti-spoilage |

**Priority shipments** pay a bonus (minimum distance 6, up to 64 tiles; bonus ≥ $20,000, auto-cancelled
if it falls below $20,000). Naming a train (`Tab` roster) adds **+25% passenger revenue**.

## 4. Finance — bonds, stock, and rate wars

- **Bonds** fund early growth. You start with a 4% $500,000 bond (Western US bonds are $1,000,000, and
  cheaper, thanks to subsidies). Interest climbs as you borrow; at **9%** you can't sell more. Pay bonds
  down before you hit the ceiling.
- **Stock** (broker, `F9`). Buy/sell **10 shares at a time**, moving the price **±10%**. Buy your own
  stock cheap early and sell high; a **2:1 split** happens at $100. Serving a new city can issue 10,000
  new shares. Hold **≥50%** of your railroad and you can't be taken over — losing control ends the game.
- **Rate wars.** Build into a rival's city and the town council awards a **monopoly** on local service to
  the better operator (needs a 66% majority); the loser's track to that station is torn up (radius 3)
  and its investment forfeited.
- **Bankruptcy** (cash can't cover bonds): bonds are paid from cash, half the rest are wiped, treasury
  and rival-held stock are cancelled, and you can't lay track until you're solvent again.

## 5. Difficulty & reality levels

Difficulty sets how long the game runs and how hard the AI plays:

| Level | Game length |
|---|---|
| Investor | 40 years |
| Financier | 60 years |
| Mogul | 80 years |
| Tycoon | 100 years |

Three **reality switches** each toggle easy ↔ hard and each raises your **difficulty factor** (which
multiplies the retirement bonus): **No-Collision ↔ Dispatcher Operation**, **Basic ↔ Complex Economy**,
**Friendly ↔ Cut-throat Competition**. Harder settings = bigger score.

## 6. The scenarios

| Region | Start year | Currency | Notes |
|---|---|---|---|
| Eastern (Northeast) US | 1830 | $ | Dense cities; the classic starter map. |
| Western US | 1866 | $ | Long east–west routes pay best; cheaper $1M bonds. |
| Great Britain | 1828 | £ | Compact and high-demand. |
| Continental Europe | 1900 | £ | Map is **2× scale** (distances doubled); later, faster engines. |

**Locomotive rosters** (the quiz answer key; speed mph / horsepower / price $ / intro year — the intro
year is jittered ±4 at game start):

*United States:* 0-4-0 Grasshopper (20/500/$10k/1820) · 4-2-0 Norris (30/1000/$20k/1833) · 4-4-0
American (40/1500/$30k/1848) · 2-6-0 Mogul (25/2000/$30k/1851) · 4-6-0 Ten-Wheeler (45/2000/$40k/1868) ·
2-8-0 Consolidation (40/2500/$40k/1877) · 4-6-2 Pacific (60/3500/$60k/1892) · 2-8-2 Mikado
(45/3500/$50k/1903) · 2-6-6-2 Mallet (50/4500/$70k/1911, the quiz calls it **"Challenger"**) · 'F'
Series Diesel (70/3500/$75k/1916, quiz **"F3A-Series"**) · 'GP' Series Diesel (60/4000/$75k/1930).

*England:* 2-2-0 Planet · 2-2-2 Patentee · 4-2-2 Iron Duke · 0-6-0 DX Goods · 4-2-2 Stirling · 4-2-2
Spinner · 0-8-0 Webb Compound · 4-4-0 Hamilton · 4-6-2 Gresley (A1) · 4-6-2 Class A4.
*Europe:* 0-8-0 Compound · 4-4-0 Hamilton · 4-6-2 Gresley · 4-6-2 Class A4 · 6/6 Crocodile · Class E18 ·
4-8-4 242 A1 · V200 · Re 6/6 · TGV. (Full stats are in the trainer's **Locomotives** tab.)

## 7. How to win

**The end condition.** A game runs to the difficulty's year limit, or until you **retire**, or until a
rival takes control of your railroad (instant game over). On retirement you receive a job title on a
ladder from **Hobo** (worst) to **President of the United States** (best), computed from your final net
worth, years served, difficulty factor, how many railroads you control, and whether you were ousted.

**Retirement bonus, in words** (the remake mirrors the original): start from `net worth ÷ 10`, multiply
by a **service-speed factor** that rewards retiring *sooner* (`1000 ÷ (age + 20)` percent), by a
**Railroad Mogul** bonus if you control rival railroads (`(25% × 2^(n−1)) + 100%` for n controlled),
by **75%** if you were forced out, and by your **difficulty factor**. Net effect: **high net worth,
early, on a hard setting, while controlling rivals** is the maximum score.

**A build order that works**
1. **Connect two nearby high-demand cities first** — passengers + mail are fast, light and high-value.
   Put an **Engine Shop** at your hub so you can add trains (`F7`).
2. Keep early routes **short and dense**; add a Post Office / Restaurant / Hotel to squeeze more out of
   passengers and mail. **Name** your trains for the +25% passenger bonus.
3. Fund expansion with **bonds**, then repay them before the 9% ceiling. Watch **climate** and lay track
   when it's cheap.
4. Work the **stock market**: accumulate your own shares cheap, keep **≥50%** so you can't be taken
   over, and sell into strength. Late game, **take over the AI railroads** for the Mogul multiplier.
5. **Locomotives:** start on the cheap starter ($10k Grasshopper/Planet); in the US the **4-4-0
   American** is the balanced early workhorse and the **4-6-2 Pacific** the fast passenger engine; move
   to **F/GP diesels** late. Match engine power to grades and consist length.

**Common pitfalls:** failing the startup loco quiz; over-leveraging to 9%; overbuilding track in bad
climate; skipping Maintenance Shops; letting a rival buy up your stock; and running long low-value bulk
routes when short passenger/mail routes pay far better early.

---

## Where the trainer fits in

The trainer edits the live game's memory: **Auto-locate cash** pins your treasury (and the year) with
one click, and **Set max cash** parks you at the game's own $30M ceiling. That removes the money
pressure so you can enjoy the *building* — but the score above rewards net worth built *quickly on a
hard setting*, so if you're chasing the "President" rank, a frozen bankroll is a sandbox, not a
high-score run. Freeze the **Year** to stop the retirement clock and build forever.
