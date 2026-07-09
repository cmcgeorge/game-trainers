# Shōgun Trainer

A small **WPF trainer** for the 1987 DOS game *James Clavell's Shōgun* (Synergistic
Software's IBM PC port) running inside **DOSBox‑X**. It attaches to the emulator,
finds the game's live state inside the emulated guest RAM, and lets you edit it —
cash, health, following, personality, the fail timer, even the become‑Shōgun
endgame — while you play.

It is the sibling of [`MemoryDumper`](../MemoryDumper): that tool *snapshots* a
process's memory for analysis; this one *writes* to a specific game it understands.
The mechanics it relies on are documented in
[`.docs/Shogun-Reverse-Engineering.md`](.docs/Shogun-Reverse-Engineering.md), and
there's a player's guide with a world map in
[`.docs/Shogun-Strategy-Guide.md`](.docs/Shogun-Strategy-Guide.md).

## What it can do

| Cheat | Effect |
|---|---|
| **Set / freeze cash** | Blackthorne's purse (YEN). Freeze re‑writes it every ~¼ s. |
| **Set / freeze health** | Blackthorne's hit points. Freeze = effectively invincible. |
| **Max player stats** | Sets Blackthorne's six packed personality nibbles to max. |
| **Make all NPCs friendly** | Maxes every living NPC's disposition toward you — the state you'd reach by gifting each repeatedly. Gifts land, befriending/recruiting succeeds, and they're less aggressive. Doesn't auto‑recruit. |
| **Recruit followers** | Conscripts living NPCs into your following (all 39, or *N*) by setting their master link to you. |
| **Freeze time** | Pins the *"DON'T TAKE TOO LONG"* fail countdown (`0xD0A4`) so you can't time out. |
| **Force following (open contest)** | Pushes your following tally (`0x15A0[0]`) past 19 every tick — the gate that opens the become‑Shōgun contest. Pick up any object afterwards to trigger it. |
| **Place 3 relics at the palace (win)** | Once the contest is open, writes Buddha/Scroll/Mirror (caste `0xC0`) into the world‑object table at the contest palace (`0xD0D0`), keeping the table sorted. Pick up any object there → *"WELL DONE! SHOGUN."* |
| **Give 3 relics (pockets)** | Puts the relics in your inventory. Handy to carry, but see the caveat — it does **not** win on its own. |
| **Go to area → Route** | Type a screen number and get **turn‑by‑turn** directions from your current area, routed around impassable screens (BFS over the world's 128‑screen grid). Falls back to a straight‑line hint if the target is on a disconnected part of the map. |
| **Go to area → Teleport** | *Experimental.* Warps you to that screen by writing the current‑area global + your location byte. Save first — an unreachable screen could strand you. |

The panel also shows live readouts: current area, fail‑timer value, and contest
tally. All editable values are single bytes (0–255).

> **On followers:** setting an NPC's master link to you *sticks* (the game never
> reverts it), so *Recruit all* really allies all 39 — but only NPCs on your **current
> screen** render as followers (~10 at a time); that's a display limit, not a cap. The
> become‑Shōgun *contest*, however, is gated by a **separate tally** the game
> re‑derives, so recruiting alone may not open it — that's what **Force following** is
> for.

## The fastest win

1. **Attach**, then tick **Freeze time** (insurance) and **Force following** — the
   contest tally should read `25 / 20 ✓`.
2. In game, **pick up any object** → you're sent to a palace (*"DON'T TAKE TOO LONG!"*).
3. Click **Place 3 relics at the palace (win)**.
4. **Pick up any object** at the palace → *"WELL DONE! SHOGUN — ALL JAPAN HONORS YOU."*

## Build & run

Requires the **.NET 8 SDK** (Windows Desktop). From the repo root:

```powershell
.\run.ps1                 # build Release and launch (prompts for Administrator)
.\run.ps1 -NoRun          # build only; prints the exe path
.\run.ps1 -Publish        # one self-contained ShogunTrainer.exe (win-x64)
```

Or straight through `dotnet`:

```powershell
dotnet build src\ShogunTrainer\ShogunTrainer.csproj -c Release
```

The build is **x64** (so it can read the 64‑bit DOSBox‑X address space) and its
manifest requests **Administrator** — reading/writing another process's memory needs
`PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION`. Launch from an elevated
terminal to skip the UAC prompt.

## Usage

1. Start Shōgun in DOSBox‑X and get **in‑game** (past the title screen).
2. Launch the trainer and click **Attach**. It finds the `dosbox-x` process, scans its
   private guest RAM for the entity table, and shows Blackthorne's live stats.
3. Toggle freezes or click the cheat buttons. The status line shows the process id and
   the table address it locked onto; the log narrates each action.

If you reload a save or restart the game, the trainer re‑locates everything on the
next tick (addresses move between runs — it never hard‑codes them).

## How it works

The game keeps **40 fixed entities** of **32 bytes** each in one contiguous table;
**entity 0 is always Blackthorne (the player)**. That table lives inside DOSBox‑X's
~16 MB emulated guest RAM, and both the guest‑RAM base and the DOS load segment move
between launches — so nothing is hard‑coded; the trainer finds everything by signature.

**Finding the live table.** Each entity's *owner* byte (`+0x11`) is a valid index
`0..39`, facings are idle, and entity 0 owns itself. That shape isn't unique on its
own — DOSBox‑X keeps **savestate/rewind copies** of guest RAM in its heap — so the
trainer also requires the real, **sorted world‑object table** (`+0xBA0`) to follow the
candidate. Only the live guest‑RAM table has that; copies hold unrelated heap bytes
there. This survives recruiting (which zeroes owner bytes and would break a naive
diagonal check) and lands the live table in a few hundred milliseconds.

**Two segments.** The game uses two data segments. The entity table and the world
tables (terrain, objects, the following tally) are in one; the `0xD0xx` **globals** and
the text buffer are in a second segment exactly `0x3000` bytes lower. Once the entity
table is found, the trainer derives the globals base as `table − 0x3900` and validates
it (the player‑index global `0xD0B8` must read 0) before touching the timer/contest.

### Entity record (32 bytes)

| Offset | Field | Used for |
|---|---|---|
| `0x00` | area/location marker (== the current‑area global for the player) | |
| `0x01`/`0x02` | X / Y tile | |
| `0x04` | disposition/attention (bit `0x80` flag; low 7 bits raised by gifts) | *make friendly* |
| `0x07` | facing (low nibble `0xF` = idle) | signature corroboration |
| `0x08`/`0x09`/`0x0A` | six packed personality nibbles (`0x09` low = friendliness) | *max stats*, *make friendly* |
| `0x0B`–`0x0E` | 4 inventory slots (`caste \| id`) | *give relics* |
| `0x0F` | **cash / YEN** | *set/freeze cash* |
| `0x10` | **hit points** (0 = dead) | *set/freeze health* |
| `0x11` | **owner / master** (== self when independent) | *recruit* & signature |
| `0x17` | secondary master link | *recruit* |

### World tables & globals (offsets from the entity table base)

| What | Where | Used for |
|---|---|---|
| Following tally (per entity) | `+0xCA0` (`DS:0x15A0`), `[0] > 19` opens the contest | *force following* |
| World‑object table | `+0xBA0` (`DS:0x14A0`), 64 × `[loc, caste\|id, 0, 0]`, sorted by loc | *place relics*, signature |
| Current area (`0xD0C2`) | `− 0x3900 + 0xD0C2` | area readout |
| Player index (`0xD0B8`, == 0) | `− 0x3900 + 0xD0B8` | globals validation anchor |
| Contest palace (`0xD0D0`) | `− 0x3900 + 0xD0D0` | *place relics* target |
| Fail timer (`0xD0A4`, word) | `− 0x3900 + 0xD0A4` | *freeze time* |

Object bytes are `caste (top 3 bits) | id (low nibble)`; the victory tally counts
caste `0xC0` objects — the three relics are **Buddha `0xCD`, Scroll `0xCE`, Mirror
`0xCF`**.

## Layout

```
src/ShogunTrainer/
  Memory/ProcessMemory.cs   P/Invoke: OpenProcess, VirtualQueryEx, Read/WriteProcessMemory,
                            committed-region enumeration
  Game/ShogunGame.cs        game constants, the 40-name roster, entity/world/global
                            offsets, and the live-table signature
  Game/TrainerEngine.cs     attach, locate the table + globals, apply cheats, enforce freezes
  MainWindow.xaml(.cs)      the UI
  App.xaml(.cs)             app shell
run.ps1                     build/run helper
```

## Notes & caveats

- **Your own machine, your own save.** The trainer only writes to a DOSBox‑X you are
  running locally. It does no network or disk I/O.
- Cash and health are **bytes** — max 255. "Infinite" is really "pinned at 255/254".
- **Freeze time** targets `0xD0A4`, the countdown identified in the RE analysis. Watch
  the timer readout tick down as you play to confirm it's the right one on your build.
- **Give 3 relics** only fills your **pockets**; the win check reads the *world‑object
  table*, not your inventory. To win, use **Place 3 relics at the palace** (after the
  contest is open) — or collect and carry the real relics there yourself.
- If **Attach** says the table wasn't found, make sure you're actually in‑game (past
  the title screen), and try running the trainer as Administrator.
```
