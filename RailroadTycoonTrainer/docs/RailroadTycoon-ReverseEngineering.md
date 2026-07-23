# Railroad Tycoon — reverse-engineering notes

How the trainer knows where the player's money lives. Everything below was recovered from **our**
copy of the game — `GAME.EXE` v455.00 — by two complementary methods: a **static** teardown (unpack
the executable, disassemble it in Ghidra) and a **live** teardown (run the game under an
AI-debugger-enabled DOSBox-X and watch memory change). A fact is marked **[Confirmed]** when it was
verified directly — either the two methods agree, or one method proved it decisively (e.g. a live
write-test that drove the on-screen display); the confirming method is named alongside.

## The target

| | |
|---|---|
| Game | Sid Meier's Railroad Tycoon (MicroProse, 1990) — the **original**, not Deluxe (1993) |
| Executable | `GAME.EXE`, 197,182 bytes, **v455.00** (the byte size is the version fingerprint; a "1994" file date is a repack) |
| Packing | **Microsoft EXEPACK** — the on-disk image is compressed and self-extracts at load |
| Toolchain | **Microsoft C** (5.x era, `Copyright (c) 1988`), **overlay-linked** (39 overlay modules in a 109 KB tail) |
| Data model | large model; `DS` = the single data group **DGROUP**, proven `= 0x164B` by the CRT startup `mov ax,0x164B; mov ds,ax` |
| Runtime | real-mode DOS under **DOSBox / DOSBox-X** |
| Save files | `RR<n>.SVE` (variable size; our sample 16,017 B) + `RR<n>.MAP` (13,035 B), `n` = 0..3 |
| Cash | **signed 16-bit word at `DGROUP:0x957A`, in units of $1,000** — the headline value |

## The gift: DGROUP is static, so offsets are constant

Railroad Tycoon is almost entirely **static memory** — the disassembly shows only a couple of runtime
allocations during play. The player's state lives in fixed globals inside DGROUP, and because this is a
real-mode Microsoft-C build, **every global sits at a compile-time-constant offset from `DS`**. That is
the whole basis for the trainer's one-click auto-locate: find DGROUP once and the cash is at a known
offset from it — no per-value memory scan.

The one thing that varies between runs is the absolute segment DOS loads the program at (it depends on
resident drivers), i.e. the numeric value of `DS`. That cancels out: the trainer anchors on a text
string whose DGROUP offset is known, so `dgroupBase = anchorHit − anchorOffset` yields the base
directly, whatever `DS` happens to be that session. (In one live session `DS` was `0x1E6F`, not the
`0x164B` the static image implies — same layout, different load address, identical offsets.)

## Finding the cash (live)

Static analysis alone could **not** pin the cash: Railroad Tycoon never `printf`s money (it uses a
custom comma-formatter, so there is no `"$%ld"` string to anchor on), and — see below — cash is not a
discrete field in the save file either. So it was found live, by the classic trainer method:

1. In-game with **$1,000,000**, search the DOS guest's RAM for the value. A 32-bit `1000000` returned
   nothing; a 16-bit **`1000`** returned 16 candidates. → money is stored **in thousands**, as a word.
2. Write a **different, recognisable** value to each candidate (2000, 3000, 4000, …) and watch the
   on-screen cash panel. Writing **`2000` to guest-linear `0x27C6A`** changed the display to
   **"$2,000,000"** — that word drives the display.
3. Convert to a DGROUP offset with that session's `DS` (`0x1E6F`): `0x27C6A − 0x1E6F0 = 0x957A`.

**[Confirmed]** the player's cash is a **signed `int16` at `DGROUP:0x957A`**, value × $1,000. A word of
`0x03E8` = 1000 shows as "$1,000,000".

> A separate, third-party disassembly of a *different* build reported cash at `0x95AA`. Our build is
> `0x957A` (its `0x95AA` reads as zero here). This kind of build drift is exactly why the trainer keeps
> a value scanner as a fallback — a number search finds the cash regardless of which build you run.

### The $30,000,000 ceiling

The game clamps cash to **`0x7530` = 30000 = $30,000,000** during its own accounting. A larger poke
*holds at rest* (the field is a plain word) but the next fiscal pass can snap it back, so the trainer's
"Set max cash" targets exactly $30M and the docs advise freezing at or below it.

## The year (live + static, agree)

The current calendar year is an unsigned word at **`DGROUP:0x96C0`** — read live as `0x0726` = **1830**
at the start of an Eastern-US game, and independently identified in the disassembly as the year global
(the game gates locomotive/industry eras on it at 1800 / 1830 / 1865). **[Confirmed].** Freezing it
stops the calendar, so the difficulty's year limit can never end the game — a "play forever" cheat.
It sits `0x146` bytes after the cash word, both inside DGROUP's BSS (`0x8320`–`0xFF5F`).

## The anchor strings (how auto-locate finds DGROUP)

Two distinctive financial-report label literals live in DGROUP's initialised data at fixed offsets:

| String | DGROUP offset |
|---|---|
| `"Outstanding Loans: "` | `0x24A8` |
| `"Stockholders Equity: "` | `0x24BC` |

`GameLocator` scans the whole DOSBox host process for the first string (the DOS guest's RAM is mapped
verbatim into the emulator, so the string appears at `hostBase + guestLinear`), treats each hit as a
candidate `dgroupBase = hit − 0x24A8`, and accepts it only if **the second string is also at its known
offset** *and* **the year word holds a plausible value (1800–2100)**. Because the on-disk EXE is
EXEPACK-compressed, these plaintext strings exist **only once** in memory — in the decompressed guest
DGROUP — so a false positive is very unlikely; the second string and the year check make it
vanishingly so. Then cash is read at `dgroupBase + 0x957A`.

**Verified live:** against the running game, `GameLocator.Locate()` found the segment in ~220 ms, read
cash = 1000 (\$1,000,000) and year = 1830, and a round-trip write (poke 12345 → read back 12345 →
restore) succeeded.

## The save file (why there is no save editor)

The save/load code is **overlay #39**; the filename is built at runtime into the buffer at `DS:0x4C64`
("`a:rr0.sve`", with the drive letter and slot digit poked in). The writer emits, in order:

1. an **80-byte header** (`DS:0x895C`) — the slot's on-screen summary (railroad name, difficulty and
   start year, current rating and year, as ASCII; a leading space marks an empty slot);
2. a **byte stream** from the bulk state block (far segment `0x122A`, whose selector lives at
   `DS:0x639E`), length = a runtime counter at `DS:0x45AE`;
3. two **4-byte-record tile streams** — track tiles, then station/signal-tower tiles — each length
   driven by a live counter (`DS:0x9570`, `DS:0x0EAA`).

Only the header is a fixed file→memory mapping; sections 2–4 are **position-independent, count-keyed
streams**, so the file's total size varies with how much you have built (our sample is one populated
map, not a fixed layout). Crucially, **cash is not written as a discrete field** — it is part of the
bulk state block. That, plus the fact that the offset was recovered from a single sample, is why the
trainer edits **live memory only** and ships **no save editor** (editing an unverified, variable-length
serialization would risk corrupting saves). The companion `.MAP` file holds the terrain grid (a
`PIC`-format image); the save overlay was not traced writing it, and no `.MAP`/`.map` filename string
appears anywhere in the executable, so its exact write path is unconfirmed — another reason the trainer
stays out of the save files entirely.

## Copy protection is still here

The startup **"IDENTIFY THIS LOCOMOTIVE"** quiz is **overlay #34** and is **active** in this build. The
overlay carries the prompt, the `elocos*.pic` artwork, and both outcome messages — pass:
*"Congratulations, your vast knowledge…"* (seen live), fail: *"Perhaps you should consult your manual
again…"*. Answering correctly is required to reach a playable game; the trainer does not touch this
(the **Locomotives** reference tab is the answer key). Note the game keeps **two** engine name tables:
the buy-list names (`0-4-0 Grasshopper` … `2-6-6-2 Mallet`, `'F' Series Diesel`, `'GP' Series Diesel`)
and the shorter **quiz** aliases (which call the Mallet **"Challenger"** and the F-Series
**"F3A-Series"**, and mix the US and England rosters into one option list).

## Other globals worth knowing (DGROUP-relative)

| Offset | Meaning | Confidence |
|---|---|---|
| `0x957A` | **Player cash** (int16, ×$1,000) | **Confirmed (live write-test)** |
| `0x96C0` | **Current year** (uint16) | **Confirmed (live + static)** |
| `0x4C64` | Save-filename buffer (`"a:rr0.sve"`) | static |
| `0x639E` | Selector of the bulk state block (= `0x122A`) | static |
| `0x45AE` / `0x9570` / `0x0EAA` | `.SVE` section byte / track / station counts | static |
| `0xBF50` | word[] tile-position array (track + stations) | static |
| `0x0C18` / `0x8C60` | rank-title pointer table / player rank index | static (med) |

## How this maps to the trainer

- **`Game/RtLayout.cs`** — the constants above (cash/year offsets, the two anchor strings and their
  offsets, the $30M cap) plus pure validators (`ValidateSegment`, `IsPlausibleYear`) and the
  thousands↔dollars conversions.
- **`Game/GameLocator.cs`** — the scan-anchor-validate-read routine described under *anchor strings*;
  returns the cash/year host addresses with no value scan.
- **`ViewModels/LiveScannerViewModel.cs`** — attaches to the DOSBox process, drives auto-locate and the
  Cheat-Engine-style value scanner (the build-independent fallback), and freezes pinned values against
  the fiscal tick.

## Reproducing the teardown

- **Live:** the repo's modified DOSBox-X exposes an AI debug server (TCP `127.0.0.1:2999`). Mount the
  game and run it (`mount c <RAILROAD dir>`, `GAME`), then use `MEMFIND`/`MEMS` to search for
  *dollars ÷ 1000* as a word, `SM` to write a sentinel, and confirm the on-screen panel — the exact
  path used to find `0x957A`.
- **Static:** unpack the EXEPACK image (standard backward-RLE unexepack + relocation rebuild), import
  the result into Ghidra with the MZ loader, and read the CRT startup for the DGROUP segment; the year
  global falls out of the era-gate comparisons.
