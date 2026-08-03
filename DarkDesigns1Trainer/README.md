# Dark Designs I: Grelminar's Staff — Live Trainer

A WPF (.NET 8) trainer for the 1990 DOS RPG **Dark Designs I: Grelminar's Staff** by John Carmack
(published by Softdisk / Big Blue Disk). It attaches to the running game (inside DOSBox /
DOSBox-X), locates the character roster in the emulated memory automatically — no manual searching
like Cheat Engine — and lets you edit every character live: name, class, level, the five attributes
(STR/DEX/CON/INT/PIE), Body (HP), Magic (MP), experience, gold, and status, with per-vital **freeze**
toggles and one-click **max** actions, both per-character and party-wide.

It additionally includes an **offline save editor** for `DDCHARS.DAT` (the character file), and a
**References** tab listing all 16 spells, 40 items, and 43 monsters from the game.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

The character record layout was recovered by reverse-engineering the LZEXE-compressed `DARKDES.EXE`
and a sample `DDCHARS.DAT`. See [Reverse Engineering](docs/ReverseEngineering.md) for the full
analysis and [Strategy Guide](docs/StrategyGuide.md) for a complete play guide with controls,
spells, items, monsters, and walkthrough.

---

## Quick start

1. **Launch Dark Designs I** in DOSBox/DOSBox-X and play past the title screen (the roster only
   lives in memory once characters are loaded).
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `DD1Trainer.exe`, which requests administrator rights via UAC —
   reading/writing another process's memory needs them, especially if the emulator is elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/ScummVM/etc. are auto-sorted to
   the top) and click **Attach**. It scans memory and lists the party automatically.
4. **Edit:** select a character on the left, then change any field on the right. Edits are written
   to the game *immediately* (they take effect when the game next reads the field — e.g. opening
   the character screen in-game).

If the scan finds nothing, make sure a party is actually loaded, then click **Re-scan**.

---

## What it can edit

The trainer decodes the full **54-byte** Dark Designs I character record:

- **Identity** — name (12-char ASCII), class (Fighter/Priest/Wizard), level.
- **Attributes** — Strength, Dexterity, Constitution, Intelligence, Piety (uint16 LE).
- **Vitals** — Body current/max (HP), Magic current (MP).
- **Progression** — Experience, Gold (uint16 LE).
- **Status** — fine, KO, STUNED, STONE, DEAD.

### Freeze toggles

The toolbar has party-wide **Freeze Body**, **Freeze Magic**, and **Freeze Status** checkboxes.
While a vital is frozen the poll loop re-pins it every tick, so it never drops in play.

### Quick actions

- **Party-wide** (toolbar): Heal Party, Max Attributes, Max Money, Max Everything.
- **Per-character** (below the character sheet): Full Heal, Max Attributes, Max Money, Max Everything.

"Max" targets are conservative safe caps: attributes 30, Body/Magic 999, level 50, gold/experience
65535.

### Save editor

The **Save Editor** tab edits `DDCHARS.DAT` offline (no game running required). The file is 1,224
bytes = 144-byte header + 20 × 54-byte records, with no checksum. A one-shot `.bak` backup is
taken before the first write. The "Max All & Save" button maxes every occupied character in one
click.

---

## How it finds the party

The roster's live address changes every DOSBox session, so the trainer never hard-codes it. It uses
a **dual-strategy locator** (`Memory/RosterLocator.cs`):

1. **String anchor** — the 34-byte title string `"Dark Designs I : Grelminar's Staff"` lives in the
   game's data segment as plain ASCII and is unique in DOSBox guest RAM. The locator finds it, then
   searches a 256 KB window forward for the 20-record character pattern. Fast (~50 ms).
2. **Structural scan** — fallback that scans all readable memory for a contiguous block of 54-byte
   records matching the character pattern (occupied slots validated, empty slots all-zero, packed
   from slot 0). Slower (~2 s) but build-independent.

---

## Verified against the real game

The record layout was derived from static analysis of the unpacked EXE and a sample
`DDCHARS.DAT` (one character, "CHRISTOPHER", Fighter L1). The parser is regression-tested:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` asserts format constants, record decode/encode, name round-trip/truncation, empty
slot detection, `LooksLikeRecord` validation, save-file round-trip with `.bak` verification,
multi-character saves, and reference table counts — and exits 0 (pass) or 1 (fail). When the
sample `DDCHARS.DAT` is present it also asserts the empirically-confirmed values.

---

## Project layout

```
src/DarkDesigns1Trainer/
  Game/        CharacterFormat.cs   the validated 54-byte offset table, class/status constants, lookup tables
               CharacterRecord.cs  typed, mutable view over a 54-byte buffer (LE accessors, name, attributes)
               SpellBook.cs        8 wizard + 8 priest spells with gold costs
               ItemBook.cs         40 items across 9 categories
               MonsterBook.cs      43 monsters from Kobold to Chaos Avatar
               GameFacts.cs        game metadata, anchor string, validator strings
               SaveFile.cs         offline DDCHARS.DAT reader/writer with .bak backup
  Memory/      RosterLocator.cs    dual-strategy locator (string anchor + structural scan)
               (shared)            ProcessMemory / MemoryRegion — from GameTrainers.Common.Memory
  ViewModels/  MainViewModel, CharacterViewModel, NamedValueViewModel, ReferenceViewModel, ICharacterHost
  App.xaml, MainWindow.xaml         the WPF UI (Party / Save Editor / References tabs)
test/FormatCheck/                   headless verification harness
docs/                               reverse-engineering notes and strategy guide
.docs/                              RE working notes (git-ignored)
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer come from
the shared `GameTrainers.Common` library rather than being duplicated here.

---

## Notes & caveats

- Tested logic: the record parser, save-file round-trip, and reference tables are verified by
  `FormatCheck`. The live attach/scan path needs the game running to exercise.
- The 144-byte `DDCHARS.DAT` header is only partially decoded and is round-tripped without
  interpretation; only the character records are exposed for editing.
- The status field encoding (KO/STUNED/STONE/DEAD) is inferred from game strings but not confirmed
  against a character in those states.
- Map files (`DDMAP1–5.DAT`) are not decoded or edited by the trainer.
- Edits take effect the next time the game reads the field (e.g. opening the character screen).
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
