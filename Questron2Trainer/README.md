# Questron II — Live Trainer

A WPF (.NET 8) trainer for the 1988 SSI DOS RPG **Questron II**. It attaches to the
running game (inside DOSBox / DOSBox-X), locates the single character record in the
emulated memory, and lets you edit it live — HP, Food, Gold, five attributes
(Charisma, Strength, Agility, Stamina, Intelligence), Level, equipped weapon and
armor, spell charges, and name — with per-vital **freeze** toggles and one-click
**max** actions.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

The record layout was reverse-engineered from static analysis of the shipped
`DEMOFILE` save (the demo character "The Thing": HP 200, Food 188, Gold 162, all
attributes 15, Level 1) and cross-checked against the game manual and strings
extracted from `START.EXE`. No live memory dump was available, so every offset
carries a confidence marker: **[Static]** (confirmed against the DEMOFILE and/or
manual) or **[Inferred]** (plausible from the DEMOFILE but not independently
confirmed). See [docs/Questron2-Reverse-Engineering.md](docs/Questron2-Reverse-Engineering.md).

---

## Quick start

1. **Launch Questron II** in DOSBox/DOSBox-X and play past the title screen (the
   character record only lives in memory once the game is loaded).
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `Q2Trainer.exe`, which requests administrator
   rights via UAC — reading/writing another process's memory needs them, especially
   if the emulator is elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/DOSBox-X/etc. are
   auto-sorted to the top) and click **Attach**. It scans memory and finds the
   character automatically.
4. **Edit:** change any field on the character sheet. Edits are written to the game
   *immediately* (they take effect when the game next reads the field — e.g. opening
   the character screen in-game).

If the scan finds nothing, make sure the game is loaded past the title screen, then
click **Re-scan**.

---

## What it can edit

The trainer decodes a **256-byte** character record:

- **Identity** — name (16-byte ASCII field), level (with rank name).
- **Vitals** — HP, Food, and Gold (all `uint16` LE).
- **Attributes** — Charisma, Strength, Agility, Stamina, Intelligence (one byte each,
  1–25 range).
- **Equipment** — equipped weapon ID and armor ID (indexing the EXE's weapon/armor
  tables).
- **Spells** — eight spell-charge bytes (one per spell, 0–99 each).

### Freeze toggles

The toolbar has **Freeze HP**, **Freeze Food**, and **Freeze Gold** checkboxes. While
a vital is frozen the poll loop re-pins its value every tick, so it never drops in
play. Toggle it off to let the value move again.

### Quick actions

- **Toolbar** (party-wide): Full Heal, Max Attributes, Max Spells, Max Gold, Max
  Everything.
- **Character sheet** (per-character): same actions repeated for convenience.

"Max" targets are conservative safe caps: attributes 25, HP/Food 9999, gold 65535,
level 20, spell charges 99.

---

## How it finds the character

The character record's live address changes every DOSBox session, so the trainer
never hard-codes it. Instead it tries two strategies in order:

1. **Anchor scan** — the copyright string `"Questron II (C) 1988 S.S.I."` appears in
   the game's data segment and loads verbatim into guest RAM. The locator scans for
   this string, then searches a 256 KB window forward for a valid character record.
2. **Structural scan** (fallback) — scans all readable memory for a 256-byte window
   that passes `IsValidRecord`: a 2–15 character printable-ASCII name starting with a
   letter, plausible HP (1–99999), Food (0–99999), Gold (0–65535), five attributes
   each in 1–25, and a level in 0–20.

---

## Verification

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` asserts format constants, reference tables (spells, weapons, armor,
items, monsters, locations), character record round-trip (encode/decode all fields),
`IsValidRecord` validation (accepts the demo record, rejects all-zeros, bad name,
HP=0, out-of-range attributes, out-of-range level, 1-char name, 16-byte name with
no null), and the locator driven over a synthetic address space (character found
with correct name/HP/level, empty memory not found, cancellation honoured). Exits 0
(pass) or 1 (fail).

---

## Project layout

```
src/Questron2Trainer/
  Game/        CharacterFormat.cs     the 256-byte offset table with [Static]/[Inferred] markers
               CharacterRecord.cs     typed, mutable view over a 256-byte buffer
               GameFacts.cs           static game facts (title, exe, copyright string, emulator hints)
               SpellBook.cs           5 spells (Magic Missile, Fireball, Sonic Whine, Time Sap, Destruct)
               WeaponBook.cs          10 weapons (Dagger … Crossbow)
               ArmorBook.cs           7 armor types (Rawhide … Ribbed Plate)
               ItemBook.cs            25 items (12 keys, 11 quest items, 2 transports)
               MonsterBook.cs         39 monsters extracted from START.EXE strings
               LocationBook.cs        26 locations (towns, cathedrals, castles, tombs, dungeons)
  Memory/      IMemorySource.cs       read-only interface for testability
               CharacterLocator.cs    anchor + structural scan locator
  ViewModels/  MainViewModel, CharacterViewModel, NamedValueViewModel, ReferenceViewModel, ICharacterHost
  App.xaml, MainWindow.xaml           the WPF UI
test/FormatCheck/                     headless verification harness (100 checks)
docs/                                 RE notes and strategy guide
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer
come from the shared `GameTrainers.Common` library rather than being duplicated here.

---

## Notes & caveats

- **No live memory dump was available.** Every offset is marked **[Static]** (confirmed
  against the DEMOFILE and/or manual) or **[Inferred]** (plausible but unconfirmed).
  The layout should be verified against a running game at the first opportunity.
- Edits take effect the next time the game reads the field (e.g. opening the character
  screen).
- The game engine is `START.EXE`, an EXEPACK-compressed Microsoft C 1987 build by
  Westwood Associates / Quest Software / SSI, version 1.2.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
