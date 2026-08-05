# Legend of Grimrock — Trainer

A Windows-only C#/WPF live-memory trainer for **Legend of Grimrock** (Almost Human, 2012; this build
is the Steam release, version **1.3.7**). It attaches to the running `grimrock.exe`, finds the game's
LuaJIT virtual machine, and reads and writes the party straight out of it.

**There is no value searching.** Not "a scan that runs quickly" — no scan at all in the Cheat Engine
sense. Grimrock keeps its entire gameplay model in ordinary Lua tables, so a champion's health is a
`value` key in a `health` table in a `stats` table on `party.champions[1]`. The trainer walks that
graph by name. Press Attach and the party is on screen.

---

## Quick start

```powershell
.\Run.ps1
```

Builds Release and launches the trainer. A UAC prompt appears — the app manifest requests
administrator rights because it needs `ReadProcessMemory` / `WriteProcessMemory` on another process.

Then:

1. Start **Legend of Grimrock** and load or begin a game.
2. In the trainer, pick **grimrock** in the process list (it is selected for you) and press
   **Attach**.
3. The party appears. The bar under the toolbar reports which locator chain answered, how long it
   took — typically **6 ms** via the module's static pointer, or **9 ms** if it has to sweep — and
   which build it found, read from the game's own `config.gameVersion`. If that is not the 1.3.7 the
   notes were taken against, it says so instead of quietly reporting numbers you should distrust.

Attaching at the main menu is fine — start a game and the party appears on its own; the trainer
re-reads four times a second and notices the moment a dungeon finishes loading.

### Other options

Every `Run.ps1` in this repository takes the same switches:

| Switch | Effect |
| --- | --- |
| `-Configuration Debug\|Release` | build configuration (default Release) |
| `-Clean` | delete `bin`/`obj` first |
| `-NoBuild` | launch the existing build |
| `-NoRun` | build only |
| `-Test` | run the verification harness (355 checks, no game needed) |
| `-Publish` | single self-contained win-x64 exe |

---

## What it can do

### Party tab

Four sub-tabs, one per champion, plus party-wide buttons.

**Party-wide**

| Button | What it writes |
| --- | --- |
| **Heal + restore energy** | sets health and energy to each living champion's own maximum |
| **Feed** | fills every food bar to 1000 |
| **Cure** | clears poison, disease, paralysis, blindness, curse, slow, starvation, burdened, overloaded |
| **Bless** | sets Haste, Rage, Invisibility, Detect Monsters and all four elemental shields for a duration you choose |
| **Max every stat to N** | raises every stat below N to N, leaving anything already higher alone |
| **Give skill points** | sets unspent skill points and lights the character sheet's Level Up badge |

**Per champion**

- Level, experience (with the game's own next-level threshold shown), food, unspent skill points.
  An edit the game refuses reverts rather than leaving a number on screen the game never took
- All twelve stats — health, energy, strength, dexterity, vitality, willpower, protection, evasion
  and the four resistances — each editable, each with a **Freeze** checkbox that holds the stat at
  the value it was ticked at. Health and energy are bars: editing one raises its maximum to fit but
  never lowers it, so dropping your current health cannot throw away the maximum you earned
- Trained skills with their levels
- All eighteen conditions, each with an on/off toggle and a duration
- Traits and talents the champion carries, listed

### Dungeon tab

- **Move the party** to another tile on the level it is on. The destination is checked against the
  map's own cell bits, so a wall is refused rather than walked into, and the occupancy bit the game
  keeps under the party moves with it.
- **Face** north / east / south / west.
- **Reveal this level's map** — fills in the automap. Every tile that is not solid rock is marked
  seen, and each of its four sides is marked as a wall only where the neighbouring tile actually is
  one, so the map shows the level's floor plan rather than a box around every square.
- All thirteen dungeon levels with their names, sizes and visited flags.
- The game's own run statistics — the same sixteen numbers the end-of-game screen shows.

### Reference tab

How the locator works, and the complete spell table with rune combinations, skill requirements and
energy costs, transcribed from the game's own `dungeon.spells`.

### Read-only mode

Clear **Allow writes** in the toolbar and the trainer becomes an inspector: every edit, button and
freeze is refused, with a message saying so.

---

## What it deliberately does not do

Each of these is a decision, and the trainer says so in the UI rather than offering something
unreliable:

- **No travel between dungeon levels.** A level change in Grimrock tears down and rebuilds the map;
  writing `party.level` alone would leave the party pointing at a map it is no longer standing on.
  Same-level movement is offered. Use the stairs for the rest.
- **No item spawning, no learning spells outright, no opening doors, no killing monsters.** All of
  these need the game's own Lua functions to run inside the game's own thread. Doing that from
  outside means injecting a thread and driving the Lua stack while the game is using it, which is a
  different and far more fragile kind of tool.
  **Grimrock has a better route for exactly these:** set `console = true` in
  `Documents\Almost Human\Legend of Grimrock\grimrock.cfg`, and the in-game console can call the
  engine's own developer helpers — `gainExp`, `teleport`, `learnTalent`, `skipLevel`, `getStuff`.
- **No inventory editing.** The slot layout is understood (`Head 1 … Bracers 10`, backpack 11–31)
  but the session this was built against had empty inventories, so nothing was read from a populated
  one and nothing is claimed about it.
- **No save-game editing.** The `.sav` format is fully decoded in
  [`docs/ReverseEngineering.md`](docs/ReverseEngineering.md) — `GRIM`, a version word, a size word
  and a zlib stream over a tagged chunk tree — but a live trainer and an offline editor are
  different tools.
- **No secret-door reveal.** Revealing the map sets the automap bits the game sets when you *see* a
  tile. Secret doors are walls until the game converts them, so a map reveal does not expose them,
  and should not.

---

## How it finds anything

Legend of Grimrock is a C++ engine wrapped around **LuaJIT 2.0.0-beta9** — the exe exports the whole
Lua C API, including `luaJIT_version_2_0_0_beta9`, which names the version outright. Searching the
1.8 MB image for `health`, `champion` or `strength` returns nothing; every gameplay noun is a Lua
global instead.

So the trainer reads LuaJIT's object model directly, and locating the VM is two chains:

1. **Static pointer.** Ghidra shows exactly one cross-reference to the word at module RVA
   `0x00188AB8` — a WRITE, inside the function that registers the engine's C API with Lua, whose
   caller also calls `luaL_newstate`. That word holds the process-wide `lua_State *`. One read.
2. **Heap signature.** If that word is missing, stale, or a different build moved it, the trainer
   sweeps committed memory for LuaJIT's own `GG_State` shape: a thread object whose global-state
   link points **exactly one `lua_State` past itself**. LuaJIT allocates the main thread and the
   global state as one block, so that equality is true of the main thread and of nothing else —
   Grimrock's live coroutines all fail it. This chain knows nothing about Grimrock and would work
   against any 32-bit LuaJIT 2.0 host.

Whichever answers, the result is believed only after validation: the thread's environment must parse
as a `GCtab`, its `_G` key must point back at the table itself, `_VERSION` must read `"Lua 5.1"`, and
all six of `Champion`, `Party`, `Dungeon`, `Map`, `Condition` and `Skill` must be present. A stale
pointer therefore fails cleanly and falls through to the sweep instead of handing the UI a
plausible-looking wrong address.

`grimrock.exe` sets `DYNAMICBASE`, so the module is **not** at `0x00400000` and nothing is
hard-coded — the one module-relative constant is added to the base the OS reports, and it is checked
against the mapped PE section table before it is used at all.

The teardown, including the LuaJIT struct layouts, the full game-state graph, the map cell bits, the
formulas recovered from Lua constant tables and the decoded save format, is in
[`docs/ReverseEngineering.md`](docs/ReverseEngineering.md). A strategy guide built from the same live
tables — every spell's runes, every skill's milestone list, the weapon and armour numbers, the
bestiary — is in [`docs/StrategyGuide.md`](docs/StrategyGuide.md).

---

## Layout

```
LegendOfGrimrock1Trainer/
├── Run.ps1                          build + launch
├── LegendOfGrimrock1Trainer.sln
├── docs/
│   ├── ReverseEngineering.md        the teardown
│   └── StrategyGuide.md             the strategy guide
├── src/LegendOfGrimrock1Trainer/
│   ├── Lua/                         LuaJIT 2.0 object model
│   │   ├── LuaLayout.cs             struct offsets and type tags
│   │   ├── LuaValue.cs              one TValue, plus the slot it came from
│   │   └── LuaHeap.cs               read/write view of the VM's heap
│   ├── Game/
│   │   ├── GameFacts.cs             build fingerprint and game rules
│   │   ├── GrimrockLayout.cs        the one RVA, and every Lua key name
│   │   ├── PeImage.cs               mapped-PE header parsing
│   │   ├── GameLocator.cs           the two chains and their validation
│   │   ├── GameTables.cs            stats, conditions, skills, spells, level names
│   │   ├── PartyReader.cs           the object graph as typed snapshots
│   │   └── TrainerActions.cs        every edit, as read-validate-write
│   ├── Memory/IMemorySource.cs      the process slice, so it can be faked
│   ├── ViewModels/                  session, champion and row view-models
│   └── MainWindow.xaml              the UI
└── test/FormatCheck/                355 checks, no game required
```

---

## Verification

```powershell
.\Run.ps1 -Test -NoRun
```

355 checks, no running game and no copyrighted files. The harness builds **synthetic LuaJIT heaps**
in memory — real object bytes: a `GG_State`, a globals table with the self-reference and version
string the validator insists on, a party with champions, stat, condition and skill tables, a dungeon
with a small map — and runs the real locator, reader and edit code over them. That makes it possible
to test the cases a live game cannot be asked to produce:

- a static pointer that is stale, aimed at a table, or aimed at a coroutine
- a module with no writable data section covering the slot, and one whose PE header will not parse
- a module relocated away from its preferred base (ASLR is real here), and a 64-bit one
- a Lua host with the wrong version, and one that is not Grimrock at all
- a process with no VM whatsoever
- reads and writes through unmapped pages, and a page that goes unreadable *inside* the heap — the
  exact hazard the read-validate-write discipline exists for

plus the whole edit surface: clamping, cure/bless accounting, the teleport's occupancy-bit
bookkeeping, the reveal's per-side wall bits and its preservation of every non-automap bit, the
freeze *write* path holding a stat against real damage while ignoring drift below its tolerance, an
editor with focus not being overwritten mid-type, a freshly built row still taking the game's numbers
while one has focus, a refused edit reverting instead of leaving a phantom value on screen, and
read-only mode actually refusing writes.

The GUI cannot be smoke-tested headlessly — it needs an interactive desktop and a running game.

---

## Requirements

- Windows 10/11, .NET 8 SDK
- Legend of Grimrock 1.3.7 (Steam or GOG), running
- Administrator rights (the manifest requests them; a UAC prompt appears on launch)

## Notes

- Single-player only. Grimrock has no multiplayer, so there is no fairness question — but the game
  autosaves, so an edit you regret is an edit you will be living with.
- The trainer never writes while **Allow writes** is clear.
- Legend of Grimrock and all its content are © Almost Human Ltd. This project ships no game assets.
