# Dark Designs I: Grelminar's Staff — Live Trainer

A WPF (.NET 8) trainer for the 1990 DOS RPG **Dark Designs I: Grelminar's Staff** by John Carmack
(published by Softdisk / Big Blue Disk). It attaches to the running game (inside DOSBox /
DOSBox-X), locates the character roster in the emulated memory automatically — no manual searching
like Cheat Engine — and lets you edit every character live: name, class, level, the five attributes
(STR/DEX/CON/INT/PIE), Body (HP), Magic (MP), experience, gold, status, the **ten carried pack
slots**, and the **four readied equipment slots**, with per-vital **freeze** toggles and one-click
**max** actions, both per-character and party-wide.

It additionally includes a **character-creation roller** that automates the town (C)reate screen's
re-roll, an **offline save editor** for `DDCHARS.DAT` (the character file) that edits inventory too,
and a **References** tab listing all 16 spells, the 41 obtainable items, and 43 monsters from the game.

> Single-player cheat tool for your own game. Nothing here touches other machines or online services.

The character record layout was recovered by reverse-engineering the LZEXE-compressed `DARKDES.EXE`
and a sample `DDCHARS.DAT`. See [Reverse Engineering](docs/ReverseEngineering.md) for the full
analysis and [Strategy Guide](docs/StrategyGuide.md) for a complete play guide with controls,
spells, items, monsters, and walkthrough.

---

## Quick start

1. **Launch Dark Designs I** in DOSBox/DOSBox-X and play past the title screen (the roster only
   lives in memory once characters are loaded).
2. **Build & run the trainer:**
   ```powershell
   .\Run.ps1
   ```
   This builds Release and launches `DD1Trainer.exe`, which requests administrator rights via UAC —
   reading/writing another process's memory needs them, especially if the emulator is elevated.
3. **Attach:** pick the emulator process from the dropdown (DOSBox/ScummVM/etc. are auto-sorted to
   the top) and click **Attach**. It scans memory and lists the party automatically.
4. **Edit:** select a character on the left, then change any field on the right. Edits are written
   to the game *immediately* (they take effect when the game next reads the field — e.g. opening
   the character screen in-game).

If the scan finds nothing, make sure a party is actually loaded, then click **Re-scan**.

---

## What it can edit

The trainer decodes the full **72-byte** Dark Designs I character record:

- **Identity** — name (12-char ASCII), class (Fighter/Priest/Wizard), level (uint16 LE).
- **Attributes** — Strength, Dexterity, Constitution, Intelligence, Piety (uint16 LE).
- **Vitals** — Body current/max (HP), Magic current/max (MP).
- **Progression** — Experience and next-level threshold (uint32 LE), Gold (uint16 LE).
- **Status** — fine, KO, STUNED, STONE, DEAD.
- **Readied equipment** — right hand, left hand, armor, ring.
- **Carried pack** — the ten slots the game's item screen lists as `A`–`J`.

### Inventory and equipment

Each of the fourteen item bytes is a dropdown of the game's own item table, so you pick
*Hell Dagger* rather than typing `16`. Each row shows the item's type, damage or protection, and
which classes may use it; the readied slots flag anything the game itself would reject with
*"Wrong type!"* (a two-handed sword in the left hand, say). **Empty Pack** clears all ten carried
slots at once.

Each carried slot also has a **Duplicate** button, which copies that item into the first free slot
— the practical answer to Dark Designs destroying most items when you use them (see below).

Two things worth knowing:

- Buying isn't simulated — the item just appears in the slot, and no gold is deducted. Ready it
  in-game via `(I)tems → (R)eady` before it counts in combat.
- The game keeps a separate working copy of each *party* member and copies it back over the roster
  when it saves, so the trainer writes both. The character sheet says which case you're in.
- Those copies live in slots the game reuses. If you reform the party in-game, the trainer notices
  on its next poll and drops the copy rather than writing into whoever now owns that slot; if a
  roster slot itself changes hands it suppresses writes entirely and asks you to Re-scan.

### Items don't have charges — they have odds

There is no charge counter anywhere in Dark Designs. A pack slot is one byte holding an item id,
and nothing else. What actually happens on `(U)se` is that the game applies the item's effect,
rolls `random(256)`, and **destroys the item** unless a per-item *potency* value beats the roll:

| Item | Potency | Survives a use |
|---|---|---|
| Cureall Potion | 255 | 99.6% |
| Recall Scroll | 250 | 97.7% |
| Extra Healing | 245 | 95.7% |
| Healing Potion | 128 | 50.0% |
| Medusa Skull | 50 | 19.5% |
| Wand of Evil | 29 | 11.3% |
| Paralyze Wand | 10 | 3.9% |
| Keys 1–3 | 0 | never |

So a Healing Potion is a coin-flip each time, and a Paralyze Wand is gone within a couple of zaps.
The same roll decides whether a **magic weapon's** special effect fires on a hit (Gaze 250,
Trident of Pain and Active Axe 200, Old Dark Sword 80, Holy Sword 77, … Boom Blade 25); ordinary
gear has potency 0 and never rolls.

Because potency lives in the game's item table rather than on the item you're holding, there is
nothing per-item to recharge. Two toolbar toggles pin the roll instead:

- **Items never break** — usable items survive every use.
- **Magic weapons always trigger** — special effects fire on every hit.

Both patch the game's item table, so they apply to every character and are never written to
`DDCHARS.DAT`. By default they are **restored when you detach**, so the game is never left altered
with nothing attached to undo it. Tick **Keep on detach** to leave them in place instead — they
then last until the game exits, and the toggles stay ticked so re-attaching and unticking one puts
the original values back.

### Freeze toggles

The toolbar has party-wide **Freeze Body**, **Freeze Magic**, and **Freeze Status** checkboxes.
While a vital is frozen the poll loop re-pins it every tick, so it never drops in play.

### Quick actions

- **Party-wide** (toolbar): Heal Party, Max Attributes, Max Money, Max Everything.
- **Per-character** (below the character sheet): Full Heal, Max Attributes, Max Money, Max Everything.

"Max" targets follow the game's *own* built-in max-character routine: attributes 99, Body/Magic max
99, level 30, and 999,999 into the **next-level threshold** (the routine writes that to `NEXT`, not
to XP — so experience is left alone and the character doesn't immediately level past the game's own
cap). Gold goes to the uint16 ceiling of 65535 rather than the routine's 10,000.

Every one of these clamps *upward*: a character who already exceeds a target keeps what they have,
so "Max" can never take anything away.

---

## Rolling a character (the 🎲 Create tab)

The town's `(C)reate a character` screen rolls five values and lets you place them on
STR/DEX/CON/INT/PIE in any order, or press **R** for a new set. The Create tab automates that loop:
it taps R for you, reads each fresh roll straight out of the game's memory, and stops when the five
values can be arranged to meet the minimums you set.

1. Open the create screen in the game, type the five numbers it shows into **Capture the current
   roll** (order doesn't matter — the trainer matches the set), and click **Lock onto roll**.
2. Set a **minimum** on the attributes you care about. Because you arrange the values yourself, the
   roller stops as soon as *some* arrangement clears every minimum — and the **Arranged** column
   then tells you which value to put where (e.g. `Strength ← #2 (18)`). Ask for more than 18 and it
   says so rather than quietly lowering your target. The boxes are locked while a roll is running,
   so the arrangement shown always matches the target the roller is actually testing for.
3. Click **Roll until target met**. When it hits, the game window comes forward with the winning
   roll on screen, ready to arrange.

The tab shows the **exact odds** of your target before you start, and tallies the rolls it sees by
rank (best / 2nd / … / worst) so you can tell whether a minimum is realistic.

### Or just set the roll

The pool is writable and the game honours it, so **Or just set the roll** writes five values
directly — useful for a set the dice would essentially never produce (all 18s is 1 in 9.8 million).
Values are clamped to 3–18, the game's *attribute* range — deliberately wider than the 10–18 its
dice actually roll, since writing something the dice couldn't produce is the whole point. The Party
tab's **Max Attributes** goes further still once the character exists. One quirk, confirmed live:
the row of numbers already painted on the create screen isn't repainted, so it keeps showing the old
roll — but the values the game hands out as you arrange the character *are* the ones written.

Written rolls are deliberately left out of the Statistics panel: that panel reports what the game's
dice do, and a roll you wrote yourself is not evidence about that.

### The dice

Each rolled value is `10 + random(5) + random(5)` — a symmetric 10–18 spread with a mean of 14 —
measured from 2,000 values read out of the running game (chi-square *p* ≈ 0.66 against that model).
See [Reverse Engineering §5](docs/ReverseEngineering.md#5-character-creation-the-rolled-stat-pool).

---

### Save editor

The **Save Editor** tab edits `DDCHARS.DAT` offline (no game running required), including each
character's readied equipment and carried pack. The file is 1,224 bytes = 144-byte header +
15 × 72-byte records, with no checksum. A one-shot `.bak` backup is taken before the first write.
The "Max All & Save" button maxes every occupied character in one click.

Edit the save with the game closed: Dark Designs rewrites `DDCHARS.DAT` from memory when you
`(Q)uit and save`, which would discard offline edits made while it was running.

---

## How it finds the party

The roster's live address changes every DOSBox session, so the trainer never hard-codes it. It uses
a **dual-strategy locator** (`Memory/RosterLocator.cs`):

1. **String anchor** — the 34-byte title string `"Dark Designs I : Grelminar's Staff"` lives in the
   game's data segment as plain ASCII and is unique in DOSBox guest RAM. The locator finds it, then
   searches a 256 KB window forward for the 15-record character pattern. Fast (~50 ms).
2. **Structural scan** — fallback that scans all readable memory for a contiguous block of 72-byte
   records matching the character pattern (occupied slots validated, empty slots all-zero, packed
   from slot 0). Slower (~2 s) but build-independent.

Having found the roster, the locator sweeps ~10 KB of the surrounding data segment for the game's
**party working copies** — matched on name and class, never on a fixed offset — so live edits are
applied to both the roster and the copy the game is actually playing out of.

The freshly-rolled stats on the create screen are **not** a roster record — there is no name, class
or level until you finish arranging them — so neither strategy can see them.
`Memory/CreationScanner.cs` finds those separately, by signature-scanning for the five numbers you
type in (matched as a set, so the order you type them doesn't matter). Against the running game a
captured roll resolved to exactly one address in the whole emulator process; the roller still
narrows any ambiguity by re-rolling and keeping the candidate that changes.

---

## Verified against the real game

The record layout was recovered by **disassembling** the unpacked EXE and cross-checked against a
sample `DDCHARS.DAT` (one character, "CHRISTOPHER", Fighter L1). The game multiplies a character
index by `0x48` (72) in all ~300 places it touches a record and by 54 in none; its character-sheet
printer, its rest/heal code and its own built-in max-character routine between them pin every field.

It was then confirmed **against the running game**: the party working copy turned up at exactly the
`0xF3C` delta the disassembly predicts, and items written into pack slots A–D came back out of the
game's own item screen as `LONG SWORD`, `SHIELD`, `LEATHER ARMOR`, `HEALING POTION` — the game
resolving those ids to the names this trainer claims. See
[Reverse Engineering §4.7](docs/ReverseEngineering.md).

The parser is regression-tested:

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
# or:
dotnet run --project test/FormatCheck
```

`FormatCheck` asserts format constants, record decode/encode, name round-trip/truncation, empty
slot detection, `LooksLikeRecord` validation, save-file round-trip with `.bak` verification,
multi-character saves, and reference table counts — and exits 0 (pass) or 1 (fail). When the
sample `DDCHARS.DAT` is present it also asserts the empirically-confirmed values.

Inventory has its own coverage: pack slots land on the right bytes and end exactly on the record
boundary, ids clamp to the game's 0–63 range, a full pack refuses an eleventh item, readied slots
sit at their own offsets without disturbing the pack, item ids match their table index, and
`CanReady` reproduces the game's own "Wrong type!" rule for all four slots. Duplicating is checked
end to end (empty slots can't be duplicated, copies land in the first hole, repeated duplication
fills the pack and then stops), as are the potency values and the item-table geometry the
never-break patch indexes by.

For the creation roller it additionally checks the pool's encode/decode, the arrangement rule
(including that it depends on the set and not the order), the shortfall ranking, the roll
signature scan, and the "set the roll" parsing — and cross-checks the exact odds model against
brute force over all 59,049 possible rolls, so a mistake in either the combinatorics or the
arrangement rule fails the build.

---

## Project layout

```
src/DarkDesigns1Trainer/
  Game/        CharacterFormat.cs   the validated 72-byte offset table, class/status constants, lookup tables
               CharacterRecord.cs  typed, mutable view over a 72-byte buffer (LE accessors, name, attributes, pack)
               CreationFormat.cs   the create screen's five-value rolled pool: layout, dice, arrangement rule
               RollOdds.cs         exact odds of a roll clearing a target, from the measured dice
               RollTally.cs        running per-rank / total statistics over a roller session
               AttributeBook.cs    what each of the five attributes does (roller tooltips)
               SpellBook.cs        8 wizard + 8 priest spells with gold costs
               ItemBook.cs         all 64 item ids (41 obtainable) with type, damage/protection, price, class mask
               MonsterBook.cs      43 monsters from Kobold to Chaos Avatar
               GameFacts.cs        game metadata, anchor string, validator strings
               SaveFile.cs         offline DDCHARS.DAT reader/writer with .bak backup
  Memory/      RosterLocator.cs    dual-strategy locator (string anchor + structural scan) + party copies
               CreationScanner.cs  finds/reads/writes the create screen's rolled stat pool
               ItemTableLocator.cs finds the 64-entry item table by content; patches item potency
               (shared)            ProcessMemory / MemoryRegion — from GameTrainers.Common.Memory
  ViewModels/  MainViewModel, CharacterViewModel, CharacterRollerViewModel, NamedValueViewModel,
               ItemSlotViewModel, IItemPack, ReferenceViewModel, ICharacterHost
  App.xaml, MainWindow.xaml         the WPF UI (Party / Create / Save Editor / References tabs)
test/FormatCheck/                   headless verification harness
docs/                               reverse-engineering notes and strategy guide
.docs/                              RE working notes (git-ignored)
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer come from
the shared `GameTrainers.Common` library rather than being duplicated here.

---

## Notes & caveats

- Tested logic: the record parser, save-file round-trip, reference tables, and the whole creation
  roller (arrangement rule, odds model, signature scan) are verified by `FormatCheck`. The live
  attach/scan path needs the game running to exercise.
- The Create tab drives the game by sending keystrokes to its window, so the emulator window comes
  to the front for each re-roll. Stop the roller before using the machine for anything else.
- The 144-byte `DDCHARS.DAT` header is only partially decoded and is round-tripped without
  interpretation; only the character records are exposed for editing.
- The status field encoding (KO/STUNED/STONE/DEAD) follows the game's own 5-entry status string
  table and its `status == 5` death check, but has not been observed on a character in those states.
- Item ids 41–59 (monster hides, claws and bites) are real, writable ids but aren't player gear;
  they're kept out of the dropdowns and only shown if the game already put one in a slot.
- The item screen is painted when you open it and is **not** repainted when the bytes change
  underneath it — back out and re-enter it to see an edit land. (The create screen's roll row
  behaves the same way.)
- Writing has been confirmed live for the pack bytes only; the other fields are pinned by the
  disassembly and by the game's party status line, but haven't been round-tripped individually
  through the game's own display.
- Map files (`DDMAP1–5.DAT`) are not decoded or edited by the trainer.
- Edits take effect the next time the game reads the field (e.g. opening the character screen).
- Requires the **.NET 8 SDK** to build and **Windows** (WPF + memory APIs).
