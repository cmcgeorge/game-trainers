# DarkSpyre — Trainer

A Windows WPF live-memory trainer for **DarkSpyre** (Event Horizon Software, 1990) running
under **DOSBox** or **DOSBox-X**.

## Features

| Tab | What it does |
|---|---|
| **Value Scanner** | Cheat-Engine-style scan: First Scan, narrow by Exact / Increased / Decreased / Changed / Unchanged, pin survivors. Guided-scan recipes for HP, SP, Encumbrance, all 6 attributes, Level, and Score. |
| **Freezes** | Standard pin table: label, address, live value, editable target, freeze checkbox (re-writes every ~200 ms). |
| **Spells** | Reference table of all 14 confirmed spells across the 6 magic classes, with SP costs and descriptions. |
| **Weapons** | Reference table of the 7 weapon proficiency classes with speed, damage, and notes. |
| **Monsters** | Reference table of 14 monster types organized by combat category, with tactics. |
| **Runes** | Reference table of all 25 runes, marking the 5 power runes needed to complete the game. |

## Prerequisites

- **Windows 10/11** — the trainer uses WPF and the Win32 `ReadProcessMemory` / `WriteProcessMemory` APIs.
- **.NET 8 SDK** — `dotnet` must be on your PATH.
- **DOSBox** or **DOSBox-X** running DarkSpyre.
- **Administrator rights** — the app manifest requests elevation; a UAC prompt appears on launch.

## Quick Start

```powershell
cd DarksypreTrainer
.\Run.ps1
```

This restores NuGet packages, builds Release, and launches the trainer (UAC prompt).

Then:

1. Load DarkSpyre in DOSBox and start playing.
2. In the trainer, click **Refresh**, select the DOSBox process, and click **Attach**.
3. Pick a **Guided Scan** recipe (e.g. "Hit Points") from the dropdown on the Value Scanner tab.
4. Follow the instructions: read the value in-game, type it, click **First Scan**.
5. Change the value in-game (take a hit, cast a spell), type the new value, click **Exact**.
6. Repeat until one row remains; click **Pin selected**.
7. On the **Freezes** tab, edit Target to set a value and tick Freeze to hold it.

## Run Script Options

```powershell
.\Run.ps1                         # build Release and launch
.\Run.ps1 -Configuration Debug    # debug build
.\Run.ps1 -Clean                  # wipe bin/obj first, then build and launch
.\Run.ps1 -NoBuild                # skip build, launch existing exe
.\Run.ps1 -NoRun                  # build only
.\Run.ps1 -Test                   # build and run the FormatCheck verification harness
.\Run.ps1 -Publish                # single self-contained win-x64 exe
```

## How It Works

DarkSpyre is a real-time dungeon-crawler RPG that runs as a DOS program. The game's mutable
state (HP, SP, attributes, encumbrance, level, score) is stored in the emulator's guest RAM
at a session-specific address. The trainer uses a **value-scan** approach:

1. Snapshot all memory matching the known value.
2. Perform an in-game action that changes the value (take a hit, cast a spell, pick up an item).
3. Narrow the candidate list by scanning for the new value or by a relative comparison.
4. When only one address remains, pin it to the freeze table.

Guided-scan recipes pre-configure the scan width and give step-by-step instructions for each
stat. Press **P** in-game to pause while reading values.

See `docs/ReverseEngineering.md` for detailed game analysis and engine notes.
See `docs/StrategyGuide.md` for gameplay help, controls, and tips.

## Notes

- Game assets (`.game/`), memory dumps (`.data/`), and research notes (`.docs/`) are git-ignored.
- The trainer does not touch the network or any external service.
- Supply your own legally obtained copy of DarkSpyre.
