# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for **Imperialism II: The Age of Exploration**
(Frog City / SSI, 1999). It is the repo's **first native-Windows target** — every other trainer drives a
DOS game under DOSBox, but Imperialism II is a native 32-bit Windows program, so the trainer attaches
directly to `Imperialism II.exe` (no emulator, no guest-address translation) and value-scans its memory.
Windows-only (WPF + Win32 memory APIs); the app manifest requests administrator rights so it can
`Read/WriteProcessMemory`.

## Project Structure & Module Organization

Three projects in `ImperialismIITrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references (pulling both `GameTrainers.Common.Memory` and
`GameTrainers.Common.Mvvm` via csproj `<Using>` items — note their `ObservableObject` uses `SetField`).

- `src/ImperialismIITrainer/` — the WPF app (`AssemblyName` `Imp2Trainer`, `RootNamespace`
  `ImperialismIITrainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies.
    - `NationLayout.cs` — the recovered layout facts: the static globals that hold a pointer to the human
      player's nation object (`0x760650`/`0x7606A8`), the field offsets (treasury `int32` at `+0x130`,
      warehouse `int16` array at `+0xDD4`), the ten confirmed commodity slots, the module `.rdata`/`.data`
      ranges, and the pure validation helpers (`LooksLikeVtable`/`IsPlausibleTreasury`/`ValidateHeader`)
      that `FormatCheck` exercises against a synthetic header. All build-specific — recovered by live RE.
    - `GameLocator.cs` — follows a static global to the nation object and validates it structurally
      (vtable in `.rdata` + plausible treasury + non-negative warehouse); returns a `NationLocation` or
      null. This is the auto-locate engine; it works because the exe has a fixed image base and no ASLR.
    - `CommodityBook.cs` — the 28 Age-of-Exploration commodities (8 raw + 6 refined + 3 food + 6 luxury +
      5 riches) from the game manual's "Possible Commodities to Transport", confirmed against the live
      warehouse. Do **not** re-source it from the exe's internal name table (Cotton/Coal/Steel/Oil…): that
      is the *industrial* set and is wrong for Age of Exploration.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` owns two tab VMs. `LiveScannerViewModel` drives
    both paths: `AutoLocateCommand` runs `GameLocator` and pins the treasury + warehouse straight to the
    Freezes table (no scan), and the value scanner (attach/scan/narrow/pin/freeze) with three guided scans
    — Treasury (Int32), Resource (Int16, labelled by the selected commodity), Labour (Int16) — is the
    fallback. Its `TargetHints` sort the `Imperialism II` process to the top of the picker (cosmetic).
    `ReferenceViewModel` exposes the commodity table, the game's built-in cheat/script surface (symbol
    names from the map), and how-to notes. The reusable scanner rows (`IScanHost`, `ScanValue`,
    `ScanResultViewModel`, `FrozenValueViewModel`) match the repo's other value-scanner trainers.
    `ProcessMemory`/`MemorySearcher` come from `GameTrainers.Common.Memory`, not a local copy.
- `test/FormatCheck/` — headless verification harness (console `Exe`, `net8.0-windows` + `UseWPF`
  because it references the WPF app for the view-model types), not the app.

It **has a `GameLocator`** (unusual for a value-scanner-style trainer) but **no save editor**. The
shipped exe is a later build (June 1999) than the bundled `Imperialism II.map` (January 1999), so the
map's *addresses* are stale and RTTI is stripped for the game's classes — but the exe is a native,
no-ASLR image, so a one-time live pointer scan recovered a stable June-build anchor (static global →
nation object; fixed field offsets). The locator uses it and falls back to `MemorySearcher` (used exactly
like the Colonization/Darklands/Perfect-General trainers) when validation fails. The `.imp` save has no
matching map, so there is nothing to parse offline. RE write-up lives in the committed `docs/`; game
assets stay in git-ignored dot-prefixed dirs and are never committed.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration Debug|Release`,
  `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\ImperialismIITrainer\ImperialismIITrainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. File-scoped
namespaces (`namespace ImperialismIITrainer.ViewModels;`), XML `<summary>` docs on public types/members,
`sealed` classes by default, `const` hex for offsets, and `// --- section ---` divider comments. No
linter/formatter config is committed; match the surrounding file. Follow the read-validate-write pattern
(a frozen row rejects a value that doesn't fit its captured width before poking RAM) so a mis-typed or
mis-scanned value can't corrupt a neighbouring field.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` needs no live game: it asserts the `CommodityBook` counts
and order, the `NationLayout` offsets + validation logic (`ValidateHeader` against a synthetic nation
header, plus the `LooksLikeVtable`/`IsPlausibleTreasury`/`LooksLikeHeapPointer` predicates), and the pure
value-scanner helpers (`ScanValue.TryParse/FitsWidth/Canonicalize`, and `FrozenValueViewModel`
poke/freeze/width-guard driven through a fake `IScanHost`). It runs `Check(...)` assertions and returns
exit code 0 (pass) or 1 (fail). Keep it green. What can't be headless — the GUI, the native-process value
scan, and the live `GameLocator.Locate()` (attach → follow the static global → validate) — was validated
by hand against the running game (the locator resolved treasury + all ten warehouse slots to the correct
addresses).

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon. Describe
which game value a change reads/writes (treasury Int32, warehouse/labour Int16) and how it was confirmed
against the live game or the map. No PR template exists.

## Domain Notes

The map (`Imperialism II.map`) is the Rosetta Stone even though its addresses are stale: playable powers
are `TGreatPower : TCountry`; treasury is a signed 32-bit `long` (`GetAvailableTreasury`/`AddToTreasury`,
goes negative in debt); a warehouse stockpile is a signed 16-bit `short` per commodity
(`GetStockpile(short)`). The game carries its own cheats (`settreasury`, `ShowMeTheMoney`, a Nova Console
with `ScSetTreasury`/`ScSetWarehouse`/`ScSetLabor` gated by `gCheatingIsEnabled`) — documented in the
References tab for context, **not** invoked by the trainer. The one-click locator that used to be a
"future upgrade" is now built (`GameLocator`/`NationLayout`): a live pointer scan recovered the
June-build static global (`0x760650`) that points to the player nation, and the treasury (`+0x130`) and
warehouse (`+0xDD4`) offsets within it. The full recovery — build drift, the pointer scan, the object
layout — is written up in `docs/Imperialism2-Reverse-Engineering.md`.
