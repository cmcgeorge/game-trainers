# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for the 1994 Quantum Quality Productions DOS
tactical wargame *The Perfect General II*, running under DOSBox / DOSBox-X. Windows-only (WPF + Win32
memory APIs); the app manifest requests administrator rights so it can `Read/WriteProcessMemory` on the
emulator.

## What makes this trainer different

TPG2 is a 16-bit DOS **protected-mode (DPMI)** program (ships `RTM.EXE` / `DPMI16BI.OVL`); its state
lives in a run-time heap at an address that changes every session. The trainer auto-locates the purchase
state by scanning for the constant ASCII file-path string `D:\ICONS\MSGR.DAT` (loaded into the DPMI heap
at run time, unique in the emulator process), then derives the count array, Buy Points, and Units
Purchased at fixed offsets from that anchor (see `Game/GameLocator.cs`). A Cheat-Engine-style **value
scan** via `GameTrainers.Common.Memory.MemorySearcher` remains available for scalars the locator doesn't
cover (a unit's hit points during battle, turn counters, etc.). See `.docs/ReverseEngineering.md` for
the full teardown, confidence tags, and the open Ghidra leads.

## Project Structure & Module Organization

Three projects in `ThePerfectGeneral2Trainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/ThePerfectGeneral2Trainer/` — the WPF app (`AssemblyName` `PG2Trainer`, `RootNamespace`
  `ThePerfectGeneral2Trainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies.
    - `UnitReference.cs` — the 16-unit `UNITINFO.DOC` stat table (cost/move/bombard/HP/damage/
      repairable/AA/attack+defense style) as **Confirmed** game rules, in `UNITINFO.DOC` stat-table
      order. Blank numeric fields (a mine has no HP) are `int?` null; range/note fields (a plane's
      move) are text.
    - `PurchaseFormat.cs` — the **Confirmed** 16-byte purchased-unit count array (one byte per type),
      in **purchase-screen** order — which differs from the stat-table order only in where MINE sits
      (first vs. 14th). `Decode`/`TotalUnits` throw on a mis-sized block so labels can never mis-align.
    - `GameLocator.cs` — auto-locates the purchase state by scanning for the `D:\ICONS\MSGR.DAT`
      anchor string (Confirmed unique in both memory dumps), then deriving the count array
      (−0x16E), Buy Points (−0x2E0), and Units Purchased (−0x2E2) at fixed offsets. Validates the
      count array (each byte ≤ 100, sum equals Units Purchased) so far-pointer soup is rejected when
      the game is not on the purchase screen.
  - `ViewModels/` — hand-rolled MVVM using `GameTrainers.Common.Mvvm` (`ObservableObject` exposes
    `SetField(ref field, value)`; `RelayCommand`).
    - `MainViewModel` — attach/detach, auto-locate, background scan `Task` with cancellation, 200 ms
      poll loop (re-writes frozen pins and purchase items, live-refreshes a small result set),
      pin/freeze actions, the Buy Points guide. Implements `IScanHost` and `IDisposable`.
    - `ScanValue` — decimal/hex parsing + width-fit helpers (pure, unit-tested).
    - `ScanResultViewModel` — one scan candidate (address + live value).
    - `FrozenValueViewModel` — a pinned address: live value, user target poked on edit, freeze
      re-write; rejects an out-of-width target (read-validate-write).
    - `PurchaseItemViewModel` — an auto-located purchase value (Buy Points or a per-type unit count):
      labelled, live value, editable target, freeze. Same read-validate-write pattern as
      `FrozenValueViewModel`.
    - `IScanHost` — the write channel the rows use to reach RAM.
  - `App.xaml` / `MainWindow.xaml` — the WPF UI (Purchase / Value Scanner / Freezes / Unit Reference tabs).
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.

Reverse-engineering notes and the strategy guide live in `.docs/`; RAM dumps in `.data/` (described by
`.data/memdump.md`); the game itself in `.game/`. Dot-prefixed dirs are git-ignored — never commit them.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration
  Debug|Release`, `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\ThePerfectGeneral2Trainer\ThePerfectGeneral2Trainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use file-scoped
namespaces (`namespace ThePerfectGeneral2Trainer.Game;`), XML `<summary>` docs on public types/members,
`sealed` classes by default, `const` for the counts/widths, and `// --- section ---` divider comments.
No linter/formatter config is committed; match the surrounding file. Keep all reverse-engineered
constants in the `Game/` layer and follow the read-validate-write pattern so a shifted layout is never
corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` asserts the Confirmed 16-byte purchase count-array format
(the verbatim sample from `.data/memdump.md` decodes in order and sums to 36; mis-sized blocks are
rejected), the `UNITINFO.DOC` unit reference, the `GameLocator` anchor/offsets/validator, the
value-parsing/width-fit helpers, and the frozen-value and purchase-item view-model logic (poke, freeze
re-write, out-of-width rejection, write-failure report). It runs individual `Check(...)` assertions and
returns exit code 0 (pass) or 1 (fail). Any parser/format or view-model change must keep the assertions
green.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon.
Describe which game state a change reads/writes and how it was confirmed against the dumps or live
game. No PR template exists.

## Domain Notes

Two distinct unit orderings exist — the purchase/count-array order (`PurchaseFormat`) and the
`UNITINFO.DOC` stat-table order (`UnitReference`); they differ only in MINE's position. Keep them
straight. Editing the count array does **not** refund Buy Points (the engine tracks that scalar
separately) — the auto-locator surfaces both so you can edit Buy Points directly. The value scanner
remains for scalars the locator doesn't cover (HP during battle, turn counters); the placed-unit records
and the unit-rules record stride are known open leads (`.docs/ReverseEngineering.md` §4/§6), left for
Ghidra work rather than guessed.
