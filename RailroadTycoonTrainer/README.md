# Railroad Tycoon Trainer

A small Windows/WPF (.NET 8) live-memory trainer for **Sid Meier's Railroad Tycoon** (MicroProse,
1990) — the original DOS game running under **DOSBox / DOSBox-X**. It attaches to the emulator, finds
the game's data in the DOS guest's RAM, and lets you set and freeze your **cash** (and the game
**year**) while you play — headlined by a **one-click auto-locate** so you never have to hunt for values
by hand the way you would in Cheat Engine.

Unlike the repo's `ImperialismIITrainer` (a native Windows game), Railroad Tycoon is a real-mode DOS
program, so the trainer attaches to the **DOSBox process** and reads the guest memory mapped inside it.

## How it finds your data

Two paths, in order of convenience:

1. **Auto-locate cash (no scan).** Railroad Tycoon keeps the player's cash as a signed 16-bit word in
   units of $1,000 at a fixed offset in the game's data segment (DGROUP). The trainer finds DGROUP by
   two static text labels the game always holds in memory (`"Outstanding Loans: "` and
   `"Stockholders Equity: "`), double-checks with the year global, then reads the cash at its known
   offset — one click, no searching. It pins both **Cash** and **Year** to the Freezes tab. *(Verified
   against the running game: locate in ~0.2 s, correct cash and year, round-trip write OK.)*
2. **Value scan (the fallback, and for anything else).** A Cheat-Engine-style scanner: read a number you
   can see in-game, First Scan, make it change, narrow, and pin the survivor. This works regardless of
   which `GAME.EXE` build you have (the cash offset differs between builds), because it searches for the
   number itself. Cash is stored in **$1,000s** — read "$1,000,000" and scan for **1000** (Int16).

See [docs/RailroadTycoon-ReverseEngineering.md](docs/RailroadTycoon-ReverseEngineering.md) for how the
offsets were recovered, and [docs/RailroadTycoon-StrategyGuide.md](docs/RailroadTycoon-StrategyGuide.md)
for how to play and win.

## Using it

1. Start **Railroad Tycoon** in DOSBox / DOSBox-X and play past the title screen into a game (answer the
   startup locomotive quiz — the **Locomotives** tab is the answer key).
2. Run the trainer: `.\Run.ps1` (a UAC prompt appears — it needs admin to read the game's memory).
3. In the **Live** tab, pick the **dosbox** process and click **Attach**.
4. Click **⚡ Auto-locate cash** (or **💰 Set max cash ($30M)** to fill up and freeze in one click).
5. On the **Freezes** tab, edit a **Target** (cash is in $1,000s: 1000 = $1,000,000) and tick **Freeze**
   to hold it against the game's fiscal tick. Freeze **Year** to stop the retirement clock.

If auto-locate reports it couldn't validate the segment (a different `GAME.EXE` build), use the **Cash**
guide on the Scan tab — the value scan always works.

## Build / test

```powershell
.\Run.ps1                        # restore, build Release, launch (UAC prompt)
.\Run.ps1 -Configuration Debug   # debug build
.\Run.ps1 -Clean                 # wipe bin/obj first
.\Run.ps1 -NoBuild               # launch the most recent build
.\Run.ps1 -NoRun                 # build only; print the exe path
.\Run.ps1 -Test -NoRun           # run the verification harness, no GUI
.\Run.ps1 -Publish               # single self-contained win-x64 exe
```

## Layout

- `src/RailroadTycoonTrainer/Game/` — the game-knowledge layer: `RtLayout` (cash/year offsets, anchor
  strings, validators, conversions), `GameLocator` (the scan-anchor-validate-read locator),
  `LocomotiveBook` and `GameFacts` (reference tables).
- `src/RailroadTycoonTrainer/ViewModels/` — the live scanner (`LiveScannerViewModel`), the reference tab
  (`ReferenceViewModel`), and the shared value-scanner rows (`IScanHost`, `ScanValue`,
  `ScanResultViewModel`, `FrozenValueViewModel`).
- `src/RailroadTycoonTrainer/MainWindow.xaml` — two tabs: **Live** (auto-locate + value scanner) and
  **References** (locomotives, stations & scenarios, how-it-works notes).
- `test/FormatCheck/` — a headless harness that checks the reference tables and the pure layout /
  scanner helpers; exits 0 (pass) / 1 (fail).
- `docs/` — the reverse-engineering notes and the strategy guide.

The process/memory plumbing (`ProcessMemory`, `MemorySearcher`, MVVM base types) comes from the shared
`GameTrainers.Common` library.

## Notes

This is a single-player cheat tool for your own saved games. It reads and writes the emulator's memory
only — it never touches the network or any external service. **Game assets are copyrighted and are not
included** — supply your own legally obtained copy of Railroad Tycoon.
