# Repository Guidelines

A Windows (WPF / .NET 9) trainer for the DOS game *Lords of the Realm*. It attaches to a
running **DOSBox-X** process and reads/writes the emulator's guest memory live — it never
modifies `LORDS.EXE` or ships game data.

## Project Structure & Module Organization

The single project lives in `Trainer/` (`LordsTrainer.csproj`); there is no `.sln`.
Reverse-engineering notes and gameplay docs are in `.docs/`. `Game/` (copyrighted assets)
and `*.bin`/`*.csv` RAM dumps are gitignored — never commit them.

The design principle across every layer is **discover at runtime, never hard-code host
addresses**, which is why it survives DOSBox-X version and relaunch changes:

- `Native.cs` — thin Win32 P/Invoke (`OpenProcess`, `Read/WriteProcessMemory`, `VirtualQueryEx`).
- `DosBoxMemory.cs` — implements `IGuestMemory`; locates guest RAM by BIOS Data Area
  fingerprint + IVT check (`guestBase = fingerprint − 0x400`).
- `LordsGame.cs` — game knowledge; anchors DGROUP by the "DIVERGANCE" label-pool
  signature, then reads treasury/goods at offsets from DGROUP. The economy block
  (materials, armoury) is found by scanning for its live-value signature.
- `Scanner.cs` — Cheat-Engine-style value scanner over conventional memory (parallel
  offset/value arrays).
- `MainWindow.xaml[.cs]` — UI plus the 120 ms freeze/refresh loop; `App.xaml.cs`
  detaches on any unhandled exception to avoid bad writes.

`IGuestMemory` abstracts memory access so `LordsGame`/`Scanner` can run against an
in-memory fake without a live emulator. Memory offsets are documented and justified in
`.docs/ReverseEngineering.md` — update it when you change any layout constant.

## Build, Test, and Development Commands

```powershell
.\Run.ps1                 # restore, build (Release), and launch
.\Run.ps1 -Configuration Debug
.\Run.ps1 -NoBuild        # run last build
.\Run.ps1 -Clean          # delete bin/obj first
dotnet build .\Trainer\LordsTrainer.csproj -c Release
```

Requires the .NET 8/9 SDK and DOSBox-X running the game. There is no test project.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, x64-only, four-space indent. Use
file-scoped namespaces (`namespace LordsTrainer;`), `sealed` classes, primary
constructors, and `readonly record struct` for value bundles, matching existing files.
PascalCase for public members, `_camelCase` for private fields, `const` hex for memory
offsets. Every public type/member carries an XML doc comment explaining *why*.
