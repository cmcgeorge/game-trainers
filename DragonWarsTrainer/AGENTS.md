# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for the 1989 DOS RPG *Dragon Wars*, running
under DOSBox / DOSBox-X. Windows-only (WPF + Win32 memory APIs); the app manifest requests
administrator rights so it can `Read/WriteProcessMemory` on the emulator.

## Project Structure & Module Organization

Three projects in `DragonWarsTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/DragonWarsTrainer/` — the WPF app (`AssemblyName` `DWTrainer`, `RootNamespace`
  `DragonWarsTrainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. `RosterFormat.cs` holds the
    validated 512-byte character-record offset table, the locator anchor + delta, and the
    skill/spell/gender/status lookup tables. `CharacterRecord.cs` is a typed mutable view over a
    512-byte buffer; it handles the Dragon Wars name encoding (every char has its high bit set
    except the last; `0` is padding) and the current/base attribute and current/max vital pairs.
  - `Memory/` — `RosterLocator.cs` finds the party by **anchor**, not by record shape: it scans
    for the unique first 48 bytes of `DATA1`'s chunk-0 header and reads the seven-slot roster at a
    fixed delta (`0x0D0E`) past it, since the address changes every session. The generic
    process-memory wrapper (`ProcessMemory`/`MemoryRegion`) comes from `GameTrainers.Common.Memory`
    (imported via csproj `<Using>` items), not a local copy.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` (attach/scan/detach, poll loop, party-wide
    actions), `CharacterViewModel` (per-character editable fields, freeze, max actions),
    `NamedValueViewModel` (attribute/skill rows), `ICharacterHost` (the write channel). Views
    (`*.xaml`) bind to these. `ObservableObject`/`RelayCommand` are used from
    `GameTrainers.Common.Mvvm` — note `ObservableObject` exposes `SetField(ref field, value)`.
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.

Ground-truth memory dumps live in `.data/` (with `memdump.md` and `DragonWars.ahk`); the game
itself is in `.game/`. Dot-prefixed dirs are git-ignored — never commit them.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration
  Debug|Release`, `-Clean`, `-NoRun`, `-Test`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\DragonWarsTrainer\DragonWarsTrainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use
file-scoped namespaces (`namespace DragonWarsTrainer.Game;`), XML `<summary>` docs on public
types/members, `sealed` classes by default, `const` hex for offsets, and `// --- section ---`
divider comments. No linter/formatter config is committed; match the surrounding file. Keep all
reverse-engineered constants in the `Game/` layer and follow the read-validate-write pattern so a
shifted layout is never corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` decodes a base64-embedded 4-record slice captured
from a real dump (the opening party: Muskels, Theb, Elendil, Cheetah), asserts every parsed field
plus the name round-trip and `IsOccupied`, and returns exit code 0 (pass) or 1 (fail). It runs
individual `Check(...)` assertions, not isolated tests — add new checks there and keep it exiting
0. Any parser/format change must keep the sample-party assertions green.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon.
Describe which game state a change reads/writes and how it was confirmed against the live game or
dumps. No PR template exists.

## Domain Notes

The roster is seven 512-byte slots; occupied slots are validated by a plausible name and Health
max. Names use the Dragon Wars high-bit encoding — always encode/decode through `CharacterRecord`,
never write raw ASCII. Setting attributes/skills/vitals to the trainer's "max" caps is safe; the
game UI may render very large numbers oddly (cosmetic).
