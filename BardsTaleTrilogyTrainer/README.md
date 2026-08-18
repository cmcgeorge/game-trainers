# Bard's Tale Trilogy Trainer

A live-memory trainer for **The Bard's Tale Trilogy** (Krome Studios / inXile, 2018 Steam remaster). Written in C# / WPF (.NET 8, x64).

## Features

- **Auto-locate**: one-click attach to `TheBardsTaleTrilogy.exe` and automatic party discovery via pointer-chain lookup (with structural scan fallback)
- **Character editing**: HP, SP (spell points), XP, level, gold, attributes, name — with freeze toggles for HP/SP/gold
- **Spell assignment**: teach any spell to any character, including **ZZGO** (Dream Spell) and **NUKE** (Götterdämmerung) — spells that are normally unobtainable through gameplay
- **Learn all class spells**: one button teaches every spell available to the character's current class
- **Infinite item charges**: setting item charges to zero makes the game treat items as having infinite uses (a Unity IL2CPP engine quirk)
- **Quick actions**: Full Heal, Max SP, Max XP, Max Gold, Max Everything — per character or party-wide
- **Reference tabs**: browse the full spell catalogue (140+ spells across all three games) and the 127-item catalogue
- **Value scanner fallback**: if auto-locate fails, use the built-in Cheat-Engine-style scan/narrow/pin workflow

## Requirements

- Windows 10/11 (x64)
- .NET 8.0 SDK
- The Bard's Tale Trilogy installed and running via Steam
- Administrator rights (the trainer reads/writes the game's process memory)

## Quick Start

1. Launch **The Bard's Tale Trilogy** via Steam
2. Load or start a party (you need to be in-game, not on the main menu)
3. Run `.\Run.ps1` in this folder (a UAC prompt will appear)
4. Click **Attach** to connect to the game process
5. Click **Locate** to find the party in memory
6. Edit values, toggle freezes, assign spells, set infinite charges

## Building

```powershell
.\Run.ps1                    # build Release + launch
.\Run.ps1 -Configuration Debug
.\Run.ps1 -Clean             # clean bin/obj first
.\Run.ps1 -Test -NoRun       # run verification harness only
.\Run.ps1 -Publish           # single self-contained win-x64 exe
```

## How It Works

The trainer attaches to the running game process and uses a **pointer chain** to locate the party:

1. Finds `GameAssembly.dll` in the process (the IL2CPP backend)
2. Reads the global game-state pointer at `GameAssembly.dll + 0xE40338`
3. Follows `global + 0xB8` to the party/economy object
4. Reads the character array and per-character fields (HP, SP, XP, gold, etc.)

If the pointer chain fails (different build, ASLR drift), a **structural scan** sweeps committed memory for a window of contiguous IL2CPP character objects matching the expected shape. A **value scanner** is available as a last resort.

## Spell List

The trainer supports all spells from the original Bard's Tale trilogy:

- **BT1 Magician**: ARFL, FOES, LOKT, MAJ, MAKB, ZAP!
- **BT1 Conjurer**: KALK, LUKO, MACO, OFOF, OROS, PHOTO
- **BT1 Sorcerer**: DIAL, KHAL, MAHL, MALE, MIBL, SOSI
- **BT1 Wizard**: BADR, DESI, DIVA, MADI, MAKI, MXST, REST
- **BT2 Archmage**: BLHE, FOFO, MAPO, MAGR, GRIM, BEDE
- **BT3 Chronomancer**: ACI, ARFI, DIKO, GETU, PINS, REAW, SCRY, SHEL, VIIT
- **BT3 Geomancer**: BA, ELBL, FAFO, FIDL, FRFO, HOWL, MAHA, PREC, SUME, WATE, WEFO, WIND
- **Dream Spells**: ZZGO (gate to any dungeon level), NUKE (Götterdämmerung — devastating damage)

## Limitations

- All offsets are marked **[Confirmed]** or **[Inferred]** — see `docs/ReverseEngineering.md` for the confidence table. The game was not available on the development machine for live verification.
- **Garth's shop editor** is a placeholder — the shop inventory offset has not been confirmed. Use the value scanner or Il2CppDumper for this feature.
- There is no save editor (the save format is IL2CPP-serialized binary).
- There is no teleport (map position was not identified).
- The trainer targets the **Steam remaster** (2018), not the original DOS games.

## Technical Details

- **Engine**: Unity with IL2CPP scripting backend (64-bit native `GameAssembly.dll`)
- **Process**: `TheBardsTaleTrilogy.exe`
- **Memory access**: `ReadProcessMemory` / `WriteProcessMemory` via `GameTrainers.Common`
- **Locator**: pointer chain (primary), structural scan (fallback), value scanner (last resort)
- **Freeze mechanism**: 200ms poll timer re-writes frozen values each tick

See `docs/ReverseEngineering.md` for the full memory layout, AOB signatures, and methodology.

## Related

- `../BardsTale1Trainer/` — trainer for the original DOS Bard's Tale I (DOSBox, 109-byte `.TPW` save format)
- `../GameTrainers.Common/` — shared memory access and MVVM libraries
