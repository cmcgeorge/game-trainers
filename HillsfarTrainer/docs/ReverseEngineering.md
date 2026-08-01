# Hillsfar — Reverse-Engineering Notes

**Target:** `MAIN.EXE`, 146,593 bytes, the MS-DOS release of *Hillsfar* (SSI / Westwood
Associates, 1989), an AD&D Forgotten Realms title. The build calls itself **v1.2** (a literal at
`DGROUP:0x0832`).

Everything below was obtained by unpacking `MAIN.EXE`, statically analysing the recovered image, and
then **confirming the result against the running game** under DOSBox 0.74-3 — in most cases by
writing a value into the emulator's memory and watching the game's own screen redraw with it.

Each fact is marked:

| Marker | Meaning |
| --- | --- |
| **[Confirmed]** | Proved by writing/reading the live game and observing the screen, or by an exact arithmetic identity that reproduces the game's own output. |
| **[Inferred]** | Consistent with the disassembly and the sample data, but not observed directly. Treat as a good guess. |
| **[Unknown]** | Identified as *used* by the program, purpose not established. Round-trip it; do not interpret it. |

---

## 1. Unpacking `MAIN.EXE` — two layers

`MAIN.EXE` is **double-packed**. This is the single most important fact about the target: a `strings`
pass or a Ghidra load of the shipped file recovers essentially nothing, because the shipped file is a
1,250-byte decompression stub plus 145,311 bytes of compressed data.

```
MAIN.EXE (146,593 bytes)
└── SEA-AXE stub  ......... 12-bit LZW + RLE  → 173,431-byte image
    └── EXEPACK image ..... backwards RLE     → 207,168-byte program + 4,029 relocations
        └── Microsoft C 1988 medium/large-model DOS program
```

### 1.1 Layer 1 — SEA-AXE

The MZ header describes a tiny program: `pages=3`, `lastpage=258` → a 1,282-byte load image, of
which 32 bytes are header. The load module begins with the ASCII signature **`SEA-AXE`** — System
Enhancement Associates' executable compressor, from the same company as the ARC archiver.

The stub keeps a parameter block at load-module offsets `0x6C`–`0x85`. Read from the shipped file:

| Stub offset | Value | Meaning |
| --- | --- | --- |
| `0x6E` | `0x0002` | number of full `0xFC00`-byte read chunks |
| `0x70` | `0x3F9F` | size of the final partial chunk |
| `0x74` | `0x2A57` | decompressed image size, in paragraphs (173,424 bytes) |
| `0x76` | `0x0007` | trailer bytes after the image inside the stream |
| `0x78` | `0x0A51` | extra paragraphs to allocate (BSS + stack) |
| `0x7A` / `0x7C` | `0x34A1` / `0x0080` | target `SS:SP`, relative to the load segment |
| `0x7E` / `0x80` | `0x284B` / `0x0012` | target `CS:IP`, relative to the load segment |
| `0x82` | `0x237A` | paragraphs below the stub for the input buffer |
| `0x84` | `0x0502` | file offset where the compressed stream begins |

`2 × 0xFC00 + 0x3F9F = 0x2379F = 145,311` — exactly the file remainder after `0x502`. That identity
is what confirms the parameter-block reading is right.

The stub first copies itself to the top of the DOS memory block and far-jumps there (`rep movsw`
followed by `push es; push di; retf`), so the decompressed program can be written to the *original*
load address (`PSP + 0x10`) while the decompressor runs from high memory.

**The compression is 12-bit fixed-width LZW with an RLE layer underneath.** Transcribed from the
stub's own decompressor:

* Codes are packed **big-endian, 12 bits each**, alternating between a "high" and a "low" reader
  (`mov ax,[si]; xchg al,ah; shr ax,4` then `lodsw; xchg al,ah; and ah,0x0F`). Bytes `b0 b1 b2`
  therefore yield `code1 = (b0<<4)|(b1>>4)` and `code2 = ((b1&0xF)<<8)|b2`.
* Dictionary: a 4,096-byte `char[]` table at stub offset `0x4DE` holding each entry's **last**
  character, and a 4,096-entry `prefix[]` word table at `0x14DE`. Entries `0x000`–`0x0FF` are
  literals; **`0x100` is the dictionary reset**; the first free code is `0x101`; insertion stops at
  `0x1000`.
* Two consecutive `0x100` codes mean **end of stream** (the second sees `prev == 0xFFFF` and
  returns).
* The KwKwK case is handled by pre-creating the entry with `char[next] = char[next-1]`, which is
  correct precisely because entry `next-1` was created on the previous step holding the first
  character of the previous string.
* `DS:SI` and `ES:DI` are re-normalised whenever `SI > 0x7530`.
* The emitted byte stream is then RLE-decoded: **`F0 <count> <byte>`** writes `<byte>` `<count>`
  times; every other byte is a literal. The escape/count/value ordering comes from the stub's
  three-state stack machine at `0x3BE`/`0x3CF`/`0x3E2`, which remembers its state in `BX` **across
  LZW codes** — so a run may straddle a code boundary.

Output: **173,431 bytes** = the declared 173,424-byte image plus the 7-byte trailer named by stub
`[0x76]`. The trailer is seven zero bytes; the stub's own relocation count (`[0x72]`) is `0`, so no
outer relocation pass runs. **[Confirmed]** — the arithmetic closes exactly and the result unpacks
cleanly at layer 2.

### 1.2 Layer 2 — EXEPACK

The recovered image is itself an EXEPACK-compressed program. The 18-byte EXEPACK header sits at image
offset `0x284B0`, which is exactly the target `CS` from the outer stub (`0x284B` paragraphs), and the
outer `IP` of `0x12` skips the header to land on the unpacker:

| Field | Value |
| --- | --- |
| real `CS:IP` | `0x09F1:0x2F68` |
| real `SS:SP` | `0x329D:0x0800` |
| `dest_len` | `0x3294` paragraphs = 207,168 bytes |
| `skip_len` | `1` |
| `exepack_size` | `0x20C7` |
| signature | `"RB"` at `0x284C0` |

The unpacker is the textbook backwards RLE: commands are read from the top of the packed data
downward as `[length:word][command:byte]`, with `0xB0`/`0xB1` = fill and `0xB2`/`0xB3` = copy, and
the low bit marking the last command.

**The trap here is the same one EXEPACK always sets:** it expands **in place**, so the compressor
stops emitting commands once the write cursor catches the read cursor, and the remaining low part of
the image is stored uncompressed and is *already at its final address*. An implementation that
stops at the last command and leaves the rest zeroed loses the first **117,144 bytes** — which is
most of the program. The check that catches this is that the unconsumed-input count and the
unfilled-destination count come out **equal**; copy that prefix across verbatim.

The relocation table is 16 groups (segments `0x0000`, `0x1000`, … `0xF000`) of `[count:word]` +
`count` offsets, at unpacker `CS:0x012D` = image offset `0x285DD`, immediately after the
`"Packed file is corrupt"` string. It yields **4,029 relocations** and consumes exactly `0x1F9A`
bytes — precisely the space between the table start and `header + exepack_size`. That exact fit is
the confirmation that the table was read correctly. **[Confirmed]**

### 1.3 The recovered program

207,168 bytes, Microsoft C (the runtime banner `MS Run-Time Library - Copyright (c) 1988, Microsoft
Corp` sits at `DGROUP:0x0008`). The entry code is a stock `__astart`:

```
mov ah,0x30 / int 21h / cmp al,2 / jae ok / int 20h    ; require DOS 2+
mov di,0x277A                                          ; ← DGROUP paragraph (relocated)
...
mov ss,di / add sp,0xB22E
```

**`DGROUP` = load segment + `0x277A`, i.e. image offset `0x277A0` (161,696).** `SS == DS == DGROUP`
(Microsoft C puts the stack inside the data group), and `SP` starts at `0xB22E`, so `DGROUP` is about
45 KB: roughly 20,896 bytes of initialised data in the file plus ~24 KB of BSS/stack above it.

Only the load segment changes between sessions, so **every global has a constant `DGROUP` offset.**
That is what makes a one-click locator possible and a value scanner unnecessary.

*Verified live:* sweeping a running DOSBox for three independent literals put `DGROUP:0000` at the
same host address every time, with all three landing at their predicted offsets. Across two
sessions the base moved (`0x76181E0` → `0x6D7F1E0`), which is why nothing may be hard-coded.
**[Confirmed]**

Ghidra note: load `MAIN.plain.exe` (the twice-unpacked, relocation-bearing MZ) and let the standard
MZ loader place the segments. Auto-analysis on the shipped `MAIN.EXE` is worthless.

---

## 2. The text codec — digraph compression

Most of the game's text is **not** plain ASCII, which is why a naïve string dump returns thousands of
4-character fragments. Bytes `< 0x80` are literal ASCII (with `0x0D` acting as a line break and
`0x00` terminating), and **every byte `>= 0x80` expands to exactly two characters.**

The table is 144 bytes at **`DGROUP:0xAAA4`**, laid out as 16 "first" characters followed by 16
groups of 8 "second" characters:

```
i      = b - 0x80
first  = T[ i >> 3 ]
second = T[ 16 + (i >> 3) * 8 + (i & 7) ]
```

| Group | First char | Its eight second characters |
| --- | --- | --- |
| 0 | `' '` | `t ahsybo` |
| 1 | `e` | `` rnasdet`` |
| 2 | `o` | `u nroftw` |
| 3 | `t` | `h oeiart` |
| 4 | `a` | `rnt lsvc` |
| 5 | `h` | `eai otr!` |
| 6 | `n` | `` gdoe'tk`` |
| 7 | `r` | `e oaitsy` |
| 8 | `s` | `` tehsoi.`` |
| 9 | `i` | `nstlcgmd` |
| 10 | `u` | `` rtnlsga`` |
| 11 | `l` | `le doayi` |
| 12 | `d` | `` eoi.rsa`` |
| 13 | `y` | `o .,!tsi` |
| 14 | `g` | `` hoeuair`` |
| 15 | `c` | `keoahtr` + terminator |

So `0x89` → group 1, slot 1 → `"er"`; `0xD9` → group 11, slot 1 → `"le"`; `0xAA` → group 5, slot 2
→ `"hi"`.

**[Confirmed]** — the layout was solved against fifteen independent expansions recovered from words
whose plaintext is known from the class and building tables (`Thief`, `Fighter`, `Magic-User`,
`Cleric`, `Healer`, `Bank`, `Book store`, `Magic shop`), and it reproduces all fifteen exactly. With
the codec applied, **734 strings** decode cleanly out of `DGROUP` — menus, pub actions, shop
dialogue, guild text and the building tables below.

One wrinkle worth recording, because it cost time: the table's **144th byte is `0x80`** — the second
character of code `0xFF`, and not a character at all. It is almost certainly an unused slot, but any
transcription of the table as ASCII text silently turns it into `?`, which makes a hard-coded copy
compare unequal to the table read out of a live game while still passing every length check.

The decoder is worth having: the class-name table, the building list and the pub-action menus are all
stored compressed, so anything matching on those strings must match the **raw** bytes, not the
decoded text.

---

## 3. The character record — 188 bytes

This is the heart of the game state, and the reason a trainer for *Hillsfar* is straightforward.

* On disk: `<name>.HIL` for saved characters, `*.PRE` for the four shipped pre-rolled ones. Both are
  **exactly 188 bytes.**
* In memory: one working copy at **`DGROUP:0x094C`**.

**The file is a raw dump of the record. There is no checksum, no encryption and no header.**
**[Confirmed]** three ways:

1. Loading `CHRISTOP.HIL` and reading `DGROUP:0x094C` gave all 188 bytes byte-for-byte identical to
   the file.
2. Editing the record in memory (name, race, gender, alignment, gold, HP, experience, level — a
   near-total rewrite) and using the game's own *Save your current Hillsfar character* wrote a file
   **byte-for-byte identical to the edited memory**, with bytes `0x00`–`0x03` carried across
   unchanged. A content checksum would have had to change.
3. Editing a `.HIL` **on disk** and loading it through the game's own *Load a character* menu
   produced a character sheet showing every edited value.

The shipped image also confirms the base address independently: the default name **`Kerwin`** is a
literal at `DGROUP:0x0950`, which is `0x094C + 4` — the record's name field.

### 3.1 Layout

Offsets are relative to the start of the record (add `0x094C` for the `DGROUP` address). All
multi-byte integers are **little-endian**.

| Off | Size | Field | Status |
| --- | --- | --- | --- |
| `0x00` | 4 | A 32-bit counter the game maintains during play. Its high word is explicitly zeroed at `0x03B70`. Not a checksum — it survives a total rewrite of the record. | **[Unknown]** |
| `0x04` | 16 | **Name.** NUL-terminated, remainder space-padded, byte `0x13` always `0x00` → 15 usable characters. | **[Confirmed]** |
| `0x14` | 1 | **Strength**, 3–19 | **[Confirmed]** |
| `0x15` | 1 | **Exceptional-strength percentile** (the `(nn)` after an 18). Non-zero only for fighters. | **[Confirmed]** |
| `0x16` | 1 | **Intelligence** | **[Confirmed]** |
| `0x17` | 1 | **Wisdom** | **[Confirmed]** |
| `0x18` | 1 | **Dexterity** (the game itself `inc`s this in two places) | **[Confirmed]** |
| `0x19` | 1 | **Constitution** — also drives natural healing, §3.4 | **[Confirmed]** |
| `0x1A` | 1 | **Charisma** | **[Confirmed]** |
| `0x1C` | 1 | **Alignment**, 0–8 (§3.2) | **[Confirmed]** |
| `0x1D` | 1 | A counter the game sets and decrements, gated by flag bit 3 of `0x45`; appears in the guild "rest" path. | **[Unknown]** |
| `0x1E` | 2 | **Age** (written as a word) | **[Confirmed]** |
| `0x20` | 1 | **Current hit points** | **[Confirmed]** |
| `0x21` | 1 | **Maximum hit points** | **[Confirmed]** |
| `0x22` | 1 | Read as `HPmax − this` in the arena/damage display path at `0x2570`. | **[Unknown]** |
| `0x24` | 1 | **Class index**, 0–15, mapped to a class bitmask by the table at `DGROUP:0x91DC` (§3.3) | **[Confirmed]** |
| `0x28` | 4 | **Gold** (32-bit; `add`/`adc` pairs across `0x28`/`0x2A` prove the width) | **[Confirmed]** |
| `0x2C` | 1 | **Gender**: 0 Male, 1 Female | **[Confirmed]** |
| `0x2D` | 1 | **Race**: 0 Dwarf, 1 Elf, 2 Gnome, 3 Half-elf, 4 Halfling, 5 Human | **[Confirmed]** |
| `0x2E` | 4 | **Experience** (32-bit; the level-up routine at `0x192F5` does 21 `cmp word` pairs across `0x2E`/`0x30` against the threshold table) | **[Confirmed]** |
| `0x32` | 1 | Thief skill — varies with Dexterity between two level-6 thieves (`0x25` at Dex 13 vs `0x2F` at Dex 18) | **[Inferred]** |
| `0x33` | 1 | Thief skill — likewise (`0x37` vs `0x41`) | **[Inferred]** |
| `0x34` | 1 | Thief skill — `0x5C` (92) for both level-6 thieves, Dexterity-independent; matches AD&D *Climb Walls* at level 6 | **[Inferred]** |
| `0x35` | 1 | **Class bitmask** — the single most-referenced byte in the record (45 `test byte [m],imm` sites). Bit 0 Thief, bit 1 Fighter, bit 2 Magic-User, bit 3 Cleric. Stored with the mask in **both nibbles** (`0x11`, `0x22`, `0x44`, `0x88` for the four single classes). | **[Confirmed]** |
| `0x36`, `0x37` | 1+1 | Heavily-read pair (42 references each), read together with the clock. Position or current-location indices. | **[Inferred]** |
| `0x38`–`0x3A` | — | Small state bytes set from immediates | **[Unknown]** |
| `0x3C` | 2 | A 16-bit countdown (`dec word`), read by the routine that draws the `Time Left` caption — the maze/building time limit | **[Inferred]** |
| `0x3E` | 2 | **Day counter** — `inc`remented by the clock tick when the hour reaches 24 (§3.4) | **[Confirmed]** |
| `0x40` | 4 | **Real-world `time_t` of the last clock tick** (§3.4) | **[Confirmed]** |
| `0x44` | 1 | **Hour of day, 1–24** (§3.4) | **[Confirmed]** |
| `0x45` | 1 | **Flag bits** — 18 `test`, 7 `or`, 4 `and` sites. Bit 0 is set for both shipped thieves (they are the two characters carrying lock picks). Bit 3 gates the `0x1D` counter. | **[Inferred]** |
| `0x46`–`0x81` | 60 | **Lock picks** — 12 records of 5 bytes (§3.5) | **[Confirmed]** |
| `0x82`, `0x83` | 1+1 | Lock-pick totals: one `dec`remented, one `inc`remented, in code adjacent to *"Will you purchase these picks?"* and *"Broken picks may be mended by the Old Wizard"* | **[Inferred]** |
| `0x84`, `0x85` | 1+1 | A second `inc`/`cmp` counter pair | **[Unknown]** |
| `0x86` | 1 | **Knock rings**, 0–99 | **[Confirmed]** |
| `0x87` | 1 | **Healing potions**, 0–99 | **[Confirmed]** |
| `0x88` | 1 | State byte, `7` in every shipped file, cleared once play begins | **[Unknown]** |
| `0x89`–`0x9A` | 18 | **Per-hour countdown timers.** The clock tick walks exactly 18 bytes from `0x89` and decrements each non-zero one (§3.4). | **[Confirmed]** |
| `0x9B`, `0x9C` | 1+1 | State bytes written from immediates and compared often | **[Unknown]** |
| `0x9F` | 1 | **Archery-range level**, `inc`remented and **capped at 15** at `0x13AF0`, which then awards experience via the class-mask-indexed table at `DGROUP:0x0D08`. The walkthrough's "improve by two levels" / "reach the FIFTH level" gates read this. | **[Confirmed]** |
| `0xAB` | 1 | **Hours until the next natural heal**, reset to 24 (§3.4) | **[Confirmed]** |
| `0xB4` | 1 | `0xFF` in every shipped file; written from immediates — a list terminator | **[Inferred]** |
| `0xB7` | 1 | **Cleric level** | **[Confirmed]** |
| `0xB8` | 1 | **Magic-User level** | **[Confirmed]** |
| `0xB9` | 1 | **Fighter level** | **[Confirmed]** |
| `0xBA` | 1 | **Thief level** | **[Confirmed]** |

The four level bytes are the cleanest structure in the record: `0xB7`–`0xBA` carry six or seven
`inc byte [m]` sites each with identical surrounding code — one level-up path per class, in
**descending bitmask order** (Cleric = bit 3 first, Thief = bit 0 last). A multi-class character has
a separate level per class, exactly as AD&D requires; the class bitmask at `0x35` says which entries
are live.

### 3.2 Alignment

Composed from two three-entry tables — `Lawful` / `Neutral` / `Chaotic` at `DGROUP:0x8B1D` and
`Good` / `True` / `Evil` at `DGROUP:0x8B2E` — as `law × 3 + moral`:

| 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Lawful Good | Lawful Neutral | Lawful Evil | Neutral Good | **True Neutral** | Neutral Evil | Chaotic Good | Chaotic Neutral | Chaotic Evil |

Index 4 is printed **"True Neutral"** — the game swaps the word order for that one case only.
Verified on screen at 0, 3, 4 and 8. **[Confirmed]**

### 3.3 Classes

Two representations coexist, and both matter.

**The bitmask** (`0x35`) is what the code actually tests. The class-*name* table at `DGROUP:0x3DB0`
is indexed directly by that mask, and reading it out gives the game's own legal-combination list:

| Mask | Name | | Mask | Name |
| --- | --- | --- | --- | --- |
| 1 | `Thief` | | 8 | `Cleric` |
| 2 | `Fighter` | | 9 | *(illegal)* |
| 3 | `FTR/TH` | | 10 | `CL/FTR` |
| 4 | `Magic-User` | | 11 | *(illegal)* |
| 5 | `MU/TH` | | 12 | `CL/MU` |
| 6 | `FTR/MU` | | 13 | *(illegal)* |
| 7 | `FTR/MU/TH` | | 14 | `CL/FTR/MU` |

Masks 0, 9, 11 and 13 have empty strings — every combination of Cleric with Thief is illegal, which
matches AD&D.

**The class index** (`0x24`) is a menu index converted to a mask by the 16-byte table at
`DGROUP:0x91DC`:

```
08 FF 02 FF FF 04 01 FF 0A 0E FF 0C FF 06 03 07
```

so index 0 → Cleric, 2 → Fighter, 5 → Magic-User, 6 → Thief, 8 → CL/FTR, 9 → CL/FTR/MU,
11 → CL/MU, 13 → FTR/MU, 14 → FTR/TH, 15 → FTR/MU/TH, and `0xFF` marks an unused slot. The four
shipped `.PRE` files carry exactly 0, 2, 5 and 6 in `0x24`, which is how the alignment of this table
was pinned. **[Confirmed]**

Note MU/Thief (mask 5) has no index inside the 16 bytes; the byte immediately after the table is
`0x05`, so the table may be 17 entries. Do not rely on index → mask for the multi-class combinations;
**write the mask at `0x35` and set `0x24` to match**, since `0x35` is what the game tests.

### 3.4 The clock — fully decoded

Two routines at code `0x05FA` and `0x06A8`, and between them they explain six record fields.

**Display** (`0x05FA`) reads `0x44` as the hour, subtracts 12 if it exceeds 12, prints that number,
and appends `am` (`DGROUP:0x0DEC`) when the hour is 24 or below 12, else `pm` (`DGROUP:0x0DEF`).
Checked on screen: `0x44 = 0x0F` (15) displayed as **`TIME: 3 pm`**. **[Confirmed]**

**Tick** (`0x06A8`), in full:

```c
now = time();                                  /* real-world time_t */
if (now - rec[0x40] <= 121) return;            /* one game hour = 122 real seconds */

for (i = 0; i < 18; i++)                       /* 18 per-hour timers */
    if (rec[0x89 + i]) rec[0x89 + i]--;

rec[0x44]++;                                   /* advance the hour */

if (rec[0xAB]) rec[0xAB]--;                    /* natural-healing countdown */
if (rec[0xAB] == 0) {
    t = (rec[0x19] <= 14) ? 0 : rec[0x19] - 14;   /* Constitution */
    if (t > 5) t = 5;
    t++;
    rec[0x20] += t;                               /* heal */
    if (rec[0x20] > rec[0x21]) rec[0x20] = rec[0x21];
    rec[0xAB] = 24;
}

if (rec[0x44] == 24) rec[0x3E]++;              /* day rolls over */
if (rec[0x44] >  24) rec[0x44] = 1;
rec[0x40] = now;                               /* remember when we ticked */
```

Consequences worth writing down:

* **One game hour costs 122 seconds of real time**, and the clock is driven off the host clock, not
  off player actions. It does not advance while the process is idle at a menu because the tick is
  only called from the play loop.
* **Natural healing is `1 + clamp(Constitution − 14, 0, 5)` hit points per 24 game hours.** So
  Constitution 14 or below heals 1 point a day and Constitution 19 heals 6.
* `0x40` is *not* a creation timestamp, though it starts life as one — which is why the four shipped
  `.PRE` files carry `time_t` values inside a 58-second window on **1989-03-30**, the moment they
  were generated. `TIM.HIL` carries 1996-01-28.

### 3.5 Lock picks

`0x46`–`0x81` is **12 records of 5 bytes**; the stride is proved by the initialiser at `0x19A40`,
which computes `si = index * 5` after seeding `0x46 = 0x37` and `0x49 = 0x23` and filling `0x47` from
the game's random-number call. **[Confirmed]**

Within a record the first four bytes are two values and their `+20` counterparts — for every one of
the 24 records across the two shipped thieves, `byte2 − byte1 = 20` and `byte0 − byte3 = 20` exactly.
That pairing is consistent with the pick's two ends (the manual has you flip a pick over) and their
tumbler-shape indices. The fifth byte is 0, 2 or 3 and is the pick's count or condition — the manual
distinguishes present, broken and absent picks. **[Inferred]** for the meaning; the geometry is
exact.

Both shipped thieves have flag bit 0 of `0x45` set and the other four characters do not, which is
what ties that bit to carrying picks.

---

## 4. Locating the game in memory

The program is relocated by DOS, so `DGROUP` lands somewhere different every session (measured:
`0x76181E0` and `0x6D7F1E0` in two consecutive runs of the same build). It must be found from
scratch each time — but because every global has a fixed `DGROUP` offset, finding it once is enough
and **no value scanning is needed at all**.

Five literals were checked for uniqueness by sweeping a live 16 MB DOSBox guest:

| `DGROUP` | Bytes | Length | Hits in the whole process |
| --- | --- | --- | --- |
| `0x0D1A` | `WARNING: DO NOT RUN MEMORY RESIDENT PROGRAMS WHILE PLAYING HILLSFAR!!` | 69 | **1** |
| `0x0E1D` | `Put the Hillsfar Program Disk in the drive` | 42 | **1** |
| `0x3DD8` | `FTR/MU/TH\0` | 10 | **1** |
| `0xAAA4` | `' eotahnrsiuldygc'` (digraph table) | 16 | **1** |
| `0x91AC` | `HILCHAGUYPRE` | 12 | **1** |

So: sweep for the 69-byte banner, subtract `0x0D1A` to get `DGROUP:0000`, and require at least two
of the other four to line up at their own offsets before believing it — a three-of-five match at
minimum, four of five in practice. Then read the record at `DGROUP:0x094C` and shape-check it.

The digraph table at `DGROUP:0xAAA4` doubles as a **build canary**: 144 bytes of pure data at a
fixed offset, so if it differs from the one a tool was written against, the attached game is a
different release and the record offsets may have moved too.

Two cautions for anyone extending this:

* **Match raw bytes, not decoded text.** Most game strings are digraph-compressed, so
  `"Temple of Tempus"` does not occur anywhere in memory — the raw form does. Anchors must be sliced
  out of the unpacked image.
* **Do not add a blind structural fallback.** The 188-byte record has a name string and plausible
  attribute bytes, which is a shape that will eventually match unrelated data in 16 MB of guest RAM;
  a confident wrong address here means writes land in another program's memory. Five candidate
  literals in one 45 KB segment is far stronger evidence, and if a different build moves them the
  honest answer is "not found".

A useful side effect of the record being the *working copy* rather than a snapshot: edits take effect
immediately, and they reach disk when the player uses *Save your current Hillsfar character* — the
save path writes the record verbatim, so nothing has to be recomputed.

---

## 5. Reference tables recovered from the image

### 5.1 Locations (`DGROUP:0x3D1D`, digraph-compressed, `\r`-terminated)

Eighteen entries, matching the eighteen buildings in the manual's opening-hours table exactly:

`Jail`, `Temple of Tempus`, `Cemetary` *(the game's spelling)*, `Rogue's Guild`, `Mage's Guild`,
`Fighter's Guild`, `Stable`, `Sewer`, `Archery`, `Arena`, `Mages Tower`, `Haunted Mansion`, `Pub`,
`Bank`, `Book store`, `Magic shop`, `Castle`, `Healer`.

### 5.2 Races, genders, classes (`DGROUP:0x8AD9`)

`Dwarf`, `Elf`, `Gnome`, `Half-elf`, `Halfling`, `Human`; then `Male`, `Female`; then the four
single-class names `Cleric`, `Fighter`, `Magic-User`, `Thief`.

### 5.3 Arena roster (`DGROUP:0x64F0`–`0x65BD`, digraph-compressed)

Eight NUL-terminated opponent descriptions, the first at `DGROUP:0x6509`:
`Lefty the left handed Orc.`, `The Red Minotaur, nobody knows his name!`,
`Ssslader, lizard man of the Vast Swamp.`, `Morin, he is a knight you should fear!`,
`Ottis the Orc, from the Thunder Peaks.`, `Taurus the Great.  A mighty minotaur.`,
`Whiplash, watch out for this lizards tail!`, `Keller the Dark Knight, a mighty fighter.`

Four of the eight also have a gossip paragraph spelling out the opponent's tell — Lefty at
`DGROUP:0x3077`, the Red Minotaur at `0x318D`, Ssslader at `0x3246` and Morin at `0x3357`. A sweep of
the whole data segment found none for Ottis, Taurus, Whiplash or Keller, so those four have to be
learned by watching. **[Confirmed]** — read directly out of the decoded string table.

### 5.4 Quest files

`DGROUP:0x91FC` holds the template `Q?.BIN` and `DGROUP:0x91EE` the substitution string
`123456789ABC` — which is exactly the twelve shipped `Q1.BIN`…`QC.BIN` files, i.e. **four classes ×
three missions**. `DGROUP:0x91AC` holds the extension table `HIL` `CHA` `GUY` `PRE`.

### 5.5 Copy protection

The code-wheel prompt is a literal at `DGROUP:0x0758`:

> *Use the translation wheel to decipher the code word. Match the Espruar rune (outer ring) with the
> Dethek rune (inner ring) and read the code word under the path: read from the inside to the
> outside. Input code word:*

This is the physical *Hillsfar* code wheel supplied with the boxed game. **It did not trigger in
this build** — the game was launched from cold and went straight to the camp menu, and a full
play session through the ride into the city never raised it. `SYMBOLS.CMP` holds the rune artwork.
No answer key was located, and none is needed here.

---

## 6. Method summary — what was actually run

* Unpacked both compression layers with purpose-written decoders derived from the stubs'
  own disassembly (12-bit LZW + `F0` RLE; then backwards EXEPACK with the in-place prefix fix), and
  rebuilt a plain relocatable MZ so Ghidra's normal loader could place the segments.
* Ghidra 12.1.2 headless auto-analysis on the rebuilt image; targeted Capstone disassembly of the
  clock, HUD, shop, level-up, archery and lock-pick routines.
* A **record-offset sweep**: every 16-bit little-endian encoding of `0x094C + n` in the code segment,
  filtered to genuine `disp16` memory-operand encodings, gave 890 validated references across 78
  record bytes — and the *shape* of the references (`test` for the class mask, matched `inc` sets for
  the four levels, `add`/`adc` pairs for 32-bit gold, `cmp word` runs for the experience thresholds)
  identified most fields before any of them was read live.
* Live DOSBox 0.74-3 session driven by `PostMessage` key injection with `PrintWindow` screen capture,
  and `ReadProcessMemory`/`WriteProcessMemory` against the emulator. Fields were confirmed by writing
  a distinctive sentinel and reading the game's own redraw: one pass put `ZZTOP` / `Str 17(88)` /
  `Int 3` / `Age 44` / `HP 77(99)` / `Gold 12345` / `Exp 54321` / `Level 9` on screen simultaneously,
  a second `Female Elf` / `Chaotic Evil`, and a third confirmed the two HUD consumables as
  3 knock rings and 7 healing potions.
* The `.HIL` format was closed by the three-way test in §3: memory matches file, game-written save
  matches edited memory, and a disk-edited file loads with every value intact.

Nothing in the game directory was modified: the two `.HIL` and four `.PRE` files were checksummed
before and after and are byte-identical, and the test files created during the session were removed.
