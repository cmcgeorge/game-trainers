# TheQuestTrainer — agent guide

Read this before changing anything in this folder. The root [`AGENTS.md`](../AGENTS.md) covers the
repository conventions; this file covers what is different about *this* trainer.

## The one thing to understand first

**The Quest keeps one character record, embedded in one big heap object, and keeps a second identical
one right next to it.** The engine object holds the live character at `+0x3DC8` and a pristine
"new character" prototype at `+0x06F0`. Both have the same vtable and both carry the same 98-entry
experience table, so any locator that matches on shape alone finds two records and has to choose.

What separates them: the prototype has an **empty name** and a **zero next-level threshold**. Those
two checks in `CharacterLocator.Validate` are load-bearing. Do not relax them.

## Layout

```
src/TheQuestTrainer/
  Game/
    GameFacts.cs          build fingerprint, clamps, the game's own rules
    QuestLayout.cs        every offset, each stated as arithmetic on the one before it
    GameTables.cs         attributes, skills + governing attributes, races, fame/outfit bands
    PeImage.cs            mapped-PE header parsing
    ModuleResolver.cs     module list first, header sweep when that fails
    StdString.cs          32-bit MSVC std::string reader — the strongest validator here
    CharacterLocator.cs   two chains + validation
    CharacterReader.cs    the record as one typed snapshot
    ItemLayout.cs         the pack, the equipment slots, the item and the item type
    ItemTables.cs         the game's own item categories, sub-types, meters and wear bands
    ItemType.cs           the shared type + the four checks that identify one
    ItemCatalog.cs        every item type in the loaded game, swept out of the heap
    InventoryReader.cs    the pack as one typed snapshot
    TrainerActions.cs     every edit, as read-validate-write
    FreezeWriter.cs       latched freezes, no dispatcher needed
  Memory/IMemorySource.cs the process slice the locator needs, so it can be faked
  ViewModels/             MainViewModel (session + IGameHost), rows, ProcessPicker
  MainWindow.xaml         Character / Skills / Inventory / Reference tabs
test/FormatCheck/         363 checks over synthetic records and a synthetic heap; needs no game
```

References `GameTrainers.Common` for both `Memory` and `Mvvm`, via csproj `<Using>` items.

## Rules that are load-bearing

**The offsets tile, and they are written as arithmetic on purpose.** `QuestLayout` derives
`AttributePoints` from `BaseAttributes + AttributeSlots * 2`, `SkillPoints` from
`SkillDisplayOrder + SkillDisplayOrderBytes`, and so on, because the record's arrays really do abut.
A mistyped constant then fails a harness check instead of quietly reading a neighbouring field in
someone's game. If you add a field, state it relative to the one before it and add the check.

**Arrays are id-indexed with an unused slot 0.** Attributes are `[6]` with ids 1..5, skills are `[21]`
with ids 1..20. The reader keeps slot 0 rather than shifting, so `snapshot.Skills[skill.Id]` is
always right and nothing has to remember whether a particular list is offset by one.

**Nothing is written to an address that has not just re-validated.** `TrainerActions.Ready` re-runs
the full validator before every write. The player can save, load, die or start a new game between two
timer ticks, and any of those replaces the record. This costs one page read and turns a write into a
freed block into a refusal with a reason. Do not add a "fast path" that skips it.

**The refresh re-validates, it does not merely re-read.** A freed or replaced heap block usually
stays committed and readable, so "the read succeeded" is exactly the case a raw read cannot catch —
the window would sit there showing whatever the stale bytes decode to while every edit was silently
refused. `Refresh` runs the full validator and, on failure, tries `LocateViaStaticSlot` to pick up
the replacement record. It deliberately does *not* run the heap sweep there: that takes about a
second and this is the UI thread, four times a second.

**A freeze latches its target, and an explicit edit re-latches it.** `FreezeWriter` captures the
value when the box is ticked and never re-derives it from what is on screen — the refresh overwrites
the displayed value with whatever the game holds, so a derived target would follow the damage down
and oscillate four times a second. That rule is about the *refresh*. When the user deliberately sets
a new value — types into a frozen Gold box, or presses **Clear crime** with Crime frozen —
`MainViewModel.Apply` and `ReLatch` move the latch to the new value; otherwise the trainer reports
success and silently undoes the edit a quarter of a second later. The freeze logic lives outside the
view model so it can be tested without a WPF dispatcher; keep it there.

**`Detach` writes the status line, so a reason must be set after it, not before.** `Detach()` puts
"Detached." up whenever it had a session to end, which silently destroys a reason set beforehand —
and the reason is the only place the validator's explanation is ever shown. Every path that ends a
session because something went wrong goes through `DetachBecause`. Do not set `Status` and then call
`Detach()`.

**A successful write is not proof the screen is right.** Every write clamps to the field it is going
into, so `ActionResult.Written` carries what actually landed and `Apply` / `GameRowViewModel.Settle`
put the editor in step with it. The refresh cannot be relied on for this: it skips every scalar while
*any* editor in the window has focus, so a box could keep showing 9,999,999,999 for the rest of the
session after the game took 999,999,999. Any new write path must return `Written`.

**A refresh may not overwrite a value being typed into, and a new row must still get one.**
`IGameHost.EditorHasFocus` is a *probe* the window answers from `FocusManager`'s logical focus, not a
flag tracked from focus events: a tracked flag latches on forever when the focused editor is
destroyed rather than blurred, and clearing it when focus leaves the application throws away a
half-typed value on alt-tab. Separately, every `Update` takes an `initial` flag — a row built while
an editor has focus must still take the game's numbers.

**A refused edit reverts.** `GameRowViewModel.Reject` and `MainViewModel.Apply` put the backing field
back and re-raise the property, including when the write was attempted and came back incomplete —
not only the "not attached" case.

**Base values, not screen values.** The record holds base attributes and skills; the game's screens
add racial and equipment modifiers. Every piece of UI copy that mentions a number says so. If you
add a field, work out which side of that line it is on before you label it.

**An item is a pointer to a type, and that is why the trainer can give you things.** Per-item state
is one word; the name, weight, damage and every ceiling belong to a shared `SItemType` the item
points at. So `ReplaceItem` writes a dword and the player has a King's Longsword. Do not try to
*add* an item — that means allocating in the game's heap, and there is no safe way to do it from
outside. `docs/ReverseEngineering.md` §15 has the whole graph.

**An item write is addressed by address, and re-finds the item first.** `TrainerActions.FindItem`
re-reads the pack and searches it for the pointer before every item edit. Items are heap objects the
game frees when the player drops, sells, eats or breaks one, and the vector closes up behind them —
so an index captured when a row was drawn names a *different item* a tick later, and a raw address
can name a freed block. This is the item-shaped version of the "nothing is written to an address
that has not just re-validated" rule, and the harness pins it with a check that writes to an item
that is not in the pack and expects a refusal. Do not add a path that writes to an item address
without going through it.

**Equipped state is read, never written.** There is no flag on an item: an item is equipped when its
pointer appears in one of two fourteen-slot arrays at `record + 0x334` and `+0x36C`. Which slot takes
which kind of item was never established, so the trainer displays equipment and stops there — and
`ReplaceItem` refuses an equipped item outright, because retyping in place would leave a body slot
holding something the game never put there. If you map the slot numbering, that is the thing to fix
first.

**The item-type catalog is a heap sweep, not an address.** `ItemCatalog` finds all ~1,080 types by
searching for the engine back-pointer every type carries and then validating what follows it. It
costs about 270 ms, so it runs once on attach and on the explicit Rescan button — never on the 250 ms
refresh. The four validation checks in `ItemTypeReader` are what make a sweep safe; the harness
plants a decoy heap block that differs from a real type only in its vtable so none of them can
quietly stop mattering.

**Max health and max mana do not exist.** The engine derives them every frame from Endurance,
Intelligence and level. There is nothing to write and no offset to find — this was confirmed, not
assumed (a session showing `72/72` and `125/165` contains neither maximum). Do not add a "set max
HP".

**The game does not re-clamp a value written from outside.** The doubling rule (a skill's base may
not exceed twice its governing attribute) is enforced when *points are spent*, not on the array.
Values of 47 and 100 against a cap of 46 were written, redrawn and survived a tab switch. `MaxSkills`
therefore treats the cap as a target, and manual edits are not clamped to it.

**`TheQuest.exe` sets DYNAMICBASE.** Nothing may assume `0x00400000`. The observed base in one
session was `0x00260000`.

**The trainer must never attach to itself.** Its process name, `TheQuestTrainer`, contains the hint
substring `quest`. `ProcessPicker` excludes the own process outright, ranks exact matches above
hints, and refuses to auto-select a hint-only match.

## What is deliberately absent

Do not add these without a very good reason, and update the README and the UI copy if you do:

- **Equipping and unequipping.** See the rule above: the slot numbering is unmapped, and a raw
  pointer write would bypass the model and paperdoll updates the game does around it.
- **Adding an item.** Allocation. Replace one instead.
- **Editing enchantments.** Traced only as far as a wand's charge ceiling.
- **Teleporting.** The map/coordinate fields were not located, and the game already ships Mark and
  Recall.
- **Save editing.** `docs/ReverseEngineering.md` §10 decodes the container and the character record's
  field order, which is enough to be interesting and nowhere near enough to be safe. It is a
  different tool.
- **A max-health/max-mana control.** See above.

## Testing

```powershell
.\Run.ps1 -Test -NoRun          # 363 checks, no game, no copyrighted files
```

`test/FormatCheck/Fakes.cs` builds a synthetic 32-bit address space with the same three-section
geometry as the real image, an engine object, a live record and the prototype beside it. The
interesting cases are already there to copy from: a relocated module, a stale slot, an empty slot, a
build whose `.data` does not cover the slot, a vtable pointing at writable memory, two live-looking
records, an unreadable page, and a `std::string` whose heap buffer has gone away.

`ItemHeap` in the same file lays out item types, their strings and item objects at the same strides
the real heap uses, and `FakeGame.BuildGameWithItems` assembles a pack covering every shape the
reader has to handle. Keep the strings reached by *pointer* rather than inlined into the type — that
is what makes the sweep's string checks real.

Extend the fixture rather than weakening a check.

**A check added for a fix must fail against the code before the fix.** It is easy to write one that
passes either way and proves nothing: the control-character name checks do that, which is why the
boundary cases around them (`0x1F` and `0x7F` rejected, `0x20`, `0x9F` and `ÿ` accepted) exist —
those are the ones that fail if `StdString.IsControl` is narrowed back to printable ASCII. Verify it
the cheap way: revert the production change, rerun, confirm the new check fails, then restore. Touch
the file afterwards — restoring a backup carries its old timestamp and MSBuild will skip the
rebuild, so you get the old binary's answer and think you are testing the new one. The same applies
to the freeze re-latch, which is pinned by an explicit counterfactual check that the tick *does*
undo an edit when the latch is not moved.

## Reverse-engineering workspace

`.docs/` and `.data/` are git-ignored (`.*/` in the root `.gitignore`) — RAM dumps, Ghidra projects
and probe scripts live there and are never committed. The Ghidra project was built outside the repo
(`C:\GhidraWork\`), because Ghidra refuses a project path containing a dot-prefixed element, and the
exe was copied to a plain path first. `docs/ReverseEngineering.md` §13 has the exact commands and the
addresses worth starting from.

One practical note that cost time: Ghidra compiles a whole script *directory* into one OSGi bundle,
so a single script that fails to compile makes every other script in that directory fail with
`ClassNotFoundException` until it is removed. Keep one working script per directory.
