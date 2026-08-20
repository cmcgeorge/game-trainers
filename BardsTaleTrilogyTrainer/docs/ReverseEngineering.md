# The Bard's Tale Trilogy (Remaster) — Reverse-Engineering Notes

## 1. Target identification

| Field | Value | Source |
|-------|-------|--------|
| **Game** | The Bard's Tale Trilogy (remaster of BT1–3) | Steam store page (app ID 843260) |
| **Developer** | Krome Studios / inXile Entertainment | Steam store page |
| **Build examined** | Unity `2018.4.0.11993000` | `TheBardsTaleTrilogy.exe` version resource |
| **Engine** | Unity 2018.4, **IL2CPP** scripting backend | `il2cpp_data/` beside the data folder |
| **Metadata version** | 24.1 (`global-metadata.dat`, sanity `0xFAB11BAF`) | parsed directly |
| **Process name** | `TheBardsTaleTrilogy.exe` | the installed game |
| **Native module** | `GameAssembly.dll` (15.7 MB, image base `0x180000000`) | PE header |
| **Architecture** | 64-bit (x64) | PE32+, machine `0x8664` |
| **ASLR** | Enabled (image base not fixed) | Unity default; addresses are derived from the module base at runtime |
| **Types / fields / methods** | 5030 / 23420 / 31945 | metadata tables |

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

## 3. Memory layout (read from the game's own metadata)

The remaster ships its full IL2CPP metadata: `TheBardsTaleTrilogy_Data/il2cpp_data/Metadata/global-metadata.dat`
(sanity `0xFAB11BAF`, version 24.1) holds every type and field **name**, and
`GameAssembly.dll` holds the matching field-**offset** table. Together they give the exact
layout of every class, with no guessing at all. Everything in this section was read out that
way and then cross-checked against the compiled code.

### 3.1 How the tables were located

`Il2CppMetadataRegistration` was found by scanning the DLL's `.rdata` for the struct whose
`fieldOffsetsCount` and `typeDefinitionsSizesCount` both equal the metadata's
`typeDefinitionsCount` (5030). It sits at RVA `0xC3BC10` in the installed build and points at:

| Table | Pointer | Count |
|-------|---------|-------|
| `types` | `0x180A76D90` | 26719 |
| `fieldOffsets` | `0x180DFB6E0` | 5030 |
| `typeDefinitionsSizes` | `0x180DF19B0` | 5030 |
| `metadataUsages` | `0x180C70DE0` | 18293 |

`fieldOffsets[typeIndex]` is an `int32[]` of that type's field offsets, in the same order as
the metadata's field table — so field name and field offset line up directly.

**The check that this is aligned correctly:** the three offsets the Cheat Engine community had
already published for this game — experience at `+0x50`, current hit points at `+0x84`, current
spell points at `+0x8C` — come out of the table unchanged, as does party gold at `+0x68`. Those
four were derived by completely different means (AOB scans of the running game), so their
agreement pins the extraction.

### 3.2 `BardsTale.Character` (instance size `0x108`)

| Offset | Field | Type |
|--------|-------|------|
| `+0x10` | `m_recentSpells` | `List<Spell>` |
| `+0x18` | `m_stats` | `GameStats` |
| `+0x28` | `m_name` | `string` |
| `+0x30` | `m_gender` | `Gender` |
| `+0x34` | `m_race` | `Race` |
| `+0x38` | `m_class` | `Class` |
| `+0x50` | `m_experience` | **`long`** |
| `+0x58`…`+0x68` | `m_strength`, `m_intelligence`, `m_dexterity`, `m_constitution`, `m_luck` | `int` ×5 |
| `+0x70` | `m_gold` | **`long`** |
| `+0x7C` | `m_level` | `int` |
| `+0x80` / `+0x84` | `m_maxHitpoints` / `m_hitpoints` | `int` |
| `+0x88` / `+0x8C` | `m_maxSpellpoints` / `m_spellpoints` | `int` |
| `+0x90` | `m_nmbrOfAttacks` | `int` |
| `+0x94` | `m_pictureNumber` | `int` |
| `+0xA0` | `m_condition` | `int` |
| `+0xA8` / `+0xAC` | `m_realLevel` / `m_levelDrain` | `int` |
| `+0xB0` | `m_nmbrOfBattles` | `int` |
| `+0xB4`…`+0xC0` | `m_disarmTrapBonus`, `m_identifyBonus`, `m_hideInShadowsBonus`, `m_criticalHit` | `int` ×4 |
| `+0xC4` / `+0xC8` | `m_songsRemaining` / `m_songsKnown` | `int` |
| `+0xD0` | `m_spellLevel` | `int[16]`, indexed by class id |
| `+0xD8` | `m_learntSpells` | `List<Spell>` |
| `+0xE0` | `m_inventory` | `Inventory` |
| `+0xF0` | `m_statusEffects` | `StatusEffects` |
| `+0x100` | `m_initialClass` | `Class` |

Three things the earlier reconstruction (from the DOS 109-byte record) got wrong and that
matter for anyone writing to these fields:

- **Experience and gold are 64-bit.** Writing a dword leaves the high half untouched.
- **There is one set of attributes, not a current/maximum pair.** The remaster has no
  max-attribute block, so there is nothing at `+0x3C`…`+0x4C` to edit.
- **There is no armour-class field.** `+0x90` is the melee attack count. Armour class is
  computed from equipment when the sheet is drawn.

`m_spellLevel` is allocated in the constructor as `il2cpp_array_new(…, 0x10)` — sixteen ints,
indexed by the class enum itself, so the casting schools occupy indices 6–12. The four bytes at
`+0x94`…`+0x97` that the earlier layout treated as spell-class levels are `m_pictureNumber`.

### 3.3 Party, inventory and items

| Class | Offset | Field | Type |
|-------|--------|-------|------|
| `Party` | static `+0x00` | `Instance` | `Party` |
| `Party` | `+0x40` | `m_members` | `PartyMember[]` |
| `Party` | `+0x60` | `m_inventory` | `Inventory` |
| `Party` | `+0x68` | `m_gold` | **`long`** |
| `PartyMember` | `+0x10` | `m_character` | `Character` |
| `Inventory` | `+0x10` | `m_items` | `Item[]` |
| `Item` | `+0x10` | `m_itemDesc` | `ItemDescription` |
| `Item` | `+0x20` | `m_equipped` | `bool` |
| `Item` | `+0x24` | `m_charges` | `int` |

`m_members` holds the seven slot wrappers, not the characters — the character is one hop
further, at `PartyMember + 0x10`. An empty slot is a wrapper with a null character.

`Party.MaxSlots` is 7, `Party.InventorySize` is 40 and `Character.InventorySize` is 16, all read
from the metadata's constant table.

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

The remaster keeps the school-level idea but adds a second, independent route.
`Character::KnowsSpell` (RVA `0x1F2590`) is, in full:

```csharp
bool KnowsSpell(Spell s)
{
    if (m_learntSpells != null && m_learntSpells.Contains(s)) return true;

    SpellDescription d = GlobalSpells.Instance.GetSpell(s);
    if (d == null || d.m_level == 0) return false;      // no school ever grants it
    return m_spellLevel != null && m_spellLevel[(int)d.m_class] >= d.m_level;
}
```

So `m_spellLevel` — an `int[16]` indexed by **class id**, schools at 6–12 — covers
the graded spells, and `m_learntSpells` covers everything else. A spell whose
`m_level` is 0 can *only* ever be known through the list.

The cross-game spells are exactly that case:

| Code | Name | Enum member | Id | Game |
|------|------|-------------|----|------|
| **ZZGO** | Dream Spell | `DreamSpell` | **78** | BT2 |
| **NUKE** | Gotterdammerung | `Gotterdamurung` | **154** | BT3 |
| GILL | Gilles Gills | `GillesGills` | 152 | BT3 |
| DIVA | Divine Intervention | `DivineIntervention` | 153 | BT2 |

`Character::LearnSpell` (RVA `0x1F2950`) is the only thing that writes the list —
`m_learntSpells ??= new List<Spell>(); if (!Contains(s)) Add(s);` — and it has just
four callers in the whole binary: `PlayerState_ReviewBoard::LearnQuestSpells`
(chapter quest spells, Chronomancers only), `::OnBuySpellYes` and
`::OnChooseBuySpellPayer` (a Review Board purchase), and
`PlayerState_Script::ProcessScript` (a map event). There is no spell bitfield
anywhere on the character.

The 0–7 cap is not folklore either — `PlayerState_ReviewBoard::UpgradeMage`
computes `Mathf.Min(7, (charLevel + 1) / 2)`.

### 4.3 The spell table is asset data, not code

`BardsTale.Spell` is an `int` enum with 249 members, extracted from
`global-metadata.dat` and reproduced verbatim in `Game/SpellId.cs`. But a spell's
**four-letter code, school and level are not in the binary at all** — they are
fields of serialized `SpellDescription` ScriptableObjects, reachable only at run
time through `GlobalSpells.Instance.m_spellsByEnum` (`GetSpell` is a bounds-checked
index into it, RVA `0x23D240`).

That is why the trainer reads the table live rather than shipping one. An earlier
version carried a community-sourced list from
`bardstaleonline.com/files/!docs/bt1-3-all-spells.txt`; its schools and levels did
not match the remaster, and it has been removed rather than corrected. Nothing is
lost: the ids are what granting a spell actually needs, and those are exact.

## 5. Item system

### 5.1 Item charges and the "zero = infinite" mechanic

`Character::UseItemCharge` decides this, and it is short enough to read in full:

```
if (item == null)          return
if (item.m_charges == 0)   return          // <-- nothing is consumed
if (BT1-specific && m_charges > 1)         // 1-in-64: the item's charges are lost
    ...
m_charges--                                 // the normal path
if (m_charges == 0) ... destroy / unequip
```

The zero test happens **before** the decrement and returns immediately, so an item whose charge
count is zero is never consumed. That is exactly what the Steam community guide describes from
the player's side — "items without a number listed in their description have infinite uses and
will never deplete" — and it makes zeroing `Item.m_charges` (`+0x24`) a safe way to get
unlimited uses out of any item.

Note that `ItemDescription.InfiniteCharges` and `Item.MaxCharges` are both the constant 255.
Those describe the *item catalogue's* upper bound, not the runtime sentinel; the runtime
sentinel is zero, as above.

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

## 6. Character classes and class-specific abilities

### 6.1 The class roster

The class field at `+0x38` (§3.2) holds one of thirteen values. Ids 0–9 are the
BT1 roster; the Archmage arrives in BT2, and the Chronomancer and Geomancer in BT3.
The trainer's earlier validation capped the field at 9, which would have made the
locator reject a legitimate BT2/BT3 party — `CharacterFormat.LooksLikeCharacter`
now accepts every id the class table names, 0–15, of which 0–12 are playable.

| Id | Class | Role | Art | Available at creation | Introduced |
|----|-------|------|-----|----------------------|------------|
| 0 | Warrior | fighter | — | yes | BT1 |
| 1 | Paladin | fighter | — | yes | BT1 |
| 2 | Rogue | stealth | — | yes | BT1 |
| 3 | Bard | hybrid | — | yes | BT1 |
| 4 | Hunter | fighter | — | yes | BT1 |
| 5 | Monk | fighter | — | yes | BT1 |
| 6 | Conjurer | caster | Conjurer | yes | BT1 |
| 7 | Magician | caster | Magician | yes | BT1 |
| 8 | Sorcerer | caster | Sorcerer | no | BT1 |
| 9 | Wizard | caster | Wizard | no | BT1 |
| 10 | Archmage | caster | Archmage | no | BT2 |
| 11 | Chronomancer | caster | Chronomancer | no | BT3 |
| 12 | Geomancer | caster | Geomancer | no | BT3 |

**Confidence:** [Inferred]. The order matches every Bard's Tale reference and the
review-board menu, but the remaster's enum has not been dumped with Il2CppDumper
to confirm the numbering.

### 6.2 Class-change rules

The Review Board's rules come from the game manual and from community reports on
the remaster. `ClassBook.CanChangeTo` encodes them; the trainer refuses a change
that breaks them unless the user ticks **Ignore requirements**.

| Rule | Source |
|------|--------|
| Only Conjurer and Magician are open at character creation | manual |
| Sorcerer needs spell level 3 or higher in **one** other magical art | manual |
| Wizard needs spell level 3 or higher in **two** other magical arts | manual |
| A magic user who leaves an art may **never return** to it | manual |
| Archmage needs spell level 3 or higher in **all four** basic arts; the promotion is only offered in BT2 | Steam community discussion (843260) |
| Chronomancer needs three arts mastered to spell level 7, and gives up their spells | BT3 community reference |
| Geomancer is open only to fighting classes, once BT3's story unlocks it | BT3 community reference |
| Spell levels arrive at character levels 1, 3, 5, 7, 9, 11 and 13 | Steam community discussion (843260) |

That last rule is what `ClassBook.SpellLevelForLevel` computes, and it is what the
trainer grants when a character changes into one of the four basic arts: a
level-13 character becoming a Sorcerer gets Sorcerer spell level 7 written to
`m_spellLevel[classId]` — that is `[char+0xD0] + 0x20 + classId*4`, the route
`CharacterRecord.SetSpellLevel` takes — while the art it came from is left in place
(the remaster keeps the old class's spells after a change). Not `+0x96`: that lies
inside `m_pictureNumber` (§3.2), and writing a spell level there was the bug this
section used to describe. See `SpellSystem.md` §6.4.

### 6.3 Special-ability scores

Every class-specific ability the games track — the Hunter's critical hit, the
Rogue's disarm and hide-in-shadows — is a plain `int32` field on the character,
**rolled against on a 0–255 scale where 255 is a certainty**, and the remaster
prints the raw score on the character sheet. (The field is 32 bits wide; the
0–255 range is the games' own, inherited from the 8-bit originals.) Community
measurement of the DOS games and of the remaster gives:

| Class | Ability | Behaviour | Source |
|-------|---------|-----------|--------|
| Hunter | Critical hit | Starts at 0 and rises by **1–32 at each level-up**; reliable from about level 16. The remaster subtracts a flat per-map penalty, so a sheet value of 100% still misses deep in a dungeon. | The Adventurers' Guild forum; Steam community |
| Hunter | Critical hit (alternative) | Construction Set manual: **1–3% per level, plus 1% per point of Dexterity over 14 per level, to a maximum of 99%** | Bard's Tale Construction Set manual |
| Rogue | Disarm traps | **+3–11 per level-up**; about **175** gives a ~95% chance in every location | Steam community |
| Rogue | Hide in shadows | **+3–11 per level-up**, same 0–255 scale | Steam community |
| Warrior, Paladin | Melee attacks | One extra attack for **every 4 levels after the 1st** | manual |
| Paladin | Resistance to evil magic | "greatly increased"; never surfaced as a number | manual |
| Bard | Songs | Six songs; may play **as many tunes as experience levels** before needing a drink | manual |
| Monk | Armour class | Improves by **1 per level after the 1st** | manual / wiki |
| Monk | Unarmed damage | Table by level: 1–2: 4, 3–4: 8, 5–6: 16, 7–8: 16, 9–12: 32, 13–16: 40, 17–24: 48, 25–30: 56, 31–39: 80, 40–48: 96, 49–55: 128, 56–61: 160, 62–63: 192, 64: 234 | Bard's Tale Online character reference |

`ClassBook` reproduces all of this. The Class abilities panel shows each score as
the game currently holds it, next to what the manual says it does; where a number
is derived rather than stored — the Monk's unarmed damage and armour-class bonus,
the Warrior's expected attacks, the Hunter's Construction-Set figure — it is
labelled as such in the note beneath it.

### 6.4 Where the scores live

They are ordinary named fields on `Character`, not a gap to be searched:

| Score | Field | Offset |
|-------|-------|--------|
| Melee attacks per round | `m_nmbrOfAttacks` | `+0x90` |
| Battles fought | `m_nmbrOfBattles` | `+0xB0` |
| Rogue: disarm traps | `m_disarmTrapBonus` | `+0xB4` |
| Rogue: identify items | `m_identifyBonus` | `+0xB8` |
| Rogue: hide in shadows | `m_hideInShadowsBonus` | `+0xBC` |
| Hunter: critical hit | `m_criticalHit` | `+0xC0` |
| Bard: tunes left | `m_songsRemaining` | `+0xC4` |
| Bard: songs known | `m_songsKnown` | `+0xC8` |

The trainer reads all eight into `ClassScores`, shows them on the Class abilities panel
beside what the manual says each one does, and makes seven of them editable on the Class
scores panel. An earlier iteration surfaced `+0x58`…`+0x7F` as a byte grid so the user could
pin these down by eye; that is no longer needed and the grid has been removed.

**Max class scores** (`ClassBook.MaxAbilityScores`) raises only the four the game rolls
against — disarm, hide in shadows, identify and critical hit — to 255, and refills
`m_songsRemaining` to the character's level, which is the manual's rule for a Bard. It
deliberately leaves `m_nmbrOfAttacks` and `m_songsKnown` alone: the first is a count the
combat loop iterates over and the second is how many of the six songs are known, so neither
means anything at 255. Note that 255 is a certainty *before* the remaster subtracts its
per-map penalty, which is why a maxed Hunter still misses deep in a dungeon.

## 7. Garth's Equipment Shoppe

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

## 8. Save file format

### 8.1 Location

| Platform | Path |
|----------|------|
| Steam (cloud) | `<Steam>\userdata\<UserID>\843260\remote\saves\` |
| Steam (local) | `<Steam>\userdata\<UserID>\843260\local\saves\` |
| GOG (Windows) | `%LOCALAPPDATA%Low\inXile Entertainment\The Bard's Tale Trilogy\` |

### 8.2 Format

Save files are **binary `.dat` files** (not JSON, not the legacy `.TPW` format).
The remaster does not use individual character files — all party and character
data is stored within the save blob. The exact binary layout has not been
decoded; community reports describe the files as "encrypted DAT/blob" format.

## 9. Locator strategy

### 9.1 Primary: the class slots the game itself uses

IL2CPP caches one `Il2CppClass*` per referenced type in a writable slot in `GameAssembly.dll`'s
data section, and the generated code reaches every static field through it. The pattern is
visible all over the compiled code:

```
mov rax, [rip + <slot>]     ; Il2CppClass* for the type
mov rcx, [rax + 0xB8]       ; Il2CppClass.static_fields
mov rcx, [rcx]              ; the first static -- Instance
```

`Il2CppClass.static_fields` is at `+0xB8` in this build, `Il2CppClass.name` at `+0x10` and
`namespaze` at `+0x18`. The slot addresses come from `Il2CppMetadataRegistration.metadataUsages`
paired with the metadata's usage lists:

| Class | Slot RVA |
|-------|----------|
| `BardsTale.Party` | `0xE44900` |
| `BardsTale.Player` | `0xE44BF8` |
| `BardsTale.Automap` | `0xE44D38` |
| `BardsTale.GlobalMaps` | `0xE44D50` |
| `BardsTale.GameSaver` | `0xE45A90` |
| `BardsTale.Roster` | `0xE45C08` |
| `BardsTale.TeleportTarget` | `0xE46478` |

These are build-specific — the community's CE table used `0xE40338`, which was the `Party` slot
in build 4.28 — so **every slot is validated before it is used**: the trainer reads the class's
own `name` and `namespaze` strings and only trusts the pointer when they read `Player` /
`BardsTale`. A stale RVA fails that test rather than silently yielding a wrong object.

From the resolved classes:

- `Party.Instance` → `m_members` (`PartyMember[]`) → `m_character` per slot.
- `Player.Instance` → `m_map`, `m_gridX`, `m_gridZ`, `m_facing`, `m_queueTeleport`.
- `GlobalMaps.Instance` → `m_cityMaps` / `m_dungeonMaps`, plus the static `m_gameChapter`.

### 9.2 Fallback: sweep the module for the class

When a slot does not validate, the trainer sweeps the loaded module for any pointer-sized value
that resolves to a class whose name and namespace match. That is slower, but it survives a game
update, which is the whole reason not to hard-code addresses.

### 9.3 Fallback: structural scan for characters

If the classes cannot be resolved at all, committed memory is swept for objects shaped like a
`Character`: plausible 64-bit experience and gold, race and class inside their enums, hit points
no greater than the maximum, spell points no greater than the maximum, a known condition, and
attributes in range. This finds the party for editing even when the map features are
unavailable.

## 10. Maps, party position and teleporting

### 10.1 Where the party is

`BardsTale.Player` is a singleton reached through its class slot (§9.1). The position lives
directly on it:

| Offset | Field | Meaning |
|--------|-------|---------|
| `+0x18` | `m_map` | the loaded `GameMap` |
| `+0x68` | `m_queueTeleport` | a pending `TeleportTarget`, polled every tick |
| `+0xE8` | `m_facing` | `Facing` — 0 North, 1 East, 2 South, 3 West |
| `+0xEC` | `m_gridX` | column |
| `+0xF0` | `m_gridZ` | row |
| `+0x100` / `+0x104` | `m_roomX` / `m_roomZ` | position inside a city building |
| `+0x108` / `+0x10C` | `m_prevX` / `m_prevZ` | the square stepped out of |

`GameMap` describes the map the party is standing in:

| Offset | Field |
|--------|-------|
| `+0xB8` | `m_name` |
| `+0xC1`…`+0xC5` | `m_wrapAroundEnabled`, `m_phaseDoorDisabled`, `m_isTower`, `m_isOutside`, `m_isWilderness` |
| `+0xC8` / `+0xCC` | `m_width` / `m_height` |
| `+0x118` | `m_level` — the floor within a multi-level area |
| `+0x198` | `m_isDungeonMap` |
| `+0x19C` | `m_mapIdx` — index into the city or dungeon array |
| `+0x1A0` | `m_desc` — the `MapDescription` it was built from |

Coordinates run **X east and Z north from a south-west origin**. That falls out of the map
files: in every map, cell `(0,0)` has solid south and west sides and the top row has a solid
north side.

### 10.2 Teleporting the way the game does

`Player::OnStateTick` polls the teleport queue on every tick:

```
mov rax, [rbx + 0x68]      ; m_queueTeleport
test rax, rax
je   skip                  ; nothing queued
cmp  byte [rax + 0x10], 0  ; m_isValid
je   skip
cmp  byte [rax + 0x34], 0  ; m_teleportDone
...                        ; fade out, LoadMap(m_map, m_isDungeon), TeleportTo(m_x, m_z, m_facing)
```

So filling that field is a real teleport, not a position poke: the game fades, loads a different
map if it needs to, runs the map's startup scripts and updates the automap. `TeleportTarget`:

| Offset | Field | Notes |
|--------|-------|-------|
| `+0x10` | `m_isValid` | the game ignores the queue unless this is set |
| `+0x11` | `m_isDungeon` | picks the city or dungeon array |
| `+0x12` | `m_doJournal` | record the jump in the journal |
| `+0x14` | `m_map` | destination map index |
| `+0x18` / `+0x1C` | `m_x` / `m_z` | destination square |
| `+0x20` | `m_facing` | |
| `+0x24` / `+0x28` | `m_mapWidth` / `m_mapHeight` | |
| `+0x2C` | `m_teleportType` | 0 quiet, 1 dimensional, 2 fade |
| `+0x30` | `m_preDelay` | `float` seconds |
| `+0x34` | `m_teleportDone` | set by the game once consumed |
| `+0x38` | `m_postJournal` | `string`, may be null |

Field-for-field this is what `Player::QueueTeleportTo` writes, which is how the game's own
scripts, the tavern's wine-cellar stairs and the dream spell all move the party.

The object itself is the only wrinkle: `QueueTeleportTo` allocates a fresh one each time, so the
field is often null. Rather than depend on that, the trainer commits a 64-byte block in the game
with `VirtualAllocEx`, stamps the real `Il2CppClass*` for `TeleportTarget` (read from its class
slot) into its header, and reuses that same block for every teleport. Boehm — the collector
IL2CPP uses — ignores pointers into memory it did not allocate, and it never moves objects, so a
reference to a block outside its heap is inert rather than dangerous. If allocation is refused,
the trainer falls back to filling whichever `TeleportTarget` it can already see.

### 10.3 The map catalogue

`GlobalMaps` is a per-chapter object serialised into the scene files — `level3` is BT1, `level4`
BT2, `level5` BT3 — holding `m_cityMaps` and `m_dungeonMaps` as inline `MapDescription[]`.
Parsing those three objects gives the complete catalogue:

| Chapter | Cities / wilderness | Dungeons | Total |
|---------|--------------------|----------|-------|
| BT1 — Tales of the Unknown | 1 | 16 | 17 |
| BT2 — The Destiny Knight | 7 | 26 | 33 |
| BT3 — Thief of Fate | 10 | 61 | 71 |
| | | | **121** |

The parse is self-checking: Unity serialises a `MonoBehaviour`'s fields in declaration order,
and the declaration order is known from the metadata, so a correct reading consumes the object's
byte range *exactly*. All three land on the final byte, which is what makes the catalogue
trustworthy.

Each `MapDescription` carries the map's name, grid size, floor number, entry point, tower flag
and the stair links (`nextMap`, `prevMap`, `parentMap`/`parentX`/`parentZ`). Note that the entry
point belongs to the *area*, not the floor: BT3's Ice Dungeon Lv2 is 5×5 but inherits the entry
point `(2, 8)` from its 9×9 first floor, so it must be clamped before use.

`m_dreamSpellTargets` (BT2 only) is the ZZGO destination table. Its `m_map` is a **city** map
index, not a dungeon one — `OnGotGetDreamSpellDestination` feeds it to `LoadMap` with the dungeon
flag clear, because the spell sets the party down at the dungeon's entrance out in the world.
Six of the seven entries coincide with the corresponding dungeon's own
`parentMap`/`parentX`/`parentZ`; **Fanskar's Castle does not**, and that is the game's own data
rather than a transcription slip. The table sends the party to The Forest (17, 27) while the
Castle's parent link is (17, 26), one square south. The map file settles it: the location script
named `FanskarCastle` sits on (17, 27). What actually holds for all seven is that each lands
exactly on the square whose script names that dungeon — `FormatCheck` pins all seven script
names against the installed game. Do not "correct" this entry to match the parent link.

### 10.4 The map files

`MapDescription.m_map` points at a Unity `TextAsset` in `resources.assets`, named
`map_bt<n>_{city|dung}NN_<name>_asc`. They are **plain text**:

```
name=SCRIPTSTRING_1572
isDungeon=1
width=5
height=5
wrapAroundEnable=1
isTower=1
monsterSet=7
level=0
map
  0,0:Door,Solid,Solid,Solid, RandomCombat
  1,0:Door,None,Solid,Solid, SpecialAhead, RandomCombat
  ...
  0,2:Solid,Solid,LockedDoor,Solid, StairsOut
locationScript=3,3,L186
scripts
L186
    @StairsIn
```

A dungeon cell is `x,z:North,East,South,West` followed by any number of behaviour flags; a city
cell is `x,z:extra,motion,picture,Module` followed by flags. Across all 121 files the vocabulary
is closed:

- **Walls**: `None`, `Solid`, `Door`, `SecretDoor`, `LockedDoor`, `CrumblingWall`,
  `InvisibleWall`, `Railing`, `SolidRailing` — each optionally suffixed `NoPHDO`, meaning Phase
  Door cannot pass it.
- **Cell flags**: `StairsIn`, `StairsOut`, `PortalUp`, `PortalDown`, `Spinner`, `Darkness`,
  `AntiMagic`, `AntiApar`, `AntiMap`, `DrainMagic`, `RegenMagic`, `RegenHealth`, `HarmParty`,
  `PoisonGas`, `Smoke`, `SilenceBard`, `Flypaper`, `Turncoat`, `RandomCombat`, `PresetCombat`,
  `RandomTrap`, `SpecialAhead`, `Secret`, `Odd`, `Runes`.
- **City modules**: `Generic`, `Tavern`, `Temple`, `Casino`, `Guild`, `Garths`, `Review`,
  `Roscoes`, `Bank`, `StorageRoom`, `WizardsGuild`, `BardsHall`.

The trainer reads these out of the player's own installation rather than bundling them: it walks
the serialised-file object table (Unity format 17), finds the `TextAsset` by name, and reads only
that blob. No game content is redistributed with the trainer.

## 11. Methodology and limitations

### 11.1 What was done

- **Metadata extraction**: `global-metadata.dat` (v24.1) was parsed for the type, field, method
  and constant tables, and `GameAssembly.dll` for `Il2CppMetadataRegistration`'s field-offset,
  type and metadata-usage tables. Together these give every class's exact layout, every enum's
  values, and the RVA of every type's class slot. The tooling used is a few hundred lines of
  Python and is kept in the working notes, not in the trainer.
- **Disassembly**: the compiled methods that matter were read with Capstone, reached through the
  method-pointer table indexed by each method's metadata entry — `Player::OnStateTick` and
  `QueueTeleportTo` for the teleport contract, `Character::UseItemCharge` for the item-charge
  rule, `PlayerState_ReviewBoard::UpgradeMage` for the spell-level cap, and
  `OnGotGetDreamSpellDestination` for what a dream-spell target actually indexes.
- **Asset extraction**: the three `GlobalMaps` objects were parsed out of `level3`/`level4`/
  `level5`, and the 121 map files out of `resources.assets`, using Unity's serialised-file
  format 17 and the field order from the metadata.
- **Cross-checks**: the extracted offsets reproduce the four the Cheat Engine community had
  published by AOB scanning; the `GlobalMaps` parse consumes each object's byte range exactly;
  the map catalogue's sizes match the map files' own headers for all 121 maps; and the class
  slot RVAs were derived twice, once from the metadata-usage table and once by reading them out
  of the compiled code.
- **Community references**: spell lists and the item catalogue came from the
  community-maintained references at `bardstaleonline.com` and `bardstale.brotherhood.de`; the
  class-change rules and the meaning of each ability score come from the game manual.

### 11.2 What was NOT done

- **No live game session.** Everything here is static: the game was never attached to, so no
  offset has been watched changing in a running process and no teleport has been performed in a
  live game. The verification harness exercises every memory path against a synthetic IL2CPP
  heap instead, which proves the code walks the layout it believes in — not that the layout is
  what the running game has. Marked `[Verified]` throughout to mean "read from the game's own
  data", which is a different claim from "watched working".
- **No save-file decoding.** The `.dat` save format is still opaque; the trainer is live-memory
  only.
- **No spell-list append.** Granting a spell that belongs to no school (ZZGO, NUKE) means
  appending to `Character.m_learntSpells`, which needs a `List<Spell>` growth path. See
  `docs/SpellSystem.md` §6.2.
- **Garth's shop inventory** is still unlocated; see §7.

### 11.3 Confidence markers

Offsets in the trainer code carry a marker:

- `[Verified]` / `[Confirmed]` — read out of the game's own metadata or its compiled code, and
  cross-checked as described in §11.1.
- `[Inferred]` — estimated rather than read. Almost nothing is left in this category; where it
  remains, it is called out at the constant.
- `[Static]` — derived from the game's data files or from community reference data.
