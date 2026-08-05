# LegendOfGrimrock1Trainer — agent guide

Read this before changing anything in this folder. The root [`AGENTS.md`](../AGENTS.md) covers the
repository conventions; this file covers what is different about *this* trainer.

## The one thing to understand first

**Legend of Grimrock's gameplay is Lua, not C++.** The exe is a C++ engine that statically links
**LuaJIT 2.0.0-beta9** (it exports the whole Lua C API, including `luaJIT_version_2_0_0_beta9`), and
every gameplay noun — party, champions, stats, conditions, skills, maps, monsters, items — is an
ordinary Lua table. Searching the 1.8 MB image for `health`, `champion` or `strength` returns
**nothing**.

The consequence for this codebase: there are no game offsets to maintain. The "layout" is a set of
**key names** in [`Game/GrimrockLayout.cs`](src/LegendOfGrimrock1Trainer/Game/GrimrockLayout.cs), and
the address arithmetic is LuaJIT's own hash tables. If you are about to add a `const uint SomeOffset`
for a game field, stop — the field almost certainly has a name.

There is exactly **one** module-relative constant in the project: `LuaStateSlotRva = 0x00188AB8`, the
`.data` word holding the process-wide `lua_State *`. It is a shortcut, not a dependency; the
locator's second chain finds the same VM without it, and the harness proves that.

## Layout

```
src/LegendOfGrimrock1Trainer/
  Lua/            LuaJIT 2.0 (32-bit, LJ_GC64 off) object model — properties of LuaJIT, not of Grimrock
    LuaLayout.cs    struct offsets, type tags, the GG_State invariant
    LuaValue.cs     one TValue plus the address it was read from
    LuaHeap.cs      read/write view of the heap: strings, tables, field and index lookup
  Game/
    GameFacts.cs      build fingerprint, game rules the UI clamps against
    GrimrockLayout.cs the one RVA, every Lua key name, the map cell bits
    PeImage.cs        mapped-PE header parsing (sections, timestamp, ASLR bit)
    GameLocator.cs    two chains + validation
    GameTables.cs     stat/condition/skill/spell/level reference data
    PartyReader.cs    the object graph as typed snapshots
    TrainerActions.cs every edit, as read-validate-write
  Memory/IMemorySource.cs   the process slice the locator needs, so it can be faked
  ViewModels/       MainViewModel (session + IGameHost), ChampionViewModel, RowViewModels, ProcessPicker
  MainWindow.xaml   Party / Dungeon / Reference tabs
test/FormatCheck/   355 checks over synthetic LuaJIT heaps; needs no game
```

References `GameTrainers.Common` for both `Memory` (`ProcessMemory`, `NativeMethods`) and `Mvvm`
(`ObservableObject` with `SetField`, `RelayCommand`), via csproj `<Using>` items.

## Rules that are load-bearing

**Addresses are valid for one tick.** LuaJIT's collector never *moves* an object, but adding a key to
a table rehashes its node array and relocates every value in it. So a `Slot` on a `LuaValue` is only
trusted for the read that produced it. Every edit path re-resolves through `IGameHost.ResolveParty` /
`ResolveChampion` before writing, and freezes re-resolve each tick rather than replaying a cached
address. Do not add a cache of resolved slots.

**A slot is only exposed when it holds a number.** `LuaHeap.GetField` hands back a live address for a
nil, a string or a table just as readily as for a double, so `PartyReader.NumberSlot` zeroes anything
that is not a number and every write path refuses a zero slot. That one choke point is what makes
"only written when it was read back as a number this tick" true rather than aspirational — keep new
snapshot fields going through it.

**A freeze latches its target.** `StatRowViewModel.FreezeTarget` is captured when the box is ticked,
never re-derived from the displayed value: the refresh overwrites that value with whatever the game
currently holds, so a derived target would follow the damage down and the freeze would oscillate
between two numbers four times a second. Freezes are also carried across a champion-list rebuild,
along with the selected tab. The write side is `Game/FreezeWriter.cs` rather than a private method on
the session, so it can be tested without a WPF dispatcher — keep it that way.

**A refresh may not overwrite a value being typed into, and a new row must still get one.**
`IGameHost.EditorHasFocus` is a *probe* the window answers from `FocusManager`'s logical focus, not a
flag tracked from keyboard-focus events: a tracked flag latches on forever when the focused editor is
destroyed rather than blurred (a champion-list rebuild does exactly that), and clearing it when
keyboard focus leaves the application throws away a half-typed value on alt-tab. Separately, every
`Update` takes an `initial` flag — a row built while an editor has focus must still take the game's
numbers, or it shows zero and a freeze ticked on it would pin the stat at zero.

**A refused edit reverts.** `GameRowViewModel.Reject` puts the backing field back and re-raises the
property. Every editable setter routes failures through it, including the case where the write was
attempted and the `ActionResult` came back incomplete — not only the "not attached" case.

**A bar's cap is raised, never lowered.** `SetStat` moves `max` up to fit a larger value, but leaves
it alone when the value is smaller. Writing it down would throw away a maximum the player earned, and
Grimrock autosaves, so nothing in the game could undo it. Scores are different — Grimrock holds the
same number in both fields — and move together.

**Interned strings are the one safe cache, and only when the read succeeded.** `LuaHeap` caches
`GCstr` contents by address because LuaJIT never mutates a string in place. Failures are deliberately
*not* cached: a transient unreadable page would otherwise unmatch that key for the rest of the
session, and every write to it would silently become a no-op. `ResetCache()` on re-attach.

**Field lookup is a linear walk, on purpose.** `LuaHeap.GetField` scans the hash part and compares
interned characters instead of reimplementing LuaJIT's string hash and following the chain from
`hashmask(hash)`. A few hundred nodes cost one read, and a linear walk cannot disagree with the VM
about where a key lives. Do not "optimise" this into a hash lookup.

**The locator validates before it believes.** `_G` must point back at itself, `_VERSION` must read
`"Lua 5.1"`, and all six engine class tables must be present. If you add a chain, it validates the
same way. The static-pointer chain additionally refuses to read its slot unless the RVA lands in a
writable, non-executable section of the *mapped* PE — a different build could put code there.

**`grimrock.exe` sets DYNAMICBASE.** Nothing may assume `0x00400000`. The observed base in one
session was `0x00990000`.

**Writes only ever replace a double with a double.** No GC write barrier is involved, and no code
here stores a GC reference into a table. Keep it that way: storing a `GCRef` from outside the VM
without running the barrier would eventually be collected out from under the game.

## What is deliberately absent

Do not add these without a very good reason, and update the README and the UI copy if you do:

- **Cross-level teleport.** A level change also tears down and rebuilds the map
  (`Party:enterDungeon` / `Party:tearDownMap`); writing `party.level` alone desynchronises the party
  from `party.map`. Same-level movement is offered and moves the `DynamicObstacle` occupancy bit with
  it — confirmed live by stepping (2,8) → (2,7) and back.
- **Item spawning, spell learning, door opening, monster killing.** All need the game's own Lua
  functions to run in the game's own thread. Grimrock already has a route for these: its developer
  console (`console = true` in `grimrock.cfg`) exposes `gainExp`, `teleport`, `learnTalent`,
  `skipLevel`, `getStuff` from `Developer.lua`. Point users there instead of injecting a thread.
- **Inventory editing.** `ItemSlot` is understood (`Head 1 … Bracers 10`, backpack 11–31) but the
  probed session had empty inventories, so nothing was observed and nothing is claimed.
- **Save editing.** The format is fully decoded in `docs/ReverseEngineering.md` (`GRIM`, version 6, a
  size word, then zlib over a tagged chunk tree whose value types are exactly Lua's four). Offsets
  would be shareable, but it is a different tool.

## Testing

```powershell
.\Run.ps1 -Test -NoRun          # 355 checks, no game, no copyrighted files
```

`test/FormatCheck/Fakes.cs` builds **real LuaJIT object bytes** in a synthetic address space:
`FakeHeap.BuildGame()` gives a module, a globals table, a decoy coroutine, a main thread, a party
with four champions and a 4×4 map. Everything the locator and reader do runs against it. When you
change the Lua layer or the locator, extend the fixture rather than weakening a check — the
interesting cases (stale pointer, coroutine decoy, relocated module, no data section, unparseable
header, 64-bit image, wrong Lua version, non-Grimrock Lua host, empty process, unmapped pages, a
poisoned page inside the heap) are all already there to copy from.

Keep the harness green and keep the layout constants restated as arithmetic (`~4u`, `0x00588AB8 -
ImageBase`) so a transcription slip fails a check rather than producing a garbage read.

## Reverse-engineering workspace

`.docs/` and `.data/` are git-ignored (`.*/` in the root `.gitignore`) — RAM dumps, Ghidra projects
and probe scripts live there and are never committed. The Ghidra project used for the teardown was
built outside the repo (`C:\GhidraWork\`), because Ghidra refuses a project path containing a
dot-prefixed element, and the exe was copied to a plain path first.

```
analyzeHeadless.bat C:\GhidraWork\grimrock grimrock -import C:\GhidraWork\grimrock.exe \
    -processor x86:LE:32:default -cspec windows
```

LuaJIT's exported symbols give a labelled Lua core for free, which is what makes `luaL_newstate`'s
caller — and from there the static `lua_State` slot — findable in one pass.
