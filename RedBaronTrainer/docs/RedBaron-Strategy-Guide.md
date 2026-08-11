# Red Baron — Strategy Guide

Dynamix / Sierra, 1990. Western Front, December 1915 to 11 November 1918.

Red Baron is a flight simulator with a career attached, and the career is the game. Single missions
are practice; the campaign is where the aircraft change under you, the enemy gets better, and one
bad decision at 8,000 feet ends a pilot you have flown for forty missions. This guide covers the
controls, the flight model, gunnery, the mission types, and how to survive a full tour.

---

## 1. Getting into the air

### The menus

`BARON` from the game directory. From the Main Menu:

| Option | What it is |
|---|---|
| **Dogfight a Famous Ace** | One-on-one against one of eleven historical aces. The fastest way into the air. |
| **Fly Single Mission** | Pick a mission type, aircraft, front sector and date. Practice. |
| **Career Menu** | Start / continue a career, or view the top-ace list. |
| **View Airplanes** | The 22 fighters with their service dates and the aces who flew them. |
| **Other Options** | Preferences (joystick, mouse, music, sound), credits, demo tapes. |
| **Mission Recorder** | Play back, edit and save `.VCR` tapes. |

**Turn Mouse on first.** The shipped `CONTROL.PRF` has it off, which makes the menus look frozen if
you are trying to point at them. Other Options → Preferences → Mouse.

### Controls

**Flight**

| Key | Action |
|---|---|
| Numeric keypad `8` / `2` | Nose down / nose up (elevator) |
| Numeric keypad `4` / `6` | Roll left / right (ailerons) |
| Numeric keypad `7` `9` `1` `3` | Diagonal combinations |
| `<` / `>` | Left / right rudder |
| `1`–`9` | Throttle, 1 = idle, 9 = full |
| `+` / `-` | Throttle up / down one step |
| **Spacebar** | Fire the machine guns |
| `U` | Clear a gun jam |

With a joystick: button 1 fires. Hold **both** buttons and move left/right for rudder, forward/back
for throttle.

**Views**

| Key | From the cockpit | From outside |
|---|---|---|
| `F1` | Forward | Front |
| `F2` | Back | Rear |
| `F3` | Left | Left side |
| `F4` | Right | Right side |
| `F5` | Up | Below |
| `F6` | Down | Above |
| `F7` | — | Chase plane |
| `Return` | Toggle cockpit / external view | |
| `Ctrl-F1`…`Ctrl-F10` | Save the current view | |
| `Alt-F1`…`Alt-F10` | Recall a saved view | |

**Sim commands**

| Key | Action |
|---|---|
| `Alt-P` | Pause (`SIMULATION PAUSED`) |
| `Alt-J` | Toggle joystick and rudder pedals |
| `Alt-R` | Realism Panel |
| `Alt-M` | Toggle mouse |
| `Alt-S` | Toggle sound |
| `C` | Time compression (only away from combat) |
| `F10` | Preferences |
| `Esc` | Continue / End mission / Restart mission |

**Flight-leader orders** (when you lead a flight)

| Key | Order |
|---|---|
| `A` | Attack the enemy |
| `M` | Minor wing attack |
| `D` | Drop below |
| `J` | Join formation |
| `W` | Warn the flight of enemies |

### The instruments

Left to right on a period panel: **oil pressure**, **altimeter** (large needle hundreds of feet,
small needle thousands), **compass**, **airspeed** in mph, **tachometer** (rpm × 100), and the
**inclinometer** — the ball that tells you whether the turn is coordinated.

Watch the **oil pressure gauge**. A falling needle means a hit in the engine, and you have a few
minutes of engine left, not seconds. Turn for home immediately; a dead-stick landing on your own
side of the line still scores.

The **tachometer** is your stall warning in disguise. In a hard climb the airspeed indicator
bleeds off long before the airframe complains — cross-check it against the horizon.

---

## 2. Flying the aeroplane

### Energy is everything

These are 90-to-120 mph aircraft with barely more power than weight. Every manoeuvre spends energy
and only the throttle and gravity put it back, and both are slow. The single most useful habit in
Red Baron is arriving **above** the fight.

- Climb on the way out, not when you get there. Time compression (`C`) makes the climb free in
  real time; use it every time you leave the aerodrome.
- Altitude is stored speed. A 1,000-foot dive buys you a firing pass; a 1,000-foot climb costs you
  one.
- Never bleed to zero in a turning fight you did not start with an advantage. If your airspeed is
  under about 60 mph and someone is behind you, you have already lost — unload, dive, and rebuild.

### Turning

Coordinate with rudder. A skidding turn in a rotary-engined aircraft loses far more energy than a
clean one, and the inclinometer ball shows it directly.

The **Sopwith Camel** and **Fokker Dr.I** turn faster to the right and left respectively than
anything else in the game because of engine torque — the Camel's rotary drags the nose right, so a
right turn is nearly free and a left turn is a fight. Learn which way your aeroplane wants to go
and always break that way.

### The classic manoeuvres

The game ships `.VCR` demonstration tapes for most of these — watch them from the Mission Recorder
before trying them at 2,000 feet.

| Manoeuvre | What it does | When |
|---|---|---|
| **Immelmann** | Half loop, then half roll upright — reverses direction and gains altitude | You have speed to spend and want to come back at a target you just passed |
| **Split-S** | Half roll inverted, then half loop down — reverses direction, loses altitude | You need to disengage downward, or to reverse onto someone below |
| **Barrel roll** | Rolling scissors in one axis — forces an overshoot | Someone faster is closing behind you |
| **Sideslip** | Cross-controlled descent, no speed gain | Losing height on final approach without diving |
| **Slip turn** | Tight, ugly, effective direction change | Low and slow, no room for a proper turn |
| **Loop** | Vertical circle | Rarely useful in combat; you arrive slow and predictable |

The **scissors** is the one that wins fights the game generates: when an enemy overshoots, roll
into him rather than away. Each reversal costs him more speed than it costs you, and rotary-engined
fighters recover badly.

### Blackouts and the airframe

With **Blackouts allowed** on, hard pulls grey the screen and then black it out — and you keep
flying blind for a second or two afterwards. Ease off before the edges close in.

With **Aircraft may be damaged** on, the wings will come off. A high-speed dive followed by a hard
pull is the standard way to lose a career pilot who never saw an enemy.

---

## 3. Gunnery

This is where most missions are won or lost, and Red Baron is unforgiving about it.

### Get close

The guns are fixed, forward-firing, and synchronised through the propeller; there is no gunsight
computer, only a ring and bead. The historical answer applies exactly: **fire from 50 to 100 yards,
from dead astern, and do not fire before that.** Boelcke's dictum in the game's own briefing text
is the correct tactic, not flavour text.

At Combat Level **Hard** the target box is the size of the engine and pilot — the area a real
round had to hit to matter. At Easy it is the whole aeroplane. That single setting changes gunnery
more than any other in the game.

### Deflection

Lead the target. In a turning fight you are shooting at where the enemy will be, and the ring on
the gunsight is your rough scale. Practise on balloons: they do not shoot back, they do not
manoeuvre, and they teach you what the sight picture looks like at the range you want.

### Bursts and jams

With **Gun jams allowed** on, long bursts jam the guns. Fire in **short bursts of about a second**.
Press `U` to clear a jam — it takes time, and in the middle of a fight it is time you do not have.

### Ammunition types

Before a mission you choose **regular** or **incendiary**:

- **Incendiary** — the only realistic way to kill a balloon or a Zeppelin. Much less accurate
  against aircraft.
- **Regular** — everything else.

Pick incendiary only for balloon-busting and Zeppelin hunts; a dogfight with incendiary loaded is
a dogfight you will probably lose.

### Where to shoot

Two-seaters have a rear gunner and a blind spot directly below and behind. Attack from below and
astern, climbing into the belly, and the gunner cannot depress far enough to reach you. Attacking a
two-seater from level astern is how new pilots die.

Balloons are winched down when spotted and are ringed with flak. One fast pass with incendiary,
then keep going — do not turn back for a second look at 500 feet over an alerted battery.

---

## 4. Mission types

| Mission | The task | What actually matters |
|---|---|---|
| **Patrol the front** | Fly the assigned path, engage what you meet | Points come from kills *and* from bringing the flight home. Do not chase. |
| **Escort reconnaissance** | Keep the recon aircraft alive | Stay above and behind them, not beside them. Their survival is scored. |
| **Escort a bombing raid** | Same, for bombers | The bombers are slow; do not out-climb them into a fight they cannot join. |
| **Stop a bombing raid** | Intercept before the target | Altitude before position. Meet them high or not at all. |
| **Hunt a Zeppelin** | Kill an airship | Incendiary. Attack along the length of the hull, not across it. Its gunners have blind spots above and below the nose. |
| **Balloon defence** | Protect your own balloons | Sit above the balloon line and dive on attackers; do not go looking. |
| **Balloon busting** | Kill enemy balloons | Incendiary, one pass, low and fast, egress on the deck. |
| **Dogfight a squadron** | Straight fight | Pick off stragglers at the edge of the formation. |
| **Fly a historic mission** | Eight recreations | See below. |

### The historic missions

`HISTORIC.DAT` ships eight, most flyable from either side:

- **Hawker Meets His Match** — Richthofen vs. Major Lanoe Hawker, a long turning duel. Flying
  Hawker is the harder and better fight.
- **Immelmann's First Victory**
- **Voss Meets McCudden** — Voss alone against a flight of D.H.2s.
- **Richthofen Cheats Fate**
- **The Master Meets His End** — Richthofen's last flight, 21 April 1918.
- **Lt. Roth Goes Balloon Hunting**
- **Udet Defends A Drachen**
- **Frank Luke, Balloon-Buster**

### Dogfighting the aces

Eleven historical aces, with their real victory totals:

| Ace | Victories | Notes |
|---|---:|---|
| Manfred von Richthofen | 80 | Patient, tactical, will not follow you into a bad position |
| Ernst Udet | 62 | Superb marksman |
| Erich Loewenhardt | 53 | |
| Werner Voss | 48 | Wildly aerobatic; the hardest pure dogfight in the game |
| Rudolf Berthold | 44 | |
| Oswald Boelcke | 40 | |
| Lothar von Richthofen | 40 | Reckless — he will overcommit, so let him |
| Ritter von Schleich | 35 | |
| Karl Degelow | 30 | |
| Hermann Goering | 22 | |
| Max Immelmann | 17 | Early-war aircraft, so an early-war fight |

Against the good ones, do not turn with them. Use the vertical: extend, climb, come back down. An
ace AI that follows you into a climb it cannot sustain is an ace AI you can shoot.

---

## 5. The aircraft

Twenty-two fighters, entering service across the war. The dates below come from `FIGHTER.DAT` and
match history.

**Allied**

| Aircraft | In service | Notes |
|---|---|---|
| Morane Bullet | 1915 | Monoplane, early, fragile |
| Nieuport 11 | 1915 | The first answer to the Fokker scourge |
| Airco D.H.2 | late 1915 | Pusher; gun in the nose, no synchronisation needed |
| Nieuport 17 | Mar 1916 | Fast climb, one gun, weak lower wing — do not dive it hard |
| Sopwith Pup | 1916 | Delightful handling, underarmed |
| Sopwith Triplane | 1917 | Extraordinary climb and turn |
| Spad 7 | Jun 1916 | Dive and zoom, do not turn |
| S.E.5a | Apr 1917 | Stable gun platform, strong, good high up — the best all-round Allied machine |
| Sopwith Camel | May 1917 | Two guns, vicious right turn, kills the careless |
| Spad 13 | Aug 1917 | Faster Spad 7; same energy tactics |
| Nieuport 28 | 1918 | Pretty, sheds fabric |
| Sopwith Snipe | Aug 1918 | Late-war Camel done right |

**German**

| Aircraft | In service | Notes |
|---|---|---|
| Fokker Eindecker | 1915 | The first synchronised gun; unimpressive once matched |
| Halberstadt D.II | 1916 | |
| Albatros D.II | Aug 1916 | Two guns, fast, the reason 1916 was Germany's |
| Albatros D.III | Jan 1917 | Sesquiplane; strong except the lower wing in a dive |
| Albatros D.Va | 1917 | Same again, heavier |
| Pfalz D.III | Jun 1917 | Sturdier than the Albatros, less agile — good for balloon work |
| Fokker Dr.I | 1917 | Climbs and turns like nothing else, slow in a straight line |
| Fokker D.VII | Apr 1918 | The best aeroplane in the game: hangs on its propeller, forgiving, fast enough |
| Fokker D.VIII | Jul 1918 | Parasol monoplane, excellent view |
| Siemens-Schuckert D.IV | 1918 | Superb climb at altitude |

**How to fly what you are given.** Rotary-engined turn-fighters (Camel, Triplane, Dr.I) win slow,
tight, low fights. In-line energy fighters (Spad, S.E.5a, Albatros, D.VII) win fast, vertical,
high fights. Flying a Spad like a Camel gets you killed; so does flying a Camel like a Spad.

---

## 6. Realism, scoring and the career

### The Realism Panel

Thirteen settings, reachable with `Alt-R` in flight or from any briefing, and three presets
(Novice / Intermediate / Expert):

| Setting | What turning it on costs you |
|---|---|
| Realistic instruments | Period gauges instead of simplified ones |
| Sun blind spot | Looking into the sun washes out the view |
| Realistic weather | Wind and cloud actually affect the flight |
| Gun jams allowed | Long bursts jam the guns |
| Blackouts allowed | High-g pulls grey and black out the screen |
| Carburettor freezes | The engine can cut at altitude |
| Limited ammunition | Finite rounds |
| Limited fuel | Finite fuel |
| Real navigation | The map stops showing where you are |
| Aircraft may be damaged | Hits and hard landings do real damage |
| Combat level | Easy / Standard / Hard — target size and flak lethality |
| Midair collisions | Flying into another aircraft is fatal |
| Flight model | Novice / Intermediate / Expert handling |

**Combat level** is the one that changes the game most. On Easy flak never hits you and enemy
target boxes are generous; on Hard the target box is engine-and-pilot sized and flak will kill you.

Note from the game's own release notes: a setting can become **locked on**. If Aircraft May Be
Damaged is on and your aeroplane has been fatally shot up, you cannot switch it off — the box goes
solid. Decide before the mission, not during it.

### Scoring

Mission score comes from:

- enemy aircraft, balloons and Zeppelins destroyed;
- completing the mission objective;
- members of your flight who survive;
- landing back at your home aerodrome (the biggest single bonus most missions offer);
- multiplied by the realism settings you flew with.

The last two are the ones players leave on the table. **Getting home is worth more than one extra
kill**, and a career flown at Novice scores a fraction of the same flying at Expert.

### The career

Start a career and pick a side: **Royal Flying Corps** or the **German Air Service**. You enlist at
a date, get posted to a squadron and an aerodrome, and fly until the Armistice, until you are
killed, or until you are retired.

- **Promotion is by victories, not score.** RFC: 2nd Lieutenant → 1st Lieutenant → Captain. German:
  Leutnant → Oberleutnant → Rittmeister.
- **Medals** come with victories and notable missions: the Victoria Cross for the RFC, the *Pour le
  Mérite* — the Blue Max — for Germany. The Red Eagle Order is Richthofen's alone.
- **Request Transfer** moves you between squadrons. An elite squadron (Jasta 11, JG 1, No. 56
  Squadron, The Black Flight, the 94th Aero) gets better aircraft sooner and better wingmen — worth
  doing as soon as you have the victories for it.
- **Personal Aircraft** unlocks at Captain / Rittmeister: your own paint scheme, which is also how
  the enemy AI recognises you.
- **Backup Career** from the Aerodrome menu. Use it. Careers end permanently.

### Surviving a tour

1. **Fly for the career, not the mission.** A pilot who comes home from forty missions with two
   kills each outscores one who wins a spectacular fight and dies in month four.
2. **Never follow a diving enemy across the lines.** That is how you meet flak with no altitude,
   no fuel margin and no friends.
3. **Check your six on a timer.** `F2` every few seconds. Most career deaths are from an aircraft
   the player never saw.
4. **Break off at 25 % fuel.** Wind is real with Realistic Weather on, and a headwind home is
   longer than the flight out.
5. **Take the aerodrome landing bonus.** Line up early, throttle back, sideslip off the extra
   height. If your home base is set, it fires flares to guide you in at dawn, dusk and night.
6. **Upgrade when offered.** New aircraft arrive on their historical dates; a 1917 pilot still in
   an Albatros D.III in July 1918 is flying an antique.

---

## 7. Mission Builder and tapes

The 1992 upgrade adds `RBMB.EXE`, which builds custom missions. From the shipped `TIPS.DOC`:

- Put a friendly unit's **final path point over friendly territory**. Bombers or recon aircraft
  left circling over enemy ground count as in jeopardy and cost you score.
- Put the **player's last path point on a friendly aerodrome** to make it the home base. That
  enables the landing bonus and the guidance flares.
- **Non-player aircraft cannot land.** They circle overhead while you do.
- Tight groups of path points are easier to edit through the **All Groups** menu, which brings the
  selected group's points to the front.
- Memory is finite. In decreasing order of free near memory: **London, Dunkirk, Verdun, Somme,
  Paris** — switch world if a mission will not fit.
- Overcast cloud is always centred at 15,000 feet.
- Maximum 200 custom missions.

Tapes recorded with the Mission Builder upgrade will **not** play back in the original Red Baron
VCR. Any tape in `TAPES\` whose name begins with `DEMO` is played by Other Options → Watch Demo, so
you can replace the shipped demo reel with your own.

---

## 8. Running it well under DOSBox

Red Baron times the game port itself and is sensitive to emulated CPU speed, so the defaults are
wrong for it in two ways. A configuration that works:

```ini
[dosbox]
machine=svga_s3
memsize=16

[cpu]
core=normal
cputype=386
cycles=fixed 12000      ; NOT max - the game is unplayably fast and the joystick stops working

[joystick]
joysticktype=2axis      ; not 'auto'
timed=false
```

If the game does not see your controller, the cause is almost always one of three things — an
unbounded `cycles` setting, `joysticktype=auto` when SDL saw no stick at start-up, or Windows
having put the pad on joystick slot 1 with slot 0 empty. All three are diagnosed in the trainer's
**Joystick** tab, and the mechanism is documented in
[RedBaron-Reverse-Engineering.md](RedBaron-Reverse-Engineering.md).

---

## Sources

- The game's own `READ.ME` (release notes, 19 Dec 1990) and `TIPS.DOC` (Mission Builder, 1992).
- In-game data files: `FIGHTER.DAT`, `ACE.DAT`, `HISTORIC.DAT`, `ELITE.DAT`, `ORDINARY.DAT`.
- [Red Baron manual — Lemon Amiga](https://www.lemonamiga.com/games/docs.php?id=1336)
- Behaviour confirmed in play against the DOS release under DOSBox-X.
