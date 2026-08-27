# Alternate Reality: The City — Live Trainer

A WPF (.NET 8) trainer for the 1987/88 Datasoft DOS RPG **Alternate Reality: The City**. It attaches
to the running game inside DOSBox / DOSBox-X, **finds your character on its own** — no value
searching, no Cheat Engine — and lets you edit it live: all seven attributes, level, experience,
hit points, every coin and valuables field, and your supplies, with freeze toggles and one-click
"max" actions.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

The layout it edits was not guessed. `CITY.EXE` stores every message as a byte-coded template whose
print opcodes carry the literal data-segment addresses of the variables they render — so the program
names its own status bar and inventory panel. Each field was recovered that way and then
**confirmed against the running game**: written, and watched change on screen. The teardown is
written up in [docs/ReverseEngineering.md](docs/ReverseEngineering.md).

---

## Quick start

1. **Launch the game** in DOSBox / DOSBox-X. Skip the launcher and run `CITY.EXE 1` for EGA
   (`CITY.EXE 0` is CGA). Press **Esc** past `NO JOYSTICK CONNECTED`.
2. **Load a character** — press **E** and pick one from the roster, or **N** to make a new one.
   The trainer has nothing to find until a character exists in memory.
3. **Build and run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `ARTrainer.exe`, which requests administrator rights via UAC —
   reading and writing another process's memory needs them, especially if the emulator is elevated.
4. **Attach:** pick the DOSBox process from the dropdown (emulators are sorted to the top) and click
   **Attach**. It locates the character automatically and fills the editor in.

If it says nothing was found, make sure you are actually *in* the city — past the
`Hit N / E / T` prompt — and click **Locate character**.

---

## What it can edit

| Group | Fields |
| --- | --- |
| **Identity** | Name (up to 20 characters) |
| **Progression** | Level, Experience, "next level at" threshold, Hit Points, Hit Points (max) |
| **Attributes** | Strength, Intelligence, Wisdom, Skill, Stamina, Charm, and the hidden Physical Speed |
| **Money** | Gold, Silver, Copper, Precious Gems, Jewelry |
| **Supplies** | Food Packets, Water Flasks, Crystals, Keys, Compass, Watch |

Edits are written to the game *immediately*. The game only repaints its status bar when it has a
reason to (an encounter starts, you change location), so a change can take a few seconds to appear
on screen — the value is already live.

### Freeze toggles

| Toggle | Effect |
| --- | --- |
| **Freeze hit points** | Re-pins current hit points to their maximum every poll tick. |
| **Freeze attributes** | Holds all seven attributes at the values they had when you ticked the box — the answer to a Ghost's permanent Strength drain. |
| **Freeze food/water** | Holds the food and water counters. |
| **Freeze money** | Holds gold, silver and copper. |

### Quick actions

**Full Heal**, **Max Attributes**, **Max Hit Points**, **Max Money**, **Fill Supplies**,
**Level Up**, **Max Everything**.

The "max" targets are conservative safe caps rather than field maximums: attributes 200 (a level-up
adds +1 to every attribute, and the field is one byte), hit points 9,999, each coin field 60,000
(they are 16-bit), supply counters 99. **Level Up** raises experience to the threshold the game is
waiting for; the game levels you on its next check, recomputes the threshold, and hands out its own
+1-to-everything bonus.

### Not editable, and why

* **Hunger, thirst and weariness** live in a block the game rewrites every tick and that reverts any
  write within seconds. Freeze food and water instead.
* **Map position.** Searched for and **not found** — five one-step snapshots differenced, the same
  again for a twenty-square walk, every candidate cross-checked against the recovered street map, and
  later a whole-RAM pass keeping only bytes that changed when you step off a square and changed back
  when you step home. Nothing moves like a coordinate except the clock. That last pass is **started,
  not finished**: a long straight walk would expose a constant per-step delta, but the starting
  position draws an encounter every few steps and an encounter blocks walking and turning. So there
  is **no teleport and no "you are here" marker**: writing a guessed address would be worse than not
  having the feature. `docs/ReverseEngineering.md` §4.5 records exactly how far the search got, so
  the next attempt can start further along.
* **The clock** is shown but not edited — it is written by the game continuously.
* **Worn and carried items** are decoded far enough to read (`Fine Silver Robe`) but not far enough
  to write safely.

---

## How it finds the character

The game is relocated by DOS, so its data segment lands somewhere different every session and no
address can be hard-coded. `Memory/GameLocator.cs` works backwards from the program's own text:

1. Sweep the emulator's memory for the status-bar header literal
   `Stats STA   CHR   STR   INT   WIS   SKL`, which sits at data-segment offset `0x012A`.
2. Subtract that offset — you now have `DGROUP:0000`.
3. Require at least **two** of three further literals (`Experience`, `Hit Points :`,
   `Magical Flamesword`) to also line up at their own known offsets, so a stale copy of the header
   in a disk buffer cannot be mistaken for the running game.
4. Read the character record at its fixed offset `DGROUP:0x4EB1` and check its shape — a name of at
   least two letters, seven attribute records whose current and maximum values sit at or below the
   natural maximum, a plausible level, hit points within their maximum, and a next-level threshold
   at or above the current experience.

If no anchor matches, the trainer stops and says so. There *is* a **structural scan** for a build
whose display text has moved, but it is behind the separate **Scan anyway** button and never runs on
its own: pointed at a process that is not the game, a predicate like that will eventually find some
byte run that fits — while testing, it cheerfully offered a character called `wwwwwwwwww` from an
unrelated process. Attaching to that silently would let one **Max Everything** click scribble into
another program. When you do use it, the status bar says the find is unanchored and asks you to check
the name and numbers against the game first.

Measured against the live game: **located in ~40 ms, 3 of 3 corroborating literals matched**, with
every decoded field matching the screen.

---

## Verified against the real game

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` runs **417 checks** with no game running (**387** without the character files below):

* the decoded layout against a fixture built from the values confirmed live — the shipped character
  `Neuro` as the game's own status bar rendered him (STA 22, CHR 17, STR 9, INT 12, WIS 16, SKL 11,
  level 2, 818 experience, 10/35 hit points, 3 food, 4 water, a compass);
* that every offset still lands on the data-segment address the game's display template names
  (`STR` at `DGROUP:0x4F1F`, experience at `0x4F73`, gold at `0x4F83`, food at `0x4F8F`, …);
* the locator's anchors and its record-recognition predicate, against both real and deliberately
  broken windows;
* **the locator itself**, driven over a synthetic address space: it finds an anchor cut in half by a
  1 MiB chunk seam (at several different split points), finds a record straddling that seam
  structurally, finds a record in the very last bytes of a region, sweeps **every** region size in
  the awkward chunk-to-chunk+overlap band with the record at its last possible position, accepts two
  validators but not one, rejects an anchor with an unrecognisable record behind it, falls back to
  the structural scan when no anchor is present, salvages past an unreadable page instead of losing
  the megabyte around it, and neither underflows on an anchor near address zero nor ignores
  cancellation;
* name encode/decode, truncation, field clearing, and the refusal to write a name the locator would
  no longer recognise;
* every clamp, every bulk action, and the exact byte range each setter flushes;
* the reference tables, the map geometry (north up, east right, one marker per square, markers
  inside their cells) and the exported SVG;
* the view-model's edit, clamp and freeze behaviour through a fake host — including that a freeze
  armed after the game has moved on pins what the game has *now*, that editing a frozen field
  re-pins it so the edit sticks, and that a poll tick writes nothing when nothing is frozen.

With the copyrighted character files present (see below) it additionally parses each shipped
`ARCCD` file and asserts they are unchanged by reading and that an edit touches only its own field:

```
ARCCD00  Neuro  —  level 2, 10/35 hp, 818 exp
ARCCD01  Darwin  —  level 0, 4/8 hp, 0 exp
ARCCD02  Shadowmancer  —  level 7, 69/69 hp, 51,088 exp
ARCNAME  roster: Neuro, Darwin, Shadowmancer
```

Absent, that group is **skipped with a note** rather than failed. To run it, put copies of the
game's `ARCCD*` / `ARCNAME` files (or a junction to the game folder) in `.game\`, which is
git-ignored.

The live path was exercised too: attach, auto-locate, read every field, write copper and Strength,
read them back, and restore — all against `CITY.EXE` running under DOSBox-X.

---

## Project layout

```
docs/          ReverseEngineering.md  the teardown: how every offset was recovered and confirmed
               StrategyGuide.md       how to play, how to win, and the maps
src/AlternateRealityTrainer/
  Game/        CharacterFormat.cs   the confirmed offset table, caps, anchors, LE accessors
               CharacterRecord.cs   typed mutable view over the 12,288-byte block
               AttributeBook.cs     the seven attributes — storage order vs. display order
               CityBook.cs          every location with coordinates
               CityTerrain.cs       the 64 x 64 street map: parser, terrain kinds, self-check
               CityMap.cs           map geometry and palette; the SVG itself via the shared SvgCanvas
               PotionBook.cs        the 51-row colour/taste identification table
               GameFacts.cs         controls, encounter menu, calendar, item ladders, tips
  Memory/      GameLocator.cs       anchored DGROUP scan + opt-in structural fallback
               (shared)             ProcessMemory / MemoryRegion — from GameTrainers.Common.Memory
               (shared)             SvgCanvas — from GameTrainers.Common.Documents
  ViewModels/  MainViewModel, CharacterViewModel, AttributeViewModel, ReferenceViewModel, ICharacterHost
  App.xaml, MainWindow.xaml         the WPF UI (Character / City map / Locations / Potions / Reference)
test/FormatCheck/                   headless verification, 417 checks
.docs/                              the original proposal (git-ignored)
.game/                              copyrighted character files, if you provide them (git-ignored)
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory layer come from the shared
`GameTrainers.Common` library rather than being duplicated here.

---

## Notes & caveats

* **The game is the source of truth for its own files.** The trainer edits live memory only — it
  never writes `ARCCD`*nn*. Your changes reach disk when *you* press **S** in the game. Copy your
  character files somewhere safe first; death in Alternate Reality is permanent and the game has no
  other undo.
* The game repaints its status bar lazily, so an edit shows up on the next redraw, not instantly.
* Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
* Tested against the `CITY.EXE` shipped with the 1987/88 IBM PC conversion (332,160 bytes). Another
  build would move the data-segment literals, which is what the structural fallback is for.

---

## Also in the box

The **City map**, **Locations**, **Potions** and **Reference** tabs carry the strategy guide with
you. The map draws all 64 × 64 squares of Xebec's Demise — **the real streets, buildings and city
wall**, read out of the game itself — with every inn, tavern, bank, shop, smithy,
healer and guild colour-coded on it — hover a marker for its prices, opening hours and the direction
you have to approach from, pick a building type to highlight just those, zoom from the whole city
down to individual squares, and **Save map…** writes the same thing out as an SVG. Alongside it:
the full potion identification table; the game's own control list and encounter menu;
the weapon and armour ladders; and the eighteen creatures you can attack without losing your moral
alignment. The long-form version is [docs/StrategyGuide.md](docs/StrategyGuide.md).
