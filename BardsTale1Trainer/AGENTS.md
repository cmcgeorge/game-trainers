# AGENTS.md

Guidance for AI agents and contributors working in this repository.

## What this is

A WPF (.NET 8, Windows-only) **live-memory trainer** for the 1987 DOS game *The Bard's
Tale: Tales of the Unknown, Volume I* (Interplay's IBM port, `BARD.EXE`). It attaches to
the running game process (e.g. DOSBox / DOSBox-X), signature-scans its memory for the
game's data segment, and reads/writes the 7-slot party array live — HP, SP, attributes,
class, race, level, experience, gold, equipment, spell mastery, etc. — with freeze
toggles, "max" buttons and global hotkeys (Ctrl+F1/F2/F3). It can also open `.TPW` saved
characters for offline **editing** (Save .TPW writes back with a `.bak`), snapshot and
restore the whole party, copy/swap party slots, diff two memory dumps by address, and
track/teleport the party on per-area map grids.

Single-player cheat tool for the user's own save. It does not touch the network or any
external service. It was ported from the sibling `MightAndMagic1Trainer` solution, which
is the architectural template.

## Build / run / test

Always prefer the helper script at the repo root:

```powershell
.\run.ps1                       # build Release + launch (UAC prompt)
.\run.ps1 -Test -NoRun          # build + run format checks, no GUI
.\run.ps1 -Configuration Debug  # debug build then launch
.\run.ps1 -Clean                # wipe the app's bin/obj first
```

- Target framework: `net8.0-windows`; output assembly is `BT1Trainer.exe`.
- The app manifest requests **administrator** elevation (required for
  `ReadProcessMemory`/`WriteProcessMemory`). Launching always shows a UAC prompt.
- **You cannot smoke-test the GUI headlessly** — it needs an interactive desktop and a
  running game. Verify logic through `test\FormatCheck` instead, which runs against the
  bundled `.TPW` files in `docs\` (and, if present, the dump in `testdata\`) and needs
  neither admin nor the game.
- When verifying changes, run `.\run.ps1 -Test -NoRun` and confirm the build is clean
  (0 warnings/errors) and **all checks pass**.

## Layout

```
src/BardsTale1Trainer/
  Game/        PartyFormat.cs     field offsets, enums, DGROUP anchors, the slot predicate
               CharacterRecord.cs typed view over a 92-byte buffer (LE accessors + props + .TPW loader/writer)
               PartySnapshot.cs   7-block .TPW-format party snapshot build/parse (pure, tested)
               ItemBook.cs        the game's 126-entry item id→name table (1-based ids)
               Spellbook.cs       all 79 spells (art/level/4-letter code/name) + class→art map + bard songs
               MonsterBook.cs     127-entry bestiary: raw names verbatim from DS:0x2874 + markup decoder
               MapBook.cs         the 17 areas (city 30×30 + sixteen 22×22 dungeon levels)
               MapCalibration.cs  two-anchor pixel↔cell transform (pure, tested; verbatim from MM1)
               ClassBook.cs       class & race reference text
  Memory/      NativeMethods.cs   P/Invoke (OpenProcess, R/W ProcessMemory, VirtualQueryEx, SendInput, RegisterHotKey)
               ProcessMemory.cs   handle wrapper + region enumeration
               PartyLocator.cs    signature scanner -> data-segment base
               DumpComparer.cs    address-space diff of two dumps (pure, tested; verbatim from MM1)
               GlobalHotkeys.cs   RegisterHotKey wrapper + WM_HOTKEY dispatch (verbatim from MM1)
               BytePatternScanner.cs, MemorySearcher.cs, MemoryDumper.cs, KeyboardSender.cs (copied verbatim from MM1)
  Mvvm/        ObservableObject, RelayCommand
  ViewModels/  MainViewModel (attach/scan/freeze timer, .TPW save, snapshots, slot tools, hotkey entry points),
               CharacterViewModel, StatViewModel, ItemSlotViewModel, SpellLevelViewModel, HexByteViewModel,
               MemorySearchViewModel, PairSearchViewModel, MemoryDumpViewModel, DumpDiffViewModel (verbatim from MM1),
               MapReferenceViewModel (BT1 twist: renders labelled grid images instead of bundled scans),
               MonsterReferenceViewModel, SpellReferenceViewModel, ItemReferenceViewModel, ReferenceViewModels
  App.xaml, MainWindow.xaml(.cs)  dark two-pane UI: party list + Character/Inventory/Raw, Memory/XY/Dump/Maps, reference tabs
  Assets/app.ico
test/FormatCheck/                 headless assertions against docs/*.TPW & testdata dump
docs/                             the two sample .TPW characters (CHRISTOPHER, A R HELPER)
testdata/                         (gitignore-worthy) DOSBox-X memory .bin + region .csv — large, not committed
run.ps1                           build/test/launch helper
README.md                         user-facing docs + full format table
```

## Architecture notes

- **MVVM, no framework.** Hand-rolled `ObservableObject`/`RelayCommand`. Keep logic out of
  code-behind (`MainWindow.xaml.cs` only wires file dialogs + the X/Y grid edit hooks).
- **Single source of truth per character** is the `byte[92]` inside `CharacterRecord`.
  Friendly fields *and* the raw hex grid write into that buffer; any edit then pushes the
  affected bytes to process memory (when live) via `CharacterViewModel.PushRange/PushByte`.
- **Names are out-of-band.** Unlike MM1, the BT1 record has no name field — names live in
  the game's on-screen roster table (`DsPartyRows`, 7×37 bytes). `CharacterViewModel.NameAddress`
  holds the row address; `PushName` writes the 16-byte name there, `PullFromMemory` re-reads it.
  Offline `.TPW` loads carry the name from the file header instead.
- **Live vs offline.** `CharacterViewModel.IsLive` is true only when attached AND the record
  has a real `Address`. Pushes are a no-op otherwise, so the offline `.TPW` view never writes
  to memory. Offline edits are persisted only by **Save .TPW file(s)**: `MainViewModel`
  remembers each offline character's source path (`_tpwPaths`), backs the file up to `.bak`
  and writes `CharacterRecord.ToTpw()` (which sets the on-disk marker byte). Attaching
  rebuilds the character list and clears the paths, ending file mode.
- **Snapshots & slot tools.** `PartySnapshot` is seven 109-byte `.TPW` blocks in slot order;
  restore matches by slot, clears the disk marker for live targets, and pushes name + record
  through the normal property paths. Slot copy/swap moves record bytes AND the name — BT1
  records have **no slot-index byte** to fix up (unlike MM1); the per-slot identity lives in
  the roster-row address, which never moves.
- **Freeze loop.** A 150 ms `DispatcherTimer` in `MainViewModel` calls `ApplyFreezes()` on
  every character (HP/SP pinned to max; gold/experience are one-directional "no-loss"
  ratchets) and, every ~13 ticks, re-reads the whole party so the UI tracks the game.
- **Scanning is cancellable.** `MainViewModel` holds a `CancellationTokenSource`; Detach /
  re-scan cancel the previous scan. `ProcessMemory` wraps a `SafeProcessHandle`, so a detach
  mid-scan can't touch a freed handle.

## Critical domain knowledge — the record format & locator

This is the load-bearing fact of the project. **Read `PartyFormat.cs` before touching
anything format-related** and keep those constants the single source of the layout.

- A live character record is **92 bytes** (`RecordSize` 0x5C); the party is a **7-slot**
  packed array at `DsPartySlots` (0xD0BF) inside the game's data segment. Slot 0 is the
  special / summoned-monster slot.
- A `.TPW` file is **109 bytes**: 16-byte name + the 92-byte record + 1 pad byte. The
  record's byte 0 is `0x01` on disk, `0x00` live (`OffDiskMarker`).
- Multi-byte fields are **little-endian**. Attributes (St, IQ, Dx, Cn, Lk) are u16 pairs:
  five **max** at 0x07 then five **current** at 0x11. Inventory is 8 u16 words at 0x25 —
  bit 15 = equipped, low bits = a **1-based** id into `ItemBook.ItemNames` (0 = empty).
- Spell mastery is four bytes at 0x41 in the order **Magician, Conjurer, Sorcerer, Wizard**
  (confirmed: CHRISTOPHER the Conjurer has byte index 1 = 1). `PartyFormat.SpellLevelIndexForClass`
  maps a class id to this index; the casting classes are 6=Conjurer, 7=Magician, 8=Sorcerer, 9=Wizard.
- **The locator does not key off the record** (too loose). `PartyLocator` finds the data
  segment by scanning for the race-name table (`RaceTableBytes` at `DsRaceTable` 0x14A) and
  validating the item table (0x808) and class table (0xD91) at their fixed offsets, then
  sanity-checks slots 1..6 (slot 0, the summon/illusion slot, is skipped — its occupant
  can fall outside `LooksLikeSlot`'s mortal bounds). BARD.EXE is packed, so these strings exist only in the live,
  unpacked segment — keep that in mind before "verifying" against the on-disk EXE.
- If you change the format or the locator, re-run `FormatCheck` and confirm it still decodes
  both `.TPW` characters and (if the dump is present) the live slot-6 / row-6 CHRISTOPHER.
- **Bestiary.** Monster names live in the DS too: markup-carrying NUL-separated names at
  `DsMonsterNames` (0x2874) and a 127-entry u16 pointer table at `DsMonsterNamePtrs`
  (0x2F3E) defining monster-id order. `MonsterBook` holds the raw names verbatim;
  `FormatCheck` re-derives them from the dump and requires a byte-for-byte match. Per-monster
  stats (four parallel byte arrays at DS:0x19C3/0x1A43/0x1AC3/0x1B43, plus three 26-byte
  summon records at DS:0x2168) are located but **undecoded — do not guess values into
  MonsterBook**; decode against ground truth first.

## Conventions

- Match the surrounding style: file-scoped namespaces, nullable enabled, implicit usings,
  `nuint`/`UIntPtr` for addresses (never `int`), LE byte order everywhere.
- New record fields go in `PartyFormat` (offset const) → `CharacterRecord` (typed property)
  → `CharacterViewModel` (bound property + push + a `LabelFor` hex label + a notification) →
  XAML. Add an assertion to `FormatCheck/Program.cs`.
- Keep `FormatCheck` green and the build warning-free. The project is intentionally
  dependency-free (BCL + WPF only); don't add packages without good reason.

## Gotchas

- The `Bash` and `PowerShell` tools may start in different working directories; use absolute
  paths or `$PSScriptRoot`.
- The sample files are binary. Use the format types or a hex view, not text reads.
- Offline `.TPW` saving is deliberate and **must** keep its safety rails: write only to the
  file the character was loaded from, and always copy the previous version to `.bak` first.
- The memory dump in `testdata/` is ~380 MB — handy for `FormatCheck` but should not be
  committed. `FormatCheck` skips the dump checks gracefully when it's absent.
- Several `Memory/` and `ViewModels/` files (the scanner, dumper, key sender, value/pair
  search) were copied verbatim from `MightAndMagic1Trainer` with only the namespace renamed —
  they are game-agnostic. Fixes worth keeping should ideally land in both.
