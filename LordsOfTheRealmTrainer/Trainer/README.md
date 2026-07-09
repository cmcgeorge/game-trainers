# Lords of the Realm — Trainer

A small Windows (WPF / .NET 9) trainer for the DOS game **Lords of the Realm** running under **DOSBox-X**. It attaches to the emulator, recognises the game's own data structures, and gives you **named one‑click cheats** — set your gold, bankrupt rival lords — plus an advanced scanner for anything else.

It does **not** modify `LORDS.EXE` or any game file, and it ships no game data. It only reads and writes the live emulator's memory while you play.

**How it knows the game (not just a memory editor):** the trainer finds the game's static data segment by signature and reads the reverse‑engineered **lords array** (treasury is an Int32 at a fixed offset per lord — see [`../.docs/ReverseEngineering.md`](../.docs/ReverseEngineering.md), confirmed by writing to the live game). So "Gold" is a real, labelled cheat, not an address you have to hunt for.

> Companion documentation lives in [`../.docs`](../.docs): `ReverseEngineering.md` (how the game stores its state) and `StrategyGuide.md` (how to play well).

---

## Requirements

- Windows 10/11 (x64)
- [.NET SDK 8 or 9](https://dotnet.microsoft.com/download) (`dotnet --version` should print ≥ 8)
- **Lords of the Realm** already running inside **DOSBox-X** (the GOG "Royal Edition" build is what this was validated against)

---

## Quick start

From the repository root:

```powershell
.\Run.ps1
```

`Run.ps1` builds the project and launches the trainer. Useful switches:

```powershell
.\Run.ps1 -Configuration Release   # release build (default)
.\Run.ps1 -NoBuild                 # run the last build without rebuilding
.\Run.ps1 -Clean                   # delete bin/obj first
```

Or drive `dotnet` directly:

```powershell
dotnet run --project .\Trainer\LordsTrainer.csproj -c Release
```

---

## How to use it

1. **Start the game** in DOSBox-X and load or begin a match.
2. **Attach.** Click **Attach to DOSBox-X**. When a match is loaded the status line reads *"Game detected — DGROUP at segment 0x…, N lord(s), you are Lord X."* — the segment is discovered fresh on each attach, not a fixed value.
3. **Cheat.** The **Cheats** panel lists every lord with their **live gold**. Confirm that **Lord 1**'s gold is close to the "crowns" on your treasury/market screen (Lord 1 is normally you). The live figure can read a little **higher** than the crowns between treasury-screen refreshes, because the game only updates the on-screen number when you open that screen.
   - **💰 Set my gold to 999,999** — instant riches for Lord 1.
   - **🗡 Bankrupt rival lords** — sets every other lord to 0 gold and freezes them there.
   - Or per lord: tick **Freeze** to hold the current value, or type a number in **Set to** and click **Set selected lord's gold**.
   - **↻ Detect lords** re-reads the roster (use it after loading a different save).
   - **🌾 Max anchor county** fills — and **freezes** — the anchor county's grain, cattle, sheep and wool (no more starvation). **🏰 Max the county you're viewing** does the same for whichever county screen you currently have open. All four of these goods are pinned to fixed addresses and show up in the "Your province" rows; the remaining goods (ale, stone, wood, iron) aren't pinned — use the scanner.

### Advanced — memory scanner (everything else)
Most per‑county goods (ale, stone, wood, iron…) and stats (happiness, population, army size…) aren't pinned to fixed addresses, so use the collapsible **Advanced** scanner:
1. Open the relevant in‑game screen and note the number (say grain `35`).
2. Enter `35`, **First Scan**. Change it in‑game (harvest/spend), enter the new number, **= value**. Repeat until a few addresses remain.
3. Double‑click a result to send it to the **Watch / freeze** table, then freeze or set it there.

If you don't know the exact number, click **Snapshot**, then narrow with **▲ Increased / ▼ Decreased / = Unchanged** as the value moves in‑game.

### Tips
- Gold/treasury is **Int32**. Small per‑county counts may be **Int16** — if Int32 finds nothing in the scanner, try Int16.
- The default scan range `0x00000–0xA0000` is the emulator's 640 KB conventional memory, where all game state lives.
- If Lord 1's gold is far from your screen's crowns (not just a little higher), your lord may be a different row — freeze/set that row instead.

---

## How it works (short version)

- **Attach:** `OpenProcess` on the `dosbox-x` process with read/write/query rights.
- **Locate guest RAM:** walk the process's committed private regions (`VirtualQueryEx`) for the ~16 MB block that contains a valid **BIOS Data Area** (COM1 `0x3F8`, COM2 `0x2F8` → bytes `F8 03 F8 02` at guest `0x400`), then set `guestBase = fingerprint − 0x400` and verify against an interrupt‑vector entry. This is why it keeps working across DOSBox‑X versions and relaunches instead of relying on a fragile fixed address.
- **Scan / poke:** `ReadProcessMemory` / `WriteProcessMemory` at `guestBase + linearAddress`.

Source map:

| File | Responsibility |
|---|---|
| `Native.cs` | Win32 P/Invoke declarations |
| `DosBoxMemory.cs` | Attach + guest‑RAM discovery + typed read/write |
| `LordsGame.cs` | Game knowledge: locate DGROUP by signature, read/write lord treasuries |
| `Scanner.cs` | First/next value scanning and change filters |
| `WatchEntry.cs` | A pinned address (freeze + set) |
| `MainWindow.xaml[.cs]` | UI and the 120 ms freeze/refresh loop |

Full reverse‑engineering rationale (segment `DS = 0x3E4D`, the DIVERGANCE state table, etc.) is in [`../.docs/ReverseEngineering.md`](../.docs/ReverseEngineering.md).

---

## Troubleshooting

- **"No DOSBox-X process found."** Start the game first; the process must be named `dosbox-x`.
- **"OpenProcess failed (Win32 error 5)."** Access denied — right‑click the trainer (or your terminal) and **Run as Administrator**.
- **"could not locate the emulated PC's RAM."** The game/emulator wasn't fully up, or an unusual DOSBox‑X memory configuration is in use. Make sure a match is actually loaded and try **Attach** again.
- **Scan finds nothing.** Wrong value type (try Int16), value is on a screen you haven't opened yet, or the number shown is derived/rounded — scan the raw stored value where you can (e.g. treasury total).
- **Value won't stick.** Some values are recomputed each season; freeze it rather than setting once.

---

## Legal / ethical note

This is a single‑player **learning and experimentation** tool for a game you own. It doesn't distribute or alter game files. Don't use it in modem/network multiplayer — writing memory mid‑game will trip the game's own **DIVERGANCE** desync check and ruin the session for your opponent.
