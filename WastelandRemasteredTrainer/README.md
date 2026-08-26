# Wasteland Remastered Trainer

A live-memory trainer for **Wasteland Remastered** (inXile Entertainment, 2020 Steam remaster of
the 1988 Interplay / Electronic Arts post-apocalyptic RPG). Written in C# / WPF (.NET 8, x64).

This is the **native Win32** counterpart to `../WastelandTrainer/` (which targets the original
DOS game under DOSBox). The remaster is a **Unity IL2CPP 64-bit** build (Unity 2018.4.0f1), so the
trainer attaches straight to `Wasteland Remastered.exe` — no emulator — and walks the IL2CPP object
graph to find the party the same way the game's own code does.

## Features

### Characters

- **Auto-locate**: attach to `Wasteland Remastered.exe` and find the party through the IL2CPP class
  pointer the game itself uses, with a structural shape scan behind it as a fallback. The scan runs
  in the background, reports progress, and can be cancelled.
- **Character editing**: all seven attributes (Strength, IQ, Luck, Speed, Agility, Dexterity,
  Charisma), Constitution (current, max and unconscious threshold), money, experience, level, skill
  points, armor class, disease, sex, nationality, and equipped weapon and armor (picked by name)
- **Skills**: all 35 skills, **editable in place** — set any skill's level, or "Learn All Skills" to
  add every skill the character is missing
- **Inventory**: 30 packed slots, **editable in place** — change an item by name, set its ammo,
  toggle its jammed flag, remove it, or add a new item to a free slot
- **Freeze toggles**: Freeze CON (re-pins current health to the game's live maximum), Freeze Money
  and Freeze Ammo, all per-character
- **Quick actions**: Full Heal, Max Attributes, Max Skills, Max Money, Max Ammo, Clear Jams,
  Max Everything — per character, and Full Heal / Max Attributes / Max Skills / Max Money /
  Max Everything party-wide

### Editing model

Edits are **tracked, not snapshotted**. Typing in a field marks it as pending; **Write** commits
only the fields you actually changed. Everything else — the experience, money and levels the game
has awarded since you located the party — is left exactly as it is. **Revert** discards pending
edits. The live view re-reads the game every 400 ms and updates every field *except* the ones you
are mid-edit on, so the sheet tracks the game without fighting your typing.

Skill and inventory rows are different: each is a single self-contained value with no snapshot
behind it, so those write through the moment you change them.

### Reference data

- **35 skills** with minimum-IQ requirements and usage descriptions (identical to the original
  `WastelandTrainer`'s table — verified byte-for-byte)
- **7 attributes** with role and in-play descriptions
- **91 items** with categories and weapon damage lines (likewise identical to `WastelandTrainer`)

## Requirements

- Windows 10/11 (x64)
- .NET 8.0 SDK
- Wasteland Remastered installed and running via Steam
- Administrator rights (the trainer reads/writes the game's process memory)

## Quick Start

1. Launch **Wasteland Remastered** via Steam
2. Load or start a party (you need to be in-game, not on the main menu)
3. Run `.\Run.ps1` in this folder (a UAC prompt will appear)
4. Click **Attach** to connect to the game process
5. Click **Locate** to find the party in memory
6. Select a ranger on the left, edit any field on the right, then click **Write** to commit those
   edits to the game. Skill levels, inventory rows, the freeze toggles and the quick-action buttons
   all take effect immediately and need no Write.

## Building

```powershell
.\Run.ps1                    # build Release + launch
.\Run.ps1 -Configuration Debug
.\Run.ps1 -Clean             # clean bin/obj first
.\Run.ps1 -Test -NoRun       # run verification harness only
.\Run.ps1 -Publish           # single self-contained win-x64 exe
```

## How It Works

The remaster ships its full IL2CPP metadata (`global-metadata.dat`, 5.6 MB), so every character
offset the trainer uses was read out of the metadata's string table, type definitions, and the
`Il2CppMetadataRegistration` field-offset table in `GameAssembly.dll` — no address was guessed. The
extraction scripts live in `.data/` (git-ignored); see `.data/README.md`.

### Locator

To find the party the trainer follows the same route the game's own code does:

1. Find `GameAssembly.dll` in the process.
2. **Sweep the module's data sections** for a pointer that resolves to an `Il2CppClass` named
   `"Party"` in the global namespace. The PE section table is parsed first so only readable,
   non-executable sections are swept — the class-pointer slots live in `.data`/`.rdata`, and the
   tens of megabytes of executable code can only produce false candidates. Each candidate is
   validated by reading the class's own name *and* namespace before it is trusted, with reads that
   distinguish "empty namespace" from "unreadable".
3. Follow `Party`'s static `m_instance` to the party object, then read `players` — a `List<Player>`
   — and walk its backing array.

Entries in that list are confirmed by **identity, not plausibility**: the `Player` class pointer is
read off a party member's own object header, and every other entry must carry the same one. A
plausibility filter would be wrong here — a ranger at negative CON is still a ranger, and dropping
them silently would hide exactly the characters you opened a trainer to rescue. Entries that
genuinely are not `Player` objects are counted and reported in the status line.

If the party cannot be reached at all, the trainer falls back to **scanning committed memory** for
objects shaped like a `Player`, and confirms each hit against the `Player` class pointer whenever
one is known.

### IL2CPP object model

Wasteland Remastered is a 64-bit IL2CPP build, so:
- Every managed object starts with a 16-byte header (`Il2CppClass*` + monitor); the first instance
  field is at +0x10.
- Managed arrays have a 32-byte header; element 0 is at +0x20.
- `Il2CppString` has a length at +0x10 and UTF-16 characters at +0x14.
- `Il2CppClass` has a `const char* name` at +0x10, `const char* namespaze` at +0x18, and
  `static_fields` at +0xB8.
- `List<T>` has its backing array at +0x10 and size at +0x18.

The `Player` object carries the same character model as the original Wasteland — seven
single-byte attributes (STR, IQ, LCK, SPD, AGL, DEX, CHR), packed `(skillId, level)` and
`(itemId, quantity)` byte arrays, 24-bit-style fields widened to int32 — but wrapped in IL2CPP
objects with the compiler-reordered field layout.

An inventory quantity byte is not a plain count: **bit 7 is the jammed-weapon flag** and the low
seven bits are the ammo/charge count. The trainer masks it on read (a jammed rifle holding 20 rounds
shows as 20, not 148) and clamps on write, so a large ammo number can never set the jam bit by
accident.

### Freeze and refresh

A 400 ms poll timer does two things: it re-applies the freezes (CON pinned to the game's live
maximum, money to the amount showing when you ticked the box, ammo topped up), and it re-reads the
character sheet so the display tracks the game. Freezes run on the timer thread — they only read and
write process memory — while the UI refresh is marshalled to the dispatcher. Skill and inventory
lists are *not* rebuilt on the poll; doing so several times a second would close any drop-down you
had open. They refresh when something changes them, or on **Refresh**.

### Party position

The status line also shows the party's map, position and clock when they can be read. That block is
reached through `Wasteland.m_instance.m_partyManager.m_saveData`, and unlike the character offsets
**this route is unverified** — so it is read-only and labelled "unconfirmed offsets" in the UI. The
DOS trainer found its equivalent position header to be a write-only shadow that the game never reads
back, so live teleport is deliberately not offered until someone confirms the remaster differs.

## Verification

```powershell
.\Run.ps1 -Test -NoRun     # builds, then runs the FormatCheck harness
```

`FormatCheck` runs **422 checks** with no game installed:
- **GameFacts** constants (process name, module, type names, limits, and the invariants they must
  satisfy — e.g. that `MaxAmmo` fits the 7-bit count field)
- **CharacterFormat** — every field offset the trainer defines, plus the quantity-byte pack/unpack
- **SkillBook** (35 skills, unique sequential ids, MinIq values)
- **AttributeBook** (7 attributes, index lookup, agreement with the record's attribute order)
- **ItemBook** (91 items, unique ids, all ids byte-sized, `IsAmmoItem` classification)
- **LooksLikePlayer** — positive and negative cases in both directions, including that a *dying*
  ranger (negative CON) and an already-maxed character both still pass
- **CharacterRecord** round-trip — read, write and clamp behaviour across the scalar fields,
  including the `Disease` clamp and the bounded attribute index
- **Packed skills** — read, update, add, free-slot counting, short arrays, and that "learn all"
  reports exactly the skills that do not fit in 30 slots
- **Packed inventory** — read with the jam bit masked, set/add/remove with gap-closing, ammo
  clamping, `MaxAmmo`, `ClearJams`
- **IL2CPP helpers** — ptr/i32/byte round-trips, the `TryRead*` failure reporting, array length,
  byte-array elements, `List<T>` count and element refs
- **Native string reads** — that a failed read is distinguishable from an empty string, and that
  `ClassMatches` rejects an unreadable namespace rather than treating it as the global one
- **Typed party walk** — the full primary path over a synthetic IL2CPP image: PE headers, a data
  section holding the class pointer, statics, the `Party` singleton, a `List<Player>`, and Player
  objects — asserting that a dying ranger is kept and a wrong-typed entry is rejected and counted
- **GameLocator structural scan** — empty memory returns null, a valid object is found, progress is
  reported, and a cancelled token stops the scan
- **PE-aware class sweep** — a data section is swept, an executable or non-readable one is not,
  and unparseable headers fall back to a full-module sweep
- **PartyStateReader** — the singleton chain is walked through either route, and every broken link
  yields null rather than a bogus reading
- **Write() field routing** — all 20 editable fields are committed at once with distinct values,
  so a mis-pasted case that writes the wrong field is caught
- **Regressions** — an empty roster does not trigger a memory scan; a failed write keeps the edit
  pending; choosing "(empty)" on an inventory row removes the item instead of orphaning the ones
  behind it; an out-of-range drop-down value is not silently rewritten; Full Heal supersedes a
  half-typed CON edit; money can be frozen at zero
- **XAML smoke test** — the real `MainWindow` is constructed on an STA thread, so a bad
  `StaticResource` or `x:Static` reference fails the harness instead of only failing at launch

## Limitations

- **Nothing here has been watched working in a live game.** Every character offset was read out of
  the game's own IL2CPP metadata and field-offset table, and the verification harness drives every
  memory path against a synthetic IL2CPP heap. But no address has been observed changing in a
  running process. Treat the first run as a test: save first.
- The **party position and clock are read through an unverified route** and are read-only. If the
  numbers look like nonsense, the offsets are wrong — that is what the "unconfirmed" label means.
- There is **no save editor** (the save format is IL2CPP-serialized binary, not documented).
- There is **no teleport** — see "Party position" above.
- The **NPC negotiation bytes** (`NPCCom`, `NPCTrade`, `NPCGreed`, `NPCRecChr`, …) are mapped in
  `CharacterFormat.cs` but their semantics are not known, so nothing edits them. Only the `NPC`
  flag itself is read, for the PC/NPC label.
- The trainer targets the **Steam remaster** (2020), not the original DOS game. For the original,
  use `../WastelandTrainer/`.

## Technical Details

- **Engine**: Unity 2018.4.0f1 with the IL2CPP scripting backend (64-bit `GameAssembly.dll`)
- **Process**: `Wasteland Remastered.exe`
- **Memory access**: `ReadProcessMemory` / `WriteProcessMemory` via `GameTrainers.Common`
- **Locator**: PE-aware data-section sweep for the `Party` `Il2CppClass` (primary), structural shape
  scan with class-pointer confirmation (fallback)
- **Namespace**: all game types are in the global namespace (empty string `""`)

## Project layout

```
src/WastelandRemasteredTrainer/
  Game/        Il2Cpp.cs                IL2CPP runtime layout; Read*/TryRead* memory helpers
               Il2CppClassLocator.cs    PE-aware data-section sweep for Il2CppClass by name
               GameLocator.cs           Party.m_instance → players list → Player objects
               CharacterFormat.cs       field offsets, quantity packing, LooksLikePlayer
               CharacterRecord.cs       typed live view (fields, packed arrays, quick actions)
               PartyState.cs            unverified read-only map/position/clock
               GameFacts.cs             process/module/type names, limits, namespace
               SkillBook.cs             35 skills (id → name, min-IQ)
               AttributeBook.cs         7 attributes (STR/IQ/LCK/SPD/AGL/DEX/CHR)
               ItemBook.cs              91 items with categories, IsAmmoItem
  Memory/      IMemorySource.cs         IMemorySource, ProcessMemorySource, FakeMemorySource
  ViewModels/  MainViewModel.cs         attach/locate/poll/freeze/party-wide commands
               CharacterViewModel.cs    per-character binding, edit tracking, quick actions
               RowViewModels.cs         editable skill and inventory rows
               ICharacterHost.cs        callback interface
  App.xaml, MainWindow.xaml             the WPF UI
test/FormatCheck/                       422 headless verification checks
.data/                                  git-ignored reverse-engineering workspace
```

MVVM plumbing (`ObservableObject`/`RelayCommand`) and the process-memory access layer come from the
shared `GameTrainers.Common` library rather than being duplicated here.

## Related

- `../WastelandTrainer/` — trainer for the original DOS Wasteland (DOSBox, 256-byte records, save
  editor with teleport)
- `../BardsTaleTrilogyTrainer/` — another Unity IL2CPP trainer, the architectural template
- `../GameTrainers.Common/` — shared memory access and MVVM libraries
