# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer and offline save editor for the 1988 DOS game *Pool of Radiance*, running under DOSBox. Windows-only (WPF + Win32 memory APIs); the app manifest requests administrator rights.

## Project Structure & Module Organization

Three projects in `PoolOfRadianceTrainer.sln`: the WPF app, its test harness, and the shared `GameTrainers.Common` library it references.

- `src/PoolOfRadianceTrainer/` — the WPF app (`AssemblyName` `PoRTrainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. `PorFormat.cs` holds the validated 285-byte character-record offset table; `CharacterRecord.cs` is a typed mutable view over that buffer (handles the `60 − x` AC/THAC0 encoding). `SaveGame.cs`/`InventoryItem.cs` edit on-disk saves; `*Book.cs` files are reference data (monsters, spells, maps, effects). `ClassTables.cs` is the single source of truth for anything derived from class and level (hit dice, THAC0, saving throws, thief skills, spells per day, ability minimums, race legality and level caps) — both `PartyGenerator.cs` and `ClassChange.cs` read it, so don't reintroduce a local copy of a row; its doc comment marks which rows are measured against real records (fighter at levels 1 and 5) and which are the published tables. `ClassChange.cs` changes a character's class and rewrites everything that follows from it while keeping level, hit points, experience and possessions. `PartyGenerator.cs` rolls a whole good-aligned level-1 party and stamps it into a `CharacterRecord` — it writes only the ranges in `RolledCharacter.WrittenRanges` (never the money, item/effect pointers or the party linked list, which belong to the slot and to the game), and those ranges are also exactly what the live path pokes; §6a of `docs/reverse-engineering.md` records what each generated field is anchored to. `MapTerrainData.cs` is **generated**, not hand-written: its per-area wall/door grids are decoded from the game's `GEO*.DAX` level geometry (format and block↔area mapping are documented in `docs/reverse-engineering.md` §7a) and parsed by `MapAscii.cs`. `WildernessMap.cs` is the exception and the opposite: the overland Moonsea map is **transcribed** from the clue-book map in `docs/strategy-guide.md` §9, because the game keeps no overland grid to decode — keep its provenance warning intact and don't quietly "improve" squares.
  - `Memory/` — the signature scanner (`CharacterLocator.cs`) that locates the party by record *shape* since its address changes every session, `ItemLocator.cs` (walks each character's item list through the game's own real-mode far pointers, and works out the guest→host offset by validating candidates — never sweep an address range for items; see `docs/reverse-engineering.md` §5a), PoR's own value-scanner (`MemorySearcher.cs`), `SaveFolderLocator.cs` (finds the folder the running game actually saves into, since a GOG install writes through an `overlay` mount to `cloud_saves\POOLRAD`), `PositionLocator.cs` (the party's map position — note it searches for **two** encodings at once, because indoors the game stores `[X][Y][Facing]` as adjacent bytes while the wilderness stores a pair of 16-bit words whose X carries a constant bias; §7b of the reverse-engineering notes explains why the bias is measured per lock instead of hard-coded), hotkey P/Invoke (`NativeMethods.cs`, now hotkeys-only) and `GlobalHotkeys.cs`. The generic process-memory wrapper (`ProcessMemory`/`MemoryRegion`) is pulled from the shared `GameTrainers.Common.Memory` library via csproj using-aliases; PoR keeps its own MVVM, `NativeMethods` and value-scanner locally because those diverged from Common's versions.
  - `ViewModels/` + `Mvvm/` — MVVM; views (`*.xaml`) bind to view models. `ObservableObject`/`RelayCommand` are hand-rolled and kept PoR-local (they diverge from `GameTrainers.Common.Mvvm` — PoR uses `SetProperty` and `CommandManager`-driven `CanExecuteChanged`), not a library.
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

"Combat-state memory" means the engine's **per-fight block** at record offset `0x108` — 24 bytes per combatant that the engine rebuilds every round (see `docs/reverse-engineering.md` §6). The trainer never writes it. The 285-byte **character record** is a different thing and is safe to write during a battle: freezing party HP through a fight is exactly what god mode does. What it isn't is reliable — the engine has already copied some fields into the fight — so `MainViewModel.IsBattleActive` drives a caveat on the status line rather than a block on the write. Don't "fix" that by disabling party edits in combat; it would break god mode.

Live records are re-read every poll tick and checked against the character they were found as (`CharacterRecord.IsSameCreatureAs`) before their bytes are adopted, because the game frees and reuses heap slots across area and combat transitions. Anything that follows a remembered address — the party panel, the combat sweep, the item-list head pointer in `LiveInventoryViewModel` — must go through that check rather than trusting a successful read.
