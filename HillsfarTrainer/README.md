# Hillsfar Trainer

A Windows/WPF live-memory trainer and character-file editor for **Hillsfar** (SSI / Westwood
Associates, 1989 — the build reports itself as `v1.2`), running under DOSBox or DOSBox-X.

**No value searching.** You do not hunt for addresses the way you would in Cheat Engine. Pick the
emulator process, press **Attach**, and the trainer finds the game's data segment by itself — under
100 ms in a live 16 MB DOSBox guest, with all four corroborating literals matched.

---

## Why it can be this simple

`MAIN.EXE` is double-packed — a SEA-AXE LZW/RLE stub wrapping an EXEPACK image — but the program
inside is an ordinary Microsoft C build with a **single data group**. Once that was unpacked, every
global turned out to sit at a constant `DGROUP` offset, and the whole 188-byte character record sits
at `DGROUP:0x094C`.

Only the load segment moves between sessions (measured at four different addresses across four
runs), so the trainer sweeps for the game's own 69-byte startup banner at `DGROUP:0x0D1A`, subtracts
the offset, and then requires **at least two of four** further literals to line up at their own
offsets *and* the record behind them to pass a shape check — a three-of-five match at minimum, and
in practice all five. The exact ratio is printed in the status bar. That is the entire location
strategy; there is no scanner and no fallback.

Full teardown in [`docs/ReverseEngineering.md`](docs/ReverseEngineering.md); play guide in
[`docs/StrategyGuide.md`](docs/StrategyGuide.md).

---

## Running it

```powershell
.\Run.ps1                                  # restore, build Release, launch
.\Run.ps1 -Test -NoRun                     # run the verification harness only
.\Run.ps1 -Configuration Debug             # debug build
.\Run.ps1 -Clean                           # remove bin/obj first
.\Run.ps1 -NoBuild                         # launch the existing exe
.\Run.ps1 -Publish                         # single self-contained win-x64 exe
```

A UAC prompt appears: reading and writing another process's memory needs elevation, especially when
the emulator itself is elevated.

### The usual sequence

1. Start Hillsfar in DOSBox and answer its two startup prompts (`2` for EGA/VGA, `4` for hard disk).
2. At **CAMP OPTIONS**, load or generate a character. The trainer needs one — the record is empty
   until then, and it will tell you so rather than attaching to nothing.
3. In the trainer, pick the DOSBox process (emulators are floated to the top of the list) and press
   **Attach**. It locates the game automatically and prints a one-line summary of the character it
   found — **check that against the game's own status panel before editing.**
4. Edit. Every change is written into the running game immediately.
5. Edits reach disk when *you* use the camp menu's **Save your current Hillsfar character**. The
   trainer never writes the game's save for you.

The trainer re-checks on every poll tick that the located address still holds a plausible character.
That is what catches a game restart: DOSBox keeps its guest RAM mapped for the emulator's lifetime,
so quitting `MAIN.EXE` and starting it again moves `DGROUP` while leaving the old address perfectly
readable. When that happens the trainer drops the address and asks you to Locate again, rather than
carrying on writing somewhere stale.

---

## What it does

### Character (live)

Everything on this tab was confirmed by writing a value into the running game and reading it back off
the game's own screen.

| | |
| --- | --- |
| **Identity** | Name (15 chars), race, gender, class, alignment, age |
| **Abilities** | Strength + exceptional-strength percentile, Intelligence, Wisdom, Dexterity, Constitution, Charisma |
| **Vitals** | Hit points and maximum, gold, experience |
| **Levels** | One per class — Cleric, Magic-User, Fighter, Thief |
| **Carried** | Knock rings, healing potions (the game caps both at 99) |
| **Progress** | Archery-range level (game cap 15; five mission steps gate on it) |
| **Clock** | Hour of day and day counter |

One-click: **Heal to full**, **Max abilities**, **Max rings + potions**, **Max archery**,
**Level up** (raises only the classes the character actually has), **Repair picks**, **Reload from
game**, **Export to .HIL**.

**Freeze** re-checks a pinned value on every poll tick (every 400 ms) — hit points, gold, knock
rings, healing potions, hour of day. A frozen value is only re-written when the game has actually
moved it, each box starts at the character's own current value, and the value clamps to the range
the game accepts — so a freeze can never pin a field to something illegal. Hit points cannot be
pinned to zero, and are additionally capped by the character's own maximum, since a current total
above the maximum is a record the trainer itself would refuse to recognise.

**The clock is the sleeper feature.** Most of Hillsfar's buildings are shut most of the day, and one
game hour costs 122 real seconds. Setting the hour turns "come back at midnight for the Cemetery"
into a single edit. The tab lists what is open at both the live hour and the hour you are editing.

### Lock picks

The twelve five-byte pick slots, decoded. The four shape bytes decide which tumblers a pick fits and
come in exact `+20` pairs in every shipped record; the fifth byte is the slot's condition.

**Repair picks** sets the condition byte on slots that already have shape data. It deliberately does
**not** invent shapes — a made-up set could fit nothing. Buy a set at your guild first, then repair.

### Character files (offline)

Edit `.HIL` and `.PRE` files with the game closed. This is safe because there is nothing hidden in
them: a character file is a raw dump of the same 188 bytes, with **no header, no checksum and no
encryption**. Verified three ways — the loaded record matches the file byte-for-byte, a game-written
save matches edited memory exactly, and a file edited on disk loads with every value on the character
sheet.

A one-shot `.bak` is taken beside the file before the first write, and bytes the trainer does not
interpret are carried through untouched.

> DOSBox caches its drive listing, so a file the trainer creates may not appear in the game's load
> menu until Hillsfar is restarted.

### Reference tabs

* **Opening hours** — all eighteen locations against a chosen hour, with an *open now* flag. Names are
  the game's own, misspelling of "Cemetary" included.
* **Arena** — the eight-opponent roster with the tell that beats each. Four tells ship in the game's
  own pub gossip and are reproduced; the rest are marked as "watch and learn".
* **Overland** — the eleven destinations, with a *hidden* flag on the three reachable only by an
  unmarked trail.
* **Controls & tips** — keys for the city, riding, the arena, lock picking and mazes.

---

## Layout

```
HillsfarTrainer.sln
Run.ps1
docs/ReverseEngineering.md      the teardown
docs/StrategyGuide.md           how to play and win, with maps
src/HillsfarTrainer/
  Game/                         all reverse-engineered knowledge lives here
    CharacterFormat.cs          the 188-byte offset table, locator anchors, shape check
    CharacterRecord.cs          typed mutable view; setters clamp and report their byte range
    CharacterFile.cs            .HIL / .PRE load, save, one-shot backup
    ClassBook.cs                class bitmask <-> names, and the game's index table
    RaceBook.cs                 races, genders, alignments
    LocationBook.cs             the eighteen locations and their hours
    ArenaBook.cs                opponents and tells
    LockPickSet.cs              the twelve five-byte pick slots
    TextCodec.cs                the game's digraph text compression (also the build-mismatch canary)
    GameFacts.cs                clock rate, healing formula, controls, tips
  Memory/
    GameLocator.cs              the anchored sweep
    IMemorySource.cs            the seam the harness drives the locator through
  ViewModels/                   hand-rolled MVVM over GameTrainers.Common
test/FormatCheck/               headless verification harness
```

`GameTrainers.Common` supplies `ProcessMemory`, `MemoryRegion`, `ObservableObject` and
`RelayCommand`; only Hillsfar-specific knowledge is local.

---

## Verification

```powershell
.\Run.ps1 -Test -NoRun
```

**2,058 checks with the shipped character files present, 1,991 without** — no game and no emulator
needed. It asserts:

* every record offset against the `DGROUP` address the teardown established, so a typo in one
  constant cannot quietly shift the table;
* the class tables, including that every Cleric/Thief combination is illegal and that the four
  shipped `.PRE` files carry the class indices which pinned the index table's alignment;
* the clock display rule at every boundary hour and the healing formula across its whole range;
* the opening-hours table, including the two ranges that wrap past midnight;
* the text codec against the fifteen digraph expansions the layout was solved from, and that its
  144th byte (`0x80`, not a character) is carried verbatim;
* every clamp, every bulk action, and the exact byte range each setter flushes — driven off a table
  that a coverage check forces to list every mutable property, so a new setter cannot be added
  without pinning its range;
* the locator over a synthetic address space — the anchor placed at every offset across a 1 MiB chunk
  seam, exactly `MinValidators` accepted and one fewer rejected, an implausible record reported
  distinctly from a missing game, unreadable pages salvaged, and no underflow near address zero;
* the view-models' edit, clamp, freeze and failed-write behaviour through a fake host;
* and, because no headless harness can build the XAML, a `TypeDescriptor` check that every type the
  UI puts in an `ItemsSource` exposes real properties — WPF cannot bind to a tuple's fields, and
  that mistake renders a whole table blank with nothing but a debug-output warning.

The GUI itself cannot be smoke-tested headlessly — it needs an interactive desktop and a running
game.

A final group parses the shipped `.HIL`/`.PRE` files. Those are copyrighted and are not in this
repository, so it is **skipped with a note** rather than failed when they are absent; drop a copy into
`.game\` to run it.

### Confirmed live

Against DOSBox 0.74-3 with the real game, using the shipped locator code:

```
Locate: 94 ms
  Validators matched : 4/4
  DGROUP:0000        : 0x78831E0
  summary            : Christopher — Male Human Fighter, level 5, Lawful Good, HP 42/42, ...
  live text table    : read=True, matches shipped=True
  writes accepted    : 4 of 4
  read back          : gold=424242 rings=42 potions=24 hour=8 pm
```

and the game's own character sheet then showed `Gold 424242`. `DGROUP` landed at a different address
in each of four runs (`0x76181E0`, `0x6D7F1E0`, `0x77911E0`, `0x78831E0`), which is the point: nothing
may be hard-coded.

---

## Limits, and why

* **No structural fallback in the locator.** The record has a name string and plausible attribute
  bytes — a shape that will eventually match something unrelated in 16 MB of guest RAM. A confident
  wrong address means a "Max everything" click writing into another program's memory. Five
  independent literals in one 45 KB segment is far stronger evidence, and if a different build moves
  them the honest answer is "not found".
* **Some record bytes are read but never written.** The 32-bit counter at `+0x00`, the flag byte at
  `+0x45` and a handful of state bytes are used by the game but their meaning is not established.
  They are round-tripped, not interpreted.
* **Thief skills are Inferred, not Confirmed.** The three bytes at `+0x32` vary with Dexterity across
  the two shipped thieves and the third matches AD&D *Climb Walls* at level 6, but no live write test
  pinned them. They are decoded by `CharacterRecord` and round-tripped, but deliberately **not**
  surfaced in the UI — an Inferred field is not one to hand a user an edit box for.
* **The trainer never writes a game save behind your back.** Live edits reach disk when the player
  uses the camp menu. The offline editor and **Export to .HIL** both write only where the user
  pointed them, and both take a one-shot `.bak` first — Export matters here because the filename it
  picks is the one the *game* uses, so its target is often the player's own save.
* **No quest editing.** The twelve `Q*.BIN` scripts were identified but not decoded, so mission
  progress cannot be set. The strategy guide's walkthroughs cover that ground instead.
