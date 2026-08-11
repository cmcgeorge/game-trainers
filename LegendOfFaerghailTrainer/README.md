# Legend of Faerghail Trainer

A Windows/WPF **live-memory trainer** for the DOS conversion of *Legend of Faerghail*
(Electronic Design Hannover / reLINE Software GmbH, build stamped `19.06.1990`), running under
**DOSBox** or **DOSBox-X**.

There is **nothing to search for**. Attach once and the trainer resolves the game's data group by
an anchored sweep, then follows the game's own far pointers to the six party slots and the
thirty-two saved-character slots. Measured against the running game: **about 40 ms, 4 of 4
validators, adjacency cross-check green.**

![tabs: Party, Saved characters, Reference, About]

## What it edits

Per character, live, while you play:

- **Identity** — name, Rnk (level), race, trade, sex, ethos, health state, armour %
- **Vitals** — hit points and magic points, current and maximum
- **Purse and progress** — gold, rations, experience, maximum load
- **Attributes** — Constitution, Strength, Dexterity, Intelligence, Wisdom
- **Abilities** — all nine, as the percentages the character sheet prints
- **Languages** — all eight, as tick boxes
- **Inventory** — 48 slots: item (from the game's own 186-entry table), in-use flag, condition %
- **Spells** — 44 slots: spell (from the game's own 141-entry table) and uses left today

**Freeze** toggles for hit points, magic points, gold and rations, re-applied on every poll tick.

**Quick actions**, per character or for the whole party: *Full heal*, *Max attributes*,
*Max abilities*, *All languages*, *Refill spells / repair gear*, *Give gold*.

A **Saved characters** tab does the same for the tavern's roster — the copies the Recruit list
draws from — and a **Reference** tab lists every item with its shop price, every spell, and the
race / trade / state / language / ability tables, all read out of `LOF.EXE` rather than copied from
a walkthrough.

## The game runs too fast — fixing it

*Legend of Faerghail* has **no frame limiter**: it redraws and polls the keyboard as fast as the CPU
allows, so under DOSBox's default `cycles=auto` the wilderness scrolls past faster than you can
steer and message pages flash by unread. There is nothing in the game's memory to slow down; the
fix is emulator-side.

**With the trainer.** The **Slower** and **Faster** buttons in the attach bar send DOSBox's own
cycle hotkeys (`Ctrl+F11` / `Ctrl+F12`) to the emulator window — each step is about 10%, and the
*steps* box controls how many go per click. DOSBox prints the new cycle count in its title bar.
This is the one feature that needs the emulator window to be focusable, because SDL reads real
keyboard input rather than posted messages.

**By hand, permanently.** Edit your `dosbox.conf`:

```ini
[cpu]
core=auto
cycles=fixed 3000
```

Around **3,000 cycles** plays comfortably; raise it for the intro and combat if you like. You can
always press `Ctrl+F11` / `Ctrl+F12` in the DOSBox window yourself — the trainer sends the same
keys.

## Requirements

- **Windows** (WPF and the Win32 process-memory APIs)
- **.NET 8 SDK**
- **DOSBox** (0.74-3 or later) or **DOSBox-X** running the game
- Administrator rights — the app manifest requests elevation so it can
  `Read/WriteProcessMemory` on the emulator, so launching raises a UAC prompt
- Your own legally obtained copy of the game. No game assets ship with this trainer.

## Using it

```powershell
.\Run.ps1                         # build Release and launch (UAC prompt)
.\Run.ps1 -Configuration Debug    # debug build
.\Run.ps1 -Clean                  # wipe bin/obj first
.\Run.ps1 -NoBuild                # launch the existing exe
.\Run.ps1 -NoRun                  # build only; print the exe path
.\Run.ps1 -Test -NoRun            # run the verification harness, no GUI
.\Run.ps1 -Publish                # single self-contained win-x64 exe
```

1. Start the game **with `START.BAT`** in DOSBox (`LOF.EXE` refuses to run on its own — it prints
   *Please start LOF with START.BAT!*). Let the `VOR.EXE` intro finish.
2. You land in the tavern in Thyn. **Recruit at least one companion** — the party array exists from
   the start but is empty until you do, and there is nothing to edit in an empty party.
3. Launch the trainer. If exactly one emulator is running it attaches by itself; otherwise pick
   the process and press **Attach**. (The picker locks while attached — every read, every write and
   the speed hotkeys all go to the process the trainer attached to.)
4. Edit. Changes land in the game's memory immediately; open a character sheet in the game to watch
   them take.

The status line under the attach bar reports where the locator landed — data-group address, guest
base, party and roster addresses, how many validators matched, and whether the roster/party
adjacency cross-check held — so you can see the attach is sane before poking anything.

## What it deliberately does not do

- **No teleport.** The party's map position was never located: it is not in the character record,
  and the world-state block was not decoded.
- **No save-file editing.** This hard-disk release **refuses to load any saved game — including one
  it has just written itself** (`The file GAMES\GAMEn is not a valid game:`). A save editor whose
  write path cannot be round-tripped through the game is an unverified write, so there isn't one.
  Edit the live party instead; the changes hold for the session.
- **No maximum-spell-uses editing.** The number on the right of the slash on sheet page 3 is not
  stored in the character record and was not tracked down.

## Verifying

```powershell
.\Run.ps1 -Test -NoRun                                        # 329 checks, no game needed
dotnet run --project test\FormatCheck -- --game "<LOF dir>"   # + the shipped ROST and GAMEn files
dotnet run --project test\FormatCheck -- --live <dosbox pid>  # + an end-to-end locate
```

`FormatCheck` is a headless console harness — no GUI, no emulator required. It re-checks every
format constant, the reference tables against ids the running game confirmed, the record encoder
and its clamping, record validation (including that a Rnk 0 non-player character is accepted), and
drives the locator over a synthetic address space that reproduces DOSBox's padded guest, its BIOS
data area, the anchor literals at their real offsets and both far pointers — including the awkward
cases: one validator instead of two, a missing BIOS area, a null pointer, a pointer into junk, a
non-adjacent roster, a gap in the party array, an anchor straddling the 1 MiB sweep seam, an
unreadable page, decoy regions, and a cancelled scan. It also drives the view-model write paths
over a recording host, so what each edit actually sends to the game — which byte range, and that an
unchanged edit sends nothing at all — is asserted rather than assumed. With a copy of the game and
a running DOSBox all groups run and the count is **348**. Exits `0` (pass) or `1` (fail).

## Documentation

- [`docs/LegendOfFaerghail-Reverse-Engineering.md`](docs/LegendOfFaerghail-Reverse-Engineering.md) —
  how the 410-byte character record, the data-group layout and both file formats were recovered,
  with a confidence marker on every field.
- [`docs/LegendOfFaerghail-Strategy-Guide.md`](docs/LegendOfFaerghail-Strategy-Guide.md) — party
  building, combat ranks, magic, languages, traps, the route, and where the trainer actually helps.

## Notes

This is a single-player cheat tool for your own saved games. It touches no network and no external
service. Game assets are copyrighted and are **not** included.
