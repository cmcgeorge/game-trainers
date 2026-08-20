# The Bard's Tale Trilogy — Spell System (verified against the installed game)

Status: **[Verified]** — everything in this document was derived from the installed
game files (`global-metadata.dat` + `GameAssembly.dll`), not inferred from the DOS
originals or from third-party Cheat Engine tables.

| Field | Value |
|-------|-------|
| Install | `C:\Program Files (x86)\Steam\steamapps\common\The Bard's Tale Trilogy` |
| Unity | 2018.4.0 (`FileVersion 2018.4.0.11993000`) |
| IL2CPP metadata | version **24.1** (`sanity 0xFAB11BAF`) |
| Image base | `0x180000000`, x64, ASLR |
| Type definitions | 5030 |

## 1. Method

No third-party tooling was required. The chain was:

1. **Parse `global-metadata.dat` (v24.1)** — header, type definitions (100 bytes each),
   fields (12 bytes each), methods (52 bytes each), field default values (12 bytes each).
   This yields every type name, field name, method name, and enum constant value.
2. **Locate `Il2CppMetadataRegistration` in `GameAssembly.dll`** by scanning `.rdata`
   for the `fieldOffsetsCount == typeDefinitionsSizesCount == 5030` signature.
   Found at VA `0x180C3BC10`. This gives `fieldOffsets[typeIndex] -> int32[]`,
   i.e. the **real** offset of every field of every type, plus `typeDefinitionsSizes`
   (instance sizes) and the `Il2CppType` table for resolving field types.
3. **Locate `Il2CppCodeRegistration`** by finding the `lea rdx, [rip+...]` in the codegen
   registration thunk that points at the metadata registration, then reading the
   adjacent `lea rcx`. Found at VA `0x180CD8410`. `methodPointers[methodIndex]`
   then gives the native address of every managed method.
4. **Disassemble the relevant methods** (capstone) with a method-address to name map
   built from steps 1 and 3, so call targets are symbolised.

## 2. `BardsTale.Character` — real layout

`instance_size = 0x108 (264)`. All offsets are from the object base (IL2CPP header included).

| Offset | Field | Type |
|--------|-------|------|
| `+0x10` | `m_recentSpells` | `List<Spell>` |
| `+0x18` | `m_stats` | `GameStats` |
| `+0x20` | `m_turncoat` | `bool` |
| `+0x21` | `m_combatMonster` | `bool` |
| `+0x22` | `m_doppleganger` | `bool` |
| `+0x23` | `m_reanimated` | `bool` |
| `+0x28` | `m_name` | `string` |
| `+0x30` | `m_gender` | `Character.Gender` |
| `+0x34` | `m_race` | `Character.Race` |
| `+0x38` | `m_class` | `Character.Class` |
| `+0x3C` | `m_monsterType` | `int` |
| `+0x40` | `m_monsterTypeDesc` | `MonsterDescription` |
| `+0x48` | `m_serializeMonsterType` | `string` |
| `+0x50` | `m_experience` | **`long`** |
| `+0x58` | `m_strength` | `int` |
| `+0x5C` | `m_intelligence` | `int` |
| `+0x60` | `m_dexterity` | `int` |
| `+0x64` | `m_constitution` | `int` |
| `+0x68` | `m_luck` | `int` |
| `+0x70` | `m_gold` | **`long`** (per character) |
| `+0x78` | `m_bloodAlcohol` | `float` |
| `+0x7C` | `m_level` | `int` |
| `+0x80` | `m_maxHitpoints` | `int` |
| `+0x84` | `m_hitpoints` | `int` |
| `+0x88` | `m_maxSpellpoints` | `int` |
| `+0x8C` | `m_spellpoints` | `int` |
| `+0x90` | `m_nmbrOfAttacks` | `int` |
| `+0x94` | `m_pictureNumber` | `int` |
| `+0x98` | `m_portrait` | `string` |
| `+0xA0` | `m_condition` | `int` (`Character.Condition`) |
| `+0xA4` | `m_isDestinyKnight` | `bool` |
| `+0xA8` | `m_realLevel` | `int` |
| `+0xAC` | `m_levelDrain` | `int` |
| `+0xB0` | `m_nmbrOfBattles` | `int` |
| `+0xB4` | `m_disarmTrapBonus` | `int` |
| `+0xB8` | `m_identifyBonus` | `int` |
| `+0xBC` | `m_hideInShadowsBonus` | `int` |
| `+0xC0` | `m_criticalHit` | `int` |
| `+0xC4` | `m_songsRemaining` | `int` |
| `+0xC8` | `m_songsKnown` | `int` |
| **`+0xD0`** | **`m_spellLevel`** | **`int[16]`** |
| **`+0xD8`** | **`m_learntSpells`** | **`List<Spell>`** |
| `+0xE0` | `m_inventory` | `Inventory` |
| `+0xE8` | `m_scriptFlags` | `BitArray[3]` |
| `+0xF0` | `m_statusEffects` | `StatusEffects` |
| `+0xF8` | `m_hiding` | `int` |
| `+0xFC` | `m_defending` | `bool` |
| `+0x100` | `m_initialClass` | `Character.Class` |

Notes:

- There is **no armour-class field** — `Character::GetAC()` computes it from equipment.
- There is **no separate "max attribute" block**; the five attributes are stored once.
- `m_condition` is an **ordinal**, not a bitfield: `Okay=0, Poisoned=1, Old=2, Dead=3,
  Stoned=4, Paralyzed=5, Possessed=6, Insane=7, Drained=8`.
- Party gold is `Party.Instance.m_gold` at `+0x68`, and it is a **`long`**. (This is the
  field the published CE gold script writes with `mov [rdi+68],rax` — a 64-bit write.)

### Enums

```
Class : Warrior=0 Paladin=1 Rogue=2 Bard=3 Hunter=4 Monk=5
        Conjurer=6 Magician=7 Sorcerer=8 Wizard=9
        Archmage=10 Chronomancer=11 Geomancer=12
        Monster=13 Illusion=14 NPC=15 MAX=16
Race  : Human=0 Elf=1 Dwarf=2 Hobbit=3 HalfElf=4 HalfOrc=5 Gnome=6 MAX=7
```

## 3. How spell knowledge actually works

### 3.1 `Character::KnowsSpell` (RVA `0x1F2590`)

Decompiled semantics:

```csharp
bool KnowsSpell(Spell s)
{
    if (m_learntSpells != null && m_learntSpells.Contains(s))
        return true;

    SpellDescription d = GlobalSpells.Instance.GetSpell(s);   // m_spellsByEnum[(int)s]
    if (d == null) return false;
    if (d.m_level == 0) return false;                         // special/quest spells
    if (m_spellLevel == null) return false;
    return m_spellLevel[(int)d.m_class] >= d.m_level;
}
```

So there are exactly **two** independent ways to know a spell:

1. **Per-school level** — `m_spellLevel[class] >= spell.m_level`, for spells that have a
   non-zero level. `m_spellLevel` is an `int[16]` **indexed by the `Class` enum**, so the
   seven casting schools live at indices **6..12** (Conjurer, Magician, Sorcerer, Wizard,
   Archmage, Chronomancer, Geomancer). Indices 0-5 and 13-15 are unused.
2. **The explicit learnt-spell list** — `m_learntSpells.Contains(spell)`.

`Character::BuildSpellList` (which populates the in-game cast menu) calls `KnowsSpell`
for each candidate, so either mechanism makes a spell castable.

### 3.2 The level cap is 7, and it is derived from character level

`PlayerState_ReviewBoard::UpgradeMage(Character c, Class cls, int charLevel)`:

```csharp
int lvl = Mathf.Min(7, (charLevel + 1) / 2);
if (c.m_spellLevel[cls] >= lvl) return;
if (c.m_class != cls) { c.m_class = cls; c.m_level = 0; }
while (c.m_level < charLevel) c.LevelUp(...);
c.m_spellLevel[cls] = Mathf.Max(c.m_spellLevel[cls], lvl);
```

`Mathf.Min(7, ...)` is the authoritative cap: **spell level ranges 0-7 per school**, and
character level 13 is what the game itself uses to reach 7. The game's own debug cheat
(`PlayerState_Cheats::OnLevelEntered`) zeroes `m_spellLevel[0..12]` before re-applying,
confirming both the indexing and the array bounds.

### 3.3 Special spells (ZZGO, NUKE, ...) are list-only

`SpellDescription.m_level == 0` marks a spell as unobtainable through school levels.
The only way the game grants such a spell is `Character::LearnSpell` (RVA `0x1F2950`):

```csharp
void LearnSpell(Spell s)
{
    m_learntSpells ??= new List<Spell>();
    if (!m_learntSpells.Contains(s))
        m_learntSpells.Add(s);
}
```

There are exactly four call sites in the whole binary:

| Caller | Meaning |
|--------|---------|
| `PlayerState_ReviewBoard::LearnQuestSpells` | chapter quest spells, Chronomancers only (`m_class == 11`) |
| `PlayerState_ReviewBoard::OnBuySpellYes` | buying a spell at the Review Board |
| `PlayerState_ReviewBoard::OnChooseBuySpellPayer` | same flow, payer variant |
| `PlayerState_Script::ProcessScript` | a map/event script teaching a spell |

There is **no spell-knowledge bitfield** anywhere on the character. The trainer's
current `OffSpellKnowledge = 0xB0` guess does not exist — `+0xB0` is `m_nmbrOfBattles`.

### 3.4 Spell identifiers

`BardsTale.Spell` is an `int` enum with 249 members. The ones the proposal calls out:

| Code | Enum member | Value |
|------|-------------|-------|
| ZZGO | `DreamSpell` | **78** |
| NUKE | `Gotterdamurung` | **154** |
| GILL | `GillesGills` | **152** |
| DIVA | `DivineIntervention` | **153** |

`NONE = 255`, `MAX = 272` (the enum is not contiguous at the tail).

The four-letter codes are **not** in the binary. They live in `SpellDescription.m_code`,
a serialized Unity asset field, so the authoritative code-to-spell mapping can only be
read from `GlobalSpells.Instance.m_spellsByEnum` at runtime (or by unpacking
`resources.assets`).

### 3.5 `SpellDescription` (instance size `0xB8`)

| Offset | Field |
|--------|-------|
| `+0x10` | `m_code` (`string`, e.g. "ZZGO") |
| `+0x18` | `m_spell` (`Spell`) |
| `+0x20` | `m_class` (`Class` — the school) |
| `+0x24` | `m_level` (`int`, 0 = special) |
| `+0x28` | `m_cost` (`int`) |
| `+0x38` | `m_combat` (`bool`) |
| `+0x39` | `m_nonCombat` (`bool`) |
| `+0x41` / `+0x42` / `+0x43` | `m_bt1Spell` / `m_bt2Spell` / `m_bt3Spell` |

`GlobalSpells::GetSpell` (RVA `0x23D240`) is simply `m_spellsByEnum[(int)spell]`.

## 4. Static anchors (replaces the guessed pointer chain)

IL2CPP stores one `Il2CppClass*` per referenced type in a writable slot in `.data`.
`Il2CppClass.static_fields` is at **`+0xB8`** in this build, and each class's
`Instance` static sits at offset `0` within its static-field block.

So: `[[[GameAssembly.dll + RVA] + 0xB8]] -> Instance`.

| Class | `Il2CppClass*` slot RVA |
|-------|-------------------------|
| `BardsTale.Party` | `0xE44900` |
| `BardsTale.Roster` | `0xE45C08` |
| `BardsTale.GlobalSpells` | `0xE44C18` |
| `BardsTale.GlobalClasses` | `0xE45400` |
| `BardsTale.GlobalItems` | `0xE45CC0` |
| `BardsTale.Player` | `0xE44BF8` |
| `BardsTale.GameSaver` | `0xE45A90` |

From there:

- `Roster.Instance + 0x18` -> `List<Character> m_characters` — **every** character in the
  roster, not just the active party.
- `Party.Instance + 0x40` -> `PartyMember[] m_members`; `PartyMember + 0x10` -> `Character`.
- `Party.Instance + 0x68` -> `long m_gold`.
- `GlobalSpells.Instance + 0x20` -> `SpellDescription[] m_spellsByEnum`.

This is the same shape as the published CE table's `GameAssembly.dll+0xE40338 -> +0xB8`
chain — that RVA was the `Party` class slot in build 4.28. It has moved to `0xE44900`
in the installed build, which is why an RVA-only locator is build-fragile. The slot can
be re-derived per build by pattern-scanning, or the structural scan can be retargeted at
the now-known real layout.

## 5. Container layouts needed for writes

`List<T>` (x64):

| Offset | Field |
|--------|-------|
| `+0x10` | `_items` (`T[]`) |
| `+0x18` | `_size` (`int`) |
| `+0x1C` | `_version` (`int`) |

IL2CPP array (x64): `+0x18` = `max_length` (`int`), `+0x20` = first element.
`Spell` and `int` elements are 4 bytes.

Verified directly against the compiled `List<Spell>::Add`:
`_items[_size] = value; _size++; _version++;` with a growth call when `_size == _items.Length`.

## 6. Consequences for the trainer

### 6.1 Setting per-school spell levels — straightforward

Write `int32` values into the `m_spellLevel` array:

```
levelAddr = [character + 0xD0] + 0x20 + (classIndex * 4)      // classIndex 6..12
```

Read `[character + 0xD0] + 0x18` first to bounds-check (it is 16). Valid values are
**0-7**. This needs no code injection and no allocation, and it covers all seven schools
including Archmage, Chronomancer and Geomancer, which the trainer currently cannot touch
at all.

Caveat worth surfacing in the UI: raising a school level grants the spells but does not
raise `m_maxSpellpoints`, because the game's own path (`UpgradeMage`) grows SP through
`Character::LevelUp`. A character with school level 7 and a low character level will know
level-7 spells but may not have the spell points to cast them. Editing `m_maxSpellpoints`
alongside is the practical answer.

### 6.2 Granting ZZGO / NUKE — needs an append to `m_learntSpells`

Fast path (no injection), when there is spare capacity:

```
items = [character + 0xD8 + 0x10]
size  = [character + 0xD8 + 0x18]
cap   = [items + 0x18]
if (size < cap):
    write int32 spellId at items + 0x20 + size*4
    write int32 size+1  at character + 0xD8 + 0x18
    increment [character + 0xD8 + 0x1C]
```

The catch: `Character..ctor` initialises `m_learntSpells = new List<Spell>()`, whose
backing array is the shared **zero-length** `EmptyArray<T>.Value`. So for any character
who has never been taught a script/quest/bought spell, `cap == 0` and the fast path
cannot fire. Growing the list means allocating a GC-tracked `int[]`, which cannot be done
with `WriteProcessMemory` alone.

**What was built.** Both, with the safe path first — `CharacterRecord.GrantSpell`:

1. If the spell is already in the list, do nothing.
2. Try `Il2Cpp.TryAppendInt32`: write the element, then `_size`, then bump `_version`. Pure
   `WriteProcessMemory`, and the element lands before the count so the game never sees a
   `_size` covering an unwritten slot.
3. Only if that fails for want of capacity, grow the list through `Il2CppRuntime`.

The growth path deliberately **does not** call `Character::LearnSpell`, even though it would
be the tidiest single call. Its RVA is build-specific, and a wrong RVA means a remote thread
jumping into arbitrary code. Instead the stub calls only functions the module *exports*, which
are resolved from its own export table at run time:

```
il2cpp_domain_get()  ->  il2cpp_thread_attach(domain)
il2cpp_gc_disable()
il2cpp_array_new_specific(klass, capacity)     // klass from the old array's own header
il2cpp_thread_detach(thread)
```

The array type comes from the `Il2CppClass*` in the header of the array being replaced, so the
runtime allocates exactly the type the field already holds — no type lookup to get wrong. The
host then copies the old elements, appends the new id, publishes `_items`, and only then raises
`_size`; a second stub re-enables collection. The collector stays disabled across that gap
because the new array is unreachable until `_items` points at it, and Boehm would otherwise be
free to take it back.

If an export is missing, or the process cannot be opened with thread-creation rights, the helper
simply does not open and the shortfall is reported. It can also be turned off in the UI, which
leaves behaviour (B) above.

A third, inferior option exists and is worth recording so it is not rediscovered:
rewriting `GlobalSpells.Instance.m_spellsByEnum[spellId].m_class` and `.m_level` to a real
school and level makes `KnowsSpell` succeed through the level path. It is global (every
character of that school gets the spell), it changes what the UI displays for that spell,
and it is not persisted in saves — so it is a demo trick, not a feature.

Persistence: `m_learntSpells` is a serialized field on `Character`, so a successful append
survives a save/load. `m_spellLevel` likewise.

### 6.3 Where this landed in the code

| Concern | Lives in |
|---------|----------|
| The `Spell` enum, generated from `global-metadata.dat` | `Game/SpellId.cs` |
| The four cross-game spells and their codes | `SpecialSpells` in the same file |
| Reading the game's real spell table | `Game/SpellCatalog.cs` |
| `List<T>` layout, append and remove | `Il2Cpp` in `Game/Il2Cpp.cs` |
| `ReadLearntSpells`, `KnowsSpell`, `GrantSpell`, `RevokeSpell` | `Game/CharacterRecord.cs` |
| Exported-only remote calls | `Memory/Il2CppRuntime.cs` |

### 6.4 Offsets the earlier layout got wrong

The original `CharacterFormat` was reconstructed from the DOS 109-byte record, before the game
was available to read. Against the verified layout, only HP/SP
(`+0x80`/`+0x84`/`+0x88`/`+0x8C`) and the XP *position* (`+0x50`) were right. Name, race,
class, level, all five attributes, armour class, the four "spell class level" bytes at
`+0x94..0x97`, the inventory pointer and the spell-knowledge pointer were all at wrong
offsets, and `LooksLikeCharacter` validated fields that do not exist (there is no
max-attribute block, and no armour-class field at all).

The worst of it: those four "spell class level" bytes at `+0x94..0x97` land on
`m_pictureNumber`, so the old "Learn All Spells" changed the character's portrait and granted
nothing.

`CharacterFormat` has since been rewritten against the table in §2, so the offsets in this
document and the ones in the code are the same set.
