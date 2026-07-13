# Repository Guidelines

A collection of independent, Windows-only C#/WPF **live-memory trainers** for classic DOS games running under **DOSBox / DOSBox-X**. Each trainer attaches to the running emulator process, signature-scans the emulated guest RAM to locate game state at runtime (addresses are never hard-coded), and reads/writes it live — with freeze toggles, "max" buttons, and, for some titles, offline save editing. Several also carry a reverse-engineering workspace behind their offsets.

## Project Structure & Module Organization

Every top-level folder is a **self-contained trainer** with its own solution/project, `README.md`, a PowerShell run script (usually `Run.ps1`), and (mostly) its own `AGENTS.md` — read the per-project `AGENTS.md`/`README.md` before working inside one:

- `AutoduelTrainer/`, `BardsTale1Trainer/`, `DragonWarsTrainer/`, `KeefTrainer/`, `LordsOfTheRealmTrainer/`, `MightAndMagic1Trainer/`, `MinesOfTitanTrainer/`, `PoolOfRadianceTrainer/`, `ShogunTrainer/`, `SwordOfTheSamuraiTrainer/`, `SyndicatePlusTrainer/`, `ThePerfectGeneral2Trainer/`, `WarOfTheLanceTrainer/`.

Layout varies: some use a single `Trainer/` project (no `.sln`), others `src/<Name>/` with a `.sln` and a `test/FormatCheck/` harness. `MightAndMagic1Trainer` is the architectural template the others were ported from. Common layers: Win32 P/Invoke (`Native*.cs`), process/guest-memory access, a game-knowledge layer holding all reverse-engineered offsets as constants, and hand-rolled MVVM (`ObservableObject`/`RelayCommand`).

`GameTrainers.Common/` is a shared library that extracts the game-agnostic plumbing out of that duplication: `GameTrainers.Common.Memory` (process/guest-memory access — `ProcessMemory`/`MemoryRegion`, `NativeMethods`, `MemorySearcher`, `MemoryDumper`, `DumpComparer`, `BytePatternScanner`, `GlobalHotkeys`, `KeyboardSender`) and `GameTrainers.Common.Mvvm` (`ObservableObject`/`RelayCommand`). The three MM1-family trainers (`MightAndMagic1Trainer`, `BardsTale1Trainer`, `PoolOfRadianceTrainer`) reference it instead of carrying their own copies; each keeps only its game-specific locators/scanners locally. `DragonWarsTrainer`, `MinesOfTitanTrainer`, and `WarOfTheLanceTrainer` also reference it, pulling both `GameTrainers.Common.Memory` and `GameTrainers.Common.Mvvm` (via csproj `<Using>` items — note their `ObservableObject` uses `SetField`) and keeping only their own game-specific locators local (DragonWars its anchor-based `RosterLocator`, Mines of Titan its `IJKM`-anchored `PartyLocator`, War of the Lance its NAT.DAT + strength-block-signature `GameLocator`). `ThePerfectGeneral2Trainer` also references it, but is the odd one out: its target is a 16-bit DOS **protected-mode (DPMI)** program whose run-time heap has no stable static signature to anchor a locator to, so it has **no `GameLocator`** — instead it drives Common's `MemorySearcher` as a Cheat-Engine-style value scanner (attach → scan → narrow → pin/freeze) and keeps only its two Confirmed data structures (`PurchaseFormat`, `UnitReference`) local. `PoolOfRadianceTrainer` intentionally kept its own divergent MVVM, `NativeMethods` and value-scanner and pulls only `ProcessMemory`/`MemoryRegion` from Common (via csproj using-aliases). The remaining trainers are still self-contained. So a fix to the shared plumbing lands once for all of them; a fix in one trainer's local copy of the old code still needs porting.

Dot-prefixed dirs (`.docs/` teardown & strategy notes, `.data/` RAM dumps, `.game/` copyrighted assets) are **git-ignored** (`.gitignore` `.*/`) — never commit them.

## Build, Test, and Development Commands

- Root launcher: from the repo root run `.\Run.ps1` for an interactive menu of every trainer, or `.\Run.ps1 -Trainer <name|number>` to pick one directly (`-List` prints the menu and exits). It discovers any top-level folder that contains its own `Run.ps1` and forwards the shared options below to it.
- Per-trainer: `.\Run.ps1` inside each trainer folder (including `KeefTrainer`, which now has one). Restores, builds Release, and launches; a UAC prompt appears because the app manifests request admin for `Read/WriteProcessMemory`.
- Every `Run.ps1` (root and per-trainer) exposes the **same** options: `-Configuration Debug|Release` (default Release), `-Clean` (remove bin/obj first), `-NoBuild` (skip build, launch the existing exe), `-NoRun` (build only), `-Test` (run the verification harness; warns if the trainer has none), and `-Publish` (single self-contained win-x64 exe, skips launch).
- Direct: `dotnet build <project>.csproj -c Release` / `dotnet run --project <test-project>`.

## Coding Style & Naming Conventions

C# targeting `net8.0-windows` or `net9.0-windows`, **x64**, with `Nullable` and `ImplicitUsings` enabled. No `.editorconfig`, linter, or formatter is committed — match the surrounding file: 4-space indent, file-scoped namespaces, `sealed` classes, PascalCase members, `_camelCase` fields, `const` hex for offsets, and XML `///` docs on public members. Keep reverse-engineered constants in the game layer; follow the read-validate-write pattern so a shifted layout is never corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. Trainers that have one use a headless console harness — usually `FormatCheck` (`net8.0` or `net8.0-windows`), or `Verify` in `SwordOfTheSamuraiTrainer` — that asserts parsers against captured dumps/save files and exits 0 (pass) or 1 (fail). The GUI cannot be smoke-tested headlessly — it needs an interactive desktop and a running game. Keep the harness green.

## Commit & Pull Request Guidelines

The root repository has no commit history yet. Per the sibling projects, write imperative, sentence-case subjects (join related changes with a semicolon); describe which game state a change reads/writes and how it was confirmed against the live game.
