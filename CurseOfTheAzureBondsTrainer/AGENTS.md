# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer and offline save editor for the 1989 DOS game
*Curse of the Azure Bonds*, running under DOSBox. Windows-only (WPF + Win32 memory APIs); the app
manifest requests administrator rights.

## Project Structure & Module Organization

Three projects in `CurseOfTheAzureBondsTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/CurseOfTheAzureBondsTrainer/` — the WPF app (`AssemblyName` `CoabTrainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. `CoabFormat.cs` holds the validated
    422-byte character-record offset table; `CharacterRecord.cs` is a typed mutable view over that
    buffer (handles the `60 − x` AC/THAC0 encoding and the paired ability scores). `SaveGame.cs` /
    `InventoryItem.cs` edit on-disk saves. `DaxArchive.cs` reads the game's `.DAX` resource
    containers at runtime. `MapTerrainData.cs` and `MonsterBook.cs` are **generated**, not
    hand-written — decoded from `GEO*.DAX` and `MON*CHA.DAX` respectively (see
    `docs/reverse-engineering.md` §5 and §7). `SpellBook.cs` / `ClassRaceBook.cs` are transcribed
    from the Rule Book that ships with the game as `curseazure.pdf`.
  - `Memory/` — the signature scanner (`CharacterLocator.cs`) that locates the party by record
    *shape* since its address changes every session, `MapLocator.cs` (works out which level the game
    has loaded by finding its 512-byte wall array resident — see §8), `ItemLocator.cs` (walks each
    character's item list through the game's own real-mode far pointers), the value-scanner
    (`MemorySearcher.cs`), `SaveFolderLocator.cs`, `PositionLocator.cs`, and hotkey P/Invoke. The
    generic process-memory wrapper (`ProcessMemory`/`MemoryRegion`) comes from
    `GameTrainers.Common.Memory` via csproj using-aliases; MVVM, `NativeMethods` and the value
    scanner are kept local because they diverged from Common's versions.
  - `ViewModels/` + `Mvvm/` — MVVM; views (`*.xaml`) bind to view models.
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.

Docs live in `docs/`. Dot-prefixed dirs are git-ignored.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration Debug|Release`,
  `-Clean`, `-NoRun`, `-Test`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\CurseOfTheAzureBondsTrainer\CurseOfTheAzureBondsTrainer.csproj -c Release`
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use
file-scoped namespaces (`namespace CurseOfTheAzureBondsTrainer.Game;`), XML `<summary>` docs on
public types, `sealed` classes by default, and `// --- section ---` divider comments. No
linter/formatter config is committed; match the surrounding file.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` asserts the parser against verbatim bytes taken from the
game's own files and returns exit code 0 (pass) or 1 (fail). It runs individual `Check(...)`
assertions, not isolated tests — add new checks there and keep it exiting 0.

The assertions are deliberately *relational* rather than literal: a paladin's THAC0 must equal his
base minus his Strength bonus, a cleric's spells-per-day must equal the Rule Book's table plus his
Wisdom bonus, hit points minus the CON bonus must equal the stored die roll. Prefer adding checks in
that style — a literal `byte[0x78] == 49` passes even when the offset is wrong for the next save.

## Domain Notes

Never write to combat-state memory mid-battle — it hangs the game; edit out of combat or via the
offline save editor. Back up saves (`CHRDATA?.SAV`) before experimenting.

"Combat-state memory" means the engine's **per-fight block** pointed at by record offset `0x18D`,
which the engine rebuilds every round. The trainer never writes it. The 422-byte **character record**
is a different thing and is safe to write during a battle: freezing party HP through a fight is
exactly what god mode does. What it isn't is reliable — the engine has already copied some fields
into the fight — so `MainViewModel.IsBattleActive` drives a caveat on the status line rather than a
block on the write. Don't "fix" that by disabling party edits in combat; it would break god mode.

Live records are re-read every poll tick and checked against the character they were found as
(`CharacterRecord.IsSameCreatureAs`) before their bytes are adopted, because the game frees and
reuses heap slots across area and combat transitions. Anything that follows a remembered address —
the party panel, the combat sweep, the item-list head pointer — must go through that check rather
than trusting a successful read.

### Three things specific to Curse

**Ability scores are (current, maximum) pairs.** Every setter must write both halves. Writing only
the current half means the next Restoration silently reverts the edit; writing only the maximum
changes nothing at all. `CharacterRecord.Pair` and `CharacterViewModel`'s stat pokes both write two
bytes for this reason — don't "simplify" either to one.

**The name field is not strictly NUL-padded.** A real party member is stored as `TRAVIS ` with
length 6 and the space still in the buffer. `CharacterSignature` enforces "name characters up to a
NUL, then nothing but NULs" rather than "NULs past the declared length"; tightening that drops party
members with no error anywhere. See `docs/reverse-engineering.md` §4.

**Level names in `MapBook` are labels, not decoded facts.** The chapter each level belongs to is
established from that chapter's monster roster; the names within a chapter are descriptive, because
Curse's printed maps are in an Adventurer's Journal that isn't part of the install. Each entry keeps
its `GEO<n>:<block>` id, and `MapLocator` answers "which level am I on?" from the game itself. Keep
that provenance intact — don't upgrade the labels to claims.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon
(e.g. `Add the Curse trainer with paired-stat editing and GEO-decoded maps`). No PR template exists.
