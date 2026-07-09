# Autoduel Trainer (C# / WPF)

A live memory editor for **AUTODUEL** (Origin Systems, 1985) running under
**DOSBox-X**. It attaches to the DOSBox process, finds the game's driver record
in the emulated guest RAM, and lets you read and edit money, skills, health,
body armor, location, and car state (battery, weapon ammo, armor damage) while
the game runs.

The memory layout and encodings it relies on were reverse-engineered from the
dumps in `../Data`; see [`../.docs/reverse-engineering.md`](../.docs/reverse-engineering.md)
for the full analysis and [`../.docs/strategy-guide.md`](../.docs/strategy-guide.md)
for how to actually win the game.

## Requirements

* Windows 10/11 (x64)
* .NET 9 SDK (build) / .NET 9 Desktop Runtime (run)
* AUTODUEL running inside DOSBox-X (the process name must start with `dosbox`)

## Build & run

```powershell
cd Trainer
dotnet build -c Release
dotnet run   -c Release      # or launch bin\Release\net9.0-windows\AutoduelTrainer.exe
```

If DOSBox is running elevated, run the trainer elevated too (change
`app.manifest` to `requireAdministrator`, or start it "as administrator").

## Usage

1. Start AUTODUEL in DOSBox-X and **load a driver** (get past the title screen —
   the record only exists once a driver is active).
2. In the trainer, pick the DOSBox process and click **Attach**. It scans guest
   RAM for a code signature, derives the driver-record address, and validates it.
3. Edit the fields and press **Apply Driver** / **Apply Car**, or use the quick
   buttons:
   * **Max Money** – set to $999,999 (the game's hard cap)
   * **Max Skills** – Driving/Marksmanship/Mechanic to 99
   * **Full Health** – health to 3 and body armor to 5
   * **Charge Battery** – power cell to full
   * **Reload Weapons** – refill every mounted weapon's magazine (lasers/heavy
     rockets are self-powered and skipped)
   * **Repair All** – restore every component's DP to its max
4. **Auto-refresh** re-reads the state ~once a second so on-screen values track
   the game. Uncheck it if you want values to hold still while you type.

## How it finds the game (robustness notes)

* Scans committed, readable private regions ≥ 1 MB (largest first) for the
  24-byte signature at `AUTODUEL.COM` file offset `0x8000`.
* `player = signatureHit − 0x8000 + 0x148BD`; the candidate is sanity-checked
  (printable name, skills ≤ 99, valid city/health) before it's accepted, so a
  stray signature match can't corrupt an unrelated address.
* Values are stored as little-endian **base-100** digits (see the RE doc); the
  trainer encodes/decodes accordingly and clamps to legal ranges on write.

## Safety

* Editing is done with `WriteProcessMemory` on the live game. It only touches the
  driver/car record; it does not patch code.
* Extreme values (e.g. money over the cap, health above the intended max) are
  clamped, but the game was never designed for edited state — keep a backup of
  your `Game/drivers` save file before experimenting.
* This is a single-player, offline trainer for a 40-year-old game; use it on your
  own machine.

## Project layout

| File | Purpose |
|---|---|
| `Memory/NativeMethods.cs` | P/Invoke: OpenProcess, Read/WriteProcessMemory, VirtualQueryEx |
| `Memory/ProcessMemory.cs` | Handle wrapper + region enumeration |
| `Memory/GameData.cs` | Struct offsets, tables, base-100 codec (the RE facts) |
| `Memory/Models.cs` | `GameSnapshot`, `ComponentInfo` |
| `Memory/TrainerEngine.cs` | Attach, signature scan, read/write |
| `MainViewModel.cs` | MVVM state, commands, auto-refresh |
| `MainWindow.xaml` | UI |
