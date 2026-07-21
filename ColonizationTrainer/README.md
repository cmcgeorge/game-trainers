# Colonization — Trainer

A WPF (.NET 8) trainer for **Sid Meier's Colonization** (MicroProse, 1994 — the original DOS "Col1",
`VICEROY.EXE`). Its headline feature is an **offline save-game editor** for `COLONYxx.SAV` files:
open a save, edit any power's **gold** and **tax rate** (the fully-verified fields), plus Liberty
Bells, crosses, boycotts, Founding Fathers, and per-colony stockpiles, then write the file back in
place. A second tab is a **live value scanner** for tweaking gold/tax/bells while the game runs in
DOSBox, and a **References** tab surfaces the game's own tables (goods, units, terrain, professions,
buildings, Founding Fathers) and a strategy digest.

> Single-player cheat tool for your own saved games. Nothing here touches other machines or online
> services.

The save format was recovered by reverse-engineering the shipped `COLONYxx.SAV` files byte-for-byte
and cross-checking against three community sources that decoded `VICEROY.EXE` — see
[docs/Colonization-Reverse-Engineering.md](docs/Colonization-Reverse-Engineering.md). A play/strategy
guide is in [docs/Colonization-Strategy-Guide.md](docs/Colonization-Strategy-Guide.md).

---

## Quick start

### Edit a save (recommended — fully verified)

1. **Build & run:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `ColTrainer.exe` (it requests admin via UAC — the live tab reads
   another process's memory).
2. On the **Save Editor** tab, click **Open save…** and pick a `COLONYxx.SAV` from your Colonization
   folder. (If the game's own folder is found beside the trainer, the newest save auto-opens.)
3. Pick the **nation** to edit (defaults to you), set **Gold** / **Tax %** (or click **Max Gold**),
   grant **Founding Fathers**, edit any **colonies'** stockpiles, then click **Save**.
4. Load the save in-game to see the changes. A one-time `.bak` of the original is kept.

> Edit with the game **not** running (or before you next load in-game), so the emulator's in-memory
> copy doesn't overwrite your changes on its next save.

### Tweak values live (while playing)

1. Launch Colonization in DOSBox / DOSBox-X.
2. On the **Live (Value Scanner)** tab, pick the emulator process and **Attach**.
3. Use a **guided scan** (Gold / Tax / Liberty Bells): type the number you can see in-game, First
   Scan, change it in-game, scan again, and repeat until one address remains — then **Pin** and
   **Freeze** it.

---

## What the Save Editor can do

- **Gold** (treasury) and **Tax rate** — the two fully-verified edits. "Max Gold" sets 999,999,999
  (kept under the 4-byte value that would push the game's tax display off screen).
- **Liberty Bells** and **Crosses** for the selected nation.
- **Founding Fathers** — 25 checkboxes with **Grant all** / **Clear all** (a documented convenience;
  gold/tax are the guaranteed-clean edits).
- **Lift all boycotts** (undo Tea-Party boycotts).
- **Colonies** — for each colony, edit the **name**, **population**, **hammers**, and the full
  **16-good stockpile**, or **Fill warehouse** in one click. (A fresh 1492 game has no colonies yet —
  found one in-game and re-open the save.)
- Editing **any of the four powers** — not just your own — via the nation dropdown.

Every field is edited through a typed record view that clamps to a safe range, and the file has no
checksum, so an untouched save round-trips **byte-for-byte** (asserted by the test harness).

## How the format was found

The save is a flat, little-endian serialization with a `"COLONIZE"` signature and **no checksum**.
The header carries record counts; the human player's **gold** is a 4-byte value at offset `0x2A`
inside a 316-byte nation record, whose position is computed from those counts:

```
gold = 0x186 + 202·colony_count + 28·unit_count + 316·nation_index + 0x2A
```

This was confirmed empirically against the shipped saves (a 1492 English game correctly reads 0 gold,
0 % tax, no Founding Fathers) and against the `eb4x/viceroy` `savegame.h`, `pavelbel/smcol_saves_utility`,
and `nawagers/Colonization-SAV-files` projects. Full teardown:
[docs/Colonization-Reverse-Engineering.md](docs/Colonization-Reverse-Engineering.md).

---

## Verified against the real game

The parser is regression-tested by a headless `FormatCheck` harness:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

It builds a synthetic save, round-trips it byte-for-byte, exercises every edit (gold/tax/bells/
Founding-Father bitfield/colony stockpile) and its clamps, checks the offset arithmetic and the
reference books, and — **if** the copyrighted `.games\COLONY00.SAV` happens to be present — asserts
the empirically-confirmed real values (map 58×72, year 1492, England, 0 gold, 0 % tax, 77 units, 65
native dwellings). Exits `0` (pass) or `1` (fail).

---

## Project layout

```
src/ColonizationTrainer/
  Game/        SaveFormat.cs      the reverse-engineered offset table (header, nation, colony)
               Bytes.cs           bounds-checked little-endian read/write helpers
               ColonyText.cs      the plain-ASCII name codec
               SaveGame.cs        load/validate/parse a COLONYxx.SAV; save in place with a .bak
               NationRecord.cs    typed view over a 316-byte nation record (gold, tax, Fathers…)
               ColonyRecord.cs    typed view over a 202-byte colony record (name, pop, stock…)
               CargoBook / UnitBook / TerrainBook / ProfessionBook / BuildingBook /
               FoundingFatherBook / NationBook   the game's own enumerations
               Walkthrough.cs     condensed strategy digest (References ▸ Strategy)
  Memory/      (shared)           ProcessMemory / MemorySearcher — from GameTrainers.Common.Memory
  ViewModels/  MainViewModel, SaveEditorViewModel (+ Colony/Good/FoundingFather rows),
               LiveScannerViewModel (value scanner), ReferenceViewModel, and the scanner
               infrastructure (IScanHost, ScanValue, ScanResultViewModel, FrozenValueViewModel)
  App.xaml, MainWindow.xaml       the WPF UI (Save Editor / Live / References tabs)
test/FormatCheck/                 headless verification (synthetic round-trip + optional real save)
docs/          reverse-engineering notes and the strategy guide
.games/        the game itself + your COLONYxx.SAV saves (git-ignored; supply your own)
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer come from the
shared `GameTrainers.Common` library rather than being duplicated here.

---

## Notes & caveats

- **Gold and Tax are the verified edits.** Liberty Bells, crosses, boycotts, and the Founding-Father
  bitfield are documented but recompute some derived state in-game; treat them as conveniences and
  keep a backup.
- Edit saves with the game **not** running (or before you next load), so the emulator's copy doesn't
  overwrite your changes.
- Setting values very high is safe for the trainer; the game's UI may render unusually large numbers
  oddly (cosmetic). "Max Gold" deliberately stays under the 4-byte display glitch.
- The live scanner is inherently manual (Colonization's guest-RAM address changes each DOSBox
  session, with no stable signature to anchor to) — the **Save Editor** is the reliable path.
- Requires the **.NET 8 SDK** and **Windows** (WPF + memory APIs). Game assets are copyrighted and
  not included — supply your own legally obtained copy.
