# Repository Guidelines

This repo pairs reverse-engineering documentation for **AUTODUEL** (Origin Systems, 1985) with a C#/WPF trainer that live-edits the game while it runs under DOSBox-X.

## Project Structure & Module Organization
- `Trainer/` — the .NET 9 WPF app (single project, no `.sln`). UI in `MainWindow.xaml` + `MainViewModel.cs`; hand-rolled MVVM base types (`ViewModelBase`, `RelayCommand`) in `Mvvm.cs`.
- `Trainer/Memory/` — the engine core: `NativeMethods.cs` (P/Invoke), `ProcessMemory.cs` (handle + region enumeration), `GameData.cs` (every reverse-engineered offset and the base-100 codec), `Models.cs`, `TrainerEngine.cs` (attach, signature scan, read/write), `GameInput.cs` (drives DOSBox's quit→reload keystrokes).
- `.docs/` — `reverse-engineering.md` (memory layout; the source of truth behind `GameData.cs`), `strategy-guide.md`, `Proposal.md`.
- `game/` — original game files; `data/` — DOSBox memory dumps (`*.bin`/`*.csv`) plus `memdump.md`.

## Build, Test, and Development Commands
- `.\Run.ps1` — restore, build Release, and launch (`-Configuration Debug`, `-NoRun`, `-Clean` are supported).
- `dotnet build Trainer/AutoduelTrainer.csproj -c Release` — build only.
- `dotnet run --project Trainer/AutoduelTrainer.csproj -c Release` — build and run via the SDK.
- No automated test project exists; verify changes against a live DOSBox-X + AUTODUEL session (load a driver past the title screen, then Attach).

## Coding Style & Naming Conventions
- C# with `Nullable` and `ImplicitUsings` enabled; targets `net9.0-windows`, x64 only (`AutoduelTrainer.csproj`).
- No `.editorconfig`, linter, or formatter is configured — match the surrounding style: 4-space indent, file-scoped namespaces (`AutoduelTrainer`, `AutoduelTrainer.Memory`), `sealed` classes, and XML `///` doc comments on public members.
- Keep every reverse-engineered constant in `GameData.cs`; never scatter raw offsets elsewhere. Clamp all `WriteProcessMemory` writes to legal ranges.

## Commit & Pull Request Guidelines
- Commit subjects are imperative, sentence-style, and end with a period: `Add freeze toggles, car-stat editing, teleport, and readable dropdowns.` Keep to one concern per commit.
- There is no PR template; describe which game state a change reads/writes and how it was confirmed against the live game.
