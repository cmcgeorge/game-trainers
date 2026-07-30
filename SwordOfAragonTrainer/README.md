# Sword of Aragon Trainer

A Windows-only C#/WPF trainer and save editor for **Sword of Aragon** (Strategic Simulations, Inc., 1989 —
MS-DOS, v1.0), the fantasy strategy game where you inherit the barony of Aladda and set out to reunite Aragon.

The game is a compiled **QuickBASIC 3.0** program that runs under DOSBox. Its machine code does not usefully
disassemble — it is a stream of far calls into the BRUN30 run-time module, which is not part of the executable
image — but its *data* is almost entirely legible. That shapes the trainer:

| Tab | What it does |
|---|---|
| **Kingdom** | Offline editor for `ARAGON.HS?`: treasury, score, and per-city population, morale, loyalty, health, tax, recruits, treasury and all seven development categories. |
| **Army** | Offline editor for `ARAGON.HR?`: the 80-slot roster (20 characters + 60 units) — name, type, level, strength, hits, experience, all eight equipment slots, and map position. |
| **Chronicle** | The game's own Chronicle of Deeds from `ARAGON.HI?`, read-only. |
| **Live (DOSBox)** | Attaches to DOSBox, locates `ARAGON.EXE`'s data segment by signature, and finds/freezes gold and 16-bit counters inside it. A whole-process value scanner is the fallback. |
| **Copy Protection** | The complete startup answer key — 13 cities × 4 fields — read out of `SWORD.EXE`. |
| **Reference** | Cities, unit and equipment price tables, the spell ladder, and the 32 world-terrain codes. |

Full reverse-engineering notes are in [docs/RE.md](docs/RE.md); a play-and-strategy guide with maps is in
[docs/StrategyGuide.md](docs/StrategyGuide.md).

## Requirements

* Windows 10/11 with the **.NET 8 SDK**.
* Your own copy of Sword of Aragon. No game files ship with this repository.
* DOSBox or DOSBox-X — only for the Live tab. Save editing needs nothing but the files.

## Running

```powershell
.\Run.ps1                      # build Release and launch
.\Run.ps1 -Test -NoRun         # run the verification harness only
.\Run.ps1 -Configuration Debug # Debug build
.\Run.ps1 -Publish             # single self-contained win-x64 exe
```

A UAC prompt appears because the app manifest requests administrator rights — `ReadProcessMemory` /
`WriteProcessMemory` against DOSBox need them. Save-file editing does not, but the manifest covers the whole app.

## Editing a save

1. **Open save…** and pick any `ARAGON.HS?` file in your game folder. The trainer finds every save letter in
   that folder and reads the matching `ARAGON.HR?`, `ARAGON.HI?` set.
2. Edit whatever you like. Nothing is written until you press **Save**; **Discard** re-reads from disk.
3. On the **first** write to each file the original is copied to `<name>.bak`. That snapshot is not refreshed
   afterwards, so it holds the state from before the trainer first touched that file — it is **not** a rolling
   undo, and if the game has saved over the letter since your last trainer session the `.bak` is older than your
   campaign. The status line after each Save says which backups it actually created.
4. Quit to the game's New/Old Game menu and load that save letter.

**Do not edit a save while the game has it open.** Sword of Aragon writes the whole set on Quit, so it would
overwrite your edits. Quit to the game's menu (or to DOSBox) first.

Some things are deliberately read-only because the game recomputes them at the start of every month, so editing
them buys you nothing: city income, category production, and the global income/upkeep totals. The durable levers
are **Devel** and **Resrc** — while Develop is below the city's resource ceiling the game charges the listed cost
per step, and past it investment becomes much more expensive. **Develop to resource ceiling** raises every
category to exactly that line.

Changing a unit's type or equipment recomputes the four derived fields whose formulas are proven — make cost,
train cost, upkeep and stacking size — from the game's own price tables, including the class purchase discounts
(a Warrior halves Infantry; a Knight takes 25 % off Cavalry *and* Mounted Infantry; a Ranger takes 25 % off Bowmen
and Horse Bowmen). Changing the **player character's** class re-runs that for every unit in the file, because it
is the player's class the discounts key off.

Armour class, hand damage and hits are **not** recomputed: their formulas are not among the reverse-engineered
findings, so the trainer shows what the game last wrote rather than guessing. The game refreshes them itself the
next time the unit is equipped or trained in-game. For the same reason **Equip best for level** leaves a foot unit
on foot — putting Infantry on a horse would produce a combination the cost model was never validated against.

## The Live tab

Sword of Aragon's variables have no statically recoverable addresses, so the trainer hard-codes none. Instead:

1. Start the game, get to the **World Map** (the live features target `ARAGON.EXE`, not the front end or the
   battle module), and **Attach** to the DOSBox process.
2. **Locate data segment.** The trainer pattern-scans for a distinctive 38-byte `ARAGON.EXE` string literal whose
   data-segment offset is known from the executable image, then checks three further literals at their own
   expected offsets relative to it. A hit is accepted only when **at least two** of those three line up — so an
   accepted location is at least a three-of-four match, and the status line reports how many of the four actually
   matched. `DS:0000` is derived from that hit, which reduces the search space from the whole address space to one
   64 KiB segment.
3. Read a figure off the **City Status** screen, type it in, and press:
   * **Find gold** — gold is a QuickBASIC single, i.e. **Microsoft Binary Format**, not IEEE 754. The trainer
     searches for MBF values within ±1 of what you typed (the game displays it rounded) and writes MBF back.
   * **Find counter** — for the 16-bit figures: population, morale, loyalty, health, recruits.
4. **Pin** a candidate, then edit **Target** to poke it, or tick **Freeze** to re-write it every ~250 ms.

If the segment scan finds nothing — a different build, or a value that lives outside DGROUP — the whole-process
value scanner underneath works the classic way: First Scan, change the number in-game, narrow by
Increased/Decreased/Exact.

**Caveat, stated plainly:** the anchor *offsets* are Confirmed from the executable image, but the live behaviour
of this path has not been verified against a running game — the reverse engineering behind this trainer was done
statically. The save editor, by contrast, is validated against all 15 shipped saves.

## The copy protection

At startup the game shows a city crest and asks you to name it from the poster, then to type the **first word**
of one of that city's four summary lines in the Duke's Notebook. The prompt says which field it wants.

The Copy Protection tab lists the complete answer key extracted from `SWORD.EXE` (offsets 0x7250–0x7444),
cross-checked row by row against the rule book. **You do not need the poster**: the prompt names the field, at
least one retry is granted, and each field has at most 13 possible answers — the tab lists them per field so you
can work down the list. The trainer never modifies any game executable.

## Layout

```
SwordOfAragonTrainer/
├─ docs/
│  ├─ RE.md                    reverse-engineering notes (formats, tables, protection)
│  └─ StrategyGuide.md         how to play, controls, maps, opening plan
├─ src/SwordOfAragonTrainer/
│  ├─ Game/                    the game-knowledge layer — every reverse-engineered constant lives here
│  │  ├─ GameFacts.cs          map size, limits, save-file naming, dates
│  │  ├─ Mbf.cs                Microsoft Binary Format single <-> double
│  │  ├─ UnitBook.cs           unit/character and equipment tables + the cost model
│  │  ├─ RosterFormat.cs       the 100-byte roster record layout
│  │  ├─ RosterRecord.cs       typed mutable view over one record
│  │  ├─ RosterFile.cs         ARAGON.HR? load/validate/save
│  │  ├─ CsvRow.cs             field-level edits to one CSV line
│  │  ├─ CityRecord.cs         typed view over one 14-line city block
│  │  ├─ KingdomFile.cs        ARAGON.HS? load/validate/save
│  │  ├─ SaveSet.cs            the four files of one save letter
│  │  ├─ SaveBackup.cs         one-shot .bak before the first write
│  │  ├─ CityBook.cs           the 20 cities: positions, populations, rulers
│  │  ├─ SpellBook.cs          the 23 spells and the per-class level ladder
│  │  ├─ ProtectionBook.cs     the copy-protection answer key
│  │  └─ TerrainBook.cs        world terrain codes and hex vocabulary
│  ├─ Memory/
│  │  ├─ GameSignatures.cs     the four DGROUP anchors and their DS offsets
│  │  └─ DgroupLocator.cs      locate DS:0000, then search inside the segment
│  └─ ViewModels/              hand-rolled MVVM
└─ test/FormatCheck/           headless verification harness
```

`GameTrainers.Common` supplies the shared plumbing: `ProcessMemory`, `MemorySearcher`,
`BytePatternScanner`, `ObservableObject`, `RelayCommand`.

## Verification

```powershell
.\Run.ps1 -Test -NoRun
```

457 checks with the full 15-save corpus present, 272 without it, in four groups:

* **Format arithmetic** — MBF round-trips (including the monotonicity property the gold scan depends on), the
  cost model against twelve worked examples, the documented hard limits pinned to literals rather than to the
  constants the clamps themselves use, and the reference tables' invariants.
* **Synthetic fixtures** — a hand-built roster and kingdom save proving the parsers write to the offsets and
  fields they claim, clamp out-of-range input, reject malformed files, and leave every unedited record and line
  byte-identical.
* **View-model rules** — the roster view-models exercised headlessly: that "equip best" never puts a foot
  unit on a horse, that the type combo cannot occupy an empty slot, that changing the player's class recomputes
  the whole roster, and that a scan candidate carries the width it was found at and renders 16-bit values signed.
* **Real saves** — when a game directory is present (pass one as an argument; the default is the scratch path
  used during development), every shipped `ARAGON.HS?`/`ARAGON.HR?` pair is parsed, round-tripped byte-for-byte,
  and every occupied roster record is checked against the cost model: **623 records across 15 saves, all
  matching**, over 16 distinct (player class, unit type) pairs. Both figures are asserted, not just printed, and
  a save whose roster is missing fails rather than quietly reducing the corpus to nothing. The whole group is
  skipped — with a note — when the copyrighted files are absent, so a clean checkout stays green.

## Scope and limits

* `ARAGON.HT?` (the world grid) is neither read nor written. Its four-`int16`-per-hex payload is only partially
  decoded, and nothing the trainer offers needs it.
* No game executable is patched or modified.
* The token dictionary for the compressed event-text files (`EVENT`, `RANDOM`, `SPECIAL`, …) is not recovered; it
  lives in code that does not disassemble. See [docs/RE.md](docs/RE.md) §8.
* Fields whose meaning is still unproven are round-tripped verbatim and never edited. `docs/RE.md` labels every
  offset and field Confirmed or Unconfirmed.
