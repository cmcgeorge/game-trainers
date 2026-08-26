# Wasteland Remastered Trainer — Agent Guidelines

This is a **live-memory trainer** for *Wasteland Remastered* (inXile, 2020 Steam remaster of the
1988 Interplay RPG). It is a C# WPF application targeting `net8.0-windows` (x64) that attaches to
the running `Wasteland Remastered.exe` process and reads/writes game state live — character
editing with tracked edits, freeze toggles, editable skills and inventory, and quick-action buttons.

**The character offsets here are not guesses.** The remaster ships its full IL2CPP metadata, so the
`Player` layout was read out of `global-metadata.dat` plus the `Il2CppMetadataRegistration`
field-offset table in `GameAssembly.dll`. If you are about to add a constant, read it out of the
metadata rather than inferring it — `.data/find_field_offsets.py` is the script that produced every
offset in `CharacterFormat.cs`, and `.data/README.md` explains how to re-run it.

**Nothing in this trainer has been watched working against a live game.** Every memory path is
exercised against a synthetic IL2CPP heap in the harness, and every offset traces to the metadata,
but no address has been observed changing in a running process. Keep that caveat in the README, and
do not quietly upgrade "extracted from metadata" into "confirmed in play".

## Project Structure

```
WastelandRemasteredTrainer/
├── AGENTS.md                        ← you are here
├── README.md                        ← user-facing readme
├── Run.ps1                          ← build + launch script
├── WastelandRemasteredTrainer.sln   ← solution (trainer + harness + Common)
├── .data/                           ← git-ignored RE workspace (see its README)
├── src/WastelandRemasteredTrainer/
│   ├── Game/
│   │   ├── GameFacts.cs             ← process/module/type names, limits, namespace
│   │   ├── Il2Cpp.cs                ← IL2CPP layout + Read*/TryRead* memory helpers
│   │   ├── Il2CppClassLocator.cs    ← PE-aware data-section sweep for Il2CppClass
│   │   ├── GameLocator.cs           ← Party.m_instance → players → Player objects
│   │   ├── CharacterFormat.cs       ← field offsets, quantity packing, shape check
│   │   ├── CharacterRecord.cs       ← typed live view over one Player
│   │   ├── PartyState.cs            ← UNVERIFIED read-only map/position/clock
│   │   ├── SkillBook.cs             ← 35 skills (shared table with WastelandTrainer)
│   │   ├── AttributeBook.cs         ← 7 attributes
│   │   └── ItemBook.cs              ← 91 items (shared table with WastelandTrainer)
│   ├── Memory/IMemorySource.cs      ← abstraction + live and fake implementations
│   ├── ViewModels/
│   │   ├── MainViewModel.cs         ← attach/locate/poll/party-wide commands
│   │   ├── CharacterViewModel.cs    ← per-character binding + edit tracking
│   │   ├── RowViewModels.cs         ← editable skill and inventory rows
│   │   └── ICharacterHost.cs        ← host callbacks
│   └── App.xaml, MainWindow.xaml    ← the WPF UI
└── test/FormatCheck/Program.cs      ← 422 headless checks, no game needed
```

## Architecture

### Locating the game state

Unlike `../BardsTaleTrilogyTrainer/`, **no metadata-usage slot RVAs are known for this build**, so
there is no "read the class pointer from a known RVA" fast path. The locator instead parses the PE
section table of `GameAssembly.dll` and sweeps only its **readable, non-executable sections** for a
pointer that resolves to an `Il2CppClass` named `"Party"`. Sweeping `.text` as well would burn the
probe budget on instruction bytes that can only ever be false candidates.

Only `Party` is required. The `Player` class pointer is read off a party member's own object header
rather than swept for, so the sweep can stop as soon as it finds `Party`. `PartyManager` and
`Wasteland` are collected opportunistically for the (unverified) party-position read.

### IL2CPP object model

64-bit IL2CPP: object header 0x10 (`Il2CppClass*` + monitor), array elements at +0x20 with
`max_length` at +0x18, `Il2CppString` length +0x10 / chars +0x14, `Il2CppClass` name +0x10 /
namespace +0x18 / `static_fields` +0xB8, `List<T>` `_items` +0x10 / `_size` +0x18.

## Key Design Decisions

1. **Identity, never plausibility, for objects already known to be Players.** `LooksLikePlayer` is
   a *shape* test for the structural fallback only. Applying it to entries of `Party.players` can
   only lose real characters — a dying ranger at negative CON is exactly who someone opens a
   trainer to rescue. Use `Il2Cpp.IsInstanceOf` there. This was a real bug; the harness has a
   regression test for it (`CheckTypedPartyWalk`).

2. **Edits are tracked, not snapshotted.** `CharacterViewModel` records which fields the user
   touched; `Write()` writes only those. A whole-record write-back would roll the game's own
   experience, money and level progression back to whatever they were when the party was located.
   `Refresh` correspondingly skips dirty fields so the live poll cannot clobber half-typed input.

3. **`TryRead*`/`TrySet*` wherever a result matters.** Every plain `Read*` helper returns 0 on
   failure, indistinguishable from a genuine zero, and every plain property setter discards
   whether the write landed. A freeze built on the former will pin a character's money to zero the
   first time a page is briefly unreadable; a `Write()` built on the latter reports success, drops
   the edit, and lets the next refresh quietly restore the old value. Freezes, the party walk and
   `Write()` all use the reporting forms, and a field that fails to write stays pending.

3a. **An empty roster is an answer, not a failure.** `ReadPlayers` returns a tri-state:
   `Unreachable` falls back to the structural scan, `EmptyRoster` reports "load a game" and scans
   nothing. Collapsing the two means clicking Locate at the title screen sweeps gigabytes and can
   hand back character-creation objects that edit nothing real.

4. **A failed string read must not look like the empty namespace.** Game types live in the global
   namespace, so `""` is the *expected* namespace value. `TryReadNativeString` reports failure
   separately; without that, the namespace check is vacuous and validation rests on the name alone.

5. **Never crawl past an unreadable read.** A failed `Read` says nothing about where readable memory
   resumes. Both sweeps skip a page (0x1000). Advancing 8 bytes instead turns one unreadable
   megabyte into 131,072 more failing reads — with a disposed handle that is minutes of a pegged
   thread.

6. **Locate is cancellable and its results are staleness-checked.** The scan runs on a background
   task with a `CancellationTokenSource` owned by `MainViewModel`; `Detach` cancels it. A completing
   scan checks `ReferenceEquals(mem, _mem)` *before* touching any session state, so a stale scan
   cannot clear a newer one's flags.

7. **The poll refreshes scalars only.** `RefreshScalars()` on the timer, full `Refresh()` on demand.
   Rebuilding the skill and item rows several times a second would close any drop-down the user has
   open.

8. **Bit 7 of an inventory quantity byte is the jam flag.** Mask on read, clamp on write. The raw
   byte is kept on `ItemEntry.Quantity` so nothing is lost.

9. **35 skills, 30 slots.** "Learn All Skills" physically cannot fit them all. It fills what it can
   in id order and *reports* what did not fit rather than silently dropping it.

10. **The party-position block is unverified and therefore read-only.** A wrong offset that shows a
    nonsense number is harmless; a wrong offset that gets written to is not. The DOS sibling found
    its equivalent header to be a write-only shadow — do not add live teleport without confirming
    the remaster differs.

## Build & Test

```powershell
.\Run.ps1 -Test -NoRun          # build + run the 350 verification checks
.\Run.ps1                       # build Release and launch (UAC prompt)
.\Run.ps1 -Clean -Configuration Debug
```

The harness needs no game installed — it drives every memory path against `FakeMemorySource`,
including a synthetic IL2CPP image (PE headers, data section, class structures, `Party` singleton,
`List<Player>`) that exercises the full primary locate path.

## Testing

Add a check for anything you change. In particular:

- **A new offset** gets an assertion in `CheckCharacterFormat` pinning its value.
- **A new packed-array operation** gets coverage in `CheckPackedSkills`/`CheckPackedItems`,
  including the short-array case — reads and writes must agree on the same bound.
- **A new view-model behaviour** goes in `CheckCharacterViewModel`, and anything that could lose a
  player's data gets an entry in `CheckRegressions`. `FormatCheck` sets `UseWPF` so `RelayCommand`
  resolves and so `CheckXamlLoads` can construct the real `MainWindow` on an STA thread — that is
  the only check that executes the compiled XAML, and it is what catches a bad `StaticResource` or
  `x:Static` before launch. `CharacterViewModel` deliberately does not touch `Application.Current`
  so it stays testable; `MainViewModel` does, and is not directly covered.
- **A new editable property** must be added to `CharacterViewModel.EditableFieldNames` and to the
  `WriteField` switch. `CheckWriteRoutesEveryField` asserts the two agree and that every field
  lands in the right place, so a mis-pasted case cannot slip through.
- `FakeMemorySource` is all-or-nothing per mapped page, matching `ProcessMemory.Read`, which returns
  0 rather than a partial count on failure. It cannot serve a read spanning two `Map` calls — put
  related fixture objects in one mapping.

Keep the check count in `README.md` in step with reality.

## Dependencies

`../GameTrainers.Common/` supplies `ProcessMemory` (Read/Write/EnumerateRegions over a
`SafeProcessHandle`, returning 0/false rather than throwing after dispose) and the MVVM base types.
Do not duplicate either here.

## Important Notes

- Game types are in the **global namespace** (`""`), not a named one.
- The `SkillBook` and `ItemBook` tables are byte-for-byte identical to `../WastelandTrainer/`'s
  `SkillBook.cs` and `ItemCatalog.cs`. Fix a data error in both, or neither.
- The assembly is named `WLRTrainer.exe`; `Run.ps1` and `app.manifest` both depend on that.
- `app.manifest` requests administrator rights — required for `ReadProcessMemory` against the game.
