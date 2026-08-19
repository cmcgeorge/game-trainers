# Roadwar 2000 Trainer

A Windows/WPF live-memory trainer and save editor for **Roadwar 2000** (Strategic Simulations
Inc., 1987) running under DOSBox or DOSBox-X.

Nothing has to be searched for. The trainer finds the game's data segment by signature — typically
in well under a second — and reads and writes the gang, the fleet, all 120 cities and the overland
position directly. There is no Cheat-Engine-style scan step and no address to type in.

![](src/Roadwar2000Trainer/Assets/app.ico)

---

## What it does

**Gang.** Food, tires, fuel, ammo, guns, medical supplies and antitoxin; crew by all five ranks;
the doctor, drill sergeant and politician (including their skill levels); the Radio Direction
Finder; the snow-tire and fuel specials; the vehicle ceiling; the day and the clock. Read-outs for
cargo capacity, passenger capacity, fuel per move and total supplies, computed the way the game
computes them.

**Freezes.** Food, fuel, ammo, crew, and a "keep vehicles repaired" toggle that pins every
vehicle's structure, manoeuvrability and tires to their maxima. A freeze re-applies its value about
twice a second, and re-takes its snapshot whenever you deliberately change the field — so a ticked
freeze holds whatever you last asked for, rather than fighting your own edits and the quick-action
buttons.

**Vehicles.** All fifteen slots. Edit structure, tires, top speed, manoeuvrability, braking,
acceleration and armour on all five facings. Repair or fully upgrade one vehicle or the whole
fleet. **Add vehicles** — pick any of the nineteen types and the trainer writes a complete,
factory-fresh record into the next free slot and raises the count, or fills the fleet to the
engine's ceiling of fifteen in one press.

**Cities.** All 120, with a filter. Each town's supply level, who holds it, how strongly, and its
five-slot cache. Fill one cache or every cache, clear one town or every town of its residents
(which is what stops residential encounters), restock every town to its shipped supply level, and
jump the gang to any city.

**Map.** The overland map the gang is actually on, drawn from the 2,016 terrain bytes in the
running game — or from either shipped `.MAP` file when you are not attached, with a west/east
selector for planning. Click a square to target it and teleport. Impassable squares are refused
rather than silently accepted, and so is the one city the shipped data stores off the grid.

**Save editor.** Opens a `.RWS` file with the game closed and edits the same fields, because the
save format is a verbatim image of the same memory. Backs the original up once to `.RWS.bak`, and
can diff a save against the running game to tell you whether they match.

**Reference.** The engine's own vehicle table, loot table and 120-city gazetteer, plus the crew
ranks, city factions, foot-gangs, named road gangs, the eight G.U.B. scientists, the six upgrade
shops and the terrain codes — all read out of `START.EXE`'s data segment, not typed in from the
manual. The loot table tells you which of the 28 sites actually pays what, including the single
richest find in the game (a fuel storage tank: 100 fuel, and the engine weights it heavily on
roads).

---

## Requirements

* Windows 10 or 11, .NET 8 SDK.
* Roadwar 2000 running in DOSBox or DOSBox-X.
* Administrator rights. The app manifest requests them, so a UAC prompt appears on launch; reading
  and writing another process's memory needs them, especially when the emulator itself is elevated.

---

## Use

```powershell
.\Run.ps1                      # build Release and launch
.\Run.ps1 -Test -NoRun         # run the format checks, no GUI
.\Run.ps1 -Configuration Debug # debug build
.\Run.ps1 -Publish             # single self-contained win-x64 exe
```

The Save Editor and Map tabs need to know where the game is installed. They probe the usual
locations on each fixed drive; if yours is somewhere else, set **`ROADWAR2000_DIR`** to the folder
or use the Browse button. A folder only qualifies if it holds both `WEST.MAP` and `EAST.MAP`, so a
same-named folder for another game is never picked up by mistake.

Then:

1. Start `START.EXE` in DOSBox and get past the title screens. You do **not** need to have started
   a game — the trainer's anchor is in the executable's initialised data, so it locates as soon as
   the program is loaded. You do need a game in progress before the gang fields mean anything.
2. Pick the emulator in the drop-down and press **Attach**. The status line reports where the data
   segment was found and how long the scan took.
3. Edit. Changes go into the running game immediately; the game's own screens show them the next
   time they redraw (press `G` for gang status, or `X` for supplies).

To edit a save instead, go to **Save Editor**, point it at the game folder and open a `.RWS`. Note
that Roadwar asks for a diskette in drive A: but the PC build writes saves into the directory it
was started from — normally the game folder itself.

---

## How it finds the game

The anchor is the vehicle-type name block — `MOTORCYCLE\0SIDECAR\0COMPACT CONVERTIBLE\0` — which
`START.EXE` places at `DS:0x2254`.

A hit on that string is deliberately **not** enough. While an overlay is being paged in, a second
copy of the same bytes is briefly present in the emulator's RAM, and a write aimed at that copy
lands nowhere the game will ever read. So every candidate must also satisfy:

* the 19-entry pointer table at `DS:0x2366`, whose entries are **absolute data-segment offsets** —
  the first must be exactly `0x2254`, and they must ascend and stay in range. Because the pointers
  carry the base offset with them, this is what separates a real data segment from a scratch copy
  of the same string;
* a vehicle-type table at `DS:0x238C` whose first record is the motorcycle: mass 1, structure 3,
  100 MPH, manoeuvrability 4.

Measured live: **1 validated candidate from 1 anchor hit in 702 ms** across a 168 MB DOSBox-X
working set, confirmed on two different emulator builds — plain DOSBox 0.74 running the game
directly, and DOSBox-X hosting Windows 3.11 with the game inside it.

Every write re-runs that validation immediately before committing, because the emulator can be
closed, or a different program started inside it, between one edit and the next. A failed
re-validation marks the session stale and the UI stops accepting edits until you re-attach.

---

## Two things the game does that will confuse you

**The fuel number on the Gang Status screen is not your fuel.** The engine stores a total, and the
`G` screen prints that total *less two moves' worth per vehicle*, because every vehicle keeps that
much in its tank and the reserve does not occupy cargo space. `X)amine Supplies` prints the stored
figure. The trainer edits the stored figure and shows you what `G` will read.

**Interior crew capacity is stored one lower than it is displayed** — the engine holds 50 for a
trailer truck and the game prints 51, counting the driver. The trainer displays it the game's way.

---

## What it does not do

**It cannot hand you a city.** Controlling cities is the win condition, and locating what feeds the
`E)mpire Status` list was attempted and failed — see §10 of `docs/reverse-engineering.md`, which
records exactly what was probed and ruled out. What the Cities tab offers instead is clearing a
town of its residents, which stops residential encounters there; the UI says so rather than
implying otherwise.

**It cannot move you between the two overland maps.** The engine loads a map's 2,016 terrain bytes
when it reads the file, and nothing the trainer writes makes it re-read one — so setting the map id
alone would leave the game walking on the other continent's terrain. "Go there" refuses a city on
the map you are not on and says why.

There is no tactical-combat editing. The tactical maps (`MAP0`–`MAP22.R2K`) were not decoded.

---

## Layout

```
Roadwar2000Trainer/
├── Run.ps1                        build/launch/test
├── README.md · AGENTS.md
├── docs/
│   ├── reverse-engineering.md     the format, and how each field was established
│   └── strategy-guide.md          how to play, how to win, and both overland maps
├── src/Roadwar2000Trainer/
│   ├── Game/                      the game-knowledge layer
│   │   ├── SaveFormat.cs          every offset, in one place
│   │   ├── GameSlab.cs            cached typed view over a 6,512-byte slab
│   │   ├── GangRecord.cs · VehicleRecord.cs · CityRecord.cs
│   │   ├── VehicleBook.cs · CityBook.cs · LootBook.cs · ReferenceBooks.cs
│   │   ├── OverlandMap.cs         48 × 42 terrain grid
│   │   └── SaveGame.cs            .RWS load/save with a one-shot backup
│   ├── Memory/GameLocator.cs      the only game-specific memory code
│   ├── ViewModels/                one per tab
│   └── MainWindow.xaml
└── test/FormatCheck/              703 headless checks (+7 more with --live)
```

`GameTrainers.Common` supplies the process/guest-memory plumbing and the MVVM base; this trainer
keeps no local copy of either.

---

## Testing

```powershell
.\Run.ps1 -Test -NoRun                          # 703 checks with no game installed
$env:ROADWAR2000_DIR = 'C:\path\to\RW2000'
.\Run.ps1 -Test -NoRun                          # 762 - adds the shipped-data checks
dotnet run --project test\FormatCheck -- --live  # 769 - adds a running DOSBox
```

The harness builds a synthetic slab from the reference tables and drives every record view over
it — including a target that refuses writes, which must leave the cache untouched. When a Roadwar
folder is present it additionally:

* checks the shipped `CHICAGO.RWS` field by field against the figures the game's own Gang Status
  and Vehicle Stats screens print for it;
* pins the whole baked-in city table — size, map and position for all 120 — against
  `START.EXE`'s initialised data, and separately asserts the shipped save still differs from it in
  exactly the 30 towns that save has looted. That pair is what stops the two sources being
  conflated, which is a mistake this trainer made once and now cannot make silently;
* verifies that all 120 city records land on a city tile of their own overland map under the
  engine's index rule.

`--live` attaches to a running DOSBox and exercises the locator end to end. It is skipped, not
failed, when there is no emulator — or when there is one the harness cannot open, which is the
normal case for an unelevated run.

There is also a `RW2K_SMOKETEST` environment variable that loads the window in a Debug build,
walks every tab so all the templates and bindings activate, writes a marker file and exits. It
caught a real fault during development (a two-way binding onto a read-only property) that only
manifests when a human clicks the second tab.
