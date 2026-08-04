# Wizardry 1: Proving Grounds of the Mad Overlord -- Trainer

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for the 1981 dungeon crawler *Wizardry: Proving Grounds of the Mad Overlord* (Sir-Tech, by Andrew C. Greenberg and Robert Woodhead), running under DOSBox / DOSBox-X via WIZDOS.COM (a UCSD p-system emulator). Windows-only (WPF + Win32 memory APIs); the app manifest requests administrator rights so it can `Read/WriteProcessMemory` on the emulator.

## Quick Start

1. Launch Wizardry 1 in DOSBox (run `WIZ1.BAT`, which invokes `wizdos wiz1.dsk`).
2. Get to the Edge of Town or load a saved party -- the trainer needs characters in memory to locate.
3. Run `.\Run.ps1` to build and launch the trainer.
4. Pick the DOSBox process, click **Attach**, and the trainer auto-locates the party roster.
5. Edit attributes, HP, gold, experience, spells, and more -- changes are written live to the game.

## Features

- **Auto-locate**: structural scan finds the party roster without manual value searching (no Cheat-Engine-style workflow needed).
- **Character editing**: name, race, class, alignment, all six attributes (STR/INT/PIE/VIT/AGI/LUK), HP, level, gold, experience, status, armor class.
- **Spell management**: learn individual or all 50 spells; set mage/priest spell charges per level.
- **Freeze toggles**: freeze HP and status (prevents death) per character or party-wide.
- **Quick actions**: Max Attributes, Max HP, Max Gold, Max XP, Learn All Spells, Full Heal, Max Everything.
- **References tab**: complete spell book (21 mage + 29 priest spells with descriptions).
- **Poll loop**: live HP/status display refreshed every 600 ms; freeze values re-pinned each tick.

## How It Works

Wizardry 1 is a UCSD Pascal p-system game, not native x86 code. The character roster (up to six 207-byte records) is allocated on the p-system heap at a session-specific address that changes every DOSBox session. The trainer locates it by a **structural scan** -- it walks every readable memory region looking for a window of contiguous 207-byte records matching the shape of a Wizardry 1 party (Pascal string names, valid race/class/alignment, attributes in 3-18, plausible HP and level). This is the same approach used by the Wasteland and Amberstar trainers for games whose roster address changes every session.

The character record layout was recovered from the reverse-engineered Pascal source (Thomas William Ewers, 2014, [github.com/snafaru/Wizardry.Code](https://github.com/snafaru/Wizardry.Code)). See `docs/ReverseEngineering.md` for the full layout and `docs/StrategyGuide.md` for gameplay help.

## Project Structure

```
Wizardry1Trainer/
  Wizardry1Trainer.sln
  Run.ps1
  README.md
  AGENTS.md
  docs/
    ReverseEngineering.md      -- RE notes (character record, attribute packing, TWIZLONG, spells)
    StrategyGuide.md           -- Strategy guide (controls, classes, combat, maps, how to win)
  src/Wizardry1Trainer/
    Wizardry1Trainer.csproj
    app.manifest
    App.xaml / App.xaml.cs
    MainWindow.xaml / .cs
    Game/
      CharacterFormat.cs        -- Byte offsets, attribute packing, TWIZLONG, constants
      CharacterRecord.cs        -- Typed mutable view over a 207-byte record
      SpellBook.cs              -- 50 spells (21 mage + 29 priest) with descriptions
      GameFacts.cs              -- Static game facts (title, emulator hints, constants)
    Memory/
      RosterLocator.cs          -- Structural scan to find the party in DOSBox memory
    ViewModels/
      MainViewModel.cs          -- Attach/scan/detach, poll loop, party-wide actions
      CharacterViewModel.cs     -- Per-character editable fields, freeze, quick actions
      ICharacterHost.cs          -- Write channel interface
      NamedValueViewModel.cs    -- Attribute row
      ReferenceViewModel.cs     -- References tab (spell book)
  test/FormatCheck/
    FormatCheck.csproj
    Program.cs                  -- Headless verification harness
```

## Build, Test, and Development

- `.\Run.ps1` -- build Release and launch (triggers a UAC prompt).
- `.\Run.ps1 -Test -NoRun` -- build and run the verification harness without launching the GUI.
- `dotnet build src\Wizardry1Trainer\Wizardry1Trainer.csproj -c Release` -- direct build.
- `dotnet run --project test\FormatCheck` -- run the harness directly.

## Reverse-Engineering Notes

The game runs under WIZDOS.COM, a UCSD p-system emulator for DOS. The game code is p-code (not x86), stored in SYSTEM.PASCAL as 16 segments. The character record (TCHAR) is 207 bytes with:

- **Attributes**: packed as six 5-bit values into 4 bytes ($2C-$2F) with a non-standard bit layout where some attributes wrap across byte boundaries. `$52 4A 52 4A` = all 18s.
- **Gold/Experience**: TWIZLONG -- a base-10000 number stored as three little-endian uint16 words (value = LOW + MID * 10000 + HIGH * 100000000), not packed BCD.
- **Spells**: 50-bit knowledge field (one bit per spell) plus 7 uint16 charge counters per class (mage/priest).

Full details in `docs/ReverseEngineering.md`.
