# Imperialism II Trainer

A Windows (WPF / .NET 8) live-memory trainer for **Imperialism II: The Age of Exploration**
(Frog City / SSI, 1999). Unlike the rest of this repo — DOS games under DOSBox — Imperialism II is a
**native 32-bit Windows** program, so the trainer attaches straight to `Imperialism II.exe` and edits
its memory directly; no emulator, no guest-address translation.

The primary path is **one-click auto-locate**: because Imperialism II is a native exe with a fixed image
base and no ASLR, a static global always points to your nation object, so the trainer follows that
pointer and pins your **treasury** and **warehouse resources** straight to the freeze table — no
scanning. A Cheat-Engine-style **value scanner** (with guided presets for treasury / resources / labour)
is kept as the fallback and for any value the locator doesn't map. Either way, pinned values go in a
freeze table that holds them against the game's turn recalculation. The app manifest requests
administrator rights so it can `Read/WriteProcessMemory`, so launching shows a UAC prompt.

## How it finds your data (auto-locate, with a scanner fallback)

The game ships a linker map (`Imperialism II.map`) that names the whole data model — the human power's
treasury is a signed 32-bit `long` on a `TGreatPower`, warehouse stockpiles are signed 16-bit. The map's
own addresses are stale (the **shipped exe is a later build**, June 1999 vs the map's January 1999), but
that only meant the *addresses* needed re-recovering, not the *approach*: one live pointer scan found the
static global that points to the player nation, and the field offsets within that object (treasury at
`+0x130`, the warehouse int16 array at `+0xDD4`) are compile-time constants that don't move between
launches. So the trainer auto-locates like the repo's anchored trainers. If a different game build fails
validation (vtable + plausible treasury + sane warehouse), it falls back to the value scanner, which is
build-independent. Full story in
[`docs/Imperialism2-Reverse-Engineering.md`](docs/Imperialism2-Reverse-Engineering.md).

## Using it

1. Launch **Imperialism II** and load your game.
2. Build and run the trainer: `.\Run.ps1` (accept the UAC prompt).
3. On the **Live** tab, pick the `Imperialism II` process (it's pre-selected) and **Attach**.
4. Click **⚡ Auto-locate player (no scan)**. Your Treasury and warehouse goods appear on the **Freezes**
   tab — edit **Target** to set a value, or tick **Freeze** to hold it. Done.
5. *Fallback / anything else* — use a **guide** and follow the status line:
   - **Treasury** (Int32): type the cash on the top bar → *First Scan*; change it in-game → *Exact*; repeat.
   - **Resource** *of* a good (Int16): pick the commodity, read its warehouse amount, scan the same way.
   - **Labour** (Int16; try Byte if empty).
   Then **Pin selected →** the Freezes tab.

The **References** tab lists the commodity table, the cheat/script hooks the game itself carries, and
how-to notes.

## Build / test

```powershell
.\Run.ps1                 # build Release and launch (UAC prompt)
.\Run.ps1 -Test -NoRun    # run the verification harness, no GUI
.\Run.ps1 -Configuration Debug
.\Run.ps1 -Clean          # wipe bin/obj first
.\Run.ps1 -Publish        # single self-contained win-x64 exe
```

Or directly: `dotnet build src\ImperialismIITrainer\ImperialismIITrainer.csproj -c Release`.

The `FormatCheck` harness (`test/FormatCheck`) is headless — it asserts the commodity reference table,
the nation-object layout + validation logic (offsets, the vtable/treasury/warehouse checks against a
synthetic header), and the pure value-scanner helpers (parse / width-fit / canonicalize, and the
frozen-value poke/freeze/width-guard through a fake host), and exits 0/1. It touches no live process. The
GUI and the live `GameLocator` scan can't be smoke-tested headlessly (both were validated by hand against
a running game).

## Layout

- `src/ImperialismIITrainer/`
  - `Game/NationLayout.cs` — the recovered layout facts: the static globals that point to the player
    nation, the field offsets (treasury `+0x130`, warehouse `+0xDD4`), the confirmed commodity slots, and
    the pure validation helpers.
  - `Game/GameLocator.cs` — follows a static global to the nation object, validates it (vtable in .rdata
    + plausible treasury + sane warehouse), and exposes the treasury/warehouse addresses. The auto-locate
    engine.
  - `Game/CommodityBook.cs` — the 28 Age-of-Exploration commodities (raw / refined / food / luxury /
    riches) taken from the game manual and confirmed against the live warehouse.
  - `ViewModels/` — hand-rolled MVVM. `LiveScannerViewModel` (auto-locate + attach/scan/narrow/pin/freeze
    + guided scans), `ReferenceViewModel`, and the reusable scanner rows (`IScanHost`, `ScanValue`,
    `ScanResultViewModel`, `FrozenValueViewModel`). `ProcessMemory`/`MemorySearcher` come from
    `GameTrainers.Common.Memory`.
- `test/FormatCheck/` — the verification harness.
- `docs/` — the reverse-engineering write-up.

## Notes

A single-player cheat tool for your own saved game. It touches no network or external service. The game
is copyrighted and is **not** included — supply your own legally obtained copy.
