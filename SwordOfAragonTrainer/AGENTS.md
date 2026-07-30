# Sword of Aragon Trainer — Agent Guidelines

Trainer and save editor for **Sword of Aragon** (SSI, 1989 — MS-DOS, v1.0). Read
[docs/RE.md](docs/RE.md) before touching anything in `Game/` or `Memory/`; it is the evidence behind every
constant in this project and it labels each finding **Confirmed** or **Unconfirmed**.

## The single most important fact about this target

The game is compiled **QuickBASIC 3.0** linked against the **BRUN30** run-time module, which is *not* in the
executable image. The code stream is therefore a sequence of far calls into an absent module with inline argument
blocks, and **it does not disassemble** — a full Ghidra auto-analysis of `SWORD.EXE` recovered ~350 instructions
from a 40 KB image and no cross-references to any interesting string.

Consequences that shape all work here:

* **No variable addresses are statically recoverable.** Do not add a hard-coded live address. Ever.
* **The data is the way in.** `SWORD.EXE` carries the entire new-game database as QuickBASIC `DATA` text — unit,
  equipment, city and enemy tables in near-source form — and the save files are plain CSV plus a fixed-size
  record array.
* **Floats are Microsoft Binary Format, not IEEE 754.** Gold, income, upkeep and experience all go through
  `Game/Mbf.cs`. An IEEE float scan will never find them.

## Architecture

* `Game/` — the game-knowledge layer. Every reverse-engineered constant belongs here and nowhere else.
  * `RosterFormat.cs` holds the 100-byte record offsets; only offsets proven in RE.md §6.3 are named.
  * `UnitBook.cs` holds the price tables and the cost model.
  * `KingdomFile.cs` / `CityRecord.cs` / `CsvRow.cs` edit `ARAGON.HS?` one CSV **field** at a time.
  * `RosterFile.cs` / `RosterRecord.cs` edit `ARAGON.HR?` in place over one buffer.
* `Memory/` — `GameSignatures.cs` (four DGROUP anchor literals and their `DS:` offsets) and `DgroupLocator.cs`
  (locate `DS:0000` at run time — accepting a hit only when at least two of the three secondary anchors also line
  up — then search the 64 KiB segment).
* `ViewModels/` — hand-rolled MVVM over `GameTrainers.Common.Mvvm`.
* `test/FormatCheck/` — the headless harness (457 checks with the shipped saves present, 272 without).
  Keep it green.

## Rules for changes

1. **Round-trip everything you do not deliberately change.** Both file parsers are built so an untouched
   load/save cycle is byte-for-byte identical, which is asserted against all 15 shipped saves. Any change that
   breaks that is a bug, not a trade-off — it would destroy the fields whose meaning is still unproven.
2. **Never write an Unconfirmed offset or field.** Expose it read-only or not at all.
3. **Keep the two byte mirrors in step.** A roster record stores level at `0x32` *and* as a byte at `0x60`, and
   type at `0x14` *and* at `0x61`. `RosterRecord` always writes both; do not add a path that writes one.
4. **Recompute derived fields after an equipment or type change — but only the four whose formulas are proven.**
   Make/train/upkeep (`0x28`/`0x2A`/`0x2C`) and stacking size (`0x48`) are functions of type + equipment + the
   player's class; `RosterRecord.RecomputeDerived` does those, so call it. It deliberately leaves armour class
   (`0x40`/`0x42`), hand damage (`0x4C`), the hand bonus (`0x50`) and hits (`0x3E`) alone — their formulas are not
   in the Confirmed set, and the game refreshes them itself on the next in-game Equip/Train. Do not "improve"
   this by guessing at them. Note that a change to **slot 0's class** changes the discount every troop unit pays,
   so that path must call `RosterFile.RecomputeAllDerived()`, not just recompute slot 0.
5. **Clamp on write, and remember `Math.Clamp` passes NaN through.** Every setter clamps to a range the game
   accepts (tax 0–80, map 0–23, score ≤ 500, city gold ≤ 32,767 because the game reads it into an `INTEGER`)
   rather than trusting the UI. For `double` fields, guard with `double.IsFinite` as well — a NaN would otherwise
   reach the CSV as the literal text `NaN`.
6. **Back up before the first write.** `SaveBackup.EnsureFor` takes a one-shot `.bak` and returns the path only
   when it actually created one. It is a "state before the trainer first touched this file" snapshot, **not** a
   rolling undo — do not describe it as one, and do not make it overwrite.
7. **Do not patch game executables.** The copy protection is defeated by knowing the answers (they are in
   `ProtectionBook`), not by modifying `SWORD.EXE`. A code patch is not derivable here anyway (see RE.md §4.4).
8. **If you extend the cost model, re-validate it.** The current model reproduces make/train/upkeep for **623 of
   623** occupied records across the 15 shipped saves and 16 distinct (player class, unit type) pairs. The harness
   asserts both figures when the full corpus is present, so any change must keep them.
9. **A live-tab candidate carries its own width.** `ScanResultViewModel.Width` is the width the value was found
   at; pins and the poll loop must use it, never the Width combo box, or a 16-bit counter gets written as 32 bits
   and clobbers the variable next to it.

## Testing

```powershell
.\Run.ps1 -Test -NoRun                                   # default scratch game path
dotnet run --project test\FormatCheck -- "D:\path\to\SARAGON"   # a different game folder
```

The harness skips (does not fail) the real-save group when the copyrighted game files are absent, so it stays
green on a clean checkout. The GUI cannot be smoke-tested headlessly — it needs a desktop, and the Live tab needs
a running DOSBox.

## Things that are known-unknown

Do not present any of these as settled without new evidence:

* The DGROUP offsets of the game's *variables* (only its string literals are recoverable statically). The Live
  tab's anchor path is therefore **unverified against a running game**; say so in any user-facing text.
* `ARAGON.HS?` header line 1 and the two-line trailer; the city fields marked Unconfirmed in RE.md §6.2.
* Roster offsets `0x26`, `0x2E`, `0x30`, `0x36`, `0x44`, `0x4A`, `0x4E`, `0x52`, `0x54`–`0x5E`, `0x62`–`0x63`.
* The **formulas** behind armour class (`0x40`/`0x42`), hand damage (`0x4C`), the hand bonus (`0x50`) and hits
  (`0x3E`). What they mean is confirmed by behaviour; how the game computes them is not, so they are never written.
* The **Ranger** purchase discount. The Warrior and Knight rows of `UnitBook.Discount` are confirmed by the
  623-record corpus; no Ranger-player save is shipped, so that row rests on the rule book alone (RE.md §6.4a).
* The `*.BIN` sprite encoding (the copy-protection city crests live in `CITY.BIN`).
* The token dictionary for the compressed event-text files.

## Style

C# on `net8.0-windows`, x64, nullable enabled, file-scoped namespaces, `sealed` classes, 4-space indent,
`const` hex for offsets, XML `///` docs on public members. Match the surrounding files. No `.editorconfig` is
committed; the repo convention is to look like its neighbours.

Never commit game files, save files, or RAM dumps. Dot-prefixed directories (`.docs/`, `.data/`, `.game/`) are
git-ignored for exactly that reason.
