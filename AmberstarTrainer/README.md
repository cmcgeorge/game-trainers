# Amberstar — Live Trainer

A WPF (.NET 8) trainer for the 1992 Thalion Software DOS RPG **Amberstar**. It attaches
to the running game (inside DOSBox / DOSBox-X), locates the party roster in the emulated
memory, and lets you edit every character live — all nine attributes, ten skills,
HP/SP/SLP, level, experience, gold, food, race, class, spells, and ailments — with
per-vital **freeze** toggles and one-click **max** actions, both per-character and
party-wide.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

The record layout it edits is derived from the open-source
[Pyrdacor/Amberstar](https://github.com/Pyrdacor/Amberstar) file specification, which
documents the big-endian character data format inherited from the Atari ST original. The
parser is regression-tested against a synthetic record with known values (see
[Verified](#verified)).

---

## Quick start

1. **Launch Amberstar** in DOSBox/DOSBox-X and play until a party exists (past the title
   screen — the roster only lives in memory once characters are loaded).
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `AmberstarTrainer.exe`, which requests administrator
   rights via UAC — reading/writing another process's memory needs them.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/DOSBox-X are
   auto-sorted to the top) and click **Attach**. It scans memory and lists the party
   automatically.
4. **Edit:** select a character on the left, then change any field on the right. Edits
   are written to the game *immediately* (they take effect when the game next reads the
   field — e.g. opening the character screen in-game).

If the scan finds nothing, make sure a party is actually loaded, then click **Re-scan**.

---

## What it can edit

The trainer decodes the full **1146-byte** Amberstar character record (big-endian):

- **Identity** — name (ASCII, null-terminated), gender, race (7 races), class (8 classes),
  level, experience, gold, food.
- **Attributes** — Strength, Intelligence, Dexterity, Speed, Constitution, Charisma, Luck,
  Anti-Magic, Age (current and max set together).
- **Vitals** — HP, SP (Spell Points), SLP (Spell Learning Points), each current/max.
- **Skills** — all 10 skill ranks (Attack, Parry, Swim, Listen, Find Traps, Disarm Traps,
  Pick Locks, Search, Read Magic, Use Magic), current and max set together.
- **Combat** — Base Defense, Base Damage.
- **Spells** — the four spell-school bitfields (White, Grey, Black, Special), via the
  **Learn All Spells** action.
- **Ailments** — Physical (stunned, poisoned, petrified, diseased, aging, dead, ash, dust)
  and Mental (irritated, mad, sleeping, afraid, blind, overloaded).

### Freeze toggles

The toolbar has party-wide **Freeze HP**, **Freeze SP**, and **Freeze Status** checkboxes.
While a vital is frozen the poll loop re-pins its current value to its max every tick, so
it never drops in play. Toggle it off to let the value move again.

### Quick actions

- **Party-wide** (toolbar): Heal Party, Max Attributes, Learn Spells, Max Money, Max
  Everything.
- **Per-character** (below the character sheet): Full Heal, Max Attributes, Max Skills,
  Learn All Spells, Max Money, Max Everything.

"Max" targets are conservative safe caps: attributes 999, skills 99, HP/SP/SLP 9999, gold
65535, food 65535, experience 999,999,999.

---

## How it finds the party

The roster's live address changes every DOSBox session, so the trainer never hard-codes
it. Instead it performs a **structural scan**: it walks every readable memory region
looking for a window of up to six contiguous 1146-byte records that match the Amberstar
party shape exactly. Each occupied slot is validated by its magic header (`00 FF`),
type (Person), plausible gender/race/class, all 20 skill bytes in 0..99, big-endian
attributes in a sane range, HP max > 0, and a well-formed ASCII name starting with a
letter. Occupied slots must pack from slot 0, followed by empty slots
(`Memory/PartyLocator.cs`).

---

## Verified

The record layout isn't guessed. It was derived from the
[Pyrdacor/Amberstar](https://github.com/Pyrdacor/Amberstar/blob/main/FileSpecs/CharData.md)
file specification and confirmed against the GAME.EXE V1.34 (22.10.1992) IBM AT build.
The parser is regression-tested against a synthetic record with known values:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` asserts the decoded fields, the name round-trip, the IsOccupied check, the
spell/race/class reference tables, and the set operations — and exits 0 (pass) or 1 (fail).

---

## Project layout

```
src/AmberstarTrainer/
  Game/        CharacterFormat.cs   the validated 1146-byte offset table (big-endian) and lookup tables
               CharacterRecord.cs   typed, mutable view over a 1146-byte buffer (big-endian accessors)
               SpellBook.cs         the four spell schools (96 spells total)
               RaceBook.cs          race names
               ClassBook.cs         class names
  Memory/      PartyLocator.cs      structural scanner → up to six party slots
               (shared)             ProcessMemory / MemoryRegion — from GameTrainers.Common.Memory
  ViewModels/  MainViewModel, CharacterViewModel, NamedValueViewModel, ReferenceViewModel, ICharacterHost
  App.xaml, MainWindow.xaml         the WPF UI
test/FormatCheck/                   headless verification against a synthetic record
docs/                               reverse-engineering notes and strategy guide
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer come
from the shared `GameTrainers.Common` library rather than being duplicated here.

---

## Notes & caveats

- Tested logic: the record parser and the big-endian accessors are verified by
  `FormatCheck`. The live attach/scan path needs the game running to exercise.
- Amberstar stores character data in **big-endian** (inherited from the Atari ST origin);
  the trainer reads and writes accordingly.
- Edits take effect the next time the game reads the field (e.g. opening the character
  screen).
- The PARTYDAT.SAV save file uses an unknown compression method and is not directly
  editable; this trainer edits live memory only.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
