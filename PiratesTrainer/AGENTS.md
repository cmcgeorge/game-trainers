# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for **Sid Meier's Pirates!** (MicroProse, 1987 —
IBM version 432.02) running under **DOSBox / DOSBox-X**. Like the repo's other DOS trainers it attaches
to the **emulator** process and reads the DOS guest's RAM mapped inside it; it is not a native-Windows
target like `ImperialismIITrainer` or `BeachHead2000Trainer`. Windows-only (WPF + Win32 memory APIs); the
app manifest requests administrator rights so it can `Read/WriteProcessMemory`.

## The target, and why it is unusual

The shipped distribution is a **DOS conversion of the original self-booting release**, and understanding
the three files matters before touching anything:

- `PIR.EXE` (1,983 bytes) is a shim, not the game. It opens `DISK1` / `DISK2` / `DISKS` as ordinary
  files, hooks **INT 80h** (sector read/write, an `INT 13h` work-alike), **INT 81h** (select disk) and
  **INT 82h** (keyboard poll — scancode `0x44` = **F10** quits to DOS), then EXECs `DISKP`.
- `DISKP` (163,952 bytes) is the game: a plain MZ image whose first 32 bytes are a relocated segment
  table. `DGROUP` is image paragraph `0x1124`, so **every global sits at a constant DGROUP offset** and
  a string-anchored locator works. `dgroupOffset = fileOffset − 0x112B0`.
- `DISK1` / `DISK2` are raw 360 KB floppy images holding the data tables; `DISKS` is the save "disk"
  (4 sectors per track, currently blank).

Because the loader serves every sector from a file, the original **disk-based copy protection cannot
fire**, and the manual's date-lookup question is **not in this build at all** — the complete 589-record
display-string table was decoded and contains no question text. The convoy schedule is shipped anyway
(`FleetSchedule.cs`), because the same tables drive where the treasure convoys sail.

## Project Structure & Module Organization

Three projects in `PiratesTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references (pulling both `GameTrainers.Common.Memory` and
`GameTrainers.Common.Mvvm` via csproj `<Using>` items — note their `ObservableObject` uses `SetField`).

- `src/PiratesTrainer/` — the WPF app (`AssemblyName` and `RootNamespace` both `PiratesTrainer`),
  layered by concern:
  - `Game/` — the game-knowledge layer, no UI dependencies. `PiratesLayout`, `GameFacts`, `CityBook`
    and `FleetSchedule` are pure data; `GameLocator` reads the attached process via `ProcessMemory`.
    - `PiratesLayout.cs` — the recovered layout facts: **gold is an unsigned `int16` at `DGROUP:0x4847`**
      (saturating at 65,535, fixed by the game's own add/spend pair), wealth (`0x4742`, ×10 gold), land
      (`0x4745`, ×50 acres), the calendar (`0x9A9F` day-of-year / `0x9A9D` years / `0x9A2B` month, a flat
      360-day year), the **era codes 0, 2, 3, 4, 5, 6** (not 0–5 — that is what makes
      `1560 + 20 × code` produce the six offered years), the settlement table at `0x4240` in 24-byte
      records, the 1,940-byte save block at `0x4130`, the three DGROUP string anchors, the convoy slot
      arithmetic, and the pure validators (`ValidateSegment`, `LooksLikeCityRecord`, `ConvoySlot`,
      `MonthForSlot`) that `FormatCheck` exercises. Every constant carries a `[Confirmed]` or
      `[Inferred]` marker in its doc comment; `KnownValues` surfaces the same distinction in the UI.
    - `GameLocator.cs` — sweeps the DOSBox host for the copyright literal, treats each hit as a candidate
      `dgroupBase = hit − 0x0183`, and accepts it only if **all three** anchors sit at their offsets, the
      era code and year decode sanely, **and** the settlement table parses. The table check is the
      important one: it is loaded from disk at run time, so a buffered copy of the program image cannot
      fake it. Also reads the live settlement list and the player's family name.
    - `CityBook.cs` / `FleetSchedule.cs` — **generated** from `DISK1` (six 1,024-byte era blocks at file
      offset `0x54000 + 0x400 × era`). Do not hand-edit; regenerate and re-run `FormatCheck`, which
      cross-checks every convoy stop against the era's own settlement table and against the slot
      arithmetic in `PiratesLayout`.
    - `GameFacts.cs` — the non-address tables read out of `DISKP`'s string table: hulls, cargo, ranks,
      specialities, difficulties, expeditions, morale bands, rivals, controls.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` owns two tab VMs. `LiveScannerViewModel` drives
    both paths: `AutoLocateCommand` runs `GameLocator` and pins every `PiratesLayout.KnownValues` entry
    plus the live settlement grid (no scan), `MaxGoldCommand` locates-then-sets-and-freezes 65,535, and
    the value scanner (attach/scan/narrow/pin/freeze) with **Gold**, **Crew** and **Any value** guides is
    the build-independent fallback. It publishes a `GameSummary` line (captain, date, era, gold, town
    count) so the user can verify the locate before poking. Auto-locate runs on a background thread.
    `ReferenceViewModel` exposes the era-filtered settlement and convoy grids plus how-to notes. The
    reusable scanner rows (`IScanHost`, `ScanValue`, `ScanResultViewModel`, `FrozenValueViewModel`) match
    the repo's other value-scanner trainers.
- `test/FormatCheck/` — headless verification harness (console `Exe`, `net8.0-windows` + `UseWPF`
  because it references the WPF app for the view-model types), not the app.

It **has a `GameLocator`** (a string-anchored DGROUP locator, in the spirit of the MM1-family and
Railroad Tycoon trainers) but **no save editor**: the shipped `DISKS` is an unformatted blank, so the
on-disk slot directory could not be validated against a real save. Live memory is the verifiable path.
RE write-up and strategy guide live in the committed `docs/`; game assets stay in git-ignored
dot-prefixed dirs and are never committed.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration Debug|Release`,
  `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\PiratesTrainer\PiratesTrainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. File-scoped
namespaces (`namespace PiratesTrainer.ViewModels;`), XML `<summary>` docs on public types/members,
`sealed` classes by default, `const` hex for offsets, and `// --- section ---` divider comments. No
linter/formatter config is committed; match the surrounding file. Follow the read-validate-write pattern
(a frozen row rejects a value that doesn't fit its captured width before poking RAM) so a mis-typed or
mis-scanned value can't corrupt a neighbouring field. Keep every reverse-engineered constant in
`Game/`, and keep its `[Confirmed]` / `[Inferred]` marker honest — the whole teardown was static, so the
distinction is the only thing protecting a user from a plausible-but-wrong poke.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` needs no live game and covers: the generated settlement
tables (counts, dense indices, spot-checked field values, geography sanity, every prosperity band
known), the convoy itineraries (slots in range, strictly increasing, every stop naming a settlement of
its own era, spot-checks against the shipped 1987 chart, and **every stop's month re-derived from
`PiratesLayout`'s slot arithmetic**), the layout facts and three-anchor `ValidateSegment` against a
synthetic DGROUP window, `LooksLikeCityRecord` against synthetic records *and* against every shipped
settlement name, the era-code ↔ index mapping, the calendar arithmetic, the `ReferenceViewModel`
era/filter logic, and the pure value-scanner helpers plus `FrozenValueViewModel` poke/freeze/width-guard
through a fake `IScanHost`. It runs `Check(...)` assertions and returns exit code 0 (pass) or 1 (fail).
Keep it green. What cannot be headless — the GUI and `GameLocator.Locate()` against a running game — is
**unverified**: no live run has been performed. That is stated plainly in the README and the RE doc, and
it is why the locator validates three anchors plus the settlement table and shows the user a summary
line to check before they poke anything.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon. Describe
which game value a change reads/writes (gold unsigned Int16 at `DGROUP:0x4847`, wealth in tens of gold)
and how it was established — static routine, live test, or inference. No PR template exists.

## Domain Notes

Gold is the exact number of gold pieces the party panel shows, in an **unsigned** 16-bit word that the
game's add-gold routine **saturates** at 65,535 rather than wrapping — so that is the true ceiling and
what "Max gold" targets. Wealth (`0x4742`) is what actually scores at retirement and is stored in tens of
gold; land (`0x4745`) is in units of 50 acres and pays half its value into wealth every month. Freezing
**day-of-year** stops the calendar; freezing the month global does nothing useful because the tick
recomputes it as day ÷ 30. Changing the era code desynchronises the settlement table loaded for the old
era. The convoy phase is a term in the game's own arithmetic (`slot = day/15 − bias + 2 × (era & 1)`,
bias 18 for the Treasure Fleet and 6 for the Silver Train), which is why 1620 and 1660 — the odd-coded
eras — run a month earlier than the other four. The full recovery is written up in
`docs/Pirates-ReverseEngineering.md`; the play/strategy guide, maps and the complete convoy schedule are
in `docs/Pirates-StrategyGuide.md`.
