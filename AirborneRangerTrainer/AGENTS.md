# Airborne Ranger Trainer — Contributor Guide

A Windows-only C#/WPF live-memory trainer for **Airborne Ranger** (MicroProse, 1988, IBM PC version
441.01) running under DOSBox / DOSBox-X, plus an offline editor for the game's career file.
Read [README.md](README.md) and [docs/ReverseEngineering.md](docs/ReverseEngineering.md) before
changing anything under `Game/` or `Memory/`.

## The one fact everything rests on

`AR.EXE` is **EXEPACK-compressed**; unpacked it is a **medium-model** 16-bit program — one code
segment, one data segment (`DGROUP`), one stack segment:

| Segment | Unpacked image range | Size | Register |
| --- | --- | --- | --- |
| Code | `0x00000`–`0x0CE7F` | 52,864 | `CS = load + 0x0000` |
| **Data** | `0x0CE80`–`0x1B50F` | **59,024 (`0xE690`)** | `DS = load + 0x0CE8` |
| Stack | `0x1B510`–`0x2802F` | 52,000 | `SS = load + 0x1B51` |

Only the load segment moves between sessions, so **every global has a constant `DGROUP` offset** and
the trainer needs no value scanning at all. Do not add a Cheat-Engine-style scanner here; if the
locator fails, the fix is a better anchor, not a scan.

## Project structure

```
docs/                      committed RE notes and strategy guide (also copied into the game folder)
src/AirborneRangerTrainer/
  Game/                    the game-knowledge layer — all reverse-engineered constants live here
  Memory/                  GameLocator + IMemorySource; ProcessMemory comes from GameTrainers.Common
  ViewModels/              hand-rolled MVVM over GameTrainers.Common.Mvvm
test/FormatCheck/          headless verification harness, exits 0 (pass) or 1 (fail)
.docs/  .game/             git-ignored: the proposal, and a copy of a copyrighted ROSTER.DAT
```

`GameTrainers.Common` supplies `ProcessMemory`/`MemoryRegion` and `ObservableObject`/`RelayCommand`
(pulled in via csproj `<Using>` items — note `ObservableObject` uses `SetField`). Keep game-specific
code local; fix shared plumbing in `GameTrainers.Common` so every trainer gets it.

## How offsets are established here

The productive route was **not** the disassembly on its own. The status panel is a fill-in-the-blanks
text template: the shipped executable stores literal `X` placeholders that the game overwrites with
ASCII digits. Searching the code segment for the placeholder addresses (`0xB930`, `0xB955`, …) finds
the single routine at `0xBB43` that fills the panel, and that routine names its own sources
(`mov al,[0xC896]` → grenades, and so on). If you need a field this trainer does not have, look for
its on-screen text first and work backwards the same way.

Two independent cross-checks keep the table honest, and both are asserted in `FormatCheck`:

* the panel's magazine count is **spare magazines + 1**, matching the game's own `inc al`;
* the panel's weight is exactly the sum of the supply-pod item prices, plus 1 for the loaded
  magazine — `3×1 + 3×2 + 1×6 + 1×3 + 1×3 + 1 = 22`.

Mark every new constant `[Confirmed]` or `[Inferred]` in its doc comment, and say how. The mission
clock in particular is **three separate decimal-digit bytes**, not a number — searching memory for
`600` while the panel reads `TIME 600` finds nothing.

## Conventions

C# targeting `net8.0-windows`, `Nullable` and `ImplicitUsings` on. Note that the WPF implicit-usings
set does **not** include `System.IO` — add it explicitly. Match the surrounding file: 4-space indent,
file-scoped namespaces, `sealed` classes, PascalCase members, `_camelCase` fields, `const` hex for
offsets, XML `///` docs on public members.

Follow the read-validate-write pattern. Every `MissionState` setter clamps, skips a no-op write, and
flushes only the byte range it touched — never the whole window.

**The shadow buffer is the trap in this trainer.** `MissionState` suppresses a write when the new
value already matches its buffer — but that buffer holds what the trainer last *wrote*, and the game
moves these counters several times a second. Comparing against it directly makes both an edit and a
freeze silently do nothing exactly when the game has drifted away from the value. Every path that
compares therefore calls `MissionViewModel.SyncFromLive` first (`Edit` for user edits, `Restore` for
freezes); any new editable or frozen field must go through one of them.

**A freeze holds values for exactly one mission.** `MissionViewModel.MissionIsRunning` (a non-zero
countdown) gates `ApplyFreezes`; any pin taken while no mission is running is *provisional* and is
re-taken on the first running tick; and every non-running tick marks all three pins provisional
again. That last part is what makes the rule hold at a mission **boundary** — without it a freeze
armed midway through one mission carries its pin into the next and clamps a fresh 600-second clock
back to where the last one ended, or restores a dead ranger's spent loadout. `FormatCheck` asserts
both directions, and the assertions were mutation-tested: reverting either half fails them.

## Build, test, run

```powershell
.\Run.ps1                      # restore, build Release, launch (UAC prompt)
.\Run.ps1 -Test -NoRun         # run the 439-check harness, no GUI
.\Run.ps1 -Clean -Configuration Debug
dotnet run --project test/FormatCheck
```

The harness needs neither the game nor an emulator. Put a copy of a real `ROSTER.DAT` in `.game\` to
enable the extra 14 checks against the shipped career file; without it that group is skipped with a
note rather than failed. Keep the harness green — the GUI cannot be smoke-tested headlessly.

## Testing against the live game

The game has no pause and enemies engage within a minute of landing, so a live session is short.
What worked: run DOSBox at a low cycle count (`cycles=2000`) so the airdrop is drivable, hold keys
rather than tapping them (the game samples make/break codes through its own INT 9 handler and misses
an instant down-up), and press fire when the jump light in the bottom-left corner brightens — if you
never jump, the mission aborts with nothing scored.

## Things deliberately not done

* **No structural fallback in the locator.** The mission-state block is all zeros before the first
  mission and holds stale values after one, so it has no distinctive shape; a structural scan would
  return a confident wrong answer. Four literals in one 59 KB segment is the stronger evidence.
* **No teleport** — map position was not identified, and a guessed address is worse than no feature.
* **Roster tail bytes 3 and 4** are round-tripped, not interpreted; six samples do not close them.
* **No `.DTX` decoding** — the container is a single undecoded compression format and nothing needs it.
