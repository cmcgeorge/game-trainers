
A WPF (.NET 8) trainer for **The Dungeons of Moria (UMoria 5.5.2)**, the single-character
roguelike by Robert Alan Koeneke and James E. Wilson. It attaches to the running game (inside
DOSBox / DOSBox-X), locates the character's stats by **value scanning** (the game is a 32-bit
DPMI program whose heap address changes every session — there is no static anchor), and lets
you edit them live — HP, mana, gold, level, experience, the six stats — with a **freeze** table
that re-writes them every tick. It also ships a **teleport** tab that locates `char_row` /
`char_col` by relative scanning and writes them to teleport, plus read-only **Maps**,
**Paragraphs** (monster memory), **Items**, and **Reference** (races/classes/spells) tabs.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

The offsets and data tables are documented in [.docs/ReverseEngineering.md](.docs/ReverseEngineering.md)
with Confirmed / Inferred / Candidate confidence tags. A strategy guide lives in
[.docs/StrategyGuide.md](.docs/StrategyGuide.md).

---

## Quick start

1. **Launch UMoria** in DOSBox/DOSBox-X and start or load a game (a character must be on-screen
   for the stats to live in memory).
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `MoriaTrainer.exe`, which requests administrator rights via
   UAC — reading/writing another process's memory needs them, especially if the emulator is
   elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/DOSBox-X/etc. are auto-sorted
   to the top) and click **Attach**.
4. **Pin a stat:** on the **Character** tab, either click a **guided scan** button (e.g. *Current
   HP*) to pre-fill the scanner with the right width and a sensible default, or type the value
   you see on-screen and click **First Scan**. Then change the value in-game (rest, quaff a
   potion, buy something, walk to gain XP…), type the new value, and click **Exact**. Repeat
   until one row remains. Click **Pin selected →** to add it to the freeze table.
5. **Edit or freeze:** on the **Freeze** tab, edit **Target** to poke a new value, or tick
   **Freeze** to re-write it every ~200 ms so the game can't move it back.
6. **Teleport:** on the **Teleport** tab, click **Snapshot Position**, walk one square in a
   cardinal direction in-game, and click the matching **Walked** button. Repeat until two
   candidates remain, pin one as **Col** (it changes on E/W) and one as **Row** (it changes on
   N/S), then type a target X/Y and click **Teleport**.

---

## What it can edit

UMoria is a DPMI program whose heap address changes every session, so the trainer never
hard-codes addresses. It uses **Cheat-Engine-style value scanning** (the same model as
`ThePerfectGeneral2Trainer` and `BattleTech1Trainer`) to locate each field by its value, then
pins survivors to a freeze table.

- **Character stats** (via guided scans): Current HP, Max HP, Current Mana, Max Mana, Gold,
  Experience, Level, the six stats (STR/INT/WIS/DEX/CON/CHR), and the Food counter.
- **Any other value** you can see on-screen (the scanner is general — pick Byte/Int16/Int32,
  type a value, narrow by Increased/Decreased/Changed/Unchanged).
- **Freeze** any pinned value so the game can't move it (e.g. freeze Current HP at max for
  invincibility, freeze Gold at 9,999,999, freeze a drained stat at its max).
- **Teleport** by writing the located `char_row` / `char_col` globals.

### Read-only reference tabs

- **Maps** — the 51-level descent (town + 50 dungeon levels). Dungeon layouts are **procedurally
  generated**, so there are no fixed maps; the tab documents what each depth contains, what
  items first appear there, and which monsters dominate. Jump to the Balrog level with one click.
- **Paragraphs** — the "monster memory" reference, mirroring what the in-game `/` and `l`
  commands print from `c_recall[]`. A curated subset of the 279-creature roster (early game,
  mid-game, dragons, liches, and the Balrog) with recall text.
- **Items** — the item-category reference (the `tval` byte on each `inven_type`), plus the ego
  weapons (Holy Avenger, Defender, Slay *), crowns, and wearable flags (RA/RC/RF/RL/FA/SI/…).
- **Reference** — the eight races, six classes (with skill-gain rates), and the 62 spells
  (31 mage + 31 priest, with letter, min level, mana cost, book, and damage).

---

## How it finds the character

UMoria 5.5.2 is compiled with DJGPP v2.01 and runs as a **32-bit protected-mode (DPMI)**
program under `CWSDPMI.EXE`. Its mutable state (the `py` struct, `cave[]`, `inventory[]`,
`c_recall[]`) lives in a flat 32-bit data segment that CWSDPMI/DOSBox allocates at a
session-specific linear address. There is no static byte signature to anchor a locator to, so
the trainer has **no `GameLocator`** — it drives `GameTrainers.Common.Memory`'s `MemorySearcher`
as a value scanner instead. See [.docs/ReverseEngineering.md](.docs/ReverseEngineering.md) for
the full reasoning and the image-relative offsets a future COFF-base locator could use.

The teleport feature locates `char_row` / `char_col` (two `int16` globals that track cardinal
moves) by **relative scanning**: snapshot an unknown-value baseline at Int16, walk one square,
narrow by Increased/Decreased, repeat until two candidates remain, then disambiguate by which
direction moves which one.

---

## Verified

The game-knowledge layer (the Confirmed stat encoding, the cave cell constants, the
roster/level/item counts, the curated monster roster including the Balrog) and the pure
value-scanner helpers (`ScanValue` parse/width-fit/canonicalize, `FrozenValueViewModel` width
guard, `ScanRecipe` ranges) are regression-tested by the headless `FormatCheck` harness:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

It exits 0 (pass) or 1 (fail). The live attach/scan/teleport path needs the game running to
exercise — it can't be smoke-tested headlessly.

---

## Project layout

```
src/MoriaTrainer/
  Game/        PlayerFormat.cs     RE constants (struct offsets, stat encoding, cave fvals, inventory slots)
               RaceBook.cs         the 8 playable races (adjustments, infravision, allowed classes)
               ClassBook.cs        the 6 classes (prime stat, hit die, mana basis, skill-gain rates)
               MonsterBook.cs      curated subset of the 279-creature roster (incl. the Balrog)
               ItemBook.cs         item-category reference (tval), ego weapons, crowns, wearable flags
               SpellBook.cs        31 mage spells + 31 priest prayers (letter, level, mana, book, damage)
               LevelBook.cs        the 51-level descent reference (town + 50 dungeon levels)
               ParagraphBook.cs    recall-paragraph renderer (mirrors in-game / and l commands)
               ScanGuide.cs        14 guided scan recipes (visible stat -> memory field + width + range)
  ViewModels/  MainViewModel       root: process attach, value scanner, freeze table, poll loop, guided scans
               TeleportViewModel   relative-scan teleport (locates char_row/char_col, writes them)
               ParagraphsViewModel monster-memory reference (filter + recall text)
               ItemsViewModel      item-category reference (filter + ego/crowns/flags)
               MapsViewModel       51-level descent reference (selectable detail, jump-to-Balrog)
               ReferenceViewModel  races/classes/spells reference (read-only)
               ScanResultViewModel one surviving scan candidate
               FrozenValueViewModel a pinned address (live/target/freeze, width-guarded writes)
               IScanHost, ScanValue  scan host channel + parse helpers
  App.xaml, MainWindow.xaml         the WPF UI (Character / Freeze / Teleport / Maps / Paragraphs / Items / Reference)
test/FormatCheck/                   headless verification of the game-knowledge layer + scan helpers
.docs/         ReverseEngineering.md, StrategyGuide.md, Proposal.md
.game/         the game itself (umoria.exe, CWSDPMI.EXE, MORIA.CNF, doc/, *.hlp)
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer
(`ProcessMemory`/`MemoryRegion`/`MemorySearcher`) come from the shared `GameTrainers.Common`
library rather than being duplicated here.

---

## Notes & caveats

- **DPMI target:** UMoria's heap address changes every DOSBox session, so the trainer never
  hard-codes addresses. Every field is located by value scanning, never by adding to a fixed base.
- **Stat encoding:** stats 3..18 are stored as one byte; 18/01..18/100 use two bytes (18, then
  the /xx byte). The guided `str`/`int`/`wis`/`dex`/`con`/`chr` scans default to Byte width; if
  your stat is 18/xx, scan the /xx byte (the byte after 18) — see the recipe notes.
- **Edits take effect** the next time the game reads the field (e.g. opening the `C` character
  screen, or the next combat round for HP).
- **Teleport** writes the raw `char_row` / `char_col` globals; the engine redraws the player on
  the new cell next frame. Walking into a wall or a monster after a teleport still triggers the
  normal collision/tunnel/attack logic.
- **Tested logic:** the game-knowledge layer and the scan helpers are verified by `FormatCheck`.
  The live attach/scan/teleport path needs the game running to exercise.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
