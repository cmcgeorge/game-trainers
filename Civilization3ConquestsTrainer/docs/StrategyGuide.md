# Sid Meier's Civilization III: Conquests — strategy guide

For the shipped **v1.22** ruleset (Steam "Civilization III Complete"). Everything here applies to the
epic game unless a section says otherwise; the nine conquests change enough that they get their own
section at the end.

---

## 1. What Conquests changes

If you are coming from vanilla Civ III or Play the World, these are the differences that actually
change decisions:

**Two new civilization traits.** Every civ has two of eight traits, and Conquests added the last two:

| Trait | Effect | Why it matters |
| --- | --- | --- |
| Agricultural | +1 food in the city centre, cheap aqueducts, irrigation-friendly | The strongest early trait — food is expansion |
| Seafaring | Cheap harbours, +1 commerce on ocean tiles, better work boats | Turns an archipelago start from a handicap into an advantage |
| Militaristic | Cheap barracks, more Great Leaders | Best on a crowded continent |
| Religious | Cheap temples/cathedrals, one-turn anarchy | One-turn anarchy is worth more than it looks — you can switch government opportunistically |
| Commercial | Less corruption, +1 commerce in big cities | Scales with empire size |
| Scientific | Cheap libraries, free tech each era | A free tech per era is four to five free techs a game |
| Expansionist | Scouts, better goody huts | Front-loaded; weak after the ancient era |
| Industrious | Faster workers, cheap factories | Quietly one of the best — worker turns are the hidden currency |

**Two new governments.** Feudalism (cheap military, terrible commerce, heavy war weariness immunity)
and Fascism (strong military, population loss on adoption, high corruption at distance). Neither is a
default choice, but Feudalism is a real option for an early warmonger who does not want Monarchy's
build costs.

**Seven more civilizations** — Byzantines, Sumerians, Hittites, Portuguese, Dutch, Inca and Maya —
bringing the roster to 31 playable civs.

**Armies got better.** They heal, they can be built with the Military Academy, and a full army of
three or four units is genuinely hard to kill.

**Victory-point scoring** for scenarios, plus the Regicide, Mass Regicide and Elimination game
options.

**Rule fixes in v1.22** — the ones you will notice are the corruption behaviour, the double-gold
trade exploit closing, and multiplayer load/save fixes.

---

## 2. Controls worth knowing

| Key | Does |
| --- | --- |
| `Enter` | End turn |
| `Space` | Skip this unit's turn (keeps it selectable) |
| `F` | Fortify (defence bonus, heals) |
| `J`/`I`/`M`/`R` | Worker: join city / irrigate / mine / road |
| `A` | Automate worker (fine early, wasteful late) |
| `B` | Build city |
| `C` | Centre on your capital |
| `F1`–`F8` | Advisors — Domestic, Trade, Military, Foreign, Culture, Science, Espionage, Histograph |
| `Shift`+click | Add to a stack move |
| `Ctrl`+`Shift`+`M` | Toggle the map grid |

The F1 Domestic advisor is where your treasury, tax/science/luxury sliders and per-city corruption
are all visible at once. If you are using the trainer, F1 is the screen to check its numbers against.

---

## 3. The opening: expansion is the whole game

Civ III rewards city count more than city quality, and the window for free land closes around
1000 BC. A workable opening:

1. **Found in place on turn 1** unless you can see something clearly better within one move. A turn
   spent walking is a turn not producing.
2. **Warrior → Settler → Warrior → Settler**, with a Worker slipped in when the city is about to grow
   past its happy limit.
3. **Space cities four tiles apart.** Tighter than feels comfortable. Overlapping work radii are fine
   — you will never work all 21 tiles of a city anyway, and more city centres means more free tiles,
   more shields and more culture.
4. **Rivers and coast first.** A river gives free fresh water (no aqueduct needed to reach size 12)
   and a commerce bonus; the coast gives a harbour.
5. **Stop expanding when corruption makes a new city produce less than one shield and one commerce**
   — usually just past your Forbidden Palace's useful radius, which is later than you think.

Scouts and goody huts are worth real money as Expansionist; as anyone else, a Warrior exploring is
usually better spent fortified at home.

### Workers

Worker turns are the currency nobody counts. Rules of thumb:

- Roads before mines before irrigation, early — commerce compounds.
- Two workers per city by the classical era is not excessive.
- Capture enemy workers rather than killing them; a captured worker is a full worker.
- Industrious civs get more out of every worker, which is why the trait outperforms its reputation.

---

## 4. Corruption — the mechanic that decides your empire's shape

More than anything else, corruption is what stops Civ III from being "found infinite cities and win".

Corruption on a city scales with **distance from your palace**, your **government**, and your
**total number of cities**. It is applied to commerce (corruption) and shields (waste).

Your levers, in rough order of impact:

1. **The Forbidden Palace.** A second palace for corruption purposes. Build it far from your capital —
   ideally at the centre of mass of your *other* half. This is the single largest corruption fix in
   the game and people routinely build it too close to home.
2. **Government.** Despotism and Communism are the corruption extremes; Republic and Democracy are the
   commerce governments. Communism flattens corruption across distance rather than reducing it, which
   makes it the correct choice for a very large, very spread-out empire.
3. **Courthouses**, and **Police Stations** under Communism.
4. **The Commercial trait**, which reduces it empire-wide.
5. **Culture** — a high-culture city is slightly less corrupt.

A corrupt city is still worth having: it produces culture, it holds territory, it can build cheap
units, and it counts toward domination.

---

## 5. Government

| Government | Best for | Cost |
| --- | --- | --- |
| Despotism | The first 40 turns and nothing else | Tile penalty cripples growth |
| Monarchy | Early-to-mid warmongering | Mediocre commerce |
| Feudalism | A war you intend to fight with cheap units | Very poor commerce |
| Republic | Almost every builder game | War weariness |
| Democracy | Peaceful high-commerce endgames | Severe war weariness; can't pop-rush |
| Communism | Large, sprawling, corrupt empires; long wars | Flat but high corruption |
| Fascism | Short brutal wars | Population loss on adoption |

**Get out of Despotism as fast as you can.** Its tile penalty (any tile producing 3+ of something
loses 1) is a permanent tax on your whole empire. Monarchy or Republic by ~1000 BC is a reasonable
target; Religious civs can switch governments almost freely because their anarchy is one turn.

Your government also caps how high any one rate slider can go — Despotism allows far less than
Democracy. If you set a slider and the game snaps it back, that cap is why.

---

## 6. The economy: rates, research and gold

Tax, science and luxury are set as three sliders that always total 100%. In memory they are stored as
0–10, which is why the trainer shows and edits them in tens of percent.

- **Run science as high as your treasury tolerates.** Being first to a key military technology decides
  more games than any amount of banked gold.
- **Deficit-spend deliberately.** Running a negative balance to reach a tech first is fine as long as
  you can see where the gold comes from before you hit zero. At zero the game sells your buildings.
- **Luxury is an emergency valve** for empire-wide disorder, not a permanent setting. Fix happiness
  with temples, markets and luxuries; use the slider only for spikes.
- **Sell technology to the AI** — aggressively. Techs are the most liquid asset in the game, and every
  AI overpays for one it does not have.
- **Trade luxuries**, not just techs. Each unique luxury you connect makes a step change in your
  cities' happiness.

Gold per turn is not a stored number in the game — it is recomputed from your cities each turn — which
is worth knowing if you are wondering why a trainer can edit your treasury but not your income.

---

## 7. Culture

Culture accumulates per city and empire-wide, and it does two things:

1. **Borders expand** at culture thresholds. Territory taken this way is free and permanent.
2. **Cities flip.** A high-culture neighbour can take one of your cities without a fight, especially a
   city you captured whose population is still theirs. This works both ways and is the main reason a
   quick conquest can unravel.

Practical advice: build a temple early in every city, especially border cities; a library soon after.
Keep captured cities garrisoned and consider starving them down or razing them if their culture is
overwhelmingly foreign. The Forbidden Palace and courthouses reduce flip risk too.

Cultural victory needs 100,000 culture empire-wide or 20,000 in a single city — a long game, but a
real one if you get an early wonder lead.

---

## 8. War

**Combat maths.** Each round compares attacker's attack against defender's defence, both modified by
terrain, fortification, city walls, river crossings and veteran level. Hit points come from the
veteran ladder: conscript 2, regular 3, veteran 4, elite 5. A veteran unit is not "slightly better" —
an extra hit point is roughly a third more staying power.

**Therefore: build barracks before you build an army.** Veteran units win fights regular units lose,
and the Militaristic trait plus barracks generates the Great Leaders that make Armies.

**Stacks, not single units.** Move a defender with your attackers. A lone attacking unit in the open
is a free kill for the AI.

**Artillery and bombardment** reduce a defender's hit points without risking your own units. Catapults
are marginal; cannon and artillery are not. Bombard first, then attack.

**Armies** (from a Great Leader, or built with the Military Academy) hold three or four units, heal
outside cities, and are the most efficient way to crack a well-defended city.

**Resources decide what you can build.** Iron, horses, saltpeter, coal, rubber, oil, aluminium and
uranium each gate a line of units. Losing a resource mid-war — because a road was pillaged or a
neighbour cancelled a trade — can strand an entire army. Always know where your iron is, and keep a
spare source or a trade partner.

**War weariness** under Republic and Democracy is real and cumulative. Plan short wars, or take
Monarchy/Communism/Feudalism if you plan a long one.

---

## 9. Technology and wonders

The tech tree branches hard after the ancient era; you cannot have everything. Pick a lane:

- **Military lane** — Bronze Working → Iron Working → Feudalism → Chivalry → Gunpowder → Military
  Tradition. Get there first and you get a window where your units simply beat theirs.
- **Economic lane** — Writing → Literature → The Republic → Currency → Trade → Banking.
- **Expansion lane** — Pottery, Masonry, Mathematics; unglamorous, but granaries and aqueducts are
  what let cities grow past their first ceiling.

**Great wonders worth prioritising**, roughly in order of how often they decide a game:

| Wonder | Why |
| --- | --- |
| Pyramids | A granary in every city on the continent — enormous with an Agricultural civ |
| Great Library | Free techs while you are behind; excellent on higher difficulties |
| Colossus | +1 commerce per ocean tile in its city; brutal in a coastal capital, doubly so as Seafaring |
| Temple of Artemis / Hanging Gardens | Empire-wide happiness, which buys you a higher tax rate |
| Leonardo's Workshop | Free unit upgrades — turns an obsolete army into a modern one |
| Adam Smith's | Pays the upkeep on all your cheap buildings |
| Hoover Dam | A power plant in every city, no pollution |

**Small wonders** are per-civ and cheap by comparison: the Forbidden Palace (see §4), Heroic Epic,
Military Academy, Wall Street, Iron Works. The Forbidden Palace is the one to plan around.

Do not chase a wonder you are not clearly going to win. A failed wonder converts to gold, but the
shields would almost always have been better spent on settlers or units.

---

## 10. Victory conditions

| Victory | Requirement | Typical player |
| --- | --- | --- |
| Conquest | Destroy every other civ | Rare — long and grindy |
| **Domination** | Hold ~66% of land area and population | The usual warmonger win, and much faster than Conquest |
| Cultural | 100,000 culture empire-wide, or 20,000 in one city | Builder with an early wonder lead |
| Space Race | Build and launch the spaceship | Tech leader who is safe at home |
| Diplomatic | Elected in the United Nations | Opportunistic; needs the AI not to hate you |
| Histographic | Highest score at 2050 AD | The default if nobody else wins |

Domination is the one people miss. If you are winning a war you are often much closer to a domination
victory than to conquering everyone — check the F8 histograph and the percentages before you commit
to another twenty turns of fighting.

---

## 11. Playing the nine conquests

The conquests are not the epic game with different graphics. They are short, scored on **victory
points** held at a turn limit rather than on the standard victory conditions, and they usually start
you at war with a fixed unit roster and no time to tech out of trouble.

| # | Conquest | Era | Note |
| --- | --- | --- | --- |
| 1 | Mesopotamia | 4000–1000 BC | Tight land, rivers decide everything. Placement > everything else. |
| 2 | Rise of Rome | 280 BC – AD 100 | Legions vs war elephants. Rome must convert its head start early. |
| 3 | Fall of Rome | AD 350–600 | The most asymmetric. As Rome, trade space for time. |
| 4 | Middle Ages | AD 1000–1500 | Culture moves more borders than armies do. |
| 5 | Mesoamerica | AD 500–1500 | No horses, no iron cavalry, jungle everywhere — a genuinely different game. |
| 6 | Age of Discovery | AD 1492–1780 | Naval. A landlocked capital is nearly a loss. |
| 7 | Sengoku – Sword of the Shogun | AD 1467–1600 | At war from turn one, no room to expand peacefully. |
| 8 | Napoleonic Europe | AD 1795–1815 | France must win fast, before the coalition's economy tells. |
| 9 | WWII in the Pacific | AD 1941–1945 | Carriers, subs and island airbases; land combat is single-tile islands. |

General conquest advice: **read the scenario's victory-point locations before your first move**, and
work backwards from them. Holding three of the right tiles beats destroying an army.

---

## 12. Quick reference

**Units are cheap; time is not.** Shields spent on a unit you never use are gone; a turn spent not
expanding in 3000 BC compounds for the rest of the game.

**Difficulty changes the AI's bonuses, not its brain.** On Emperor and above the AI starts with extra
settlers and units and pays less for everything. You beat it by out-expanding it early, not by
out-fighting it late.

**Things that are usually mistakes:**

- Staying in Despotism past 1500 BC
- Spacing cities six or more tiles apart "so they can grow"
- Building the Forbidden Palace next to your capital
- Attacking with a stack that has no defender in it
- Chasing a wonder you are not going to win
- Ignoring the F8 histograph until 1900 AD

**Things that are usually right:**

- One more settler
- One more worker
- Barracks before the war, not during it
- Selling every tech you have to everyone who will buy it
- Checking whether you are already close to a domination win

---

*If you are using the trainer alongside this guide: it edits treasury, tax/science/luxury rates,
culture, era and research points per civilization, and heals / refreshes / promotes units. It does
not grant technologies or edit gold-per-turn — see `ReverseEngineering.md` for why.*
