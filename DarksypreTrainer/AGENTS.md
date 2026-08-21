# DarksypreTrainer — Agent Guide

## Game

DarkSpyre (Event Horizon Software, 1990). Real-time dungeon-crawler RPG with randomly
generated dungeons. Runs under DOSBox / DOSBox-X as a DOS program.

## Project Layout

```
DarksypreTrainer/
├── .docs/
│   └── Proposal.md              — original task description
├── docs/
│   ├── ReverseEngineering.md    — game analysis, engine notes, data structures
│   └── StrategyGuide.md         — controls, walkthrough, tips, maps
├── .data/                       — git-ignored: memory dumps
├── .game/                       — git-ignored: original game files
├── src/DarksypreTrainer/
│   ├── Game/
│   │   ├── GameFacts.cs         — confirmed constants from manual/walkthrough
│   │   ├── ScanGuide.cs         — guided-scan recipes for each character stat
│   │   ├── SpellBook.cs         — 14 spells across 6 magic classes
│   │   ├── WeaponBook.cs        — 7 weapon proficiency types
│   │   ├── MonsterBook.cs       — 14 monster types by combat category
│   │   └── RuneBook.cs          — 25 runes (5 power runes + 20 others)
│   ├── ViewModels/
│   │   ├── IScanHost.cs         — read/write interface for scan-result and freeze rows
│   │   ├── ScanValue.cs         — decimal/hex parse helpers and width-fit guards
│   │   ├── ScanResultViewModel.cs
│   │   ├── FrozenValueViewModel.cs
│   │   └── MainViewModel.cs     — attach, value scan, guided scans, freeze table
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .cs    — 6-tab WPF UI
│   ├── app.manifest             — requireAdministrator
│   └── DarksypreTrainer.csproj
├── test/FormatCheck/
│   ├── FormatCheck.csproj
│   └── Program.cs               — 85+ checks: constants, reference tables, scan helpers
├── DarksypreTrainer.sln
├── AGENTS.md                    — this file
├── README.md
└── Run.ps1
```

## Architecture

DarkSpyre's mutable game state (HP, SP, attributes, encumbrance, level, score) is stored
in DOSBox guest RAM at a session-specific address. Without a confirmed static byte signature
to anchor a locator, the trainer uses a **value-scanner** model identical to
`QuestForGlory1Trainer`, `MoriaTrainer`, `BattleTech1Trainer`, and `ThePerfectGeneral2Trainer`:

1. **Attach** to the DOSBox process via `ProcessMemory.Open`.
2. **Scan** with `MemorySearcher` (from `GameTrainers.Common.Memory`) — first scan snapshots
   all matching bytes; subsequent scans narrow by Exact / Increased / Decreased / Changed /
   Unchanged.
3. **Pin** a survivor to the freeze table (`FrozenValueViewModel`) for live editing.
4. **Freeze** to re-write the value every ~200 ms so the game can't move it back.

Guided-scan recipes (`ScanGuide`) pre-configure the scan width and give the user step-by-step
instructions for each stat. The game can be paused with **P** to read values safely.

References `GameTrainers.Common` (both `Memory` and `Mvvm` namespaces via csproj `<Using>`
items). `ObservableObject` uses `SetField`; commands are `RelayCommand`.

## Coding Conventions

Follow the root `AGENTS.md`: 4-space indent, file-scoped namespaces, `sealed` classes,
PascalCase members, `_camelCase` private fields, XML `///` docs on public members, no
comments unless asked.

## Build & Run

```powershell
.\Run.ps1                         # build Release and launch (UAC prompt)
.\Run.ps1 -Configuration Debug    # debug build
.\Run.ps1 -Clean                  # clean then build
.\Run.ps1 -NoRun                  # build only
.\Run.ps1 -Test                   # build and run FormatCheck verification harness
```

## Verification

`test/FormatCheck` runs 85+ headless checks with no live game or copyrighted files:
- `GameFacts` constants (all confirmed values from manual/walkthrough)
- `ScanGuide` recipe count, widths, ranges, and defaults
- `SpellBook` spell count, class assignments, SP costs
- `WeaponBook` type count, proficiency names, `ById` lookup
- `MonsterBook` monster count, categories, specific entries
- `RuneBook` rune count, power rune count, specific rune effects
- `ScanValue` parse/fit/canonicalize helpers
- `FrozenValueViewModel` width guard and freeze behaviour through a `FakeHost`
