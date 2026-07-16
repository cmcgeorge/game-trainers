# Wasteland — Live Trainer

A WPF (.NET 8) trainer for the 1988 Interplay / Electronic Arts post-apocalyptic RPG
**Wasteland**. It attaches to the running game (inside DOSBox / DOSBox-X), locates the party
roster in the emulated memory, and lets you edit every ranger live — the seven attributes, all
35 skills, constitution (CON), money, experience, level, skill points, armour class, name,
gender and nationality — with party-wide **Freeze Health (CON)** and **Freeze Ammo** toggles and
one-click **max** actions, both per-character and party-wide. A **Maps** tab reads the party's live
map and X/Y from memory
and **teleports** the squad within the current map; a **References** tab lists skills, item ids,
the game's own **paragraph** book, and a condensed **strategy** guide.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

The record layout it edits was recovered by reverse-engineering four live DOSBox-X memory dumps
of the default party (start-of-game, out in the desert, and mid-combat) and cross-checked against
the game manual, the community wiki, and the open-source `kayahr/wastelib` file-format project. The
parser is regression-tested against a verbatim 7-record roster captured from those dumps (see
[Verified against the real game](#verified-against-the-real-game)). Full teardown notes are in
[.docs/Wasteland-Reverse-Engineering.md](.docs/Wasteland-Reverse-Engineering.md); a play/strategy
guide is in [.docs/Wasteland-Strategy-Guide.md](.docs/Wasteland-Strategy-Guide.md).

---

## Quick start

1. **Launch Wasteland** in DOSBox/DOSBox-X and play until the party exists (past the title
   screen — the roster only lives in memory once characters are loaded).
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `WLTrainer.exe`, which requests administrator rights via UAC —
   reading/writing another process's memory needs them, especially if the emulator is elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/ScummVM/etc. are auto-sorted to
   the top) and click **Attach**. It scans memory and lists the party automatically.
4. **Edit:** select a ranger on the left, then change any field on the right. Edits are written to
   the game *immediately* (they take effect when the game next reads the field — e.g. opening the
   character screen in-game).

If the scan finds nothing, make sure a party is actually loaded, then click **Re-scan**.

---

## What it can edit

The trainer decodes the full **256-byte** Wasteland character record:

- **Identity** — name (plain ASCII), gender, nationality, level, experience, money.
- **Attributes** — Strength, IQ, Luck, Speed, Agility, Dexterity, Charisma.
- **Constitution** — current CON and MAXCON.
- **Progression** — unspent skill points (SKP), armour class.
- **Skills** — all 35 skill ranks (Brawling … Cyborg Tech), edited by id (reuse or append into the
  30-slot packed list).
- **Inventory** — 30 packed `(item id, ammo/qty)` slots per ranger. Pick from the full 91-item
  catalog in the editable drop-down, or type an item name or a raw item id (0 = empty) straight into
  the same box (the separate id field is gone). Every edit compacts the list to the gap-free,
  `0x00`-terminated form the game reads and writes the whole block back, so a change always lands
  inside the run the game scans — items pack to the top, so a new one may jump to the first free row.
  The item table is decoded from `WL.EXE` itself (see below).

### Freeze toggles

The toolbar has two party-wide freeze checkboxes; each runs off the same poll loop.

- **Freeze Health (CON)** — re-pins every ranger's current constitution (CON, the hit-point stat) to
  its max each tick, so it never drops in play. Toggle it off to let CON move again.
- **Freeze Ammo** — tops every *ammo-bearing* item (a weapon that fires, or a clip/shell/power pack)
  up to **99** each tick, so ammo never runs low, and **clears the jammed-weapon flag** so a frozen
  weapon can't stay jammed — turn Freeze Ammo on and a jammed weapon un-jams on the next tick. Only
  weapons and ammunition are touched — melee weapons, armour, and gear/quest items are left alone
  (their second byte is unused or a status byte, so forcing it could corrupt them). A count already
  above 99 is never reduced. Only the quantity byte is written, never item ids.

### Quick actions

- **Party-wide** (toolbar): Heal Party, Max Attributes, Max Skills, Max Money, Max Everything.
- **Per-character** (below the character sheet): Full Heal, Max Attributes, Max Skills, Max Money,
  Max Everything.

"Max Skills" raises the skills a ranger **already knows** to the trainer's max — adding brand-new
skills is left to the per-skill editor so the 30-slot list never overflows. "Max" targets are
conservative caps (attributes 99, skills 10, money 16,777,215).

---

## Maps & teleport

Wasteland keeps the party's live X/Y and the current 12-byte map name in a 256-byte **party-state
header** that sits immediately before the roster (`rosterBase − 0x100`). Because the Party tab
already locates the roster, the **Maps** tab reads that header directly — so the live map name and
position appear as soon as the party is found, with **no move-search** needed (unlike the Dragon
Wars trainer). Click any square on the 64×64 schematic (or type a target X/Y) and press
**Teleport** to write the two position bytes. Teleport only moves the party *within the map it is
already on* — walk to the target map first, and never teleport mid-combat.

The **Areas** list is a descriptive reference (each town/dungeon and its landmarks). Interior grid
coordinates are not reproduced because they were not confirmed against live memory — only the
Ranger Center start (X 55, Y 62) is a confirmed coordinate.

---

## How it finds the party

The roster's live address changes every DOSBox session, so the trainer never hard-codes it. Unlike
Dragon Wars (which anchors to a unique data-file byte run), Wasteland has no stable adjacent
signature, so the roster is found by **structure** (`Memory/PartyLocator.cs`): it scans committed
memory for an array of seven contiguous 256-byte records where the occupied members pack from slot 0
(an occupied slot never follows an empty one) and every occupied slot passes a strict validity test —
a **2+-character** NUL-terminated printable-ASCII name starting with a letter, seven attribute bytes
each in `1..100`, a plausible MAXCON, **current CON not exceeding MAXCON**, a valid **gender** (0/1)
and **nationality** (0..4). Those extra field checks reject stray byte runs that merely look
name-like, and the scan keeps the candidate with the **most** occupied members, so a lone false
positive can never win over the real party. Edits are clamped to the same ranges, so an edited ranger
never falls out of the next scan.

---

## Verified against the real game

The record layout isn't guessed. It was derived by differential analysis of four DOSBox-X memory
dumps of a live party (baseline / desert / in-combat / post-movement) and confirmed against the
`kayahr/wastelib` ids and the ground-truth stats in `.data/memdump.md`. The parser is
regression-tested against a verbatim 7-record roster captured from those dumps:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` asserts the decoded sample party — the default four rangers — e.g.:

```
Hell Razor    STR 12 IQ 14 LCK 13 SPD 9 AGL 14 DEX 15 CHR 11   MAXCON 28  Private
Angela Deth   STR 8  IQ 15 LCK 14 SPD 11 AGL 10 DEX 17 CHR 14   MAXCON 27  Private
```

plus Hell Razor's decoded skill and inventory lists, the name encode/decode round-trip, occupied /
empty slot detection, and the reference tables, and exits 0 (pass) or 1 (fail). It also has a
`--live <pid>` mode that runs the structural `PartyLocator` against a running emulator.

---

## Project layout

```
src/WastelandTrainer/
  Game/        CharacterFormat.cs   the validated 256-byte offset table + party-header offsets
               CharacterRecord.cs   typed, mutable view over a 256-byte buffer (packed skill/inventory arrays)
               WastelandText.cs     the plain-ASCII name/rank codec
               SkillBook.cs         the 35 skills (id → name, min-IQ)
               ItemCatalog.cs       the full 91-item table (id → name, category), decoded from WL.EXE
               MapBook.cs           area/landmark reference + the live map-name reader
               ParagraphBook.cs     runtime loader for the game's own paragraphs.txt
               Walkthrough.cs       condensed strategy sections (References ▸ Strategy)
  Memory/      PartyLocator.cs      structural scanner → the seven roster slots
               (shared)             ProcessMemory / MemoryRegion — from GameTrainers.Common.Memory
  ViewModels/  MainViewModel, CharacterViewModel, MapsViewModel, ReferenceViewModel, ICharacterHost,
               NamedValueViewModel, SkillRowViewModel, ItemRowViewModel
  App.xaml, MainWindow.xaml         the WPF UI
test/FormatCheck/                   headless verification against ground-truth party bytes
.data/         DOSBox-X memory dumps + region CSVs (memdump.md describes them), extract/scan scripts
.game/         the game itself (manual.txt, paragraphs.txt, wl.exe, data files)
.docs/         reverse-engineering notes and the strategy guide
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer come from the
shared `GameTrainers.Common` library rather than being duplicated here.

---

## Notes & caveats

- Tested logic: the record parser and the packed skill/inventory decoding are verified by
  `FormatCheck` against the bundled roster bytes. The live attach/scan/teleport path needs the game
  running to exercise.
- Edits take effect the next time the game reads the field (e.g. opening the character or inventory
  screen; take one step after a teleport to redraw the map).
- The per-character weapon/equip byte (`0x1F`) and the unidentified padding regions are deliberately
  left untouched.
- Setting values very high is safe for the trainer, though the game's own UI may render unusually
  large numbers oddly — that's cosmetic. Keep a save from before you started editing.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
