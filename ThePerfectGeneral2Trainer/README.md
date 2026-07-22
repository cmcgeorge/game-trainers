# The Perfect General II — Live Trainer

A WPF (.NET 8) trainer for the 1994 Quantum Quality Productions DOS tactical wargame **The Perfect
General II**. It attaches to the running game (inside DOSBox / DOSBox-X) and **auto-locates** the
purchase state — Buy Points Remaining and per-type unit counts — by scanning for a constant string the
game loads into its DPMI heap. A Cheat-Engine-style **value scanner** remains available for other
scalars (a unit's hit points during battle, turn counters), plus a **freeze table** to hold any pinned
value and a read-only **Unit Reference** transcribed from the shipped `UNITINFO.DOC`.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

TPG2 is a 16-bit DOS *protected-mode* (DPMI) program whose state lives in a run-time heap whose address
changes every session. The trainer locates it by scanning for the ASCII file-path string
`D:\ICONS\MSGR.DAT` (loaded into the DPMI heap at run time, unique in the emulator process), then
deriving the count array, Buy Points, and Units Purchased at fixed offsets from that anchor —
byte-verified against two full memory dumps. The one dynamic structure that was confirmed byte-for-byte
against the dumps — the 16-byte purchased-unit count array — is modelled in `Game/PurchaseFormat.cs`
and now wired into the live UI through `Game/GameLocator.cs`.

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
4. **Auto-Locate:** click **Auto-Locate** (in the toolbar or on the **Purchase** tab). The trainer
   scans for the game's anchor string and populates the Purchase tab with Buy Points Remaining and
   per-type unit counts. Edit any **Target** to poke a value, or tick **Freeze** to hold it. (Navigate
   to the purchase screen in-game first — the locator validates the count array and will tell you if
   the purchase screen isn't active.)
5. **Value Scanner (optional):** for values the auto-locator doesn't cover (a unit's hit points during
   battle, turn counters), use the **Value Scanner** tab — type a number you see on screen, click
   **First Scan**, change it in-game, click **Exact** (or **Increased/Decreased/Changed/Unchanged**),
   repeat until one row remains, then **Pin selected →** to the freeze table.

---

## The four tabs

- **Purchase** — the auto-located purchase state. Click **Auto-Locate** to scan for the game's
  `D:\ICONS\MSGR.DAT` anchor and populate Buy Points Remaining + 16 per-type unit counts (in
  purchase-screen order). Edit any **Target** to poke a value into RAM, or tick **Freeze** to have it
  re-written every ~200 ms so spending can't move it back. The locator validates the count array
  (each byte ≤ 100, sum equals Units Purchased) and tells you if the purchase screen isn't active.
- **Value Scanner** — the Cheat-Engine loop over the attached process, backed by
  `GameTrainers.Common.Memory.MemorySearcher`. Byte / 16-bit / 32-bit widths; exact and relative
  (increased/decreased/changed/unchanged) narrowing; an unknown-value first scan. Results are capped at
  1000 rows so a broad scan can't flood the grid, and are live-refreshed by the poll loop once the set
  is small (≤ 200). Use this for values the auto-locator doesn't cover (HP during battle, turn counters).
- **Freezes** — the pin list. Each row shows the live value, holds a user-set target that is poked on
  edit, and (when frozen) is re-written every poll tick so combat/spending can't move it back. A value
  that doesn't fit the pin's width is rejected before it can corrupt neighbouring bytes
  (read-validate-write).
- **Unit Reference** — the **Confirmed** `UNITINFO.DOC` unit table (cost, move, bombard range, hit
  points, damage, repairable, AA, attack/defense style) surfaced in-app so you can weigh match-ups
  without alt-tabbing to the manual.

---

## How the auto-locator works

The buffer's live address changes every DOSBox session, so the trainer never hard-codes it — the
repo-wide rule. The auto-locator scans all committed memory regions for the ASCII string
`D:\ICONS\MSGR.DAT` (a file path the game loads into its DPMI heap at run time). This string was found
exactly once in each of two full ~400 MB DOSBox-X process dumps across two game states (purchase screen
and round start). The purchase count array sits 0x16E bytes before the anchor; Buy Points Remaining
(Int16 LE) sits 0x2E0 bytes before; Units Purchased (Int16 LE) sits 0x2E2 bytes before — all
byte-verified against the purchase-screen dump. When the game is not on the purchase screen, the
count-array area is overwritten with DPMI far-pointer soup; the validator rejects that (each byte must
be ≤ 100 and the sum must equal Units Purchased), so the UI can tell the user to navigate to the
purchase screen. The value scanner remains for scalars the locator doesn't cover. See
`.docs/ReverseEngineering.md` §4 and §6 for the open leads (Ghidra work to recover the record stride
and decode the placed-unit records).

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
`GameLocator` anchor bytes and offsets (byte-verified against the dumps), the purchase-screen validator
(accepts the sample, rejects far-pointer soup and mismatches), the value-parsing helpers (decimal/hex,
width-fit), and the frozen-value and purchase-item view-models headlessly (target poke, freeze
re-write, out-of-width rejection, write-failure report). It exits 0 (pass) or 1 (fail).

---

## Project layout

```
src/ThePerfectGeneral2Trainer/
  Game/        UnitReference.cs    the UNITINFO.DOC unit table (Confirmed game rules), stat-table order
               PurchaseFormat.cs   the Confirmed 16-byte purchased-unit count array, purchase-screen order
               GameLocator.cs      auto-locator: scans for D:\ICONS\MSGR.DAT anchor, derives count array / Buy Points / Units Purchased
  ViewModels/  MainViewModel       attach/auto-locate/scan/detach, 200 ms poll loop, pin/freeze
               ScanValue           decimal/hex parsing + width-fit helpers
               ScanResultViewModel one scan candidate (address + live value)
               FrozenValueViewModel a pinned address: live value, poked target, freeze
               PurchaseItemViewModel an auto-located purchase value: label, live, target, freeze
               IScanHost           the write channel the rows use to reach RAM
  App.xaml, MainWindow.xaml        the WPF UI (Purchase / Value Scanner / Freezes / Unit Reference tabs)
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
  DOSBox-X process dumps (`.data/`) for the known purchase basket; the `D:\ICONS\MSGR.DAT` anchor and
  the offsets to the count array, Buy Points, and Units Purchased were found by comparing the two dumps
  and byte-verifying against the purchase-screen sample. The unit-rules tables were transcribed from
  the shipped `UNITINFO.DOC`. See `.docs/ReverseEngineering.md` for full provenance and confidence tags.
- Editing the purchase count array changes the counts the purchase screen shows; it does **not** by
  itself refund Buy Points, which the engine tracks separately — the auto-locator surfaces Buy Points
  as an editable value so you can set it directly.
- Some emulators map guest RAM more than once; a broad scan may show duplicate addresses. Narrow with
  a few in-game changes until a single row survives.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
