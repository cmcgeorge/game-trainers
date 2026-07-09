# Autoduel Trainer

A reverse-engineering study of **AUTODUEL** (Origin Systems, 1985) and a live
memory-editing trainer for it, built from DOSBox-X memory dumps and Ghidra
disassembly.

The repository contains three things:

1. **Reverse-engineering documentation** — how the game stores its state.
2. **A strategy guide** — including how to actually win the game.
3. **A C# / WPF trainer** — a Windows app that attaches to a running DOSBox-X
   instance and reads/edits the live game (money, skills, health, car, etc.).

## Quick start

```powershell
# From the solution root, in PowerShell:
.\Run.ps1
```

`Run.ps1` builds the trainer (Release by default) and launches it. Then:

1. Start **AUTODUEL** in **DOSBox-X** and load a driver (get past the title
   screen — the driver record only exists once a driver is active).
2. In the trainer, select the `dosbox-x` process and click **Attach**.
3. Edit values and click **Apply**, or use the quick buttons (Max Money, Max
   Skills, Full Health, Charge Battery, Reload Weapons, Repair All).

See [Trainer/README.md](Trainer/README.md) for full usage and safety notes.

## Repository layout

| Path | What it is |
|---|---|
| [`.docs/reverse-engineering.md`](.docs/reverse-engineering.md) | Memory layout, struct maps, base-100 encoding, save-file format, key code locations |
| [`.docs/strategy-guide.md`](.docs/strategy-guide.md) | Walkthrough, the FBI quest chain + passwords, car-building, combat, prices |
| [`Trainer/`](Trainer/) | The C# / WPF trainer application (source + its own README) |
| `Game/` | The original AUTODUEL game files |
| `Data/` | DOSBox-X memory dumps (`*.bin` / `*.csv`) and `memdump.md` describing them |
| `Run.ps1` | Build-and-run helper script |

## How it works (in brief)

The trainer attaches to the DOSBox-X process with `OpenProcess` /
`ReadProcessMemory` / `WriteProcessMemory`, scans the emulated guest RAM for a
byte signature from `AUTODUEL.COM`, and derives the address of the game's
40-byte driver record and the car record that follows it. Numeric fields are
stored as little-endian **base-100** digits (money maxes out at $999,999); the
trainer decodes and re-encodes them and clamps writes to legal ranges.

Every offset and encoding was verified two ways: differentially across six
memory dumps of known game states, and by disassembling the game code in Ghidra
to read the routines that use those fields. The read and write paths were then
confirmed against the live game. Full details are in
[`.docs/reverse-engineering.md`](.docs/reverse-engineering.md).

## Requirements

* Windows 10/11 (x64)
* [.NET 9 SDK](https://dotnet.microsoft.com/download) to build; the .NET 9
  Desktop Runtime to run a published build
* DOSBox-X running AUTODUEL

### `Run.ps1` options

```powershell
.\Run.ps1                        # build Release and launch
.\Run.ps1 -Configuration Debug   # build Debug and launch
.\Run.ps1 -NoRun                 # build only
.\Run.ps1 -Clean                 # remove bin/obj first, then build and launch
```

## Scope & intent

This is an offline, single-player trainer for a 40-year-old game, created as a
reverse-engineering exercise on your own machine. It edits only the driver/car
data record in the running emulator — it does not patch game code. Keep a backup
of `Game/drivers` (the save file) before experimenting with edited state.

## License

Provided as-is for personal, educational use. AUTODUEL and its assets are the
property of their respective rights holders; no game files are redistributed by
this project's tooling.
