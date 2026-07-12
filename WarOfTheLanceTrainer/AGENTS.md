# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for the 1989 SSI DOS strategy wargame *War of
the Lance* (a Dragonlance / AD&D title), running under DOSBox / DOSBox-X. Windows-only (WPF + Win32
memory APIs); the app manifest requests administrator rights so it can `Read/WriteProcessMemory` on
the emulator.

## Project Structure & Module Organization

Three projects in `WarOfTheLanceTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/WarOfTheLanceTrainer/` — the WPF app (`AssemblyName` `WOTLTrainer`, `RootNamespace`
  `WarOfTheLanceTrainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies.
    - `SaveContainer.cs` — the 7-byte container header shared by every `.DAT`/`.UNT`/`.MAP`/`.BIN`
      file (`[0]=0xFD` magic, `[1..2]` checksum16 LE, `[3..4]` tag16 LE, `[5..6]` payloadLen16 LE);
      validates the `fileLen == payloadLen + 7` invariant.
    - `GameText.cs` — the game's "high-bit ASCII" string codec: each text byte is the ASCII code with
      bit 7 set, `0xFF` separates words. All signatures/dumps go through this — never hand-write raw
      text bytes.
    - `GameFacts.cs` — the verified game-knowledge constants (28 nation/place names, 3 sides + codes,
      12 unit-type labels, 21 terrain names, 5 season labels; `TotalTurns=30`, `MaxStrength=240`,
      `EmptySlot=0xFF`), decoded verbatim from NAT.DAT/WL2.DAT/MENU.DAT.
    - `UnitTable.cs` — parses the 400-slot, 4-bytes-per-slot (`X,Y,TypeCode,Flag=0x05`) `.UNT`
      placement table; `0xFF 0xFF` X/Y marks an empty slot.
    - `StrengthTable.cs` — the leading 29 current-strength cells of the WL.DAT working buffer, their
      manual-appendix labels, and the constant qualities/base-number run used as the locator
      signature (block sits at delta `-29` in front of it).
  - `Memory/` — `GameLocator.cs` finds live state by **anchor**, not by hard-coded address: it scans
    for the NAT.DAT nation-name signature (confirms the game is loaded) and for the strength-block
    signature, reading the editable block at the fixed `-29` delta and validating all 29 cells fall
    in `1..240`. The process-memory wrapper (`ProcessMemory`) and `BytePatternScanner` come from
    `GameTrainers.Common.Memory` (imported via csproj `<Using>` items), not a local copy.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` (attach/scan/detach, 600 ms poll loop,
    Max-all / Restore-base / Freeze-all actions), `UnitStrengthViewModel` (per-unit editable/clamped/
    freezable strength cell), `IStrengthHost` (the write channel). Views (`*.xaml`) bind to these.
    `ObservableObject`/`RelayCommand` are used from `GameTrainers.Common.Mvvm` — note
    `ObservableObject` exposes `SetField(ref field, value)`.
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.

Reverse-engineering notes and the strategy guide live in `.docs/`; the game itself is in `.game/`.
Dot-prefixed dirs are git-ignored — never commit them.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration
  Debug|Release`, `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\WarOfTheLanceTrainer\WarOfTheLanceTrainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use
file-scoped namespaces (`namespace WarOfTheLanceTrainer.Game;`), XML `<summary>` docs on public
types/members, `sealed` classes by default, `const` hex for offsets, and `// --- section ---`
divider comments. No linter/formatter config is committed; match the surrounding file. Keep all
reverse-engineered constants in the `Game/` layer and follow the read-validate-write pattern so a
shifted layout is never corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` decodes base64-embedded verbatim slices of the shipped
files (NAT.DAT, MENU.DAT, WL.UNT, and the head of WL.DAT) and asserts the container header, the
high-bit text codec round-trip, the nation/season tables, the `.UNT` unit table, and the
strength-block base-number run + locator signature. It runs individual `Check(...)` assertions and
returns exit code 0 (pass) or 1 (fail). Any parser/format change must keep the assertions green.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon.
Describe which game state a change reads/writes and how it was confirmed against the game files or
live game. No PR template exists.

## Domain Notes

The trainer is deliberately scoped to the **verified** leading 29-cell current-strength block. A
WL.DAT vs. SCEN.DAT diff showed the deeper per-unit record layout is interleaved and can't be aligned
reliably without live RAM dumps, so it is intentionally left out rather than guessed. All on-screen
strings use the high-bit-ASCII encoding — always encode/decode through `GameText`, never write raw
ASCII. Strength writes are clamped to `1..240`; the campaign starts every unit at its base number
(the manual appendix), which is what **Restore base** returns to.
