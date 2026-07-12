# War of the Lance — Live Trainer

A WPF (.NET 8) trainer for the 1989 SSI DOS strategy wargame **War of the Lance** (a Dragonlance /
AD&D title). It attaches to the running game (inside DOSBox / DOSBox-X), locates the unit
current-strength table in the emulated memory, and lets you edit each unit's live strength — with a
per-unit **freeze** toggle and one-click **Max all** / **Restore base** actions.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

The formats it reads were recovered by static reverse-engineering of the shipped data files in
`.game/` (the container header, the high-bit-ASCII string tables, and the unit tables), cross-checked
against the bundled manual (`.game/warlance.txt`). The parsers are regression-tested against verbatim
byte slices captured from those files (see [Verified against the game files](#verified-against-the-game-files)).

---

## Quick start

1. **Launch War of the Lance** in DOSBox/DOSBox-X and play past the title screen into a loaded
   campaign or scenario (the strength table only lives in memory once a game is running).
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `WOTLTrainer.exe`, which requests administrator rights via UAC —
   reading/writing another process's memory needs them, especially if the emulator is elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/ScummVM/etc. are auto-sorted to
   the top) and click **Attach**. It scans memory and lists the located units automatically.
4. **Edit:** change a unit's **Strength** cell, tick **Freeze** to pin it, or use **Max all (240)** /
   **Restore base**. Edits are written to the game *immediately*.

If the scan finds nothing, make sure a game is actually loaded, then click **Re-scan**.

---

## What it can edit

The trainer locates and edits the leading **current-strength block** of the WL.DAT / SCEN.DAT working
buffer — one byte of live strength per unit for the Highlord assault force and Neraka's core (29
cells: 9 Baaz draconians, 10 Kapak draconians, 8 mercenary infantry, 2 mercenary cavalry). Each cell
is labelled from the manual's unit appendix (side, nation, unit type, campaign-start base number).

### Freeze toggles

Each unit row has a **Freeze** checkbox, plus a toolbar **Freeze all strengths** master toggle. While
a unit is frozen the poll loop re-writes its current strength every tick (600 ms), so combat losses
can't lower it. Toggle it off to let the value move again.

### Quick actions

- **Max all (240)** — sets every located unit to the engine's ceiling (240, the Griffon base number).
- **Restore base** — resets every unit to its campaign-start base number.

A value you type is clamped to `1..240` on write (240 is the engine's highest base number, and the
clamp stops a stray keystroke from zeroing a unit that is still on the map). A live value read from
RAM keeps its real `0..240` range, so a destroyed unit shows as `0` and stays `0` when frozen instead
of being resurrected.

---

## How it finds the units

The buffer's live address changes every DOSBox session, so the trainer never hard-codes it. Instead
it uses two independent anchors (`Memory/GameLocator.cs`):

- The **nation-name table** (NAT.DAT, high-bit ASCII) loads into guest RAM byte-for-byte identical to
  the shipped file, so its head is the required "the game is loaded" gate — if it isn't present the
  scan reports nothing and no editable rows appear, so a coincidental match in an unrelated process
  can't produce writes into arbitrary memory.
- The **current-strength block** is found by anchoring on the short, constant qualities/base-number
  run that immediately follows it (`3,3,3,3,4,4,4,5,110,110,110,110,20,20,20,20,110,110,110,110,20,20,20,20`)
  and stepping back a fixed 29-byte delta — the same anchor+delta approach the sibling Dragon Wars
  trainer uses. A candidate is accepted only when every cell is `≤ 240` and at least one is non-zero
  (a single destroyed unit can legitimately read `0` mid-campaign, but an all-zero run is never a
  live army).

---

## Verified against the game files

The formats aren't guessed. They were derived by static analysis of the shipped files and confirmed
byte-for-byte by the `FormatCheck` harness, which embeds verbatim slices of NAT.DAT, MENU.DAT, WL.UNT
and the head of WL.DAT:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` asserts the container header (`0xFD` magic + `fileLen == payloadLen + 7` invariant), the
high-bit-ASCII codec round-trip (and its rejection of non-ASCII input), the 28-entry nation table, the
five season labels, the fixed 400-slot `.UNT` unit table (including rejection of wrong-sized
payloads), and the strength-block base-number run + locator signature. It also exercises the strength
view-model headlessly: the `1..240` edit clamp, the preservation of a destroyed unit's live `0` (so
freezing never resurrects it), and the write-failure report path. It exits 0 (pass) or 1 (fail).

---

## Project layout

```
src/WarOfTheLanceTrainer/
  Game/        SaveContainer.cs   the 7-byte container header (magic/checksum/tag/length) + validation
               GameText.cs        the high-bit-ASCII string codec (0xFF-separated words)
               GameFacts.cs       verified constants: nation/side/unit/terrain/season tables, limits
               UnitTable.cs       the 400-slot, 4-bytes-per-slot .UNT placement table parser
               StrengthTable.cs   the 29 current-strength cells, labels, and locator signature/delta
  Memory/      GameLocator.cs     nation-table + strength-block anchor scanner → live addresses
               (shared)           ProcessMemory / BytePatternScanner — from GameTrainers.Common.Memory
  ViewModels/  MainViewModel, UnitStrengthViewModel, IStrengthHost
  App.xaml, MainWindow.xaml       the WPF UI
test/FormatCheck/                 headless verification against verbatim game-file byte slices
.docs/         reverse-engineering notes + strategy guide
.game/         the game itself (copyrighted; supply your own copy)
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer come from the
shared `GameTrainers.Common` library rather than being duplicated here.

---

## Notes & caveats

- **Reverse-engineering method.** Ghidra was not available in this environment, so the formats were
  recovered by static analysis of the shipped data files and the bundled manual rather than by
  disassembling the executable or dumping live RAM. The file-format findings are verified byte-exact
  by `FormatCheck`; the live attach/scan path needs the game running to exercise.
- The trainer is deliberately scoped to the **verified** leading 29-cell strength block. A WL.DAT vs.
  SCEN.DAT diff showed the deeper per-unit record layout is interleaved and can't be aligned reliably
  without live memory dumps, so it is intentionally left out rather than guessed.
- Edits take effect the next time the game reads the strength (e.g. the next combat resolution).
- Some emulators map guest RAM more than once; the anchor scan uses the first match whose 29 cells
  look like a real strength block.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
```
