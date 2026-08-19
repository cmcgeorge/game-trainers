# Roadwar 2000 — strategy guide

America, 1999. A engineered plague has emptied the cities, the bombs that did go off have left
the rest glowing, and an invader who inoculated their own troops first is coming ashore. You are
a road gang leader. The Government Underground Biolab has eight scientists scattered across the
continent who between them can finish a vaccine, and it wants you to bring them home.

This guide covers what the game expects of you, what the engine actually does under the hood, and
where everything is. Numbers quoted as "the engine" come from `docs/reverse-engineering.md`, where
they were read out of the game's own tables rather than out of the manual — the two disagree in a
few places, and the engine wins.

---

## 1. Getting in

`START.EXE` under DOSBox. The opening sequence asks, in order:

1. **Press any key** at the title, then at the credits.
2. **Display mode** — `RGB`, `COMPOSITE` or `B/W`. Space bar accepts the highlighted one; any
   other key cycles. RGB is right for DOSBox.
3. **Keyboard setup (1 or 2)** — see below. This is the one choice worth thinking about.
4. **Adjust screen? (y/n)** and **Adjust colour?** — `n` to both unless the picture is off-centre.
5. **DO YOU WISH TO RECALL A SAVED GAME?** — `Y` loads, `N` starts fresh and asks for a gang name
   of up to 20 characters.

### The two movement schemes

Both use the number row; the difference is which digit means which direction.

```
        SETUP 1  (the manual's rosette)      SETUP 2  (numeric-keypad style)

              8   1   2                            7   8   9
                \ | /                                \ | /
              7 - + - 3                            4 - + - 6
                / | \                                / | \
              6   5   4                            1   2   3
```

Setup 1 is what the manual and every printed reference use, so its numbering is the one quoted
everywhere. Setup 2 matches the keypad layout your fingers already know. Pick one and stay with
it — the game does not tell you which is active once you are past the prompt.

---

## 2. Commands

Not every command is available from every menu; the game silently ignores the ones that are not,
so pressing a key you are unsure about costs nothing.

| Key | Command | Notes |
| --- | --- | --- |
| `1`–`8` | Move one square | Costs about two hours and one move's fuel |
| `A` | Abandon vehicle | Prompts for the vehicle number; the rest renumber |
| `C` | Scout the city | Send troops by rank to find out who lives here. Some do not come back |
| `D` | Drop supplies | Any amount of food, tires, fuel, guns or medical |
| `E` | Empire status | Cities you control, and progress towards winning |
| `F` | Fix tires | Consumes spare tires from stores to repair flats |
| `G` | Gang status | Two pages: gang stats, then vehicle stats — `<` `>` to page vehicles |
| `H` | Heal with antitoxin | One dose inoculates 50 crew. All at once or they reinfect each other |
| `I` | Initialise save disk | Formats a blank disk (a no-op under DOSBox) |
| `K` | Check cache | What you have stashed in the town you are standing in |
| `L` | Search for loot | Works everywhere except forest and desert |
| `M` | Manpower report | Gang members by rank |
| `P` | Search for people | Usually finds a foot-gang; sometimes an agent, a healer or a scientist |
| `Q` | Quit |  |
| `R` | Recall saved game |  |
| `S` | Save game | See §8 on where the file actually goes |
| `T` | Transfer to/from cache | Metropolitan areas only |
| `U` | Use the Radio Direction Finder | Then `1` or `2` to pick a scientist's homer |
| `V` | Search for vehicles | The main way to grow the fleet |
| `W` | Damage report | Tactical combat only |
| `X` | Examine supplies | A quick one-screen inventory |

Two reading notes for the `G` screen, both from the engine:

* **The fuel it shows is not the fuel you have.** It prints your stored fuel less two moves' worth
  per vehicle, because every vehicle keeps that much in its tank and that reserve does not eat
  cargo space. `X` shows the real stored number. With nine vehicles burning 49 a move, `G` reads
  98 lower than `X`.
* **A `*` beside a supply means you have the special version of it** — snow tires beside TIRES,
  and a fuel special beside FUEL that roughly halves your consumption. These are found while
  looting and are worth going out of your way for.

---

## 3. The gang

### Crew

Five grades, best first: **armsmaster, bodyguard, commando, dragoon, escort**. Grade is a
survival multiplier — it applies to every roll a member makes, in fire combat, in boarding, in
scouting parties, and in the accident and disease checks that quietly thin your ranks each night.

Each member eats one unit of food per night. That single line governs the early game: a gang of
400 eats 400 food a night, and 400 food takes 400 of your cargo spaces.

Recruit by pressing `P`, then sending envoys. The better the prospect, the less likely they are to
join. A good politician does the talking for you, and sometimes talks a rival politician into
joining instead of fighting.

### The three cronies

Only one of each can travel with you, and accepting a new one dismisses the old. Each has a skill
level the game never shows you — you judge it from results. In the engine they are three
independent bytes, and zero means you have none.

* **Doctor** — fewer casualties in foot combat, fewer losses to disease and accident.
* **Drill sergeant** — fewer desertions, more promotions. Promotions are how a rabble of escorts
  becomes a gang of armsmasters, so this one compounds.
* **Politician** — your envoy and your negotiator, and the reason you can walk out of a
  bureaucrat-run town without paying.

Get all three early. They cost nothing to keep.

### Supplies

| Supply | Takes cargo space | Notes |
| --- | --- | --- |
| Food | yes | One per crew member per night |
| Tires | yes | Consumed repairing battle damage |
| Fuel | yes | One move costs the sum of your vehicles' consumption |
| Guns | yes | One gun arms one crew member; unarmed men fall back to crossbows |
| Medical supplies | yes | What healers charge |
| Ammo | **no** | One round per shot fired |
| Antitoxin | **no** | One dose per 50 crew, and everyone must be dosed together |

Ammo and antitoxin being weightless is a genuine asymmetry worth exploiting: carry a great deal of
both, and keep the space for food and fuel.

### Caches

Every metropolitan area holds a cache of up to **255 each** of food, tires, fuel, guns and medical
supplies — that ceiling is a byte in the engine, not a soft limit. Transfer with `T` while you are
standing in the town, check with `K`. Setting up caches along a route you will travel again is the
single best use of a slow early game.

---

## 4. Vehicles

You start with one vehicle and a ceiling of six. **The ceiling rises by one every time you fight a
*tactical* road battle to a finish** — not abstract, not quick — to an absolute maximum of 15.
That is the whole reason to fight the long way round.

Find vehicles with `V`. The table below is the engine's, not the manual's; the two disagree on the
motorcycle's and sidecar's front armour and on the bus's topside capacity, and this is what the
game uses.

| Vehicle | Mass | Str | MPH | Man | Brk | Acc | Missile L/R/F/B | Armour L/R/F/B/T | Vol | Tires | Board L/R/F/B | Int | Top | Fuel | Spaces |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Motorcycle | 1 | 3 | 100 | 4 | 2 | 2 | 2/2/2/2 | 0/0/1/0/0 | 1 | 2 | 1/1/0/1 | 2 | 0 | 1 | 5 |
| Sidecar | 2 | 5 | 60 | 4 | 2 | 2 | 3/3/3/3 | 0/1/1/1/0 | 1 | 3 | 1/1/0/2 | 3 | 0 | 1 | 20 |
| Compact convertible | 3 | 8 | 80 | 3 | 2 | 1 | 3/3/2/2 | 1/1/1/1/0 | 2 | 4 | 1/2/0/2 | 6 | 0 | 2 | 45 |
| Compact hardtop | 3 | 8 | 70 | 3 | 2 | 1 | 4/4/4/4 | 2/2/2/2/0 | 2 | 4 | 0/1/2/0 | 4 | 4 | 2 | 45 |
| Midsize convertible | 5 | 13 | 90 | 2 | 2 | 1 | 3/3/2/3 | 1/1/1/1/0 | 2 | 4 | 2/3/0/3 | 8 | 0 | 3 | 125 |
| Midsize hardtop | 5 | 13 | 80 | 2 | 2 | 1 | 4/4/5/6 | 2/2/2/2/0 | 2 | 4 | 1/2/2/0 | 5 | 6 | 3 | 125 |
| Sports car convertible | 4 | 10 | 120 | 3 | 2 | 2 | 3/3/2/3 | 1/1/1/1/0 | 2 | 4 | 2/3/0/2 | 6 | 0 | 4 | 80 |
| Sports car hardtop | 4 | 10 | 120 | 3 | 2 | 2 | 4/4/4/4 | 2/2/2/2/0 | 2 | 4 | 0/1/2/0 | 4 | 4 | 4 | 80 |
| Station wagon | 6 | 15 | 80 | 2 | 2 | 1 | 6/6/5/6 | 2/2/2/2/0 | 2 | 4 | 2/3/3/3 | 8 | 9 | 3 | 180 |
| Limousine | 8 | 20 | 100 | 2 | 2 | 1 | 6/6/5/6 | 2/2/2/2/0 | 2 | 4 | 1/2/3/3 | 8 | 9 | 4 | 320 |
| Van | 7 | 18 | 70 | 2 | 2 | 1 | 8/8/5/6 | 2/2/2/2/0 | 2 | 4 | 0/3/3/3 | 11 | 12 | 3 | 245 |
| Pickup truck | 9 | 23 | 80 | 2 | 2 | 1 | 6/6/4/3 | 1/1/2/1/0 | 2 | 4 | 4/5/0/3 | 14 | 2 | 4 | 405 |
| Off-road convertible | 6 | 15 | 70 | 2 | 2 | 1 | 2/2/2/2 | 1/1/1/1/0 | 2 | 4 | 1/2/0/2 | 4 | 0 | 4 | 180 |
| Off-road hardtop | 6 | 15 | 70 | 2 | 2 | 1 | 3/3/3/3 | 2/2/2/2/0 | 2 | 4 | 0/1/2/0 | 4 | 2 | 4 | 180 |
| **Bus** | 14 | 35 | 70 | 1 | 1 | 1 | 26/26/3/5 | 2/2/2/2/0 | 2 | 6 | 0/2/10/0 | 51 | 50 | 10 | **980** |
| Tractor | 10 | 25 | 40 | 2 | 1 | 1 | 3/3/3/3 | 0/0/1/0/0 | 1 | treads | 2/2/0/2 | 3 | 0 | 6 | 500 |
| Construction vehicle | 18 | 45 | 30 | 2 | 1 | 1 | 4/4/4/4 | 0/0/1/0/0 | 1 | treads | 3/3/0/3 | 4 | 0 | 10 | 1620 |
| Flatbed truck | 16 | 40 | 80 | 1 | 1 | 1 | 14/14/4/4 | 0/0/2/0/0 | 2 | 14 | 6/7/0/4 | 51 | 2 | 8 | 1280 |
| **Trailer truck** | 20 | 50 | 80 | 1 | 1 | 1 | 14/14/4/8 | 5/5/2/0/0 | 2 | 18 | 0/1/10/5 | 51 | 50 | 10 | **2000** |

Interior capacity is shown as the game shows it, including the driver. Armour runs 0 (open air) to
5 (solid metal, complete protection); top armour always starts at zero and can only be added.
"Treads" cannot be shot out.

### What the table tells you

**Carrying capacity is exactly `5 × mass²`.** That is not in the manual — it was measured off the
engine and holds for all nineteen types. It is why big vehicles are so lopsidedly good: doubling
mass quadruples what you can carry. A trailer truck at mass 20 carries 2,000 spaces; two
limousines at mass 8 carry 640 between them for the same fuel.

**The bus and the trailer truck are the game.** They are the only two vehicles that carry both a
large crew *and* a large cargo, and the trailer truck's 5/5 side armour is better than anything
else's. A gang of one trailer truck and one bus can hold 101 crew and 2,980 spaces on 20 fuel a
move. Everything else is a stepping stone.

**Missile factor is how many crew can shoot through a facing.** The bus's 26 to a side is not a
typo — a full bus firing broadside is devastating. The trailer truck's 14 to a side plus 10 boarders
through the front is what makes it a capture platform.

**Watch fuel consumption.** A motorcycle burns 1 per move, a trailer truck 10. Ten light vehicles
cost you more fuel and carry less than one heavy one.

### Upgrades

Six kinds of shop, found while looting, each permanently improving a vehicle: **speed**,
**performance**, **foundry** (armour plate), **brake**, **welding** (structure), and
**underbody**. Improvements stack, and captured road-gang vehicles often arrive already improved —
in the shipped save one sports car hardtop has been raised from 120 to 140 MPH with 5/5/5/5/4
armour, which is better than anything the factory sells.

Body shops repair structural damage. Tire stores are the only source of snow tires.

---

## 5. Terrain

| Terrain | Travel | Notes |
| --- | --- | --- |
| Road | fastest | Interstates; road gangs live along them |
| Plains | slow | Ranches are common here |
| Farmland | slow | Farms are common here; the best early food |
| Desert | very slow | Almost nothing. Run out of fuel and you die |
| Forest | very slow | Same, and looting does not work here |
| Ruins | — | Nuked cities. Little to gain, mutants to lose men to |
| Oilfield | — | Fuel, and road gangs fighting over it |
| Mountain / wilderness / water | impassable | The gang cannot enter |

Looting works everywhere **except forest and desert**.

**Winter.** December, January and February slow the northern half of the map to a crawl. Snow tires
double your winter range and come only from tire stores. Plan to be south by December or to have
found a set.

---

## 6. Encounters

### Foot-gangs (found with `P`)

* **Mercenaries** — trained ex-military. Dangerous to fight, and usually willing to ally with a
  stronger gang. Never insult them.
* **Street gangsters** — a mixed bag with a competent leader.
* **Armed rabble** — scum, but they recruit.
* **The needy** — starving. Any of them will follow you for a meal. The cheapest recruits in the
  game.
* **Cannibals** — fond of ambushing envoy parties. Do not send envoys.
* **Satanists**, **mutants** — hostile on sight.

Your options at a foot-gang are: **send envoys** (the only way to recruit — a good politician can
do it without troops), **fire a volley** (a show of strength that makes enemies), **wait** (reads
as weakness) or **leave** (reads as weakness). Sending zero envoys backs out harmlessly.

### City residents (found with `C`)

Ten kinds, and the engine tracks which holds each of the 120 cities: **nobody**, **lawful national
guard**, **renegade national guard**, **a local gang**, **bureaucrats**, **survivalists**,
**reborners**, **satanists**, **invaders**, **the Mob**.

Scout before you enter. **Invaders are the ones to run from** — they are well-armed regulars who
despise road gangs. **Reborners** are harmless and generous. **Bureaucrats** want tolls, which a
politician can talk down. **Renegade guardsmen** and **the Mob** will fight.

Scouting costs men: some of the parties you send do not return.

### Road gangs

Eleven named gangs drive modified vehicles and are the real opposition: **Furies**, **Muthuh
Truckers**, **Motorheads**, **Hot Rod Lincolns**, **Hard Hats**, **Greyhounds**, **Redneck
Yahoos**, **Dune Buggers**, **Skulls**, **Roughriders** and the **Invader Death Squad**. Beyond
those, generic **renegade national guardsmen**, **road gangsters**, **armed rabble** and
**cannibals** roam everywhere.

Beating a road gang gives you their supplies and, if you fight it tactically, their vehicles.

---

## 7. Road combat

When a rival road gang appears the game asks **fight detailed road combat?**

* **`N` — abstract.** Instant, bloody, and it does **not** raise your vehicle ceiling.
* **`Y` — deployment**, then a choice of **tactical** or **quick**.

### Tactical

The long way, and the only way that raises your vehicle ceiling and lets you capture vehicles.

**Deployment.** Auto-deployment spreads your crew evenly by quality and hands out guns as widely
as it can; you can then adjust. Two rules bite: weapon types cannot be changed until every man is
allocated, and **changing a vehicle's crew resets its weapons to crossbows** — so set crew first,
weapons last. Vehicles deploy in columns 10–19 only, never on trees, derricks, rocks, fences,
wrecks, water or buildings, never on mud or tilled fields in farmland, and only on roads in cities
and on highways.

**Movement.** All speed changes and turns happen *before* you move, and moving ends the vehicle's
turn. Manoeuvrability is the number of 45° turns available, reduced by 1 for every 30 MPH (or part)
over 30, and reduced in proportion to tires lost. A stationary vehicle cannot turn at all; a
vehicle at 10 MPH turns freely. Press a number key to enter viewing mode and scroll the map; `Q`
to exit it.

**Ramming.** Entering an occupied square rams. Damage scales with both speeds, both masses and the
relative facings — head-on worst, broadside middling, rear least. Every vehicle takes **half
damage** when ramming or being rammed head-on, because all of them have reinforced fronts. A large
enough mass ratio simply disintegrates the smaller vehicle.

**Fire.** Two volleys for most vehicles, each through a *different* facing, and only if not
everyone fired in the first. Crossbows reach 5 squares, guns 10 and are more accurate; a man out
of ammo falls back to a crossbow. You cannot see or shoot through trees or buildings — hold Ctrl
and press a facing key to check line of sight. Tires have protection 4 and can be shot out.

**Boarding.** Only onto vehicles orthogonally adjacent or directly ahead or behind. Order is
fixed: topside crew hit the boarders, boarders hit the topside crew (or the interior if the
topside is gone), then the interior crew hit the boarders. Clear a vehicle of crew and it is
yours, with your boarders as its crew. At least one interior crewman must stay on each of your
own vehicles.

### Quick combat

Tactical's rules for fire and ramming, resolved automatically, with no boarding and no captures,
and everything driving flat out. You set three things:

* **delay** — how long each frame stays on screen;
* **ram ratio** — 1 rams anything your mass or lighter, 2 rams half your mass or lighter, ½ rams
  up to twice your mass. Higher is safer;
* **aiming priority** — three numbers for topside, interior and tires, each 1–8, summing to
  exactly 10. Shooting tires cripples; shooting interiors kills.

### Aftermath

The loser's supplies are yours, capped by your cargo space — anything that does not fit is lost at
random. `G`, `X` and `D` are available during the aftermath so you can dump the cheap stuff before
the good stuff spills.

---

## 8. Saving

`S` at the map, then `S` again at the save menu, then a name with no extension and no periods.

The game asks for a formatted diskette in drive A:, but the PC build writes **`<NAME>.RWS` into the
directory it was started from** — normally the game folder. The file is exactly 6,512 bytes, and
it is a raw image of the game's live state with no checksum, which is what makes offline editing
possible at all. `R` recalls; `D` at the save menu deletes.

Save often, and especially before entering a city you have not scouted.

---

## 9. How to win

The manual's journal spells the condition out, and two strings inside `A.R2K` confirm it:
`THE G.U.B. IS LOCATED IN %s` and `THE PASSWORD IS PANACEA.`

1. **Build a gang worth contacting.** Control enough cities and the G.U.B. sends an agent to you
   with instructions. Small cities are the cheap ones to take — the engine sizes towns from 0 to
   228, and a small town's residents are correspondingly weak.
2. **Collect scientists.** There are eight: Myron Smidlapp, Alec Trotier, Pedro Pintero, Gloria
   Mills, Gabriel Washington, Donny Dade, Dorothy Macalister and Cheng Lu Sinh. They turn up while
   searching for people (`P`) and introduce themselves only when they judge the moment right,
   which means only to a gang that looks like it can protect them.
3. **Deliver them to the G.U.B.** Six or seven earns you the last **Radio Direction Finder**, which
   is how the final one or two are found: press `U`, then `1` or `2` to lock onto a homer.
4. **Bring in the last of the eight.**

### A working order of play

**Days 1–20 — get wheels.** You start with one vehicle and a handful of men. Search for vehicles
(`V`) relentlessly and take anything with mass. Do not recruit yet: every man you take eats every
night and you have nowhere to put food. B.O.'s advice to J. J. Jennings in the manual is exactly
right — wheels first, then a couple of dozen good men to watch your back, then food, *then*
numbers.

**Days 20–60 — get big.** Once you have a bus or a trailer truck and a few hundred spaces of
food, start hiring. Aim for a couple of hundred men. Find the three cronies. Set up caches in two
or three towns on a route you will use again.

**Days 60 onwards — get strong.** Now fight road gangs *tactically*, every time, even when quick
combat would do — that is the only thing that raises your vehicle ceiling from 6 towards 15, and
captured vehicles arrive pre-upgraded. Take small cities to build the empire the G.U.B. is
watching for. Scout every town before entering; if it says invaders, leave.

**Throughout:**

* **Fuel is the one thing you cannot improvise.** Jammer Jacques' rule — never carry so much of
  everything else that you have to scrimp on fuel — is the single most useful line in the manual.
* **Know where the healers are** in whatever region you are in. When there are none, move.
* **Do not let the gang outgrow the fleet.** Passenger capacity is a hard cap and food is a
  nightly tax.
* **Avoid ruins.** Mutants roam at night and there is nothing there.
* **Deploy in a checkerboard** in tactical combat, for room to manoeuvre.
* **Do not scream down a road at 100 MPH.** Speed over 30 costs you a turn of manoeuvrability for
  every 30 MPH, and a vehicle that cannot turn is a vehicle that gets rammed.

---

## 10. The maps

48 columns by 42 rows each, drawn from the game's own `WEST.MAP` and `EAST.MAP`. **X runs 1–48
west to east; Y runs 0–41 north to south** — the engine's own convention, which is why X starts
at one. The two maps are separate worlds as far as movement is concerned; the game moves you
between them at the seam.

```
 .  plains          "  forest         =  road            1  small metropolis
 ,  farmland        x  ruins          O  oilfield        2  large metropolis
 ~  desert       (blank)  impassable: water, mountain, wilderness
                                                         3  metroplex
```

Each map is followed by its cities in north-to-south order with the coordinates the engine uses,
so a coordinate here can be typed straight into the trainer's Map tab. Supply is the town's
starting level, taken from the game's own initialised data; it falls as the town is stripped.

One oddity: **HOUSTON is stored at X = 0**, the only city that is. The engine's flat index wraps
that onto the previous row's last column, and while the map does carry a city tile there, the
game prints a blank location line if you stand on it. The trainer will not teleport you there.

```
WESTERN MAP  (map id 1)

              1         2         3         4
     123456789012345678901234567890123456789012345678
    +------------------------------------------------+
  0 | "xxx"xxxxxxxx .................,,,,,,,,,,,1==  | 0
  1 |  2"xxxxxxxxxx  .................,,,,,,,,,,=""  | 1
  2 |   =xxx...xxxx    .................,,,,,,,,=,,  | 2
  3 |"x 2xx,..=1====x  ..... ..........O..,,,,,,=,,  | 3
  4 |"x1,=====,""xxx=x     ...............,=====1=,  | 4
  5 | "=,xx,,,,,,xx""=     ..........======,,,,,,==  | 5
  6 | "=,xx,,,,, x  xx===  .........=......,,,,,,=,  | 6
  7 | "2=======. x   x   ===,,....==.......,,,,,,=,  | 7
  8 | "1,xx,,,"=xxxx   xxxxx======..........,,,,,=,  | 8
  9 | "1,xx..xx=,xxxxxx"..xxO  x......x....,,,,,,=,  | 9
 10 | "="xx...x.=1=====..,xxx     ...."........,,=,  | 10
 11 |"x,=xx.......,,,,,=, x x.... .....,........,=,  | 11
 12 | xx=xx,......====..=.   .....x.,,,,........,,=  | 12
 13 | "x=x  ...===....= ,= =========,,,O,,..,,,,,,1  | 13
 14 |""x=xx .~=......~=  1=xx   xxx =,===========1,  | 14
 15 |""x=,x ~.=......~~==1x""   xxx ==,,,,,,,,,,,,,  | 15
 16 | "x=,==1=.......~~..=x..O xx x 2.O..,,,O,,,,,,  | 16
 17 | "",2,xx...........=x... ,,x   1....,,,,O,,,,,  | 17
 18 |  "11=,xx.........=,x... xxxx  =..,.,,,,,,,,,=  | 18
 19 |   2,1,,xx.~~.....=x.... xxx   =..,,,,,,,,O1=,  | 19
 20 |   x2 1,,xx.~~...=x.....     x =........,,,=,,  | 20
 21 |    1, 1,,xx~.~~=....   x  x x =........,,,=,O  | 21
 22 |    ""  1O,x~~~1 ......  xx  ==,..,,....,,,=O1  | 22
 23 |      , 1O ~~~=. .."x.....  1= ,.,,,.O.,,,,1=O  | 23
 24 |      ,11=   =~.... xx"""x  = ======1=======,,  | 24
 25 |          322======.   xxxx= x ....,=,,,,,O=,,  | 25
 26 |           =,~~~~~,2...   x= x ..OO.1,,,O...==  | 26
 27 |            2=~~~~..=.......= ==============2,  | 27
 28 |            1~1~~~~~~1======1= ....O.....,,=,,  | 28
 29 |            ~~~  ~~~=  .....1===.........,1,,,  | 29
 30 |             ~~~  ~~=xxx....=.. ====.....,1,,,  | 30
 31 |              ~~   ~1xxxx...=... ...===...1,,,  | 31
 32 |               ~~   ~=xxxx..,=.~~ ~~...==2,===  | 32
 33 |               ~~~   ~=xxxx.,1.~~~~  ...==,,,O  | 33
 34 |                ~~~   =xxxx ..=.~~~~....=.=,O   | 34
 35 |                 ~~   ~=xxxxx.=.~~~~...=..O1    | 35
 36 |               ~~~~     =xxxx..=.~~....=....    | 36
 37 |                ~~~~    ,=xxxx..=.~~..=..O,,    | 37
 38 |                 ~~~      =xxx ..1====2..O11    | 38
 39 |                  ~~~ ~    =xx=1=.... .,...     | 39
 40 |                   ~~~~~    ==xx...... .,..     | 40
 41 |                      ~~     ,xx...... ....     | 41
    +------------------------------------------------+
     123456789012345678901234567890123456789012345678
```

| City | X | Y | Supply |
| --- | --- | --- | --- |
| WINNEPEG | 44 | 0 | 15 |
| VANCOUVER | 3 | 1 | 40 |
| SEATTLE | 4 | 3 | 41 |
| SPOKANE | 11 | 3 | 9 |
| TACOMA | 3 | 4 | 13 |
| FARGO | 44 | 4 | 4 |
| PORTLAND | 3 | 7 | 32 |
| SALEM | 3 | 8 | 7 |
| EUGN/SPRINGFLD | 3 | 9 | 7 |
| BOISE | 13 | 10 | 5 |
| OMAHA | 46 | 13 | 15 |
| SLT LK CTY/OGD | 21 | 14 | 24 |
| LINCOLN | 45 | 14 | 5 |
| PROVO | 21 | 15 | 6 |
| RENO | 8 | 16 | 5 |
| DENVER | 32 | 16 | 41 |
| SACRAMENTO | 5 | 17 | 26 |
| COLRADO SPRNGS | 32 | 17 | 8 |
| SANTA ROSA | 4 | 18 | 8 |
| NAPA/VLJ/FRFLD | 5 | 18 | 9 |
| SN FRAN/OAKLND | 4 | 19 | 82 |
| STOCKTON | 6 | 19 | 9 |
| WICHITA | 44 | 19 | 11 |
| SN JOSE/MTN VW | 5 | 20 | 33 |
| MODESTO | 7 | 20 | 7 |
| SLNS/MONT/SEAS | 5 | 21 | 8 |
| FRESNO | 8 | 21 | 13 |
| VISALIA | 9 | 22 | 7 |
| LAS VEGAS | 16 | 22 | 12 |
| TULSA | 46 | 22 | 18 |
| BAKERSFIELD | 9 | 23 | 11 |
| ALBUQUERQUE | 29 | 23 | 12 |
| OKLAHOMA CITY | 44 | 23 | 21 |
| S BRB/S MR/LOM | 8 | 24 | 8 |
| OXN/SIMI V/VNT | 9 | 24 | 14 |
| AMARILLO | 37 | 24 | 5 |
| LOS ANGELES | 11 | 25 | 187 |
| ANA/S ANA/G GR | 12 | 25 | 49 |
| RVRSD/SN B/ONT | 13 | 25 | 39 |
| PHOENIX | 20 | 26 | 38 |
| LUBBOCK | 37 | 26 | 6 |
| SAN DIEGO | 13 | 27 | 47 |
| DALLAS/FT WRTH | 45 | 27 | 75 |
| TIJUANA | 13 | 28 | 14 |
| MEXICALI | 15 | 28 | 9 |
| TUCSON | 22 | 28 | 14 |
| EL PASO | 29 | 28 | 12 |
| CIUDAD JUAREZ | 29 | 29 | 14 |
| WACO | 43 | 29 | 5 |
| TEMPLE/KILLEEN | 43 | 30 | 6 |
| HERMOSILLO | 21 | 31 | 5 |
| AUSTIN | 43 | 31 | 14 |
| SAN ANTONIO | 42 | 32 | 27 |
| CHIHUAHUA | 30 | 33 | 10 |
| CORPUS CHRISTI | 44 | 35 | 9 |
| TORREON | 34 | 38 | 10 |
| MONTERREY | 39 | 38 | 44 |
| MCALN/PHR/EDNB | 43 | 38 | 8 |
| BROWNSVILLE | 44 | 38 | 6 |
| DURANGO | 32 | 39 | 7 |

```
EASTERN MAP  (map id 2)

              1         2         3         4
     123456789012345678901234567890123456789012345678
    +------------------------------------------------+
  0 |======"""" """"""""""""""""""""""""""""""""""  "| 0
  1 | """""=""=======""""""""""""""""""""""""""     "| 1
  2 |"   """==     ""===="""""""""""" """ ".."  """ "| 2
  3 |" """""         """"=""""""""""""""""... """"" ,| 3
  4 |"""" "    "      """"=""""""" """""".1  """""" =| 4
  5 |="""""" """"  """ """"="""""""""""".=""""""""" ,| 5
  6 |,=="""""""""""" "      ="""""""""".=""""",,""" ,| 6
  7 |,,,2=,,,"""""   ""      ="""""1===2==""" ,,""" ,| 7
  8 |,,,=,=",,,",  ,"","   . =""".==.xx=x"=xx,,"".  ,| 8
  9 |,,,=,,==",,,  ",,,   ...2==== ,"xx=x"=x,,,     ,| 9
 10 |,,=,,,,,==,,  ,,,,,, ...1     ,,",=x"=",       ,| 10
 11 |,,=,,,,,,,=2  1==1=,.===.2=1==1==1"",,=        ,| 11
 12 |,,=,,,,,,,,=  ,,=O,21    =O,,,=."==1==2        ,| 12
 13 |,,=,,,,,,,,31,,=,,1    =="O"""1",==1=1,        =| 13
 14 |,,=,,,,,,,=..==,,=O==21,,,,"""1,,3             =| 14
 15 |,,=,,,,,,=,,,==O=O,,=1,2=======22              =| 15
 16 |,=,,,,,,=,,,,=2,1,,2,,"xx""",,1,"              ,| 16
 17 |2=====,,=====,==2==,,O,xxx",,=,                =| 17
 18 |,,,,,,=2=,O,,"=,,",""xxxxx",,2,                ,| 18
 19 |,,,"==="=,,O,,1,,",,"xxxx",,2 "                ,| 19
 20 |====."""=.,,,,=","""xxx"",,=1                  =| 20
 21 |,,""""""=,,,,=,,"""xxx",,===,1                 ,| 21
 22 |,"xxx""=",==1,,""xxxx,,,1"1,,                  ,| 22
 23 |,,,,,,"1==",,=""xxxx.=1=",=,"                  "| 23
 24 |""""",==..".,"=""xx.1."".=" "                  "| 24
 25 |"=====,=,."",,,="===""".="                     =| 25
 26 |=,""".,=.,"=1===2.."..,="                      ,| 26
 27 |,O"O"",=O.=""""..="."""=                       "| 27
 28 |""""""=","=.,,,,""="""=                        "| 28
 29 |""""""=O,"=",",,,,=""=                         ,| 29
 30 |"""O,"=,"=""""".",=" =                         ,| 30
 31 |=1===== =     """""="1                         2| 31
 32 |O     =2O      "  "="=                          | 32
 33 |                   =,"=                         | 33
 34 |                   ="1=                         | 34
 35 |                   2=,=                         | 35
 36 |                    =, 1                        | 36
 37 |                     ==2                        | 37
 38 |                       2                        | 38
 39 |                            2                   | 39
 40 |                          .                     | 40
 41 |                                                | 41
    +------------------------------------------------+
     123456789012345678901234567890123456789012345678
```

| City | X | Y | Supply |
| --- | --- | --- | --- |
| QUEBEC | 38 | 4 | 5 |
| MNPLS/ST PAUL | 4 | 7 | 53 |
| OTTAWA | 31 | 7 | 8 |
| MONTREAL | 35 | 7 | 71 |
| TORONTO | 25 | 9 | 16 |
| HAMILTON | 25 | 10 | 8 |
| MILWAUKEE | 12 | 11 | 35 |
| GRAND RAPIDS | 15 | 11 | 16 |
| FLINT | 18 | 11 | 14 |
| BUFFALO | 26 | 11 | 32 |
| ROCHESTER | 28 | 11 | 25 |
| SYRACUSE | 31 | 11 | 17 |
| ALB/SCHEN/TROY | 34 | 11 | 20 |
| DETROIT | 20 | 12 | 109 |
| WINDSOR | 21 | 12 | 5 |
| SPFD/CHCP/HLYK | 36 | 12 | 14 |
| BOSTON | 39 | 12 | 70 |
| CHICAGO | 12 | 13 | 178 |
| GRY/HMND/E CHI | 13 | 13 | 17 |
| TOLEDO | 19 | 13 | 20 |
| SCRANTON | 31 | 13 | 17 |
| HARTFORD | 36 | 13 | 19 |
| PROV/WRWK/PAWT | 38 | 13 | 23 |
| CLEVELAND | 22 | 14 | 48 |
| YNGSTWN/WARREN | 23 | 14 | 14 |
| ALNTN/BTH/EAST | 31 | 14 | 16 |
| NEW YORK CITY | 34 | 14 | 228 |
| AKRON | 22 | 15 | 17 |
| PITTSBURGH | 24 | 15 | 57 |
| PHILADELPHIA | 32 | 15 | 118 |
| NWRK/JERSY CTY | 33 | 15 | 63 |
| INDIANAPOLIS | 15 | 16 | 18 |
| DAYTON | 17 | 16 | 21 |
| COLUMBUS | 20 | 16 | 28 |
| WILMINGTON | 31 | 16 | 40 |
| KANSAS CITY | 1 | 17 | 34 |
| CINCINNATI | 17 | 17 | 36 |
| ST LOUIS | 8 | 18 | 59 |
| BALTIMORE | 30 | 18 | 55 |
| LOUISVILLE | 15 | 19 | 23 |
| WASHINGTON,DC | 29 | 19 | 77 |
| RICHMOND | 29 | 20 | 16 |
| NRFK/VA B/PTSM | 30 | 21 | 21 |
| NASHVILLE | 13 | 22 | 22 |
| GBRO/W-S/HI PT | 25 | 22 | 21 |
| RALEIGH/DURHAM | 27 | 22 | 16 |
| MEMPHIS | 8 | 23 | 23 |
| CHARLOTTE | 23 | 23 | 16 |
| GRNVL/SPRTNBRG | 21 | 24 | 15 |
| BIRMINGHAM | 13 | 26 | 22 |
| ATLANTA | 17 | 26 | 51 |
| BEAUMONT | 2 | 31 | 10 |
| JACKSONVILLE | 22 | 31 | 19 |
| HOUSTON  *(stored at X 0 - see the note above)* | 0 | 32 | 76 |
| NEW ORLEANS | 8 | 32 | 30 |
| ORLANDO | 22 | 34 | 18 |
| TMPA/ST PTRBRG | 20 | 35 | 40 |
| W PM BH/B RATN | 24 | 36 | 15 |
| FT LAUD/HLLYWD | 24 | 37 | 26 |
| MIAMI | 24 | 38 | 41 |

---

## 11. Loot

`L` searches for a lootable site. What can turn up depends on the terrain, and the engine keeps a
frequency for each site in each of four terrain classes — which is why farms cluster in farmland
and ranches on the plains.

| Looking for | Go to | Best single site |
| --- | --- | --- |
| Food | Farmland (farms), plains (ranches), any city (supermarkets, convenience stores, restaurants, malls) | Shelter or supermarket, 50 |
| Guns and ammo | Military bases and armories first, then gun shops, sporting goods stores, police stations | Military base, 60 |
| Fuel | Fuel storage tanks, gas stations, oilfield terrain | **Fuel storage tank, 100** |
| Tires — and **snow tires** | Tire stores and junkyards | Junkyard, 30 |
| Medical supplies | Hospitals, medical centres, drug stores, veterinarians | Hospital, 3 |
| A vehicle | Body shops, auto dealers, bus depots, taxi garages, racing tracks, shopping malls, military bases, high schools | — |
| Vehicle repair | Body shops | — |
| Vehicle upgrades | Speed shops, performance shops, foundries, brake shops, welding shops, underbody shops | — |

A **cache** is the one site that pays a little of everything at once — food, guns, tires, fuel and
medical supplies together. A **fuel storage tank** is the single richest find in the game: 100 fuel,
and the engine gives it a frequency of 100 on roads, so drive the interstates when you are dry.

The twenty-eight sites in the engine's own order: convenience store, supermarket, shopping mall,
military base, farm, ranch, sporting goods store, gun shop, armory, restaurant, body shop, high
school/college, auto dealer, tire store, junkyard, gas station, parking lot, fuel storage tank,
medical centre, hospital, veterinarian, cache, police station, bus depot, taxi garage, shelter,
drug store, racing track.

A town's supply level falls as you strip it. Large cities are effectively inexhaustible; small
western towns run dry quickly. The trainer's Cities tab shows each town's current level against
what it shipped with.

---

## 12. Quick reference

**Winning:** control cities → the G.U.B. contacts you → find eight scientists → deliver six or
seven → receive the Radio Direction Finder → find the last one or two. The password is PANACEA.

**Vehicle ceiling:** starts at 6, +1 per **tactical** road battle fought to a finish, maximum 15.

**Carrying capacity:** `5 × mass²` spaces.

**Fuel shown on `G`:** stored fuel − 2 × fuel consumption. `X` shows stored.

**Cache limit:** 255 of each of food, tires, fuel, guns, medical, per city.

**Antitoxin:** one dose per 50 crew, everyone at once.

**Food:** one per crew member per night.

**Best vehicles:** trailer truck (2,000 spaces, 101 crew, 5/5 side armour) and bus (980 spaces,
101 crew, 26 crew firing to a side).

**Run from:** invaders, in a city or on the road.

**Never insult:** mercenaries.

---

*Terrain, city, vehicle and loot figures in this guide were read out of the game's own data
tables rather than transcribed from the manual; see `docs/reverse-engineering.md` for how, and for
the few places where the two disagree.*
