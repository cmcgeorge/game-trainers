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

Monsters use the **same record format** as characters, so the trainer can list and edit the enemy
combatants exactly like party members. Their records exist only while a battle is on screen, and the
game builds them fresh for each encounter, so the tab **follows the fight by itself** — a background
sweep of the combat arena fills the list when a battle starts, tracks each monster's HP and status as
it's fought, and clears it when the battle ends. No re-scan needed.

**🩸 Weaken** is the button to win a fight with: it leaves the enemy alive on 1 HP with AC 20 and
THAC0 20, so your next blow can't miss and can't fail to kill — and because the *game* applies that
killing blow, the body drops, its treasure joins the encounter's, and the XP is credited. Use it on
one enemy or the whole arena.

**🔁 Auto-weaken every battle** is the same thing left switched on. Tick it and the poll loop
puts every creature the arena sweep is listing on 1 HP as it appears, so an encounter is already
won by the time the first round is drawn and nothing has to be clicked between fights. It skips a
creature that is already weakened, and one that is already dead or off the field, so it never
re-writes a record needlessly and never stands a corpse back up; a creature you are hand-editing in
the panel beside it is left alone too, until you click away. A monster that has merely been slept or
held is still an enemy, and gets weakened like any other. **🔁 Auto-kill every battle** is
the standing form of Kill, with Kill's cost — it asks once before switching on, because while it is
ticked no encounter pays XP or leaves treasure.

**💀 Kill** zeroes the records instead. That edits the character sheet, not the fight: the engine
never runs its death routine, so the creatures finish the round and the battle tends to end in a
surrender — which pays no XP and no treasure. It's for walking away from a fight, not for looting
one. (Why it can't be fixed from the record alone is traced out in
[docs/reverse-engineering.md](docs/reverse-engineering.md) §5.) You can also freeze your own party's HP through a
fight with god mode.

### 🔒 Freeze spells

Casters normally lose a memorized spell when they cast it. Tick **🔒 Freeze spells (party)** in the
toolbar (or **🔒 Freeze spells** on a single character) right after resting/memorizing: the trainer
snapshots that character's 21-byte memorized-spell block and re-stamps it every poll tick, so
casting never uses a spell up. Toggle it off before you re-memorize a different loadout.

Like god-mode HP freeze, this writes to the live game each tick; if a spell doesn't reappear
immediately after a mid-fight cast it should on the next tick — verify the behavior in your own game.

### 🎓 Change class

The **Class** box in the identity row writes the class byte and nothing else, which leaves a
character whose sheet says "Magic-User" while its saving throws, THAC0 and empty spell book still
describe a fighter. The **Change class** panel further down the 🧙 Character tab does it properly.

Pick the new class — the list offers what the character's **race** may take, with a checkbox to
show the combinations the game would refuse — and the panel previews the whole change before you
commit: the new per-class levels, THAC0, saving throws, and any spells or thief skills, followed by
anything questionable (an illegal race/class pairing, a level the class can't reach, an ability
below the class minimum, experience that doesn't support the level, a lawful-good thief).

The character **keeps its level and its hit points**. A level-5 fighter becomes a level-5
magic-user, clamped to what that class and race can actually reach (an elf fighter stops at 7, a
half-elf cleric at 5, the training halls at Fighter 8 / Thief 9 / Cleric 6 / Mage 6), and keeps the
25 hit points it earned — which is exactly the kind of character only a trainer can make. What gets
rewritten is everything the class decides: per-class levels, THAC0 (keeping whatever your equipment
and Strength were already worth), the level-appropriate saving throws, thief skills with their
racial and Dexterity adjustments, and the spell book — a new caster knows every spell of the levels
it can cast, and a character leaving a caster class loses the lot. Experience, abilities, Armor
Class, money and items are untouched.

Two things the game still gets the last word on: the training hall recomputes progression when you
next level, and readied gear isn't re-checked — a magic-user in plate mail keeps that Armor Class
until you unready it in-game.

### 🧝 Party generator

Rolls a whole party for you instead of six trips through the create-a-character screens. Every
character comes out **good-aligned**, **level 1**, and in a race/class combination the game itself
offers — following the party the Rule Book recommends: front-line fighters, a healer, a scout and a
magic-user. Pick how many to roll (a short party keeps the roles that matter — four is always a
fighter, a cleric, a magic-user and a thief), choose straight **3d6** or **4d6 drop-the-lowest**,
and hit **🎲 Roll a new party** until you like the look of them.

Abilities go to the class that needs them (a Fighter/Mage's best roll lands in Strength, its second
in Intelligence), and everything derived from them is filled in to match: hit points from the class
hit die at maximum plus the Constitution bonus (a multiclass averages its dice, as the game does),
unarmored AC from Dexterity, THAC0 from class and Strength, the level-1 saving-throw row — best-of
for a multiclass — thief skills with their racial and Dexterity adjustments, and a starting spell
book that always includes **Sleep** and **Magic Missile**. Exceptional Strength is rolled only for
fighters at STR 18, and only up to 18/50 for a female fighter, exactly as the game allows.

Then write the party to either target:

- **🧝 Replace the live party** — stamps the characters over the party in the running game, in
  marching order. Takes effect immediately; save in the game to keep them.
- **💾 Write into the loaded save** — rewrites the `CHRDATn.SAV` records of the save loaded on the
  Powers/Inventory tab, after backing the whole folder up. Close the game first.

Both **replace characters that already exist** — neither can add new party members, because how many
members a party has is the game's own bookkeeping (a linked list in memory, and a count somewhere in
`SAVGAM?.DAT` that this trainer hasn't decoded). So make six characters in the game however quickly
you like, then generate over them. Each slot keeps its **money and carried items** — only the
character sheet changes — which means readied armour may no longer suit the new class until you
re-ready it in-game and the AC recomputes.

### 🎒 Inventory (offline save editor)

Like the **🧬 Powers** tab, this edits the save on disk (each character's `CHRDATAn.ITM` file), so
close the game or reload the save afterward; a backup is made automatically before the first change.
The save folder is found for you on startup — including the `cloud_saves\POOLRAD` overlay folder a
GOG install really writes to — and the most recent of the save slots in it is loaded. It lists every
carried item (as the game shows it, with ready/identified/cursed flags) and offers:

- **ID'd column** — tick it to identify that one item; untick to hide it again.
- **🔎 ID all items** — for the selected character, or **★ every character** at once: reveals each
  item's full name (sets the "hidden-names" flag to identified). Great for unidentified magic.
- **⧉ Duplicate inventory** — copy one character's entire inventory onto another, replacing it.

It shares the save you load on the **🧬 Powers** tab (same *Save folder* box).

The same tab hosts the **live** item tools, which edit the running game instead of the save. They
list a character's items by following the game's own item list — the far pointer in its record, then
link to link — so what you see is exactly what the game's item screen shows, in the same order, and
the list keeps itself up to date as you pick things up. Note
that an item's *name* is text the game itself caches and only rewrites when it next draws the item
screen — so after identifying, the ID'd column flips immediately while the full name ("Long Sword"
→ "Long Sword +2 Flame Tongue") appears a moment later, once the game redraws. The list follows the
game as that happens; no re-scan needed.

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
- **🗺 Maps** — each district drawn with the game's own walls and doors, the overland Moonsea map
  with its terrain and landmarks, keyed locations with their `(x, y)` coordinates, plus **live
  position tracking and teleport** (see below).
- **🗺 Strategy** — a condensed walkthrough; the full guide with maps is in `.docs/strategy-guide.md`.

### 🗺 Maps & teleport

The **🗺 Maps** tab draws every area's schematic with its **walls, doors, archways and illusory
walls**, water and impassable squares, plus keyed locations with coordinates. The indoor geometry is
decoded from the game's own level data (`GEO*.DAX` — see `docs/reverse-engineering.md` §7a), not
transcribed, so it matches what you walk into.

**All 29 of the game's levels are there**, not just the city districts: the Phlan blocks and their
undersides (Kuto's Well catacombs), the Temple of Bane and Valhingen Graveyard, every wilderness
location you can enter — Nomad Camp, Kobold Caves, all four levels of Yarash's Pyramid, the Lizard
Man Keep and its catacombs, the Buccaneer's Base, the Outpost of Zhentil Keep — and the whole
endgame: Stojanow Gate, the four Valjevo Castle quadrants, and both floors of the Inner Tower.

The **wilderness** — the overland Moonsea map you reach by boat once Sokal Keep is cleared — is
there too, 42×33 with its terrain (plains, swamp, forest, hills, mountains, river, deep water) and
every lettered landmark: the city-edge squares back to Phlan, the boat landings, Yarash's Pyramid,
the Nomad Camp, Zhentil Keep Outpost, the Kobold Caves, Lizardman Keep and the rest. Unlike the
districts, that terrain is **transcribed from the clue-book map**, because the game keeps no
overland grid to decode — the squares are a travel aid, while your live position and the teleport
target are read from and written to the game itself and are exact.

**Locate & Teleport** finds the party for real, on either kind of map. The position isn't in the
character record and its address moves every DOSBox session, so:

1. Read your **X and Y off the game's own display** and type them in.
2. **📸 Snapshot** — collects every address that could hold them.
3. Walk to a different square, update X and Y, **🎯 Narrow** — drops every address that no longer
   predicts them. Repeat until one remains; usually one Narrow is enough.
4. The green marker then tracks you live, and **🧭 Teleport** sends you to any square — click one on
   the schematic, pick a keyed location, or type the target.

Do it while *exploring*, never mid-combat, then take a step to redraw the map. Indoors the marker is
an arrow showing your facing; in the wilderness the game records no facing, so it is a plain dot.
Locking in the wilderness also selects the overland map for you, and the marker is hidden whenever
you are browsing a map the party is not standing on.

Under the hood the two worlds store position differently — indoors it is three adjacent bytes
`[X][Y][Facing]`, in the wilderness a pair of 16-bit words whose X is offset by a constant — which is
why the tab searches for both shapes at once and lets the loser die at the first Narrow. Details in
`docs/reverse-engineering.md` §7b.

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
ALTHARION       Male Human Fighter (LG) L5   STR 18/89  HP 25/25  THAC0 base 16  XP 34,135
```

The third is a verbatim `CHRDATA1.SAV` from a real saved game, and it is the only fixture above
level 1 — which is what lets the per-level class tables (THAC0, saving throws) be checked rather
than assumed: its THAC0 base of 16 is four points better than the level-1 fighter's 20, four levels
up, and its saving throws are the fighter level-5 row exactly.

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
               ClassTables.cs        per-class/per-level THAC0, saves, thief skills, spell slots, caps
               ClassChange.cs        changes a character's class and every number that follows from it
               PartyGenerator.cs     rolls a good-aligned level-1 party and stamps it into records
               EffectBook.cs         the effect/"power" dictionary   InventoryItem.cs  63-byte item record
               MonsterBook.cs        bestiary + XP values      SpellBook.cs      cleric/mage spells
               ClassRaceBook.cs      classes/races/XP tables    Walkthrough.cs    in-app strategy
               MapBook.cs            areas + keyed location coordinates (Maps tab)
               MapTerrainData.cs     per-area walls/doors, generated from the game's GEO*.DAX
               WildernessMap.cs      the overland Moonsea map (transcribed — the game has no grid)
               MapAscii.cs           parses both map formats into BoardSquare grids
  Memory/      NativeMethods.cs      hotkey P/Invoke (memory P/Invokes now in GameTrainers.Common)
               CharacterLocator.cs   signature scanner (returns party + monsters)
               PositionLocator.cs    party map position — indoor bytes and wilderness words
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
  the memory scanner for live-fight edits. The status line says so while a battle is on screen,
  because a party edit made mid-round can look like it did nothing.
- Some emulators can map guest RAM more than once. The scan drops the duplicate mapping, but only
  when the identical record is further away than any real creature could be: two same-species
  monsters standing next to each other in one fight are byte-for-byte identical at the start of it,
  and both are kept.
- Offline save edits go to the files, so the trainer warns if the game is still running — it will
  overwrite them the next time it saves. Set `POOLRAD_SAVE_ROOTS` (a `;`-separated list of folders)
  if your install is somewhere the save-folder search doesn't look.
- **💀 Kill** asks for confirmation: it forfeits the encounter's treasure and XP and cannot be undone.
- Setting values absurdly high (255 HP, huge money) is safe for the trainer, though the game's own UI
  may display very large numbers oddly — that's cosmetic.
- Always keep a backup of your save (`CHRDATA?.SAV` / `SAVE*`) before experimenting.
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
