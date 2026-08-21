# DarkSpyre — Trainer

A Windows WPF live-memory trainer for **DarkSpyre** (Event Horizon Software, 1990) running
under **DOSBox** or **DOSBox-X**.

It finds your character on its own. Attach to the emulator and the trainer searches guest
RAM for the live character by content — no addresses to type, no Cheat-Engine-style value
hunting for the things that matter.

## Features

| Tab | What it does |
|---|---|
| **Character** | Found automatically on attach: live hit points, spell points and encumbrance, their maxima, and all six attributes. Edit the maxima and attributes, freeze HP or SP, refill both, max all attributes. |
| **Value Scanner** | Cheat-Engine-style scan for what the locator does not cover — score, level number, inventory. First Scan, then narrow by Exact / Increased / Decreased / Changed / Unchanged, pin survivors. |
| **Freezes** | Pin table: label, address, live value, editable target, freeze checkbox (re-writes every ~200 ms). |
| **Spells** | All 14 spells across the 6 magic classes, with SP costs and effects. |
| **Weapons** | The 7 weapon proficiency classes with speed, damage and notes. |
| **Monsters** | All 35 creatures the game ships in `CR.DAT`, with the attributes read out of their own records and a ranged/melee flag decoded from the same place. |
| **Items** | All 162 objects in the game's `OBJ.DAT` table, in table order. |
| **Runes** | All 25 runes, named as the game itself spells them, marking the 5 power runes. |

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

1. Load DarkSpyre in DOSBox and start a character — the menus have no character to find.
2. The trainer attaches to the emulator by itself; if it does not, click **Refresh**,
   pick the process and click **Attach**.
3. Open the **Character** tab. It is already populated.
4. Click **Refill HP & SP**, tick **Freeze** beside hit points, and play.

If the game was still at its title screen when you attached, click **Locate character**
once you are in the dungeon. Changing level moves the character's actor record; the
trainer notices and searches again on its own.

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

DarkSpyre spreads live character state across three structures in the emulator's guest
RAM, and which one you write to matters:

- a **status block** of six 16-bit values (current and maximum HP, SP and encumbrance) —
  this is what the on-screen bars print, and the game rebuilds it every frame, so the
  trainer only reads it;
- a **character record** — six attribute bytes then the three maxima. Raising a maximum
  here is what the engine actually adopts;
- the **player actor**, entry 0 of the creature table loaded from `CR.DAT`, holding
  current HP and SP. This is the copy the game plays out of, so that is where current
  values are written.

The locator finds all three by content, with no hard-coded address and no assumed
distance between them: it looks for the actor's `player` name field, then for a status
block whose current values match that actor, then for a character record carrying exactly
the maxima that status block reported. Each stage confirms the next, which is what makes
the result unambiguous — on a 16 MB guest-RAM dump every stage resolves to exactly one
address, in well under a second.

See `docs/ReverseEngineering.md` for the layout, the evidence behind it, and the decoded
file formats. See `docs/StrategyGuide.md` for controls, tactics and how to win.

## Verification

```powershell
.\Run.ps1 -Test
```

`test/FormatCheck` runs 179 headless checks with no live game and no copyrighted files:
the memory layout and its validation rules, the locator against a synthetic guest RAM
seeded with decoys, the character view-model's write routing and freeze behaviour, every
reference table, and the scan helpers. Pass a raw guest-RAM dump to re-run the locator
over real memory:

```powershell
dotnet run --project test\FormatCheck -c Release -- path\to\dump.bin
```

## Notes

- Game assets, memory dumps and research notes are git-ignored; the decoder that
  regenerates the Monsters and Items tables from the game's own files lives in
  `.docs/decode_game_data.py`.
- The trainer does not touch the network or any external service.
- Supply your own legally obtained copy of DarkSpyre.
