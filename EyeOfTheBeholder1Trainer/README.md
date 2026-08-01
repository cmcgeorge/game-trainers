# Eye of the Beholder I Trainer

A live-memory trainer for **Eye of the Beholder** (Westwood Studios / SSI, 1991), a first-person AD&D dungeon crawl running under DOSBox / DOSBox-X.

## Features

- **Automatic party location** — structural scan finds the six-character roster in the emulator's memory without manual searching. No Cheat Engine-style value scanning required.
- **Live editing** — edit attributes, HP, AC, food, race, class, alignment, levels, and XP in real time; changes take effect immediately in the running game.
- **Freeze toggles** — keep HP and food pinned at max so the party never dies or starves.
- **Quick actions** — Full Heal, Max Attributes, Max HP, Max Everything for individual characters or the whole party.
- **Offline save editor** — load `EOBDATA.SAV`, edit characters, and save back with a one-shot `.bak` backup. No emulator needed.
- **Reference tables** — browse all 46 spells (23 cleric + 23 mage), 15 classes, 12 races, and 9 alignments.

## Requirements

- Windows 10/11 x64
- .NET 8.0 SDK
- DOSBox or DOSBox-X with Eye of the Beholder installed
- Administrator rights (the trainer reads/writes the emulator's process memory)

## Quick Start

```powershell
.\Run.ps1
```

This builds Release and launches the trainer (a UAC prompt appears). Launch Eye of the Beholder in DOSBox, then pick the DOSBox process and click **Attach**.

## Usage

1. **Launch the game** in DOSBox / DOSBox-X and get past the title screen so characters are loaded.
2. **Attach** — select the DOSBox process from the dropdown and click Attach. The trainer automatically scans for the party.
3. **Edit** — select a character from the party list and edit any field. Changes write to live memory immediately.
4. **Freeze** — check Freeze HP to keep all characters at max HP, or Freeze Food to prevent starvation.
5. **Save Editor** — switch to the Save Editor tab to load and edit `EOBDATA.SAV` files offline.

## Project Structure

```
src/EyeOfTheBeholder1Trainer/
    Game/           Character format, spell book, game facts, save file
    Memory/         Party locator (structural scan)
    ViewModels/     MVVM view-models (main, character, save editor, reference)
    MainWindow.xaml  WPF UI
test/FormatCheck/   Headless verification harness
docs/               Reverse-engineering notes and strategy guide
```

## Verification

```powershell
.\Run.ps1 -Test -NoRun
```

Runs the `FormatCheck` harness, which validates the character record format, lookup tables, spell book, save-file round-trip, and structural scan fixtures.

## How It Works

The trainer uses a **structural scan** to locate the party in DOSBox's memory. The party is an array of six contiguous 243-byte records with no file header. Each record begins with a Character ID matching its slot index (0–5), followed by an active flag, a 10-character ASCII name, six ability score pairs (modified + base), hit points, armor class, race, class, alignment, levels, and experience. The scan walks every readable memory region looking for a window that matches this shape — active slots have plausible names, ability scores in the AD&D range (3–25), valid HP, and in-range race/class/alignment — which is specific enough to pin the live roster without a static anchor.

See `docs/ReverseEngineering.md` for the full reverse-engineering analysis.
