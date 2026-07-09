# Dragon Wars — Live Trainer

A WPF (.NET 8) trainer for the 1989 Interplay DOS RPG **Dragon Wars**. It attaches to the
running game (inside DOSBox / DOSBox-X), locates the party roster in the emulated memory, and
lets you edit every character live — the four attributes, all 27 skills, Health/Stun/Power,
level, experience, gold, combat values (AV/DV/AC), name and gender — with per-vital **freeze**
toggles and one-click **max** actions, both per-character and party-wide.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

The record layout it edits was recovered by reverse-engineering two DOSBox-X memory dumps of a
live party (start-of-game vs. in-combat) and cross-checked against the open-source
`fraterrisus/dragonjars` reimplementation. The parser is regression-tested against verbatim
512-byte records captured from those dumps (see [Verified against the real game](#verified-against-the-real-game)).

---

## Quick start

1. **Launch Dragon Wars** in DOSBox/DOSBox-X and play until a party exists (past the title
   screen — the roster only lives in memory once characters are loaded).
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `DWTrainer.exe`, which requests administrator rights via UAC —
   reading/writing another process's memory needs them, especially if the emulator is elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/ScummVM/etc. are auto-sorted to
   the top) and click **Attach**. It scans memory and lists the party automatically.
4. **Edit:** select a character on the left, then change any field on the right. Edits are written
   to the game *immediately* (they take effect when the game next reads the field — e.g. opening
   the character screen in-game).

If the scan finds nothing, make sure a party is actually loaded, then click **Re-scan**.

---

## What it can edit

The trainer decodes the full **512-byte** Dragon Wars character record:

- **Identity** — name (Dragon Wars high-bit string encoding), gender, level, experience, gold.
- **Attributes** — Strength, Dexterity, Intelligence, Spirit (current and base set together).
- **Vitals** — Health, Stun, and Power, each current/max.
- **Combat** — Armor Value (AV), Defense Value (DV), Armor Class (AC).
- **Skills** — all 27 skill ranks (Arcane Lore … Thrown Weapons).
- **Spells** — the eight spell-bitfield bytes, via the **Learn All Spells** action.

### Freeze toggles

The toolbar has party-wide **Freeze Health**, **Freeze Stun**, and **Freeze Power** checkboxes
(and each is applied per-character). While a vital is frozen the poll loop re-pins its current
value to its max every tick, so it never drops in play. Toggle it off to let the value move again.

### Quick actions

- **Party-wide** (toolbar): Heal Party, Max Attributes, Learn Spells, Max Money, Max Everything.
- **Per-character** (below the character sheet): Full Heal, Max Attributes, Max Skills,
  Learn All Spells, Max Money, Max Everything.

"Max" targets are conservative safe caps: attributes 99, skills 60, Health/Stun/Power 999, gold
999,999.

---

## How it finds the party

The roster's live address changes every DOSBox session, so the trainer never hard-codes it.
Instead it **anchors** to a unique byte run — the first 48 bytes of `DATA1`'s chunk-0 header,
which load verbatim into guest RAM and appear exactly once — and reads the roster at a fixed
delta (`0x0D0E`) past that anchor (`Memory/RosterLocator.cs`). The roster is an array of seven
512-byte slots; occupied slots (validated by a plausible name and Health max) are listed.

---

## Verified against the real game

The record layout isn't guessed. It was derived by differential analysis of two DOSBox-X memory
dumps of a live party and confirmed against the `fraterrisus/dragonjars` field tables. The parser
is regression-tested against verbatim 512-byte records captured from those dumps:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` asserts the decoded sample party — the opening four characters — e.g.:

```
Muskels   STR 21 DEX 20 INT 10 SPR 10   HP 16/16  Stun 16/16  Pow 0/0    AV 5 DV 5 AC 0
Elendil   STR 10 DEX 16 INT 12 SPR 14   HP 12/12  Stun 12/12  Pow 28/28  AV 4 DV 4 AC 0
```

plus the name encode/decode round-trip and occupied/empty slot detection, and exits 0 (pass) or
1 (fail).

---

## Project layout

```
src/DragonWarsTrainer/
  Game/        RosterFormat.cs      the validated 512-byte offset table, anchor + delta, and name tables
               CharacterRecord.cs   typed, mutable view over a 512-byte buffer (DW name encoding, cur/base pairs)
  Memory/      RosterLocator.cs     anchor scanner (DATA1 header + fixed delta) → the seven roster slots
               (shared)             ProcessMemory / MemoryRegion — from GameTrainers.Common.Memory
  ViewModels/  MainViewModel, CharacterViewModel, NamedValueViewModel, ICharacterHost
  App.xaml, MainWindow.xaml         the WPF UI
test/FormatCheck/                   headless verification against ground-truth party bytes
.data/         DOSBox-X memory dumps + region CSVs (memdump.md describes them), DragonWars.ahk
.game/         the game itself
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer come from
the shared `GameTrainers.Common` library rather than being duplicated here.

---

## Notes & caveats

- Tested logic: the record parser and the Dragon Wars name encoding are verified by `FormatCheck`
  against the bundled dump bytes. The live attach/scan path needs the game running to exercise.
- Edits take effect the next time the game reads the field (e.g. opening the character screen).
- Some emulators can map guest RAM more than once; the anchor scan uses the first match that has a
  real party behind it.
- Setting values very high is safe for the trainer, though the game's own UI may render unusually
  large numbers oddly — that's cosmetic.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
```
