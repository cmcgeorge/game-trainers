# Fountain of Dreams Trainer

A live-memory trainer for **Fountain of Dreams** (Electronic Arts, 1990), the post-apocalyptic
RPG set in the irradiated ruins of Florida.

## Quick Start

1. Launch Fountain of Dreams in **DOSBox** or **DOSBox-X**.
2. Run `.\Run.ps1` (builds and launches the trainer; a UAC prompt appears because the trainer
   needs admin rights to read/write the game's memory).
3. Pick the DOSBox process from the drop-down and click **Attach**.
4. The trainer scans the emulator's memory for the party roster and displays each character.
5. Edit attributes, cash, constitution, level, experience, and more; changes take effect live.

## What It Does

- **Auto-locates the party** by structural scan — no manual value searching (unlike Cheat Engine).
  The locator sweeps the emulator's address space for the 332-byte character records by their
  shape (name, attributes in range, plausible CON/level/profession).
- **Live editing**: attributes (ST/IQ/DX/WP/AP/CH/LK), cash, CON/MaxCON, level, rank, experience,
  next-level XP, and armor class — all written back to the game immediately.
- **Freeze Health**: pins CON to max each tick so the party never dies.
- **Quick actions**: Full Heal, Max Attributes, Max Money, Max Everything — per character or
  party-wide.
- **Inventory**: view all 27 inventory slots with item IDs and data.
- **References tab**: browse attributes, skills, professions, and items.

## Character Record Layout

The 332-byte record was reverse-engineered from the shipped `DISK1` save file and cross-checked
against the `ARCHTYPE` profession template file, the `FOD.EXE` character-creation strings, and
the game manual:

| Offset | Size | Field |
|--------|------|-------|
| 0x00   | 20   | Name (null-terminated ASCII + quote text) |
| 0x14   | 4    | Cash (uint32 LE) |
| 0x18   | 7    | Attributes: ST, IQ, DX, WP, AP, CH, LK (1 byte each, range 3-20) |
| 0x1F   | 1    | Profession |
| 0x23   | 1    | CON (current constitution) |
| 0x44   | 1    | Armor Class |
| 0x46   | 2    | MaxCON (uint16 LE) |
| 0x50   | 1    | Level |
| 0x52   | 2    | Rank (uint16 LE) |
| 0x54   | 4    | Experience (uint32 LE) |
| 0x5E   | 2    | Next-level XP (uint16 LE) |
| 0x80   | 162  | Inventory (27 slots x 6 bytes, 0xFF = empty) |

See `docs/FountainOfDreams-Reverse-Engineering.md` for the full analysis.

## Architecture

- **`src/FountainOfDreamsTrainer/Game/`** — game-knowledge layer: `CharacterFormat` (offset
  constants), `CharacterRecord` (typed mutable view), `AttributeBook`, `ProfessionBook`,
  `SkillBook`, `ItemBook`, `GameFacts`.
- **`src/FountainOfDreamsTrainer/Memory/`** — `IMemorySource` interface and `PartyLocator`
  (structural scan, like Wasteland's).
- **`src/FountainOfDreamsTrainer/ViewModels/`** — hand-rolled MVVM (`MainViewModel`,
  `CharacterViewModel`, `ReferenceViewModel`, row view models).
- **`test/FormatCheck/`** — headless verification harness (run with `.\Run.ps1 -Test -NoRun`).
- References `GameTrainers.Common` for shared `ProcessMemory`/`MemoryRegion` and
  `ObservableObject`/`RelayCommand`.

## Build & Test

```powershell
.\Run.ps1                           # Build Release + launch
.\Run.ps1 -Configuration Debug      # Build Debug + launch
.\Run.ps1 -Test -NoRun              # Run tests without launching
.\Run.ps1 -Clean                    # Clean + build + launch
.\Run.ps1 -Publish                  # Single self-contained exe
```

## Game Info

- **Title**: Fountain of Dreams
- **Publisher**: Electronic Arts
- **Year**: 1990
- **Platform**: DOS
- **Engine**: Microsoft C 1988, EXEPACK-compressed (`KEH.EXE` = main engine, `FOD.EXE` =
  character creation launcher)
- **Party size**: Up to 3 characters
- **Professions**: Survivalist, Vigilante, Medic, Hood, Mechanic (playable); Yuppie, Clown (NPC)
