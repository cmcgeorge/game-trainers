# Sid Meier's *Pirates!* — Play &amp; Strategy Guide

*MicroProse, 1987 — IBM version 432.02.* This guide covers how to start, how to play, how the game
decides whether you won, and where the money is. Every table, coordinate and schedule in it was decoded
from the game's own data files (see `Pirates-ReverseEngineering.md`), so the numbers are the game's, not
a recollection.

---

## 1. Getting it running

The directory holds a DOS conversion of the original self-booting release. Run **`PIR.EXE`** — not
`DISKP` — from inside DOSBox with that directory as the current drive:

```
mount c C:\Temp\Scratch\Win31DOSBox\C-DRIVE
c:
cd \GAMES\PIRATES
pir
```

`PIR.EXE` opens `DISK1`, `DISK2` and `DISKS` and services the game's disk reads from them. All three
must be in the current directory.

**There is no copy-protection question in this build.** The original 1987 release asked you to look up a
convoy date in the manual; that prompt is simply not present here, and the original disk-based check is
bypassed by the loader. You will go straight from the credits to the main menu. (§8 has the answer key
anyway — it is the convoy schedule, which is the most useful thing in the game.)

### First-run setup

The game asks three questions:

| Question | Options |
|---|---|
| Graphics | 1) CGA  2) Tandy-1000  3) EGA |
| Drives | 1) 1 floppy drive  2) 2 floppy drives |
| Control | 1) Joystick  2) Keyboard |

Pick **EGA** and **keyboard** unless you have a reason not to. Joystick mode asks you to calibrate by
moving the stick to each corner. If you pick CGA on a monochrome setup the game complains
"COLOR MONITOR NEEDED!".

---

## 2. Controls

| Input | Where | What it does |
|---|---|---|
| **F10** | anywhere | **Quit to DOS.** Added by the DOS loader — it restores the interrupt table and drops you back to the prompt. |
| Arrow keys / joystick | menus | Move the highlight |
| **Enter** / fire button | menus, prompts | Choose. Prompts read *"Press ENTER to continue"* on keyboard, *"Press TRIGGER"* on joystick |
| Left / right | sailing | Turn the ship |
| Up / down | sailing | Raise sail (faster, wider turns) / lower to **battle sails** (slower, turns tightly, guns bear) |
| Fire / Enter | sea battle | Fire the loaded broadside. The panel shows `GUNS LOADED` / `RELOADING` |
| Steer into the enemy + fire | sea battle | Grapple and board |
| Up / down | fencing | Swing high / thrust low |
| Left / right | fencing | Press the attack / give ground |
| Arrow keys / joystick | cargo transfer | Move goods between holds; *"Press ENTER when done"* |
| Any direction | land battle | Manoeuvre your buccaneers |

**You cannot sail directly into the wind.** The sailing panel reads `WINDS FROM THE …`; to make ground
upwind you tack, alternating diagonals. Wind strength (`LIGHT` / `MEDIUM` / `STRONG`) changes how much
this hurts — and how badly a big galleon handles compared with a sloop.

---

## 3. Starting a career

The main menu offers three things:

* **Start a new career** — the real game.
* **Continue a saved game** — four slots on the save disk.
* **Command a Famous Expedition** — six short historical scenarios, scored on their own terms:

| Scenario | Commander | Year |
|---|---|---|
| Battle of San Juan De Ulua | John Hawkins | 1569 |
| The Silver Train Ambush | Francis Drake | 1573 |
| The Treasure Fleet | Piet Heyn | 1628 |
| The Sack of Maracaibo | L'Ollonais | 1666 |
| The King's Pirate | Henry Morgan | 1671 |
| The Last Expedition | Baron De Pointis | 1697 |

A new career asks four things:

**Nationality.** Spanish, English, French or Dutch. This is who will grant you a Letter of Marque, and
who will be shooting at you. The Spanish own almost everything worth robbing, so playing *against*
Spain — English, French or Dutch — is the straightforward route; playing *as* Spain means hunting
pirates and other nations' shipping, which pays far less.

**Time period.** Six eras. The game stores them as codes 0, 2, 3, 4, 5 and 6, which is why the years
come out at twenty-year intervals from 1560:

| Menu | Year | Character |
|---|---|---|
| The Silver Empire | 1560 | 32 towns, almost all Spanish, huge treasure convoys, nowhere friendly to refit |
| Merchants and Smugglers | 1600 | The first non-Spanish colonies appear |
| The New Colonists | 1620 | 38 towns; the Caribbean islands start filling in |
| War For Profit | 1640 | Wars everywhere; letters of marque are easy to come by |
| The Buccaneer Heroes | 1660 | **The default**, and the best-balanced era: rich Spanish towns, plenty of friendly ports |
| Pirates' Sunset | 1680 | 41 towns, but the convoys are thinner and everyone hunts pirates |

If you decline to pick a period, the game gives you **1660**.

**Difficulty.** Apprentice / Journeyman / Adventurer / Swashbuckler. It scales the opposition *and* your
retirement score — the top titles are effectively unreachable below Adventurer.

**A speciality.** You get exactly one:

| Ability | What it buys you |
|---|---|
| Skill at Fencing | Duels become easy — and duels decide boardings, town assaults and your escape from prison |
| Skill at Navigation | Better speed and manoeuvre, especially upwind |
| Skill at Gunnery | Faster reloads, harder-hitting broadsides |
| Wit and Charm | Governors, their daughters and tavern informants all treat you better |
| Skill at Medicine | Fewer crew lost to wounds and disease; a longer career |

**Fencing** is the strongest pick for a first serious game: it turns every boarding into a win, and
boarding is how you take ships without sinking the cargo. **Navigation** is the best pick if you intend
to chase convoys, because catching a galleon is mostly a sailing problem.

---

## 4. The loop

You sail the Spanish Main. Time passes — the game runs a **360-day year of twelve 30-day months** — and
while it passes your crew eat, grow restless and desert, and you get older.

**At sea** you can:

* **Chase sails.** The lookout reports one; you may *Investigate*, *Hail for news* or *Sail away*.
  Hailing is free intelligence — it tells you what nation is at war with whom and where the convoys are.
* **Fight.** Close, exchange broadsides, then grapple and board when their morale is `SHAKEN` or worse.
  Sinking a ship destroys its cargo; boarding keeps it. The panel shows both crews and both morales —
  board when yours is higher.
* **Take a prize.** After a win you choose *Yes, send a prize crew* (you keep the hull, but it costs
  crew to man) or *No, plunder and sink her*. Prize crews are how a fleet grows; they are also how you
  end up with `Not enough crew: one ship lost.`

**In port** the menu is: *Visit the Governor*, *Visit a tavern*, *Trade with a merchant*, *Divide up the
plunder*, *Check information*, *Leave town*.

* **The Governor** gives you your mission, promotes you, grants land, and introduces his daughter. Rank
  and land are most of your retirement score, so visit often.
* **The tavern** is where you recruit ("A rowdy group of sailors asks to join your crew") and where you
  buy information from travellers — including treasure-map pieces and the current whereabouts of the
  convoys. Money spent in taverns is usually money well spent.
* **The merchant** buys and sells cargo and ships, and repairs damage. Repair before a long voyage; a
  `(DAMGD)` ship is slow and fragile. Nobody friendly to Spain will trade with a known pirate.
* **Divide up the plunder** ends the voyage: the crew take their shares and leave, and what is left is
  *your* wealth. This is the only way loot becomes score — gold sitting in the hold counts for nothing
  at retirement.

**Ashore** you can *Attack town*, *Sneak into town*, or *March into town* from an inland landing. Forts
fire on ships entering harbour (`As you approach the town the fort opens fire`), so a heavily-fortified
port is usually taken by landing outside it and marching. Land battles show
`PIRATE PARTY / DEFENDERS — MEN, MUSKETS` for both sides; numbers and terrain decide it.

### Crew, food and morale

The party panel shows `CREW`, `CANNON`, `GOLD`, `FOOD … DAYS`, `CREW IS …` and `GOODS`. Morale runs
`PANIC → SHAKEN → ANGRY → FIRM → STRONG → WILD! → WILD!!` and crew mood
`ANGRY! → UNHAPPY → PLEASED → HAPPY!`.

Three things make crew unhappy: **time without plunder**, **running low on food**, and **losses**. When
food runs short you get `Captain, we have only n day's food left.`; then
`Roll call reveals n crew members have deserted.` A big crew is only useful if you feed it and pay it —
this is the central tension of the game. Divide the plunder before morale collapses, then sign on a new
crew.

### Ships

| Hull | Notes |
|---|---|
| Pinnace | Tiny, fast, barely armed. Ideal for sneaking into a harbour |
| Sloop | Quick and nimble, small hold — the classic early raider |
| Barque | Modest hauler; handles better than a fluyt |
| Cargo Fluyt | Big hold, poor guns. A prize worth taking, a poor flagship |
| Merchantman | Balanced trader with enough guns to bully a sloop |
| **Frigate** | The best all-round fighting ship — fast enough to choose the fight |
| War Galleon | Heaviest guns and crew; clumsy in light winds |
| Galleon | The Treasure Fleet's workhorse. Huge hold, sluggish |
| **Fast Galleon** | A galleon that sails properly. The finest prize on the Main |

Cargo is `Food`, `Goods`, `Sugar`, `Tobacco`, `Hides` and `Cannon`, all counted in tons of hold space;
gold costs no space. Cannon win battles but eat hold and need crew to work.

---

## 5. The map

Positions below are the game's own settlement coordinates (column 0–255 west→east, row 0–255
north→south), rendered to scale. `*` Spanish · `+` English · `#` French · `o` Dutch.

### 1560 — The Silver Empire

```
                                        #St.Augustine


                                              #Grand Bahama

                                                 +Nassau
                                        #Florida Keys
                                                    +Eleuthera
                                     *Havana


                                                *Pr.Principe
                 *Campeche                            *Santiago
   *Vera Cruz                                                     *Isabella
                                                              *Yaguana       *San Juan
           *Villahermosa                           *Santigo Vega    *Sant.Domingo






                           *Gran Granada                    *Rio De Hacha
                                                                     *Coro         *Cumana
                                                         *Santa Marta               *Margarita
                                            *Nombre Dios        *Maracaibo  *Borburata   *Trinidad
                                                      *Cartagena          *Pr.Cabello
                                            *Panama               *Gibraltar
```

### 1660 — The Buccaneer Heroes (the default era)

```
                                                                                +Bermuda
                                        *St.Augustine






                                                    +Eleuthera

                                     *Havana


                                                *Pr.Principe
                 *Campeche                            *Santiago
   *Vera Cruz                                                #Tortuga                   +Montserrat
                                                             #Port-De-Paix    *San Juan  +Antigua
           *Villahermosa                                      #Leogane                oSt.Martin
                                                   +Port Royale     *Sant.Domingo     oSt.Eustatius
                                                             #Petit Goave              +St.Kitts
                                                                                       +Nevis
                                                                                          #Guadeloupe
                                                                                           #Martinique
                                                                                                +Barbados
                                       *San.Catalina
                           *Gran Granada                               oCuracao
                                                             *Rio De Hacha          *Cumana
                                                          *Santa Marta               *Margarita
                                           *Puerto Bello        *Maracaibo  *Caracas      *Trinidad
                                                      *Cartagena  *Gibraltar
                                            *Panama
```

Two spots on the map are not towns: the **Florida Channel** appears twice as a waypoint at the top
(coordinates 67,51 and 71,33). That is where the Treasure Fleet leaves for Spain — the last place to
catch it, and the place it is least escorted.

The trainer's **Settlements** tab lists every era with exact coordinates, garrison, population and
treasury.

### The great prizes

| 1560 | Treasury | Defence |  | 1660 | Treasury | Defence |
|---|---:|---|---|---|---:|---|
| Santiago | 90,000 | 3 forts, 450 soldiers | | Panama | 80,000 | 1 fort, 400 soldiers |
| Havana | 50,000 | 3 forts, 250 soldiers | | Havana | 60,000 | 4 forts, 450 soldiers |
| Panama | 50,000 | 1 fort, 250 soldiers | | Cartagena | 55,000 | 4 forts, 400 soldiers |
| Vera Cruz | 50,000 | 2 forts, 350 soldiers | | Santiago | 40,000 | 3 forts, 300 soldiers |
| Cartagena | 40,000 | 4 forts, 400 soldiers | | Vera Cruz | 35,000 | 4 forts, 300 soldiers |

**Panama is the anomaly and the whole strategy of the game.** It holds the most gold on the Main and it
is defended by a single fort — because it is on the Pacific side, reachable only by marching overland.
That is what Morgan actually did in 1671, and it is what the game rewards. Gran Granada (25,000, *no*
fort) is the same trick on Lake Nicaragua.

---

## 6. How you win

There is no victory screen — you **retire**, and the game scores the life you led. The epilogue reads
your wealth, land, rank, marriage, wounds, family rescues and Pirate Points (out of 100), and assigns a
station in life from a ladder of twenty-four:

> King's Advisor · Governor · Lt. Governor · Fleet Admiral · Rich Banker · Plantation Owner · Wealthy
> Merchant · General · Sugar Planter · Merchant Captain · Councilmember · Colonel · Shop Owner · Major ·
> Tavernkeeper · Sailing Master · Sergeant · Bartender · Sailor · Farm Hand · Rogue · Scoundrel ·
> Pauper · Beggar

What the epilogue actually reads:

1. **Personal wealth** — accumulated at each *Divide up the plunder*, stored in tens of gold pieces and
   printed as `"You accumulated the sum of N gold pieces"`. Loot still in your hold is worth nothing.
2. **Land** — granted by Governors, in units of 50 acres, and it pays a monthly income into your wealth
   for the rest of your career. **Land granted early is worth far more than land granted late.**
3. **Rank** — Ensign → Captain → Major → Colonel → Admiral → Baron → Count → Marquis. Earned by serving a
   Governor: sink his enemies' ships, sack his enemies' towns, report back.
4. **Marriage** — court a Governor's daughter. The epilogue grades the bride from
   `"shrewish and pestersome"` to `"an exciting and beautiful creature"`.
5. **Family rescued** — sister, father, mother, uncle, scattered by the Spanish. Tavern informants and
   captured captains tell you where they are.
6. **Reputation** — Cowardly → Promising → Well Known → Famous → Notorious → Infamous!
7. **Age and health** — you start at about twenty and you get slower. Wounds accumulate; each one is
   named in the epilogue. When your reflexes go, retire.

### A winning line of play

1. **Take a Letter of Marque early**, from whoever is at war with Spain. Free target list, free
   promotions.
2. **Sail a sloop, not a galleon, for the first few years.** You need to *catch* things; a fast small
   ship with a small crew is cheap to feed and hard to escape.
3. **Chase the convoys, not random shipping.** §8 tells you where they will be. A single Treasure Fleet
   galleon is worth more than a season of merchant-hunting.
4. **Divide the plunder every year or two**, at a friendly port, before morale rots. This is the *only*
   thing that converts loot to score.
5. **Ask the Governor for a promotion at every visit**, and take land whenever it is offered.
6. **Take Panama.** Land on the Caribbean coast near Puerto Bello, march overland, and take the richest
   city in the game past its single fort.
7. **Court a daughter and rescue your family** in the middle years, when you can still win the duels.
8. **Retire while you can still fence.** A Marquis with 300 acres and a good marriage who retires at
   forty beats an Admiral who fenced badly at fifty-five.

---

## 7. Things the game will not tell you

* **The wind decides the battle.** Getting upwind of an enemy is worth more than guns. Battle sails let
  you turn inside a bigger ship; a galleon in a light wind can be circled indefinitely by a sloop.
* **Board, don't sink.** A sunk ship's cargo is gone. Rake the sails, wait for their morale to drop,
  then grapple.
* **Fortified harbours are for marching, not sailing.** Cartagena and Havana with four forts will
  cripple a ship on the way in. Land outside and *March into town*.
* **Hail every sail you don't intend to fight.** War declarations and convoy positions arrive that way,
  free.
* **Buy every treasure map piece offered.** They combine; the Lost Inca Treasure is worth the detour.
* **Watch the food, not the gold.** `We have only n day's food left` is the beginning of a mutiny.
* **A big fleet is a liability.** Every prize crew is crew not on your flagship, and every hull is
  another mouth. Sell prizes at a friendly port rather than dragging them along.

---

## 8. Convoy schedule — where the silver is

The Treasure Fleet (Spain's escorted bullion convoy) and the Silver Train (the mule train carrying Peru's
silver across the isthmus) run a fixed annual route. Intercepting them is the fastest money in the game
— and the same tables were the answer key to the manual's copy-protection question, which is why they
are reproduced in full.

Read them as *town — month, first or second half of that month*. The trainer's **Convoys** tab has the
same data with an era picker and a town filter.

### 1560

| # | Treasure Fleet | Silver Train |
|---|---|---|
| 1 | Cumana — Oct early | Cumana — Apr early |
| 2 | Pr. Cabello — Oct late | Borburata — Apr late |
| 3 | Maracaibo — Nov early | Pr. Cabello — May early |
| 4 | Rio de Hacha — Nov late | Coro — May late |
| 5 | Nombre Dios — Dec early | Gibraltar — Jun early |
| 6 | Cartagena — Dec late | Maracaibo — Jun late |
| 7 | Campeche — Jan late | Rio de Hacha — Jul early |
| 8 | Vera Cruz — Feb early | Santa Marta — Jul late |
| 9 | Havana — Mar early | Cartagena — Aug early |
| 10 | Santiago — Mar late | Panama — Aug late |
| 11 | Florida Channel — Apr late | Nombre Dios — Oct early |
| 12 | Florida Channel — May early | |

### 1600

| # | Treasure Fleet | Silver Train |
|---|---|---|
| 1 | Cumana — Oct early | St. Thome — Apr early |
| 2 | Caracas — Oct late | Cumana — Apr late |
| 3 | Maracaibo — Nov early | Caracas — May early |
| 4 | Rio de Hacha — Nov late | Pr. Cabello — May late |
| 5 | Santa Marta — Dec early | Coro — Jun early |
| 6 | Puerto Bello — Dec late | Gibraltar — Jun late |
| 7 | Cartagena — Jan early | Maracaibo — Jul early |
| 8 | Campeche — Feb early | Rio de Hacha — Jul late |
| 9 | Vera Cruz — Feb late | Santa Marta — Aug early |
| 10 | Havana — Mar late | Cartagena — Aug late |
| 11 | Florida Channel — Apr late | Panama — Sep early |
| 12 | Florida Channel — May early | Puerto Bello — Oct late |

### 1620

| # | Treasure Fleet | Silver Train |
|---|---|---|
| 1 | Caracas — Sep early | St. Thome — Mar early |
| 2 | Maracaibo — Sep late | Cumana — Mar late |
| 3 | Rio de Hacha — Oct early | Caracas — Apr early |
| 4 | Santa Marta — Oct late | Pr. Cabello — Apr late |
| 5 | Puerto Bello — Nov early | Gibraltar — May early |
| 6 | Cartagena — Dec early | Maracaibo — May late |
| 7 | Campeche — Jan early | Rio de Hacha — Jun early |
| 8 | Vera Cruz — Jan late | Santa Marta — Jun late |
| 9 | Havana — Feb late | Cartagena — Jul early |
| 10 | Florida Channel — Mar late | Panama — Jul late |
| 11 | Florida Channel — Apr early | Puerto Bello — Sep early¹ |

¹ The shipped manual chart says *Aug early* here. The game's own route row puts Panama in slots 9–11 and
Puerto Bello at slot 12, i.e. September; every other era shows the same one-to-two-month gap between
those two ports. Trust the game.

### 1640

| # | Treasure Fleet | Silver Train |
|---|---|---|
| 1 | Caracas — Oct early | Cumana — Apr early |
| 2 | Maracaibo — Oct late | Caracas — Apr late |
| 3 | Rio de Hacha — Nov early | Gibraltar — May early |
| 4 | Santa Marta — Nov late | Maracaibo — May late |
| 5 | Puerto Bello — Dec early | Rio de Hacha — Jun early |
| 6 | Cartagena — Jan early | Santa Marta — Jul early |
| 7 | Campeche — Feb early | Cartagena — Jul late |
| 8 | Vera Cruz — Feb late | Panama — Aug late |
| 9 | Havana — Mar late | Puerto Bello — Oct early |
| 10 | Florida Channel — Apr late | Barbados — Nov late |
| 11 | Florida Channel — May early | |

### 1660

| # | Treasure Fleet | Silver Train |
|---|---|---|
| 1 | Caracas — Sep early | Cumana — Mar early |
| 2 | Maracaibo — Sep late | Caracas — Mar late |
| 3 | Rio de Hacha — Oct early | Gibraltar — Apr early |
| 4 | Santa Marta — Oct late | Maracaibo — Apr late |
| 5 | Puerto Bello — Nov early | Rio de Hacha — May early |
| 6 | Cartagena — Dec early | Santa Marta — Jun early |
| 7 | Campeche — Jan early | Cartagena — Jun late |
| 8 | Vera Cruz — Jan late | Panama — Jul late |
| 9 | Havana — Feb late | Puerto Bello — Sep early |
| 10 | Florida Channel — Mar late | Barbados — Oct late |
| 11 | Florida Channel — Apr early | |

### 1680

| # | Treasure Fleet | Silver Train |
|---|---|---|
| 1 | Caracas — Oct early | Cumana — Apr early |
| 2 | Rio de Hacha — Oct late | Caracas — Apr late |
| 3 | Santa Marta — Nov early | Maracaibo — May late |
| 4 | Puerto Bello — Nov late | Rio de Hacha — Jun late |
| 5 | Cartagena — Dec late | Santa Marta — Jul early |
| 6 | Campeche — Jan late | Cartagena — Jul late |
| 7 | Vera Cruz — Feb early | Panama — Aug late |
| 8 | Havana — Mar early | Puerto Bello — Oct early |
| 9 | Florida Channel — Apr late | Barbados — Nov late |
| 10 | Florida Channel — May early | |

### How to use it

The Treasure Fleet is escorted and slow; the Silver Train is a land target you catch by sacking the town
it is *in*. Two reliable plays:

* **Wait at the Florida Channel in the spring.** The Treasure Fleet passes through it every year on the
  way home, at the end of its route, at its most laden.
* **Take Puerto Bello or Nombre de Dios when the Silver Train arrives.** The town message reads
  `The Silver Train is in town!` — that is the whole year's Peruvian silver sitting in one place.

Note the pattern across eras: in **1620 and 1660** both convoys run **one month earlier** than in the
other four periods. That is not a transcription quirk — it is a term in the game's own slot arithmetic
(`+2 × (era code & 1)`), and those two eras are the ones with odd codes.

---

## 9. Named rivals

The game will put other captains on the horizon — `Arrrgh! It's the pirate …` or
`It's that sea-dog: …` — drawn from: **Pegleg, One-Eye, El Dragon, Rivero, Mansfield, Vasseur, Robert
Baal, Le Grand.** Some are pirates, some are pirate-*hunters* (the ship label reads `(HUNTER)` or
`(PIRATE)`). Beating a named rival is worth reputation; losing to one costs a ship.

---

## 10. Saving

*Save Game* from the travel menu writes to one of four slots on `DISKS`. The game will prompt
`Insert Save Disk then press …` — under `PIR.EXE` there is nothing to insert, just press the key. If
`DISKS` has never been written the game reports `Not an Initialized Save Disk` and formats it for you.

Save before every town assault. Land battles are the one place the game can end a twenty-year career in
a single screen.
