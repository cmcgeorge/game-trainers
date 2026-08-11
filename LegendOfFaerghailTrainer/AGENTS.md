# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for the 1990 DOS RPG *Legend of Faerghail*
(Electronic Design Hannover / reLINE Software GmbH), running under DOSBox / DOSBox-X. Windows-only
(WPF + Win32 memory APIs); the app manifest requests administrator rights so it can
`Read/WriteProcessMemory` on the emulator.

## Project Structure & Module Organization

Three projects in `LegendOfFaerghailTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/LegendOfFaerghailTrainer/` — the WPF app (`AssemblyName` `LoFTrainer`, `RootNamespace`
  `LegendOfFaerghailTrainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. `CharacterFormat.cs` is the
    410-byte record offset table, every entry carrying a **[Confirmed]** / **[Inferred]** /
    **[Unidentified]** marker. `CharacterRecord.cs` is a typed mutable view over one record with
    little-endian accessors, clamping, and the `IsValidRecord` / `IsEmptySlot` structural checks.
    `GameFacts.cs` holds the build facts and the DGROUP anchors and pointer offsets the locator
    needs. `ReferenceBooks.cs` (races, trades, states, languages, abilities, regions),
    `ItemBook.cs` (186 items with shop prices) and `SpellBook.cs` (142 spell slots) are tables read
    out of `LOF.EXE`, not transcribed from a walkthrough.
  - `Memory/` — `IMemorySource.cs` is the read-only interface the locator needs (so it can be driven
    from a synthetic address space in the harness). `GameLocator.cs` sweeps for the DGROUP anchor,
    corroborates with further literals, pins guest linear 0 via the emulated BIOS data area, then
    follows the game's far pointers at `DS:0x0030` (party) and `DS:0x3FF6` (roster).
    `DosBoxSpeed.cs` drives DOSBox's `Ctrl+F11` / `Ctrl+F12` cycle hotkeys via SendInput. The
    generic process-memory wrapper (`ProcessMemory`/`MemoryRegion`) comes from
    `GameTrainers.Common.Memory` (imported via csproj `<Using>` items), not a local copy.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` (attach/locate/poll, party-wide actions,
    emulator speed), `CharacterViewModel` (per-character fields, freezes, quick actions),
    `NamedValueViewModel`/`NamedFlagViewModel` (attribute, ability and language rows),
    `ItemRowViewModel`/`SpellRowViewModel` (inventory and spell slots), `ReferenceViewModel`
    (read-only tables), `ICharacterHost` (the write channel). `ObservableObject`/`RelayCommand`
    come from `GameTrainers.Common.Mvvm` — note `ObservableObject` exposes `SetField(ref field, value)`.
  - `MainWindow.xaml` — one `CharacterEditor` DataTemplate reused by the Party and Saved-characters
    tabs, so per-character quick actions are code-behind handlers rather than commands.
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app. `FakeMemory.cs`
  builds the synthetic DOSBox guest the locator is driven over.

RE notes and a strategy guide live in `docs/`. Dot-prefixed dirs (`.docs/`, `.data/`, `.game/`) are
git-ignored — never commit them. `.docs/` holds the teardown workspace (memory dumps, the BIOS-key
injector, the SDL-surface screenshotter, the table extractors); it is where to look before redoing
any of this work.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags:
  `-Configuration Debug|Release`, `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet run --project test\FormatCheck -- --game "<LOF dir>" --live <dosbox pid>` — adds the
  shipped-file group and an end-to-end locate against a running game.
- `dotnet build src\LegendOfFaerghailTrainer\LegendOfFaerghailTrainer.csproj -c Release` — direct build.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use
file-scoped namespaces (`namespace LegendOfFaerghailTrainer.Game;`), XML `<summary>` docs on public
types/members, `sealed` classes by default, `const` hex for offsets, and `// --- section ---`
divider comments. No linter/formatter config is committed; match the surrounding file. Keep all
reverse-engineered constants in the `Game/` layer, and keep every write **minimal** — poke the
bytes a field owns, never the whole record, because the game rewrites carried weight and the state
byte constantly and a full flush would race it.

Note that `UseWPF` projects do **not** get `System.IO` from implicit usings; add it explicitly.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` runs **329 checks with no game present and 348 with a
copy of the game and a running DOSBox**: format constants, locator constants, the reference tables
against ids the running game confirmed (item 27 = Leather armour, spell 1 = Burning hands, class 10
= Healer, …), character-record round-trip and clamping, record validation (accepts a live record, a
dead character and a Rnk 0 non-player character; rejects a bad occupied flag, eleven-character and
unterminated names, out-of-range race/trade/state, level > 99, current above maximum), the locator
over a synthetic guest (validator threshold, missing BIOS area, null and junk pointers, a
non-adjacent roster, a gap in the party array, an anchor straddling the 1 MiB sweep seam, an
unreadable page *before* the anchor, decoy regions, cancellation), the marshalled size of the
Win32 `INPUT` struct used by the speed hotkeys, the view-model write paths driven over a recording
`ICharacterHost` (which byte ranges each edit sends, that an unchanged edit sends none, that a slot
edit carries its high-water byte, that every editor ceiling still passes `IsValidRecord`, and that
freezes converge), and — when supplied — the shipped `ROST\ROST` and `GAMES\GAMEn` files. Exits 0 (pass) or 1 (fail). Any parser/format change must keep it green.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon.
Describe which game state a change reads/writes and how it was confirmed against the running game,
the game files, or the manual. No PR template exists.

## Domain Notes

*Legend of Faerghail* is a **six-character party** RPG with a separate **32-slot roster** of saved
characters that the tavern recruits from; both arrays are 410-byte records and they sit adjacent in
one heap allocation, exactly `32 × 410 + 2` bytes apart. `LOF.EXE` is a plain unpacked MZ image, a
Microsoft C 1988 large-model build, so it has one data group and the locator is an anchored sweep
plus two pointer follows rather than a value scan.

Four things bite:

- **The attribute order in the record is Con, Str, Dex, Int, Wis**, while the character sheet prints
  Str, Con, Dex, Int, Wis.
- **The nine ability bytes are not evenly spaced** (`0x25, 0x27, 0x28, 0x2B, 0x2D, 0x30, 0x32,
  0x34, 0x36`). That is measured, twice, with different data — do not "fix" it into a stride.
- **Rnk 0 is legal.** Non-player characters carry level 0 and trade 12 (`??`); rejecting them makes
  the whole roster array fail validation.
- **`+0x6A`/`+0x6B` are high-water marks, not counts** — one past the highest occupied slot, i.e.
  how far the game scans. They read like populations only because every shipped record is packed
  from slot 0; the game itself put a quest item in slot 9 of a three-item character and wrote 10.
  Any slot edit must recompute them, or an item in a far slot is invisible in game.

Two more things that are easy to undo by accident:

- **Every write-through setter no-ops when the bytes do not move.** That is not an optimisation:
  WPF re-pushes bound values when a template is re-applied and when a virtualising `DataGrid`
  recycles a row, and without the guard scrolling the inventory writes into the emulator.
- **The poll loop raises notifications only for fields whose bytes changed.** Raising everything
  would re-read the record into whatever text box the user is halfway through typing in, four
  times a second.

This release **cannot load a saved game**, including one it has just written itself, so there is
deliberately no save editor. There is also no teleport (map position was never located). Both
exclusions are documented in `docs/LegendOfFaerghail-Reverse-Engineering.md` §6 and §8 — do not add
either without new evidence.
