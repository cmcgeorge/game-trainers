# The Quest — Trainer

A Windows/WPF live-memory trainer for **The Quest** (Redshift Ltd., 2006; v1.9.10 GOG re-release).

It attaches to the running game, finds your character by itself, and edits it in place. There is no
value searching, no address to paste in, and nothing to configure — press **Attach** and the
character sheet fills in.

The game is a native 32-bit Windows program, so there is no DOSBox in the way.

Two companion documents live in [`docs/`](docs/):

- [`ReverseEngineering.md`](docs/ReverseEngineering.md) — how the character record was found and what
  every field in it means.
- [`StrategyGuide.md`](docs/StrategyGuide.md) — how the game's systems actually work, with the real
  numbers.

---

## Quick start

```powershell
.\Run.ps1
```

Builds Release and launches the trainer. A UAC prompt appears — reading and writing another
process's memory needs it when the game was started elevated.

Then:

1. Start **The Quest** and load or begin a game. (You can attach from the main menu, but the
   character record only exists once a game is loaded.)
2. Pick `TheQuest` in the **Process** box — it is selected for you if it is running.
3. Press **Attach**.

The status bar tells you which chain found the record, where it is, and whether the build you are
attached to is the one the offsets were measured on.

### Other options

```powershell
.\Run.ps1 -Test -NoRun          # 223 checks, no game and no copyrighted files needed
.\Run.ps1 -Configuration Debug  # Debug build
.\Run.ps1 -Clean                # delete bin/obj first
.\Run.ps1 -NoBuild              # launch the last build
.\Run.ps1 -Publish              # single self-contained win-x64 exe
```

---

## What it can do

### Character tab

| | |
|---|---|
| **Name, race, portrait** | Read-only. The portrait is the game's own resource id. |
| **Health**, **Mana** | Current values, editable, each with a **Freeze**. The maxima are *derived* by the game and are not stored anywhere — you can set health above the maximum and the game will show `500/72` without complaint. |
| **Gold** | Editable, freezable. |
| **Crime** | The outstanding bounty. Editable, freezable, plus a **Clear crime** button. Serving a prison sentence permanently lowers skills, so this is the one status worth zeroing. |
| **Level** | Editable, plus **Level up**. Writing a level also raises experience to that level's floor and rewrites the game's cached next-level threshold, so the character stays internally consistent. |
| **Experience** | Editable on its own. Note the game applies a *level* only when it next awards experience — use the Level field if you want it now. |
| **Attribute points**, **Skill points** | Unspent points, editable. |
| **Fame** | −100..+100, editable, with the game's own reputation word shown beside it. |
| **Attributes** | The five base values, editable. |

### Skills tab

All twenty skills with their base value, the value the character was created with, the governing
attribute, and the game's own cap for that skill. **Max skills** raises everything to the game's own
ceiling — twice the base value of its governing attribute — without lowering anything already above
it, and leaves the two race-locked schools alone (Undead Magic for non-Rasvim, Healing Magic for
Rasvim).

### Inventory tab

Everything the character is carrying: name, kind, weight, damage, condition or charges, and whether
it is worn. Three things you can do with it.

**Restore** — and **Restore all** — fills an item's one mutable word to the ceiling the game itself
would use: repairs worn weapons and armour, recharges wands from their own enchantment, and refills
quivers. It is the outcome of the game's repair hammer and recharge shop without the hammer, the
skill check or the fee.

**Replace with…** points an item at any of the ~1,080 item types the loaded game knows about, in mint
condition. This is how the trainer gives you things — and the reason it is phrased as *replace*
rather than *add* is worth knowing: an item is a heap allocation, and the trainer has no safe way to
make the game allocate one. What it can do is change what an item you already carry *is*, because the
only difference between a Loaf of Bread and a King's Longsword is which shared type the item points
at. Bring some bread. The picker filters on the displayed name or the game's internal id
(`base_weap_longsword`), and the list is swept out of the game's own heap on attach, so it includes
whatever the expansions have loaded.

**The Value column** sets that word outright, when neither "full" nor "empty" is what you wanted.

An **equipped item cannot be replaced** — unequip it in the game first. The equipment slots hold raw
pointers and the game rebuilds the model and the paperdoll from them, so retyping in place would
leave a body slot holding something the game never put there. For the same reason the trainer shows
what is worn but never moves it; equipping is one click in the game's own inventory screen.

### Reference tab

Attributes, skills and their governing attributes, race ids, the reputation ladder and the wardrobe
ladder, all lifted from the game's own tables.

### Base values, not screen values

The trainer edits **base** attributes and skills. What the game's status and skills screens show is
that base plus racial and equipment modifiers. A Derth's racial ability is
`-5 Str/Dex/End, +10 Int, +10 Healing/Mind/Attack Magic`, so a base of 40 in Attack Magic reads 50 in
the game. That is the game agreeing with the trainer, not disagreeing.

### Read-only mode

Ticking **Read-only** refuses every write and releases every freeze. Useful for watching the game's
own numbers move — how experience is awarded, what a level-up does to the cached threshold — without
touching anything.

---

## What it deliberately does not do

- **Moving equipment.** Which body slot takes which kind of item was not established, and equipping
  by writing a raw pointer would bypass the model and paperdoll updates the game does around it. The
  Inventory tab shows what is worn; the game's own inventory screen is where you change it.
- **Adding an item outright.** An item is a heap allocation. Replacing one is a pointer write and
  safe; making the game allocate a new one is not.
- **Item enchantments.** Read only as far as a wand's charge ceiling. Damage, armour and the outfit
  score are all still the game's own arithmetic over what you carry.
- **Maximum health and maximum mana.** They are not stored — the engine derives them from Endurance,
  Intelligence and level every frame. Raise the attribute, or freeze the current value.
- **Resistances, damage and armour.** Derived, same reason.
- **Teleporting.** The map and coordinate fields were not located, and the game's own **Mark** and
  **Recall** spells already do this properly.
- **Spells and quest flags.** Script-driven; the game's dialog and quest system is the supported
  route.
- **Save editing.** The format is partly decoded in `docs/ReverseEngineering.md`, but that is a
  different tool. The Quest autosaves aggressively, so anything written here reaches disk anyway.

---

## How it finds anything

Two independent chains and one validator. Neither chain asks you to search for a value.

**Chain A — the module's own pointer.** `.data` holds a pointer to the game's engine object at
RVA `0x00335790`, and the live character record is embedded in that object at `+0x3DC8`. Two reads.
The slot is only read at all when its RVA lands in a writable, non-executable section of the
*mapped* PE, because a different build could put code there.

**Chain B — the structural sweep.** Every character record carries a copy of the per-level
experience table, and its first eight entries (400, 900, 1500, 2500, 4000, 7000, 11000, 17000) are a
32-byte pattern nothing else in the process matches. Subtract the table's offset from a hit and you
have a candidate. This chain knows no RVAs at all, so it survives a build that moves the static slot.
It takes about a second over a 257 MB process.

**The validator is what makes either chain safe.** A record is accepted only if its first dword is a
vtable pointer into the image's *read-only* data, its embedded experience table matches, its name and
portrait are well-formed MSVC `std::string`s, its name is non-empty, and its level, health, mana,
attributes and race id are all in range. Those last two checks matter more than they look: the game
keeps a pristine **new-character prototype** in the same engine object, with the same vtable and the
same table, and "no name, no next-level threshold" is exactly what separates it from the character
you are playing.

`TheQuest.exe` sets DYNAMICBASE, so nothing assumes `0x00400000`; the observed base in one session
was `0x00260000`.

### While it is attached

The record is re-validated on every refresh, not just re-read: a freed heap block usually stays
readable, so "the read worked" is precisely the case that would otherwise leave the window showing
stale numbers. If the record moves — you saved, loaded, died or started a new game — the trainer
picks up the replacement from the module's engine pointer and tells you it did. If it cannot, it
says so and stops rather than writing into whatever now lives there.

---

## Layout

```
src/TheQuestTrainer/
  Game/
    GameFacts.cs          build fingerprint, clamps, the game's own rules
    QuestLayout.cs        every offset, each stated as arithmetic on the one before it
    GameTables.cs         attributes, skills and governing attributes, races, fame and outfit bands
    PeImage.cs            mapped-PE header parsing (sections, timestamp, ASLR, DLL bit)
    ModuleResolver.cs     module list first, header sweep when that fails
    StdString.cs          the 32-bit MSVC std::string, and why it is the best validator here
    CharacterLocator.cs   two chains + validation
    CharacterReader.cs    the record as one typed snapshot
    ItemLayout.cs         the pack, the equipment slots, the item and the item type
    ItemTables.cs         the game's own item categories, sub-types and wear bands
    ItemType.cs           the shared type, and the four checks that identify one
    ItemCatalog.cs        every item type in the loaded game, swept out of the heap
    InventoryReader.cs    the pack as one typed snapshot
    TrainerActions.cs     every edit, as read-validate-write
    FreezeWriter.cs       latched freezes, testable without a dispatcher
  Memory/IMemorySource.cs the process slice the locator needs, so it can be faked
  ViewModels/             MainViewModel (session + IGameHost), rows, ProcessPicker
  MainWindow.xaml         Character / Skills / Inventory / Reference tabs
test/FormatCheck/         363 checks over synthetic records and a synthetic heap; needs no game
```

References `GameTrainers.Common` for both `Memory` (`ProcessMemory`, `NativeMethods`) and `Mvvm`
(`ObservableObject`, `RelayCommand`).

---

## Verification

```powershell
.\Run.ps1 -Test -NoRun
```

363 checks against a synthetic 32-bit address space with the same section geometry as the real
image. It covers the cases a live game cannot be asked to produce: a module relocated away from its
preferred base, a stale static slot, an empty slot, a build whose `.data` does not cover the slot, a
record whose vtable points at writable memory, the new-character prototype sitting next to the live
record, two live-looking records at once, an unreadable page in the middle of the heap, and a
`std::string` whose heap buffer has gone away.

The inventory half gets its own synthetic heap: item types with their strings reached by pointer, a
pack covering every shape the reader has to handle (worn gear, gear already at full, an item with no
meter, a wand whose charges come from an enchantment, a stack of ammunition), an item equipped in
each of the two weapon sets, a vector broken in each of the four ways it can be, an item whose type
stopped validating, and a decoy heap block that looks like an item type in everything but its vtable.

It was also checked against a live session (v1.9.10, character *Gerth the Derth*): both chains found
the same record, every field matched the game's own screens, and every write path — gold, health,
mana, crime, fame, a skill, an attribute, points, the three-field level write, and the item repair,
recharge and replace paths — was set to a test value, read back, and restored.
`docs/ReverseEngineering.md` §11 and §15.7 have the logs.

---

## Requirements

- Windows 10/11
- .NET 8 SDK (`dotnet` on PATH)
- The Quest v1.9.10. Other builds will attach; if the link stamp does not match, the status bar says
  so and Chain B is the one to trust.

---

## Notes

The trainer never attaches to itself: its own process name contains "quest", so an exact name match
outranks a substring match and a substring-only match is never selected automatically.

Every write re-validates the record first. Saving, loading, dying or starting a new game replaces the
record, and re-validating turns "wrote gold into a freed heap block" into a refusal with a reason.

Values are clamped to the field they are going into, and the editor is put back in step with what
actually landed — ask for 9,999 in a skill and both the game and the box end up showing 250. Editing
a field that is frozen moves the freeze to the new value, so **Clear crime** with Crime frozen stays
cleared.
