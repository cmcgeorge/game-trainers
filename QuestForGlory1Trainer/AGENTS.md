# QuestForGlory1Trainer — Agent Guide

## Game

Quest for Glory I: So You Want to Be a Hero (Sierra On-Line, 1989 / 1992 VGA remake).
SCI0 interpreter (`SCIV.EXE`). Runs under DOSBox / DOSBox-X.

## Project Layout

```
QuestForGlory1Trainer/
├── .docs/
│   ├── Proposal.md              — original task description
│   ├── ReverseEngineering.md    — SCI0 engine notes, global-variable indices, room numbers
│   └── StrategyGuide.md         — controls, walkthrough, maps
├── .data/                       — git-ignored: DOSBox RAM dumps + companion CSV
├── .game/                       — git-ignored: original game files (RESOURCE.*, SCIV.EXE)
├── src/QuestForGlory1Trainer/
│   ├── Game/
│   │   ├── GameOffsets.cs       — SCI0 global-variable indices and tick constants
│   │   ├── RoomBook.cs          — room number → name mapping for the Teleport tab
│   │   └── SkillBook.cs         — stat / skill reference table for the Stats tab
│   ├── ViewModels/
│   │   ├── IScanHost.cs         — read/write interface used by scan-result and freeze rows
│   │   ├── ScanValue.cs         — decimal / hex parse helpers and width-fit guards
│   │   ├── ScanResultViewModel.cs
│   │   ├── FrozenValueViewModel.cs
│   │   └── MainViewModel.cs     — attach, value scan, guided scans, Day/Time, Teleport
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .cs    — 5-tab WPF UI
│   ├── app.manifest             — requireAdministrator
│   └── QuestForGlory1Trainer.csproj
├── QuestForGlory1Trainer.sln
├── AGENTS.md                    — this file
├── README.md
└── Run.ps1
```

## Architecture

Quest for Glory I's SCI0 heap is dynamically allocated in DOSBox guest RAM every session;
global variables and Ego-object properties have no stable adjacent byte signature that could
anchor a `BytePatternScanner` locator. The trainer therefore uses a **value-scanner** model
identical to `BattleTech1Trainer` and `ThePerfectGeneral2Trainer`:

1. **Attach** to the DOSBox process via `ProcessMemory.Open`.
2. **Scan** with `MemorySearcher` (from `GameTrainers.Common.Memory`) — first scan snapshots
   all matching bytes; subsequent scans narrow by Exact / Increased / Decreased / Changed /
   Unchanged.
3. **Pin** a survivor to the freeze table (`FrozenValueViewModel`) for live editing.
4. **Pin As Clock / Day / Room** to unlock the higher-level editors:
   - **Day/Time tab**: writes tick value to the game-clock address and day to the day address.
   - **Teleport tab**: writes a room number to the room-number address; the game loads the
     room on the next room transition (or immediately via ALT-T in the game's debug mode).

References `GameTrainers.Common` (both `Memory` and `Mvvm` namespaces via csproj `<Using>`
items). `ObservableObject` uses `SetField`; commands are `RelayCommand`.

## Coding Conventions

Follow the root `AGENTS.md`: 4-space indent, file-scoped namespaces, `sealed` classes,
PascalCase members, `_camelCase` private fields, XML `///` docs on public members, no
comments unless asked. `ScanWidth` values match SCI0 word sizes (Int16 for most globals).

## Build & Run

```powershell
.\Run.ps1                         # build Release and launch (UAC prompt)
.\Run.ps1 -Configuration Debug    # debug build
.\Run.ps1 -Clean                  # clean then build
.\Run.ps1 -NoRun                  # build only
```

No verification harness; `-Test` warns and is ignored.

## Key Reverse-Engineering References

See `.docs/ReverseEngineering.md` for:
- SCI0 global variable indices (room=1, day=3, clock=4)
- Time tick constants (150 ticks/hour, 3600 ticks/day)
- Room number table (confirmed vs estimated)
- Memory dump cross-reference (Day 1 Midday, Thief)
