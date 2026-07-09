# Pool of Radiance — Live Trainer

A WPF (.NET 8) trainer for the 1988 SSI DOS game **Pool of Radiance**, the first AD&D "Gold Box"
CRPG. It attaches to the running game (inside DOSBox / DOSBox-X), finds the party in the emulated
memory, and lets you edit every character live — ability scores, HP, AC/THAC0, class levels, XP,
money, status, and any raw byte — with a **god-mode** freeze, one-click **max** buttons, a
**combat panel** for zapping enemy HP, a **Cheat-Engine-style memory scanner** for everything else,
and reference tabs (monsters, spells, rules, strategy) built from the reverse-engineering work.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

The layout it edits was recovered by reverse-engineering DOSBox-X memory dumps of a live party and
cross-checked against community documentation; the full write-up is in
[`.docs/reverse-engineering.md`](.docs/reverse-engineering.md), and a strategy guide with maps is in
[`.docs/strategy-guide.md`](.docs/strategy-guide.md).

---

## Quick start

1. **Launch Pool of Radiance** in DOSBox/DOSBox-X and play until a party exists (past the title
   screen — the party only lives in memory once loaded).
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `PoRTrainer.exe`, which requests administrator rights via UAC —
   reading/writing another process's memory needs them, especially if the emulator is elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/ScummVM/etc. are auto-sorted to
   the top) and click **🔌 Attach**. It scans memory and lists the party automatically.
4. **Edit:** select a character on the left. Use the **🧙 Character** tab for friendly fields, or the
   **🔢 Raw Bytes** tab for the full 285-byte record with every offset labelled. Edits are written to
   the game *immediately* (they take effect when you next open the character screen in-game).
5. **Cheat fast:** tick **🛡 God mode** to freeze party HP, hit **✚ Heal party**, or **★ Max
   EVERYTHING**. Per-character quick actions live on the Character tab.

If the scan finds nothing, make sure a party is actually loaded, then click **🔎 Re-scan**.

### Global hotkeys

Three system-wide hotkeys fire even while the **game** window has focus, so there's no alt-tabbing
mid-fight:

- **Ctrl+F1** — toggle god mode (freeze party HP)
- **Ctrl+F2** — heal the party once
- **Ctrl+F3** — ★ Max EVERYTHING, party-wide

If another app already owns one of these combinations, the toolbar says so and the rest still work.

---

## What it can edit

The trainer decodes the full **285-byte** Pool of Radiance character record. On the friendly tabs:

- **Identity** — name, race, class (incl. multiclass), alignment, gender, age, status.
- **Ability scores** — STR/INT/WIS/DEX/CON/CHA and the exceptional-strength percentile (18/xx).
- **Hit points & combat** — current/max HP, Armor Class and THAC0 (shown as the game shows them —
  lower is better — with the internal `60 − x` encoding handled for you), experience, freeze-HP.
- **Class levels** — each of the eight class-level bytes (set a Fighter to 8, etc.).
- **Money & treasure** — all seven counters: copper, silver, electrum, gold, platinum, gems, jewelry.

The **🔢 Raw Bytes** tab exposes every byte of the record with its known field label, for the
handful of fields the friendly editors don't surface (saving throws, thief skills, spell memorization).

### ⚔ Combat panel

Monsters use the **same record format** as characters, so while a battle is on screen the scan also
finds the enemy combatants. The Combat tab lists them with live HP; **💀 Kill selected** or
**☠ Kill ALL enemies** sets their current HP to 0. You can also freeze your own party's HP through a
fight with god mode.

### 🔒 Freeze spells

Casters normally lose a memorized spell when they cast it. Tick **🔒 Freeze spells (party)** in the
toolbar (or **🔒 Freeze spells** on a single character) right after resting/memorizing: the trainer
snapshots that character's 21-byte memorized-spell block and re-stamps it every poll tick, so
casting never uses a spell up. Toggle it off before you re-memorize a different loadout.

Like god-mode HP freeze, this writes to the live game each tick; if a spell doesn't reappear
immediately after a mid-fight cast it should on the next tick — verify the behavior in your own game.

### 🎒 Inventory (offline save editor)

Like the **🧬 Powers** tab, this edits the save on disk (each character's `CHRDATAn.ITM` file), so
close the game or reload the save afterward; a backup is made automatically before the first change.
It lists every carried item (as the game shows it, with ready/identified/cursed flags) and offers:

- **🔎 ID all items** — for the selected character, or **★ every character** at once: reveals each
  item's full name (sets the "hidden-names" flag to identified). Great for unidentified magic.
- **⧉ Duplicate inventory** — copy one character's entire inventory onto another, replacing it.

It shares the save you load on the **🧬 Powers** tab (same *Save folder* box).

### 🔍 Memory scanner

Some things aren't in the character record at all — the party's **map X/Y and facing**, the
**in-combat clock**, the **encounter counters**. The Memory tab is a small Cheat-Engine-style
scanner for exactly those: first-scan a known value (or Unknown), change it in-game, then narrow
with Increased/Decreased/Changed until one address remains, and Write a new value or Poke an address
directly. Candidates are dropped on Detach.

### Reference tabs

- **🐉 Monsters** — the bestiary with the game's own XP-per-kill values and stat blocks.
- **✨ Spells** — every cleric and magic-user spell with what it does and why it matters.
- **📖 Rules** — classes, races, level caps, and the XP-to-level tables.
- **🗺 Maps** — each district's grid size and keyed locations with their `(x, y)` coordinates, plus a
  **manual teleport** helper (see below).
- **🗺 Strategy** — a condensed walkthrough; the full guide with maps is in `.docs/strategy-guide.md`.

### 🗺 Maps & teleport

The **🗺 Maps** tab is an offline reference (areas, grid sizes, keyed locations with coordinates,
transcribed from the strategy guide). It also hosts a **manual teleport**: the party's map X/Y isn't
in the character record and its address moves every DOSBox session, so the workflow is — scan for
your current X (then Y) on the **🔍 Memory** tab, paste those addresses into the teleport box, pick a
location (or type a target X/Y), and **🧭 Teleport** pokes the coordinates. Do it while *exploring*,
never mid-combat, then take a step to redraw the map.

---

## How it finds the party

The party's live address changes every DOSBox session (and per emulator memory layout), so the
trainer never hard-codes it. Instead it **signature-scans** the target process for the record shape
(`Game/CharacterSignature.cs`): a valid length-prefixed name, six in-range ability scores, a race
byte ≤ 7, a class byte ≤ 17, positive max HP, and a valid status enum. That reliably isolates the
party — and any in-combat monsters — wherever the OS mapped the emulated RAM.

---

## Verified against the real game

The record layout isn't guessed. It was derived by differential analysis of two DOSBox-X memory
dumps of a live party (exploring vs. in combat) and confirmed against the Gold Box Companion format
docs and the open-source `coab` reimplementation. The parser is regression-tested against **verbatim
285-byte records** captured from those dumps:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` asserts the decoded sample party, e.g.:

```
THRENDER GRONE  Male Dwarf Fighter (LG)      STR 17  HP 11/11  AC 1  THAC0 19  age 52  XP 32
RHIANNON        Female Elf Fighter/Mage (TN)  STR 15  HP 7/7   AC 0            age 180
```

and the combat dump independently pins the live fields — Rhiannon's current HP 7→0 and status
okay→**unconscious** between the two captures.

---

## Project layout

```
src/PoolOfRadianceTrainer/
  Game/        PorFormat.cs          the validated 285-byte offset table + enums
               CharacterRecord.cs    typed, mutable view over a 285-byte buffer (AC/THAC0 60-x encoding)
               CharacterSignature.cs the record-shape predicate used by the scanner
               SaveGame.cs           offline CHRDATAn.SAV/.SPC/.ITM editor (effects, items, backup)
               EffectBook.cs         the effect/"power" dictionary   InventoryItem.cs  63-byte item record
               MonsterBook.cs        bestiary + XP values      SpellBook.cs      cleric/mage spells
               ClassRaceBook.cs      classes/races/XP tables    Walkthrough.cs    in-app strategy
               MapBook.cs            areas + keyed location coordinates (Maps tab)
  Memory/      NativeMethods.cs      hotkey P/Invoke (memory P/Invokes now in GameTrainers.Common)
               CharacterLocator.cs   signature scanner (returns party + monsters)
               MemorySearcher.cs     Cheat-Engine-style value scanner (PoR-local)
               GlobalHotkeys.cs      system-wide Ctrl+F1/F2/F3
               (shared)              ProcessMemory / MemoryRegion — pulled from GameTrainers.Common via alias
  ViewModels/  MainViewModel, CharacterViewModel, MemorySearchViewModel, child VMs, converters
  Mvvm/        ObservableObject, RelayCommand (PoR-local; diverges from GameTrainers.Common.Mvvm)
  App.xaml, MainWindow.xaml          dark, gold-accented UI
test/FormatCheck/                    headless verification against ground-truth party bytes
.docs/         reverse-engineering.md, strategy-guide.md
.data/         DOSBox-X memory dumps + region CSVs (memdump.md describes them)
.game/         the game itself (START.EXE, GAME.OVR, *.DAX, POOL.CFG)
```

---

## Notes & caveats

- Tested logic: the record parser, the `60 − x` AC/THAC0 encoding, and round-tripping are verified by
  `FormatCheck` against the bundled dump bytes. The live attach/scan path needs the game running to
  exercise.
- Edits take effect the next time the game reads the field (e.g. opening the character screen).
  During combat, the game may track a separate combatant copy — use god mode / the combat panel /
  the memory scanner for live-fight edits.
- Some emulators can map guest RAM more than once, so the scan may list a record twice; they point at
  the same character.
- Setting values absurdly high (255 HP, huge money) is safe for the trainer, though the game's own UI
  may display very large numbers oddly — that's cosmetic.
- Always keep a backup of your save (`CHRDATA?.SAV` / `SAVE*`) before experimenting.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
