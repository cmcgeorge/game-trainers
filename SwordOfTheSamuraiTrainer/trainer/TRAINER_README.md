# Sword of the Samurai — Trainer (baked‑in EXE patches)

Cheats are **baked directly into patched copies of the game's EXEs** — no Cheat Engine,
AutoHotkey, or resident tool at runtime. You install the patched `START.EXE` and `RP.EXE`
over the originals (originals are auto‑backed‑up to `*.orig`).

This was produced by static reverse engineering in Ghidra (project `C:\ghidra_proj\SOTS`),
plus a from‑scratch **EXEPACK unpacker** (the shipped EXEs are Microsoft‑EXEPACK compressed;
DOS runs the decompressed images identically — see *How it works*).

---

## What the trainer gives you

| Cheat | Status | Where | Effect |
|---|---|---|---|
| **Personal attributes maxed** | ✅ baked in (`START.EXE`) | character creation | A new samurai starts with all five core stats at the in‑range max `0x78` (120): leadership/generalship, the two combat skills (duel + mass‑battle), the **Honor attribute**, and the land/family bonus. |
| **Honor (the stat that drives rank/promotion)** | ✅ baked in (`START.EXE`) | = one of the five attributes above | The persistent in‑game Honor *attribute* (the value the game clamps to `0x80` and ranks you by) starts maxed. |
| **Troops / army strength** | ✅ baked in (`RP.EXE`) | every battle muster | Your army handed to the BATTLE module is forced to **2000** strength (vs. the game's natural ~128 cap), so you crush field battles regardless of your real province levies. Enemy army is left honest. |
| **Money (koku / rice)** | ❌ not bakeable here | strategic/overlay code | See *Limits* below. |
| **Honor end‑game *score*** (the `honor.scl` leaderboard number) | ⚠️ derived, optional | `RP.EXE FUN_2567_0000` | Computed at retirement, not stored; can be forced but only affects the death scoreboard. Not done by default. |

> Combat auto‑win is **not** part of this trainer — you already have `AUTOWIN.EXE` for that
> (deploy it over `duel/battle/melee.EXE` as documented in `REVERSE_ENGINEERING_NOTES.md`).
> The troops cheat + AUTOWIN together = total dominance.

### New game required
These patches set values at **character creation / battle dispatch**, so start a **New Game**
after installing (there were no existing save files in the install). Maxed attributes persist
for that character; the troop override applies to every battle automatically.

---

## Install / uninstall

From the `trainer/` folder (needs `C:\Python314\python.exe`, already present):

```sh
# install (auto-backs up Game\START.EXE -> START.EXE.orig, etc.):
python build\deploy.py install ..\Game build\..\patched START.EXE RP.EXE
#   (or just copy trainer\patched\START.EXE and trainer\patched\RP.EXE over Game\, keeping .orig backups)

# check what's installed:
python build\deploy.py status ..\Game START.EXE RP.EXE

# revert to stock:
python build\deploy.py restore ..\Game START.EXE RP.EXE
```

Manual install is just: back up `Game\START.EXE`→`START.EXE.orig` and `Game\RP.EXE`→`RP.EXE.orig`,
then copy `trainer\patched\START.EXE` and `trainer\patched\RP.EXE` into `Game\`. The loader
(`OLD.COM`) runs both modules by filename, so the swap is transparent. The patched files are the
*unpacked* images (larger on disk, byte‑for‑byte the same program once loaded).

---

## The exact patches

Offsets are into the **unpacked** images (`trainer/patched/*.EXE`). Both edits were verified to
match the expected original bytes before writing, and confirmed **not** to overlap any relocation.

### START.EXE — max starting attributes
The character generator (`FUN_1000_0010`) copies a 6‑record stat template (file `0x7F46`) into the
player's Pers record; record 0 is the player. Its five stat words live at file **`0x7F54`**:

```
0x7F54:  20 00 20 00 50 00 40 00 40 00   ->   78 00 78 00 78 00 78 00 78 00
         (Swords/Generals/Honor/Land/Family, each -> 0x78 = 120)
```
`0x78` is used (not `0xFF`) because several routines treat these as **signed** bytes and the
game's own data never exceeds `0x78–0x80`. Only the player's record is touched, so rival/NPC
records are unchanged. (Clan‑trait bonuses added at generation simply clamp to the `0x80`/`0x70`
caps.) Spec: `build/start_patch.json`.

### RP.EXE — max troops
The muster→BATTLE chokepoint `FUN_23bb_00ba` divides your summed army by the battle scale and
stores it to the handoff field `COMM+0x64` (which BATTLE reads). We replace the army computation
with a constant load; the following `les bx,[0x8540]; mov es:[bx+0x64],ax` then stores it:

```
file 0x160C9:  8B 46 0A 2B D2 F7 36 A0 3D   ->   B8 D0 07 90 90 90 90 90 90
               mov ax,[bp+0x0A]; sub dx,dx;       mov ax,0x07D0 (2000); nop x6
               div word [0x3da0]
```
The value **2000** is a single immediate — easy to tune. It's overflow‑safe at every battle scale
(BATTLE multiplies `COMM+0x64` by 1/2/4/8; `2000×8 = 16000 < 65536`). Don't go above ~8000.
Only the *player* army write is patched; the enemy write (file `0x160E5`) is left honest.
Spec: `build/rp_patch.json`.

---

## Limits (honest)

Reverse engineering established that **money and the running honor total are NOT in the static
EXEs**:

- **Money (koku/rice):** RP.EXE has no treasury field, no "can‑afford/subtract" logic, and no
  `koku` text. The economy is owned by the strategic layer and lives in the **26 KB resident work
  buffer** (the block at `COMM+0x20` that RP copies in/out), manipulated by **RP.CAT overlay code**
  that isn't in any standalone EXE image. There is no constant or code site in `START.EXE`/`RP.EXE`
  to patch for koku.
- **Honor *score*** (the 190/160/… numbers on the Scroll of Honor): computed at retirement from
  province value + duel bonuses + territory, not stored as a single field.

To cheat **money**, the realistic options are (a) reverse‑engineer the RP.CAT overlays / the
work‑buffer layout and patch there, or (b) a small runtime memory poke (Cheat‑Engine/AHK style,
like your other trainers) that writes the koku field in the running DOSBox process. Say the word
and I can pursue either — the overlay route keeps it "baked in"; the memory‑poke route is faster
but is a runtime tool.

---

## How it works (reproducible)

1. **Unpack.** The shipped EXEs are Microsoft EXEPACK (`"RB"` sig + *"Packed file is corrupt"*).
   `build/unexepack.py` rebuilds a plain runnable MZ. It was validated to produce output
   **byte‑identical** (same SHA‑256) to the known‑good unpacked RP image — so the unpacked files
   are faithful and DOS loads them exactly like the originals.
2. **Analyze.** Imported into Ghidra (`x86:LE:16:Real Mode`); located the Pers stat template in
   `START.EXE` and the battle‑muster handoff + stat fields in `RP.EXE`. Offset map:
   `START file = 0x670 + DGROUP_off`; `RP file = 0x23F0 + (ghidra_seg−0x1000)*16 + off`.
3. **Patch.** `build/patch.py` applies byte edits only after verifying the original bytes (safety),
   from the JSON specs in `build/`.
4. **Deploy.** `build/deploy.py` keeps `.orig` backups and swaps the patched images in.

---

## Verify it worked (1 minute, in‑game)

Headless analysis can't see the DOSBox screen, so confirm visually:

1. Launch the game normally; start a **New Game** and create a samurai.
2. On the character / status screen, the five attribute bars should read **maxed**.
3. Trigger a field **battle**: your army should show ~2000 strength (overwhelming) while the
   enemy is normal.

If anything looks off, `python build\deploy.py restore ..\Game START.EXE RP.EXE` returns you to
stock instantly.
