# Airborne Ranger — Live Trainer

A WPF (.NET 8) trainer for the 1988 MicroProse DOS action game **Airborne Ranger**. It attaches to
the running game inside DOSBox / DOSBox-X, **finds the game's data segment on its own** — no value
searching, no Cheat Engine — and edits the mission live: wounds, every ammunition counter, the
first-aid kits, the countdown clock and the merit-point tally, with freeze toggles and one-click
"max" actions. It also edits the career file `ROSTER.DAT` offline.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

The layout it edits was not guessed. `AR.EXE` is EXEPACK-compressed; unpacked, its status panel
turns out to be a **fill-in-the-blanks text template** — the shipped executable stores literal `X`
placeholders that the game overwrites with ASCII digits. Searching the code segment for the
placeholder addresses finds the one routine that fills the panel, and that routine names its own
source variables. Every field was then read out of a live DOSBox session and matched against the
screen, and two of them were **written and watched change on screen**. The teardown is written up in
[docs/ReverseEngineering.md](docs/ReverseEngineering.md).

---

## Quick start

1. **Launch the game** in DOSBox / DOSBox-X:
   ```
   AR.EXE
   ```
   Answer the graphics prompt (**4** = MCGA reads most clearly) and pick a control device
   (**2** = Keyboard – Directional).
2. **Get into a mission** — assign a ranger, choose a mission, set the difficulty, take the supply
   pod, and jump when the light in the bottom-left corner brightens.
3. **Build and run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `ARangerTrainer.exe`, which requests administrator rights via
   UAC — reading and writing another process's memory needs them, especially if the emulator is
   elevated.
4. **Attach:** pick the DOSBox process from the dropdown (emulators are sorted to the top) and click
   **Attach**. It locates the data segment automatically and fills the editor in.

You can attach any time after the graphics-mode prompt; the mission fields only mean anything once a
mission is under way. The **Roster** tab needs no attach at all — it works with the game closed.

---

## What it can edit

| Group | Fields |
| --- | --- |
| **Condition** | Wounds (3 = death), First-aid kits |
| **Ammunition** | Rounds in the loaded magazine, Spare magazines, Hand grenades, LAW rockets, Time bombs |
| **Mission** | Countdown clock (0–999), Merit points, Soldiers eliminated, Targets destroyed |
| **Career (offline)** | Each ranger's name, rank, score, six decorations and the campaign ribbon |

Edits are written to the game *immediately*. The game's own status panel is a text buffer it only
redraws when it feels like it, so the panel on the map screen can lag — the live mirror in the
trainer always shows the truth.

### Freeze toggles

| Toggle | Effect |
| --- | --- |
| **Freeze wounds** | Re-pins the wound counter every poll tick. Pinned at zero this is effectively invulnerability — see the caveat below. |
| **Freeze ammo** | Holds magazines, rounds, grenades, LAW rockets, time bombs and first-aid kits. |
| **Freeze clock** | Stops the countdown moving. |

A freeze is a poll-tick re-pin, not a hook: it restores the value about 2.5 times a second. That is
fast enough for ordinary wounds, but the game has instant-kill events that write a wound count of 4
directly, and one of those can end the mission inside the gap between ticks. It makes you very hard
to kill, not immortal.

A freeze holds values **for one mission**. It only fires while a mission is actually running (the
countdown is non-zero), and it re-takes what it is holding at the start of each mission. Leave
"Freeze ammo" ticked across a whole session and each mission is held at its own starting loadout —
rather than the next mission inheriting whatever the last ranger died with, or a fresh 600-second
clock being clamped back to where the previous mission ended.

### Quick actions

**Heal**, **Resupply**, **Max Clock**, **Max Everything**. The "max" targets are what the display
can actually show: supply counters 99, spare magazines **98** (the panel prints that field *plus one*
for the chambered magazine, and its two-digit renderer turns a displayed 100 into the characters
`:0`), and the clock 999 (stored as three separate decimal-digit bytes, so 999 is a hard ceiling
rather than a policy).

### Roster editing

The **Roster** tab opens `ROSTER.DAT` from your game directory and edits the six career slots:
name, rank (the game's own fifteen-entry ladder, including the `KIA` and `POW` markers), career
score, the six decorations and the campaign ribbon. Rank and decorations are stored twice in the
file — as text on the roster screen *and* as a binary index and bitmask — and the editor writes both,
because the game reads one and prints the other.

**Do this with the game closed.** The game rewrites `ROSTER.DAT` when a veteran ranger finishes a
mission, and it will overwrite your edits. The original is copied to `ROSTER.DAT.bak` before the
first save (once — a later save will not overwrite that copy), and everything the trainer does not
understand is written back byte for byte.

### Not editable, and why

* **Two bytes in each roster record** (tail offsets 3 and 4) are not interpreted. Across the six
  shipped records they read `00 00`, `01 A4`, `00 00`, `01 0F`, `02 0E`, `01 E2`, which correlates
  with neither the score nor any obvious mission count in either byte order. Six samples is not
  enough to close it, so they are round-tripped verbatim rather than guessed at.
* **Fatigue.** The green bar in the action view is drawn as a bar, not as digits, so the
  fill-template trick that recovered everything else does not reach it.
* **Map position.** Not identified, so there is **no teleport** rather than an unreliable one.
* **The `.DTX` data files** (artwork, screens, terrain tiles) use an undecoded compression format.
  Nothing in the trainer needs them.

---

## How it finds the game

`AR.EXE` is a medium-model 16-bit program: one code segment, one data segment, one stack segment.
DOS relocates it, so the segment lands somewhere different every session — but *within* the segment
every global has a constant offset. `Memory/GameLocator.cs`:

1. Sweeps the emulator's memory for the status panel's own caption `CARBINE MAGS`, which sits at
   data-segment offset `0xB923`.
2. Subtracts that offset — you now have `DGROUP:0000`.
3. Requires at least **two** of four further literals (the rank table, the decoration line, the
   mission list, the version string `441.01`) to line up at their own known offsets, so a stale copy
   of the caption in a disk buffer cannot be mistaken for the running game.
4. Reads the mission state and sanity-checks its shape — clock digits in 0..9, a wound count the
   game could produce, a magazine that is not over-full, a known weapon code.

There is deliberately **no structural fallback**. Between missions the mission-state block holds
whatever the last one left behind, and on a fresh run it is all zeros, so it has no shape
distinctive enough to scan for — a structural sweep would confidently return a wrong address. Four
independent literals in one 59 KB segment is much stronger evidence, and if a different build moved
them, "not found" is the honest answer.

---

## Verified against the real game

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` runs **439 checks** with no game running (453 with the copyrighted roster present):

* every offset pinned to the `DGROUP` address the game's own panel-fill routine names;
* the decoded layout against a fixture built from the values confirmed live — the panel reading
  `CARBINE MAGS 04 / GRENADES 03 / LAW ROCKETS 01 / TIME BOMBS 01 / WOUNDS 00 / FIRST AID 01 /
  WEIGHT 22 / TIME 600`;
* that the displayed magazine count and the carried weight are reproduced by the game's own rules —
  including reconstructing the weight from the supply-pod price table, which is what turned a
  plausible offset table into a confirmed one;
* the three-digit clock round-tripping across its whole range;
* every clamp, every bulk action, and the exact byte range each setter flushes;
* **the locator itself**, driven over a synthetic address space: it finds an anchor cut in half by a
  1 MiB chunk seam at eleven different split points, accepts exactly `MinValidators` and rejects one
  fewer, rejects an anchor with implausible state behind it, salvages past an unreadable page
  instead of losing the megabyte around it, does not underflow on an anchor near address zero, and
  honours cancellation;
* the roster layout — that its record geometry tiles exactly, that a malformed file is refused
  rather than rewritten, that editing one record leaves the other five and the two undecoded tail
  bytes untouched, and that an unedited file round-trips byte for byte;
* the reference tables, including that each mission's challenge level matches the game's own
  thirteen-digit table and that the STANDARD supply-pod loadout sums to exactly the pod's capacity;
* the view-model's edit, clamp and freeze behaviour through a fake host — including that a freeze
  armed after the game has moved on pins what the game has *now*, that editing a frozen field
  re-pins it so the edit sticks, that a failed write is reported rather than swallowed, and that a
  poll tick writes nothing when nothing is frozen.

With a copy of the copyrighted `ROSTER.DAT` present it additionally parses the shipped roster and
asserts that each record's binary rank index and decoration mask reproduce the text the game prints
beside them:

```
CPL Daniel — 8,950
COL T. van der Beek — 581,350  [COM1 COM2 BSTR SSTR DSC CMH (CMPN)]
SGT loser — 18,893
COL Michel — 133,650  [COM1 COM2 BSTR SSTR DSC CMH (CMPN)]
COL Daniel — 131,724  [COM1 COM2 BSTR SSTR DSC CMH (CMPN)]
PSG General *Daniel* — 30,700  [COM1 (CMPN)]
```

Absent, that group is **skipped with a note** rather than failed. To run it, put a copy of your
`ROSTER.DAT` in `.game\`, which is git-ignored.

The live path was exercised too: attach, auto-locate, read every field and match it against the
game's status panel, then write spare magazines and the clock and watch the heads-up display change
to **10 magazines** and a countdown running down from **999** — all against `AR.EXE` under
DOSBox 0.74-3.

---

## Project layout

```
docs/          ReverseEngineering.md  the teardown: the EXEPACK unpack, the segment map,
                                      the panel-fill routine, ROSTER.DAT, copy protection
               StrategyGuide.md       how to play, how to win, the twelve missions and the maps
src/AirborneRangerTrainer/
  Game/        MissionFormat.cs   the confirmed offset table, caps, anchors, LE accessors
               MissionState.cs    typed mutable view over the live mission window
               RosterFormat.cs    the 495-byte ROSTER.DAT layout
               RosterFile.cs      record views, strict parsing, one-shot .bak on save
               RankBook.cs        the fifteen rank slots, from the game's own table
               DecorationBook.cs  the six awards and the ribbon-line renderer
               MissionBook.cs     the twelve missions, briefings and challenge levels
               WeaponBook.cs      the five weapon codes
               GameFacts.cs       equipment weights, controls, ribbons, tips
  Memory/      GameLocator.cs     anchored DGROUP scan (no structural fallback — see above)
               (shared)           ProcessMemory / MemoryRegion — from GameTrainers.Common.Memory
  ViewModels/  MainViewModel, MissionViewModel, RosterViewModel, ReferenceViewModel, IMissionHost
  App.xaml, MainWindow.xaml       the WPF UI (Mission / Roster / Missions / Reference)
test/FormatCheck/                 headless verification, 439 checks
.docs/                            the original proposal (git-ignored)
.game/                            a copy of your ROSTER.DAT, if you provide one (git-ignored)
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory layer come from the shared
`GameTrainers.Common` library rather than being duplicated here.

---

## Notes & caveats

* **There is no pause in this game.** Enemies engage within a minute of landing and three wounds
  kill you, which is exactly what **Freeze wounds** is for. The map screen (see the strategy guide)
  is the closest thing to a pause and the countdown does not run while it is up.
* **The game owns `ROSTER.DAT`.** The Roster tab is an offline editor; close the game before using
  it.
* Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
* Tested against the `AR.EXE` shipped with the IBM PC release, internal version **441.01**
  (73,029 bytes, EXEPACK-compressed). Another build would move the data-segment literals, and the
  locator would correctly report that it found nothing.

---

## Also in the box

The **Missions** and **Reference** tabs carry the strategy guide with you: all twelve missions with
the game's own briefing text, their terrain and the game's own challenge rating, plus a tactical
note for each; the mission-area schematic; the keyboard controls recovered from the game's interrupt
handler and command dispatcher (with the handful whose physical key is inferred rather than
confirmed marked as such); the five weapon codes; the supply-pod weight table and the arithmetic
that proves it; the rank ladder and the six decorations; and the 23 campaign ribbons the
manual-lookup copy protection asks about. The long-form version is
[docs/StrategyGuide.md](docs/StrategyGuide.md).
