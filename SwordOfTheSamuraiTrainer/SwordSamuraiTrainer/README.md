# Sword of the Samurai — WPF Trainer

A Windows (WPF) trainer that edits the **live DOSBox process**, doing everything
[../SAVE_GAME_EDITING.md](../SAVE_GAME_EDITING.md) / `save_edit.py` do — but in the running game:

- **Freeze the protagonist's age at "Youth"** (a true freeze, not a one-shot save reset).
- **Max your stats** (record 0 → 0x80).
- **Cripple rivals** (every non-kin daimyo → 1), or Max/Min any single record from the roster.

> Save-editing sets values at load time and the game can undo them over turns (age especially).
> This trainer re-writes the age byte ~8×/second, so the aging routine can never move you off youth
> while the freeze is on; the stat edits are applied on demand, exactly like the save editor.

## How it works

**Important:** the running game does **not** display from the on-disk save format. On load it builds a
separate **live character array** (word-scaled, `0x60`-byte records) and reads *that* for the screen —
so editing the save-image buffers does nothing visible (an earlier version did exactly that, which is
why "nothing changed"). The trainer targets the live array:

- Each record ends with the tail `01 00 01 00 12 00` at `rec+0x5A`. The trainer scans for that tail
  and walks back to record 0 (you) — no name table needed, so it survives ASLR / relaunch.
- Verified live field map (record = `0x60` bytes, values are little-endian words):
  - **age** — byte `rec+0x33` (`0`=Youth, `1`=Young adult, …) — freeze target
  - **family index** — byte `rec+0x32` (records sharing yours are kin)
  - **stat cluster** — words `rec+0x3C … rec+0x58` (0–128; max→128, cripple→1). The word at `rec+0x3A`
    (a ~31 marker) is left alone — it's part of the record signature.
  - **army / loyal warriors** — word `rec+0x40` (pinned by a Cheat-Engine-style value search, then
    confirmed by writing it and watching the on-screen count change)
- There are two live copies plus save-image buffers; the trainer edits every character array it finds,
  so the one the game reads is always covered.

This live map was reverse-engineered against the running game (value-tracking the army count 4→8→11→13,
then diffing). See `tools/Verify` (`--track`, `--arrays`, `--engine`).

### The save-image block (legacy)

The on-disk save (`talltale.dat`) uses a different, byte-scaled layout. Inside it:

- The driver-name run **`MGRAPHIC.EXE\0RSOUND.SAM`** sits at header offset `0x23`. It's
  game-specific, name-independent, and present in every copy of the state block — an ideal anchor,
  so the **block base is `match − 0x23`**.
- **Character records** are a `0x60`-byte array from base `+0x6F`; record 0 is you. Per record:
  family-name index at `+0x32` (records sharing yours are **kin**), age byte at `+0x33`
  (`0`=Youth, `1`=Mature adult, `2`=Old), and the editable **stat span `+0x3A … +0x58`** (values
  `2…128`, set to `0x80` to max or `1` to cripple — names/markers/padding are left untouched).
- The record count is auto-detected each run (the name table is the first ≥4-letter ASCII run past
  `+0x100`; records are every whole `0x60` block before it), so it adapts if the roster changes.

The trainer scans DOSBox's writable memory for the signature, parses the roster in **every** copy
(normally two — the working state and the save buffer — both are edited), freezes the age on a
timer, and applies stat edits on demand. Everything is found by scanning each run, so ASLR / a fresh
launch / a different player name don't matter; if a block moves it re-scans automatically.

*Verified against the running game (`DOSBox` PID 2804, the `DOSBox-2804-…` dump):* 2 signature
matches at base `0xEBEDDCD` / `0xEC60310`; the parsed roster reproduces SAVE_GAME_EDITING.md §4
exactly — rec0 YOU (fam 151, stats `128×6`), rec4 kin (fam 151, untouched), rec1/2/3/5 rivals — and
live write tests flipped the age (`Youth`↔`Old`) and a rival stat byte and restored both. See
`tools/Verify`.

## Build

Requires the .NET 8 SDK (Windows).

```powershell
dotnet build -c Release SwordSamuraiTrainer.sln
```

## Run

1. Launch DOSBox and start Sword of the Samurai; **Restore/Continue** so a game is loaded (the
   state block only exists once a game is in play).
2. Run the trainer:
   ```powershell
   .\src\SotSAgeTrainer\bin\Release\net8.0-windows\win-x64\SotSAgeTrainer.exe
   ```
3. The status dot turns green and the roster fills in (YOU / kin / rival, with ages and stats).
4. **Age:** leave **Target = Youth** and click **❄ Freeze age** (stays green while held). *Apply
   once* writes the stage a single time without freezing.
5. **Stats:** click **★ Max my stats** to max record 0, **⚔ Cripple all rivals** to drop every
   non-kin daimyo to 1, or use the per-row **Max/Min** buttons (e.g. to buff an heir shown as
   *kin*, or to spare a near-default record). Rival names aren't stored as text, so confirm which
   rows are your real rivals in-game — the roster shows family index, age and stats to help.

If the dot is amber ("connected — load your game"), you're attached to DOSBox but no save is loaded
yet. If it can't open DOSBox, run the trainer as the same Windows user (or as admin).

## Verifier (optional, non-destructive)

Proves the scan/read/write path against your live game using the exact `Core` code the app runs on:

```powershell
dotnet run -c Release --project tools\Verify                 # scan + read only
dotnet run -c Release --project tools\Verify -- --write      # + write-back round-trip (safe)
dotnet run -c Release --project tools\Verify -- --mutate     # + flip age Youth<->Old and restore
dotnet run -c Release --project tools\Verify -- --roster     # parse & print the character roster
dotnet run -c Release --project tools\Verify -- --stat-test  # + flip a rival stat byte and restore
```

## Layout

```
src/SotSAgeTrainer/        WPF app
  Core/                    process memory + scanner + edit engine (no WPF deps)
    NativeMethods.cs       Win32 P/Invoke (OpenProcess / RPM / WPM / VirtualQueryEx)
    ProcessMemory.cs       region enumeration, read/write, pattern scan
    AgeTrainer.cs          find DOSBox, parse roster, freeze age, max/min stats
    TrainerModels.cs       LifeStage, CharacterRecord/role, GameBlock, status snapshot
  ViewModels/MainViewModel.cs
  MainWindow.xaml          UI (connection · age freeze · roster + stat edits · log)
tools/Verify/              console proof harness (links Core)
```

## Notes & limits

- The trainer is built **x64**; DOSBox 0.74 is 32-bit (WOW64). A 64-bit process reading/writing a
  32-bit one is fine (the reverse would not be).
- The age freeze is poll-based (~120 ms) — ample for a turn-based game; for an absolute guarantee at
  the exact instant the game increments age you'd patch the aging routine in `RP.EXE` itself.
- Stat edits are **one-shot** (like the save editor); if gameplay later changes a value, click again.
- Edits stay inside the verified fields: age `R+0x33` and the stat span `R+0x3A…R+0x58` (only bytes
  valued 2…128). Names, family/clan indices, faction markers and padding are never touched, and
  money/army follow because the game derives them. Nothing is written to disk; close the trainer to stop.
```
