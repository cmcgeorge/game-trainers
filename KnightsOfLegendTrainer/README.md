# Knights of Legend — Trainer

A Windows WPF live-memory trainer for **Knights of Legend** (Origin Systems, 1989,
by Todd Porter) running under **DOSBox** or **DOSBox-X**.

No game binary or memory dumps were available for static analysis, so the trainer
uses a Cheat-Engine-style value scanner with guided scan recipes instead of an
automatic locator. Pick a stat from the guided dropdown, follow the instructions
to narrow it to a single address, pin it, and edit or freeze it.

## Features

| Tab | What it does |
|---|---|
| **Value Scanner** | Guided scans for Gold Crowns, Adventure Points, Body Points, Max Body Points, the seven primary statistics (STR/QUI/SIZ/HEA/FOR/CHA/INT), Level, Fatigue, and Rations. First Scan, then narrow by Exact / Increased / Decreased / Changed / Unchanged, pin survivors. |
| **Freezes** | Pin table: label, address, live value, editable target, freeze checkbox (re-writes every ~200 ms). |
| **Save Editor** | Edit quest status in a chardata save file. 24 quests at offsets 482-487, each with a 2-bit code (Not Given / Given / Complete / Medal Given). One-shot .bak backup. |
| **Races** | The 4 playable races (Human, Elven, Dwarven, Kelden) with descriptions and notes. |
| **Classes** | All 33 character classes by race and gender, with starting levels. |
| **Weapons** | All 36 weapon types across 9 training masters, with locations and max proficiency. |
| **Armor** | 12 armor types covering head, torso, legs, and shields. |
| **Magic** | The 6 magic orders and their 20 spells, with descriptions. |
| **Monsters** | 20 monsters organized by category and quest, with tactics. |
| **Quests** | All 24 quests with givers, keywords, target locations, monsters, and rewards. |

## Prerequisites

- **Windows 10/11** — the trainer uses WPF and the Win32 `ReadProcessMemory` / `WriteProcessMemory` APIs.
- **.NET 8 SDK** — `dotnet` must be on your PATH.
- **DOSBox** or **DOSBox-X** running Knights of Legend.
- **Administrator rights** — the app manifest requests elevation; a UAC prompt appears on launch.

## Quick Start

```powershell
cd KnightsOfLegendTrainer
.\Run.ps1
```

This restores NuGet packages, builds Release, and launches the trainer (UAC prompt).

Then:

1. Load Knights of Legend in DOSBox and start or restore a character.
2. The trainer attaches to the emulator by itself; if it does not, click **Refresh**,
   pick the process and click **Attach**.
3. Open the **Value Scanner** tab. Pick a guided scan (e.g. "Gold Crowns") and
   follow the instructions to narrow the address.
4. Pin the result to the **Freezes** tab, edit the Target value, and optionally
   tick Freeze to hold it.

## Mouse Control Issues

Knights of Legend uses a two-click mouse interface (click to highlight, click again
to select) that can be difficult to use in DOSBox. The game also has no frame
limiter, so CPU speed affects gameplay. Recommended fixes:

### Use Keyboard Controls

The game is fully playable with keyboard alone:

- **Arrow keys** — move the cursor / navigate
- **< and >** — cycle through icons
- **ENTER** — select the highlighted icon
- **ESC** — go back / activate the U-Turn icon
- **Number keys** — select menu items on the table of contents screen
- **Ctrl-Q** — quit to DOS

### DOSBox Settings

Add or adjust these in your `dosbox.conf`:

```ini
[cpu]
cycles=fixed 3000

[mouse]
sensitivity=1.0
```

- `cycles=fixed 3000` gives consistent game speed (the game has no frame limiter).
- `sensitivity=1.0` (or lower, e.g. `0.5`) tames the mouse for the icon-based
  interface. Lower values make the cursor less jittery.

If using DOSBox-X, also consider:

```ini
[render]
aspect=true
```

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

No game binary was available for static analysis, so the trainer has no automatic
locator. Instead, it provides **guided scan recipes** that tell you what on-screen
value to scan for, what data width to use (Byte, Int16, or Int32), and step-by-step
instructions for narrowing the candidate set to a single address:

1. **Pick a guided scan** from the dropdown (e.g. "Gold Crowns").
2. **Read the value** from the game's character sheet.
3. **First Scan** with that value.
4. **Change the value** in-game (buy something, take damage, etc.).
5. **Next Scan** with the new value (Exact), or use Increased/Decreased/Changed.
6. **Repeat** until one address remains.
7. **Pin** it to the Freezes tab, where you can edit or freeze it.

The **Save Editor** tab edits quest status in the `chardata` save file. The file
stores 24 quests at offsets 482-487 using 2-bit codes packed four quests per byte:
00 = Not Given, 01 = Given, 10 = Complete, 11 = Medal Given. The editor preserves
all other bytes verbatim and takes a `.bak` backup before the first write.

See `docs/ReverseEngineering.md` for what is known and what is not. See
`docs/KnightsOfLegend-Strategy-Guide.md` for controls, combat, the 24-quest
walkthrough, and tactics.

## Verification

```powershell
.\Run.ps1 -Test
```

`test/FormatCheck` runs headless checks with no live game and no copyrighted files:
the game constants, the save format quest status encoding (round-trip, partial
rewrite preservation, clamping), all reference tables (races, classes, weapons,
armor, spells, magic orders, monsters, quests), the scan guide recipes, and the
scan/freeze view-model helpers.

## Notes

- All game knowledge comes from online resources (the manual, walkthroughs, reviews).
  Every constant carries a **[Manual]** or **[Inferred]** confidence marker.
- The trainer does not touch the network or any external service.
- Supply your own legally obtained copy of Knights of Legend.
