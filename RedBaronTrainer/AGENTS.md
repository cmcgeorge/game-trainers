# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for *Red Baron* (Dynamix / Sierra, 1990),
running under DOSBox / DOSBox-X. Windows-only (WPF + Win32 memory APIs); the app manifest requests
administrator rights so it can `Read/WriteProcessMemory` on the emulator.

**The one thing to know before touching anything here:** Red Baron is *two* executables.
`BARON.COM` chains `PS.EXE` (menus, career, roster) which chains `RB.EXE` (the flight simulator)
and is chained back to when the mission ends. They are separate processes inside the guest with
unrelated data groups, so the locator carries two anchor sets and `MainViewModel` re-locates
whenever the anchor stops matching. Anything you add has to say which module it belongs to.

## Project Structure & Module Organization

Three projects in `RedBaronTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references.

- `src/RedBaronTrainer/`
  - `Game/` — pure data layer, no UI or process dependencies.
    - `GameFacts.cs` — every reverse-engineered constant, as `DS:` offsets. Nothing absolute.
      Split into a shell block and a simulator block; keep it that way.
    - `RealismSettings.cs` — the thirteen Realism Panel values, the codec, and the Novice / Expert /
      No-limits presets. The Novice and Expert vectors are what the game's own buttons write, so
      they double as fixtures — `FormatCheck` asserts against them.
    - `PilotRecord.cs` — the 90-byte pilot record. Only the 18-byte name is decoded; the rest is
      deliberately left as bytes. Do not add speculative field accessors here (see below).
    - `GameFolder.cs` — offline reads of `MREAL.PRF`, `CREAL.PRF` and `ROSTER.DAT`, and writes of the
      two `.PRF` files only. `ROSTER.DAT` is deliberately read-only: pilot edits go to the shell's
      live memory, where the game owns the write-back, rather than rewriting a 908-byte save behind
      its back. Writes go via a temp file and a rename, and take a one-shot `.bak` first.
  - `Memory/`
    - `IMemorySource.cs` — the read/write interface the locator needs, so it can be driven from a
      synthetic address space in the harness. `ProcessMemory`/`MemoryRegion` come from
      `GameTrainers.Common.Memory` (imported via csproj `<Using>` items), not a local copy.
    - `GameLocator.cs` — sweeps guest RAM for a data-group anchor, rejects non-paragraph-aligned
      candidates, requires 2 of 4 corroborating literals, pins guest linear 0 via the emulated BIOS
      data area, then validates each structure before handing back its address.
    - `JoystickProbe.cs` — `winmm` joystick enumeration. Deliberately `winmm` and not XInput: it is
      the API SDL 1.2 uses, and SDL 1.2 is what DOSBox is linked against.
    - `DosBoxInspector.cs` — finds the emulator, its `.conf` (via `-conf` on the command line, or
      beside the exe), the game folder (by parsing `mount` lines), and checks the two settings Red
      Baron's game-port timing is sensitive to.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` (attach, poll, re-locate, all writes),
    `RealismSettingViewModel` (one panel row, tick box or three-way), `PilotViewModel` (one roster
    slot). `ObservableObject`/`RelayCommand` come from `GameTrainers.Common.Mvvm` — note
    `ObservableObject` exposes `SetField(ref field, value)`.
- `test/FormatCheck/` — headless verification harness (console `Exe`). `FakeGuest` builds the
  synthetic DOSBox the locator is driven over, for both modules.

RE notes and a strategy guide live in `docs/`. Dot-prefixed dirs (`.docs/`, `.data/`) are
git-ignored — never commit them. `.docs/` holds the teardown workspace (the guest-memory reader,
the differential scanner, the input driver, a working `rb.conf`); it is where to look before
redoing any of this work.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags:
  `-Configuration Debug|Release`, `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the verification harness without launching the GUI.
- `dotnet run --project test\FormatCheck -- --game "C:\GAMES\RED" --live <pid>` — adds parsing of a
  real installation and an end-to-end locate against a running emulator. Both extras are read-only,
  so this is safe to run against a game mid-mission.

## Coding Style & Naming Conventions

Match the surrounding file: 4-space indent, file-scoped namespaces, `sealed` classes, PascalCase
members, `_camelCase` fields, `const` hex for offsets, XML `///` docs on public members. Keep every
reverse-engineered constant in `Game/GameFacts.cs`.

Follow the read-validate-write pattern, and note that here it has a **time** dimension the sibling
trainers do not: the two executables replace each other in the guest, so an address validated at
locate time can belong to the other program a second later. Every write therefore re-checks
`GameLocator.AnchorStillMatches` immediately before committing, pilot writes additionally re-read
and re-validate the slot, and a failed re-locate sets `_stale`, which every `CanWriteX` consults.
Anything new that writes must do the same. `MainViewModel` also writes only the field it owns —
pilot writes touch the 18-byte name, not the whole record, so nothing the game has updated since the
last read gets clobbered.

## What not to add

**Do not invent labels for the undecoded 72 bytes of a pilot record.** They were probed
methodically — including a self-identifying sweep where the word at offset *k* held 200 + *k* — and
the Pilot Record screen renders score and victories as *sums over an internal table*, not as single
fields. `docs/RedBaron-Reverse-Engineering.md` §5 records exactly what was tried and what came back.
If you crack it, put the evidence in that document at the same time as the code.

Likewise, the in-flight ammunition counter is real and writable but lives on the near heap at an
address that moves every run, with no static pointer to it. The trainer clears **Limited
Ammunition** in the realism panel instead — same outcome, survives a relaunch. Do not replace that
with a heap scan without a locator that can prove what it found.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` is a headless console harness that asserts the realism
codec, the pilot-record parser and a full locate over a synthetic guest, exiting 0 (pass) or 1
(fail). Keep it green, and add a case to it for anything new in `Game/` or `Memory/`. The GUI
cannot be smoke-tested headlessly — it needs an interactive desktop, a running game, and a UAC
prompt somebody clicks.

## Commit & Pull Request Guidelines

Imperative, sentence-case subjects (join related changes with a semicolon). Say which game state a
change reads or writes and how it was confirmed against the live game — "verified by writing X and
watching the Pilot Record screen print Y" is the standard this project is held to.
