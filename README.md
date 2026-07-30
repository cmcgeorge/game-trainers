# Game Trainers

A collection of independent, Windows-only **live-memory trainers** for classic games — mostly DOS titles running under **DOSBox / DOSBox-X**, plus two native 32-bit Windows titles (`ImperialismIITrainer` and `BeachHead2000Trainer`, which attach straight to the game process). Each trainer is a small C#/WPF app that attaches to the running process, signature-scans (or value-scans) its RAM to locate the game's state at runtime (addresses are discovered live, never hard-coded), and reads/writes it while you play — freeze toggles, "max" buttons, teleporting, and, for some titles, offline save editing. Several trainers also include a reverse-engineering workspace documenting how their offsets were recovered.

Each game lives in its own self-contained folder with its own solution/project, `README.md`, run script, and (for most) an `AGENTS.md` contributor guide.

## Trainers

| Folder | Game | Target |
| --- | --- | --- |
| `AmberstarTrainer/` | Amberstar (Thalion Software, 1992) | net8.0-windows |
| `AutoduelTrainer/` | Autoduel (Origin Systems, 1985) | net9.0-windows |
| `BardsTale1Trainer/` | The Bard's Tale: Tales of the Unknown, Vol. I (1987) | net8.0-windows |
| `BattleTech1Trainer/` | BattleTech: The Crescent Hawk's Inception (Westwood/Infocom, 1988) | net8.0-windows |
| `BeachHead2000Trainer/` | BeachHead 2000 (Digital Fusion / WizardWorks, 2000) — native Win32 | net8.0-windows |
| `ColonizationTrainer/` | Sid Meier's Colonization (MicroProse, 1994) | net8.0-windows |
| `DarklandsTrainer/` | Darklands (MicroProse, 1992) | net8.0-windows |
| `DragonWarsTrainer/` | Dragon Wars (Interplay, 1989) | net8.0-windows |
| `ImperialismIITrainer/` | Imperialism II: The Age of Exploration (Frog City / SSI, 1999) — native Win32 | net8.0-windows |
| `KeefTrainer/` | Keef the Thief (EA / Naughty Dog, 1989) | net9.0-windows |
| `LordsOfTheRealmTrainer/` | Lords of the Realm | net9.0-windows |
| `MightAndMagic1Trainer/` | Might & Magic Book One (1986) | net8.0-windows |
| `MinesOfTitanTrainer/` | Mines of Titan (Westwood Associates, 1989) | net8.0-windows |
| `MoriaTrainer/` | The Dungeons of Moria / UMoria 5.5.2 (Koeneke/Wilson, 1988) | net8.0-windows |
| `PiratesTrainer/` | Sid Meier's Pirates! (MicroProse, 1987 — IBM v432.02) | net8.0-windows |
| `PoolOfRadianceTrainer/` | Pool of Radiance (1988) | net8.0-windows |
| `QuestForGlory1Trainer/` | Quest for Glory I: So You Want to Be a Hero (Sierra On-Line, 1989) | net8.0-windows |
| `RailroadTycoonTrainer/` | Sid Meier's Railroad Tycoon (MicroProse, 1990) | net8.0-windows |
| `ShogunTrainer/` | James Clavell's Shōgun (1987) | net8.0-windows |
| `SwordOfAragonTrainer/` | Sword of Aragon (SSI, 1989) | net8.0-windows |
| `SwordOfTheSamuraiTrainer/` | Sword of the Samurai | net8.0-windows |
| `SyndicatePlusTrainer/` | Syndicate | net8.0-windows |
| `ThePerfectGeneral2Trainer/` | The Perfect General II (QQP, 1994) | net8.0-windows |
| `WarOfTheLanceTrainer/` | War of the Lance (SSI, 1989) | net8.0-windows |
| `WastelandTrainer/` | Wasteland (Interplay / Electronic Arts, 1988) | net8.0-windows |

`MightAndMagic1Trainer` is the architectural template most of the others were ported from.

### Shared library

`GameTrainers.Common/` is a small shared library holding the game-agnostic plumbing that used to be copied between trainers: the process/guest-memory access layer (`GameTrainers.Common.Memory`) and the hand-rolled MVVM base types (`GameTrainers.Common.Mvvm`). The MM1-family trainers — `MightAndMagic1Trainer`, `BardsTale1Trainer`, and `PoolOfRadianceTrainer` — reference it instead of duplicating that code, as do `DragonWarsTrainer`, `MinesOfTitanTrainer`, `WarOfTheLanceTrainer`, `ThePerfectGeneral2Trainer`, `BattleTech1Trainer`, `QuestForGlory1Trainer`, `DarklandsTrainer`, `WastelandTrainer`, `MoriaTrainer`, `ColonizationTrainer`, `RailroadTycoonTrainer`, `ImperialismIITrainer`, `AmberstarTrainer`, and `BeachHead2000Trainer`; each keeps only its own game-specific locators and scanners. `WastelandTrainer` locates the party by **structure** (an array of seven contiguous 256-byte records that pack from slot 0) rather than by a static anchor, and teleports by writing the party's X/Y into the party-state header that precedes the roster. `ThePerfectGeneral2Trainer`, `BattleTech1Trainer`, `QuestForGlory1Trainer`, `DarklandsTrainer`, `MoriaTrainer`, and `BeachHead2000Trainer` drive Common's `MemorySearcher` as a Cheat-Engine-style value scanner rather than a fixed locator, because their live game state has no stable static signature to anchor to (`BattleTech1Trainer` additionally uses Common's `BytePatternScanner` to *detect* the game via its read-only EXE strings; `QuestForGlory1Trainer` additionally exposes a Day/Time editor and a Teleport editor that write directly to SCI0 global variables once their addresses are scanned and pinned; `DarklandsTrainer` targets a PKLITE-packed, extender-relocated EXE that has no reliable detection signature either, so it has neither a locator nor a detector and keeps only its Confirmed attribute/skill/currency/Fame reference tables and a read-only DEFAULT-save reader; `MoriaTrainer` targets UMoria 5.5.2, a DJGPP-compiled 32-bit DPMI roguelike whose heap address changes every session, so it has neither a locator nor a detector — it uses `MemorySearcher` with 14 guided scans for character stats plus a relative-scan teleport that locates `char_row`/`char_col` by walking cardinal directions in-game, and keeps its Confirmed game-knowledge layer local — the stat encoding, the cave cell constants, a curated monster roster including the Balrog, the 31+31 spells, the item categories, and the 51-level descent reference). `ColonizationTrainer` (Sid Meier's Colonization — MicroProse, 1994) is the odd one whose **primary** feature is an **offline save-game editor** rather than a live trainer: the `COLONYxx.SAV` format is a flat little-endian serialization with a `"COLONIZE"` signature and no checksum, so it edits any power's gold/tax/Founding-Fathers and any colony's stockpile in place (the human player's gold is a 4-byte field at a nation-record offset computed from the header's colony/unit counts, verified byte-for-byte against the shipped saves); it also offers Common's `MemorySearcher` as a live value scanner for gold/tax/bells, and keeps its own `SaveFormat`/record views and game-knowledge books local. `ImperialismIITrainer` (Imperialism II — Frog City / SSI, 1999) is the repo's only **native 32-bit Windows** target: it attaches straight to `Imperialism II.exe` (no emulator). Its primary path is a **one-click auto-locate** — because the exe has a fixed image base and no ASLR, a static global always points to the player's nation object, so a `GameLocator` follows it and pins the treasury (`+0x130`) and warehouse resources (`+0xDD4`) with no scanning; Common's `MemorySearcher` (guided Treasury/Resource/Labour scans) is the build-independent fallback. It has **no save editor** (the `.imp` save has no matching map). The anchor was recovered by live RE — the game's own linker map names the data model (`TGreatPower : TCountry`, treasury a 32-bit `long`, stockpiles 16-bit) but is an earlier build than the shipped exe, so the map's *addresses* were re-recovered with a pointer scan. It keeps its `GameLocator`/`NationLayout` and `CommodityBook` (the 28 Age-of-Exploration goods from the game manual, ten confirmed by reading the live warehouse) local. `RailroadTycoonTrainer` (Sid Meier's Railroad Tycoon — MicroProse, 1990) is a DOS-under-DOSBox target that, like the MM1-family trainers, **has a string-anchored `GameLocator`**: Railroad Tycoon is an almost-entirely-static Microsoft-C build, so the player's cash is a signed 16-bit word (in units of $1,000) at a fixed offset in its data segment (DGROUP). The locator finds DGROUP by two static report labels the game always keeps in memory (`"Outstanding Loans: "`, `"Stockholders Equity: "`), validates with the year global, and reads cash at its known offset — one-click auto-locate that pins cash and year with no scan (live-confirmed against the running game), with `MemorySearcher` as the build-independent fallback. There is **no save editor**: the `.SVE` is a variable-length, count-keyed serialization in which cash is not a discrete field. It keeps its `RtLayout`/`GameLocator` and its `LocomotiveBook`/`GameFacts` reference tables (the engine rosters double as the startup copy-protection quiz answer key) local. `AmberstarTrainer` (Amberstar — Thalion Software, 1992) also references Common and follows the structural-scan model: Amberstar was originally developed for the Atari ST (Motorola 68000, **big-endian**) and ported to PC, so all multi-byte values in its 1146-byte character records are big-endian. The party is up to six contiguous records (each validated by its `00 FF` magic header, type = Person, plausible fields, and an ASCII name); the roster address changes every DOSBox session, so `PartyLocator` scans every readable region for a window matching the party shape exactly — no static anchor. It keeps its game-knowledge layer local (`CharacterFormat` offset table, `CharacterRecord` big-endian view, `SpellBook`/`RaceBook`/`ClassBook` = 96 spells, 7 races, 8 classes). The `PARTYDAT.SAV` save file uses an unknown compression method and is not editable; this trainer edits live memory only. Its RE notes and strategy guide live in its committed `docs/`. `BeachHead2000Trainer` (BeachHead 2000 — Digital Fusion / WizardWorks, 2000) is the repo's **second native 32-bit Windows** target: it attaches straight to `Bh.exe` (shipped in the Steam "BeachHead Gold Edition" package) — no emulator. Its mutable state (health, ammo, score, current level) is heap-allocated with no stable static anchor, so it has neither a locator nor a detector and uses `MemorySearcher` with six guided scans (all Int32). It additionally exposes a **level-file editor** for the shipped `Level_00`…`Level_60` plain-text scripts (starting ammo, time limit, enemy aggression, artillery flag), which `LevelFile` parses and round-trips without losing comments or unknown lines. It keeps its Confirmed game-knowledge local (`GameFacts` constants, `WeaponInfo`/`EnemyInfo`/`ControlInfo` reference tables). `SwordOfAragonTrainer` (Sword of Aragon — SSI, 1989) also references Common and is, like `ColonizationTrainer`, primarily an **offline save editor**: Sword of Aragon is a compiled **QuickBASIC 3.0** DOS game linked against the absent **BRUN30** run-time module, so its code stream is a sequence of far calls into a module that is not in the image and **does not disassemble** (a full Ghidra auto-analysis of `SWORD.EXE` recovered ~350 instructions from a 40 KB image, with no cross-references to any interesting string) — which means no variable address is statically recoverable, but the *data* is almost entirely legible. `ARAGON.HS?` is plain CSV (a 3-line header whose third line carries wealth/score/income/upkeep, then 20 city blocks of 14 lines each, then a 2-line trailer) and `ARAGON.HR?` is an array of exactly 80 fixed 100-byte roster records split 20 characters + 60 units, so both are edited field-by-field in place with everything else round-tripped verbatim and a one-shot `.bak` taken before the first write. The roster layout was proved arithmetically rather than guessed: summing the unit/equipment price tables that `SWORD.EXE` carries as QuickBASIC `DATA` text reproduces each record's stored make/train/upkeep and stacking-size fields for **623 of 623** occupied records across the 15 shipped saves and 16 (player class, unit type) pairs — including the class purchase discounts (Warrior halves Infantry; Knight takes 25 % off Cavalry *and* Mounted Infantry, which the rule book does not mention; Ranger takes 25 % off Bowmen and Horse Bowmen). Its live tab has **no `GameLocator`** in the usual sense but is not a blind value scanner either: QuickBASIC stores every string literal behind a 4-byte `(length, DS-offset)` descriptor and the whole literal pool sits at one constant offset from the file image, so `DgroupLocator` signature-scans DOSBox for a distinctive 38-byte `ARAGON.EXE` literal whose `DS:` offset is known, accepts the hit only when at least two of three further literals also line up at their own expected offsets (a three-of-four match at minimum, with the count reported), derives `DS:0000` from it, and searches that single 64 KiB segment — with Common's `MemorySearcher` as the build-independent fallback. Gold is **Microsoft Binary Format**, not IEEE 754 (QuickBASIC 3.0 predates Microsoft's move to IEEE), so it is scanned and written through a local `Mbf` converter; usefully, positive MBF singles stay monotonic read as unsigned Int32, which is what makes Increased/Decreased narrowing work on gold at all. The startup copy protection ("using the Sword of Aragon poster… enter the first word of the summary information for that city") is answered rather than patched: the complete 13-city × 4-field answer key is stored as plain literals in `SWORD.EXE` and ships in the trainer's `ProtectionBook`, so no game executable is modified. It keeps its game-knowledge layer local (`GameFacts`, `Mbf`, `UnitBook`, `RosterFormat`/`RosterRecord`/`RosterFile`, `CsvRow`/`CityRecord`/`KingdomFile`, `SaveSet`/`SaveBackup`, `CityBook`/`SpellBook`/`ProtectionBook`/`TerrainBook`), and its `FormatCheck` harness runs 457 checks (272 without the copyrighted saves) — MBF round-trips, the cost model, the documented hard limits pinned to literals, table invariants, synthetic roster/kingdom fixtures, and every shipped save parsed and round-tripped byte-for-byte. Its RE notes and a play/strategy guide with maps live in its committed `docs/`. `ARAGON.HT?` (the world grid) is deliberately neither read nor written. `PiratesTrainer` (Sid Meier's Pirates! — MicroProse, 1987, IBM version 432.02) also references Common and, like `RailroadTycoonTrainer`, **has a string-anchored `GameLocator`**. Its target is unusual: the shipped distribution is a DOS conversion of the original *self-booting* release, in which a 1,983-byte shim (`PIR.EXE`) opens the raw floppy images `DISK1`/`DISK2`/`DISKS` as ordinary files, hooks **INT 80h** (an `INT 13h`-style sector read/write), **INT 81h** (select disk) and **INT 82h** (a keyboard poll where scancode `0x44` = **F10** quits to DOS), and then EXECs `DISKP` — the game proper, a plain MZ image whose first 32 bytes are a relocated segment table putting `DGROUP` at image paragraph `0x1124`. Every global therefore has a constant DGROUP offset, so `GameLocator` sweeps DOSBox for the title-screen literal `"COPYRIGHT (C)  1987  MICROPROSE INC."` (DGROUP `0x0183`), derives `DGROUP:0000 = hit − 0x0183`, and accepts the candidate only if the eight-byte `"PIRATES!"` save magic (`0x4128`) and the `JAN…DEC` month table (`0x31C9`) also sit at their offsets, the era code and date decode sanely, **and** the run-time-loaded settlement table at `0x4240` parses as 24-byte records — that last check is what a buffered copy of the program image cannot fake. It then pins gold (an **unsigned** `int16` at `DGROUP:0x4847` that the game's own add-gold routine *saturates* at 65,535 rather than wrapping — fixed beyond doubt by the matched add/spend pair, the latter printing "Not enough gold."), crew, personal wealth (`0x4742`, in tens of gold), land (`0x4745`, in units of 50 acres), the flat 360-day calendar (`0x9A9F`/`0x9A9D`/`0x9A2B`) and the era code (`0x475A` — stored as 0, 2, 3, 4, 5, 6, *not* 0–5, which is what makes `1560 + 20 × code` produce the six offered years), and lists the era's settlements live so the user can eyeball that the locate is right before poking. `MemorySearcher` (guided Gold/Crew/Any-value scans) is the fallback. There is **no save editor**: the shipped `DISKS` is an unformatted blank, so the on-disk slot directory could not be validated, although the 1,940-byte in-memory save block at `DGROUP:0x4130` and its `PIRATES!` magic are both confirmed. Its `CityBook`/`FleetSchedule` are **generated** from `DISK1` (six 1,024-byte era blocks at `0x54000 + 0x400 × era`): 32 settlements in 1560 rising to 41 by 1680, each with map position, nation, forts, garrison, population and treasury, plus the Treasure Fleet / Silver Train itineraries — which double as the answer key to the 1987 manual's date-lookup copy protection. Both the routes *and* their calendar phase come out of the binary (`slot = day/15 − bias + 2 × (era & 1)`, bias 18 for the Fleet and 6 for the Train), and reconstructed that way they reproduce the shipped answer key 11 of 12 itineraries entry-for-entry. Neither protection is active in this build — the disk check cannot fire once every sector comes from a file, and the manual question is absent from the program's complete 589-record display-string table. The whole teardown was **static**; that is stated plainly in its README and RE doc, and is why the locator validates three anchors plus the settlement table. The remaining trainers are still self-contained.

## Prerequisites

- **Windows** (the trainers use WPF and the Win32 process-memory APIs).
- **.NET 8 SDK** (some trainers target .NET 9 — install the .NET 9 SDK to build everything).
- **DOSBox** or **DOSBox-X** running the target game.
- Administrator rights: most trainers ship a manifest that requests elevation for `ReadProcessMemory` / `WriteProcessMemory`, so launching triggers a UAC prompt.

## Building and Running

Every trainer has its own `.\Run.ps1`, and they all expose the **same** options. Run one from
inside its folder, or use the **root launcher** to pick one interactively.

### Root launcher

From the repository root, `.\Run.ps1` discovers every trainer (any top-level folder that has its
own `Run.ps1`) and forwards the shared options to the one you choose:

```powershell
.\Run.ps1                         # menu: pick a trainer, then build and launch
.\Run.ps1 -List                   # list the trainers and exit
.\Run.ps1 -Trainer Shogun         # run a trainer by name (exact or unique partial)
.\Run.ps1 -Trainer 4 -Clean       # run the 4th listed trainer, cleaning first
```

### Per-trainer

```powershell
cd PoolOfRadianceTrainer
.\Run.ps1                         # restore, build Release, and launch (UAC prompt)
.\Run.ps1 -Configuration Debug    # debug build
.\Run.ps1 -Clean                  # wipe bin/obj first, then build and launch
.\Run.ps1 -NoBuild                # skip the build, launch the existing exe
.\Run.ps1 -NoRun                  # build only; print the exe path
.\Run.ps1 -Test -NoRun            # run the verification harness, no GUI
.\Run.ps1 -Publish                # publish a self-contained win-x64 exe, no launch
```

Shared options (identical for the root launcher and every trainer):

- **`-Configuration Debug|Release`** — build configuration (default `Release`).
- **`-Clean`** — remove `bin`/`obj` before building.
- **`-NoBuild`** — skip building and launch the most recent build.
- **`-NoRun`** — build only; do not launch.
- **`-Test`** — run the trainer's verification harness (warns if it has none).
- **`-Publish`** — publish a single self-contained win-x64 exe; skips launch.

Only `AmberstarTrainer`, `BardsTale1Trainer`, `BattleTech1Trainer`, `BeachHead2000Trainer`, `ColonizationTrainer`, `DarklandsTrainer`, `DragonWarsTrainer`,
`ImperialismIITrainer`, `MightAndMagic1Trainer`, `MinesOfTitanTrainer`, `MoriaTrainer`, `PiratesTrainer`, `PoolOfRadianceTrainer`,
`RailroadTycoonTrainer`, `SwordOfAragonTrainer`, `SwordOfTheSamuraiTrainer`, `ThePerfectGeneral2Trainer`, `WarOfTheLanceTrainer`, and
`WastelandTrainer` ship a verification harness; `-Test` warns and is ignored on the others (including `QuestForGlory1Trainer`). `SwordOfTheSamuraiTrainer` also has `.\Edit-SotsSave.ps1` for offline save editing.

You can always build directly with the SDK:

```powershell
dotnet build <project>.csproj -c Release
```

Then start the target game in DOSBox / DOSBox-X, and use **Attach** in the trainer.

## Testing

There is no unit-test suite. Trainers that ship verification use a headless console harness (usually `FormatCheck`, or `Verify` in `SwordOfTheSamuraiTrainer`) that checks the parsers against captured memory dumps / save files and exits `0` (pass) or `1` (fail). Run it via `.\Run.ps1 -Test -NoRun` where available, or `dotnet run --project <test-project>`. The GUI itself cannot be tested headlessly — it needs an interactive desktop and a running game.

## Notes

These are single-player cheat tools for the user's own saved games. They do not touch the network or any external service. Game assets are copyrighted and are **not** included — supply your own legally obtained copy. Original game files, memory dumps, and reverse-engineering notes live in dot-prefixed folders (`.game/`, `.data/`, `.docs/`) that are git-ignored.
