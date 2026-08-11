# Red Baron Trainer

A Windows/WPF **live-memory trainer** for *Red Baron* (Dynamix / Sierra, 1990) running under
**DOSBox** or **DOSBox-X**, plus a diagnostic for the game's notoriously fussy joystick detection.

There is **nothing to search for**. Start the trainer and it finds the emulator, pins guest linear
0 through the emulated BIOS data area, sweeps for the game's data group, and reports which of Red
Baron's two executables is currently running. Measured against the live game: **3 ms for the
simulator, 18 ms for the shell, 4 of 4 corroborating literals in both cases**, over a 16 MB guest.

It also finds your game folder on its own, by reading the `mount` lines out of the emulator's own
`.conf`.

Every write re-checks that the game is still where the locate left it immediately before the bytes
go out, rather than trusting an address that was validated up to a second ago — the two executables
replace each other in the guest, so "attached" is a claim with a shelf life.

## Red Baron is two programs

`BARON.COM` chains `PS.EXE` — the menus, career and roster — which chains `RB.EXE`, the flight
simulator, and gets chained back to when the mission ends. They are separate processes inside the
guest with unrelated data groups, so the trainer follows the game between them: it re-checks its
anchor on every poll tick and re-locates when the game switches. The tab that applies to whatever
is running is the one that is live.

## What it does

### Realism — the cheat that matters

Red Baron's Realism Panel is thirteen 16-bit values, and five of them are not difficulty tuning at
all — they switch whole subsystems off:

| Setting | Turned off |
|---|---|
| **Limited ammunition** | the sim never decrements a round counter |
| **Limited fuel** | the tank never empties |
| **Aircraft may be damaged** | hits, flak and heavy landings do nothing |
| Gun jams allowed | long bursts never jam a Vickers or Spandau |
| Real navigation | the map keeps showing where you are |

**Combat level** — which is what drives career scoring — is a separate value, so the **No limits**
preset clears exactly those five and is otherwise identical to the game's own Expert preset: Combat
Level stays on Hard, the Flight Model on Expert, and blackouts, carburettor freezes, realistic
weather and the sun blind spot stay as Expert sets them. A career then keeps the top score
multiplier for a difficulty it is no longer actually flying. (`FormatCheck` asserts that the preset
differs from Expert in exactly those five places and nowhere else, so this list cannot drift away
from the code.)

The panel can be written to:

- **the running game**, when the shell is up (it is at a fixed data-group offset there); and
- **`MREAL.PRF`** (single missions) and **`CREAL.PRF`** (careers) on disk. This is the one that
  reaches the simulator: `RB.EXE` re-reads the file at the start of every sortie, so a change made
  here survives even though the sim is a different process.

The first write copies the file to `<name>.bak` beside it, and an existing backup is never
overwritten — so the `.bak` always holds the file as it was before this trainer ever touched it.
Writes go through a temporary file and a rename, so a failure mid-write cannot leave the game with a
truncated preference file.

`ROSTER.DAT` is read but deliberately never written: pilot edits go to the shell's live memory,
where the game itself owns the write-back.

Presets: **Novice** and **Expert** are byte-for-byte what the game's own buttons set; **No limits**
is described above.

### Pilots

The ten career slots of `ROSTER.DAT` as the shell holds them in memory, plus the career currently
being flown. **Pilot names** are editable; each record is also shown as a hex dump.

A name has to be 1 to 17 printable characters. Blanking one is refused rather than written: it would
clear the slot's first byte, which is exactly what the shell reads as "free", and the career behind
it would survive but become unreachable until something reused the slot.

Only the 18-byte name field is written. The other 72 bytes of a pilot record hold score, victories
and medals, but the Pilot Record screen renders them as *sums* over an internal table rather than
as single fields — probing never resolved them to "score lives at +N". Rather than ship
confident-looking labels over guesses, the trainer edits what it can prove and shows you the rest.
The evidence and the dead ends are written up in
[docs/RedBaron-Reverse-Engineering.md](docs/RedBaron-Reverse-Engineering.md#5-career-roster-and-pilot-records).

### Simulator

**Stick and rudder** on/off — the same flag the in-flight `Alt-J` toggle drives, at `DS:0x27B4`
with a second copy at `DS:0x6932` that the trainer keeps in step. If the sim decided at start-up
that no stick was attached, this is what it turned off.

### Joystick diagnostics

See below — this is the tab to open when the game says it cannot see your controller.

## "The game is not detecting my joystick"

Red Baron never asks the BIOS about the joystick. It times the one-shots on port 0x201 itself:
calibrate a delay loop against the PIT, fire the one-shots, then count delay-loop iterations until
each axis line falls — **and give up at 400**. A count of exactly 400 is how the game concludes
"no joystick".

Three things can each produce that, and on the machine this was developed against, two of them were
true at the same time. The **Joystick** tab checks all three.

Only the 400-tick ceiling below is peculiar to Red Baron; the three causes themselves apply to any
DOS game that reads the game port directly, which is most of them. The repository's
[top-level README](../README.md#controllers-under-dosbox) carries the general version, and this tab
is a usable diagnostic for those games too — it never needs to be attached to Red Baron to report
the slot table or the emulator's settings.

**1. Windows has the pad on joystick slot 1, with slot 0 empty.** The tab queries `winmm` —
`joyGetDevCaps` / `joyGetPosEx`, the exact API SDL 1.2 uses, and SDL 1.2 is what DOSBox and the
SDL1 build of DOSBox-X are linked against. A typical bad result:

```
id=0   (empty - JOYERR_UNPLUGGED)
id=1   Microsoft PC-joystick driver, 5 axes, 16 buttons, X=32767 Y=32767   <- the pad
```

Windows assigns these IDs by device arrival and never compacts them, so a controller that has been
unplugged and replugged — or a second one connected after a first — ends up above slot 0. Unplug
every other HID game device or re-pair the pad as the only controller so it lands on slot 0, and
plug it in **before** starting the emulator: SDL only enumerates joysticks during initialisation.

**2. `[cpu] cycles=max` (or `auto`).** This is the big one. With an unbounded cycle count the
emulated CPU rips through the game's delay loop far faster than the emulated one-shot decays, the
count saturates at 400, and the game writes "absent". Use a fixed count:

```ini
[cpu]
core=normal
cputype=386
cycles=fixed 12000
```

That is roughly a fast 286 / slow 386, which is what Red Baron was tuned for — and it stops the sim
running at absurd speed, which is worth doing anyway.

**3. `[joystick] joysticktype=auto` with `timed=true`.** `auto` emulates a game port only if SDL
saw a stick when the emulator started, which with (1) in play is a coin toss. `timed=true` drives
the one-shot durations from wall-clock time rather than emulated cycles — exactly the mismatch the
game's calibration cannot absorb.

```ini
[joystick]
joysticktype=2axis
timed=false
autofire=false
swap34=false
buttonwrap=false
```

With that configuration the simulator detects the stick, `Alt-J` reports `JOYSTICK AND RUDDER
PEDALS ENABLED`, and the enable flag reads 1. The full disassembly of the detection routine is in
[docs/RedBaron-Reverse-Engineering.md](docs/RedBaron-Reverse-Engineering.md#7-the-joystick-and-why-the-game-says-it-is-not-there).

## Running it

```powershell
.\Run.ps1                                   # restore, build Release, launch
.\Run.ps1 -Configuration Debug -Test -NoRun # build Debug, run the checks, do not launch
.\Run.ps1 -Publish                          # single self-contained win-x64 exe
```

A UAC prompt appears: the app manifest requests administrator rights, which
`ReadProcessMemory`/`WriteProcessMemory` against the emulator needs.

Start Red Baron with `BARON.COM` in DOSBox and let the main menu appear, then Attach — or just
start the trainer, which attaches on its own and keeps trying.

## Verifying it

```powershell
dotnet run --project test\FormatCheck -- --game "C:\GAMES\RED" --live 12345
```

`FormatCheck` asserts the realism codec, the pilot-record parser, and a full run of the locator
over a synthetic guest image — no emulator and no copyrighted bytes needed. `--game` adds parsing
of a real installation's `ROSTER.DAT`, `MREAL.PRF` and `CREAL.PRF`; `--live <dosbox pid>` adds an
end-to-end locate against the running game, including the game-folder discovery and the emulator
config checks. Both extras are read-only.

## Documentation

- [docs/RedBaron-Reverse-Engineering.md](docs/RedBaron-Reverse-Engineering.md) — program
  architecture, how the data group is found, the realism panel decode, the pilot record (including
  what is *not* decoded and why), live simulator state, and the joystick subsystem disassembled.
- [docs/RedBaron-Strategy-Guide.md](docs/RedBaron-Strategy-Guide.md) — controls, flight model,
  gunnery, mission types, the 22 aircraft, the eleven aces, and how to survive a career.

## Layout

```
RedBaronTrainer/
  src/RedBaronTrainer/
    Game/      GameFacts, RealismSettings, PilotRecord, GameFolder
    Memory/    IMemorySource, GameLocator, JoystickProbe, DosBoxInspector
    ViewModels/ MainViewModel and the two row types
  test/FormatCheck/   headless verification harness
  docs/               reverse-engineering notes and strategy guide
```

The process/guest-memory plumbing and the MVVM base types come from `GameTrainers.Common`.
