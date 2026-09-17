# Space 1889 Trainer — working notes

A WPF (.NET 8, `net8.0-windows`) live-memory trainer and save editor for the 1990 DOS RPG *Space: 1889* (Paragon
Software), running under DOSBox / DOSBox-X. It is Windows-only, and the manifest requests administrator rights for
`Read/WriteProcessMemory`.

## The one thing to understand first

Everything lives in **one 20,198-byte block** that `1889.COM` allocates and every program shares. `CG.EXE`,
`M.EXE` and `S.EXE` each keep a far pointer to it. **A `.SAV` file is a verbatim copy of it**: a live save was
byte-identical to RAM. So:

- `StateFormat` is the single offset table. The live editor, the Save Editor and the harness all use it.
- `GameState` is a cache over an `IStateTarget`, which is one of `LiveStateTarget` (DOSBox), `BufferTarget` (a save
  file or the harness), or a recording/refusing target in the harness.
- `CharacterViewModel` and `WorldViewModel` run against an `IStateHost`. That host is `MainViewModel` (live) or
  `SaveEditorViewModel` (file). Do not fork the editors per host.

## Projects

- `src/Space1889Trainer/` — the app (`AssemblyName` and `RootNamespace` `Space1889Trainer`). It references
  `GameTrainers.Common` for `Memory` (ProcessMemory, MemoryRegion), `Mvvm` (ObservableObject with `SetField`,
  RelayCommand) and `Documents` (HtmlPage, SvgCanvas).
  - `Game/`: pure data, no WPF or process access. `StateFormat` has every offset with a
    [Confirmed]/[Static]/[Inferred] marker. `ItemBook` is baked from `ITEMS.DAT`; the harness compares every row
    with the shipped file.
  - `Memory/`: `GameLocator`, `LiveStateTarget`, `IMemorySource`.
  - `Files/`: readers for the player's own game folder (PIC, SYS maps, SET, NPC), `TileRules`, `MapLibrary` (a
    renderer that needs no WPF), and `MapAnnotations`. Nothing from the game is embedded in the app except the
    item table.
  - `ViewModels/`, `Cluebooks/`, `MainWindow.xaml`: the `CharacterEditor` and `WorldEditor` DataTemplates are
    shared by the Party/World tabs and the Save Editor.
- `test/FormatCheck/` — the headless harness (console `Exe`, STA).

RE notes, the strategy guide and the schematic maps it embeds are in `docs/`. `.docs/` (the teardown workspace:
unpacker, Ghidra exports, live tools, agent analyses) and `.game/` are git-ignored. Never commit them.

## Do not weaken the locator

- The anchor is `"PARTY ACCT.\0"` at block offset 0x5ED. `M.EXE` carries a second copy at `DS:0x968`. That copy is
  rejected **only** because `LooksLikeState` fails on the strings in front of it, so do not relax validation to
  "anchor found".
- Validation deliberately **does not range-check attributes or skills**. A maxed party must still validate, or one
  "Max everything" click would make the next attach fail. Validate structure: names, sex bytes, the account name,
  the planet range.
- The pointer witness (`GameFacts.Modules`) ranks candidates. Unwitnessed blocks are still accepted, because
  between programs nothing points at the block. Keep that, and keep saying so in the status line.
- `LiveStateTarget.Write` re-validates the live block before every write. It costs a 20 KB read per write, and it
  is what stops an edit after a quit to DOS from landing in someone else's memory.

## Conventions that bite

- **Write only the bytes a field owns.** The game rewrites the clock (`0x3B2E`) and position every tick. A
  whole-block flush would race it and undo steps.
- **Item numbering:** an inventory entry stores **index + 1**, but loaded ammunition (entry byte +1), flyer guns
  (`0x42B8`/`0x42B9`) and the locked-door table store the **0-based index**. `ItemBook.ById` and `ItemBook.ByIndex`
  exist for that reason.
- **Position is row × 2, column × 2** (`0x4298`, `0x429A`). A teleport also sets the previous position
  (`0x429E`/`0x42A0`), because M.EXE saves it as the map's return point when you leave.
- **"HEALTH: a/b"**: b is `max − ⌈(STR+END)/2⌉`, not END. The default party makes END look right by coincidence.
- **Social class is SOC − 1**, and the game draws 5 and 6 almost identically. Read the bytes, not the screen.
- **The pub and inn are the shared maps 992/999**, but their NPC lists are keyed by the city (`x92`/`x99`).
  `WorldRecord.MapId` and `MapLibrary.Npcs` handle this; keep it in one place.
- **Removing an item** shifts later entries *and* their in-use flags down, clears the weapon/armour slot if it
  pointed at the item, and renumbers it if it pointed past. `CharacterRecord.RemoveItem` does all three.
- **Keys in the live game:** outside the GAME menu `L` is LEAD, not LOAD.
- `UseWPF` projects do not get `System.IO` from implicit usings; add it.

## What is deliberately absent

- **Moving between maps or planets.** The engine loads a map on entry. Setting planet/area/map would leave it
  walking on the old map's cells.
- **Reviving the dead.** Death zeroes the record.
- **Editing careers.** The ids only affect CURE bonuses, and the names are free text copied from `89CAREER.DAT`.
- Any claim that the flyer fields work in flight. They are [Static] and labelled so in the UI.

## Testing

`dotnet run --project test\FormatCheck -- "<game dir>" [--live] [--export-maps <dir>]`. The harness runs 415
checks with a game folder, and 422 when `--live` finds a running DOSBox. `--live` is read-only. Keep it green.

After any change to `MainWindow.xaml`, run the harness: its `CheckWindow` builds the real window and walks every
tab. A Debug build of the app also honours `S1889_SMOKETEST=<marker file>`.

To run the Debug GUI from a non-elevated shell, set `__COMPAT_LAYER=RunAsInvoker`. Reading DOSBox memory works
unelevated when the emulator runs as the same user.

For live experiments with no interactive desktop:

- `PostMessage` `WM_KEYDOWN`/`WM_KEYUP` to DOSBox's SDL window, holding each key about 200 ms. BIOS keyboard-buffer
  injection does not reach this game.
- Read the SDL surface for screenshots.

The scripts are in `.docs/live.py`.
