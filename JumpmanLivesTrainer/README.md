# Jumpman Lives! Trainer

A live-memory trainer for **Jumpman Lives!** (Apogee Software, 1991) — a DOS platformer by Dave Sharpless, written in Borland Turbo Pascal 6.0.

The trainer attaches to the DOSBox emulator process, auto-locates the game's data segment by scanning for a static byte pattern (the `jp1` jump-trajectory table), and reads/writes the player's lives, score, time bonus, level, speed, and position — with freeze toggles and "max" buttons. **No manual value searching required.**

## Quick Start

1. Start Jumpman Lives! (`JMAN2.EXE`) in DOSBox and get past the main menu
2. Run `.\Run.ps1` (a UAC prompt appears — the trainer needs admin rights for `ReadProcessMemory`/`WriteProcessMemory`)
3. Pick the DOSBox process in the toolbar and click **Attach**
4. The trainer locates the game automatically — edit fields on the **Live** tab

## What It Can Do

- **Edit lives** (0–99), **score**, **time bonus** (0–1500), **current level** (1–45), **speed** (1–8)
- **Edit player X/Y position** (teleport within the level)
- **Enable trainer mode** (the game's built-in 21-lives mode — no need to press TAB ×4)
- **Freeze lives** and **freeze bonus** (hold the values on every poll tick)
- **Max Everything** button — max lives, max bonus, and enable trainer mode in one click

## How It Finds the Game

The game is a Turbo Pascal 6.0 program with a single data segment (DGROUP). The Borland linker map (`JMLIVES!.MAP`) gives every global's offset. The locator sweeps the emulator's memory for the 22-byte `jp1` jump-trajectory table at DGROUP offset `0x7D46`, validates with the `PLAYSPEED` and `ftwo` patterns, and reads the player array at `DGROUP + 0xCFE6`. See `docs/ReverseEngineering.md` for full details.

## Project Layout

```
JumpmanLivesTrainer/
├── src/JumpmanLivesTrainer/
│   ├── Game/
│   │   ├── GameLayout.cs      # all DGROUP offsets, player record layout, LE accessors
│   │   └── GameFacts.cs       # controls, 45 levels, tips (reference data)
│   ├── Memory/
│   │   ├── IMemorySource.cs   # abstraction for testability
│   │   └── GameLocator.cs     # anchored byte-pattern scan (no value searching)
│   ├── ViewModels/
│   │   ├── MainViewModel.cs   # process attach/detach, locate, poll
│   │   ├── PlayerViewModel.cs # editable fields, freezes, live mirror
│   │   ├── ReferenceViewModel.cs
│   │   └── IGameHost.cs       # write-back interface
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / MainWindow.xaml.cs
│   ├── app.manifest
│   └── JumpmanLivesTrainer.csproj
├── test/FormatCheck/
│   ├── FormatCheck.csproj
│   └── Program.cs             # layout constants, LE accessors, validation, locator
├── docs/
│   ├── ReverseEngineering.md  # full RE notes from source code + MAP file
│   └── StrategyGuide.md       # controls, level list, tips
├── JumpmanLivesTrainer.sln
├── Run.ps1
└── README.md (this file)
```

## Build and Test

```powershell
.\Run.ps1                              # Build Release and launch
.\Run.ps1 -Configuration Debug -Test   # Build Debug, run tests, don't launch
.\Run.ps1 -NoRun                       # Build only
.\Run.ps1 -Publish                     # Single self-contained exe
```

The trainer references `GameTrainers.Common` for `ProcessMemory`, `MemoryRegion`, and the MVVM base classes.

## Requirements

- .NET 8.0 SDK (or later)
- Windows 10/11 (WPF)
- DOSBox (or DOSBox-X) running Jumpman Lives!
