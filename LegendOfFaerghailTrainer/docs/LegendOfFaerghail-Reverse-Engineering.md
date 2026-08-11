# Legend of Faerghail — reverse-engineering notes

Everything here was recovered from the PC/DOS conversion of *Legend of Faerghail* (Electronic
Design Hannover / reLINE Software GmbH, build stamped `19.06.1990`) as shipped in the 1992
hard-disk release, running under DOSBox 0.74-3 with `memsize=16`.

Confidence is marked throughout:

| Marker | Meaning |
| --- | --- |
| **[Confirmed]** | Pinned against the running game — either read out of live memory and matched to the number the game printed on its own character sheet, or written with a sentinel and watched change on screen. |
| **[Inferred]** | Consistent across every shipped record and every live read, but never proved on screen. |
| **[Unidentified]** | Located but not understood. Named so it is not silently treated as padding. |

---

## 1. The target

`LOF.EXE` is a plain, **unpacked** MZ image — no LZEXE, no EXEPACK, no SEA-AXE, which makes it the
easy case among the DOS trainers in this repository.

| Header field | Value |
| --- | --- |
| File size | 315,152 bytes |
| `e_cparhdr` | `0x054F` paragraphs (header = 0x54F0 bytes) |
| `e_crlc` | 5,428 relocations |
| `e_cs:e_ip` | `2F0E:0010` |
| `e_ss:e_sp` | `47BB:0800` |
| `e_minalloc` | `0x02C3` paragraphs |

5,428 relocations is what tells you it is not packed: a compressed stub carries none. The
run-time is **Microsoft C 1988** (`MS Run-Time Library - Copyright (c) 1988, Microsoft Corp`,
plus the `R6000`/`R6001`/`R6003` error strings and a `;C_FILE_INFO` marker), built in the **large
memory model** — many code segments, one data group.

Two more facts sit in plain text near the end of the image: the build stamp `19.06.1990`, and
`Please start LOF with START.BAT!`. `START.BAT` runs `VOR.EXE` (the intro) and then `LOF.EXE`;
starting `LOF.EXE` directly aborts.

The shipped copy is a cracked release — `NEMESIS.NFO` is dated 10 April 1992, and the eleven
characters in `ROST\ROST` are named after the cracking group (`Stiletto`, `Lord Krynn`, `Shadow`),
not after anything Rainbow Arts wrote.

---

## 2. Method

The static route stalled almost immediately and the live route did all the work. That is worth
stating plainly, because it is the opposite of what the file layout suggests.

**Ghidra** (12.1.2, headless, `x86:LE:16` real mode) auto-analysed the image in 87 seconds and
recovered **404 functions** from a 315 KB program. It cross-referenced almost none of the game's
strings, because a large-model 8086 binary reaches its literals through far pointers that the
constant-propagation analyser cannot follow. Searching the code for the 16-bit immediate of a
string's `DS:` offset finds nothing either — the operands are not there.

What Ghidra *did* give, and what turned out to be the single most valuable thing in the whole
teardown, was the record stride and the two globals: the decompiled output contains
`(int)idx * 0x19a + *(int *)0x30` **1,337 times**. That is a 410-byte record indexed off a far
pointer at `DS:0x0030`. A second pointer, `DS:0x3FF6`, appears twelve times. Ghidra also decompiled
the game's own **debug menu** (its strings — `Debugmode activ!`, `Enter room-number?` — are still
in the image), which adds 1,000,000 to one 32-bit record field and 100,000 to another; that pinned
the experience field before any live reading started.

The live route needed two tricks, because the machine's desktop session was disconnected part-way
through and neither screenshots nor `SendInput` worked any more:

- **Driving the game.** `LOF.EXE` reads the keyboard through BIOS `INT 16h`, so a keystroke can be
  injected by writing DOSBox's guest RAM directly: put the ASCII/scancode word at `40:001E + tail`
  and advance the tail at `40:001C`. The head pointer moving afterwards proves the game consumed it.
- **Seeing the game.** DOSBox with `output=surface` renders into an ordinary software SDL surface —
  for this game a 640 × 400 × 32bpp buffer, exactly 1,024,000 bytes, sitting in its own private
  commit. Reading that block and saving it as a PNG gives a screenshot with no display involved.

With those two in place the rest was ordinary: write a sentinel into a record byte, open the
character sheet, read the number back off the rendered screen.

The workspace scripts that do all of this live in the git-ignored `.docs/` folder
(`Dump-Memory.ps1`, `Guest.ps1`, `Send-BiosKey.ps1`, `Render-Surface.ps1`, `Play.ps1`, and a set of
small Python analysers).

---

## 3. Memory map

DOSBox keeps its guest RAM in one private read/write commit — 16,781,312 bytes for `memsize=16` —
but **guest linear 0 is not the base of that commit**: the allocation is padded (0x20 bytes in the
build measured). Anchor on the BIOS data area instead: `40:0000` holds the COM1 port (`0x03F8`) and
`40:0013` the conventional-memory size in KB (`640`, i.e. `0x0280`). Both were confirmed live.

The program loads at paragraph `0x1A2` (guest linear `0x1A20`) in the session measured. Nothing in
the trainer depends on that number — it moves with the DOS environment — but it is what makes the
file-to-guest arithmetic below reproducible:

```
guest linear = file offset - 0x54F0 (header) + 0x1A20 (load base)
```

### DGROUP

Large model gives the program exactly one data group, so **every global has a constant `DS:`
offset** even though the load segment moves. DGROUP was located by finding the two far pointers
Ghidra had named and searching the dump for values that resolve to the party and roster addresses:

- the party pointer sat at guest `0x38230`, so `DGROUP = 0x38230 − 0x30 = 0x38200`
- the roster pointer then landed at `0x38200 + 0x3FF6 = 0x3C1F6` — which is exactly where the scan
  found it. Two independent confirmations of the same base.

That fixes the file arithmetic for anything in the data group:

```
DS offset = file offset − 0x3BCD0
```

Ten literals were checked against a live 16 MB guest under that rule and all ten matched:

| String | File offset | `DS:` offset |
| --- | --- | --- |
| `Negotiating` | `0x41D52` | `0x6082` |
| `Warrior    ` | `0x41E98` | `0x61C8` |
| `War scythe` | `0x42D0C` | `0x703C` |
| `Burning hands` | `0x44430` | `0x8760` |
| `Load which gamestanding:` | `0x498B4` | `0xDBE4` |
| `Hit point ` | `0x4AF0D` | `0xF23D` |
| `Negotiating ability.... ` | `0x4B041` | `0xF371` |
| `Common language` | `0x4B160` | `0xF490` |
| `ROST\ROST` | `0x4B5DF` | `0xF90F` |
| `R6000` | `0x4CE38` | `0x11168` |

(The last one is past 64 KB, so it belongs to a far segment following DGROUP rather than to DGROUP
itself; the linear arithmetic still holds because the whole image is loaded contiguously.)

### The two globals that matter

| `DS:` offset | Contents |
| --- | --- |
| `0x0030` | far pointer → party slot 0 — **6** records of 410 bytes |
| `0x3FF6` | far pointer → roster slot 0 — **32** records of 410 bytes |

Both buffers are heap allocations, so their addresses change every session (measured at
`0x519E4` and `0x4E6A2` in one run). They are **adjacent in one allocation**, two bytes apart:

```
party = roster + 32 × 410 + 2      (0x519E4 − 0x4E6A2 = 0x3342)
```

The trainer uses that as a cross-check on the pointers rather than as a way to find them.

The roster size of 32 is not a guess: it matches the manual ("A maximum of 32 characters may be
found and saved"), and slots 11–31 in the live buffer are all unused entries whose name field reads
`__________`.

---

## 4. The character record (410 bytes)

The same 410-byte record is used for a party slot, a roster slot, and a party slot inside a saved
game. All multi-byte fields are **little-endian** — the game began life on the Amiga, but the PC
conversion is a native 8086 build and nothing is byte-swapped.

| Offset | Size | Field | Confidence |
| --- | --- | --- | --- |
| `+0x00` | 1 | Occupied flag: 1 = in use, 0 = empty slot | **[Confirmed]** — the game's own party loops test it |
| `+0x01` | 14 | Name, NUL-terminated. The game prints at most 10 characters and its entry screen writes at most 10 | **[Confirmed]** |
| `+0x0F` | 1 | 1 in every live record | [Unidentified] |
| `+0x10` | 2 | `0xFFFF` in every player record, `0x0000` for the NPC | [Unidentified] |
| `+0x16` | 1 | `0xFF` in every player record | [Unidentified] |
| `+0x17` | 1 | **Level ("Rnk")**. 0 marks a non-player character | **[Confirmed]** — sentinel 8 printed `Rnk 08` |
| `+0x18` | 1 | Sex: 0 = female, 1 = male | **[Confirmed]** |
| `+0x19` | 1 | Alignment: 0 = lawful, 1 = chaotic | **[Confirmed]** |
| `+0x1A` | 1 | **Race** 0–5 | **[Confirmed]** — sentinel 3 turned a Human into a Halfling on screen |
| `+0x1B` | 1 | **Trade** 0–12 | **[Confirmed]** — matches the tavern's Recruit list for all eleven shipped entries |
| `+0x1C` | 1 | 100 for every player character, 65 for the NPC | [Unidentified] |
| `+0x1D` | 1 | 10 or 30, class-dependent | [Unidentified] |
| `+0x1E` | 1 | **Armour protection %** — the number in the party portrait box | **[Confirmed]** |
| `+0x1F` | 1 | **Health state** 0–7 (Good…Dead) | **[Confirmed]** — a character at 0 HP reads 7 |
| `+0x20` | 2 | **Maximum hit points** | **[Confirmed]** |
| `+0x22` | 2 | **Current hit points** | **[Confirmed]** |
| `+0x25` | 1 | Ability: **Negotiating** % | **[Confirmed]** |
| `+0x27` | 1 | Ability: **Attack** % | **[Confirmed]** |
| `+0x28` | 1 | Ability: **Defence** % | **[Confirmed]** |
| `+0x2B` | 1 | Ability: **Concentration** % | **[Confirmed]** |
| `+0x2D` | 1 | Ability: **Pick-pocketing** % | **[Confirmed]** |
| `+0x30` | 1 | Ability: **Stalking / Sneak** % | **[Confirmed]** |
| `+0x32` | 1 | Ability: **Trap detecting** % | **[Confirmed]** |
| `+0x34` | 1 | Ability: **Trap disarming** % | **[Confirmed]** |
| `+0x36` | 1 | Ability: **Lock picking** % | **[Confirmed]** |
| `+0x44` | 1 | **Constitution** | **[Confirmed]** |
| `+0x45` | 1 | **Strength** | **[Confirmed]** |
| `+0x46` | 1 | **Dexterity** | **[Confirmed]** |
| `+0x47` | 1 | **Intelligence** | **[Confirmed]** |
| `+0x48` | 1 | **Wisdom** | **[Confirmed]** |
| `+0x60` | 1 | A copy of Constitution in every record seen | [Unidentified] |
| `+0x64` | 2 | **Maximum load**, in tenths of a pound | **[Confirmed]** — 5300 prints `0530` |
| `+0x66` | 2 | **Carried load**, in tenths of a pound | **[Confirmed]** — 289 prints `0028` |
| `+0x68` | 1 | **Maximum magic points** | **[Confirmed]** — sentinel 104/66 printed `Magic 0066 / 0104` |
| `+0x69` | 1 | **Current magic points** | **[Confirmed]** |
| `+0x6A` | 1 | **Spell-list high-water mark** — one past the highest occupied spell slot | **[Confirmed]** |
| `+0x6B` | 1 | **Inventory high-water mark** — one past the highest occupied inventory slot | **[Confirmed]** |
| `+0x6C` | 4 | **Experience** | **[Confirmed]** — printed with `%011ld` |
| `+0x70` | 2 | **Rations** | **[Confirmed]** |
| `+0x72` | 4 | **Gold** | **[Confirmed]** — printed with `%05ld$` |
| `+0x76` | 4 | A second 32-bit counter the debug menu raises by 1,000,000 | [Unidentified] |
| `+0x7A` | 8 | **Languages** — one byte each, non-zero = spoken | **[Confirmed]** |
| `+0x82` | 192 | **Inventory** — 48 slots × 4 bytes | **[Confirmed]** for the fields below |
| `+0x142` | 88 | **Spells** — 44 slots × 2 bytes | **[Confirmed]** |

### Attribute order

The record stores **Con, Str, Dex, Int, Wis**, while the character sheet prints **Str, Con, Dex,
Int, Wis**. Getting that backwards swaps a warrior's two most important numbers, so it is worth
saying twice. Confirmed against three separate live sheets.

### The abilities are not an array

The nine trained abilities are at `0x25, 0x27, 0x28, 0x2B, 0x2D, 0x30, 0x32, 0x34, 0x36` — spacings
of 2, 1, 3, 2, 3, 2, 2, 2. That is not a mistake in the reading. It was measured twice with
different data: first against the shipped values (10, 8, 7, 39, 0, 0, 5, 5, 0 for the roster's
Connar), then by writing an ascending `0x0B…0x1C` ramp across `0x24`–`0x36` and reading the sheet
back (12, 14, 15, 18, 20, 23, 25, 27, then 41 for the byte at `0x36`). Both datasets fit the same
nine offsets exactly, and no uniform-stride model fits either. The bytes in between belong to
fields that appear on no sheet page.

A 16-bit reading — each ability as a little-endian word one byte lower whose high byte is the
displayed percent — fits the data equally well and would make the low byte a fractional
"progress" accumulator, which the game's `(Lock picking improves!)` messages make plausible. The
two readings are indistinguishable from the outside (the high byte of a little-endian word *is* the
byte at offset + 1), so the trainer writes the percent byte and leaves its neighbour alone.

### Languages

Eight bytes at `+0x7A`, in the order Common, Animal, Orc, Lizard, Dwarven, Elven, Dark, Magic.
Writing 1 across the range made all eight lines appear on sheet page 5. The order was pinned by the
shipped data rather than by assumption: the Half-Orc (`Connar`, race 5) is the only character with
`+0x7C` set, and the Dwarf (`Gorth`, race 4) is the only one with `+0x7E` set. The shipped records
store **2**, not 1, for a spoken language; the display only tests for non-zero.

### The count bytes are high-water marks, not populations

`+0x6A` and `+0x6B` read exactly like "number of spells known" and "number of items carried" in
every shipped record, because every shipped record has its lists packed from slot 0. That reading
is wrong, and the game corrected it: handed the Count's Amulet on leaving Thyn, a character
carrying three items in slots 0–2 received it in slot **9**, and `+0x6B` went to **10**, not 4.

So the byte is how far the game scans, not how many entries it will find — which is also why the
game can put a quest item in a far slot and still list it. The practical consequence for a trainer
is direct: write an item into slot 20 without raising this byte and the game never scans that far,
so the item is invisible. The trainer therefore recomputes both marks after every slot edit.

### Inventory

Four bytes per slot:

| Byte | Meaning |
| --- | --- |
| `+0` | Item id into the game's item table |
| `+1` | In-use flag — the `E` the inventory page prints beside worn or wielded gear |
| `+2` | [Unidentified], zero in everything observed |
| `+3` | Condition % — the `100%`, `96%` the inventory page prints |

`0x142 − 0x82 = 0xC0 = 48 × 4`, so the array fills the gap to the spell list exactly. That the
array really is that long rather than a short packed list was settled by the game itself: the
Amulet the town guard hands you at the start of the journey landed in **slot 9** of a character who
was carrying three items.

### Spells

Two bytes per slot: the spell id, then the **uses left today**. Writing 99 into the second byte of
a Magician's first slot changed his sheet from `Burning hands 8/ 8` to `Burning hands 99/ 8`, which
is what proves the byte is the current count and not the maximum. Where the maximum comes from was
**not** determined — it is not stored beside the id, and it is not in the record.

---

## 5. Reference tables inside the executable

All of these are plain arrays of NUL-padded fixed-width names, and all of them are in DGROUP.

| Table | File offset | Stride | Entries |
| --- | --- | --- | --- |
| Abilities | `0x41D52` | variable | 9 |
| Languages | `0x41DBE` | variable | 8 |
| Sex | `0x41E28` | 12 | 2 |
| Races | `0x41E4A` | 12 | 6 |
| Trades | `0x41E98` | 12 | 12 + `??` |
| Alignment | `0x41F28` | 12 | 2 |
| Trades (short) | `0x42101` | 11 | 13 |
| Health states | `0x42190` | 6 | 8 |
| Combat actions | `0x42204` | 12 | 5 |
| Regions | `0x42240` | 22 | 8 |
| Compass headings | `0x422F0` | 22 | 4 |
| Times of day | `0x42348` | 22 | 8 |
| Morale | `0x423F8` | 13 | 5 |
| **Items** | `0x42CCC` | **32** | **186** |
| **Spells** | `0x4440C` | **36** | **142** (ids 1–141 real) |
| Traps | `0x4B60C` | variable | 9 |

### Fixing the item and spell base offsets

Both tables begin with a **blank sentinel at id 0**, which is easy to get wrong by one record. The
bases above are the ones the running game agrees with:

- The shipped `Connar` carries item ids **27, 34, 13** and his inventory page lists *Leather
  armour*, *Small shield*, *Short sword*. That only lines up if the table starts one record before
  the first named entry, making `Club` id 1.
- A live Magician's record held spell ids **1** and **2** while his sheet listed *Burning hands* and
  *Light* — the same one-record shift.

Independently: `Assanla`'s third item is id 42, and with this base that is *Thieves' picks*, which
is exactly what a Rogue starts with. With the naïve base it would have been *Anvil*.

Of each 32-byte item record, the first 16 bytes are the name and the word at `+0x14` is the shop
price (`Leather armour` 150, `Small shield` 50, `Two handed swrd` 456 — the priciest weapon). The
other twelve bytes are **[Unidentified]**: `+0x12` reads as a sensible weight for armour (Leather 15,
Small shield 5, Large shield 8 — matching the `lb` figures on the inventory page) but as a sensible
*damage* figure for weapons (Club 5, Broadsword 18, Two-handed sword 25), and the same field cannot
be both, so neither is claimed.

Spell ids 128–141 kept their **German** names in this English build (`Rang erniedrigen` = reduce
rank, `Drachenatem` = dragon breath, `Wiedererwecken` = resurrect). They are monster and event
effects rather than spells a character learns.

---

## 6. File formats

### `ROST\ROST` — the tavern's saved characters

```
+0x000  1 byte    count of stored characters (0x0B = 11 in the shipped file)
+0x001  N × 414   entries
```

Each 414-byte entry is a **410-byte character record followed by four bytes** that are not part of
the record — the in-memory roster array uses a stride of 410, not 414, which is how the extra four
were isolated. Their values differ per character (`8a 06 00 00`, `ee 05 00 00`, …) and were not
identified.

The in-memory roster matched the file byte-for-byte for all eleven entries, which is what proves
the game reads the file verbatim into its array.

### `GAMES\GAMEn` — a saved game

13,134 bytes, fixed:

```
+0x0000   1 byte     0x00
+0x0001   3 bytes    "FOL" signature
+0x0004   6 × 410    the party, in slot order
+0x09A0   10,670     world state
```

The signature is genuinely distinctive: `\0FOL` occurs **exactly once** in a live 16 MB guest.

The world state was not decoded. What is known: the four shipped saves differ from a freshly
written one in only 32 bytes, most of them in a run near `+0x2680` that looks like map or
event-flag data; the byte at `+0x9B6` is 7 in the shipped saves and 4 in a save made by a
four-character party, which makes it a plausible party count but not a confirmed one.

### The save/load I/O buffer

Saving and loading both go through **one scratch buffer**, not through the live arrays: the whole
13,134-byte file appears verbatim at a fixed heap address (`0x85A7C` in the session measured), the
party is copied into it on save and out of it on load. That distinction matters — an early pass at
this teardown found the buffer first and nearly mistook it for the live party. The tell was that
walking around changed nothing inside it, and that the live party array is somewhere else entirely.

### This release cannot load a saved game

Worth recording because it looks like a trainer bug and is not: **this hard-disk release refuses to
load any saved game, including one it has just written itself.** Loading any slot prints

```
The file GAMES\GAME4 is not a valid game:
```

while saving works and produces a well-formed 13,134-byte file. Ruled out: the party-count byte at
`+0x9B6` (patched to match, still rejected), a missing `GAMEn.SAV` marker file in the game
directory (created, still rejected), and `DSKTAB.DFS` (unchanged by a save — it is 140 zero bytes
before and after). The game's floppy-era disk-identity check (`diskx.id`, `disk%d.id`,
`DISK0.ID`) is the remaining suspect and was not chased further.

The practical consequence is that **the trainer ships no save editor.** A save editor whose write
path cannot be round-tripped through the game is an unverified write, and this repository does not
ship those. Edit the live party instead.

---

## 7. How the trainer locates the game

1. Sweep the emulator's committed regions (≥ 1 MB) for `Negotiating ability.... ` — 24 bytes,
   present exactly once in a live guest. `DGROUP = hit − 0xF371`.
2. Require at least **two of four** further literals to land on their own DGROUP offsets:
   `Hit point ` at `0xF23D`, `Common language` at `0xF490`, `Warrior    ` at `0x61C8`,
   `Load which gamestanding:` at `0xDBE4`.
3. Pin guest linear 0 by finding the emulated BIOS data area near the start of the same region
   (`0x03F8` at `40:0000`, `640` at `40:0013`). A DOS far pointer holds a *guest* `seg:off`, so this
   step is what makes step 4 possible at all.
4. Read the far pointers at `DGROUP:0x0030` and `DGROUP:0x3FF6`, convert `seg × 16 + off` to a host
   address, and require each array to be well formed: every slot either a valid record or an empty
   one, and no occupied slot after an empty one (both arrays pack from slot 0).
5. Cross-check that `party − roster` is exactly `32 × 410 + 2`. If it does not match, the roster
   is **not opened** — one of the two pointers is not what it claims to be — and the status line
   says so in those words. "Adjacency failed" and "there was no roster pointer" are reported
   differently on purpose: only one of them means the locate may be wrong.

Measured live across repeated attaches: **40–49 ms, 4/4 validators, adjacency holds.**

There is deliberately **no structural fallback**. Six contiguous 410-byte records is a shape that
16 MB of guest RAM will eventually match by accident, and a confident wrong address would turn one
"Max everything" click into a write into unrelated memory.

### Record validation

A window is accepted as a character only if: the occupied byte is exactly 1; the name is 1–10
printable characters starting with a letter and NUL-terminated inside the field; race < 6,
trade < 13, state < 8; level ≤ 99; sex and alignment ≤ 1; maximum hit points in 1–9999 with the
current value not above it; and maximum load in 1–30000 with the carried value not above it.

Level **0** is deliberately allowed. Non-player characters picked up in the world — the shipped
roster's `Siegurd` is one — carry level 0 and trade 12 (`??`). Rejecting them made the whole roster
array fail validation, and the live roster stopped resolving. That was caught by the harness
running against the real files, not by inspection.

---

## 8. Deliberately not done

- **No teleport.** The party's map position was not located. It is not in the character record, and
  the world state inside the save buffer was not decoded.
- **No save editing.** See §6 — this release cannot load a save at all.
- **No `ROST\ROST` editing.** The read path is proved (memory matches the file byte-for-byte), but
  the write path was never round-tripped through the game, and the four trailing bytes per entry are
  not understood.
- **No maximum-spell-uses editing.** Where the number on the right of the slash comes from was not
  found.
- **`+0x76`, `+0x1C`, `+0x1D`, `+0x0F`, `+0x60` are shown or preserved, never written.**

## 9. Verifying this

`test/FormatCheck` re-checks every constant in this document:

```powershell
.\Run.ps1 -Test -NoRun                                          # 329 checks, no game needed
dotnet run --project test\FormatCheck -- --game "<LOF dir>"     # + the shipped ROST and GAMEn files
dotnet run --project test\FormatCheck -- --live <dosbox-pid>    # + an end-to-end locate
```

With a copy of the game and a running DOSBox all three groups run and the count is **348**. The
locator is driven over a synthetic address space that reproduces DOSBox's padded guest, its BIOS
data area, the anchor literals at their real offsets and both far pointers — including the awkward
cases: one validator instead of two, a missing BIOS area, a null pointer, a pointer into junk, a
non-adjacent roster, a gap in the party array, an anchor straddling the 1 MiB sweep seam, an
unreadable page (placed *before* the anchor, so the sweep really has to step over it), decoy
regions, and a cancelled scan. A recording `ICharacterHost` covers the write side: every edit is
checked for the exact byte range it sends, an unchanged edit must send nothing, a slot edit must
carry its high-water byte, every editor ceiling must still produce a record `IsValidRecord`
accepts, and each freeze must settle after one write instead of firing for ever. It also pins the marshalled size of the Win32 `INPUT` struct the
speed hotkeys are sent with: get that wrong and `SendInput` fails silently on every call.

---

## Sources

- The game's own manual, `lof.txt`, shipped in the game directory.
- [Legend of Faerghail — MobyGames](https://www.mobygames.com/game/3436/legend-of-faerghail/)
- [The CRPG Addict: Game 122: Legend of Faerghail (1990)](http://crpgaddict.blogspot.com/2013/11/game-122-legend-of-faerghail-1990.html)
- [Legend of Faerghail — Lemon Amiga (manuals and docs)](https://www.lemonamiga.com/games/docs.php?id=976)
