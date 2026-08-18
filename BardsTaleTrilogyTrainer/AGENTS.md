# Bard's Tale Trilogy Trainer — Agent Guidelines

This is a **live-memory trainer** for *The Bard's Tale Trilogy* (Krome Studios / inXile, 2018 Steam remaster of the classic Interplay trilogy). It is a C# WPF application targeting `net8.0-windows` (x64) that attaches to the running `TheBardsTaleTrilogy.exe` process and reads/writes game state live — with freeze toggles, "max" buttons, spell assignment (including ZZGO and NUKE), and item charge editing.

## Project Structure

```
BardsTaleTrilogyTrainer/
├── AGENTS.md                        ← you are here
├── README.md                        ← user-facing readme
├── Run.ps1                          ← build + launch script
├── BardsTaleTrilogyTrainer.sln     ← solution (trainer + tests + Common)
├── docs/
│   └── ReverseEngineering.md        ← RE notes, memory layout, methodology
├── src/BardsTaleTrilogyTrainer/
│   ├── BardsTaleTrilogyTrainer.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / MainWindow.xaml.cs
│   ├── app.manifest
│   ├── Game/
│   │   ├── GameFacts.cs             ← process names, module RVAs, constants
│   │   ├── CharacterFormat.cs        ← IL2CPP object layout, validation
│   │   ├── CharacterRecord.cs        ← typed mutable view over live bytes
│   │   ├── GameLocator.cs            ← pointer-chain + structural scan locator
│   │   ├── Spellbook.cs              ← all BT1-3 spells including ZZGO/NUKE
│   │   └── ItemBook.cs               ← 127-item catalogue + Garth's shop
│   ├── Memory/
│   │   └── IMemorySource.cs          ← ProcessMemorySource + FakeMemorySource
│   └── ViewModels/
│       ├── MainViewModel.cs          ← attach/locate/poll/freeze orchestration
│       └── CharacterViewModel.cs     ← per-character editing VM
└── test/FormatCheck/
    ├── FormatCheck.csproj
    └── Program.cs                    ← headless verification harness
```

## Architecture

The trainer follows the repo's **three-tier locator** pattern:

1. **Pointer chain** (primary): `GameAssembly.dll + 0xE40338` → global game state → `+0xB8` → party/economy object → character array → per-character fields. This is the one-click auto-locate path.
2. **Structural scan** (fallback): if the pointer chain fails (different build, ASLR drift), sweep committed memory for a window of six contiguous IL2CPP character objects matching `CharacterFormat.LooksLikeCharacter`.
3. **Value scanner** (last resort): `GameTrainers.Common.Memory.MemorySearcher` for Cheat-Engine-style scan/narrow/pin — the user manually finds gold/HP/XP.

### IL2CPP Object Model

The game is a **Unity IL2CPP** build (64-bit native, `GameAssembly.dll`), not a managed .NET assembly. IL2CPP objects have:

- An 8-byte class pointer (vtable-equivalent) at offset `0x00`
- Instance fields starting at offset `0x10` (after the object header)
- `String` objects with a length field at `+0x10` and UTF-16 characters at `+0x14`

All offsets in `CharacterFormat` are relative to the **character object base** (the start of the IL2CPP object, including its header). See `docs/ReverseEngineering.md` for the full layout and confidence markers.

### Confidence Markers

Because the game was not installed on the development machine, every offset carries a **[Confirmed]** (cross-checked against CE scripts or live gameplay reports) or **[Inferred]** (plausible from CE scripts but not independently verified) marker. These are surfaced in the UI via `KnownValues` so the user understands what is and isn't verified.

## Game-Knowledge Layer

- `GameFacts`: process name (`TheBardsTaleTrilogy.exe`), module name (`GameAssembly.dll`), RVAs, default paths
- `CharacterFormat`: IL2CPP character object layout — HP (+0x84), SP (+0x8C), XP (+0x50), gold (+0x68 at party level), attributes, level, name, spell bitfield, item slots
- `Spellbook`: 140+ spells across BT1/BT2/BT3 including **ZZGO** (Dream Spell) and **NUKE** (Götterdämmerung), Archmage/Chronomancer/Geomancer expansions
- `ItemBook`: 127 items from the original Bard's Tale catalogue with IDs, names, and categories; Garth's 22 basic shop items

## Key Design Decisions

- **No hardcoded addresses**: the locator derives everything from the module base + RVA, which works regardless of ASLR. The structural scan fallback handles build differences.
- **Write safety**: the locator validates the character object shape at locate time via `LooksLikeCharacter` (plausible XP, HP, SP, race, class, level, attributes, HP ≤ HPMax, SP ≤ SPMax, Max attributes in range). Writes go to the stored address without re-validation — a limitation acknowledged here, to be addressed when live verification is available.
- **Freeze via poll timer**: a 500ms `System.Threading.Timer` re-writes frozen values each tick, marshalled through the WPF `Dispatcher` to avoid cross-thread collection access.
- **Spell assignment**: sets the four spell-class level bytes (Conjurer/Magician/Sorcerer/Wizard) to the spell's level, which grants access to all spells of that class up to that level. `LearnAllClassSpells` sets all four classes to level 7. For "Any Magic User" spells (ZZGO, NUKE, GILL, DIVA), the trainer sets all class levels to 7 and informs the user that the special spell may require an additional flag we haven't located — the spell-knowledge bitfield offset is [Inferred] and its exact bit layout is unknown.
- **Item charges**: setting the charge byte to `0` makes the game treat the item as having infinite uses (a Unity engine quirk). `SetAllItemsInfinite` does this for all carried items. Array element access uses the correct IL2CPP array header size of `0x20` (klass + monitor + bounds + length), not the object header size of `0x10`.
- **Garth's shop**: the shop inventory offset has **not been confirmed** against a live game. The UI informs the user to use the value scanner or Il2CppDumper for this feature.

## Build & Test

```powershell
.\Run.ps1                    # build Release + launch (UAC prompt)
.\Run.ps1 -Test -NoRun       # run FormatCheck harness only
.\Run.ps1 -Configuration Debug
.\Run.ps1 -Clean
.\Run.ps1 -Publish           # single self-contained exe
```

## Testing

`test/FormatCheck` is a headless console harness (no GUI, no game required) that asserts:

- `GameFacts` constants (process name, module name, RVAs)
- `CharacterFormat` offsets and `LooksLikeCharacter` validation
- `Spellbook` completeness (ZZGO, NUKE, counts, code-to-spell mapping)
- `ItemBook` completeness (127 items, Garth's shop, categories)
- `FakeMemorySource` round-trip (synthetic memory read/write)
- `CharacterRecord` round-trip over synthetic IL2CPP objects
- `GameLocator` structural scan (finds valid character, rejects empty memory)

## Dependencies

- `GameTrainers.Common.Memory` — `ProcessMemory`, `MemorySearcher`, `BytePatternScanner`, `NativeMethods`
- `GameTrainers.Common.Mvvm` — `ObservableObject`, `RelayCommand`

## Important Notes

- The trainer targets the **Steam remaster** (2018), not the original DOS games. The original Bard's Tale I trainer lives in `../BardsTale1Trainer/`.
- All offsets are **[Confirmed]** or **[Inferred]** — see `docs/ReverseEngineering.md` for the full confidence table and methodology.
- The game must be running and a party loaded before attaching.
- Edits are live only — there is no save editor (the save format is IL2CPP-serialized binary, not documented).
