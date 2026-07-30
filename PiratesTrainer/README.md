# Pirates! Trainer

A Windows-only C#/WPF **live-memory trainer** for *Sid Meier's Pirates!* (MicroProse, 1987 — IBM
version 432.02) running under DOSBox. It attaches to the emulator process, locates the game's data
segment by three static strings, and reads/writes the player's state live — gold, crew, personal
wealth, land and the game clock — with a Cheat-Engine-style value scanner as the fallback.

It also ships the game's own tables, decoded out of the disk images: every settlement in all six eras
with its garrison, population, treasury and map position, and the Treasure Fleet / Silver Train
itineraries — which are simultaneously the answer key to the 1987 manual's copy-protection question.

> Single-player, offline, for your own copy of the game. It reads and writes the emulator's memory only;
> it never touches the network and never edits your save disk.

## Requirements

* Windows, .NET 8 SDK
* DOSBox or DOSBox-X running *Pirates!*
* Administrator rights — the app manifest requests them, because `ReadProcessMemory` /
  `WriteProcessMemory` on another process need them. A UAC prompt appears on launch.

## Build and run

```powershell
.\Run.ps1                      # build Release and launch (UAC prompt)
.\Run.ps1 -Test -NoRun         # run the verification harness, don't launch the GUI
.\Run.ps1 -Configuration Debug # ...also -Clean, -NoBuild, -Publish
```

Or from the repository root: `.\Run.ps1 -Trainer Pirates`.

## Using it

1. Start the game in DOSBox. **Run `PIR.EXE`**, not `DISKP` — `PIR.EXE` is the loader that serves the
   game's disk reads out of `DISK1` / `DISK2` / `DISKS`.
2. Get *into* a game — past the era and character screens. The settlement table only exists once a game
   has started, and the trainer uses it to verify it found the right memory.
3. In the trainer's **Live** tab, pick the `dosbox` process and **Attach**.
4. Click **⚡ Auto-locate**. It sweeps the emulator's memory for the game's data segment and pins
   everything it knows about.
5. **Check the yellow summary line** — captain, date, era, gold, settlement count — against what the
   game is showing. If it matches, the base is right. If it doesn't, Detach and use a guided scan.
6. Edit a **Target** in the Freezes tab to poke a value once, or tick **Freeze** to re-write it every
   ~200 ms so the game's own tick can't undo it.

**💰 Max gold** does the whole thing in one click: locate, set the purse to 65,535, freeze.

### What auto-locate pins

| Value | DGROUP offset | Width | Evidence | Notes |
|---|---|---|---|---|
| Gold | `0x4847` | 16-bit | **Confirmed** | Your purse. Unsigned; the game saturates at 65,535 rather than wrapping |
| Crew | `0x4843` | 16-bit | Inferred | Active party's head count |
| Wealth | `0x4742` | 16-bit | **Confirmed** | Accumulated personal wealth, in **tens** of gold pieces |
| Land | `0x4745` | 8-bit | **Confirmed** | Land grants, in units of **50 acres**; pays a monthly income |
| Day of year | `0x9A9F` | 16-bit | **Confirmed** | 0–359. Freeze it to stop the calendar |
| Years elapsed | `0x9A9D` | 16-bit | **Confirmed** | Year = 1560 + 20 × era code + this |
| Month | `0x9A2B` | 16-bit | **Confirmed** | Derived each tick as day ÷ 30 — freeze the *day*, not this |
| Era code | `0x475A` | 8-bit | **Confirmed** | 0=1560, 2=1600, 3=1620, 4=1640, 5=1660, 6=1680 |
| Rank | `0x473D` | 8-bit | Inferred | Ensign … Marquis |
| Pirate points | `0x9A27` | 16-bit | Inferred | Retirement score out of 100 |

"Confirmed" means a routine in the disassembly can only mean that field; "Inferred" means it is
consistent with the code but not pinned by a single unambiguous routine. All of it was derived
**statically** and has not been checked against a running game — which is why the trainer validates
three independent anchors, decodes the settlement table, and shows you what it read before you poke
anything. `docs/Pirates-ReverseEngineering.md` has the derivations and an honest confidence table.

### Settlements (live)

After a locate, this tab lists the era's towns straight out of the running game — name, nation, forts,
garrison, population and treasury, which drift as you sack them. You can pin a town's treasury byte
(it is in thousands of gold pieces) to see or change how rich it is before deciding whether it is worth
the fight. It is also the clearest possible check that the locator found the right memory: if the names
read correctly, it did.

### If auto-locate fails

Use the guided scans. They search for the number itself, so they do not care about layout at all:

* **Gold** — read the figure on the party panel, type it, First Scan; spend or gain some, type the new
  figure, Exact; repeat to one row; Pin.
* **Crew** — same, from `CREW: n MEN`.
* **Any value** — pick a width and do the same for anything you can watch change on screen (food days,
  cannon, a ship's crew).

Scanner-found pins are labelled `Gold (scanned)` / `Crew (scanned)` so the Freezes grid always tells you
whether a row came from the static layout or from your own scan. That matters because the two can
disagree — the scanned one is the one you verified against the screen, so a later Auto-locate never
removes or overwrites it.

## Reference tabs

* **Convoys** — the Treasure Fleet and Silver Train itineraries for all six eras, per half-month. Both
  the copy-protection answer key and the schedule the convoys actually sail.
* **Settlements (by era)** — every town's starting garrison, population, treasury, prosperity and map
  coordinates.
* **Ships, goods & ranks** — the hull, cargo, rank, speciality, difficulty and expedition tables.
* **Controls** — including **F10 = quit to DOS**, which the loader adds and the game never mentions.
* **Offsets** — what auto-locate pins and how firmly each offset was established.

## Copy protection

The original 1987 release had two: the disk was a booter with deliberately bad sectors, and the game
asked you to look up a convoy date in the manual.

**Neither is active in this build, and no answer is needed.** `PIR.EXE` serves every sector read from an
ordinary file, so the disk check cannot fire; and the manual question is simply not in the program — the
complete 589-record display-string table was decoded and contains no question, no prompt and no
wrong-answer message. The Convoys tab carries the answer key regardless, because the same tables are
where the silver actually is.

## Layout

```
PiratesTrainer/
├─ Run.ps1                    build/launch/test/publish
├─ PiratesTrainer.sln
├─ docs/
│  ├─ Pirates-ReverseEngineering.md   how every offset was derived, with a confidence table
│  └─ Pirates-StrategyGuide.md        how to play, how to win, maps, the full convoy schedule
├─ src/PiratesTrainer/
│  ├─ Game/                   the reverse-engineered knowledge layer
│  │  ├─ PiratesLayout.cs     DGROUP offsets, anchors, calendar and convoy arithmetic, validation
│  │  ├─ GameLocator.cs       three-anchor data-segment locate + live settlement reader
│  │  ├─ GameFacts.cs         ships, goods, ranks, specialities, difficulties, expeditions, controls
│  │  ├─ CityBook.cs          six era settlement tables (generated from DISK1)
│  │  └─ FleetSchedule.cs     convoy itineraries (generated from DISK1)
│  ├─ ViewModels/             hand-rolled MVVM over GameTrainers.Common
│  └─ MainWindow.xaml
└─ test/FormatCheck/          headless harness — 190+ assertions, exits 0/1
```

The project references `GameTrainers.Common` for `Memory` (process access, `MemorySearcher`,
`BytePatternScanner`) and `Mvvm` (`ObservableObject`, `RelayCommand`).

## Safety

* Gold is a 16-bit word capped at 65,535 by the game's own arithmetic — that is what "Max gold" targets,
  and there is nothing sensible above it.
* Freezing **day of year** stops the calendar (so you never age out of your career) while crew, food and
  everything else carry on normally.
* Changing the **era code** desynchronises the settlement table that was loaded for the old era. Don't.
* Save on the save disk before experimenting.
