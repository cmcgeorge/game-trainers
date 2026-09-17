# Space 1889 Trainer

A Windows/WPF live-memory trainer and save editor for **Space: 1889** (Paragon Software, 1990), running under
DOSBox or DOSBox-X.

You never search for values. The trainer finds the game's 20,198-byte state block by signature, typically in under
50 ms, and reads and writes the party, the ether flyer and the world directly. There is no Cheat-Engine-style scan
step and no address to type in. A saved game is a byte-for-byte copy of the same block, so the Save Editor edits
`.SAV` files with the same editors.

![](src/Space1889Trainer/Assets/app.ico)

---

## What it does

**Party.** The trainer edits every character's:

- name and sex;
- **wealth** (in pennies, with a £sd read-out) and **monthly income**;
- health and maximum health, with the game's own "HEALTH: a/b" line and whether the character is out of action;
- fatigue, mental state (insanity), horse and body weight;
- all **six attributes** and all **24 skills**.

Quick actions per character: **Full heal**, **Attributes 6**, **Skills 6**, **Reload guns**, **+£1,000** and **Max
everything**. **Heal all**, **Reload all guns** and **Max everyone** apply to the whole party.

**Inventory.** All 21 slots per character:

- change the item (any of the 181 real items, by name) and the loaded ammunition, ROUNDS and IN GUN;
- toggle the "in use" flag that lamps, clothing and water breathers need;
- **Equip** a weapon or armour the way USE does;
- **Remove** an item (later items shift down and the weapon/armour slots are renumbered, as DROP does);
- **Add** any item to the next free slot (firearms arrive loaded).

**Freezes.** Health, fatigue and mental state, each character's wealth, the party account, food, and loaded guns.
A value freeze re-seeds whenever you deliberately change the field, so it holds what you last asked for.

**World and flyer:**

- **party account**, **food** (up to 32,767) and the **day**;
- the current planet, area and map, and whether it is lit;
- **teleport** to any row and column of the current map;
- the **ether flyer**: hull, lift, propeller, power, engine, armour, top and bottom guns, ammonia and glow-crystal
  fuel, and **Repair flyer**;
- the bridge crew;
- the twelve **story flags**, each explained (Inner Earth entrance open, Tutankhamen's tomb opened, Edison rescued, …).

**Maps.** Every map in your own copy of the game (144 of them) is drawn either with the game's tiles or as a
schematic. Exits, shops, stairs, NPCs, the scripted dig and drop squares and the entrances a story event reveals are
labelled; hover over a square for its row, column, tile and the names and notes on it. While attached, the tab **follows the party**. **Click any walkable square of the party's current map to
teleport there**. Walls, water and shop signs are refused unless you tick "Allow teleporting onto any square".

**Save Editor.** Opens `NAME.SAV` (or the shipped `DEF.S`) with the game closed. It edits characters, inventory and
world fields, heals or maxes everyone, and saves with a one-time `.SAV.bak` backup. **Compare with running game**
lists the byte ranges where the file and the live game differ.

**Reference.** The full item catalogue from `ITEMS.DAT`, filterable, with price, weight, shops, shots, damage and
range. Also the 24 skills and what each does, the careers and classes, and the story flags.

**Cluebook.** Writes a self-contained HTML strategy guide to your Documents folder. It covers the rules as the code
implements them, controls, a full walkthrough with coordinates, side quests, locked doors and dig sites, the item
catalogue, and schematic maps of every city and planet drawn from your game folder.

## Requirements

- Windows 10 or 11 with the .NET 8 SDK (to build) or the .NET 8 Desktop Runtime (to run a build).
- Space: 1889 for DOS, running in DOSBox 0.74, DOSBox-X or DOSBox Staging.
- Administrator rights. The manifest asks for them, because reading another process's memory needs them.

## Use

1. Start the game (`1889` in DOSBox) and begin or load a game.
2. Run `.\Run.ps1` in this folder (accept the UAC prompt), or `.\Run.ps1` from the repository root and pick
   *Space1889Trainer*.
3. Pick the emulator in the drop-down and press **Attach**. The status line reports where the state block was found
   and which Space 1889 program vouched for it.
4. Edit. Changes are written immediately. The game redraws its status panel only now and then: open the **PARTY**
   screen (`P`) or change leader (`L`) to see an edit.

The **game folder** (for maps, the Save Editor and the cluebook) is found automatically from the emulator's
`mount` lines or the environment variable `S1889_GAME_DIR`. Otherwise set it on the Maps tab.

Tips:

- **Game time runs in real time.** GAME → PAUSE (`G`, then `P`) stops the clock while you edit.
- A teleport takes effect on screen after your next step.
- Save editing: save in the game (`G`, `S`), edit the file, then load it (`G`, `L`). Do not load a file while you
  have unsaved edits to it open in the trainer.

## How it finds the game

`1889.COM` allocates one DOS memory block for the game state, then chains `SETUP.EXE`, `CG.EXE`, `M.EXE` (the ground
game) and `S.EXE` (space) in and out of the rest of memory. Every one of them keeps a far pointer to that block, and
a save is a verbatim copy of it. The trainer:

1. sweeps the emulator's memory for `"PARTY ACCT."`, the name the game gives the sixth "character" (the party bank
   account), at offset 0x5ED of the block;
2. validates each candidate block: every character slot is empty or holds a terminated name and an `m`/`f` sex
   byte, at least one is filled, and the planet byte is in range;
3. corroborates it by finding a Turbo C data group whose state pointer resolves to that block: `M.EXE DS:0x5737`,
   `S.EXE DS:0x2DD5` or `CG.EXE DS:0x1DF5`. Guest addresses are pinned through the emulated BIOS data area.

Every write re-validates the live block first, and writes only the bytes a field owns. The full field map and the
evidence behind every offset are in [docs/Space1889-Reverse-Engineering.md](docs/Space1889-Reverse-Engineering.md).

## What it does not do

- **Move the party between maps or planets.** The engine loads a map when you walk into it, and nothing the trainer
  writes makes it reload one, so teleporting is confined to the party's current map.
- **Revive a dead character.** Death wipes the whole record. A dead slot is shown as empty.
- **Hand out quest events.** Setting a story flag reveals the map change it controls the next time that map loads,
  but it does not give the items that event gives. Add those on the inventory grid.
- **Guarantee the flyer fields.** They were decoded from the ship builder and `S.EXE`'s combat code but never
  exercised in a live flight. Change them while landed, and keep a save.

## Layout

```
Space1889Trainer.sln
src/Space1889Trainer/
  Game/        StateFormat (the block's offset table), GameState (cached block + targets), CharacterRecord,
               WorldRecord, SaveGame, ItemBook (baked from ITEMS.DAT), ReferenceBooks, GameFacts
  Memory/      GameLocator (anchor + validation + pointer witness), LiveStateTarget, IMemorySource
  Files/       GameFolder, PicFile, MapFile, AreaFiles (SET/NPC), TileRules, MapLibrary, MapAnnotations
  ViewModels/  MainViewModel (attach, poll, freezes), CharacterViewModel, WorldViewModel, MapsViewModel,
               SaveEditorViewModel, ReferenceViewModel, CluebookViewModel
  Cluebooks/   Cluebook, HtmlCluebookWriter, SchematicSvg, SchematicPng
test/FormatCheck/  headless verification harness
docs/          reverse-engineering notes, strategy guide, schematic maps (docs/maps)
```

## Testing

```powershell
.\Run.ps1 -Test -NoRun                          # build and run the harness
dotnet run --project test\FormatCheck -- "C:\GAMES\S1889"          # plus the shipped-file checks
dotnet run --project test\FormatCheck -- "C:\GAMES\S1889" --live   # plus a read-only locate in a running DOSBox
dotnet run --project test\FormatCheck -- "C:\GAMES\S1889" --export-maps docs\maps   # regenerate the guide's maps
```

The harness runs **415 checks** with a game folder (422 with `--live`). They cover:

- format constants;
- the reference books;
- block validation;
- every record field, and the inventory add/remove/equip rules;
- save files and backups;
- the locator over a synthetic DOSBox guest (witnesses, decoys, seams, unreadable pages, cancellation);
- the file decoders;
- the view-models over a fake host;
- the cluebook's self-containment;
- a smoke test that builds the real window and walks every tab;
- against the shipped files: `DEF.S`, `ITEMS.DAT`, `89CAREER.DAT` and all 144 maps.
