# Darklands — Live Trainer

A WPF (.NET 8) trainer for the 1992 MicroProse open-world DOS RPG **Darklands**, set in an
"as-people-believed-it" Holy Roman Empire around 1400. It attaches to the running game (inside DOSBox /
DOSBox-X) and gives you a Cheat-Engine-style **value scanner** to pin down the numbers you can read on
the character and party screens — attributes, skill levels, party Fame, the coin purse — plus a
**freeze table** to hold them, and read-only **Attribute / Skill / Currency & Fame** references
transcribed from `DARKLAND.EXE` and the shipped `SAVES\DEFAULT` template.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

Darklands ships as a **PKLITE-compressed** Microsoft-C binary that a DOS extender relocates into
extended memory, so its mutable state lives in guest RAM at an address that changes every DOSBox session
and is **not** stored next to any constant byte-run that a fixed locator could anchor to (see
`.docs/ReverseEngineering.md` §1, §5). So — like `ThePerfectGeneral2Trainer` and `BattleTech1Trainer` —
the dependable primitive is a guided value scan rather than a hard-coded address, and there is
deliberately **no `GameLocator`**.

---

## Quick start

1. **Launch Darklands** in DOSBox/DOSBox-X and load or start a party (state only exists in memory once a
   game is running).
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `DarklandsTrainer.exe`, which requests administrator rights via UAC
   — reading/writing another process's memory needs them, especially if the emulator is elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/DOSBox-X/etc. are auto-sorted to the
   top) and click **Attach**.
4. **Scan:** on the **Value Scanner** tab, use a **guide** button (Endurance / Strength / Skill / Fame /
   Florins) to set the right width, then type a number you can see on screen, click **First Scan**,
   change the number in-game, type the new value and click **Exact** (or use
   **Increased/Decreased/Changed/Unchanged** when you don't know it). Repeat until one row remains.
5. **Pin & edit:** select a surviving row and click **Pin selected →**. On the **Freezes** tab, edit its
   **Target** to poke a value, or tick **Freeze** to have it re-written every ~200 ms.

---

## The tabs

- **Value Scanner** — the core Cheat-Engine loop over the attached process, backed by
  `GameTrainers.Common.Memory.MemorySearcher`. Byte / 16-bit / 32-bit widths; exact and relative
  narrowing; an unknown-value first scan. Five **guide** buttons preset the width and walk you through
  pinning Endurance (Byte), Strength (Byte), a skill level (Byte), Fame (Int16), and Florins (Int16).
  Results are capped at 1000 rows and live-refreshed once the set is small (≤ 200).
- **Freezes** — the pin list. Each row shows a label, the live value, a user-set target poked on edit,
  and (when frozen) re-written every poll tick. A value that doesn't fit the pin's width is rejected
  before it can corrupt neighbouring bytes (read-validate-write).
- **Attributes** — the six **Confirmed** primary attributes (+ party-wide Divine Favor) and what each
  governs.
- **Skills** — the nineteen **Confirmed** character skills, their long and short spellings, and what
  they cover.
- **Reference** — the three-tier currency (1 Florin = 20 Groschen = 240 Pfennig) and the eleven-rung
  party **Fame** ladder, both **Confirmed** from the EXE.

---

## Why a scanner instead of an auto-locator

DOSBox maps the guest's RAM into the emulator at an offset that changes every session, so no absolute
guest address is stable — the trainer never hard-codes one (the repo-wide rule). Darklands compounds
this: the EXE is PKLITE-packed and extender-relocated, so even its static tables aren't at a fixed image
offset, and the live, mutable attribute/skill/Fame/purse values are dynamically allocated with no
constant byte-run to anchor a value locator to (the same problem TPG2 and BattleTech face). Rather than
ship a fragile guess, the trainer gives you the guided scan that reliably pins any on-screen scalar. See
`.docs/ReverseEngineering.md` §5–§6 for the open Ghidra leads (a live-state anchor, the full save
layout, and the byte→skill mapping).

---

## Verified against the game files

The game-knowledge layer is regression-tested by the `FormatCheck` harness:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` asserts the **Confirmed** attribute / skill / currency / Fame tables, the currency-split
maths, and the read-only save reader against a synthetic fixture built from the DEFAULT template's
Confirmed offsets (location, label, party count, portrait codes, name/nickname, and the current/max
attribute blocks). It also exercises the value-parsing helpers (decimal/hex, width-fit) and the
frozen-value view-model headlessly (target poke, freeze re-write, out-of-width rejection, write-failure
report). No copyrighted save file is read. It exits 0 (pass) or 1 (fail).

---

## Project layout

```
src/DarklandsTrainer/
  Game/        AttributeBook.cs    the six Confirmed primaries (+ Divine Favor), for the Attributes tab
               SkillBook.cs        the nineteen Confirmed skills (long + short spellings)
               GameFacts.cs        currency ratios + the Fame ladder, and the pfennig-split helper
               SaveFile.cs         read-only DEFAULT-save reader (regression-tested; never writes saves)
  ViewModels/  MainViewModel       attach/scan/detach, 200 ms poll loop, pin/freeze, five guides
               ScanValue           decimal/hex parsing + width-fit helpers
               ScanResultViewModel one scan candidate (address + live value)
               FrozenValueViewModel a pinned address: label, live value, poked target, freeze
               IScanHost           the read/write channel the rows use to reach RAM
  App.xaml, MainWindow.xaml        the WPF UI (Value Scanner / Freezes / Attributes / Skills / Reference)
test/FormatCheck/                  headless verification of the game layer + view-model logic
.docs/         ReverseEngineering.md (teardown) + StrategyGuide.md (how to play / win / maps)
.game/         the game itself (copyrighted; supply your own copy)
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer (`ProcessMemory`,
`MemorySearcher`) come from the shared `GameTrainers.Common` library rather than being duplicated here.

---

## Notes & caveats

- **Reverse-engineering method.** The teardown was done by extracting the EXE's embedded ASCII tables,
  hex-dumping the `SAVES\DEFAULT` template, and Ghidra headless auto-analysis (see
  `.docs/ReverseEngineering.md` for full provenance and confidence tags). Everything the trainer relies
  on is tagged **Confirmed** from `DARKLAND.EXE` / the DEFAULT save; the runtime RAM layout is
  deliberately left to the value scanner.
- **Save editing is out of scope.** The DEFAULT offsets are derived from a single template sample, so
  the trainer's `SaveFile` reader is **read-only** (used only by `FormatCheck`) and the trainer never
  writes them back — live edits go through the value scanner instead.
- **Widths.** Attribute and skill values are bytes; Fame and the coin counts behave as 16-bit words —
  the guides preset these, but try a wider type if a scan finds nothing. The three coins (Florin /
  Groschen / Pfennig) are tracked separately in RAM, so scan each on its own.
- Some emulators map guest RAM more than once; a broad scan may show duplicate addresses. Narrow with a
  few in-game changes until a single row survives.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
