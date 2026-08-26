# KnightsOfLegendTrainer — Agent Guide

## Game

Knights of Legend (Origin Systems, 1989, by Todd Porter). Party-based DOS RPG with
up to 6 characters, 4 races, 33 classes, 7 primary statistics (0-100), tactical
turn-based combat, 6 magic orders, and 24 quests. Runs as a DOS program under
DOSBox / DOSBox-X.

## Project Layout

```
KnightsOfLegendTrainer/
├── .docs/                       — git-ignored: proposal
├── docs/
│   ├── ReverseEngineering.md    — what is known, what is not, sources, dead ends
│   └── KnightsOfLegend-Strategy-Guide.md — controls, combat, quests, tactics, mouse fix
├── src/KnightsOfLegendTrainer/
│   ├── Game/
│   │   ├── GameFacts.cs         — constants, each marked [Manual] or [Inferred]
│   │   ├── CharacterFormat.cs   — known stat names, combat options, LE helpers
│   │   ├── ScanGuide.cs         — 13 guided scan recipes for live memory
│   │   ├── SaveFormat.cs        — chardata quest status encoding (offsets 482-487)
│   │   ├── RaceBook.cs          — 4 races (Human, Elven, Dwarven, Kelden)
│   │   ├── ClassBook.cs         — 33 classes by race/gender
│   │   ├── WeaponBook.cs        — 36 weapons across 9 training masters
│   │   ├── ArmorBook.cs         — 12 armor types (head/torso/legs/shield)
│   │   ├── SpellBook.cs         — 20 spells across 6 magic orders
│   │   ├── MagicOrderBook.cs    — 6 magic orders with locations and components
│   │   ├── MonsterBook.cs       — 20 monsters by category and quest
│   │   └── QuestBook.cs         — 24 quests with givers, keywords, targets
│   ├── ViewModels/
│   │   ├── IScanHost.cs         — read/write channel for scan-result and freeze rows
│   │   ├── ScanValue.cs         — decimal/hex parse helpers and width-fit guards
│   │   ├── ScanResultViewModel.cs
│   │   ├── FrozenValueViewModel.cs
│   │   ├── SaveEditorViewModel.cs — chardata quest status editor
│   │   └── MainViewModel.cs     — attach, scan, freeze, save editor, reference tabs
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .cs    — 9-tab WPF UI
│   ├── app.manifest             — requireAdministrator
│   └── KnightsOfLegendTrainer.csproj
├── test/FormatCheck/            — headless checks, no live game required
├── KnightsOfLegendTrainer.sln
├── AGENTS.md                    — this file
├── README.md
└── Run.ps1
```

## Architecture

The trainer is **value-scanner-only**. No game binary, memory dumps, or Ghidra
analysis were available, so there is **no `GameLocator`** — the trainer drives
Common's `MemorySearcher` as a Cheat-Engine-style value scanner with 13 guided
scan recipes (Gold Crowns, Adventure Points, Body Points, Max Body Points, the
seven primary statistics, Level, Fatigue, and Rations). All game knowledge comes
from online resources (the manual, walkthroughs, reviews), so every constant
carries a **[Manual]** or **[Inferred]** confidence marker.

It also includes a **chardata save-file editor** for quest status: the save file
stores 24 quests at offsets 482-487 using 2-bit codes (00 = not given, 01 = given,
10 = complete, 11 = medal given). The editor loads a chardata file, displays all
24 quest statuses, and writes changes back with a one-shot `.bak` backup. Only the
quest status region is edited; all other bytes are preserved verbatim.

References `GameTrainers.Common` (`Memory` and `Mvvm` namespaces via csproj
`<Using>` items). `ObservableObject` uses `SetField`; commands are `RelayCommand`.

## Coding Conventions

Follow the root `AGENTS.md`: 4-space indent, file-scoped namespaces, `sealed`
classes, PascalCase members, `_camelCase` private fields, XML `///` docs on public
members, no comments unless they explain something non-obvious.

## Build & Run

```powershell
.\Run.ps1                         # build Release and launch (UAC prompt)
.\Run.ps1 -Configuration Debug    # debug build
.\Run.ps1 -Clean                  # clean then build
.\Run.ps1 -NoRun                  # build only
.\Run.ps1 -Test                   # build and run FormatCheck verification harness
```

## Verification

`test/FormatCheck` runs headless checks with no live game and no copyrighted files:

- `GameFacts` constants
- `CharacterFormat` stat names, combat options, LE helpers
- `SaveFormat` quest status encoding (write/read round-trip, partial rewrite
  preservation, clamping, small-file rejection, all 24 quests x 4 codes)
- `RaceBook` (4 races), `ClassBook` (33 classes), `WeaponBook` (36 weapons,
  9 masters), `ArmorBook` (12 pieces), `SpellBook` (20 spells, 6 orders),
  `MonsterBook` (20 monsters), `QuestBook` (24 quests)
- `MagicOrderBook` (6 orders with locations)
- `ScanGuide` recipes (13 recipes, correct widths, unique fields)
- `ScanValue` parse/fit/canonicalize helpers
- `FrozenValueViewModel` width guard and freeze behaviour
- `SaveEditorViewModel` load/save round-trip, backup creation, SetAllQuests,
  small-file rejection

## Working On This Trainer

The game binary (`kol` command from the `knights` directory) was not available
for static analysis. All offsets and formats come from online sources. If a copy
of the game becomes available, a `GameLocator` could be added by
signature-scanning or structural-scanning the emulator's guest RAM, following the
pattern of `WastelandTrainer` or `DarksypreTrainer`. The `chardata` save file
format is only partially known (quest status at 482-487); the full record layout
for character statistics, inventory, and spells in the save file remains unknown.
