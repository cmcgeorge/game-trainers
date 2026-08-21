# DarksypreTrainer — Agent Guide

## Game

DarkSpyre (Event Horizon Software, 1990). Real-time single-character dungeon crawler,
50 levels, runs as a DOS program under DOSBox / DOSBox-X.

## Project Layout

```
DarksypreTrainer/
├── .docs/                       — git-ignored: proposal, decode_game_data.py
├── docs/
│   ├── ReverseEngineering.md    — memory layout, decoded file formats, method, evidence
│   └── StrategyGuide.md         — controls, tactics, monsters, runes, how to win
├── src/DarksypreTrainer/
│   ├── Game/
│   │   ├── GameFacts.cs         — constants, each marked [File] or [Manual]
│   │   ├── CharacterFormat.cs   — the three live structures and their validation rules
│   │   ├── ScanGuide.cs         — scan recipes for what the locator does not cover
│   │   ├── SpellBook.cs         — 14 spells across 6 magic classes
│   │   ├── WeaponBook.cs        — 7 weapon proficiency classes
│   │   ├── MonsterBook.cs       — generated from CR.DAT: 35 creatures with attributes
│   │   ├── ItemBook.cs          — generated from OBJ.DAT: 162 objects
│   │   └── RuneBook.cs          — 25 runes, named as the game spells them
│   ├── Memory/
│   │   ├── IMemorySource.cs     — read-only view the locator needs (fixture-friendly)
│   │   └── CharacterLocator.cs  — three-stage content search for the live character
│   ├── ViewModels/
│   │   ├── ICharacterHost.cs    — write channel for the character panel
│   │   ├── CharacterViewModel.cs— live fields, freezes, write routing
│   │   ├── NamedValueViewModel.cs
│   │   ├── IScanHost.cs         — read/write channel for scan-result and freeze rows
│   │   ├── ScanValue.cs         — decimal/hex parse helpers and width-fit guards
│   │   ├── ScanResultViewModel.cs
│   │   ├── FrozenValueViewModel.cs
│   │   └── MainViewModel.cs     — attach, locate, value scan, freeze table
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .cs    — 8-tab WPF UI
│   ├── app.manifest             — requireAdministrator
│   └── DarksypreTrainer.csproj
├── test/FormatCheck/            — 179 headless checks, no live game required
├── DarksypreTrainer.sln
├── AGENTS.md                    — this file
├── README.md
└── Run.ps1
```

## Architecture

The trainer is **locator-first**. DarkSpyre keeps live character state in three
structures — a per-frame status block, a character record (attributes and maxima) and
the player's entry in the creature table (current HP and SP). `CharacterLocator` finds
all three by content in three mutually confirming stages, so nothing is hard-coded:
neither addresses nor the distance between the structures, only each structure's internal
layout. `docs/ReverseEngineering.md` has the offsets and the evidence for each.

Write routing matters and is easy to get wrong:

| Field | Written to | Why |
|---|---|---|
| Current HP, current SP | player actor | the copy the engine plays out of |
| Maximum HP, SP, encumbrance | character record | the status block is rebuilt from it each frame |
| Attributes | character record | same |
| Current encumbrance | nowhere — read-only | the game recomputes it from your inventory |

The Cheat-Engine-style value scanner (`MemorySearcher` from `GameTrainers.Common.Memory`)
is retained as a fallback for state the locator does not cover: score, level number,
inventory. Note the poll loop reads scan-result rows directly instead of calling
`MemorySearcher.RefreshValues`; that method re-snapshots every committed region *and*
rewrites the searcher's stored previous values, which would rebase Increased/Decreased
onto the last poll tick rather than the last scan.

References `GameTrainers.Common` (`Memory` and `Mvvm` namespaces via csproj `<Using>`
items). `ObservableObject` uses `SetField`; commands are `RelayCommand`.

## Reference Tables Are Generated

`Game/MonsterBook.cs` and `Game/ItemBook.cs` are generated from the game's own data files
— edit `.docs/decode_game_data.py`, not the C#:

```powershell
python .docs\decode_game_data.py "<path to DARKSYPR>" src\DarksypreTrainer\Game
```

Facts sourced from the shipped files beat facts from walkthroughs. Several
web-sourced claims that used to live in this trainer were simply wrong: monsters that do
not exist in `CR.DAT` (Vulture, Banshee, Electric Storm, Crystal Ninja), and rune
spellings (Laquz, Keno, Othilia) that match neither the game nor the manual.

## Coding Conventions

Follow the root `AGENTS.md`: 4-space indent, file-scoped namespaces, `sealed` classes,
PascalCase members, `_camelCase` private fields, XML `///` docs on public members, no
comments unless they explain something non-obvious.

## Build & Run

```powershell
.\Run.ps1                         # build Release and launch (UAC prompt)
.\Run.ps1 -Configuration Debug    # debug build
.\Run.ps1 -Clean                  # clean then build
.\Run.ps1 -NoRun                  # build only
.\Run.ps1 -Test                   # build and run FormatCheck verification harness
```

## Verification

`test/FormatCheck` runs 179 headless checks with no live game or copyrighted files:

- `CharacterFormat` offsets, endianness helpers, and every validation rule
- `CharacterLocator` over a synthetic guest RAM seeded with decoys — a second creature
  record, a status block belonging to a different character, structures straddling page
  boundaries, an actor with no matching status block, and empty RAM
- `CharacterViewModel` write routing (current values to the actor, maxima and attributes
  to the record), attribute clamping, freeze behaviour, and stale-record detection
- `GameFacts`, `ScanGuide`, `SpellBook`, `WeaponBook`, `MonsterBook`, `ItemBook`,
  `RuneBook` — including cross-checks such as every rune name appearing in the game's
  own object table
- `ScanValue` parse/fit/canonicalize helpers and `FrozenValueViewModel`

Passing a raw guest-RAM dump re-runs the locator over real memory:
`dotnet run --project test\FormatCheck -c Release -- dump.bin`.

## Working On This Trainer

The engine (`RUNTIME.1`) is a packed MZ image, so static disassembly of the shipped files
yields almost nothing — the unpacked code only exists in guest RAM. The productive loop
is: run the game under DOSBox against a scratch copy of the game directory, dump the
emulator's committed regions, and confirm each field by poking it and watching the game's
own screen. DOSBox with `output=surface` keeps its scaled 32-bit BGRA framebuffer in a
private region of exactly `width * height * 4` bytes, so the screen can be read straight
out of the process — useful when the desktop is not available.
