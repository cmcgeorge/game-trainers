# Curse of the Azure Bonds — Live Trainer

A WPF (.NET 8) trainer for the 1989 SSI DOS game **Curse of the Azure Bonds**, the second AD&D
"Gold Box" CRPG. It attaches to the running game (inside DOSBox / DOSBox-X), finds the party in the
emulated memory, and lets you edit every character live — ability scores, HP, AC/THAC0, class
levels, XP, money, status, and any raw byte — with a **god-mode** freeze, one-click **max** buttons,
a **combat panel** for zapping enemy HP, a **Cheat-Engine-style memory scanner**, and reference tabs
built from the game's own data files.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

The 422-byte character record it edits was recovered from the game's own files and cross-checked
against rules the game has to satisfy; the write-up is in
[`docs/reverse-engineering.md`](docs/reverse-engineering.md).

---

## Quick start

1. **Launch Curse of the Azure Bonds** in DOSBox/DOSBox-X and play until a party exists (past the
   title screen — the party only lives in memory once loaded).
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `CoabTrainer.exe`, which requests administrator rights via UAC —
   reading/writing another process's memory needs them, especially if the emulator is elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/ScummVM/etc. are auto-sorted to
   the top) and click **🔌 Attach**. It scans memory and lists the party automatically.
4. **Edit:** select a character on the left. Use the **🧙 Character** tab for friendly fields, or the
   **🔢 Raw Bytes** tab for the full 422-byte record with every known offset labelled. Edits are
   written to the game *immediately* (they take effect when you next open the character screen).
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

The trainer decodes the full **422-byte** Curse of the Azure Bonds character record — the same
structure a `CHRDATAn.SAV` file holds, byte for byte. On the friendly tabs:

- **Identity** — name, race, class (incl. multiclass), alignment, gender, age, status.
- **Ability scores** — STR/INT/WIS/DEX/CON/CHA and the exceptional-strength percentile (18/xx).
- **Hit points & combat** — current/max HP, Armor Class and THAC0 (shown as the game shows them —
  lower is better — with the internal `60 − x` encoding handled for you), experience, freeze-HP.
- **Class levels** — each of the eight class-level bytes (set a Fighter to 12, etc.).
- **Money & treasure** — all seven counters: copper, silver, electrum, gold, platinum, gems, jewelry.

The **🔢 Raw Bytes** tab exposes every byte of the record with its known field label, for the fields
the friendly editors don't surface (saving throws, thief skills, spell memorization).

### 🧪 Ability drain and restoration

This is the one thing Curse's record does that its predecessor's doesn't, and the trainer uses it.
Every ability score is stored **twice** — the value in play and the maximum it was rolled at — so a
Ray of Enfeeblement, a shadow's touch or a Feeblemind shows up in the record as the two halves
disagreeing. The Character tab flags a drained character in red and lists what was lost, and
**🧪 Restore drained** puts every score back to its stored maximum, which is exactly what a
Restoration would have done.

It also means editing a score has to write *both* halves, or the next restore silently undoes your
work. The trainer does; the harness asserts it.

### ⚔ Combat panel

Monsters use the **same record format** as characters, so the trainer can list and edit the enemy
combatants exactly like party members. Their records exist only while a battle is on screen, and the
game builds them fresh for each encounter, so the tab **follows the fight by itself** — a background
sweep of the combat arena fills the list when a battle starts, tracks each monster's HP and status as
it's fought, and clears it when the battle ends. No re-scan needed.

**🩸 Weaken** is the button to win a fight with: it leaves the enemy alive on 1 HP with AC 20 and
THAC0 20, so your next blow can't miss and can't fail to kill — and because the *game* applies that
killing blow, the body drops, its treasure joins the encounter's, and the XP is credited.

**💀 Kill** zeroes the records instead. That edits the character sheet, not the fight: the engine
never runs its death routine, so the creatures finish the round and the battle tends to end in a
surrender — which pays no XP and no treasure. It's for walking away from a fight, not for looting
one. You can also freeze your own party's HP through a fight with god mode.

### 🔒 Freeze spells

Casters normally lose a memorized spell when they cast it. Tick **🔒 Freeze spells (party)** in the
toolbar (or **🔒 Freeze spells** on a single character) right after resting/memorizing: the trainer
snapshots that character's 84-byte memorized-spell block and re-stamps it every poll tick, so casting
never uses a spell up. Toggle it off before you re-memorize a different loadout.

### 🎒 Inventory & 🧬 Powers (offline save editor)

These edit the save on disk, so close the game or reload the save afterward; a backup is made
automatically before the first change. The save folder is found for you on startup — Curse writes to
a `SAVE` sub-folder of the game directory (its `CURSE.CFG` records the path).

- **ID'd column** — tick it to identify one item; untick to hide it again.
- **🔎 ID all items** — for the selected character, or **★ every character** at once.
- **⧉ Duplicate inventory** — copy one character's entire inventory onto another.
- **🧬 Powers** — assign persistent effects by editing the character's `CHRDATAn.FX` file.

The same tab hosts the **live** item tools, which edit the running game instead of the save. They
list a character's items by following the game's own item list — the far pointer in its record, then
link to link — so what you see is what the game's item screen shows, in the same order. An item's
*name* is text the game caches and only rewrites when it next draws the item screen, so after
identifying, the ID'd column flips immediately while the full name appears a moment later.

### 🔍 Memory scanner

Some things aren't in the character record at all — the in-combat clock, the encounter counters. The
Memory tab is a small Cheat-Engine-style scanner for exactly those: first-scan a known value (or
Unknown), change it in-game, then narrow with Increased/Decreased/Changed until one address remains,
and Write a new value or Poke an address directly. Candidates are dropped on Detach.

### Reference tabs

Everything here comes from files that ship with the game, not from a walkthrough:

- **🐉 Monsters** — 71 creatures decoded from the game's own `MON*CHA.DAX` archives. Each block in
  those files is a complete 422-byte character record, so the Armor Class, hit points and XP-per-kill
  listed are the game's own numbers.
- **✨ Spells** — all 84 spells, transcribed from the bundled Rule Book's own descriptions. Curse
  reaches **fifth-level** spells on both the clerical and magic-user lists.
- **📖 Rules** — classes, races, racial level limits, the experience tables and spells-per-day, all
  from the Rule Book's appendices.
- **🗺 Maps** — all sixteen levels drawn with the game's own walls and doors, plus live position
  tracking, teleport, and **level identification** (see below).
- **🗺 Strategy** — a condensed guide drawn from the Rule Book and the Adventure Journal.

### 🗺 Maps, teleport, and "which level am I on?"

The **🗺 Maps** tab draws every level's schematic with its **walls, doors, archways and illusory
walls** and its impassable squares. The geometry is decoded from the game's own level data
(`GEO*.DAX` — see [`docs/reverse-engineering.md`](docs/reverse-engineering.md) §7), not transcribed,
so it matches what you walk into. All sixteen explorable levels are there, across the game's five
chapters: Tilverton, Yulash and the Temple of Moander, Zhentil Keep, Dracandros's stronghold, and
Myth Drannor.

**Locate & Teleport** finds the party for real. The position isn't in the character record and its
address moves every DOSBox session, so:

1. Read your **X and Y off the game's own display** and type them in.
2. **📸 Snapshot** — collects every address that could hold them.
3. Walk to a different square, update X and Y, **🎯 Narrow** — drops every address that no longer
   predicts them. Repeat until one remains; usually one Narrow is enough.
4. The green marker then tracks you live, and **🧭 Teleport** sends you to any square — click one on
   the schematic or type the target.

Do it while *exploring*, never mid-combat, then take a step to redraw the map.

**🧭 Identify area** answers the question coordinates can't. Every level in Curse is 16×16, so
knowing you are at (7, 4) says nothing about *which* level you're in. The trainer reads the levels
back out of your install's own `GEO*.DAX` files and looks for one of them resident in the game's
memory — a 512-byte exact match — then selects that map for you. Until a level is identified the
marker is drawn on whatever map you have open and the status line says it's unconfirmed.

That matters here because of an honest gap: Curse's printed maps live in an **Adventurer's Journal
that isn't part of this install**, so unlike the sister trainer there was nothing to match decoded
blocks against by eye. The *chapter* each level belongs to is established from that chapter's monster
roster (module 2 fights bar patrons and sewer otyughs; module 6 ends at Tyranthraxus), and each map
carries the archive block it came from. The **geometry is exact** regardless — and Identify means a
label being wrong costs you a name, never a location or a teleport.

---

## How it finds the party

The party's live address changes every DOSBox session, so the trainer never hard-codes it. Instead it
**signature-scans** the target process for the record shape (`Game/CharacterSignature.cs`): a valid
length-prefixed name, six ability-score **pairs** in range whose maximum is never below the current
value, a race byte ≤ 7, a class byte ≤ 17, positive max HP, and a valid status enum. Curse's paired
scores make that a much stronger filter than a single-byte version — arbitrary bytes have to be in
range twelve times over.

One detail is worth knowing because it bites: a character entered as `"TRAVIS "` is stored with
length 6 and the trailing space still in the buffer. A scanner that insists on NUL padding past the
declared length rejects that record and quietly returns a five-member party. See §4 of the notes.

---

## Verified against the game's own files

The layout isn't guessed, and the checks aren't "byte 0x78 is 49" — they're relationships the game
had no reason to satisfy unless the offsets are right:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

305 checks over verbatim bytes from a real saved party, a monster record from `MON2CHA.DAX`, and item
records from `ITEM1.DAX`. Among them:

- a paladin's THAC0 equals his base minus his 18/00 Strength bonus — and the same holds for all six
  characters at four different Strength scores;
- a party carrying no items has an AC of exactly 10 minus the AD&D Dexterity adjustment, for all six;
- hit points minus the Constitution bonus equal the stored die roll, exactly, for three characters;
- experience reads the Rule Book's documented starting 25,000 — halved to 12,500 for the multi-class
  characters, as the Rule Book says it must be — and those totals buy exactly the class levels the
  record stores;
- a level-5 cleric with Wisdom 17 has the Rule Book's 3/3/1 plus its +2/+2/+1 Wisdom bonus = 5/5/2;
- the effects far pointer resolves 9 bytes before the first link in that character's own `.FX` file;
- both paladins carry effect `0x08` (protected from evil), both dwarves carry the three dwarven
  bonuses, and the elf carries 90% sleep/charm resistance.

---

## Project layout

```
src/CurseOfTheAzureBondsTrainer/
  Game/        CoabFormat.cs         the validated 422-byte offset table + enums
               CharacterRecord.cs    typed, mutable view (paired stats, AC/THAC0 60-x encoding)
               CharacterSignature.cs the record-shape predicate used by the scanner
               SaveGame.cs           offline CHRDATAn.SAV/.FX/.ITM editor (effects, items, backup)
               DaxArchive.cs         the .DAX container + PackBits RLE reader
               EffectBook.cs         the effect/"power" dictionary   InventoryItem.cs  63-byte item record
               MonsterBook.cs        bestiary, generated from MON*CHA.DAX
               SpellBook.cs          all 84 spells, from the bundled Rule Book
               ClassRaceBook.cs      classes, races, level limits, XP and spell tables
               Walkthrough.cs        in-app strategy      MapBook.cs   the sixteen levels
               MapTerrainData.cs     per-level walls/doors, generated from GEO*.DAX
               MapAscii.cs           parses the map format into BoardSquare grids
  Memory/      CharacterLocator.cs   signature scanner (returns party + monsters)
               MapLocator.cs         identifies the loaded level from its resident wall data
               ItemLocator.cs        walks the game's own item linked list
               PositionLocator.cs    party map position
               MemorySearcher.cs     Cheat-Engine-style value scanner
               SaveFolderLocator.cs  finds the folder the running game saves into
               GlobalHotkeys.cs      system-wide Ctrl+F1/F2/F3
               (shared)              ProcessMemory / MemoryRegion — from GameTrainers.Common
  ViewModels/  MainViewModel, CharacterViewModel, MapsViewModel, child VMs, converters
  Mvvm/        ObservableObject, RelayCommand
  App.xaml, MainWindow.xaml          dark, gold-accented UI
test/FormatCheck/                    headless verification against the game's own bytes
docs/          reverse-engineering.md, strategy-guide.md
```

---

## Notes & caveats

- **Tested logic:** the record parser, the paired-stat handling, the `60 − x` encoding, the item and
  DAX decoders, and the generated map/monster data are all verified by `FormatCheck` against bytes
  from the game's own files. **The live paths — attach, scan, the item-list walk, position lock and
  level identification — need the game running to exercise**, and had not been run against a live
  process at the time this was written.
- Edits take effect the next time the game reads the field (e.g. opening the character screen).
  During combat the game tracks a separate combatant copy — use god mode / the combat panel for
  live-fight edits. The status line says so while a battle is on screen.
- Offline save edits go to the files, so the trainer warns if the game is still running — it will
  overwrite them the next time it saves. Set `CURSE_SAVE_ROOTS` (a `;`-separated list of folders) if
  your install is somewhere the save-folder search doesn't look, or just type the path into the box.
- **💀 Kill** asks for confirmation: it forfeits the encounter's treasure and XP.
- Setting values absurdly high (255 HP, huge money) is safe for the trainer, though the game's own UI
  may display very large numbers oddly — that's cosmetic. Coins weigh, so maxing money will floor a
  character's movement.
- Always keep a backup of your save (`CHRDATA?.SAV` / `SAVGAM?.DAT`) before experimenting.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
