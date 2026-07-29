# BeachHead 2000 — Live Trainer

A WPF (.NET 8) trainer for **BeachHead 2000** (Digital Fusion / WizardWorks, 2000), the classic
beach-defense arcade game shipped in the Steam "BeachHead Gold Edition" package. You man a
fixed bunker and defend against waves of infantry barges, tanks, APCs, bombers, jets, and
helicopters using three weapon types (bullets, projectiles, missiles). The trainer attaches
directly to the running game process (`Bh.exe` — a native 32-bit Windows executable, no
emulator needed) and gives you a Cheat-Engine-style **value scanner** to pin down health,
ammo, score, and the current level — plus a **freeze table** to hold them — and an offline
**level-file editor** for the 61 shipped level scripts.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

BeachHead 2000 is a native 32-bit Windows game with **no ASLR** (image base `0x00400000`),
but its mutable game state (health, ammo, score, current level) lives in **heap-allocated
memory** with no adjacent constant byte-run to anchor a locator to — so, like
`DarklandsTrainer` and `ThePerfectGeneral2Trainer`, the dependable primitive is a guided
**value scan** rather than a hard-coded address, and there is deliberately **no
`GameLocator`**. The trainer also includes a **level-file editor** for the plain-text
`Level_00`…`Level_60` scripts that define starting ammo, time limit, enemy aggression, and
unit waves — the only offline-editable surface the game exposes.

---

## Quick start

1. **Launch BeachHead 2000** from the Steam "BeachHead Gold Edition" package (the `509610`
   subfolder). The process name is `Bh`.
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `BeachHead2000Trainer.exe`, which requests administrator
   rights via UAC — reading/writing another process's memory needs them.
3. **Attach:** pick the `Bh` process from the dropdown (game processes are auto-sorted to the
   top) and click **Attach**.
4. **Scan:** on the **Value Scanner** tab, use a **guide** button (Health / Bullets /
   Projectiles / Missiles / Score / Level) to set the right width, then type a number you can
   see on screen, click **First Scan**, change the number in-game, type the new value and click
   **Exact** (or use **Increased/Decreased/Changed/Unchanged** when you don't know it). Repeat
   until one row remains.
5. **Pin & edit:** select a surviving row and click **Pin selected →**. On the **Freezes** tab,
   edit its **Target** to poke a value, or tick **Freeze** to have it re-written every ~200 ms.

---

## The tabs

- **Value Scanner** — the core Cheat-Engine loop over the attached process, backed by
  `GameTrainers.Common.Memory.MemorySearcher`. Byte / 16-bit / 32-bit widths; exact and
  relative narrowing; an unknown-value first scan. Six **guide** buttons preset the width and
  walk you through pinning Health (Int32), Bullets (Int32), Projectiles (Int32), Missiles
  (Int32), Score (Int32), and Level (Int32). Results are capped at 1000 rows and
  live-refreshed once the set is small (≤ 200).
- **Freezes** — the pin list. Each row shows a label, the live value, a user-set target poked
  on edit, and (when frozen) re-written every poll tick. A value that doesn't fit the pin's
  width is rejected before it can corrupt neighbouring bytes (read-validate-write).
- **Level Editor** — an offline editor for the shipped `Level_00`…`Level_60` files (plain-text
  scripts in the `beachhead\` subdirectory). Edit starting ammo (bullets/projectiles/missiles),
  time limit, enemy aggression (tank/jet/heli-gun/heli-rocket, 1–9), and the artillery flag.
  A **Max Ammo** button sets ammo to 999/99/99. Changes take effect when the level is loaded
  in-game (restart the level or advance to it). Always back up level files before editing.
- **Reference** — read-only weapon, enemy, and control tables for BeachHead 2000.

---

## Why a scanner instead of an auto-locator

BeachHead 2000's exe has no ASLR (a 2000-era build), so the image is always at `0x00400000`,
but the mutable game state — health, ammo, score, current level — is dynamically allocated on
the heap with no adjacent constant byte-run that a fixed locator could anchor to (confirmed by
dumping the `.data` section and scanning the full process memory). Rather than ship a fragile
guess, the trainer gives you the guided scan that reliably pins any on-screen scalar.

The level-file editor is the offline complement: the `Level_00`…`Level_60` files are
plain-text scripts that define the starting conditions for each level, and the trainer
parses, edits, and round-trips them without losing comments, blank lines, or unknown
properties.

---

## Verified against the game files

The game-knowledge layer is regression-tested by the `FormatCheck` harness:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` asserts the **Confirmed** game-facts constants (process name, image base, level
count, aggression range, weapon/enemy counts), the level-file parser (parse, field extraction,
round-trip with comments preserved, edge cases), the value-parsing helpers (decimal/hex,
width-fit), and the frozen-value view-model logic (poke, freeze re-write, out-of-width
rejection, write-failure report). No copyrighted game file is read — the level-file tests use
a synthetic fixture built from the Confirmed format observed in the shipped `Level_00`. It
exits 0 (pass) or 1 (fail).

---

## Project layout

```
src/BeachHead2000Trainer/
  Game/        GameFacts.cs       Confirmed constants: process name, image base, 61 levels,
                                aggression range, weapon/enemy/control tables, max ammo
               LevelFile.cs       Level-file parser/editor: Parse/Load/Save/ToText, preserves
                                all lines for round-trip, extracts Ammo/Time/Aggression/Artillery
  ViewModels/  MainViewModel      attach/scan/detach, 200 ms poll loop, pin/freeze, six guides,
                                level-file editor (load/edit/save/max ammo), reference data
               ScanValue          decimal/hex parsing + width-fit helpers
               ScanResultViewModel one scan candidate (address + live value)
               FrozenValueViewModel a pinned address: label, live value, poked target, freeze
               IScanHost           the read/write channel the rows use to reach RAM
  App.xaml, MainWindow.xaml        the WPF UI (Value Scanner / Freezes / Level Editor / Reference)
test/FormatCheck/                  headless verification of the game layer + view-model logic
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer
(`ProcessMemory`, `MemorySearcher`) come from the shared `GameTrainers.Common` library rather
than being duplicated here.

---

## Notes & caveats

- **Process target.** The trainer attaches to `Bh.exe` — the actual game executable, not the
  Steam launcher. The process picker auto-sorts processes matching `bh`/`bh2000`/`beachhead`
  to the top.
- **Level files.** The `Level_00`…`Level_60` files live in the `beachhead\` subdirectory of
  the game install (Steam Gold Edition installs to the `509610` folder). They are plain text
  (ASCII) with a simple script format: `Ammo <bullets> <projectiles> <missiles>`, `Time
  <seconds>`, `Aggression <tank> <jet> <heliGun> <heliRocket>` (1–9), `Artillery <0|1>`,
  then `Object`/`ObjectInc` blocks for enemy waves, terminated by `End`. The editor preserves
  all lines and only rewrites the header fields.
- **Widths.** All guided scans default to Int32. If a scan finds nothing, try Int16 — some
  values may be stored as 16-bit words.
- **Full-screen.** BeachHead 2000 runs full-screen by default, which makes alt-tabbing to the
  trainer difficult. The game ships with two DirectDraw wrappers — **DDrawCompat** (active by
  default, `ddraw.dll` in the game root) and **dgVoodoo** (in the `dgVoodoo\` subfolder). To run
  the game **windowed** so you can freely switch between the trainer and the game:

  1. **Close the game.** If it's hung, use Ctrl+Alt+Del → Task Manager → end `Bh.exe`, or press
     Ctrl+Alt+End (DDrawCompat's terminate hotkey).
  2. **Swap the DirectDraw wrapper** in the game root folder (`...\509610`):
     - Rename `ddraw.dll` (2.81 MB — the DDrawCompat one) to `ddraw_DDrawCompat.bak`.
     - Copy `DDraw.dll` from the `dgVoodoo\` subfolder into the game root and rename the copy to
       `ddraw.dll`.
     - Copy `D3D9.dll` from `dgVoodoo\` into the game root (dgVoodoo's D3D rendering backend).
     - Copy `dgVoodoo.conf` from `dgVoodoo\` into the game root.
  3. **Edit `dgVoodoo.conf`** in the game root — change `FullScreenMode = true` to
     `FullScreenMode = false`, and change `CaptureMouse = true` to `CaptureMouse = false` so the
     mouse isn't trapped in the window.
  4. **Launch the game.** It should now run in a 640×480 window that you can alt-tab freely
     between the trainer and the game.

  Alternatively, run `dgVoodoo\dgVoodooCpl.exe` (the dgVoodoo Control Panel) for a GUI to
  configure windowed mode, scaling, and other settings — but you still need to swap the
  `ddraw.dll` as described above.

  **To revert** to DDrawCompat fullscreen: rename `ddraw_DDrawCompat.bak` back to `ddraw.dll`
  and remove the dgVoodoo DLLs from the game root.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
