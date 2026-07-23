# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for **Sid Meier's Railroad Tycoon** (MicroProse,
1990) — the original DOS game (`GAME.EXE` v455.00), running under **DOSBox / DOSBox-X**. Like the repo's
other DOS trainers, it attaches to the **emulator** process and reads the DOS guest's RAM mapped inside
it; it is **not** a native-Windows target like `ImperialismIITrainer`. Windows-only (WPF + Win32 memory
APIs); the app manifest requests administrator rights so it can `Read/WriteProcessMemory`.

## Project Structure & Module Organization

Three projects in `RailroadTycoonTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references (pulling both `GameTrainers.Common.Memory` and
`GameTrainers.Common.Mvvm` via csproj `<Using>` items — note their `ObservableObject` uses `SetField`).

- `src/RailroadTycoonTrainer/` — the WPF app (`AssemblyName` `RRTTrainer`, `RootNamespace`
  `RailroadTycoonTrainer`), layered by concern:
  - `Game/` — the game-knowledge layer, no UI dependencies. `RtLayout` and the reference books are pure
    data; `GameLocator` reads the attached process (via `ProcessMemory`) to resolve addresses.
    - `RtLayout.cs` — the recovered layout facts: **cash is a signed `int16` at `DGROUP:0x957A` in units
      of $1,000** (live write-test confirmed), the **year `uint16` at `DGROUP:0x96C0`**, the two DGROUP
      string anchors the locator keys on (`"Outstanding Loans: "` @ `0x24A8`, `"Stockholders Equity: "`
      @ `0x24BC`), the $30M ($30000) accounting cap, the pure validators (`ValidateSegment`,
      `IsPlausibleYear`) that `FormatCheck` exercises, and the thousands↔dollars conversions. All
      build-specific — recovered by live RE under DOSBox-X plus a static Ghidra teardown.
    - `GameLocator.cs` — scans the DOSBox host for the anchor string, treats each hit as a candidate
      `dgroupBase = hit − 0x24A8`, and accepts it only if the second string sits at its offset **and**
      the year word is plausible; then reads cash/year. Returns a `GameLocation` or null. This is the
      auto-locate engine; the DS *segment* varies per launch but DGROUP-relative offsets are constant, so
      the arithmetic cancels the load address out.
    - `LocomotiveBook.cs` / `GameFacts.cs` — reference tables: the three engine rosters (which double as
      the copy-protection quiz answer key — note the buy-list keeps `2-6-6-2 Mallet` / `'F' Series
      Diesel` while the quiz shows the aliases `Challenger` / `F3A-Series`), plus stations, improvements,
      scenarios, difficulty levels and reality switches.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` owns two tab VMs. `LiveScannerViewModel` drives
    both paths: `AutoLocateCommand` runs `GameLocator` and pins **Cash** + **Year** to the Freezes table
    (no scan), `MaxCashCommand` locates-then-sets-and-freezes $30M, and the value scanner
    (attach/scan/narrow/pin/freeze) with a **Cash** guide (Int16, dollars ÷ 1000) and an **Any value**
    guide is the build-independent fallback. Its `TargetHints` sort **dosbox** processes to the top of
    the picker. Auto-locate runs on a background thread (it scans the whole emulator address space).
    `ReferenceViewModel` exposes the locomotive/station/scenario tables and how-to notes. The reusable
    scanner rows (`IScanHost`, `ScanValue`, `ScanResultViewModel`, `FrozenValueViewModel`) match the
    repo's other value-scanner trainers. `ProcessMemory`/`MemorySearcher` come from
    `GameTrainers.Common.Memory`, not a local copy.
- `test/FormatCheck/` — headless verification harness (console `Exe`, `net8.0-windows` + `UseWPF`
  because it references the WPF app for the view-model types), not the app.

It **has a `GameLocator`** (a string-anchored DGROUP locator, in the spirit of the MM1-family trainers)
but **no save editor**. Railroad Tycoon's `.SVE` is a variable-length, count-keyed multi-region
serialization (overlay #39) in which **cash is not a discrete field** — it lives in the bulk state block
— so there is nothing safe to edit offline; live memory is the verifiable path. The locator falls back
to `MemorySearcher` (used exactly like the Colonization/Darklands/Perfect-General trainers) when
validation fails — e.g. a different `GAME.EXE` build that shifts the cash offset (a third-party disasm of
another build reported `0x95AA`, ours is `0x957A`). RE write-up lives in the committed `docs/`; game
assets stay in git-ignored dot-prefixed dirs and are never committed.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration Debug|Release`,
  `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\RailroadTycoonTrainer\RailroadTycoonTrainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. File-scoped
namespaces (`namespace RailroadTycoonTrainer.ViewModels;`), XML `<summary>` docs on public types/members,
`sealed` classes by default, `const` hex for offsets, and `// --- section ---` divider comments. No
linter/formatter config is committed; match the surrounding file. Follow the read-validate-write pattern
(a frozen row rejects a value that doesn't fit its captured width before poking RAM) so a mis-typed or
mis-scanned value can't corrupt a neighbouring field.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` needs no live game: it asserts the locomotive/station/
scenario reference tables, the `RtLayout` offsets + segment validation (`ValidateSegment` against a
synthetic DGROUP window, `IsPlausibleYear`, the conversions), and the pure value-scanner helpers
(`ScanValue.TryParse/FitsWidth/Canonicalize`, and `FrozenValueViewModel` poke/freeze/width-guard driven
through a fake `IScanHost`). It runs `Check(...)` assertions and returns exit code 0 (pass) or 1 (fail).
Keep it green. What can't be headless — the GUI, the value scan, and the live `GameLocator.Locate()`
(scan → anchor → validate → read) — was validated by hand against the running game: the locator resolved
cash and year to the correct addresses in ~0.2 s and a round-trip write succeeded.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon. Describe
which game value a change reads/writes (cash signed Int16 in $1,000s, year uint16) and how it was
confirmed against the live game. No PR template exists.

## Domain Notes

Cash is stored **in thousands of dollars** — a screen reading of "$1,000,000" is the word `1000` — and
the game clamps it to `0x7530` (30000 = $30M) during accounting, so freeze at or below that. The year
global (`0x96C0`) gates the engine/industry eras (1800/1830/1865); freezing it stops the retirement
clock. The startup **locomotive-ID quiz** (overlay #34) is still active in this build — answer it to
reach a game; the **Locomotives** tab is the answer key. The full recovery — EXEPACK unpack, the
DGROUP proof (`DS = 0x164B` from the CRT startup), the live cash write-test, the `.SVE` overlay-#39
serialization, and why there's no save editor — is written up in
`docs/RailroadTycoon-ReverseEngineering.md`; the strategy guide (controls, mechanics, how to win) is in
`docs/RailroadTycoon-StrategyGuide.md`.
