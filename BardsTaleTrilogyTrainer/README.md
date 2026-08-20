# Bard's Tale Trilogy Trainer

A live-memory trainer for **The Bard's Tale Trilogy** (Krome Studios / inXile, 2018 Steam remaster). Written in C# / WPF (.NET 8, x64).

## Features

### Maps, location and teleport

- **Every map of the trilogy** — all **121** areas (17 in BT1, 33 in BT2, 71 in BT3), listed by
  chapter with their real grid size, floor number and stair links, taken from the game's own map
  tables. The list can be browsed without the game running.
- **Real terrain, drawn from your own installation** — walls, doors, secret and locked doors,
  crumbling and invisible walls, railings, stairs, spinners, darkness, anti-magic, traps and the
  rest, read out of the game's map files at `resources.assets` rather than bundled with the
  trainer. City and wilderness maps carry their barriers on the square rather than the edge — a
  building, a mountain, a stretch of water — so those are traced with an outline, along with the
  rim of any map that does not wrap around; city gates are marked `GTE` open / `LCK` locked, and
  the services show as taverns, temples, Garth's, the guild, Roscoe's, the bank and the review
  board.
- **Where the party is, live** — chapter, map, floor, square and heading, updated as you walk,
  with a marker on the map. Tick **Follow the party** and the picker moves with you.
- **Teleport anywhere** — click a square, or type X/Z and a heading. The jump goes through the
  game's own teleport queue, so it loads a different map properly: fade, map load, startup
  scripts and automap, exactly as an in-game staircase or scripted teleport would. Pick the
  transition (fade, dimensional or silent) and whether it is written into the journal.
  The picker lists all three games at once, but a jump into a game you are not playing is
  refused with a message rather than sent — the destination is a bare index into the loaded
  chapter's own map array, and the game does not bounds-check it.
- **Entry points and dream-spell destinations** — jump to a map's own entry square, or to any of
  BT2's seven ZZGO dream-spell destinations.

### Characters

- **Auto-locate**: attach to `TheBardsTaleTrilogy.exe` and find the party through the same class
  pointers the game itself uses, with a module sweep and a shape scan behind it
- **Character editing**: HP, SP, XP, gold, level, attributes, race, class and condition — with
  freeze toggles for HP and SP
- **Spell levels**: each of the seven magical schools edited on its own, or every school raised
  at once
- **Granting spells outright**: ZZGO, NUKE, GILL and DIVA — the cross-game spells no school
  level can teach — written straight into the character's learnt-spell list, one at a time or
  all four at once, to one character or the whole party, and removable again
- **Class changing**: turn a character into any of the trilogy's thirteen classes, with the
  Review Board's own rules checked first (Sorcerer needs one school at spell level 3, Wizard
  two, Archmage all four, Chronomancer three schools mastered, Geomancer fighters only) — and an
  override for when you want the class anyway. The new school's spell level comes along.
- **Class-specific stats**: a per-character panel showing what the class actually does — the
  Hunter's critical-hit score, the Rogue's disarm, identify and hide-in-shadows scores, the
  Bard's songs, the Monk's unarmed damage, the Warrior's extra attacks, a caster's school and
  spells known — read from the game's own fields, each with what it means and where that came
  from
- **Class-score editing**: the seven scores the game stores are editable, with **Max class
  scores** raising the four it rolls against (critical hit, disarm, hide, identify) to 255 and
  refilling the Bard's tunes. Attacks per round and songs known are left alone — they are counts,
  not chances. 255 is a certainty *before* the game's per-map penalty, so a maxed Hunter can
  still miss deep in a dungeon
- **Infinite item charges**: zeroing an item's charge count stops the game consuming it
- **Quick actions**: full heal, max attributes — per character or party-wide
- **Reference tabs**: the game's own spell table, read live out of the running process, and the
  127-item catalogue

## Requirements

- Windows 10/11 (x64)
- .NET 8.0 SDK
- The Bard's Tale Trilogy installed and running via Steam
- Administrator rights (the trainer reads/writes the game's process memory)

## Quick Start

1. Launch **The Bard's Tale Trilogy** via Steam
2. Load or start a party (you need to be in-game, not on the main menu)
3. Run `.\Run.ps1` in this folder (a UAC prompt will appear)
4. Click **Attach** to connect to the game process
5. Click **Locate** to find the party in memory
6. Edit values, toggle freezes, change classes, set infinite charges — or open the **Maps** tab
   to see where the party is standing and click anywhere to teleport there

## Building

```powershell
.\Run.ps1                    # build Release + launch
.\Run.ps1 -Configuration Debug
.\Run.ps1 -Clean             # clean bin/obj first
.\Run.ps1 -Test -NoRun       # run verification harness only
.\Run.ps1 -Publish           # single self-contained win-x64 exe
```

## How It Works

The remaster ships its full IL2CPP metadata, so the trainer does not have to guess at anything:
every offset it uses was read out of `global-metadata.dat` and `GameAssembly.dll`'s field-offset
table, then cross-checked against the compiled code.

To find the party and the party's position it follows the same route the game's own code does:

1. Find `GameAssembly.dll` in the process.
2. Read the `Il2CppClass*` for `BardsTale.Party`, `Player`, `GlobalMaps` and `TeleportTarget`
   out of their metadata-usage slots — **and check each one by reading the class's own name and
   namespace**, so a stale address from a different build is rejected rather than trusted.
3. Follow `static_fields` to each type's `Instance`, then read `Party.m_members` for the roster
   and `Player.m_gridX` / `m_gridZ` / `m_facing` / `m_map` for the position.

If a class slot does not check out — a game update, most likely — the trainer sweeps the loaded
module for a pointer that does resolve to the right class. If that fails too, it falls back to
scanning memory for objects shaped like a character, so the character editor still works.

A teleport is queued the way the game queues its own: a `TeleportTarget` is filled in and handed
to `Player.m_queueTeleport`, which the game polls every tick. That is why teleporting to a
different map works properly rather than dropping the party into the wrong level's geometry.

Map terrain comes from your own installation: the trainer opens
`TheBardsTaleTrilogy_Data/resources.assets`, walks the object table, and reads the one map file
it needs. Nothing from the game is redistributed with the trainer.

## Spells

There are two ways the game lets a character know a spell, and the trainer uses both.

**A school level.** Most spells are granted by `m_spellLevel[school] >= spell.level`, where the
school is the class id — Conjurer 6 through Geomancer 12 — and the level runs 0–7. Editing a
school on the Characters tab grants everything that school teaches up to that level.

**The learnt-spell list.** Spells whose level is 0 belong to no school, so no school level can
ever grant them; the game only ever puts them in `Character.m_learntSpells`, from a map script,
a Review Board purchase, or a chapter's quest-spell grant. That is where **ZZGO** (Dream Spell,
id 78), **NUKE** (Gotterdammerung, 154), **GILL** (Gilles Gills, 152) and **DIVA** (Divine
Intervention, 153) live. The Spells tab writes them there directly, and they survive a save.

The Spells tab lists the game's **own** spell table rather than a curated one — every code,
school, level and spell-point cost is read out of `GlobalSpells.Instance` once the party has
been located, because that data lives in the game's serialized assets and cannot be honestly
hard-coded.

### Granting a spell into a full list

`m_learntSpells` is a `List<Spell>`, and a character who has never been taught a spell has a
zero-length backing array — there is no free slot to write into, and a garbage-collected array
cannot be conjured with `WriteProcessMemory`. So the trainer appends in place when there is
room, and otherwise asks the game itself to allocate a bigger array, via a short stub run on a
new thread in the game process. That path:

- uses only functions `GameAssembly.dll` **exports** (`il2cpp_domain_get`, `il2cpp_thread_attach`,
  `il2cpp_gc_disable`/`_enable`, `il2cpp_array_new_specific`, `il2cpp_thread_detach`), resolved
  from the module's own export table, so no game-version-specific address is ever called;
- takes the array's type from the class pointer of the array it is replacing, so the runtime
  allocates exactly the type the field already holds;
- writes **nothing to disk** — a scratch page is committed in the game, used once, and released.

It can be turned off with the checkbox on the Spells tab, which leaves the trainer to plain
reads and writes at the cost of not being able to teach a character whose list is full.

## Limitations

- **Nothing here has been watched working in a live game.** Every offset was read out of the
  game's own metadata and compiled code, and the verification harness drives every memory path
  — locate, read position, teleport — against a synthetic IL2CPP heap, plus it decodes all 121
  real map files from the installed game. But no address has been observed changing in a running
  process, and no teleport has actually been performed in-game. Treat the first run as a test:
  save first.
- The class slot addresses are **build-specific**. They are validated by name before use and
  there is a sweep behind them, so a game update should degrade rather than misfire — but a
  different build may take the slower path.
- **Granting a spell into a full learnt-spell list runs a short stub inside the game.** Nothing
  is written to disk and only exported runtime functions are called, but it is still code
  injection: if it goes wrong the game crashes and unsaved progress is lost. Save first, and use
  the checkbox on the Spells tab to turn it off if you would rather not.
- **Raising a school level does not raise spell points.** The game grows maximum SP through
  `Character.LevelUp`, which the trainer does not call, so a character given school level 7 at a
  low character level will know level-7 spells without the points to cast them. Edit SP Max
  alongside.
- **Garth's shop editor** is not implemented — the shop inventory has not been located.
- There is no save editor (the save format is IL2CPP-serialised binary).
- Map terrain needs the game installed; the map **catalogue** works without it.
- The trainer targets the **Steam remaster** (2018), not the original DOS games.

## Technical Details

- **Engine**: Unity 2018.4 with the IL2CPP scripting backend (64-bit `GameAssembly.dll`)
- **Process**: `TheBardsTaleTrilogy.exe`
- **Memory access**: `ReadProcessMemory` / `WriteProcessMemory` via `GameTrainers.Common`; the
  teleport target is committed with `VirtualAllocEx`
- **Locator**: validated class slots (primary), module sweep (fallback), character-shape scan
  (last resort)
- **Map data**: Unity serialised-file format 17, read from the installed game on demand
- **Freeze mechanism**: a 400 ms poll timer re-writes frozen values and re-reads the party's
  position

See `docs/ReverseEngineering.md` for the memory layout, the map format and the methodology, and
`docs/SpellSystem.md` for how spell knowledge is stored.

## Related

- `../BardsTale1Trainer/` — trainer for the original DOS Bard's Tale I (DOSBox, 109-byte `.TPW` save format)
- `../GameTrainers.Common/` — shared memory access and MVVM libraries
