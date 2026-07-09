# Repository Guidelines

A Windows trainer that attaches to the DOS game *Syndicate* running under DOSBox-X and edits its live memory (money, agent health, ammo), plus the reverse-engineering workspace that recovered those addresses.

## Project Structure & Module Organization
- `.\Trainer\` — .NET 8 WPF solution. `.\Trainer\SyndicateTrainer\` is the GUI trainer; `.\Trainer\Diag\` is a console harness that **links** (not references) `GameConnection.cs`, `ProcessMemory.cs`, and `NativeMethods.cs` to exercise the memory logic against a live game. Only the GUI project is in `.\Trainer\SyndicateTrainer.sln`.
- `.\re_work\` — Python reverse-engineering workspace. `le_loader.py` rebuilds the DOS/4GW LE executable into a flat `main.bin` (+ `main.json` layout); `dump_tool.py` / `validate_trainer.py` diff DOSBox-X memory dumps to recover game addresses; `scripts\` holds Ghidra headless scripts.
- `.\.docs\` — the authoritative teardown (`Syndicate-Reverse-Engineering.md`) and its derived `Syndicate-Strategy-Guide.md`.
- `.\Game\` — the shipped GOG install (DOSBox-X + `SYNDICAT\MAIN.EXE`); treat as read-only input.
- Root `dosbox-x-*.bin` / `.csv` files are large process dumps consumed by the Python tools.

The trainer locates the game by signature-scanning the DOSBox-X process for the `PERSUADERTRON` anchor string, deriving `GameBase`, then reading reverse-engineered offsets. All addresses live as constants in `GameConnection.cs`.

## Build, Test, and Development Commands
- `.\Run.ps1` — build the trainer (Release, x64) and launch it.
- `.\Run.ps1 -Configuration Debug -NoRun` — build only.
- `dotnet build Trainer\SyndicateTrainer\SyndicateTrainer.csproj -c Release -p:Platform=x64` — direct build.
- `dotnet run --project Trainer\Diag\Diag.csproj` — run the diagnostic harness against a running game.
- `python re_work\validate_trainer.py` — replay the attach/offset logic against the root dumps (run from the repo root; other `.\re_work\` scripts assume they run from inside that folder, so mind the working directory).

Start Syndicate in DOSBox-X first, then Attach. The trainer ships an `asInvoker` manifest and can normally open a same-user DOSBox-X process without elevation; run it as **Administrator** only if you hit "Access denied".

## Coding Style & Naming Conventions
- C#: `net8.0-windows`, **x64 only** (DOSBox-X is a 64-bit host), `Nullable` and `ImplicitUsings` enabled, `AllowUnsafeBlocks=false`. Use file-scoped namespaces and XML `///` docs on public members.
- All process-memory access is serialised through `_lock`; long scans run on a background thread and publish state atomically. Follow the read-validate-write pattern — only write to an address whose current bytes still look valid, so a shifted layout is never corrupted.
- Python RE scripts are standalone and 4-space indented.

## Reverse-Engineering Workflow
Rebuild the flat image with `python le_loader.py Game/SYNDICAT/MAIN.EXE main`, import `main.bin` into Ghidra as raw `x86:LE:32:default` at base `0x10000`, then recover offsets by diffing dumps. Record any new address in both `GameConnection.cs` and `.docs\Syndicate-Reverse-Engineering.md`.
