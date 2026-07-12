# Mines of Titan Trainer

A Windows-only C#/WPF **live-memory trainer** for the 1989 Westwood/Infocom sci-fi RPG
*Mines of Titan* (the PC port of *Mars Saga*) running under **DOSBox / DOSBox-X**. It attaches to
the running emulator, signature-scans the emulated guest RAM to find the party at runtime
(addresses are discovered live, never hard-coded), and reads/writes character state while you play.

## Features

- **Attach** to a DOSBox/DOSBox-X process and **scan** for the loaded party.
- Per-character editing: **name**, **sex**, **age**, the six **attributes** (Might, Agility,
  Stamina, Wisdom, Education, Charisma; 0–15), the 27 **skill** ranks, and **credits**.
- Quick actions per character and party-wide: **Max Attributes**, **Max Skills**, **Max Credits**,
  **Max Everything**.
- **Freeze Credits** — re-pins credits every poll tick so purchases never drain them.

The trainer follows the repo's **read-validate-write** rule: it only edits a memory window that
first validates as a real character (printable ASCII name, `M`/`F` sex, sane age, attributes in
0–15), and pokes back only the changed field's bytes, so a shifted layout can never corrupt RAM.
Health is derived from Might/Agility/Stamina in-game, so maxing those attributes maxes Health;
the trainer never writes the partially-decoded vitals block directly.

## How it finds the party

Two strategies (see `.docs/ReverseEngineering.md` for the full teardown):

1. **Anchor** — each save slot starts with the ASCII magic `IJKM`; the character array begins
   `0x1A` bytes past it and packs as 86-byte (`0x56`) records. Fast and exact once a game has been
   loaded/saved.
2. **Structural** — a fallback that walks memory for a run of windows shaped like valid records.
   This finds a freshly-created party that has never touched `SAVEGAME.DAT`.

## Build & run

```powershell
.\Run.ps1                         # restore, build Release, and launch (UAC prompt)
.\Run.ps1 -Configuration Debug    # debug build
.\Run.ps1 -Clean                  # wipe bin/obj first
.\Run.ps1 -NoBuild                # launch the existing exe
.\Run.ps1 -NoRun                  # build only; print the exe path
.\Run.ps1 -Test -NoRun            # run the verification harness, no GUI
.\Run.ps1 -Publish                # publish a self-contained win-x64 exe, no launch
```

The app manifest requests administrator rights for `ReadProcessMemory` / `WriteProcessMemory`, so
launching triggers a UAC prompt. Start *Mines of Titan* in DOSBox, load or create a party, then
**Attach** and (if needed) **Re-scan**.

## Layout

- `src/MinesOfTitanTrainer/` — the WPF app.
  - `Game/` — `CharacterFormat` (offsets/constants) and `CharacterRecord` (typed byte view).
  - `Memory/` — `PartyLocator` (anchor + structural scan) and `LocatedCharacter`.
  - `ViewModels/` — hand-rolled MVVM over `GameTrainers.Common`.
- `test/FormatCheck/` — headless harness that asserts the parser against a synthetic record and,
  if present, the real `.game/SAVEGAME.DAT`.
- `.docs/` — reverse-engineering notes and a strategy guide (git-ignored).

## Notes

This is a single-player cheat tool for your own saved games. It does not touch the network. Game
assets are copyrighted and are **not** included — supply your own legally obtained copy. Original
game files and RE notes live in git-ignored dot-folders (`.game/`, `.docs/`, `.data/`).
