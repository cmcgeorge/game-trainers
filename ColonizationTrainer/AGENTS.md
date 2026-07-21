# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) trainer for the 1994 DOS strategy game *Sid Meier's Colonization*
(MicroProse — the original "Col1", `VICEROY.EXE`). Unlike the repo's live-memory trainers, its
primary feature is an **offline save-game editor** for the `COLONYxx.SAV` files; a secondary tab is a
Cheat-Engine-style live value scanner. Windows-only (WPF + Win32 memory APIs); the app manifest
requests administrator rights so the live tab can `Read/WriteProcessMemory` on the emulator.

## Project Structure & Module Organization

Three projects in `ColonizationTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references (pulling both `GameTrainers.Common.Memory` and
`GameTrainers.Common.Mvvm` via csproj `<Using>` items — note their `ObservableObject` uses `SetField`).

- `src/ColonizationTrainer/` — the WPF app (`AssemblyName` `ColTrainer`, `RootNamespace`
  `ColonizationTrainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. `SaveFormat.cs` is the single source of
    truth for the reverse-engineered byte layout: all header/nation/colony offsets and record sizes
    as `const`, plus the `NationBase`/`ColonyBase` offset formulas (the file is **not** a fixed
    layout — sections after the 390-byte header are located from the header's record counts, so never
    hard-code an absolute offset past the header). `Bytes.cs` is the bounds-checked little-endian
    accessor every field read/write goes through (it throws rather than reading past the buffer).
    `ColonyText.cs` is the NUL-terminated ASCII name codec. `SaveGame.cs` loads/validates a save
    (checks the `"COLONIZE"` signature and that the header counts don't point past EOF), exposes typed
    views (`SaveHeader` fields inline, `NationRecord[4]`, `ColonyRecord` list) over the decoded buffer,
    and on `Save` writes the buffer back **in place** (there is no checksum) after a one-time `.bak`.
    `NationRecord`/`ColonyRecord` are typed mutable views that clamp on write (gold to a positive
    32-bit range, tax to 0..99, stock to signed-16-bit). The reference books (`CargoBook` 16 goods,
    `UnitBook` 24 units, `TerrainBook` 21 terrain rows + yields, `ProfessionBook` 28, `BuildingBook`
    42, `FoundingFatherBook` 25 fathers in bitfield order, `NationBook` 4 nations + 5 difficulties)
    are the game's own enumerations, recovered from `.games/PEDIA.TXT` and the viceroy struct;
    `Walkthrough.cs` is the condensed strategy digest. **Keep the good/father/unit index orders exactly
    as the game uses them** — the colony `stock` array and the nation founding-fathers bitfield depend
    on them, and `FormatCheck` asserts the counts and orders.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` owns three independent tab VMs.
    `SaveEditorViewModel` is the verified path (open/save, a nation selector defaulting to the human
    player, gold/tax/bells/crosses/boycotts/Founding-Father edits, and a colony list); it auto-opens
    the newest `COLONY*.SAV` from a `.games`/`.game` folder found beside the exe. `ColonyEditorViewModel`
    + `GoodRowViewModel` + `FoundingFatherRowViewModel` are its row VMs. `LiveScannerViewModel` is a
    self-contained value scanner (attach/scan/narrow/pin/freeze with Gold/Tax/Bells guided scans),
    reusing the scanner infrastructure (`IScanHost`, `ScanValue`, `ScanResultViewModel`,
    `FrozenValueViewModel`) ported from the Darklands/Perfect-General value-scanner trainers.
    `ReferenceViewModel` exposes the reference books. `ProcessMemory`/`MemorySearcher` come from
    `GameTrainers.Common.Memory` (imported via csproj `<Using>`), not a local copy.
- `test/FormatCheck/` — headless verification harness (console `Exe`, `net8.0-windows` + `UseWPF`
  because it references the WPF app for the view-model types), not the app.

Ground-truth material: the game and its `COLONYxx.SAV` saves live in `.games/`; teardown/strategy
notes are in `.docs/` (the original proposal) and the committed `docs/`. Dot-prefixed dirs
(`.games/`, `.docs/`) are git-ignored — never commit the copyrighted game or saves.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration
  Debug|Release`, `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\ColonizationTrainer\ColonizationTrainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use
file-scoped namespaces (`namespace ColonizationTrainer.Game;`), XML `<summary>` docs on public
types/members, `sealed` classes by default, `const` hex for offsets, and `// --- section ---`
divider comments. No linter/formatter config is committed; match the surrounding file. Keep every
reverse-engineered constant in `Game/SaveFormat.cs` and follow the read-validate-write pattern (each
setter clamps, then pokes only its own field range through `Bytes`) so a shifted or
partially-understood layout is never corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` builds a **synthetic** COLONIZE save (no copyrighted
file needed), round-trips it byte-for-byte, exercises every edit and its clamps, checks the offset
arithmetic and reference-book counts/orders, and — only if the copyrighted `.games\COLONY00.SAV`
happens to be present — asserts the empirically-confirmed real values (map 58×72, year 1492, England,
0 gold/0% tax, 77 units, 65 native dwellings). It runs `Check(...)` assertions and returns exit code
0 (pass) or 1 (fail). Any format/parser change must keep the synthetic round-trip byte-identical and
the assertions green.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon.
Describe which save field a change reads/writes and how it was confirmed against the shipped saves or
the community format sources. No PR template exists.

## Domain Notes

The save is a flat little-endian serialization, signature `"COLONIZE"`, **no checksum** — edits are
pure in-place byte writes. The 24,537-byte size of the shipped saves is **not** a format constant; it
is that particular new game (0 colonies, 77 units). The header (offset 0, 390 bytes = 158 HEAD + 4×52
PLAYER + 24 OTHER) carries `colony_count` (`0x2E`), `unit_count` (`0x2C`), `human_player` (`0x28`),
map size (`0x0C`/`0x0E`), `year` (`0x1A`), `turn` (`0x1E`), `difficulty` (`0x36`). Colonies begin at
`0x186` (202 bytes each), then units (28 each), then the four 316-byte nation records
(England/France/Spain/Netherlands). **Gold** is a 4-byte value at nation offset `0x2A`, **tax** a byte
at `0x01`, the **Founding-Fathers** acquired bitfield a u32 at `0x07` (25 named bits, bit 18 dead).
Gold/tax are the fully-verified edits; the Founding-Father and liberty-bell edits are documented but
recompute derived state in-game (the editor also bumps `founding_father_count` to stay consistent).
The live tab uses a value scanner rather than a fixed locator because `VICEROY.EXE` is a large overlaid
real-mode program whose guest-RAM state has no stable session-independent signature — the Save Editor
is the reliable path. Full teardown in `docs/Colonization-Reverse-Engineering.md`.
