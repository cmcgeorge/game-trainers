# Repository Guidelines

A collection of independent, Windows-only C#/WPF **live-memory trainers** for classic DOS games running under **DOSBox / DOSBox-X**. Each trainer attaches to the running emulator process, signature-scans the emulated guest RAM to locate game state at runtime (addresses are never hard-coded), and reads/writes it live — with freeze toggles, "max" buttons, and, for some titles, offline save editing. Several also carry a reverse-engineering workspace behind their offsets.

## Project Structure & Module Organization

Every top-level folder is a **self-contained trainer** with its own solution/project, `README.md`, a PowerShell run script (usually `Run.ps1`), and (mostly) its own `AGENTS.md` — read the per-project `AGENTS.md`/`README.md` before working inside one:

- `AutoduelTrainer/`, `BardsTale1Trainer/`, `KeefTrainer/`, `LordsOfTheRealmTrainer/`, `MightAndMagic1Trainer/`, `PoolOfRadianceTrainer/`, `ShogunTrainer/`, `SwordOfTheSamuraiTrainer/`, `SyndicatePlusTrainer/`.

Layout varies: some use a single `Trainer/` project (no `.sln`), others `src/<Name>/` with a `.sln` and a `test/FormatCheck/` harness. `MightAndMagic1Trainer` is the architectural template the others were ported from. Common layers: Win32 P/Invoke (`Native*.cs`), process/guest-memory access, a game-knowledge layer holding all reverse-engineered offsets as constants, and hand-rolled MVVM (`ObservableObject`/`RelayCommand`).

Dot-prefixed dirs (`.docs/` teardown & strategy notes, `.data/` RAM dumps, `.game/` copyrighted assets) are **git-ignored** (`.gitignore` `.*/`) — never commit them.

## Build, Test, and Development Commands

Conventions vary per trainer — always check the local `README.md`. Run from inside a trainer's own folder:

- `.\Run.ps1` — most trainers: restore, build Release, launch (a UAC prompt appears; the app manifest requests admin for `Read/WriteProcessMemory`). Exceptions: `KeefTrainer` has no script (use `dotnet run --project KeefTrainer`); `SwordOfTheSamuraiTrainer` uses `.\Run-SotsTrainer.ps1`.
- Flags differ per script — most take `-Configuration Debug|Release` and `-Clean`. Build-without-launch is `-NoRun` on most, but `-NoBuild` on `LordsOfTheRealmTrainer` and `SwordOfTheSamuraiTrainer`. `-Test` (run the verification harness, no GUI) exists only on `BardsTale1Trainer`, `MightAndMagic1Trainer`, and `PoolOfRadianceTrainer`; `ShogunTrainer` has `-Publish` instead.
- Direct: `dotnet build <project>.csproj -c Release` / `dotnet run --project <test-project>`.

## Coding Style & Naming Conventions

C# targeting `net8.0-windows` or `net9.0-windows`, **x64**, with `Nullable` and `ImplicitUsings` enabled. No `.editorconfig`, linter, or formatter is committed — match the surrounding file: 4-space indent, file-scoped namespaces, `sealed` classes, PascalCase members, `_camelCase` fields, `const` hex for offsets, and XML `///` docs on public members. Keep reverse-engineered constants in the game layer; follow the read-validate-write pattern so a shifted layout is never corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. Trainers that have one use a headless console harness — usually `FormatCheck` (`net8.0` or `net8.0-windows`), or `Verify` in `SwordOfTheSamuraiTrainer` — that asserts parsers against captured dumps/save files and exits 0 (pass) or 1 (fail). The GUI cannot be smoke-tested headlessly — it needs an interactive desktop and a running game. Keep the harness green.

## Commit & Pull Request Guidelines

The root repository has no commit history yet. Per the sibling projects, write imperative, sentence-case subjects (join related changes with a semicolon); describe which game state a change reads/writes and how it was confirmed against the live game.
