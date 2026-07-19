# Game Trainers

A collection of independent, Windows-only **live-memory trainers** for classic DOS games running under **DOSBox / DOSBox-X**. Each trainer is a small C#/WPF app that attaches to the running emulator process, signature-scans the emulated guest RAM to locate the game's state at runtime (addresses are discovered live, never hard-coded), and reads/writes it while you play — freeze toggles, "max" buttons, teleporting, and, for some titles, offline save editing. Several trainers also include a reverse-engineering workspace documenting how their offsets were recovered.

Each game lives in its own self-contained folder with its own solution/project, `README.md`, run script, and (for most) an `AGENTS.md` contributor guide.

## Trainers

| Folder | Game | Target |
| --- | --- | --- |
| `AutoduelTrainer/` | Autoduel (Origin Systems, 1985) | net9.0-windows |
| `BardsTale1Trainer/` | The Bard's Tale: Tales of the Unknown, Vol. I (1987) | net8.0-windows |
| `BattleTech1Trainer/` | BattleTech: The Crescent Hawk's Inception (Westwood/Infocom, 1988) | net8.0-windows |
| `DarklandsTrainer/` | Darklands (MicroProse, 1992) | net8.0-windows |
| `DragonWarsTrainer/` | Dragon Wars (Interplay, 1989) | net8.0-windows |
| `KeefTrainer/` | Keef the Thief (EA / Naughty Dog, 1989) | net9.0-windows |
| `LordsOfTheRealmTrainer/` | Lords of the Realm | net9.0-windows |
| `MightAndMagic1Trainer/` | Might & Magic Book One (1986) | net8.0-windows |
| `MinesOfTitanTrainer/` | Mines of Titan (Westwood Associates, 1989) | net8.0-windows |
| `MoriaTrainer/` | The Dungeons of Moria / UMoria 5.5.2 (Koeneke/Wilson, 1988) | net8.0-windows |
| `PoolOfRadianceTrainer/` | Pool of Radiance (1988) | net8.0-windows |
| `QuestForGlory1Trainer/` | Quest for Glory I: So You Want to Be a Hero (Sierra On-Line, 1989) | net8.0-windows |
| `ShogunTrainer/` | James Clavell's Shōgun (1987) | net8.0-windows |
| `SwordOfTheSamuraiTrainer/` | Sword of the Samurai | net8.0-windows |
| `SyndicatePlusTrainer/` | Syndicate | net8.0-windows |
| `ThePerfectGeneral2Trainer/` | The Perfect General II (QQP, 1994) | net8.0-windows |
| `WarOfTheLanceTrainer/` | War of the Lance (SSI, 1989) | net8.0-windows |
| `WastelandTrainer/` | Wasteland (Interplay / Electronic Arts, 1988) | net8.0-windows |

`MightAndMagic1Trainer` is the architectural template most of the others were ported from.

### Shared library

`GameTrainers.Common/` is a small shared library holding the game-agnostic plumbing that used to be copied between trainers: the process/guest-memory access layer (`GameTrainers.Common.Memory`) and the hand-rolled MVVM base types (`GameTrainers.Common.Mvvm`). The MM1-family trainers — `MightAndMagic1Trainer`, `BardsTale1Trainer`, and `PoolOfRadianceTrainer` — reference it instead of duplicating that code, as do `DragonWarsTrainer`, `MinesOfTitanTrainer`, `WarOfTheLanceTrainer`, `ThePerfectGeneral2Trainer`, `BattleTech1Trainer`, `QuestForGlory1Trainer`, `DarklandsTrainer`, `WastelandTrainer`, and `MoriaTrainer`; each keeps only its own game-specific locators and scanners. `WastelandTrainer` locates the party by **structure** (an array of seven contiguous 256-byte records that pack from slot 0) rather than by a static anchor, and teleports by writing the party's X/Y into the party-state header that precedes the roster. `ThePerfectGeneral2Trainer`, `BattleTech1Trainer`, `QuestForGlory1Trainer`, `DarklandsTrainer`, and `MoriaTrainer` drive Common's `MemorySearcher` as a Cheat-Engine-style value scanner rather than a fixed locator, because their live game state has no stable static signature to anchor to (`BattleTech1Trainer` additionally uses Common's `BytePatternScanner` to *detect* the game via its read-only EXE strings; `QuestForGlory1Trainer` additionally exposes a Day/Time editor and a Teleport editor that write directly to SCI0 global variables once their addresses are scanned and pinned; `DarklandsTrainer` targets a PKLITE-packed, extender-relocated EXE that has no reliable detection signature either, so it has neither a locator nor a detector and keeps only its Confirmed attribute/skill/currency/Fame reference tables and a read-only DEFAULT-save reader; `MoriaTrainer` targets UMoria 5.5.2, a DJGPP-compiled 32-bit DPMI roguelike whose heap address changes every session, so it has neither a locator nor a detector — it uses `MemorySearcher` with 14 guided scans for character stats plus a relative-scan teleport that locates `char_row`/`char_col` by walking cardinal directions in-game, and keeps its Confirmed game-knowledge layer local — the stat encoding, the cave cell constants, a curated monster roster including the Balrog, the 31+31 spells, the item categories, and the 51-level descent reference). The remaining trainers are still self-contained.

## Prerequisites

- **Windows** (the trainers use WPF and the Win32 process-memory APIs).
- **.NET 8 SDK** (some trainers target .NET 9 — install the .NET 9 SDK to build everything).
- **DOSBox** or **DOSBox-X** running the target game.
- Administrator rights: most trainers ship a manifest that requests elevation for `ReadProcessMemory` / `WriteProcessMemory`, so launching triggers a UAC prompt.

## Building and Running

Every trainer has its own `.\Run.ps1`, and they all expose the **same** options. Run one from
inside its folder, or use the **root launcher** to pick one interactively.

### Root launcher

From the repository root, `.\Run.ps1` discovers every trainer (any top-level folder that has its
own `Run.ps1`) and forwards the shared options to the one you choose:

```powershell
.\Run.ps1                         # menu: pick a trainer, then build and launch
.\Run.ps1 -List                   # list the trainers and exit
.\Run.ps1 -Trainer Shogun         # run a trainer by name (exact or unique partial)
.\Run.ps1 -Trainer 4 -Clean       # run the 4th listed trainer, cleaning first
```

### Per-trainer

```powershell
cd PoolOfRadianceTrainer
.\Run.ps1                         # restore, build Release, and launch (UAC prompt)
.\Run.ps1 -Configuration Debug    # debug build
.\Run.ps1 -Clean                  # wipe bin/obj first, then build and launch
.\Run.ps1 -NoBuild                # skip the build, launch the existing exe
.\Run.ps1 -NoRun                  # build only; print the exe path
.\Run.ps1 -Test -NoRun            # run the verification harness, no GUI
.\Run.ps1 -Publish                # publish a self-contained win-x64 exe, no launch
```

Shared options (identical for the root launcher and every trainer):

- **`-Configuration Debug|Release`** — build configuration (default `Release`).
- **`-Clean`** — remove `bin`/`obj` before building.
- **`-NoBuild`** — skip building and launch the most recent build.
- **`-NoRun`** — build only; do not launch.
- **`-Test`** — run the trainer's verification harness (warns if it has none).
- **`-Publish`** — publish a single self-contained win-x64 exe; skips launch.

Only `BardsTale1Trainer`, `BattleTech1Trainer`, `DarklandsTrainer`, `DragonWarsTrainer`,
`MightAndMagic1Trainer`, `MinesOfTitanTrainer`, `MoriaTrainer`, `PoolOfRadianceTrainer`,
`SwordOfTheSamuraiTrainer`, `ThePerfectGeneral2Trainer`, `WarOfTheLanceTrainer`, and `WastelandTrainer`
ship a verification harness; `-Test` warns and is ignored on the others (including `QuestForGlory1Trainer`). `SwordOfTheSamuraiTrainer` also has `.\Edit-SotsSave.ps1` for offline save editing.

You can always build directly with the SDK:

```powershell
dotnet build <project>.csproj -c Release
```

Then start the target game in DOSBox / DOSBox-X, and use **Attach** in the trainer.

## Testing

There is no unit-test suite. Trainers that ship verification use a headless console harness (usually `FormatCheck`, or `Verify` in `SwordOfTheSamuraiTrainer`) that checks the parsers against captured memory dumps / save files and exits `0` (pass) or `1` (fail). Run it via `.\Run.ps1 -Test -NoRun` where available, or `dotnet run --project <test-project>`. The GUI itself cannot be tested headlessly — it needs an interactive desktop and a running game.

## Notes

These are single-player cheat tools for the user's own saved games. They do not touch the network or any external service. Game assets are copyrighted and are **not** included — supply your own legally obtained copy. Original game files, memory dumps, and reverse-engineering notes live in dot-prefixed folders (`.game/`, `.data/`, `.docs/`) that are git-ignored.
