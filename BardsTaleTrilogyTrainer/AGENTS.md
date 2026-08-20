# Bard's Tale Trilogy Trainer — Agent Guidelines

This is a **live-memory trainer** for *The Bard's Tale Trilogy* (Krome Studios / inXile, 2018 Steam remaster of the classic Interplay trilogy). It is a C# WPF application targeting `net8.0-windows` (x64) that attaches to the running `TheBardsTaleTrilogy.exe` process and reads/writes game state live — character editing with freeze toggles, class changing, spell levels, item charges, and a Maps tab that shows where the party is standing and teleports it anywhere in the trilogy.

**The offsets here are not guesses.** The remaster ships its full IL2CPP metadata, so the class layouts were read out of `global-metadata.dat` plus the field-offset table in `GameAssembly.dll` and cross-checked against the compiled code. If you are about to add a constant, read it out of the metadata rather than inferring it — `docs/ReverseEngineering.md` §3.1 and §11.1 describe how.

## Project Structure

```
BardsTaleTrilogyTrainer/
├── AGENTS.md                        ← you are here
├── README.md                        ← user-facing readme
├── Run.ps1                          ← build + launch script
├── BardsTaleTrilogyTrainer.sln      ← solution (trainer + tests + Common)
├── docs/
│   ├── ReverseEngineering.md        ← memory layout, map format, methodology
│   └── SpellSystem.md               ← how spell knowledge is really stored
├── src/BardsTaleTrilogyTrainer/
│   ├── Game/
│   │   ├── GameFacts.cs             ← process/module names, class slot RVAs, constants
│   │   ├── Il2Cpp.cs                ← IL2CPP object/array/string/class layout + typed reads
│   │   ├── Il2CppClassLocator.cs    ← resolves Il2CppClass* by slot, validated by name
│   │   ├── CharacterFormat.cs       ← Character/Party/Item layout, validation
│   │   ├── CharacterRecord.cs       ← typed live view over one Character
│   │   ├── GameLocator.cs           ← process/module/party discovery
│   │   ├── ClassBook.cs             ← 13 classes, change rules, class-specific abilities
│   │   ├── Spellbook.cs             ← school ⇄ class-id mapping, bard songs
│   │   ├── SpellId.cs               ← the game's own Spell enum + the cross-game spells
│   │   ├── SpellCatalog.cs          ← the live spell table, read from GlobalSpells.Instance
│   │   ├── ItemBook.cs              ← 127-item catalogue + Garth's shop
│   │   ├── MapFormat.cs             ← Player/GameMap/MapDescription/TeleportTarget offsets
│   │   ├── MapBook.cs               ← all 121 maps (generated from the game's own data)
│   │   ├── MapGrid.cs               ← decoded map model + the map-file parser
│   │   ├── MapArchive.cs            ← reads map files out of the installed resources.assets
│   │   └── MapNavigator.cs          ← reads the party position, performs teleports
│   ├── Memory/
│   │   ├── IMemorySource.cs         ← ProcessMemorySource + FakeMemorySource (+ Allocate)
│   │   └── Il2CppRuntime.cs         ← exported-only remote calls; grows a full List<Spell>
│   ├── ViewModels/
│   │   ├── MainViewModel.cs         ← attach/locate/poll orchestration
│   │   ├── CharacterViewModel.cs    ← per-character VM + SpellLevelViewModel + learnt spells
│   │   ├── MapsViewModel.cs         ← map picker, live marker, teleport
│   │   └── MapRenderer.cs           ← draws a decoded grid (edge walls in dungeons, blocked-square
│   │                                    outlines in cities/wilderness); cell ⇄ pixel mapping
│   └── MainWindow.xaml(.cs)
└── test/FormatCheck/
    └── Program.cs                   ← headless harness + synthetic IL2CPP world
```

## Architecture

### Locating the game state

1. **Validated class slots** (primary). IL2CPP caches an `Il2CppClass*` per type in a slot in `GameAssembly.dll`'s data section; the generated code reaches every static through it. `Il2CppClassLocator` reads the slots named in `GameFacts` and **checks each one by reading the class's own `name`/`namespaze`** before trusting it, so a stale RVA from another build is rejected rather than followed into garbage. From there: `Party.Instance` → `m_members` (`PartyMember[]`) → `m_character`; `Player.Instance` → position; `GlobalMaps.Instance` → the chapter's map arrays.
2. **Module sweep** (fallback). If a slot does not validate, the loaded module is swept for any pointer that resolves to a class with the right name. Slower, but survives a game update.
3. **Character-shape scan** (last resort). Committed memory is swept for objects matching `CharacterFormat.LooksLikeCharacter`, so the character editor works even when the classes cannot be resolved.

### IL2CPP object model

64-bit IL2CPP: objects start with `Il2CppClass*` + monitor, so the first field is at `+0x10`; arrays put `max_length` at `+0x18` and element 0 at `+0x20`; strings put the length at `+0x10` and UTF-16 characters at `+0x14`; `Il2CppClass` has `name` at `+0x10`, `namespaze` at `+0x18` and `static_fields` at `+0xB8`. All of this lives in `Il2Cpp.cs` as constants plus extension methods on `IMemorySource` — use those rather than open-coding a read.

## Game-Knowledge Layer

- `GameFacts` — process/module names, the class-slot RVAs, party and inventory sizes.
- `CharacterFormat` — `Character` (instance size `0x108`), `Party`, `PartyMember`, `Inventory`, `Item`. Note: experience and gold are **`long`**; there is **one** set of attributes; there is **no armour-class field**; spell levels are an `int[16]` reached through `+0xD0` and indexed by class id.
- `MapFormat` — `Player`, `GameMap`, `MapDescription`, `GlobalMaps`, `TeleportTarget`, `DreamSpellTarget`, plus the `Facing`, `TeleportType` and `GameChapter` enums.
- `MapBook` — all 121 areas with grid size, floor, entry point and stair links; BT2's dream-spell destinations; each chapter's new-game start. Generated from the game's data — see below.
- `ClassBook` — the thirteen playable classes, the Review Board's rules, and every class-specific statistic, read from the game's own fields (`ClassScores`).
- `Spellbook`, `ItemBook` — the spell and item catalogues.

## Key Design Decisions

- **Nothing is hard-coded that can be derived.** Class slot RVAs are build-specific, so they are always name-validated and always have a sweep behind them.
- **Teleport goes through the game's own queue.** `Player::OnStateTick` polls `m_queueTeleport` and, when it holds a valid `TeleportTarget`, fades, calls `LoadMap` and then `TeleportTo`. Filling that field is therefore a real teleport — it loads a different map, runs its startup scripts and updates the automap. Writing `m_gridX`/`m_gridZ` directly is kept only as a same-map fallback (`TrySetGridPosition`).
- **A teleport is refused unless it belongs to the loaded chapter.** `TeleportTarget.m_map` is a bare index into the loaded chapter's own `m_cityMaps`/`m_dungeonMaps`, and `Player.LoadMap` indexes it without a bounds test — so a destination picked from another game of the trilogy is not "the wrong map", it is an index out of range inside the game's state machine. The picker lists all 121 maps at all times, so `MapNavigator.AcceptsDestination` is what stands between a click and that: chapter first, then the live array length from `GlobalMaps` (the length, not the descriptors that read back, because the length is what `LoadMap` indexes). An unreadable chapter is refused rather than assumed.
- **The teleport target is the trainer's own block.** `QueueTeleportTo` allocates a fresh object each time, so the field is usually null. Rather than depend on finding one, `MapNavigator` commits 64 bytes with `VirtualAllocEx`, stamps the real `TeleportTarget` class pointer into the header and reuses that block. Boehm ignores pointers outside its own heap and never moves objects, so this is inert to the collector. If allocation is refused it borrows a live `TeleportTarget` instead.
- **Map terrain is read from the player's installation, never bundled.** `MapArchive` walks `resources.assets` (Unity serialised-file format 17) and reads just the one `map_*_asc` TextAsset it needs. `MapBook` holds only metadata — names, sizes, indices. Keep it that way: `.gitignore` excludes `.game/` for the same reason.
- **`MapBook.cs` is generated.** It comes from parsing the three `GlobalMaps` objects in `level3`/`level4`/`level5` plus the map files' own headers. Do not hand-edit the map table; regenerate it. The parse is self-checking — Unity serialises fields in declaration order, so a correct reading consumes each object's byte range exactly, and all three do.
- **Map coordinates**: X runs east, Z runs north, origin at the south-west corner — which is why `MapRenderer` flips Z into pixel rows so north is up.
- **Dungeons record their barriers on the edge; cities and wilderness record them on the square.** A dungeon cell names a wall on each of its four sides, so `MapRenderer.DrawWalls` draws them straight. A city or wilderness cell names no walls at all — what stops the party is the whole square being `Blocked` (a building, a mountain, a stretch of water), which is why `DrawBarriers` traces an outline around those instead, drawing each edge from its open side only so a building block stays clean. The rim counts too, unless the map wraps around. `Blocked` — not the `extra`/`motion` numbers, which look like building ids and street tiles — is the real barrier: flood-filling Skara Brae's non-`Blocked` squares (done once while working this out, not in the harness) gives a single region of 868 out of 900, where `extra == 0` fragments into 36. What `FormatCheck` pins down is the weaker invariant that survives without a flood fill — a city grid records no edge walls, its blocked squares all touch open ground, and it does not wrap — which is enough to catch those maps silently going back to drawing as open ground.
- **Item charges**: zero means "never consumed". `Character::UseItemCharge` returns before the decrement when the count is zero. (`ItemDescription.InfiniteCharges` is 255, but that is the catalogue's bound, not the runtime sentinel.)
- **Spell knowledge has two independent routes, and the trainer uses both.** `Character::KnowsSpell` returns true if `m_learntSpells` contains the spell, *or* if `m_spellLevel[description.m_class] >= description.m_level` — and it skips the second test entirely when the level is 0. So:
  - **School levels**: `m_spellLevel[classId]` for the seven casting classes, capped at `Mathf.Min(7, (level + 1) / 2)` to match `PlayerState_ReviewBoard::UpgradeMage`.
  - **Outright grants**: spells with level 0 (ZZGO 78, NUKE 154, GILL 152, DIVA 153, and the chapter quest spells) exist only in `m_learntSpells`. `CharacterRecord.GrantSpell` appends to that list.
- **The spell table is never hard-coded.** A spell's code, school and level live in serialized `SpellDescription` assets, not in the executable, so `SpellCatalog` reads `GlobalSpells.Instance.m_spellsByEnum` from the running game. The community table that used to live in `Spellbook.cs` was wrong for the remaster and has been deleted rather than corrected; `SpellId` (generated from `global-metadata.dat`) carries the ids, which is all that has to be static.
- **Growing a learnt-spell list is the one place the trainer runs code in the game.** `new List<Spell>()` shares a zero-length backing array, so a fresh character has no slot to append into, and a GC array cannot be made with `WriteProcessMemory`. `Il2CppRuntime` therefore runs a short stub on a new thread. It calls **only exported** functions (`il2cpp_domain_get`, `il2cpp_thread_attach`, `il2cpp_gc_disable`/`_enable`, `il2cpp_array_new_specific`, `il2cpp_thread_detach`) resolved from the module's export table — deliberately *not* `Character::LearnSpell`, whose RVA would be build-specific and would send the thread into arbitrary code on a patched build. The new array's type comes from the class pointer of the array being replaced. Collection stays disabled between the allocation and the moment the array is reachable, and the write order (fill the array, publish `_items`, then raise `_size`) means the game never sees an inconsistent list.
- **Class changing**: `CharacterRecord.ChangeClass` writes the class and grants the new school the level the character's experience level entitles it to. `ClassBook.CanChangeTo` applies the Review Board's rules; the UI refuses unless **Ignore requirements** is ticked. The game's own path (`PlayerState_ReviewBoard::UpgradeMage`) resets `m_level` to 0 and levels back up so HP and SP grow with the new class; a trainer cannot call `LevelUp` without injecting code, so the level is kept where it is and the vitals do not move.
- **Class scores**: the seven `ClassScores` fields are editable. `ClassBook.MaxAbilityScores` is the rule for "max" and lives in the game-knowledge layer so the harness can assert it: only the four scores the game rolls against go to 255, and `m_songsRemaining` refills to the character's level. `m_nmbrOfAttacks` and `m_songsKnown` are counts, not chances, and are left alone.
- **Freeze and position polling** share one 400 ms `System.Threading.Timer`, marshalled through the WPF `Dispatcher`.

## Build & Test

```powershell
.\Run.ps1                    # build Release + launch (UAC prompt)
.\Run.ps1 -Test -NoRun       # run FormatCheck harness only
.\Run.ps1 -Configuration Debug
.\Run.ps1 -Clean
.\Run.ps1 -Publish           # single self-contained exe
```

## Testing

`test/FormatCheck` is a headless console harness — no GUI, no running game. It asserts:

- `GameFacts`, `CharacterFormat` and `MapFormat` constants against each other (field ordering, object bounds, distinct and aligned slot RVAs)
- `LooksLikeCharacter` accepts a plausible character and rejects each way of being implausible
- `MapBook` integrity: 121 maps, per-chapter counts, unique and contiguous indices, asset-name pattern, entry points, dream targets against the city maps they actually name
- `MapFileParser` over miniature dungeon and city maps in the game's own text format
- `ClassBook` roster, the spell-level formula and its inverse, and the class-change rules
- `Il2Cpp` helpers — managed strings, arrays, native strings, class matching, statics
- `CharacterRecord` round-trip: 64-bit fields, spell levels through the array, class change, inventory charges, class scores
- `SpellId` values pinned against the game's enum (ZZGO 78, NUKE 154, GILL 152, DIVA 153; 249 distinct members), so a bad edit grants the wrong spell loudly rather than silently
- `SpellCatalog` over a synthetic `GlobalSpells` — codes read from managed strings, level 0 recognised as "no school grants this", per-school listing
- Learnt spells end to end: append into spare capacity, the version bump, a duplicate grant as a no-op, a **full list refused cleanly** without the runtime helper, removal shifting the tail down, and the full `KnowsSpell` rule
- **A synthetic IL2CPP world** (`SyntheticWorld`) — module slots, classes, static blocks, a `Player`, a `GameMap`, a `GlobalMaps` and a one-member `Party` — driven through the real `Il2CppClassLocator`, `GameLocator` and `MapNavigator`, including a full teleport and a check that the trainer reuses its own target block
- **The real installation**, when present: opens `resources.assets`, decodes all 121 maps and holds each against the catalogue, round-trips the cell⇄pixel mapping across a whole map, and actually renders four real grids (on an STA thread, since `RenderTargetBitmap` needs one). Skipped, not failed, when the game is not installed.

Keep it green: `.\Run.ps1 -Test -NoRun`.

## Dependencies

- `GameTrainers.Common.Memory` — `ProcessMemory`, `MemorySearcher`, `BytePatternScanner`, `NativeMethods`
- `GameTrainers.Common.Mvvm` — `ObservableObject`, `RelayCommand`

## Important Notes

- The trainer targets the **Steam remaster** (2018), not the original DOS games. The original Bard's Tale I trainer lives in `../BardsTale1Trainer/`.
- **Nothing has been verified against a running game.** Every offset was read out of the game's own data and the harness drives each memory path against a synthetic heap, but no address has been watched changing in a live process and no teleport has been performed in-game. Say so plainly when documenting; do not upgrade `[Verified]` to "tested".
- The game must be running with a party in a map before Locate will find anything.
- Edits are live only — there is no save editor.
