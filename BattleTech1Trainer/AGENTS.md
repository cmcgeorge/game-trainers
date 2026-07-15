# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for the 1988 Westwood Associates / Infocom DOS RPG
*BattleTech: The Crescent Hawk's Inception*, running under DOSBox / DOSBox-X. Windows-only (WPF + Win32
memory APIs); the app manifest requests administrator rights so it can `Read/WriteProcessMemory` on the
emulator.

## What makes this trainer different

BattleTech is a **real-mode DOS** binary (`BTECH.EXE`), so its code and static data load into DOSBox's
emulated conventional RAM essentially verbatim — which is why the trainer can **detect** the game by
signature-scanning for the EXE's static strings (`Memory/GameDetector.cs` over
`GameTrainers.Common.Memory.BytePatternScanner`). But the *mutable* state (C-Bills, character
health/armor, skill ordinals) sits in dynamically managed structures with no adjacent constant byte-run
to anchor to, and its address changes every session. So — like `ThePerfectGeneral2Trainer` — there is
deliberately **no `GameLocator`**; the dependable primitive is a Cheat-Engine-style **value scan** via
`GameTrainers.Common.Memory.MemorySearcher`, driven from `MainViewModel`, with three guided-scan buttons
(C-Bills / Health-Armor / Skill). See `.docs/ReverseEngineering.md` for the full teardown, confidence
tags, and the open Ghidra leads.

## Project Structure & Module Organization

Three projects in `BattleTech1Trainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/BattleTech1Trainer/` — the WPF app (`AssemblyName` `BattleTechTrainer`, `RootNamespace`
  `BattleTech1Trainer`), layered by concern:
  - `Game/` — pure data layer, no UI or process dependencies. Keep all reverse-engineered constants here.
    - `WeaponTable.cs` — the **Confirmed** 17-byte weapon-record decoder (11-byte NUL-padded name + 6
      stat bytes, last byte a class tag). `Decode`/`DecodeTable` throw on a mis-sized block so a shifted
      read can never garble the name/stat boundary. This is the load-bearing confirmed structure.
    - `WeaponReference.cs` — the weapon names by scale (personal vs. 'Mech), names **Confirmed** from the
      table, notes **Corroborated**.
    - `MechReference.cs` — the 'Mech roster; names **Confirmed** from the `0x1D9C8` name table,
      tonnage/armor/role **Corroborated**. Read-only (the in-EXE record stride is not yet decoded).
    - `SkillSheet.cs` — the seven **Confirmed** skills and the five 0–4 proficiency levels, plus a
      `DescribeLevel` that annotates out-of-range ordinals rather than throwing.
    - `GameSignatures.cs` — the verbatim EXE byte runs (Inspect-field block, title string) the detector
      scans for. Read-only detection anchors, **not** state locators.
  - `Memory/` — `GameDetector.cs`, the presence check (first-hit signature scan). Not a value locator.
  - `ViewModels/` — hand-rolled MVVM using `GameTrainers.Common.Mvvm` (`ObservableObject` exposes
    `SetField(ref field, value)`; `RelayCommand`).
    - `MainViewModel` — attach/detach, **Detect game**, background scan `Task` with cancellation, 200 ms
      poll loop (re-writes frozen pins, live-refreshes a small result set), pin/freeze, the guided scans.
      Implements `IScanHost` and `IDisposable`.
    - `ScanValue` — decimal/hex parsing + width-fit helpers (pure, unit-tested).
    - `ScanResultViewModel` — one scan candidate (address + live value).
    - `FrozenValueViewModel` — a pinned address: label, live value, target poked on edit, freeze
      re-write; rejects an out-of-width target (read-validate-write).
    - `IScanHost` — the write channel the rows use to reach RAM.
  - `App.xaml` / `MainWindow.xaml` — the WPF UI (Value Scanner / Freezes / 'Mechs / Weapons / Skills tabs).
- `test/FormatCheck/` — headless verification harness (console `Exe`), not the app.

Reverse-engineering notes and the strategy guide live in `.docs/`; the game itself in `.game/`.
Dot-prefixed dirs are git-ignored — never commit them.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration Debug|Release`,
  `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet build src\BattleTech1Trainer\BattleTech1Trainer.csproj -c Release` — direct build.
- `dotnet run --project test\FormatCheck` — run the harness directly.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. Use file-scoped
namespaces (`namespace BattleTech1Trainer.Game;`), XML `<summary>` docs on public types/members, `sealed`
classes by default, `const` for sizes/counts, and `// --- section ---` divider comments. No
linter/formatter config is committed; match the surrounding file. Keep all reverse-engineered constants
in the `Game/` layer and follow the read-validate-write pattern so a shifted layout is never corrupted.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` asserts the Confirmed 17-byte weapon-table format (stride,
name trimming, class tag, mis-sized-block rejection), the 'Mech / weapon / skill references, the
detection signatures (the Inspect-field block and title decode to their known ASCII), the
value-parsing/width-fit helpers, and the frozen-value view-model logic (poke, freeze re-write,
out-of-width rejection, write-failure report). It runs individual `Check(...)` assertions and returns
exit code 0 (pass) or 1 (fail). Any parser/format or view-model change must keep the assertions green.

## Commit & Pull Request Guidelines

Commit subjects are imperative, sentence-case summaries; join related changes with a semicolon. Describe
which game state a change reads/writes and how it was confirmed against the shipped files or live game.
No PR template exists.

## Domain Notes

The trainer is intentionally scoped to the value scanner, the signature detector, and the read-only
Confirmed references. The 'Mech record stride, a live-state anchor for a future `GameLocator`, the save
format, and the `.MTP` map format are all known open leads (`.docs/ReverseEngineering.md` §6) left for
Ghidra work rather than guessed. The `.BLD` town/room scripts are lightly obfuscated with the confirmed
cipher `plaintext = (0x40 - cipherByte) & 0x7F` (§2.1) — content only, no runtime offsets. C-Bills is
stored wider than a byte (walkthroughs reach ~350,000), so the C-Bills guide scans Int32.
