# The Perfect General II — Live Trainer

A WPF (.NET 8) trainer for the 1994 Quantum Quality Productions DOS tactical wargame **The Perfect
General II**. It attaches to the running game (inside DOSBox / DOSBox-X) and gives you a Cheat-Engine
-style **value scanner** to pin down the numbers the game shows you — Buy Points, a unit's hit points,
turn counters — plus a **freeze table** to hold them and a read-only **Unit Reference** transcribed
from the shipped `UNITINFO.DOC`.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

Unlike the sibling trainers, TPG2 is a 16-bit DOS *protected-mode* (DPMI) program whose state lives in
a run-time heap with **no stable static signature**, so a fixed anchor-based locator isn't reliable
(see `.docs/ReverseEngineering.md`). The dependable primitive is therefore a guided value scan, which
is what this trainer is built around. The one dynamic structure that *was* confirmed byte-for-byte
against the memory dumps — the 16-byte purchased-unit count array — is modelled in `Game/PurchaseFormat.cs`
and exercised by the test harness.

---

## Quick start

1. **Launch The Perfect General II** in DOSBox/DOSBox-X and play into a scenario (state only exists in
   memory once a game is running).
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `PG2Trainer.exe`, which requests administrator rights via UAC —
   reading/writing another process's memory needs them, especially if the emulator is elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/DOSBox-X/etc. are auto-sorted to the
   top) and click **Attach**.
4. **Scan:** on the **Value Scanner** tab, type a number you can see on screen (e.g. Buy Points
   Remaining), click **First Scan**, change the number in-game, type the new value and click **Exact**
   (or use **Increased/Decreased/Changed/Unchanged** when you don't know the exact value). Repeat until
   one row remains. The **Buy Points guide** button walks you through this for the purchase screen.
5. **Pin & edit:** select a surviving row and click **Pin selected →**. On the **Freezes** tab, edit
   its **Target** to poke a value, or tick **Freeze** to have it re-written every ~200 ms.

---

## The three tabs

- **Value Scanner** — the core Cheat-Engine loop over the attached process, backed by
  `GameTrainers.Common.Memory.MemorySearcher`. Byte / 16-bit / 32-bit widths; exact and relative
  (increased/decreased/changed/unchanged) narrowing; an unknown-value first scan. Results are capped at
  1000 rows so a broad scan can't flood the grid, and are live-refreshed by the poll loop once the set
  is small (≤ 200).
- **Freezes** — the pin list. Each row shows the live value, holds a user-set target that is poked on
  edit, and (when frozen) is re-written every poll tick so combat/spending can't move it back. A value
  that doesn't fit the pin's width is rejected before it can corrupt neighbouring bytes
  (read-validate-write).
- **Unit Reference** — the **Confirmed** `UNITINFO.DOC` unit table (cost, move, bombard range, hit
  points, damage, repairable, AA, attack/defense style) surfaced in-app so you can weigh match-ups
  without alt-tabbing to the manual.

---

## Why a scanner instead of an auto-locator

The buffer's live address changes every DOSBox session, so the trainer never hard-codes it — the
repo-wide rule. The other trainers pin a fixed offset from a constant static signature (a name table, a
strength-block run). TPG2's DPMI heap block holding the confirmed count array is surrounded by 16:16
protected-mode far-pointer soup with **no strong constant bytes adjacent**, and the unit-rules tables
are stored as strided struct records rather than contiguous arrays, so neither could be turned into a
reliable value-independent locator from the two static dumps available. Rather than ship a fragile
guessed anchor, the trainer gives you the guided scan that reliably pins any on-screen scalar. See
`.docs/ReverseEngineering.md` §4 and §6 for the open leads (Ghidra work to recover the record stride
and a DGROUP string anchor).

---

## Verified against the game files

The game-knowledge layer is regression-tested by the `FormatCheck` harness:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` asserts the Confirmed 16-byte purchase count-array format (the verbatim sample from
`.data/memdump.md` decodes in purchase-screen order and sums to the recorded *Units Purchased: 36*, and
mis-sized blocks are rejected), the `UNITINFO.DOC` unit reference (16 units, spot-checked costs/HP), the
value-parsing helpers (decimal/hex, width-fit), and the frozen-value view-model headlessly (target
poke, freeze re-write, out-of-width rejection, write-failure report). It exits 0 (pass) or 1 (fail).

---

## Project layout

```
src/ThePerfectGeneral2Trainer/
  Game/        UnitReference.cs    the UNITINFO.DOC unit table (Confirmed game rules), stat-table order
               PurchaseFormat.cs   the Confirmed 16-byte purchased-unit count array, purchase-screen order
  ViewModels/  MainViewModel       attach/scan/detach, 200 ms poll loop, pin/freeze, Buy Points guide
               ScanValue           decimal/hex parsing + width-fit helpers
               ScanResultViewModel one scan candidate (address + live value)
               FrozenValueViewModel a pinned address: live value, poked target, freeze
               IScanHost           the write channel the rows use to reach RAM
  App.xaml, MainWindow.xaml        the WPF UI (Value Scanner / Freezes / Unit Reference tabs)
test/FormatCheck/                  headless verification of the game layer + view-model logic
.docs/         ReverseEngineering.md (teardown) + StrategyGuide.md (how to play / win / maps)
.game/         the game itself (copyrighted; supply your own copy)
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer
(`ProcessMemory`, `MemorySearcher`) come from the shared `GameTrainers.Common` library rather than
being duplicated here.

---

## Notes & caveats

- **Reverse-engineering method.** The confirmed count-array format was recovered by searching two full
  DOSBox-X process dumps (`.data/`) for the known purchase basket; the unit-rules tables were
  transcribed from the shipped `UNITINFO.DOC`. The DPMI heap has no stable anchor, so a
  signature-based auto-locator was intentionally *not* shipped — the value scanner replaces it. See
  `.docs/ReverseEngineering.md` for full provenance and confidence tags.
- Editing the purchase count array changes the counts the purchase screen shows; it does **not** by
  itself refund Buy Points, which the engine tracks separately — pin Buy Points with the guided scan.
- Some emulators map guest RAM more than once; a broad scan may show duplicate addresses. Narrow with
  a few in-game changes until a single row survives.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
