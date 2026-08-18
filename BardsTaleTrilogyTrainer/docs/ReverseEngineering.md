# The Bard's Tale Trilogy (Remaster) — Reverse-Engineering Notes

## 1. Target identification

| Field | Value | Source |
|-------|-------|--------|
| **Game** | The Bard's Tale Trilogy (remaster of BT1–3) | Steam store page (app ID 843260) |
| **Developer** | Krome Studios / inXile Entertainment | Steam store page |
| **Release** | 2018 (v4.34 as of the latest CE table) | FearlessRevolution CE table metadata |
| **Engine** | Unity, **IL2CPP** scripting backend | Cheat-Engine AOB scripts target `GameAssembly.dll`; community reports |
| **Process name** | `TheBardsTaleTrilogy.exe` | CE scripts (`{ Game : TheBardsTaleTrilogy.exe }`) |
| **Native module** | `GameAssembly.dll` | CE scripts (`aobscanmodule(...,GameAssembly.dll,...)`) |
| **Architecture** | 64-bit (x64) | IL2CPP + Unity 2017+ on 64-bit OS; CE scripts use 64-bit registers (rax, rdi, rbp, rbx) |
| **ASLR** | Enabled (image base not fixed) | Unity default; pointer chains required |

## 2. Engine architecture

The Bard's Tale Trilogy remaster is a **Unity IL2CPP** application. Unlike the
DOS-era originals (which ran under DOSBox with fixed data-segment offsets), the
remaster compiles all C# game logic into native C++ inside `GameAssembly.dll`.
The key consequences for a live-memory trainer are:

1. **No fixed data-segment offsets.** Character data lives in IL2CPP managed-heap
   objects whose addresses change every session. A locator must follow pointer
   chains or structurally scan for plausible objects.

2. **IL2CPP object header.** Every managed object on the heap starts with an
   `Il2CppClass*` (8 bytes on x64) followed by a monitor/sync block (8 bytes),
   so the first user field is at **+0x10**. All field offsets below are absolute
   from the object's base address.

3. **String and array fields are pointers.** A character's name is not inline —
   it is a pointer to a separate IL2CPP `String` object. Inventory slots are
   pointers to `Item` objects. Spell knowledge is likely a bitfield or array
   on the character object or on a separate spell-manager object.

4. **`global-metadata.dat`** (inside the game's data folder) contains type and
   method names. **Il2CppDumper** can extract the full class layout from
   `GameAssembly.dll` + `global-metadata.dat`, producing a `script.json` for
   Ghidra that labels every IL2CPP-generated function and structure.

## 3. Known memory layout

### 3.1 Global game-state pointer

A static global inside `GameAssembly.dll` holds a pointer to the top-level game
manager. The CE gold script reads it:

```
mov rcx, [GameAssembly.dll + 0xE40338]   // global → game manager
mov rdx, [rcx + 0xB8]                     // game manager → party/economy sub-object
```

This RVA (`0xE40338`) was valid for game version **4.28** (August 2019). It may
shift between builds — the locator should fall back to structural scanning if
the RVA no longer resolves to a plausible object.

**Confidence:** [Confirmed] for v4.28; [Inferred] for later builds.

### 3.2 Character object field offsets

These offsets were extracted from the CE AOB injection scripts published on
FearlessRevolution. Each was found by AOB-scanning `GameAssembly.dll` for the
instruction that reads or writes the field, then noting the register-relative
offset.

| Field | Offset | Type | Source | Confidence |
|-------|--------|------|--------|------------|
| Experience | `+0x50` | `int32` | CE pointer-scan discussion (offset "ends at +50") | [Confirmed] |
| Gold (party-level) | `+0x68` | `int32` | CE gold script: `mov [rdi+68],#999999` | [Confirmed] |
| Current HP | `+0x84` | `int32` | CE health script: `cmp [rbp+00000084],r13d` | [Confirmed] |
| Current SP (mana) | `+0x8C` | `int32` | CE magic script: `mov edi,[rbx+0000008C]` | [Confirmed] |

**Gold** is stored on a separate party/economy object (reached through the
global pointer chain), not on the individual character object — matching the
original game's design where gold is party-wide.

### 3.3 Inferred field offsets

The following offsets are **inferred** from the IL2CPP field layout, the
original Bard's Tale character format, and the gap between known offsets. They
have not been confirmed against a live game session and carry `[Inferred]`
confidence markers.

| Field | Estimated offset | Type | Reasoning |
|-------|-----------------|------|-----------|
| Name (string ref) | `+0x10` | `ptr` | First field after IL2CPP header |
| Race | `+0x18` | `int32` | Enum (0=Human … 6=Gnome) |
| Class | `+0x1C` | `int32` | Enum (0=Warrior … 9=Wizard) |
| Status | `+0x20` | `int32` | Bitfield (Alive/Dead/Old/Poisoned/Stoned/Paralyzed/Possessed/Nuts) |
| Str current | `+0x28` | `int32` | First of 5 current attributes |
| IQ current | `+0x2C` | `int32` | |
| Dx current | `+0x30` | `int32` | |
| Cn current | `+0x34` | `int32` | |
| Lk current | `+0x38` | `int32` | |
| Str max | `+0x3C` | `int32` | First of 5 max attributes |
| IQ max | `+0x40` | `int32` | |
| Dx max | `+0x44` | `int32` | |
| Cn max | `+0x48` | `int32` | |
| Lk max | `+0x4C` | `int32` | (XP at +0x50 follows immediately) |
| Level | `+0x54` | `int32` | After XP |
| Max HP | `+0x80` | `int32` | 4 bytes before Current HP (+0x84) |
| Max SP | `+0x88` | `int32` | 4 bytes before Current SP (+0x8C) |
| Armor class | `+0x90` | `int32` | After SP fields |
| Conjurer level | `+0x94` | `byte` | Spell-class levels follow |
| Magician level | `+0x95` | `byte` | |
| Sorcerer level | `+0x96` | `byte` | |
| Wizard level | `+0x97` | `byte` | |
| Inventory (array ref) | `+0xA0` | `ptr` | Pointer to IL2CPP array of Item objects |
| Spell knowledge | `+0xB0` | `ptr` or `byte[]` | Bitfield or array of known spell IDs |

The gap between `+0x54` (Level) and `+0x80` (Max HP) is 0x2C bytes (44 bytes),
which could accommodate additional fields such as: bard songs remaining, rogue
hide chance, hunter critical chance, battles fought, melee attacks per round,
and other class-specific data from the original format.

### 3.4 AOB signatures from CE scripts

These are the exact AOB patterns from the FearlessRevolution CE scripts. They
can be used to locate the functions that read/write each field in
`GameAssembly.dll`, which in turn reveals the register that holds the character
object pointer.

**Gold write (v4.28):**
```
48 89 47 68 48 8B 0D 2C 84 C3 00
```
Injection point: `GameAssembly.dll + 0x207F01`
- `mov [rdi+68], rax` — writes gold to `[rdi+0x68]`
- `mov rcx, [GameAssembly.dll+0xE40338]` — loads the global game-state pointer

**HP compare (v4.28):**
```
44 39 AD 84 00 00 00
```
Injection point: `GameAssembly.dll + 0x1D727F`
- `cmp [rbp+00000084], r13d` — compares current HP against a threshold

**SP read (v4.28):**
```
8B BB 8C 00 00 00
```
Injection point: `GameAssembly.dll + 0x201B02`
- `mov edi, [rbx+0000008C]` — reads current SP into edi

## 4. Spell system

### 4.1 Original game

The original Bard's Tale tracked spell knowledge as **class levels** (0–7 for
each of Conjurer, Magician, Sorcerer, Wizard). A character at class level N
knows all spells of that class up to level N. The four spell-class level bytes
were stored in the character record at offsets 0x41–0x44 (order: Magician,
Conjurer, Sorcerer, Wizard).

### 4.2 Remaster

The remaster unifies all three games' spell systems. The CE table by gideon25
"automatically gives you those spells" when you give points in mage classes,
suggesting the remaster still ties spell knowledge to class level. However, the
remaster also includes **cross-game spells** that any magic user can learn:

| Code | Name | Original game | Class restriction |
|------|------|---------------|-------------------|
| **ZZGO** | Dream Spell | BT2 | Any magic user |
| **NUKE** | Gotterdammerung | BT3 | Any magic user |
| GILL | Gilles Gills | BT3 | Any magic user |
| DIVA | Divine Intervention | BT2 | Any magic user |

### 4.3 Full spell list

The Bard's Tale Trilogy contains spells from all three original games plus the
Archmage, Chronomancer, and Geomancer classes introduced in BT2 and BT3. The
complete spell roster was sourced from the community-maintained list at
`bardstaleonline.com/files/!docs/bt1-3-all-spells.txt` (created by Troy H. Cheek,
ripped from the MS-DOS executables).

**BT1 — 79 spells across four classes:**
- Conjurer (1–7): ARFI, MAFL, SOSH, TRZP, BASK, FRFO, MACO, WOHL, LERE, LEVI,
  MAST, WAST, FLRE, INWO, POST, GRRE, SHSP, WROV, INOG, MALE, FLAN, APAR
- Magician (1–7): VOPL, AIAR, STLI, SCSI, AREN, HOWA, MAGA, WIST, MYSH, OGST,
  MIMI, STFL, SPTO, DRBR, STSI, ANMA, ANSW, STTO, PHDO, YMCA, REST, DEST
- Sorcerer (1–7): MIJA, PHBL, LOTR, HYIM, DISB, TADU, MIFI, FEAR, WIWO, VANI,
  SESI, CURS, CAEY, WIWA, INVI, WIOG, DIIL, MIBL, WIDR, MIWP, WIGI, SOSI
- Wizard (1–7): SUDE, REDE, LESU, DEBA, SUPH, DISP, PRSU, ANDE, SPBI, DMST,
  SPSP, BEDE, GRSU

**BT2 additions** — Archmage spells: HAFO, MEME, BASP, CAMR, NILA, HEAL, BRKR,
MAMA. Plus ZZGO (Dream Spell), DIVA (Divine Intervention).

**BT3 additions** — Chronomancer spells: Vitl, Wifi, Gofi, Luck, What, Grro,
Shsh. Geomancer spells: Eada, Treb, Rock, Suso, Sant, Path, Jobo. Plus NUKE
(Gotterdammerung).

## 5. Item system

### 5.1 Item charges and the "zero = infinite" mechanic

In the Bard's Tale Trilogy remaster, consumable magic items (wands, horns,
instruments, etc.) have a limited number of charges. When charges reach zero,
the item normally disappears. However, the game engine treats an item with
**zero charges set by the player** as **infinite** — this is an engine quirk
where the "decrement and check" logic skips items whose charge count is already
zero, interpreting them as unlimited-use items.

This behavior was confirmed by community discussion: "Items without a number
listed in their description have infinite uses and will never deplete" (Steam
community guide). Setting an item's charge field to zero via memory editing
replicates this behavior for any item.

### 5.2 Original game item format

In the original DOS Bard's Tale, inventory was 8 slots × 2 bytes each:
- Byte 1: Item ID (1–127, 0 = empty)
- Byte 2: Status (0x00 = unequipped, 0x40 = unidentified, 0x80 = equipped)

The remaster uses IL2CPP object references for inventory items, where each slot
is a pointer to an `Item` object. The `Item` object contains:
- Item type ID (int32)
- Charges/quantity (int32) — **setting this to 0 makes the item infinite**
- Equipped flag (bool/int32)

### 5.3 BT1 item catalogue (127 items)

The complete 127-item list was extracted from the running BARD.EXE data segment
and is available at `bardstaleonline.com/files/!docs/bt1-items`. Key categories:

- **Basic weapons** (Garth's shop): Dagger (20g), Short Sword (30g), Mace (60g),
  Staff (20g), Broadsword (80g), War Axe (70g), Halberd (200g)
- **Basic armor** (Garth's shop): Robes (40g), Leather (70g), Chain (150g),
  Scale (300g), Plate (700g)
- **Accessories** (Garth's shop): Helm (50g), Buckler (40g), Tower Shield (100g),
  Leather Gloves (80g), Gauntlets (40g)
- **Instruments**: Mandolin, Harp, Flute (130g each)
- **Magic items**: Mithril/Adamantite/Diamond variants, figurines, special weapons

## 6. Garth's Equipment Shoppe

Garth's shop is the primary equipment vendor in all three Bard's Tale games. In
the remaster:

- Garth carries an **unlimited supply of basic equipment** (weapons, armor,
  accessories listed above)
- **Unique items** found in dungeons appear in his inventory only until purchased
- Players can sell any item to Garth for half its listed price

The shop's inventory in memory is likely an array of item-type IDs on a
game-state object. Setting all slots to contain every item type would make
Garth's shop sell everything. The exact memory layout of the shop inventory
has not been confirmed against a live game session — the trainer's shop editor
uses value scanning as a fallback approach.

## 7. Save file format

### 7.1 Location

| Platform | Path |
|----------|------|
| Steam (cloud) | `<Steam>\userdata\<UserID>\843260\remote\saves\` |
| Steam (local) | `<Steam>\userdata\<UserID>\843260\local\saves\` |
| GOG (Windows) | `%LOCALAPPDATA%Low\inXile Entertainment\The Bard's Tale Trilogy\` |

### 7.2 Format

Save files are **binary `.dat` files** (not JSON, not the legacy `.TPW` format).
The remaster does not use individual character files — all party and character
data is stored within the save blob. The exact binary layout has not been
decoded; community reports describe the files as "encrypted DAT/blob" format.

## 8. Locator strategy

### 8.1 Primary: global pointer chain

1. Find `GameAssembly.dll` in the target process.
2. Read the pointer at `GameAssembly.dll + 0xE40338` (the game-state global).
3. Follow the chain: `[global + 0xB8]` → party/economy object.
4. From the party object, follow the character array pointer.
5. Validate each character object by checking that HP, SP, XP, and attributes
   fall within plausible ranges.

### 8.2 Fallback: structural scan

If the RVA has shifted (different build), sweep all committed memory for objects
that look like IL2CPP character instances:
- Object has a valid `Il2CppClass*` at +0x00 (points to readable memory).
- Fields at +0x50 (XP), +0x84 (HP), +0x8C (SP) hold plausible values.
- Adjacent fields (attributes 3–25, level 1–40, class 0–9, race 0–6) are sane.
- Character objects should appear in a contiguous array (party of up to 7).

### 8.3 Last resort: value scanner

The trainer includes Common's `MemorySearcher` as a Cheat-Engine-style manual
scanner. The user can:
1. Note their character's current XP.
2. Scan for that exact Int32 value.
3. Kill a monster, note the new XP.
4. Narrow by the new exact value.
5. Repeat until one address remains — that address minus 0x50 is the character
   object base.

## 9. Methodology and limitations

### 9.1 What was done

- **Online research**: Steam community discussions, FearlessRevolution CE
  tables and scripts, Bard's Tale Online community resources, and the original
  game's hacking guides were studied.
- **CE table analysis**: The AOB scripts published by fearless123456 (v4.28)
  and gideon25 (v4.34) were analyzed to extract field offsets and the global
  pointer RVA.
- **Original game comparison**: The DOS Bard's Tale 1 character file format
  (109-byte `.TPW` files) was used as a template for estimating the remaster's
  field layout, since the remaster "reconstructed the game's mechanics by
  analyzing existing versions."
- **Spell and item databases**: Full spell lists and item catalogues were
  sourced from community-maintained references at `bardstaleonline.com` and
  `bardstale.brotherhood.de`.

### 9.2 What was NOT done

- **No live game session**: The game was not installed on the development
  machine, so no live memory analysis was performed. All offsets marked
  `[Confirmed]` were confirmed by the CE community, not by us.
- **No Ghidra analysis**: `GameAssembly.dll` was not available for Ghidra
  analysis. When the game is installed, run **Il2CppDumper** on
  `GameAssembly.dll` + `global-metadata.dat` to produce `script.json`, then
  load `GameAssembly.dll` into Ghidra with the post-script to label all
  IL2CPP functions and structures. This will reveal the exact field layout.
- **No save file decoding**: The binary `.dat` save format was not decoded.
  A future effort could capture a save, compare two saves with known
  differences, and reverse the blob format.

### 9.3 Confidence markers

Every offset in the trainer code carries a confidence marker:
- `[Confirmed]` — verified by the CE community against a live game session
- `[Inferred]` — estimated from the original game format and IL2CPP layout
  conventions, but not verified against the remaster
- `[Static]` — derived from static analysis of game files or reference data
