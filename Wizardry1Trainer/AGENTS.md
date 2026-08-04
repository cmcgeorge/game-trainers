# Wizardry 1 Trainer -- Agent Guide

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for Wizardry 1: Proving Grounds of the
Mad Overlord (Sir-Tech, 1981), running under DOSBox / DOSBox-X via WIZDOS.COM (a UCSD p-system
emulator). Windows-only (WPF + Win32 memory APIs); the app manifest requests administrator
rights so it can `Read/WriteProcessMemory` on the emulator.

## Project Structure & Module Organization

Three projects in `Wizardry1Trainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/Wizardry1Trainer/` -- the WPF app (`AssemblyName` `Wiz1Trainer`, `RootNamespace`
  `Wizardry1Trainer`), layered by concern:
  - `Game/` -- pure data layer, no UI or process dependencies. `CharacterFormat.cs` holds the
    validated 207-byte character-record offset table (TCHAR from the Pascal source), the
    non-standard attribute packing (six 5-bit values into 4 bytes with cross-byte wrapping),
    the TWIZLONG base-10000 encoding for gold/experience, and the race/class/alignment/status
    lookup tables. `CharacterRecord.cs` is a typed mutable view over a 207-byte buffer; it
    handles UCSD Pascal STRING[15] encoding (byte 0 = length, bytes 1-15 = ASCII) and packed
    attribute read/write. `SpellBook.cs` holds the 50 spells (21 mage + 29 priest) with
    descriptions, ordered by the game's internal spell-ID index.
  - `Memory/` -- `RosterLocator.cs` finds the party by **structural scan**, not by anchor: the
    UCSD p-system allocates the character array on its heap at a session-specific address, so
    there is no static string or byte pattern to anchor to. The locator walks every readable
    region looking for a window of contiguous 207-byte records matching the shape of a Wizardry
    1 party (occupied slots pack from slot 0, followed by empty slots). The generic
    process-memory wrapper (`ProcessMemory`/`MemoryRegion`) comes from
    `GameTrainers.Common.Memory` (imported via csproj `<Using>` items), not a local copy.
  - `ViewModels/` -- hand-rolled MVVM. `MainViewModel` (attach/scan/detach, poll loop,
    party-wide actions), `CharacterViewModel` (per-character editable fields, freeze, max
    actions), `NamedValueViewModel` (attribute rows), `ICharacterHost` (the write channel),
    `ReferenceViewModel` (spell book). `ObservableObject`/`RelayCommand` are used from
    `GameTrainers.Common.Mvvm` -- note `ObservableObject` exposes `SetField(ref field, value)`.
- `test/FormatCheck/` -- headless verification harness (console `Exe`), not the app.

## Build, Test, and Development Commands

- `.\Run.ps1` -- build Release and launch (triggers a UAC prompt). Flags: `-Configuration
  Debug|Release`, `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` -- build and run the verification harness without launching the GUI.
- `dotnet build src\Wizardry1Trainer\Wizardry1Trainer.csproj -c Release` -- direct build.
- `dotnet run --project test\FormatCheck` -- run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use
file-scoped namespaces (`namespace Wizardry1Trainer.Game;`), XML `<summary>` docs on public
types/members, `sealed` classes by default, `const` hex for offsets, and `// --- section ---`
divider comments. No linter/formatter config is committed; match the surrounding file. Keep all
reverse-engineered constants in the `Game/` layer and follow the read-validate-write pattern so a
shifted layout is never corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` validates the character record layout, attribute
packing (including the non-standard cross-byte wrapping), TWIZLONG encoding, spell book counts
and ordering, IsOccupied/IsValidCharacter (including status 0-7 and equipment count 0-8
validation), status name lookups, and name round-trip -- and returns exit code 0 (pass) or 1
(fail). Add new checks there and keep it exiting 0.

## Domain Notes

- The character record is 207 bytes ($CF). Names use UCSD Pascal STRING[15] encoding: byte 0 =
  current length (0..15), bytes 1-15 = ASCII characters. Always encode/decode through
  `CharacterRecord`, never write raw bytes.
- Attributes are packed as six 5-bit values into 4 bytes at $2C-$2F with a non-standard bit
  layout: STR = byte[0] & 0x1F, INT wraps from byte[1] low bits to byte[0] high bits, PIE =
  (byte[1] >> 2) & 0x1F, VIT = byte[2] & 0x1F, AGI wraps from byte[3] low bits to byte[2] high
  bits, LUK = (byte[3] >> 2) & 0x1F. Confirmed: $52 4A 52 4A = all 18s.
- Gold and experience use TWIZLONG (base-10000, 3 x uint16 LE): value = LOW + MID * 10000 +
  HIGH * 100000000. Not packed BCD.
- Spell knowledge is 50 bits (one per spell) packed into 8 bytes. Mage spells are indices 0-20
  (7 levels, 4/2/2/3/3/4/3 spells per level); priest spells are indices 21-49 (7 levels,
  5/4/4/4/6/4/2 spells per level).
- The UCSD p-system heap is dynamically allocated, so the roster address changes every session.
  The structural scan validates: Pascal string name (1-15 chars, first char A-Z), race 1-5,
  class 0-7, alignment 1-3, status 0-7, attributes 3-18, HP max 1-999, level 1-99,
  equipment count 0-8.
- Setting attributes to 18 is safe. The game UI may render very large gold/XP values oddly
  (cosmetic). Setting level above the game's natural cap is technically safe but may cause
  unexpected behavior.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon.
Describe which game state a change reads/writes and how it was confirmed against the Pascal
source or the live game. No PR template exists.
