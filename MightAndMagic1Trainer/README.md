# Might & Magic 1 — Live Trainer

A WPF (.NET 8) trainer for the 1986 DOS game **Might and Magic Book One: The Secret
of the Inner Sanctum**. It attaches to the running game (e.g. inside DOSBox),
finds the character roster in the emulated memory, and lets you edit every
character live — HP, SP, attributes, level, class, and any raw byte — with
freeze ("god mode") toggles and one-click "max everything" buttons.

Around that core it adds tools built from reverse-engineering `Mm.exe`: a vector
**auto-map** drawn from the game's own maze data with exact teleport, an **auto-fight**
loop, a **roll predictor** that reads the game's RNG and forecasts upcoming rolls, plus
reference tabs (spells, items, monsters, classes, walkthrough). The decode work behind
them is written up under [`docs/`](docs/).

> Single-player cheat tool for your own save. Nothing here touches other
> machines or online services.

---

## Quick start

1. **Launch the game** in your emulator and play until you're past the title
   screen and the party exists (the roster only lives in memory once loaded).
2. **Build & run the trainer:**
   ```powershell
   dotnet build -c Release
   # then run the produced MM1Trainer.exe (it self-elevates via UAC)
   ```
   The app requests administrator rights — reading/writing another process's
   memory needs them, especially if the emulator itself runs elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/ScummVM/etc.
   are auto-sorted to the top) and click **Attach**. It scans memory and locates
   the roster automatically.
4. **Edit:** select a character on the left. Use the **Character** tab for
   friendly fields, or the **Raw Bytes** tab for the full 127-byte record. Edits
   are written to the game *immediately*.
5. **Cheat fast:** tick **Freeze HP / Freeze SP** for god mode, or hit
   **★ Max EVERYTHING** for one character or the whole party. The **Gold / Gems /
   Food "can't drop"** toggles are one-directional ("no-loss"): the party may still
   earn or find more, but the value is restored whenever the game tries to lower it
   (spell costs, purchases, theft). Available per-character or party-wide.
   The eight **resistances** (Magic, Fire, Cold, Electricity, Acid, Fear, Poison,
   Sleep — each a normal/current percent pair) have their own editor section and
   **Max resistances (100%)** buttons, per character and party-wide.

If the scan finds nothing, make sure a party is actually loaded, then click
**Re-scan**. If several candidates are found, the **Roster location** dropdown
lets you choose (the best/longest match is selected by default).

### Global hotkeys

Three system-wide hotkeys work even while the **game** window has focus, so there's
no alt-tabbing mid-fight:

- **Ctrl+F1** — toggle god mode (party-wide HP/SP/condition freezes on or off)
- **Ctrl+F2** — heal the party once (HP/SP refilled to max, conditions cleared)
- **Ctrl+F3** — ★ Max EVERYTHING, party-wide

If another application already owns one of these combinations, the status bar says
so at startup and the rest still work.

### Party snapshots

**📸 Snapshot…** (Characters sidebar) saves the whole party to a file;
**↩ Restore…** writes a saved snapshot back onto the current party, slot by slot
(live characters are pushed to the game immediately). It's an undo for experiments
gone wrong and a way to keep "known good" parties around. Snapshots use the plain
roster file format, so a snapshot can also be opened with the offline browse button
or even copied over a real `ROSTER.DTA`.

### Slot tools

With a character selected, the **Slot tools** row on the Character tab copies or
swaps records between roster slots: pick the other character, then **⧉ Copy onto**
(duplicate this character over that slot) or **⇄ Swap with** (reorder the roster).
The record's slot-index byte is fixed up automatically. Copy overwrites the target —
snapshot first if unsure.

### Offline browsing & editing

The **Load Roster.dta (offline)** button parses a save file so you can inspect and
edit characters without the game running. Edits stay in the trainer until you click
**💾 Save roster file**, which writes the records back to the same file — the
previous version is kept beside it as `<file>.bak`, so one bad save is always
undoable. (Attaching to a live game ends file mode; the save button greys out.)

### Cast a spell (Spells tab)

Casting in-game means a long key walk (select character → **c** → spell level →
spell number → **Enter**). The **🪄 Spells** tab's **Cast a spell** picker does that
walk for you: select the caster in the party list, choose a spell by name, and the
trainer figures out the keystrokes from the character's **class** and **known spell
level**. The dropdown lists the whole school grouped by level (Cleric spells for
Clerics/Paladins, Sorcerer spells for Sorcerers/Archers), shows each spell's SP/gem
cost and a description, and dims (🔒) any spell above the caster's level. The exact
keys it will send are shown under **Keys sent**; click **▶ Cast spell** to play them.

> It sends the caster's roster-slot number as the in-game character digit and ends at
> the spell's target prompt — pick the target (monster / character) yourself, since
> the trainer can't read the game's screen.

**📖 All spells & descriptions** opens a reference pop-up listing every Cleric and
Sorcerer spell grouped by level, with costs and descriptions — handy regardless of
which character is selected. It's modeless, so you can keep it open beside the trainer.

### Quick-cast macros (Spells tab)

For sequences the spell picker doesn't cover, the **Quick-cast macros** section below
it turns any hand-written key sequence into a one-click button. Attach first, then
click **▶ Cast** — the trainer brings the game window to the foreground and replays
the keys via `SendInput` (DOSBox reads real hardware input, so this is the reliable
way).

- **Sequence syntax:** type the characters to press (spaces between them are
  ignored); use tokens like `{ENTER} {ESC} {SPACE} {UP} {DOWN} {LEFT} {RIGHT}` and
  `{DELAY:200}` to pause mid-sequence. Example for "character 5 casts level-1 spell
  6": `5 c 1 6 {ENTER}`.
- Tune **Delay between keys** / **Pause after focusing** if the game drops keys.
- Macros are saved to `%APPDATA%\MM1Trainer\macros.json` and reloaded next launch.

> The macro just presses keys for you — it can't read the game's state, so make sure
> the game is on the screen the sequence expects (e.g. the combat/explore view).

### Memory search & edit (Memory tab)

Some things aren't in the character roster at all — notably the party's **map
position (North/East) and facing direction**, which live in separate game state.
The **🔍 Memory** tab is a small Cheat-Engine-style scanner for exactly this:

1. Pick a **width** (Byte / Int16 / Int32).
2. If you know the number, type it and **First-scan = value**. If you don't (e.g.
   position, which the game never shows numerically), click **First-scan Unknown**.
3. Change the value in-game (take a step north), then narrow with **▼ Decreased**,
   **▲ Increased**, **≠ Changed**, or **= Unchanged**. Repeat until a single address
   remains.
4. Select the address and **Write** a new value — or use **Manual poke** if you
   already know the address.

Candidates are dropped on **Detach** (addresses are only meaningful while attached).

### Live map marker & teleport (Maps tab)

Once the X/Y search is narrowed to a **single** address, the **🗺 Maps** tab can show
the party live on the bundled reference maps — and teleport it two ways:

**Type a destination (most reliable).** Enter the target **X** and **Y** (each 0–15,
the game's per-area cell coordinates) next to **🚀 Teleport** and the trainer writes them
straight to the locked position address. This needs no calibration and is exact — the
boxes start pre-filled with the party's current cell.

**Click the map.** First teach the trainer where game cell (x, y) sits in the image:
tick **✛ Mark party position**, then click the party's actual spot on the map; move
somewhere else in-game (different row *and* column) and mark once more — two anchors
calibrate the map. A gold marker then tracks the party as you walk, and ticking
**🚀 Teleport on click** moves it to whatever cell you click.

Click-teleport only works *within the area the party is already in*: the game stores the
position as a 0–15 cell per 16×16 area and tracks *which* area you're in separately, while
most bundled maps show several areas at once. Clicking outside that area is rejected (it
would corrupt the position rather than move you) — type the X/Y to reposition within an area.

Calibration is per map image and persists to `%APPDATA%\MM1Trainer\map-calibration.json`
(**🗑 Clear calibration** forgets a mis-marked map; marking a third time replaces the
older anchor). The trainer can't know *which* map the party is on — pick the right map
yourself, and re-lock the position after entering a new area if the game keeps it at a
different address.

### Auto-drawn map & exact teleport (Map (drawn) tab)

Unlike the image-based **Maps** tab, the **🧭 Map (drawn)** tab needs no scanned pictures
and no calibration: it decodes the game's own `Mazedata.dta` and draws each of the 55
mazes as crisp vector graphics — walls, doors, and secret/illusory passages. Click
**Load Mazedata.dta** and point it at the file in your game folder (it also tries
`C:\Temp\Games\MM1\Mazedata.dta` automatically and remembers your choice).

- **Current map, detected automatically.** The running game keeps the active 16×16 maze in
  memory byte-for-byte, so the trainer fingerprints it against the 55 known records and
  selects the matching map for you — the match is exact (all 55 fingerprints are unique).
  Untick **Follow current map** to browse the set freely.
- **Live party cell.** Lock the party's X/Y once via the **📍 X / Y Search** tab; the trainer
  then learns the position's memory location and shows a gold marker that tracks you as you
  walk — no re-locking needed on later sessions.
- **Teleport to an exact cell.** Type a destination **X / Y** (each 0–15) and **🚀 Teleport**,
  or tick **🚀 Teleport on click** and click any cell. Both write the exact cell, so unlike
  the image map there's nothing to calibrate.

### Auto-fight (Auto-fight tab)

The **⚔ Auto-fight** tab replays a key sequence each combat round while a fight is detected,
then stops on its own when combat ends — handy for grinding easy encounters. Set the keys
(default `a a b {SPACE} {SPACE} {SPACE}` — attack, attack, block, then clear the messages),
attach past the title screen, and tick **Enable**. Combat is detected from the game's own
"non-combat-only" gate, read straight from memory.

> The detection is reverse-engineered and **not yet confirmed live** — watch the game-state
> readout ("in combat: yes/no") to verify it tracks your fights before leaving it unattended,
> and keep the game window able to take focus (it sends real keystrokes via `SendInput`).

### Roll predictor (Roll Predictor tab)

Might & Magic 1's "random" numbers come from a deterministic 32-bit shift register, so once
you can read its live state every upcoming roll is predictable. The **🎲 Roll Predictor** tab
reads that state each tick and shows, for each common die (d2 … d100), the **next** result and
the next eight — each row assuming that die is the next draw the game makes. As the game rolls
and the state advances, the predictions update. It uses a byte-exact reimplementation of the
game's `rand(n)` (guarded by self-test vectors); attach and get past the title to populate it.

### Compare two dumps (Dump tab)

The Dump tab's **Compare two dumps** section diffs two saved dumps and lists every
changed byte run **by its live process address** — the reverse-engineering loop
(dump → change one thing in-game → dump → diff) without leaving the trainer. Pick
the older and newer `.bin` (each needs its `.csv` index beside it) and **⇆ Compare**:
the grid shows address, length and the before/after bytes, ready to poke in the
Memory tab. The comparison is done per address (not file offset), so it stays correct
even if the emulator remapped memory between dumps; runs separated by ≤q 4 unchanged
bytes are merged, and the diff stops with a warning if more than 2,000 runs differ
(too much changed between dumps to be useful).

### Roll a hero (Roll a Hero tab)

Rolling a good new character in the tavern means hitting **Enter** over and over until
the stats come up the way you want. The **🎲 Roll a Hero** tab does that for you: it taps
Enter and reads each fresh roll straight from the game's memory, stopping when your
target is met.

Because a freshly-rolled character isn't saved to the roster yet, the trainer first has
to *find* the temporary roll in memory:

1. On the game's **CREATE NEW CHARACTERS** screen, type the **seven numbers currently
   shown** into the *On screen* boxes (Intellect, Might, … Luck), then click
   **🎯 Lock onto roll**. The trainer signature-scans for those exact values (and, if
   several spots match, re-rolls a few times to settle on the live one).
2. Set your **target**: a **minimum for each stat** you care about (e.g. Intellect ≥ 18,
   Personality ≥ 12). Leave a stat at **0** to ignore it. The roller stops once every
   stat is at or above its minimum.
3. Click **🎲 Roll until target met**. The trainer re-rolls, showing each result in the
   *Live* column and tracking the best roll seen. When a roll meets the target it stops
   and brings the game to the front so you can press the class number to keep the hero.
   **⏹ Stop** halts it any time; *Advanced timing* tunes the per-roll delays and the
   safety cap on total rolls.

Under the target the tab shows the **odds** of your target on any one roll, modelled as
seven independent rolls of three six-sided dice (3d6) — e.g. *“about 1 in 1,700 rolls”* —
with a rough time estimate, and warns when a minimum above 18 makes the target impossible
for 3d6.

The **Statistics** section tallies every fresh roll read this session — each attribute's
running average and observed range, plus the total's average, range, and spread (σ) — and
compares them with fair 3d6 (each stat should average 10.5 over 3–18, totalling ~73.5). From
that it gives a plain-language verdict on whether the game looks like it's rolling fair dice
or **quietly steering the result** (for instance, capping the total so an all-18 hero can
never appear), or whether the stats fall outside 3–18 entirely (bonuses or a different
method). **🗑 Clear statistics** resets the tally. *Note:* the check assumes the game intends
3d6; if M&M1 applies racial bonuses or uses another scheme, the “outside 3–18” verdict is
itself the answer.

The lock and the statistics are dropped on **Detach** (they belong to that game session).

---

## How it finds the roster

The same character record exists both in `ROSTER.DTA` on disk and in the game's
live RAM. The trainer doesn't rely on a fixed address (which changes every launch);
instead it **signature-scans** the target process for runs of consecutive records
that match `RosterFormat.LooksLikeRecord` — an uppercase name field, a class byte
in 1–6, a sex byte in 1–2, and a structural marker (`0x46 0x46` at +0x62) that is
constant across every record (sample and freshly created). The longest consecutive
run at the 128-byte memory stride wins, which reliably isolates the real roster
from stray false positives.

---

## Reverse-engineered record format

Records are **127 meaningful bytes**. On disk they are packed end-to-end every
127 bytes; in live memory they are padded to a **128-byte stride** (confirmed by
the bundled `docs/MM.CEM` Cheat Engine dump, where the records start at file
offset `0x1B`). Offsets below are relative to the start of a record.

| Offset | Size | Field |
|-------:|:----:|-------|
| `0x00` | 15  | Name (ASCII, NUL-padded; `0x0F` = terminator) |
| `0x10` | 1   | Sex (1=Male, 2=Female) |
| `0x11` / `0x12` | 1 / 1 | Alignment (original / current) |
| `0x13` | 1   | Race (1=Human, …) |
| `0x14` | 1   | Class (1=Knight 2=Paladin 3=Archer 4=Cleric 5=Sorcerer 6=Robber) |
| `0x15`–`0x22` | 7×(1+1) | Seven attributes, each `[normal, current]`. Order: Intellect, Might, Personality, Endurance, Speed, Accuracy, Luck |
| `0x23` / `0x24` | 1 / 1 | Level current / base (current is drainable) |
| `0x25` | 1   | Age |
| `0x26` | 1   | Times rested (rolls into age at `0xFF`) |
| `0x27` | 4   | Experience (UInt32 LE) |
| `0x2B` / `0x2D` | 2 / 2 | Spell points current / max (UInt16 LE) |
| `0x2F` | 1   | Highest spell level known |
| `0x31` | 2   | **Gems** (UInt16 LE) |
| `0x33` / `0x35` / `0x37` | 2 each | Hit points current / modified / max (UInt16 LE) |
| `0x39` | 3   | **Gold** (UInt24 LE) |
| `0x3C` / `0x3D` | 1 / 1 | Armor class (from items / total) |
| `0x3E` | 1   | **Food** |
| `0x3F` | 1   | **Condition** (0 = OK) |
| `0x40`–`0x45` | 6 | Equipped item ids |
| `0x46`–`0x4B` | 6 | Backpack item ids |
| `0x4C`–`0x51` | 6 | Equipped item charge counts (one per equipped slot) |
| `0x52`–`0x57` | 6 | Backpack item charge counts (one per backpack slot) |
| `0x58`–`0x67` | 8×(1+1) | Resistances, each `[normal, current]` percent. Order (per ryz/MightAndMagic-SaveEditor): Magic, Fire, Cold, Electricity, Acid, Fear, Poison, Sleep. Every character innately has Fear 70/70 and Sleep 25/25 — the Fear pair is the scan marker above |
| `0x7E` | 1 | Slot index |

The friendly editors cover all of the above; the **Raw Bytes** tab annotates each
known offset and exposes every other byte (resistances, quest progress, …) for
hand-editing.

### Inventory (Inventory tab)

The per-character **Inventory** sub-tab lists all 12 slots — 6 equipped, 6 backpack —
and lets you set each slot's **item** (choose from the game's full item list by name;
the leading number is the raw item id, `0` = empty — type in the dropdown to jump to an
item) and its **remaining charges**. The **Enh.** dropdown beside an item switches a weapon
or piece of armor between its base and **+1 / +2 / +3** versions (in MM1 these are separate
items, so the trainer swaps the slot to the matching item id); it's disabled for named
uniques that have no enhanced version. Tick **🧊 Freeze** beside a slot to pin that charge
count so the item is never used up, **Freeze ALL charges** for the whole character, or
the **Freeze item charges — whole party** toggle in the Characters sidebar to apply it to
everyone. (Item charges are pinned to the shown value each tick, the same way HP/SP are
held at max.)

The item list is the game's own 255-entry item table, extracted verbatim from `MM.EXE`
(255 × 24-byte entries: a 14-byte name plus cost / damage / armor bonus / charges) and baked
into `Game/ItemBook.cs`. The slot byte is a 1-based index into that table, so the names line
up exactly with what the game shows. The **🎒 Items** reference tab lists **all 255 items**
grouped by category, each with its id, shop cost and damage/armor/charges.

### Monsters reference (Monsters tab)

The **🐉 Monsters** tab lists the game's complete bestiary — **all 195 monsters**,
extracted verbatim from `MM.EXE` the same way as the items (195 × 32-byte entries at
file offset `0x1B2F2`: a 15-byte name plus group size / HP / AC / damage / attacks /
speed / experience) and baked into `Game/MonsterBook.cs`. Monsters are grouped the way
the game's data orders them: ten difficulty tiers of sixteen (groups I–X, which random
encounters draw from by area danger level), sixteen aquatic monsters, and the fixed
special encounters. A search box filters by name or id. The remaining record bytes
(special attacks, resistances, treasure) aren't decoded yet and are omitted rather
than guessed.

Each item also shows a descriptive line: its **equip effect** (elemental resistance, attribute
bonus, armour-class bonus, thievery, or a curse) and which **classes & alignments** can use it —
MM1 has no prose item descriptions, so this is the game's own structured effect data. Those
fields live in the same `MM.EXE` records as the base table but weren't part of the verbatim
cost/damage extraction; they are transcribed into `Game/ItemEffectBook.cs` from Andrew Schultz's
[Might & Magic I item FAQ](http://alexandria.rpgclassics.com/PC/mightandmagic/mightandmagic_1.txt)
(Apple IIe data) and joined to `ItemBook` by name — 254 of the 255 ids match exactly; `OBSIDIAN
BOW` (id 85) is absent from that list and so shows no effect line. The Items search box matches
this text too, so you can filter by e.g. `cursed`, `knight`, or `fire`.

### How the layout was derived & verified

The offsets were cross-checked against the open-source
[ryz/MightAndMagic-SaveEditor](https://github.com/ryz/MightAndMagic-SaveEditor)
(`Character.cs`) and confirmed against the bundled `docs/` files. The decoded
values are internally consistent — spell points are non-zero only for casters
(Cleric 54, Sorcerer 81; Knight & Robber 0), the whole party shares ~255 k XP and
39 food, and every character reads condition 0 (OK). The `FormatCheck` console
project asserts these invariants:

```
ALARIC  Knight    L9  HP 108/108  SP 0/0    AC 12  XP 255493  Gold 17956  Gems 2348  Food 39  Cond 0(OK)
FARAMIR Sorcerer  L9  HP 54/54    SP 81/81  AC 4   XP 257318  Gold 16153  Gems 2309  Food 39  Cond 0(OK)
```

---

## Project layout

```
src/MightAndMagic1Trainer/
  Game/        RosterFormat.cs      field offsets, enums, record signature
               CharacterRecord.cs   typed view over a 127-byte buffer
               PartySnapshot.cs     party snapshot build/parse (roster file format)
               MapCalibration.cs    two-anchor map-image ⇄ game-coordinate transform
               MonsterBook.cs       the 195-entry bestiary extracted from MM.EXE
               Lfsr.cs              byte-exact port of the game's LFSR rand(n) (roll predictor)
               MazeData.cs          decodes Mazedata.dta into the 55 vector mazes
  Memory/      RosterLocator.cs     signature scanner (game-specific)
               RollScanner.cs       locates the create-screen roll buffer for the roller (game-specific)
               DataSegment.cs       fixed DS-offset reader for the game's globals (offset-map.md)
               (shared)             NativeMethods, ProcessMemory, MemoryDumper, DumpComparer,
                                    GlobalHotkeys, KeyboardSender, MemorySearcher — see GameTrainers.Common
  ViewModels/  MainViewModel, CharacterViewModel, StatViewModel, HexByteViewModel,
               DrawnMapViewModel, AutoCombatViewModel, RollPredictorViewModel, …
  App.xaml, MainWindow.xaml         dark, two-pane UI (party list + editor/hex tabs)
test/FormatCheck/                   headless verification against docs/Roster.dta & MM.CEM
docs/                               sample Roster.dta + MM.CEM, plus reverse-engineering write-ups:
                                    offset-map.md, formulas.md, maze-atlas.md, ovr-format.md,
                                    ovr-events.md, remaster-design.md
```

Run the format checks any time with:
```powershell
dotnet run --project test/FormatCheck
```

---

## Notes & caveats

- Tested logic: record parsing, round-tripping, and the locator predicate are
  verified by `FormatCheck` against the bundled files. The live attach/scan path
  needs the actual game running to exercise.
- Some emulators map guest RAM in ways that can produce more than one signature
  hit; use the location dropdown if the auto-pick isn't the party you expect.
- Setting values absurdly high (0xFFFF HP, etc.) is safe for the trainer but the
  game's own UI may display them oddly — that's cosmetic.
- Always keep a backup of your `ROSTER.DTA` before experimenting.
```
