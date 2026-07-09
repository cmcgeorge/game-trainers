# Keef the Thief — Trainer

C#/WPF trainer for **Keef the Thief** (EA / Naughty Dog, 1989) running under
**DOSBox** or **DOSBox-X** on Windows.

## Usage

1. Launch the game in DOSBox / DOSBox-X (`Game\KEEF.BAT`).
2. Run the trainer:

   ```
   dotnet run --project KeefTrainer
   ```

   or build once (`dotnet build`) and start
   `bin\Debug\net9.0-windows\KeefTrainer.exe`.

3. The trainer attaches automatically (it scans every `dosbox*` process for
   the game's stat table and re-attaches if the emulator restarts).

- **Edit** — type a value and press **Enter** (Esc reverts).
- **❄ Freeze** — keeps rewriting the value 4×/sec.
- **Presets** — God Mode (freeze HP/MP 999 + survival meters 100),
  Max Stats, Refill, Gold 9999, 99 Picks & Flints.

Edits persist through in-game saves (the game saves from the same memory the
trainer writes).

## How it works

The game keeps its authoritative character state in a static table of
`{ int16 value; char[15] label; }` entries inside `KF.EXE`'s data segment.
The trainer finds that table inside the emulator's guest RAM by signature
(`Strength:` / `Speed:` / `Constitution:` labels at fixed strides), then reads
and writes every field at fixed offsets from it via
`ReadProcessMemory`/`WriteProcessMemory`.

Full reverse-engineering details and the verified field map:
[`../.docs/KeefTrainer-MemoryMap.md`](../.docs/KeefTrainer-MemoryMap.md).

Diagnostics: `KeefTrainer.exe --screenshot out.png` renders the UI to a PNG
shortly after startup and exits.

## Notes

- Max HP ≡ Constitution and Max MP ≡ Wisdom (the game derives them), so raise
  those attributes to raise the visible `x/y` maximums.
- Gold is clamped by the game at 9999 when looting; Level caps at 24.
- Weapon/armor numbers reflect the currently equipped items and are
  overwritten when you re-equip.
