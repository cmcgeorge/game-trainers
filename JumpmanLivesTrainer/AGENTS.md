# JumpmanLivesTrainer

A live-memory trainer for **Jumpman Lives!** (Apogee Software, 1991, Dave Sharpless) — a DOS platformer written in **Borland Turbo Pascal 6.0**. The shipped executable is `JMAN2.EXE` (136,431 bytes, EXEPACK-compressed).

## Architecture

This trainer follows the repo's **string-anchored `GameLocator`** pattern (same family as `RailroadTycoonTrainer` and `AirborneRangerTrainer`): it attaches to the DOSBox emulator, sweeps the emulator's memory for a static byte pattern whose DGROUP offset is known, derives `DGROUP:0000`, and reads/writes game state at fixed offsets — **no value scanning required**.

The game's complete Turbo Pascal source code and Borland linker map (`JMLIVES!.MAP`) were recovered from archive.org, giving every global's DGROUP offset directly. The `player` record layout comes from `TYPES.INC`, and the record size (92 bytes) is confirmed by MAP arithmetic (`p` at `0xCFE6`, `dots` at `0xD436`, difference `0x450 = 1104 = 12 × 92`).

## Key Design Facts

- **Anchor**: `jp1` (22-byte vertical jump trajectory table) at DGROUP `0x7D46` — long enough to be unique in 16 MB of guest RAM
- **Validators**: `PLAYSPEED` at `0x7D26` (8 bytes) and `ftwo` at `0x7D90` (6 bytes); both must match
- **Player array**: `p[1..12]` at `0xCFE6`, 92 bytes per record; active players are 1–4
- **Current player**: `pl` (BYTE) at `0xD981`
- **Globals**: `trainer` at `0x7D2E`, `current_level` at `0x7D3A`, `bonus` (LONGINT) at `0x7D3C`, `maxpl` at `0x7D40`
- **No save editor**: the save file (`jmlives!.sav`) is a raw dump of player records — editable through the live trainer instead
- **No teleport**: player X/Y is editable but the level layouts are binary sprite arrays that were not decoded into maps

## Game Knowledge

All reverse-engineered constants live in `Game/GameLayout.cs` (offsets, sizes, LE accessors, validation) and `Game/GameFacts.cs` (controls, 45 level names, tips). The locator is in `Memory/GameLocator.cs`, and the `IMemorySource` abstraction in `Memory/IMemorySource.cs` lets the `FormatCheck` harness drive the locator against a synthetic memory image.

## Testing

`test/FormatCheck` runs 94 checks: layout-constant, LE-accessor, validation-helper, game-facts, locator (synthetic memory + edge cases), and PlayerViewModel (edit/clamp/freeze/write-back through a fake `IGameHost`). Run with `.\Run.ps1 -Test`.
