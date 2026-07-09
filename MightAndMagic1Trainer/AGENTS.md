# AGENTS.md

Guidance for AI agents and contributors working in this repository.

## What this is

A WPF (.NET 8, Windows-only) **live-memory trainer** for the 1986 DOS game
*Might & Magic Book One*. It attaches to the running game process (e.g. DOSBox),
signature-scans its memory for the character roster, and reads/writes character
records live — HP, SP, attributes, gold, gems, food, condition, etc. — with
freeze toggles and "max" buttons. It can also open a `ROSTER.DTA` save file for
**read-only** offline inspection.

It also carries a small **reverse-engineering toolkit** built from a Ghidra study of
`Mm.exe`: a fixed-offset **data-segment reader** (`DataSegment`), a byte-exact port of
the game's **LFSR RNG** driving a live **roll predictor**, a **maze decoder** that
renders any of the 55 mazes as a vector auto-map with live current-map detection, and
an **auto-fight** loop. The methods behind these — the DS offset map, combat/HP/XP
formulas, the `Mazedata.dta` and `.ovr` formats, and a remaster design — are written
up under `docs/` (each claim tagged Confirmed / Inferred / Candidate).

Single-player cheat tool for the user's own save. It does not touch the network
or any external service.

## Build / run / test

Always prefer the helper script at the repo root:

```powershell
.\run.ps1                       # build Release + launch (UAC prompt)
.\run.ps1 -Test -NoRun          # build + run format checks, no GUI
.\run.ps1 -Configuration Debug  # debug build then launch
.\run.ps1 -Clean                # wipe the app's bin/obj first (not the test project's)
```

Equivalent raw commands:

```powershell
dotnet build -c Release
dotnet run --project test\FormatCheck      # the verification harness
```

- Target framework: `net8.0-windows`; output assembly is `MM1Trainer.exe`.
- The app manifest requests **administrator** elevation (required for
  `ReadProcessMemory`/`WriteProcessMemory`). Launching always shows a UAC prompt.
- **You cannot smoke-test the GUI headlessly** — it needs an interactive desktop
  and a running game. Verify logic through `test\FormatCheck` instead, which runs
  against the sample files in `docs\` and needs neither admin nor the game.
- When verifying changes, run `.\run.ps1 -Test -NoRun` and confirm the
  build is clean (0 warnings/errors) and **all checks pass**.

## Layout

```
src/MightAndMagic1Trainer/
  Game/        RosterFormat.cs    field offsets, enums, the record signature predicate
               CharacterRecord.cs typed view over a 127-byte buffer (LE accessors + props)
  Memory/      RosterLocator.cs   signature scanner -> roster base address (game-specific, local)
               RollScanner.cs     signature-scan for the temporary create-screen roll buffer
                                  (7 stat bytes at stride 1 or 2) + read it back (game-specific, local)
               DataSegment.cs     locates DGROUP (string anchor + a second validation string) and
                                  reads/writes globals at fixed DS-relative offsets (docs/offset-map.md)
                                  (game-specific, local)
               (shared)           NativeMethods (P/Invoke), ProcessMemory (handle wrapper + region
                                  enumeration), KeyboardSender (focus + SendInput key replay),
                                  MemorySearcher (Cheat-Engine-style value scanner), MemoryDumper,
                                  DumpComparer, BytePatternScanner, GlobalHotkeys — now in
                                  GameTrainers.Common.Memory (imported via a global using)
  (Mvvm/       ObservableObject, RelayCommand now live in GameTrainers.Common.Mvvm)
  ViewModels/  MainViewModel (attach/scan/freeze timer), CharacterViewModel,
               StatViewModel, ItemSlotViewModel (one inventory slot: item id + charges + charge freeze),
               HexByteViewModel,
               DrawnMapViewModel (renders a decoded maze + live party cell + click/typed teleport;
                                  fingerprints the live map; self-learns the party-position DS offset),
               AutoCombatViewModel (auto-fight: replays a key sequence while DataSegment.InCombat),
               RollPredictorViewModel (live LFSR state -> predicted next rolls per die),
               SpellMacrosViewModel (spellbook "Cast a spell" picker + quick-cast macros, persisted to %APPDATA%),
               MemorySearchViewModel (drives MemorySearcher; off-roster edits e.g. position),
               CharacterRollerViewModel (auto re-rolls a new character: locks the create-screen
                                  roll buffer via RollScanner, taps Enter, reads each roll, stops on target)
  Game/Spellbook.cs  MM1 spell tables (level/number/SP/gem/description) + class→school map
  Game/ItemBook.cs   curated item reference list + the game's 255-entry id→name table
                     (extracted from MM.EXE; slot bytes are 1-based indices into it)
  Game/ItemEffectBook.cs  per-item equip effect (resistance/attribute/AC/curse) + class&alignment
                     usability for the Items tab; transcribed from the MM1 item FAQ and name-joined
                     to ItemBook (254/255 ids; OBSIDIAN BOW id 85 has no entry)
  Game/Lfsr.cs       byte-exact 32-bit LFSR rand(n) (taps 27/30, rejection sampling -> [1,n]); SelfTest vectors
  Game/MazeData.cs   decodes Mazedata.dta (55×512: two co-registered 16×16 wall planes) -> MazeMap;
                     plane-1 fingerprinting identifies the live map
  App.xaml, MainWindow.xaml(.cs)  dark two-pane UI: party list + Characters/Spells/Memory tabs
  SpellReferenceWindow.xaml(.cs)  modeless pop-up: all Cleric/Sorcerer spells + descriptions
                                  (SpellReferenceViewModel; opened from the Spells tab button)
  Assets/app.ico                  application + window icon (gold gem on a dark tile)
test/FormatCheck/                 headless assertions against docs/Roster.dta & MM.CEM
tools/IconGen/                    dev-only WPF tool that regenerates Assets/app.ico (not in the .sln)
docs/                             sample Roster.dta + MM.CEM (Cheat Engine memory dump), plus the
                                  reverse-engineering references: offset-map.md (+ offset-map-globals.txt),
                                  formulas.md, maze-atlas.md, ovr-format.md / ovr-events.md, remaster-design.md
run.ps1                           build/test/launch helper
README.md                         user-facing docs + full format table
```

## Architecture notes

- **MVVM, no framework.** Hand-rolled `ObservableObject`/`RelayCommand`, now shared
  across the MM1-family trainers via `GameTrainers.Common.Mvvm`. View
  models raise `PropertyChanged`; the View binds. Keep logic out of code-behind
  (`MainWindow.xaml.cs` only wires the file-open dialog + DataContext).
- **Single source of truth per character** is the `byte[127]` inside
  `CharacterRecord`. Both the friendly fields *and* the raw hex grid write into
  that same buffer; any edit then pushes the affected bytes to process memory
  (when live) via `CharacterViewModel.PushRange/PushByte`.
- **Live vs offline.** `CharacterViewModel.IsLive` is true only when attached AND
  the record has a real `Address`. Pushes are a no-op otherwise, so the offline
  `Load Roster.dta` view never writes anywhere.
- **Freeze loop.** A 150 ms `DispatcherTimer` in `MainViewModel` calls
  `ApplyFreezes()` on every character (re-writing frozen HP/SP), then optionally
  `PullFromMemory()` on the selected character if "Live refresh" is on **and** that
  character `IsLive` (the guard is at the call site, not just inside the method).
- **Scanning is cancellable.** `MainViewModel` holds a `CancellationTokenSource`;
  `Detach`/re-scan cancel the previous scan. Don't reintroduce an uncancellable
  `Task.Run` — a detach mid-scan must not write results against a disposed handle.
- **Inventory & charge freeze.** Each character has 12 item slots — 6 equipped
  (`OffEquipment` 0x40) + 6 backpack (`OffBackpack` 0x46) — with a parallel pair of
  charge-count arrays (`OffEquipmentCharges` 0x4C, `OffBackpackCharges` 0x52, 6 bytes
  each; resistances follow at 0x58). `ItemSlotViewModel` exposes each slot's item id +
  charges (single bytes written via the owner's `PushByte`) and a `FreezeCharges` flag.
  The item picker uses `ItemBook.Choices` (id 0 = empty + the 255 names); the slot byte is
  a 1-based index into `ItemBook.ItemNames`. The charge freeze is a hard pin (not a no-loss
  ratchet): `ApplyChargeFreeze` rewrites the live byte from the buffer each tick, so the
  displayed value is the freeze target.
- **No-loss freezes.** Gold/gems/food use a one-directional "ratchet" in
  `CharacterViewModel.ApplyNoLoss` (run from the same timer tick): a rise raises the
  high-water mark, a drop is rewritten back up. Toggling the flag resets the mark so
  it re-baselines. This differs from HP/SP, which are pinned straight to max.
- **Cast macros / spellbook** (`SpellMacrosViewModel` + `KeyboardSender`) replay
  keystrokes into the *attached* emulator window via `SendInput` (DOSBox ignores
  posted messages). The send runs on a background `Task`; status updates marshal back
  on the captured UI `SynchronizationContext` (don't touch bound properties from the
  `Task.Run` body). The **Cast a spell** picker derives the key walk
  `{slot+1} c {level} {number} {ENTER}` from `Spellbook.cs` using the selected
  character's class (→ school) and known spell level (offset 0x2F); `MainViewModel`
  pushes the selection in via `SetCaster`.
- **Memory search** (`MemorySearchViewModel` + `MemorySearcher`) is a Cheat-Engine-lite
  scanner for state the roster format doesn't cover (party position/facing). Unknown-value
  first scans keep a raw byte *baseline* (not a per-position list — that would OOM); the
  first relative narrowing materialises the candidate list. `MainViewModel.Detach` resets it.
- **Character roller** (`CharacterRollerViewModel` + `RollScanner` + `KeyboardSender`).
  The CREATE NEW CHARACTERS roll is a *temporary* buffer, not a roster record, so the roster
  scan can't see it. Flow: the user types the 7 on-screen numbers; `RollScanner.Find`
  signature-scans memory for them at stride 1 (contiguous) or 2 ([normal,active] pairs);
  multiple hits are narrowed by re-rolling and keeping the candidate that stays in stat range
  *and* changes. Once locked, the roll loop (background `Task`, like the cast macros) taps
  `{ENTER}`, reads the new stats via `RollScanner.TryReadStats`, and stops when every stat is at
  or above its own per-stat minimum (`RollStatViewModel.Minimum`; 0 = unconstrained). UI updates marshal through the captured
  `SynchronizationContext` (`OnUi`); commands re-query on attach (`RefreshCommands`) and the
  lock is dropped on detach (`Reset`) since the address belongs to the old process.
  - **Roller statistics** (`ThreeD6` + `RollHistory`, both pure/testable in `FormatCheck`).
    `ThreeD6` is the 3d6 odds model (all 216 outcomes enumerated): `PAtLeast`/`PMeetsAll` drive the
    live `OddsText` for the current minimums. `RollHistory` accumulates each fresh roll (deduping a
    back-to-back identical read as stale) into per-stat and total mean/range/σ, and `Assess` compares
    the total against fair 3d6 (mean 73.5, σ≈7.83) to flag `LikelyConstrained` (spread far below dice),
    `LikelyBiased`, `OutOfRange`, or `ConsistentWith3d6`. The roll loop owns the `RollHistory`, mutates
    it only on the read thread, and posts immutable `RollStatsSnapshot`s to the UI via `OnUi`; the tally
    resets with the lock.
- **Data segment (fixed offsets).** `DataSegment` finds DGROUP once — anchors on a unique
  static string (`"FOR DEFEATING THE MONSTERS"` @ DS:0x3062), validated by a second
  (`"ROUND #:"` @ DS:0x3632) so a stray on-disk image can't masquerade — then reads/writes
  globals as `base + offset` with no per-value scan. The offsets are catalogued, with
  confidence tags, in `docs/offset-map.md`; keep the `Off*` consts the single source and cite
  the doc. `InCombat` is the game's own gate, `[0xC5DC + activeCharIndex] & 2` (not the old
  `0x3BCF` guess, which is actually byte 1 of the RNG state).
- **RNG roll predictor.** `Lfsr` is a byte-exact port of `rand(n)` (32-bit LFSR, feedback bits
  27/30, rejection sampling to `[1, n]`); its `SelfTest` vectors run in a `Debug.Assert` at
  predictor construction — keep them green if you touch the algorithm. `RollPredictorViewModel`
  reads the live LFSR state (`DS:0x3BCE`) each tick and predicts the next rolls per die, but
  **clamps the live retry byte** (`DS:0x3BD3`, normally 4) at the VM boundary: a 0 there would
  send the faithful `Rand` down a 65,536-shift path on the UI thread. It also short-circuits a
  tick when `(state, retry)` is unchanged (predictions are a pure function of those).
- **Drawn auto-map.** `MazeData` decodes `Mazedata.dta` (55×512 = two co-registered 16×16
  planes, 2 bits/direction, W/N/E/S); `DrawnMapViewModel` renders it as **frozen** vector
  geometry (one build per map change), identifies the current map by fingerprinting the live
  maze buffer against the 55 plane-1 records (the ~96 KB scan runs on a pool thread with a
  UI-thread continuation, one scan at a time), overlays the party cell, and teleports (clamped
  0–15). It self-derives the party-position DS offset from a one-time 📍 X/Y lock and persists
  it to `%APPDATA%\MM1Trainer\drawnmap.json`; an out-of-range read drops a stale offset and
  suppresses re-learning it until a valid coordinate reads again (so a bad lock can't spam the
  settings file).
- **Auto-fight.** `AutoCombatViewModel` replays a key sequence (via `KeyboardSender`, like the
  cast macros) on a background `Task` while `DataSegment.InCombat` holds, observing cancellation
  through the token's `WaitHandle` (the CTS is cancelled but not disposed under the waiter). The
  combat gate is **reverse-engineered but not yet confirmed live** — the UI says so; don't trust
  auto-fight unattended without watching the readout flip.
- **Reverse-engineering docs.** `docs/` holds the decoded references the toolkit is built on:
  `offset-map.md` (+ the machine-generated `offset-map-globals.txt`), `formulas.md` (XP/HP/combat
  math), `maze-atlas.md` (every decoded maze), `ovr-format.md`/`ovr-events.md` (the `.ovr`
  event-overlay format + extracted content), and `remaster-design.md` (a Godot remaster proposal).
  Each tags confidence (Confirmed / Inferred / Candidate); treat Candidate rows as leads, not
  gospel, and prefer extracting facts from `Mm.exe` over FAQs.

## Critical domain knowledge — the record format

This is the load-bearing fact of the project. **Read `RosterFormat.cs` before
touching anything format-related** and keep these constants the single source of
the layout (don't hardcode offsets elsewhere).

- A character record is **127 bytes** (`RecordSize` / `FileStride`).
- On disk (`ROSTER.DTA`) records are packed every **127** bytes.
- In live memory they are padded to a **128**-byte stride (`MemoryStride`) — the
  scanner and slot walker use 128, the file loader uses 127. This 1-byte
  difference is real and confirmed by `docs/MM.CEM`; don't "fix" it.
- Up to `MaxSlots` (18) characters; the sample data has 6.
- Multi-byte fields are **little-endian**: gold u24 @0x39, gems u16 @0x31,
  SP u16 @0x2B/0x2D, HP u16 @0x33/0x35/0x37, experience u32 @0x27.
- Attributes (@0x15, 7 of them) are `[normal, active]` byte pairs — `normal` is
  permanent, `active` (labelled "current" in the raw hex grid) is the value the
  game uses and what resting restores.
- Condition @0x3F: `0` = OK.
- The offset table was cross-checked against
  [github.com/ryz/MightAndMagic-SaveEditor](https://github.com/ryz/MightAndMagic-SaveEditor)
  (`Character.cs`). See README for the full annotated table. Fields marked
  best-effort (e.g. exact condition names) are uncertain — verify before relying.
- The memory scanner (`RosterLocator` + `RosterFormat.LooksLikeRecord`) keys off a
  valid name, class 1–6, sex 1–2, and a constant marker
  (`MarkerByteA`@0x62/0x63). The old `0x70`==`0x24` marker was dropped: that tail
  byte varies with game state (0x00 on freshly rolled characters), which made the
  loader reject brand-new rosters. If you change record validation,
  re-confirm `FormatCheck` still detects exactly 6 records in both files and still
  rejects the `CHEATENGINE` header in `MM.CEM`.

## Conventions

- Match the surrounding style: file-scoped namespaces, nullable enabled, implicit
  usings, `nuint`/`UIntPtr` for addresses (never `int`), LE byte order everywhere.
- New record fields go in `RosterFormat` (offset const) → `CharacterRecord`
  (typed property) → `CharacterViewModel` (bound property + push + a hex label in
  `LabelFor` + a `PropertyChanged`/`RaiseAll` notification so the view updates) →
  XAML. Add an assertion to `FormatCheck/Program.cs`.
- Keep `FormatCheck` green and the build warning-free.
- Don't add packages or change target framework without good reason; the project
  is intentionally dependency-free (BCL + WPF only).

## Gotchas

- The `Bash` tool and `PowerShell` tool may start in different working
  directories here; use absolute paths or `$PSScriptRoot`.
- Reading the sample files: they're binary. Use the format types or a hex view,
  not text reads.
- Don't overwrite a user's real `ROSTER.DTA`. The offline path is read-only by
  design; keep it that way unless explicitly asked to add saving.
