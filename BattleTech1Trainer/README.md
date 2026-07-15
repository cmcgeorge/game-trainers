# BattleTech: The Crescent Hawk's Inception — Live Trainer

A WPF (.NET 8) trainer for the 1988 Westwood Associates / Infocom DOS RPG **BattleTech: The Crescent
Hawk's Inception**. It attaches to the running game (inside DOSBox / DOSBox-X) and gives you a
Cheat-Engine-style **value scanner** to pin down the numbers the game shows you — C-Bills, a
character's Health/Armor, skill levels — plus a **freeze table** to hold them, a **Detect game** button
that confirms the right process, and read-only **'Mech / Weapon / Skill** references transcribed from
`BTECH.EXE`.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

The game is a real-mode DOS binary whose mutable state (C-Bills, health, skills) lives in guest RAM at
an address that changes every DOSBox session, and that state is **not** stored next to any constant
byte-run that a fixed locator could anchor to (see `.docs/ReverseEngineering.md` §5). So — like
`ThePerfectGeneral2Trainer` — the dependable primitive is a guided value scan rather than a hard-coded
address. The one static structure that *was* confirmed byte-for-byte — the 17-byte weapon table — is
modelled in `Game/WeaponTable.cs` and exercised by the test harness.

---

## Quick start

1. **Launch BattleTech** in DOSBox/DOSBox-X and play past the title screen (state only exists in memory
   once a game is running).
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `BattleTechTrainer.exe`, which requests administrator rights via UAC
   — reading/writing another process's memory needs them, especially if the emulator is elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/DOSBox-X/etc. are auto-sorted to the
   top) and click **Attach**. Optionally click **Detect game** to signature-scan the process and confirm
   BattleTech is really the one you attached to.
4. **Scan:** on the **Value Scanner** tab, use a **guide** button (C-Bills / Health-Armor / Skill) to set
   the right width, then type a number you can see on screen, click **First Scan**, change the number
   in-game, type the new value and click **Exact** (or use **Increased/Decreased/Changed/Unchanged** when
   you don't know it). Repeat until one row remains.
5. **Pin & edit:** select a surviving row and click **Pin selected →**. On the **Freezes** tab, edit its
   **Target** to poke a value, or tick **Freeze** to have it re-written every ~200 ms.

---

## The tabs

- **Value Scanner** — the core Cheat-Engine loop over the attached process, backed by
  `GameTrainers.Common.Memory.MemorySearcher`. Byte / 16-bit / 32-bit widths; exact and relative
  narrowing; an unknown-value first scan. Three **guide** buttons preset the width and walk you through
  pinning C-Bills (Int32), Health/Armor (Byte), and a skill ordinal (Byte). Results are capped at 1000
  rows and live-refreshed once the set is small (≤ 200).
- **Freezes** — the pin list. Each row shows a label, the live value, a user-set target poked on edit,
  and (when frozen) re-written every poll tick. A value that doesn't fit the pin's width is rejected
  before it can corrupt neighbouring bytes (read-validate-write).
- **'Mechs** — the roster reference (names **Confirmed** from the EXE; tonnage/armor/role **Corroborated**).
- **Weapons** — every weapon name from the **Confirmed** 17-byte weapon table, tagged by scale.
- **Skills** — the seven **Confirmed** character skills and what the 0–4 ordinal means.

---

## Why a scanner instead of an auto-locator

DOSBox maps the guest's conventional RAM into the emulator at an offset that changes every session, so no
absolute guest address is stable — the trainer never hard-codes one (the repo-wide rule). The static
tables in the EXE *are* locatable by signature (that's what **Detect game** uses), but they are read-only
code/data — the live, mutable C-Bills/health/skill values are not stored next to them, so there is no
constant byte-run to anchor a value locator to (the same problem TPG2 faces). Rather than ship a fragile
guess, the trainer gives you the guided scan that reliably pins any on-screen scalar. See
`.docs/ReverseEngineering.md` §5–§6 for the open Ghidra leads (the 'Mech record stride, a live-state
anchor, the save format, and the `.MTP` map format).

---

## Verified against the game files

The game-knowledge layer is regression-tested by the `FormatCheck` harness:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` asserts the Confirmed 17-byte weapon-table format (records decode at stride 17, names are
trimmed, class tags and mis-sized blocks are handled), the 'Mech / weapon / skill references, the
detection signatures (the Inspect-field block and title decode to their known ASCII), the value-parsing
helpers (decimal/hex, width-fit), and the frozen-value view-model headlessly (target poke, freeze
re-write, out-of-width rejection, write-failure report). It exits 0 (pass) or 1 (fail).

---

## Project layout

```
src/BattleTech1Trainer/
  Game/        WeaponTable.cs      the Confirmed 17-byte weapon-record decoder (+ regression-tested)
               WeaponReference.cs  the weapon names by scale (Confirmed), for the Weapons tab
               MechReference.cs    the 'Mech roster (names Confirmed; stats Corroborated)
               SkillSheet.cs       the 7 skills + 5 proficiency levels (Confirmed)
               GameSignatures.cs   the EXE byte signatures the detector scans for (Confirmed)
  Memory/      GameDetector.cs     signature scan that confirms the attached process is BattleTech
  ViewModels/  MainViewModel       attach/scan/detect/detach, 200 ms poll loop, pin/freeze, guides
               ScanValue           decimal/hex parsing + width-fit helpers
               ScanResultViewModel one scan candidate (address + live value)
               FrozenValueViewModel a pinned address: label, live value, poked target, freeze
               IScanHost           the write channel the rows use to reach RAM
  App.xaml, MainWindow.xaml        the WPF UI (Value Scanner / Freezes / 'Mechs / Weapons / Skills)
test/FormatCheck/                  headless verification of the game layer + view-model logic
.docs/         ReverseEngineering.md (teardown) + StrategyGuide.md (how to play / win / maps)
.game/         the game itself (copyrighted; supply your own copy)
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer (`ProcessMemory`,
`MemorySearcher`, `BytePatternScanner`) come from the shared `GameTrainers.Common` library rather than
being duplicated here.

---

## Notes & caveats

- **Reverse-engineering method.** Ghidra and DOSBox were not available on the build machine, so the
  teardown was done by static analysis of the shipped files (see `.docs/ReverseEngineering.md` for full
  provenance and confidence tags). Everything the trainer relies on is tagged **Confirmed** from
  `BTECH.EXE`; the runtime RAM layout is deliberately left to the value scanner.
- **C-Bills width.** Walkthroughs reach ~350,000 C-Bills, so the total is wider than a byte — the C-Bills
  guide scans Int32; try Int16 if that finds nothing.
- **Save editing is out of scope for v1.** No save file ships in `.game/`, and community-cited save
  offsets are unverified — the trainer does not hard-code any of them.
- Some emulators map guest RAM more than once; a broad scan may show duplicate addresses. Narrow with a
  few in-game changes until a single row survives.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
