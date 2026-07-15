# Quest for Glory I — Trainer

A Windows WPF live-memory trainer for **Quest for Glory I: So You Want to Be a Hero** (Sierra
On-Line, 1989 / 1992 VGA remake) running under **DOSBox** or **DOSBox-X**.

## Features

| Tab | What it does |
|---|---|
| **Value Scanner** | Cheat-Engine-style scan: First Scan, narrow by Exact / Increased / Decreased / Changed / Unchanged, pin survivors. Guided-scan buttons for HP, Stamina, Mana, Gold, Game Clock, and Room Number. |
| **Day / Time** | Once the game-clock address is pinned, set the in-game hour (0–23) and day, or jump to a named time-of-day preset (Dawn, Midday, Night, etc.). |
| **Teleport** | Once the room-number address is pinned, pick a destination from a named room list and write its number. Walk through a door to trigger the room load (or use ALT-T in-game). |
| **Freezes** | Standard pin table: label, address, live value, editable target, freeze checkbox (re-writes every ~200 ms). |
| **Stats** | Reference table of all character stats and skill IDs. |

## Prerequisites

- **Windows 10/11** — the trainer uses WPF and the Win32 `ReadProcessMemory` / `WriteProcessMemory` APIs.
- **.NET 8 SDK** — `dotnet` must be on your PATH.
- **DOSBox** or **DOSBox-X** running Quest for Glory I.
- **Administrator rights** — the app manifest requests elevation; a UAC prompt appears on launch.

## Quick Start

```powershell
cd QuestForGlory1Trainer
.\Run.ps1
```

This restores NuGet packages, builds Release, and launches the trainer (UAC prompt).

Then:

1. Load the game in DOSBox and reach a point where you know a stat value (e.g. open the stats screen).
2. In the trainer, click **Refresh**, select the DOSBox process, and click **Attach**.
3. Use a **Guided Scan** button (HP, Stamina, Mana, Gold, etc.) and follow the status-bar instructions.
4. Narrow the results until one row remains; click **Pin selected** (or **Pin As Clock** / **Pin As Room**).
5. Use the **Day/Time** and **Teleport** tabs once those special pins are set.

## Run Script Options

```powershell
.\Run.ps1                         # build Release and launch
.\Run.ps1 -Configuration Debug    # debug build
.\Run.ps1 -Clean                  # wipe bin/obj first, then build and launch
.\Run.ps1 -NoBuild                # skip build, launch existing exe
.\Run.ps1 -NoRun                  # build only
.\Run.ps1 -Publish                # single self-contained win-x64 exe
```

## How It Works

Quest for Glory I runs on Sierra's SCI0 interpreter. The interpreter allocates its heap
(containing all global variables, Ego object properties, and room state) dynamically inside
DOSBox's guest RAM each session — there is no stable static byte signature near the mutable
game state to anchor an offset scanner. The trainer therefore uses a **value-scan** approach:

1. Snapshot all memory matching the known value.
2. Perform an in-game action that changes the value (take a hit, buy an item, wait for the clock to tick).
3. Narrow the candidate list by scanning for the new value or by a relative comparison.
4. When only one address remains, pin it.

The Day/Time editor writes directly to the game-clock tick counter (`global[4]`, 0–3599 per day) and
the day counter (`global[3]`). The Teleport editor writes the destination room number to `global[1]`;
the game loads the new room on the next room transition event.

See `.docs/ReverseEngineering.md` for detailed SCI0 engine notes and confirmed offsets.
See `.docs/StrategyGuide.md` for gameplay help, controls, and maps.

## Notes

- Game assets (`.game/`), memory dumps (`.data/`), and research notes (`.docs/`) are git-ignored.
- The trainer does not touch the network or any external service.
- Supply your own legally obtained copy of Quest for Glory I.
