# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for **Alternate Reality: The City** (Datasoft /
Intellicreations, IBM PC conversion © 1987, 1988; original concept and program by Philip Price)
running under **DOSBox / DOSBox-X**. Like the repo's other DOS trainers it attaches to the
**emulator** process and reads the DOS guest's RAM mapped inside it; it is not a native-Windows
target like `ImperialismIITrainer` or `BeachHead2000Trainer`. Windows-only (WPF + Win32 memory APIs);
the app manifest requests administrator rights so it can `Read/WriteProcessMemory`.

## The target, and how it gave itself up

`AR.EXE` (16,652 bytes) is only a launcher — a Microsoft C 1986 program that asks for the display
adapter and joystick and then `exec`s one of `city` / `dungeon` / `arena` / `wilder` / `palace`. Skip
it: `CITY.EXE 1` starts the game in EGA, `CITY.EXE 0` in CGA.

`CITY.EXE` (332,160 bytes) is a plain, **unpacked**, relocatable MZ image — no EXEPACK, no PKLITE, no
DOS extender, 613 relocations, a 2,560-byte header, entry at `0000:0000`. It is also a large-model
1987 binary with no symbols, so Ghidra's auto-analysis recovers only ~138 functions out of 330 KB:
almost all control flow is far calls through relocated pointers. **The disassembly was not the
productive route** — do not start there.

What worked was the **text engine**. Every message is a byte-coded template terminated by `0xFF`, in
which literal text is plain ASCII *with tab (`0x09`) standing in for the space character*, and in
which `0xB0` / `0xB1` / `0xB2` / `0xB3` mean "print the u32 / u16 / u8 / NUL-terminated string at
`DGROUP:⟨operand⟩⟩`". Because the operand is a literal data-segment address, **the program contains a
symbol table for its own display variables.** Sweeping `CITY.EXE` for those opcodes and keeping the
hits whose operand lands inside the character buffer produces the whole field map mechanically.

Ghidra's segment `3d1a` is `DGROUP`; **`DGROUP` byte *n* is file offset `0x2DBA0 + n`**. Two
independent anchors agree on that base in a live session (the `Magical⟨tab⟩Flamesword` literal at
`DGROUP:0xC8DF`, and the eight-slot × 32-byte roster-name table at `DGROUP:0x118C` that `ARCNAME`
loads into).

## The character record

One 12,288-byte block, written to disk verbatim as `ARCCD`*nn* and living at **`DGROUP:0x4EB1`**. It
is the **working copy**, not a save snapshot: a live scan finds exactly one occurrence of the
attribute cells in the entire 222 MiB DOSBox process, and writing an attribute changes the attribute
the game uses. `ARCSP`*nn* (952 bytes) is a second block at `DGROUP:0x7EB2` holding per-location /
per-encounter state; it is **not decoded and never touched**.

Offsets that matter, all confirmed twice (against the game's own display template, then against the
running game):

| Offset | Type | Field |
| --- | --- | --- |
| `0x26`–`0x2B` | u8 ×4 + u16 | minute, hour, day, month index, year — **rewritten every tick, read-only** |
| `0x4C` | char[32] | name, NUL-padded ASCII |
| `0x6E` + `10×i` | 3 × u8 + u8 | the seven attributes: current, maximum, **natural** maximum, then a fractional accumulator |
| `0xC1` / `0xC2` | u8 / u32 | level / experience |
| `0xC6` / `0xCA` / `0xCE` | u32 | next-level threshold / hit points / hit points max |
| `0xD2`…`0xDA` | u16 ×5 | gold, silver, copper, precious gems, jewelry |
| `0xDE`…`0xE3` | u8 ×6 | food packets, water flasks, crystals, keys, compass, watch |

**Storage order is not display order.** The record stores `STR INT WIS SKL STA CHR SPD`; the status
bar prints `STA CHR STR INT WIS SKL` and has no column for Speed. `AttributeBook.DisplayOrder` holds
the mapping and `FormatCheck` asserts it reproduces the screen.

**The three attribute bytes are not interchangeable.** They read equal in every shipped save, which
is misleading: a Wraith's touch was seen live taking the current value *and* the maximum to 0 while
leaving the natural maximum at the rolled value (`0, 0, 9`). `LooksLikeRecord` therefore requires
only that neither of the first two exceeds the third — an earlier version demanded all three agree
and could not find a drained character at all, which is precisely when its owner wants a trainer.

**Do not write `0x30`–`0x45`.** Those two parallel five-word blocks are the hunger/thirst/weariness
meters; the game rewrites them every tick and reverts any edit within seconds.

**Map position is not known — but the search is unfinished, not settled.** What has been ruled out:
a coordinate pair and a map-pointer hypothesis over five one-step snapshots and a twenty-square walk,
every candidate constrained to a walkable square, and a write-test on `0x30`–`0x45` (which moves as
you walk and is the obvious suspect — writing it does not move the character). The only field that
moves on every step is the clock minute. **The catch:** all of that differenced a 384 KB window
around `DGROUP`, and the emulated RAM is one 16 MB region. A later whole-RAM pass with a
step-out/step-back filter also found nothing coordinate-shaped, but it could not complete a long
straight walk — the inn on North Main Street draws an encounter every few steps and an encounter
blocks walking *and* turning. So there is deliberately **no teleport and no "you are here" marker**,
and `docs/ReverseEngineering.md` §4.5 records exactly how far the search actually got.

## The street map

Recovered by scoring every 4,096-byte window in `CITY.EXE` against the 60 building squares the
shipped hint file names — the right window scored **92 of a possible 95**, the runner-up 59. That
search score also rewarded each building *type* getting its own code, which is why it exceeds 60; the
run-time check in `CityTerrain` is the plain 60-square count, which the real map passes at 57. It is at
file offset `0x279F0`, loads verbatim to **`DGROUP − 0x61B0`** (`CharacterFormat.DgroupTerrainOffset`)
and never changes while the game runs. One byte per square, `index = (64 − north) × 64 + (east − 1)`:
the low nibble is the location type (1 Inn, 2 Tavern, 3 Bank, 4 Shop, 5 Smithy, 6 scenery, 7 Healer,
8 Guild) and `0x40`/`0x20` mark a building block / a wall.

It has a sibling: a second 64 × 64 plane at file `0x269F0`, loaded to **`DGROUP − 0x71B0`**, holding
a **location ID** per square (99 distinct). A 28-entry name table at file `0x22336` turns those into
`City Square`, `Gold Alley`, `North Main Street`, `Stellar Maze`, `City Wall` and the rest, each
paired with one of seven sentence stems at `DGROUP:0x7416`–`0x7487` (`You are on `, `You are at the `
…) — that is how the status line is composed. Confirmed live: the plane matches the file byte for
byte, exactly one occurrence in 16 MB. The ID → name mapping is **not** decoded, and the trainer does
not use the plane yet; see `docs/ReverseEngineering.md` §4.7.

`CityTerrain` parses the street map and **refuses any block that does not explain at least 80 % of the
known building squares** (`MinimumKnownPlaceMatch`), so a bad read is never drawn as if it were the city.
The map is the game's data: it is read from the attached process, or from the player's own
`CITY.EXE`, and is **never committed**.

The full teardown — MZ header, segment map, text-engine opcode table, the live experiment log, the
other file formats — is in the committed `docs/ReverseEngineering.md`, alongside a play/strategy
guide with maps in `docs/StrategyGuide.md`. Both are also written into the game directory's own
`docs/` folder, which is where they were authored; the committed copies are what make every constant
in `CharacterFormat.cs` reviewable from the repository alone. If you change an offset, change both.

## Project Structure & Module Organization

Three projects in `AlternateRealityTrainer.sln`: the WPF app, its test harness, and the shared
`GameTrainers.Common` library it references (pulling both `GameTrainers.Common.Memory` and
`GameTrainers.Common.Mvvm` via csproj `<Using>` items — note their `ObservableObject` uses
`SetField`).

- `src/AlternateRealityTrainer/` — the WPF app (`AssemblyName` **`ARTrainer`**, `RootNamespace`
  `AlternateRealityTrainer`), layered by concern:
  - `Game/` — the game-knowledge layer, no UI and no process dependencies.
    - `CharacterFormat.cs` — the offset table above, the `[Confirmed]`/`[Inferred]` notes, the
      "max" caps, the locator anchors, the little-endian accessors, and `LooksLikeRecord`, the pure
      predicate the structural fallback and `FormatCheck` both use. `LiveFieldsLength` (`0xE4`) is
      the prefix that holds everything the trainer reads or validates — the poll loop and the
      structural scan both use it instead of the full 12 KB.
    - `CharacterRecord.cs` — typed mutable view over a caller-owned buffer. Every setter reports the
      exact byte range it touched through a `flush` delegate, so the shell writes 1–32 bytes rather
      than 12 KB; pass `null` for an offline view (which is how `FormatCheck` uses it).
    - `AttributeBook` / `CityBook` / `PotionBook` / `GameFacts` — pure reference data.
    - `CityMap.cs` — the drawn 64 × 64 location map: cell geometry, the per-kind palette, and a
      standalone SVG renderer. Both the WPF canvas and **Save map…** go through it, so the on-screen
      map and the exported one cannot disagree. North counts up from the southern edge, so row 1 is
      drawn at the *bottom* — `FormatCheck` pins that, because a mirrored map is the easy mistake.
      `MainWindow.xaml` tiles its grid brush with the literals `15` and `120`; the harness asserts
      they still equal `CellSize` and `CellSize × MajorEvery`.
  - `Memory/GameLocator.cs` — the auto-locate. Anchors on the status-bar header literal
    `Stats STA   CHR   STR   INT   WIS   SKL` (`DGROUP:0x012A`), derives `DGROUP:0000`, requires at
    least **two of three** further literals to line up at their own offsets *and* the record behind
    them to pass `LooksLikeRecord`, then returns it. The structural scan for that same shape is
    **opt-in** (`allowStructuralScan`, the *Scan anyway* button) and never runs by itself — over a
    couple of hundred megabytes of a process that is not the game it will eventually match some byte
    run, and it was seen offering a character called `wwwwwwwwww`. Measured live: **located in ~40 ms, 3/3 validators**. Windows are
    read with a needle-sized overlap and a per-page salvage path, so one unreadable page costs a page
    rather than a region.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` owns attach/detach/locate, the 600 ms poll
    loop, and `ICharacterHost`. `CharacterViewModel` deliberately keeps **two** views of the record:
    editable properties bound two-way to the UI, and a separate read-only *live mirror* the poll loop
    refreshes — so a background refresh can never fight a half-typed value in a text box. Freezes pin
    against the live mirror's values, not the editor's.
- `test/FormatCheck/` — headless harness, **417 checks** (387 without the shipped saves), exit 0/1.
  Runs with no game present.
  A final group parses the shipped `ARCCD*` files if it can find them (under `.game\`, or the DOSBox
  install the trainer was developed against) and **skips with a note** rather than failing when it
  cannot — those files are copyrighted and are not in the repository.

Dot-prefixed dirs (`.docs/`, `.game/`) are git-ignored by the root `.gitignore` (`.*/`); never commit
them.

## Build, Test, and Development Commands

- `.\Run.ps1` — restores, builds Release, launches `ARTrainer.exe` (a UAC prompt appears).
- Shared options, same as every other trainer: `-Configuration Debug|Release`, `-Clean`, `-NoBuild`,
  `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` builds and runs `FormatCheck` without launching the GUI.
- Direct: `dotnet build src\AlternateRealityTrainer\AlternateRealityTrainer.csproj -c Release`,
  `dotnet run --project test\FormatCheck`.

## Coding Style & Naming Conventions

Match the surrounding file: 4-space indent, file-scoped namespaces, `sealed` classes, PascalCase
members, `_camelCase` fields, `const` hex for offsets, XML `///` docs on public members. Keep every
reverse-engineered constant in `Game/` and out of the view-models. Follow read-validate-write: the
locator never returns an address it has not validated, and `CharacterRecord` clamps on the way in.

## Testing Guidelines

`FormatCheck` is the gate; keep it green. When you change an offset, add the assertion that pins it
to the `DGROUP` address the game's display template names — that is what makes a regression obvious
rather than silent. The GUI cannot be smoke-tested headlessly; the live path (attach → locate → read
every field → write → read back → restore) was exercised by hand against `CITY.EXE` under DOSBox-X.

## Commit & Pull Request Guidelines

Imperative, sentence-case subjects (join related changes with a semicolon). Say which game state a
change reads or writes and how it was confirmed against the live game.
