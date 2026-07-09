# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer and offline save editor for the 1988 DOS game *Pool of Radiance*, running under DOSBox. Windows-only (WPF + Win32 memory APIs); the app manifest requests administrator rights.

## Project Structure & Module Organization

Two projects in `PoolOfRadianceTrainer.sln`:

- `src/PoolOfRadianceTrainer/` — the WPF app (`AssemblyName` `PoRTrainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. `PorFormat.cs` holds the validated 285-byte character-record offset table; `CharacterRecord.cs` is a typed mutable view over that buffer (handles the `60 − x` AC/THAC0 encoding). `SaveGame.cs`/`InventoryItem.cs` edit on-disk saves; `*Book.cs` files are reference data (monsters, spells, maps, effects).
  - `Memory/` — Win32 P/Invoke (`NativeMethods.cs`), process-memory access, and the signature scanner (`CharacterLocator.cs`) that locates the party by record *shape* since its address changes every session.
  - `ViewModels/` + `Mvvm/` — MVVM; views (`*.xaml`) bind to view models. `ObservableObject`/`RelayCommand` are hand-rolled, not a library.
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.

Docs live in `.docs/` (reverse-engineering write-up, strategy guide); ground-truth memory dumps in `.data/`. Dot-prefixed dirs are git-ignored.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration Debug|Release`, `-Clean`, `-NoRun`, `-Test`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\PoolOfRadianceTrainer\PoolOfRadianceTrainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use file-scoped namespaces (`namespace PoolOfRadianceTrainer.Game;`), XML `<summary>` docs on public types, `sealed` classes by default, and `// --- section ---` divider comments. No linter/formatter config is committed; match the surrounding file.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` asserts the parser against verbatim 285-byte records captured from real dumps and returns exit code 0 (pass) or 1 (fail). It runs individual `Check(...)` assertions, not isolated tests — add new checks there and keep it exiting 0. Parser/format changes must keep the sample-party assertions green.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon (e.g. `Add inventory, spell-freeze, and map features; fix Powers-tab crash`). No PR template exists.

## Domain Notes

Never write to combat-state memory mid-battle — it hangs the game; edit out of combat or via the offline save editor. Back up saves (`CHRDATA?.SAV`) before experimenting.
