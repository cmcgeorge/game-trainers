# Legend of Grimrock — reverse-engineering notes

Everything here was recovered from the shipped executable, from the running game, and from the
game's own save files. Nothing was taken from a leaked source drop, and nothing that could not be
checked twice is stated as fact. Where a number is inferred rather than proven, it says so.

Target: `C:\Program Files (x86)\Steam\steamapps\common\Legend of Grimrock\grimrock.exe`
(Almost Human Ltd., 2012–2013).

---

## 1. The target in one page

| Fact | Value | How it was established |
| --- | --- | --- |
| File size | 1,804,800 bytes | on disk |
| SHA-256 | `34406E14 8099838340F3313FBFFBA7D1 4C5DE898 81089325 C2C10EB0 1B5ACABC` | `Get-FileHash` |
| Machine | `0x014C` — 32-bit x86 | PE COFF header |
| Linker | MSVC 10 (Visual Studio 2010) | optional header; confirmed by the `MSVCR100.dll` import |
| `TimeDateStamp` | `0x5115140B` = 2013-02-08 15:04:43 UTC | PE COFF header |
| Preferred image base | `0x00400000` | optional header |
| `DllCharacteristics` | `0x8140` — **DYNAMICBASE**, NX, terminal-server aware | optional header |
| Observed load address | `0x00990000` in one session | `Process.MainModule.BaseAddress` |
| Sections | `.text 0x401000`, `.rdata 0x52A000`, `.data 0x583000`, `.rsrc`, `.reloc` | section table |
| Packing | none — four normally named sections, raw ≈ virtual, plain `steam_api.dll` import | section table |
| Version resource | **absent** — `VersionInfo` reads empty | `Get-Item .VersionInfo` |
| Game version | **1.3.7** | the game's own Lua global `config.gameVersion` |
| Assets | one 681 MB `grimrock.dat` archive | directory listing |

**ASLR is on.** That single line is the reason this trainer is built the way it is: `grimrock.exe`
opts in to `DYNAMICBASE`, so the module is *not* at `0x00400000` and no absolute virtual address
recovered on one run is valid on the next. Every address in this document is therefore written as an
RVA, and the trainer adds the module base the OS reports.

The version resource being empty is worth noting because it removes the obvious build check. The
build is instead identified two ways: the PE `TimeDateStamp`, and — much more usefully — the game's
own `config.gameVersion` string, which the trainer can read out of the live Lua state.

---

## 2. The finding that shapes everything: the game is Lua

The exe exports **127 symbols**, and they are the entire Lua C API:

```
luaJIT_setmode          0x00468DD0
luaJIT_version_2_0_0_beta9  0x00497D60
lua_newstate            0x00449300
lua_pcall               0x004480A0
luaL_newstate           0x00448DB0
luaL_openlibs           0x00448F30
... 121 more
```

`luaJIT_version_2_0_0_beta9` names the version outright: **LuaJIT 2.0.0-beta9**, statically linked,
32-bit, therefore compiled with `LJ_GC64` off. That matters enormously, because it fixes the object
layout the rest of this document depends on.

The second half of the finding came from a string scan. Searching the whole 1.8 MB image for
`health`, `champion`, `strength`, `evasion`, `party` and friends returns **nothing**. Searching for
`get*`/`set*` returns 363 hits, and every one of them is engine plumbing: `setRenderEntity`,
`getBoundingSphere`, `setSpotSharpness`, `getStaticShadowMap`. There is no C++ champion, no C++ stat
table, no C++ inventory.

Reading the live process settles it. Every gameplay noun is a Lua global:

```
"Champion"  <tab 0299b558>     "Party"      <tab 03d8c048>     "Map"      <tab 0299a5e8>
"Monster"   <tab 03d97dc8>     "Item"       <tab 03dc36b0>     "Spell"    <tab 03cf0ab0>
"party"     <tab 32e57b80>     "dungeon"    <tab 0ecfd160>     "gameMode" <tab 029acdc0>
```

and the loaded chunk list — 149 distinct Lua sources, recovered by sweeping the heap for `GCproto`
objects and reading each one's `chunkname` — is the whole game:

```
AI, Alcove, Altar, Ambush, Arch, AttackPanel, BaseEntity, Blob, Blockage, Blocker, BreakableWall,
BurstSpell, Button, CameraShake, Champion, CharClass, CharSheet, CharacterGeneration,
ChooseDungeonMenu, CinematicMode, Combat, Condition, Config, Console, Counter, Crab, Crafting,
Credits, Crystal, Cube, CustomMaterials, Decoration, Defs, Developer, Door, Dream, Dungeon,
DungeonBuilder, DungeonEditor, DynamicObstacle, Earthquake, FSM, FX, FrozenMonster, GameMode,
GameOver, Goromorg, Grimrock, Gui, HealingEffect, Herder, IceLizard, IceShards,
ImportPortraitDialog, Item, KeyBindings, Lever, LightAttachment, LightSource, Lock, MainMenu, Map,
MapEditor, MapMarker, MapMode, MessageSystem, ModSystem, Monster, MonsterGroup, MonsterLightCuller,
NameGenerator, NewGameMenu, ObjectContainer, Ogre, PVS, ParticleAttachment, Party, PauseMenu,
PerfCounters, Pit, PoisonCloud, PressurePlate, Projectile, ProjectileSpell, Race, Receptor,
SaveGame, SaveGameMenu, ScrambleWriter, ScriptEntity, ScriptInterface, Secret, Settings, Skeleton,
Skill, Slime, SoundAttachment, SoundSystem, Spawner, Spells, SplashScreen, Stairs, StartingLocation,
StaticShadowMap, Statistics, SteamContext, Swipe, Talent, Teleporter, Tentacles, Timer, ToolTip,
TorchHolder, TriggerEvents, Uggardian, WallSet, WallTapestry, WallText, Warden,
lib/{Array,base,class,content,doc,imgui,mat,noise,prim,quat,vec}, lib/imgui/{CodeEditor,MultiLineTextBox},
@assets/dungeons/grimrock/level01..level13, @assets/scripts/{items,monsters,objects,wall_sets}
```

Line-number tables are stripped (`firstline` and `numline` both read 0), but chunk names survive, so
this inventory is exact rather than guessed.

### What that means for a trainer

The usual approach — find a struct, write down offsets, hope the next patch does not move them —
does not apply and is not needed. Grimrock's champion health is a `value` key in a `health` table in
a `stats` table on a champion table in `party.champions`. **Names, not offsets.** A trainer that can
read LuaJIT's object model gets the entire game state by walking it, and stays correct across any
rebuild that does not change LuaJIT itself.

This is also why a Cheat-Engine-style value scan is a bad fit here even though it would technically
work: a champion's health is an IEEE-754 double in a hash node, sitting a few bytes from a `GCstr`
pointer, and there are 520 other tables in the heap with a `health` key (every monster archetype).
Scanning finds hundreds of candidates. Walking the graph finds exactly one.

---

## 3. LuaJIT 2.0 object layout (32-bit, `LJ_GC64` off)

These are properties of LuaJIT, not of Grimrock, and they are what the trainer's `Lua/LuaLayout.cs`
encodes. Each was checked field-by-field against the live process before being trusted.

### TValue — 8 bytes, NaN-boxed

```
+0  uint32  lo    GC pointer, or the low half of a double
+4  uint32  it    type tag, or the high half of a double
```

A slot is a number when `it < 0xFFFFFFF2` — LuaJIT's own test, `itype(o) < LJ_TISNUM` with
`LJ_TISNUM == LJ_TNUMX == ~13u`. Otherwise the tag is one of:

| Tag | Value | Type |
| --- | --- | --- |
| `LJ_TNIL` | `0xFFFFFFFF` | nil |
| `LJ_TFALSE` | `0xFFFFFFFE` | false |
| `LJ_TTRUE` | `0xFFFFFFFD` | true |
| `LJ_TLIGHTUD` | `0xFFFFFFFC` | light userdata |
| `LJ_TSTR` | `0xFFFFFFFB` | string |
| `LJ_TUPVAL` | `0xFFFFFFFA` | upvalue |
| `LJ_TTHREAD` | `0xFFFFFFF9` | thread |
| `LJ_TPROTO` | `0xFFFFFFF8` | prototype |
| `LJ_TFUNC` | `0xFFFFFFF7` | function |
| `LJ_TTRACE` | `0xFFFFFFF6` | trace |
| `LJ_TCDATA` | `0xFFFFFFF5` | cdata |
| `LJ_TTAB` | `0xFFFFFFF4` | table |
| `LJ_TUDATA` | `0xFFFFFFF3` | userdata |

It is worth matching the VM exactly here rather than picking a "safely low" constant. The lowest real
tag is `LJ_TUDATA` at `0xFFFFFFF3`, so anything below that is a number — including negative infinity
(high word `0xFFF00000`) and every negative NaN (`0xFFF80000` and up). A boundary of `0xFFF00000`
would look conservative and would in fact classify those as "some other object", which fails silently
rather than loudly. Grimrock does not store an infinity in a stat, but a reader that disagrees with
the VM about what a number is has no way to say so. The harness asserts the boundary over a spread of
probes including both infinities and NaN.

### GCHeader — every collectable object

```
+0  GCRef   nextgc
+4  uint8   marked
+5  uint8   gct      ~itype: 4 string, 6 thread, 7 proto, 8 function, 11 table, 12 userdata
```

`gct` at `+5` is the single most useful byte in the heap. Scanning for it turns "find an object of
type X" into a one-byte filter.

### GCstr — 16-byte header, characters inline

```
+0   GCHeader
+6   uint8   reserved
+7   uint8   unused
+8   uint32  hash
+12  uint32  len
+16  char[len] '\0'
```

Confirmed live on the champion name `Contar Stoneskull`:

```
1A719120  00 00 00 00  01 04 00 ff  38 e8 1a 27  11 00 00 00
          nextgc=0     marked=1     hash          len=0x11 = 17
1A719130  43 6f 6e 74 61 72 20 53 74 6f 6e 65 73 6b 75 6c  "Contar Stoneskul"
1A719140  6c 00                                            "l\0"
```

`gct = 0x04` is `LJ_TSTR`, and 17 is exactly the length of the name. Strings are **interned**, so
`health` exists once in the whole process — searching the entire 909 MB of committed memory for
`health\0` returns exactly two hits, one of which is the `GCstr` at `0x029AE0C0` and the other a
copy inside a save-serialisation buffer.

### GCtab — 32 bytes

```
+0   GCHeader
+6   uint8   nomm        negative metamethod cache
+7   int8    colo        colocated-array marker
+8   MRef    array       -> TValue[asize], keys 0..asize-1
+12  GCRef   gclist
+16  GCRef   metatable
+20  MRef    node        -> Node[hmask+1]
+24  uint32  asize
+28  uint32  hmask       size of the hash part minus one
```

### Node — 24 bytes, value first

```
+0   TValue  val
+8   TValue  key
+16  MRef    next
+20  MRef    freetop
```

Value-first is a convenience worth noting: writing a table field only needs the node address, with
no further offset.

### lua_State — 48 bytes

```
+0   GCHeader
+6   uint8   dummy_ffid   always FF_C (1)
+7   uint8   status
+8   MRef    glref        -> global_State
+12  GCRef   gclist
+16  TValue* base
+20  TValue* top
+24  MRef    maxstack
+28  MRef    stack
+32  GCRef   openupval
+36  GCRef   env          the globals table, for the main thread
+40  void*   cframe
+44  uint32  stacksize
```

---

## 4. Finding the VM without searching for a value

LuaJIT allocates the main thread and the global state together as one `GG_State`:

```c
typedef struct GG_State {
  lua_State L;        /* main thread  */
  global_State g;     /* global state */
  jit_State J; ...
} GG_State;
```

so for the main thread — **and for the main thread only** —

```
L->glref == (char *)L + sizeof(lua_State) == L + 48
```

Observed live: `L = 0x027501C0`, `glref = 0x027501F0`. Exactly 48 apart.

That equality is the whole locator. A first sweep of the process turned up 69,359 byte positions
where `gct == 6`; adding `dummy_ffid == FF_C` and plausible stack pointers cut it to 256; adding
`glref == L + 48` left **two**, both with the same `glref` — the main thread and one live coroutine,
and the coroutine fails the equality (its `glref` points at the shared global state, not one
`lua_State` past itself). Grimrock keeps several coroutines alive, so this discrimination is not
theoretical.

### Chain A: the module's own pointer

Ghidra found a shortcut. Searching `.text` for the 4-byte constant `0x00588AB8` returns exactly one
instruction:

```
0040BB75   89 3D B8 8A 58 00     mov  dword ptr [0x00588AB8], edi
0040BB7B   E8 30 7A 03 00        call 0x004435B0
```

Ghidra's cross-reference list for `0x00588AB8` contains that one WRITE and nothing else. The
containing function `FUN_0040BB60` decompiles to several hundred `luaL_Reg`-shaped
`{char *name; code *fn;}` pairs — it is the engine's whole C-API registration pass — and its only
caller, `FUN_004074C0`, also calls the exported `luaL_newstate`. So the word at
**RVA `0x00188AB8`** (VA `0x00588AB8` at the preferred base) is written once during Lua bootstrap
and holds the process-wide `lua_State *` for the rest of the session.

Reading it costs one `ReadProcessMemory`. On the live game it resolved in **6 ms**.

### Chain B: the structural sweep

If that word is missing, stale, or a different build moved it, the trainer sweeps committed memory
for the `GG_State` signature above. It knows nothing about Grimrock and would work against any
32-bit LuaJIT 2.0 host. On the live game it resolved in **8.8 ms** across 64 regions and 9 MB, and
returned the same `lua_State` as chain A.

(Regions larger than 64 MB are skipped. LuaJIT's allocator hands out chunks of about a megabyte, so
the VM never lives in a reservation that big, while Grimrock's texture and mesh arenas do — skipping
them removes several hundred megabytes of hopeless scanning.)

### Validation — the part that makes either chain safe

Whichever chain answers, the candidate is believed only after its environment is proved to be a real
Lua globals table:

1. `L->env` must parse as a `GCtab` (`gct == 11`, sane `asize`/`hmask`).
2. That table's `_G` key must be a table **whose address is the table itself**.
3. Its `_VERSION` must read `"Lua 5.1"` — LuaJIT reports the language version, not its own.
4. All six of `Champion`, `Party`, `Dungeon`, `Map`, `Condition`, `Skill` must be present as tables.

Step 2 alone rules out essentially any accidental match; steps 3 and 4 then confirm it is *this*
game rather than some other Lua host. A stale static pointer therefore fails cleanly and falls
through to the sweep instead of handing the UI a plausible-looking wrong address. The verification
harness drives all of it over synthetic heaps: a pointer aimed at a table, a pointer aimed at a
coroutine, a Lua host with the wrong version, a Lua host with no Grimrock classes, a module with no
writable data section, and a process with no VM at all.

Also worth stating plainly: the field-name lookup walks the hash part **linearly** and compares
interned characters, rather than reimplementing LuaJIT's string hash and following the chain from
`hashmask(hash)`. A few hundred nodes cost one read, and a linear walk cannot disagree with the VM
about where a key lives — a hash reimplementation that is subtly wrong for one build would.

---

## 5. The game state graph

Everything below was read out of the running game. Types are as the VM holds them.

### `_G`

412 entries. The ones a trainer cares about:

| Key | Type | Meaning |
| --- | --- | --- |
| `party` | table | **the live party** — absent entirely at the main menu |
| `dungeon` | table | loaded dungeon: `maps`, `archs`, `spells`, `recipes`, `materials` |
| `gameMode` | table | `paused`, `map`, `timeMultiplier`, `hideGui`, FSM |
| `config` | table | `gameVersion`, `difficulty`, `oldSchoolMode`, key bindings, video settings |
| `charSheet`, `mapMode`, `gui`, `console` | table | UI modes |
| `Champion`, `Party`, `Map`, `Monster`, `Item`, `Spell`, `Skill`, `Talent`, `Condition` | table | class tables |
| `Stats`, `StatNames`, `ItemSlot`, `CellBits`, `CellBits2`, `Elements`, `Resistances`, `DamageFlags`, `DifficultyLevels` | table | enums |
| `SaveGameVersion` | number | `6` |
| `SaveGameMinSupportedVersion` | number | `4` |

`party` being absent at the main menu is the cleanest "no game loaded" signal the engine offers, and
the trainer uses exactly that rather than guessing from a mode flag.

### `party`

```
level  = 1            x = 2            y = 8            facing = 0
champions = <table>   map = <table>    statistics = <table>   knownSpells = <table>
FSM = <table>         torch = <udata>  lightEntity = <udata>  node = <udata>
camera = <udata>      cameraHeading/cameraPitch/cameraFov = numbers
controlsEnabled = true                 restingTimer = 0        flags = 0
```

`facing` is `0` north, `1` east, `2` south, `3` west. `x`/`y` are 0-based tile coordinates.

### `party.champions[1..4]`

```
name = "Contar Stoneskull"   sex = "male"        enabled = true      ordinal = 1
championIndex = 1            food = 750          skillPoints = 0     luck = 0
stats = <table>   conditions = <table>   skills = <table>   talents = <table>
class = <table>   race = <table>         items = <table>    runes = <table>
regenTimer, coolDownTimer = numbers      unarmedWeapon = <table>
```

### `champion.stats[name] = { name, value, max }`

Twelve entries, in the order the game's own `Stats` global lists them:

```
health  energy  strength  dexterity  vitality  willpower
protection  evasion  resist_fire  resist_cold  resist_poison  resist_shock
```

`StatNames` gives the labels: Health, Energy, Strength, Dexterity, Vitality, Willpower, Protection,
Evasion, Resist Fire, Resist Cold, Resist Poison, Resist Shock.

For `health` and `energy` the pair is current/maximum. For everything else Grimrock holds the same
number in both fields, which is why the trainer writes both together — raising only `value` on a
resource would draw the character-sheet bar past the end of its own track.

Live sample (a fresh default party, level 1):

| | Contar (Human Fighter) | Mork (Minotaur Fighter) | Yennica (Human Rogue) | Sancsaron (Human Mage) |
| --- | --- | --- | --- | --- |
| health | 65 | 92 | 52 | 42 |
| energy | 52 | 45 | 55 | 70 |
| strength | 16 | 20 | 11 | 10 |
| dexterity | 14 | 7 | 16 | 12 |
| vitality | 12 | 17 | 13 | 13 |
| willpower | 11 | 8 | 12 | 18 |
| protection | 1 | 0 | 0 | 0 |
| resist_fire | 0 | 0 | 0 | 25 |

Sancsaron's 25 fire resistance is the `fire_resistant` trait ("Daemon Ancestor", Resist Fire +25),
and Contar's 1 protection is the Armors skill's level-2 milestone (Protection +1) — the numbers
already have the character's traits and skills folded in, which is a useful cross-check that these
really are the effective stats the game fights with.

### `champion.conditions[name] = { name, uiName, value, timer, iconIndex, description, updateFunc }`

Eighteen entries. `value` non-zero means the champion has it; `timer` is remaining seconds.

| Key | Label | Effect (from the game's own description) |
| --- | --- | --- |
| `unused_skill_points` | Level Up | bookkeeping — lights the sheet's badge |
| `poison` | Poisoned | gradual health loss |
| `starving` | Starving | attack power halved; resting recovers nothing |
| `diseased` | Diseased | no health regeneration |
| `paralyzed` | Paralyzed | cannot attack or cast |
| `cursed` | Cursed | gains no experience |
| `blind` | Blind | accuracy −50 |
| `slow` | Slow | cool-down times doubled |
| `haste` | Hastened | cool-down times halved |
| `rage` | Rage | attack power +10, evasion −10 |
| `detect_monsters` | Detect Monsters | — |
| `burdened` | Burdened | slower movement, more food used |
| `overloaded` | Overloaded | cannot move |
| `fire_shield` | Fire Shield | Resist Fire +35 |
| `frost_shield` | Frost Shield | Resist Cold +35 |
| `poison_shield` | Poison Shield | Resist Poison +35 |
| `shock_shield` | Shock Shield | Resist Shock +35 |
| `invisibility` | Invisibility | enemies cannot see you |

`burdened`, `overloaded` and `unused_skill_points` carry no meaningful timer — the game recomputes
them from carried weight and level every frame. The trainer knows this and does not offer a duration
for them; clearing them works, and they come straight back if the champion really is overloaded.

### `champion.skills[i] = { name, level }`

An array holding only the skills a champion has actually trained. Levels run **0 to 50**, one point
per level. That is a real rule, not a guess: every `Skill.skills[*].upgrades` table tops out at
level 50 (Iron Body, Armor Master, Ninja Master, the four elemental Masteries), and the arithmetic
checks out on the live party — Contar is a Human (4 starting points) with the Skilled trait (+3) and
carries athletics 2 + armors 2 + swords 3 = 7.

The seventeen skills: `athletics`, `armors`, `dodge`, `swords`, `axes`, `maces`, `daggers`,
`unarmed_combat`, `assassination`, `staves`, `missile_weapons`, `throwing_weapons`, `spellcraft`,
`fire_magic`, `air_magic`, `ice_magic`, `earth_magic`.

### `champion.class` and `champion.race`

```
class = { name = "Fighter", level = 1, exp = 0, nextLevel = 850, health = 60, energy = 50, skills = {...} }
race  = { name = "Human", strength = 10, dexterity = 10, vitality = 10, willpower = 10,
          skillPoints = 4, foodRate = 1, description = "..." }
```

Level and experience live on the **class instance**, not on the champion.

Only the four races and classes actually in use exist in memory: the definitions are locals inside
`CharacterGeneration.lua` and the rest are collected. A trainer therefore cannot enumerate races or
classes from a running game, only read the ones the party chose.

### `dungeon.maps[1..13]`

```
name = "Into the Dark"   width = 32   height = 32   level = 1   visited = true
cells = <table>   cells2 = <table>   entities = <table>   objs = <table>   pvs = <table>
```

The shipped campaign, in order: Into the Dark, Old Tunnels, Pillars of Light, Archives, Hallways,
Trapped, Ancient Chambers, The Vault, Goromorg Temple I, Goromorg Temple II, The Tomb, The Prison,
The Cemetery.

`cells` is a Lua array of 1025 doubles for a 32×32 level, each a bitmask. Indexing is

```
cells[y * width + x + 1]        (x, y 0-based)
```

Confirmed live: the party standing at (2, 8) put the `DynamicObstacle` bit on `cells[8*32+2+1]`, and
moving it moved the bit.

`CellBits` (from the game's own global):

| Bit | Value | Meaning |
| --- | --- | --- |
| `Wall` | 1 | solid |
| `Obstacle` | 2 | static obstacle |
| `DynamicObstacle` | 4 | a moving body stands here — the party sets this on its own tile |
| `Pit` | 8 | pit |
| `PitOpen` | 16 | pit is open |
| `Pad` | 32 | pressure plate |
| `Altar` | 64 | altar |
| `DoorNorth/East/South/West` | 128 / 256 / 512 / 1024 | door on that side |
| `CustomFloor/Ceiling` | 2048 / 4096 | custom mesh |
| `CustomWall_North/East/South/West` | 8192 / 16384 / 32768 / 65536 | custom wall mesh |
| `StairsDown` / `StairsUp` | 131072 / 262144 | stairs |
| `StairExtensionDown` / `Up` | 524288 / 1048576 | stair extension |
| **`MapFloor`** | **2097152** | **automap: floor seen** |
| **`MapWall_North/East/South/West`** | **4194304 / 8388608 / 16777216 / 33554432** | **automap: wall seen** |
| `MapDoor_North/East/South/West` | 67108864 … 536870912 | automap: door seen |
| `MonsterBlocker` | 1073741824 | monsters cannot enter |

`cells2` is a second mask holding decoration and kill-pillar bits (`CellBits2`): `KillPillars_*`,
`KillWallDeco_*`, `Plants`, `StatueBase`, `CantDropItem`.

### `party.statistics.stats[i] = { name, uiName, value }`

Sixteen entries — the same list the end-of-game screen shows: play time, monsters killed, items
found, secrets found, treasures found, Toorum's notes found, skulls found, iron doors opened, tiles
moved, times fallen into pit, melee/ranged/unarmed attacks performed, rocks thrown, spells cast,
potions mixed.

### `dungeon.spells[name]`

Twenty entries, complete with rune strings. The letters read across the 3×3 rune board — `A B C`
top, `D E F` middle, `G H I` bottom.

| Spell | Skill | Skill level | Runes | Energy |
| --- | --- | --- | --- | --- |
| Fireburst | fire_magic | 2 | `A` | 15 |
| Enchant Fire Arrow | fire_magic | 7 | `ABFH` | 20 |
| Fireball | fire_magic | 13 | `ACF` | 33 |
| Fire Shield | fire_magic | 16 | `AE` | 50 |
| Shock | air_magic | 4 | `C` | 21 |
| Enchant Lightning Arrow | air_magic | 9 | `BCFH` | 20 |
| Lightning Bolt | air_magic | 14 | `CD` | 40 |
| Invisibility | air_magic | 19 | `CEH` | 35 |
| Shock Shield | air_magic | 22 | `CE` | 55 |
| Ice Shards | ice_magic | 3 | `GI` | 24 |
| Enchant Frost Arrow | ice_magic | 7 | `BFHI` | 20 |
| Frostbolt | ice_magic | 13 | `CI` | 29 |
| Frost Shield | ice_magic | 19 | `EI` | 45 |
| Poison Cloud | earth_magic | 3 | `G` | 17 |
| Poison Bolt | earth_magic | 7 | `CG` | 22 |
| Enchant Poison Arrow | earth_magic | 11 | `BFGH` | 20 |
| Poison Shield | earth_magic | 13 | `EG` | 35 |
| Light | spellcraft | 5 | `BE` | 25 |
| Darkness | spellcraft | 5 | `EH` | 25 |
| Powerbolt | air_magic | — | *(none)* | 0 |

Powerbolt carries no runes and no cost — it is not cast from the board, and the trainer's reference
tab says so rather than inventing a combination for it.

---

## 6. Rules recovered from Lua constants

LuaJIT keeps a function's numeric constants in the prototype's split constant array (`GCproto.k`,
`sizekn` doubles upward, `sizekgc` GC references downward). Reading them next to the string
constants recovers the shape of a formula even with line info stripped. These are honest
reconstructions of *which* constants a function uses, not decompiled source:

| Function | Numeric constants | String constants | Reading |
| --- | --- | --- | --- |
| `CharClass:expForLevel` | 1.37, 2, 850 | `math`, `pow`, `floor` | a `floor` over a `pow`; the level-2 threshold reads 850 XP live |
| `Champion:gainExp` | 1.25 | `spirit_mirror_pendant`, `isEquipped`, `cursed`, `hasCondition` | +25 % experience with the Spirit Mirror Pendant; none at all while cursed |
| `Champion:levelUp` | 10, 2 | `modifyStatCapacity` health, `modifyStat` energy/willpower/vitality, `addSkillPoints`, `random` | health and energy capacity go up, partly randomly, and skill points are granted |
| `Champion:regenerateHealthAndEnergy` | 0.2, 1.2, 0.6 | `diviner_cloak`, `brace_fortitude`, `diseased`, `starving` | no regeneration while diseased or starving; two items modify the rate |
| `Champion:consumeFood` | 0.9, 0.75, 1.2 | `endurance`, `race.foodRate`, `brace_fortitude`, `diviner_cloak` | the Endurance talent's −25 % is the 0.75 |
| `Champion:getMaxLoad` | 3, 15 | `strength`, `porter` | carrying capacity scales with 3 × Strength; Porter adds 15 kg |
| `Champion:updateEncumbrance` | 0.85 | `burdened`, `overloaded`, `getLoad`, `getMaxLoad` | Burdened begins at 85 % of capacity |

The Endurance and Porter numbers agree exactly with the talents' own description strings
("Decreases food consumption rate by 25%", "Increases carrying capacity by 15kg"), which is a
satisfying independent check on the reading.

---

## 7. The save format, decoded

Not needed by this trainer — it edits live memory — but decoded because it independently confirms
the data model.

```
offset 0   char[4]  "GRIM"
offset 4   uint32   version         6  (the Lua global SaveGameVersion also reads 6)
offset 8   uint32   uncompressed size
offset 12  ...      zlib stream (0x78 0x9C)
```

The inflated body is a tree of tagged chunks:

```
chunk  := char[4] tag; uint32 length; payload[length]
value  := uint32 type; payload
          type 0 = string  (uint32 length, then bytes)
          type 1 = number  (IEEE-754 double)
          type 2 = boolean (one byte)
          type 3 = nil     (no payload)
```

Those four types are exactly Lua's basic types, which is the format telling you what the engine is.

A shipped 253,993-byte autosave expands to 1,300,710 bytes and parses cleanly into 45,775 records:

```
DESC 1   OPTS 1   CHAR 4   CHAM 4   STAT 49  COND 72  SKIL 24  TALE 8   RUNE 4
ORDL 4   BSTA 24  CIMG 4   PRTY 1   QUAK 1   OBST 1   DREA 1   STVA 16  LGHT 1
ENTY 4078  ITEM 366  EXT1 897  TEXT 69  PCHN 76  ALWS 195  OPBY 4  DOOR 111
INIT 70  LEVL 13  CELL 13  NAME 13  WALL 13  STRM 13  MSGS 1  NXID 1
```

and the champion section reads back as exactly the live model:

```
[CHAR]
  num = 1
  [CHAM]
    str = 'Contar Stoneskull'   nil   num 0   num 0   num 750   bool true
    str = 'Human'   str = 'Fighter'   num 1 (level)   num 0 (exp)   num 850   str = 'male'
    [STAT] 'health' 65 65      [STAT] 'energy' 52 52      [STAT] 'strength' 16 16    …
    [COND] 'paralyzed' 0 0     [COND] 'poison' 0 0        …
    [SKIL] 'athletics' 2       [SKIL] 'armors' 2          [SKIL] 'swords' 3          …
    [TALE] 'skilled'           [TALE] 'athletic'
    [BSTA] 'health' 65 65      …   (base stats, before items)
```

`STAT` is the effective stat and `BSTA` the base — the only distinction the save makes that the live
tables do not surface separately, and worth knowing if a save editor is ever written. `LEVL`
chunks (13 of them) carry `CELL`, `NAME`, `WALL` and `STRM` sub-chunks; the `STRM` payloads are raw
binary and are the one place the generic parser bails out, which is expected rather than a defect.

---

## 8. What was confirmed against the live game

Everything in this document was read from a running `grimrock.exe` (PID 18904, module base
`0x00990000`). The specific end-to-end confirmations:

- **Both locator chains agree.** Static pointer: `L = 0x027501C0`, `_G = 0x02751318`, 6.03 ms.
  Signature sweep with the static chain deliberately disabled: same `L`, same `_G`, 8.8 ms across
  64 regions and 9 MB.
- **The whole party reads correctly** — four champions with names, races, classes, twelve stats
  each, trained skills, traits, and the level/experience pair off the class instance. Cross-checked
  against the autosave the game had written minutes earlier.
- **All thirteen dungeon levels enumerate** with the right names and 32×32 sizes.
- **Writes reach the game.** `champion.food` 750 → 999 → 750, read back at each step.
- **Teleport works and the occupancy bit follows it.** The party was stepped from (2, 8) to (2, 7)
  and back; `cells[8*32+2+1]` lost its `DynamicObstacle` bit, `cells[7*32+2+1]` gained it, and both
  returned exactly to their starting values afterwards. The game did not snap the position back,
  which is what proves `party.x`/`party.y` are the authority rather than a cache of some C++ state.
- **The build fingerprint matches.** PE machine `0x014C`, stamp `0x5115140B`, ASLR on, five sections
  in the expected order.

---

## 9. What this trainer deliberately does not do

Each of these is a decision, not an oversight.

- **No cross-level travel.** Writing `party.level` alone would leave the party pointing at a map it
  is no longer standing on: a level change in Grimrock also tears down and rebuilds the map through
  `Party:enterDungeon` / `Party:tearDownMap`. Same-level movement is offered; the stairs are for the
  rest.
- **No item spawning, no learning spells outright, no opening doors, no killing monsters.** All of
  these need the game's own Lua functions (`spawn`, `Party:discoverSpell`, `Door:open`,
  `Monster:die`) to run **inside the game's own thread**. Calling them from outside means injecting
  a thread and driving the Lua stack while the game is using it, which is a different and far more
  fragile kind of tool. The engine does expose developer helpers — `Developer.lua` defines global
  `gainExp`, `teleport`, `learnTalent`, `skipLevel`, `getStuff`, `dumpItems` — and the game's own
  console (`config.console`, toggled with the key in `consoleKey`) can call them. That is the right
  route for those effects, and the README says so.
- **No secret-door reveal.** "Reveal the map" sets the automap bits the game itself sets when you
  see a tile. Secret doors are wall tiles until the game converts them, so a map reveal does not and
  should not expose them.
- **No save editing.** The format is decoded above and the offsets would be shared with the live
  model, but a live trainer and an offline editor are different tools and only one was asked for.

---

## 10. Reproducing this

**Ghidra.** Version 12.1.2 headless. Two practical notes: copy the exe somewhere with no spaces or
apostrophes in the path first, and put the Ghidra project outside any dot-prefixed directory (it
refuses those).

```
analyzeHeadless.bat C:\GhidraWork\grimrock grimrock -import C:\GhidraWork\grimrock.exe \
    -processor x86:LE:32:default -cspec windows
```

Analysis takes about three minutes. LuaJIT's exported names give you a labelled Lua core for free,
which is what makes `luaL_newstate`'s caller — and from there the static `lua_State` slot — easy to
find. A post-script that prints the function containing an address, its callers, and the
cross-references to a data address is enough to reproduce section 4:

```java
Function f = getFunctionContaining(toAddr(0x0040BB75L));
println(f.getName() + " callers " + f.getCallingFunctions(monitor));
for (Reference r : getReferencesTo(toAddr(0x00588AB8L))) println(r.getFromAddress() + " " + r.getReferenceType());
```

**Live inspection.** Everything in sections 3, 5 and 6 came from `ReadProcessMemory` plus the
structure walker that is now `src/LegendOfGrimrock1Trainer/Lua/`. Start the game, load a save, and
run the trainer — the Reference tab restates the locator's reasoning, and the status bar reports
which chain answered and how long it took.

**Save format.** `zlib.decompress(data[12:])`, then the chunk/value grammar in section 7. A
seventy-line parser reproduces the tree.

---

## 11. Open ends

Honest list of what is *not* pinned down.

- **`champion.items`** — the slot table (`ItemSlot` gives `Head 1, Torso 2, Legs 3, Feet 4, Cloak 5,
  Neck 6, Weapon 7, OffHand 8, Gauntlets 9, Bracers 10, backpack 11..31`) is understood, but the
  probed game was at turn zero with empty inventories, so nothing was read from a populated one and
  the trainer does not surface items at all. `dungeon.archs` carries 414 archetypes including 81
  with an `attackPower`, so an inventory view is straightforward work once a save with gear is
  available.
- **`party.knownSpells`** was empty in the probed session, so the representation of a learned spell
  is inferred (a set keyed by spell name) rather than observed.
- **The two userdata payloads** (`party.node`, `party.torch`, `champion.portraitImage`) hold C++
  object pointers. They were identified as `GCudata` with `gct = 12` and an 8-byte payload holding a
  heap pointer and a code pointer, but the C++ classes behind them were not mapped, and nothing in
  this trainer touches them.
- **Torch fuel** lives on a torch *item*, which is an entity in `map.entities`, not on the party.
  Reachable in principle; not read here for the same reason as the inventory.
- **`CellBits2`** values are transcribed from the game's global but none were exercised.
