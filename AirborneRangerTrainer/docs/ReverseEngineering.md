# Airborne Ranger — Reverse-Engineering Notes

**Target:** the IBM PC / MS-DOS release of *Airborne Ranger* (MicroProse Software, 1988),
internal version **441.01** — the version string is a literal at `DGROUP:0xB7F7` and is printed in
the bottom-left corner of the control-device screen.

**Credits, verbatim from the game's own credits screen** (`DGROUP:0xB9BC` onwards):
Software Development *Randall Don Masteller*; Game Design & Project Leader *Lawrence Schick*;
Graphics & Animation *Iris Leigh Idokogi*, *Barbara Bents*, *Jaclyn Ross*; Sound Effects & Music
*Ken Lagace*; Original Software Development *Scott Spanburg*.

**Files examined:** `AR.EXE`, `ROSTER.DAT`, `ID_A.DAT`, `ID_B.DAT`, `AARCV.BAT`, `IBMSNDS.EXE`,
`TANDYSND.EXE`, and the 60-odd `*.DTX` data files.

**Tools:** Ghidra 12.1.2 (headless import/auto-analysis), a purpose-written EXEPACK unpacker, a
Capstone-based 16-bit disassembler for targeted regions, and — decisively — **a live DOSBox 0.74-3
session** with read/write process-memory probing of the running game.

Offsets marked **[Confirmed]** were established in two independent ways: the game's own code was
read to see which variable it renders into which on-screen field, *and* the value was read out of a
live session and matched against the screen. Where a value was additionally **written** and the
change watched on screen, that is stated explicitly. Anything marked **[Inferred]** is a hypothesis
that has not been closed.

---

## 1. Executable layout

### 1.1 `AR.EXE` is EXEPACK-compressed

`AR.EXE` is 73,029 bytes. Its MZ header is unusual in exactly the way a packed executable is:

| Field | Value | Meaning |
| --- | --- | --- |
| `e_cblp` / `e_cp` | `0x0145` / `0x008F` | 143 pages, 325 bytes in the last → 73,029 bytes exactly (nothing appended) |
| `e_crlc` | `0x0000` | **zero relocations** — impossible for a 73 KB real-mode program |
| `e_cparhdr` | `0x0020` | 512-byte header |
| `e_minalloc` | `0x316F` | asks for ~201 KB beyond the image |
| `e_ss:e_sp` | `282B:0080` | |
| `e_cs:e_ip` | `118D:0010` | entry point 37 bytes before the end of the load image |

An entry point at the very end plus no relocation table is the EXEPACK signature, and the literal
`Packed file is corrupt` sits at file offset `0x11BDE`. The EXEPACK variable block at `CS:0000`
(file offset `0x11AD0`) decodes as the 16-byte `RB` variant:

```
real IP    0020      real SP    CB14
real CS    0000      real SS    1B51
mem_start  0000      dest_len   2803 paragraphs  = 163,888 bytes
exepack_sz 0275      signature  "RB"
```

`scratchpad/unexepack.py` (reproduced in this repository's history) implements the decompressor.
One detail matters and is easy to get wrong: **EXEPACK expands in place**, so the compressor sets
the *last-block* flag at the point where the write cursor catches the read cursor, and the
remaining prefix is stored uncompressed and is already at its final address. A naive
implementation stops there and leaves the first ~52 KB zeroed. Here the cursors converge at exactly
`0xCE72`, and copying `packed[0:0xCE72]` across completes the image. The relocation table (152
entries) follows the `Packed file is corrupt` literal inside the EXEPACK block, in the standard
sixteen `(count, offsets…)` groups.

The result is a **163,888-byte load image**; every offset in this document is relative to that
unpacked image unless it says `DGROUP:`.

### 1.2 Segment layout

The unpacked entry code at `0000:0020` gives the whole memory model away:

```
0020  push ds
      mov  ax, <reloc>          ; PSP-ish fixup
      ...
0040  mov  ax, cs:[0]           ; = 0x0CE8
      mov  ds, ax
      mov  ax, ss / mov bx, sp
      mov  [0x12], ax           ; save entry SS:SP in DGROUP
      mov  [0x14], bx
      mov  ax, cs:[2]           ; = 0x1B51
      mov  ss, ax
      mov  ax, 0xCB10
      mov  sp, ax
```

Words at `0000:0000` and `0000:0002` in the unpacked image are `0x0CE8` and `0x1B51`, so:

| Segment | Image range | Size | Register |
| --- | --- | --- | --- |
| Code | `0x00000`–`0x0CE7F` | 52,864 bytes | `CS = load + 0x0000` |
| **Data (DGROUP)** | `0x0CE80`–`0x1B50F` | **59,024 bytes (`0xE690`)** | `DS = load + 0x0CE8` |
| Stack | `0x1B510`–`0x2802F` | 52,000 bytes | `SS = load + 0x1B51`, `SP = 0xCB10` |

This is a medium-model layout: one code segment, one data segment, a separate stack segment. It is
the single most useful fact about the program, because **every global has a constant `DGROUP`
offset**. Only the load segment moves between sessions, so a trainer that finds `DGROUP:0000` once
can read every variable below without any value searching.

**`DGROUP` byte *n* is at offset `0x0CE80 + n` in the unpacked image.**

The program also carries an overlay manager — its error strings are at `DGROUP:0x19E6`
(`OVERLAY LOAD FAILED FOR SOME REASON`, `OVERLAY HAS OVERRUN ALLOCATED MEMORY`,
`ALLOCATED 1MB OF SPACE????`, `MS-DOS LIED TO US ABOUT HOW MUCH MEMORY WAS AVAILABLE`) — but none
of the state this document covers lives in an overlay. It is all in `DGROUP`.

A stray developer artefact survives in the game directory: `AARCV.BAT` contains `cv /S ab.exe`,
i.e. "run CodeView on `ab.exe`" — the executable's working name during development.

---

## 2. The status panel is a fill-in-the-blanks template

The route into everything else was the game's own text engine. Messages are byte-coded templates
terminated by `0xFF`, with `0x0D` for a newline and `0x01 nn` / `0x03 ptr` for cursor and body
control. Crucially, **numeric fields are literal `X` placeholders in the executable that the game
overwrites in place with ASCII digits**.

The post-landing status panel lives at `DGROUP:0xB910`. Side by side, the shipped image and a live
session mid-mission:

```
static   B930  58 58 20 ...  "XX "         live   B930  30 34 20 ...  "04 "
static   B93E  1f 58 58      "XX"          live   B93E  1f 30 30      "00"
static   B954  1f 58 58 20   "XX "         live   B954  1f 30 33 20   "03 "
static   B963  1f 58 58      "XX"          live   B963  1f 30 31      "01"
static   B979  1f 20 20 20   "   "         live   B979  1f 30 31 20   "01 "
static   B988  1f 58 58 0d   "XX"          live   B988  1f 32 32 0d   "22"
static   B99E  1f 58 58 20   "XX "         live   B99E  1f 30 31 20   "01 "
static   B9AC  1f 58 58 58   "XXX"         live   B9AC  1f 36 30 30   "600"
```

Writing to that buffer would only change the text on the panel, not the game state — but it points
straight at the code that fills it, and *that* code names the real variables.

### 2.1 The fill routine at `0xBB43`

Searching the 52 KB code segment for the little-endian encodings of the eight placeholder addresses
turns up one tight cluster. Disassembled:

```
BB43  mov  al, [0xC895]           ; spare carbine magazines
BB46  or   al, al
BB48  jnz  BB51
BB4A  cmp  byte ptr [0xC894], 0   ; rounds left in the loaded magazine (signed)
BB4F  jl   BB59
BB51  inc  al                     ; count the loaded magazine as one
BB53  mov  word ptr [0xE248], 1   ; ...and add its weight
BB59  mov  si, 0xB930             ; CARBINE MAGS
BB5C  call BBB9
BB5F  mov  al, [0xC896]  |  mov si, 0xB955  |  call BBB9   ; GRENADES
BB68  mov  al, [0xC897]  |  mov si, 0xB97A  |  call BBB9   ; LAW ROCKETS
BB71  mov  al, [0xC898]  |  mov si, 0xB99F  |  call BBB9   ; TIME BOMBS
BB7A  mov  al, [0xC892]  |  mov si, 0xB93F  |  call BBB9   ; WOUNDS
BB83  mov  al, [0xC89A]  |  mov si, 0xB964  |  call BBB9   ; FIRST AID
BB8F  mov  ax, [0xCA42]
BB92  add  ax, [0xE248]
BB96  mov  si, 0xB989   |  call BBB9                        ; WEIGHT
BB9C  mov  si, 2                                            ; TIME: three digits
BB9F  mov  al, [si + 0xBE54]
BBA3  or   al, 0x30
BBA5  mov  [si + 0xB9AD], al
BBA9  dec  si  |  jns BB9F

BBB9  mov  dl, '0'                ; the two-digit renderer
BBBB  inc  dl
BBBD  sub  al, 10
BBBF  jae  BBBB
BBC1  dec  dl  |  add al, 10
BBC5  mov  [si], dl
BBC7  add  al, '0'
BBC9  mov  [si+1], al
BBCC  ret
```

Three things fall out of this at once. The panel's magazine count is **spare magazines + 1** for the
one in the weapon. Carried weight is a stored total **plus a flag word** that is 1 while a magazine
is loaded. And the mission clock is not a number at all — it is **three separate bytes each holding
one decimal digit**, which is why searching all 16 MB of DOSBox RAM for the 16-bit value `600` while
the panel showed `TIME 600` produced nothing but unrelated tables.

### 2.2 The confirmed live layout

| `DGROUP` | Type | Field | Status |
| --- | --- | --- | --- |
| `0xC892` | `u8` | **Wounds** — 3 is death | [Confirmed] |
| `0xC894` | `i8` | **Rounds in the loaded magazine** (30 = full; negative = none) | [Confirmed] |
| `0xC895` | `u8` | **Spare carbine magazines** (panel shows this + 1) | [Confirmed, write-tested] |
| `0xC896` | `u8` | **Hand grenades** | [Confirmed] |
| `0xC897` | `u8` | **LAW rockets** | [Confirmed] |
| `0xC898` | `u8` | **Time bombs** | [Confirmed] |
| `0xC89A` | `u8` | **First-aid kits** | [Confirmed] |
| `0xCA42` | `u16` | **Carried weight**, excluding the loaded magazine | [Confirmed] |
| `0xE248` | `u16` | 1 while a magazine is loaded; added to the displayed weight | [Confirmed] |
| `0xBE54` | `u8` | Mission clock — **hundreds digit** | [Confirmed, write-tested] |
| `0xBE55` | `u8` | Mission clock — **tens digit** | [Confirmed, write-tested] |
| `0xBE56` | `u8` | Mission clock — **units digit** | [Confirmed, write-tested] |
| `0xC891` | `u8` | Selected weapon: 0 carbine, 1 grenade, 2 LAW, 3 time bomb, 4 knife | [Confirmed] |
| `0xA2D4` | `u16` | Merit points earned this mission | [Confirmed] |
| `0xA2D6` | `u8` | Enemy soldiers eliminated | [Confirmed] |
| `0xA2D8` | `u8` | Military targets destroyed | [Confirmed] |
| `0xA2D3` | `i8` | Mission outcome — 0 killed, 1 captured, negative = still alive | [Inferred] |
| `0xA2D7` | `u8` | Assessment branch selector | [Inferred] |
| `0x0950` | `128 × u16` | Scan-code → direction-bit table for the game's own INT 9 handler | [Confirmed] |
| `0x0AD9` | `u16` | Bitmask of the movement/fire keys currently held | [Confirmed] |

A live session mid-mission, read at the moment the panel showed
`CARBINE MAGS 04 / GRENADES 03 / LAW ROCKETS 01 / TIME BOMBS 01 / WOUNDS 00 / FIRST AID 01 /
WEIGHT 22 / TIME 600`:

```
C892 = 0   C894 = 30   C895 = 3   C896 = 3   C897 = 1   C898 = 1   C89A = 1
CA42 = 21  E248 = 1    BE54,BE55,BE56 = 6,0,0
```

Every field matches, and the weight is not merely plausible but exactly reconstructible: the
supply-pod screen prices a carbine magazine at 1, a grenade at 2, a first-aid kit at 3, a LAW rocket
at 6 and a time bomb at 3, and `3×1 + 3×2 + 1×6 + 1×3 + 1×3 = 21`, plus 1 for the loaded magazine
= the 22 on screen. That arithmetic closing on the nose is what turns a plausible offset table into
a confirmed one.

**Write test.** Writing `C895 = 9` and `BE54,BE55,BE56 = 9,9,9` into the running game and stepping
back into the action view produced a heads-up display reading **10 magazines** (9 spare + the loaded
one) with the **countdown running down from 999** — so the game reads these variables, it does not
merely render them.

---

## 3. Rules recovered from the code

### 3.1 Wounds and death

Three separate sites gate on the wound counter, all with the same constant:

```
A402  mov al, [0xC892] | cmp al, 3 | jb …      ; alive check
8D17  mov al, [0xC892] | cmp al, 3 | jae …     ; death path
9D45  mov al, [0xC892] | cmp al, 3 | jb …      ; sets outcome 0 ("did not survive")
```

so **three wounds kill you**, exactly as the manual says. Instant-death events do not accumulate
wounds one at a time — at `0xA908` the game simply writes `[0xC892] = 4`.

Wound count also feeds the ranger's own effectiveness: at `0x6D19` it is doubled, added to another
term, clamped to 5, and doubled again before being used as an index — a wounded ranger is a slower,
worse-aimed ranger, not merely a closer-to-dead one.

### 3.2 First aid

```
6039  cmp al, 8                    ; command code 8 = use a first-aid kit
603D  mov al, [0xC89A] | or al,al | jz  bail   ; no kits
6044  mov al, [0xC892] | or al,al | jz  bail   ; not wounded
604B  cmp al, 3        | jae bail              ; already dead — no resurrection
604F  dec byte ptr [0xC89A]
6053  dec byte ptr [0xC892]
```

One kit removes exactly one wound, and you cannot heal out of the dead state.

### 3.3 Time bombs and recalling the aircraft

The in-game command dispatcher at `0x5F60` compares against **ASCII** codes:

| Code | Key | Effect |
| --- | --- | --- |
| `0x35` `0x36` `0x37` | `5` `6` `7` | Arm a time bomb with a 5 / 10 / 15-second fuse (fuse constants 5/10/15 and tick counts 50/100/150 are loaded into `BX`/`SI` and stored at `0xC88E`/`0xC88F`, weapon code 3 into `0xC891`). Refuses if `[0xC898]` is zero. |
| `0x31` | `1` | **Recall the aircraft** — clamps the countdown digits at `0xBE54`–`0xBE56` down to a per-situation minimum from a table, zeroing the hundreds and units. This is the clincher for the clock's representation. |
| `0x20` | `Space` | Toggle stand ⇄ crawl (`[0xBE40] = 1`) |
| `0x08` | `Backspace` | Use a first-aid kit (§3.2) |
| `0x4B` | `K` | Toggles `[0x9F55]` — [Inferred] a display or audio option |
| `0x80`–`0x83` | *(extended)* | Select weapon: `[0xC891]` ← 0 carbine / 1 grenade / 2 LAW rocket / 4 knife |
| `0x88` | *(extended)* | Show the map / status panel (calls the panel routines at `0xBBCD`, `0x6B13`, `0x2432`) |
| `0x89` | *(extended)* | Toggles `[0xD92F]` — [Inferred] |
| `0x2F` `0x3E` `0x87` `0xB1` `0xB2` | | A second dispatcher block at `0x606A`; see §3.5 |

The `0x80 + n` shape means these are function keys. Mapping `F1 → 0x80` makes `F1`–`F4` the four
weapons — which agrees exactly with the published DOS control list (F1 carbine, F2 grenade, F3 LAW
rocket, F4 knife) — and would make `0x88` = `F9` for the map. The weapon codes are
[Confirmed]; **the specific function keys that produce them are [Inferred]**, since the DOSBox
session became unavailable before that could be tested on screen.

### 3.4 The movement keys, from the game's own INT 9 handler

The game installs its own keyboard interrupt handler at `0x25BC`:

```
25BC  in   al, 0x60
25BE  mov  bl, al
25C0  and  bx, 0x7F                 ; scan code without the break bit
25C3  shl  bx, 1
25C5  cmp  word ptr [bx + 0x950], 0 ; DGROUP:0x0950 — one word per scan code
25CA  je   ignore
25CC  test al, 0x80                 ; break code?
25D0  mov  ax, [bx + 0x950]  | or  [0x0AD9], ax    ; press: set the bits
25DB  mov  ax, [bx + 0x950]  | xor ax,0xFFFF | and [0x0AD9], ax   ; release: clear them
```

Reading `DGROUP:0x0950` out of a live session configured for **Keyboard — Directional** gives the
whole scheme, with the four direction bits combined for the diagonals:

| Bit | Meaning | Keys |
| --- | --- | --- |
| `0x0001` | North | `↑`, keypad `8`, `` ` `` |
| `0x0002` | South | `↓`, keypad `2`, keypad `-` |
| `0x0004` | West | `←`, keypad `4`, `\` |
| `0x0008` | East | `→`, keypad `6`, keypad `+` |
| `0x0005` / `0x0009` / `0x0006` / `0x000A` | NW / NE / SW / SE | keypad `7` / `9` / `1` / `3` |
| `0x0010` | **Fire** | `Enter`, keypad `5`, keypad `0` (`Ins`) |

### 3.5 A debug key that resupplies you

The second dispatcher block contains this:

```
606E  cmp al, 0x3F           ; '?'
6070  je  6098
…
6098  mov byte ptr [0xC895], 3   ; spare magazines
609D  mov byte ptr [0xC896], 3   ; grenades
60A2  mov byte ptr [0xC897], 3   ; LAW rockets
60A7  mov byte ptr [0xC898], 3   ; time bombs
60AC  mov byte ptr [0xC89A], 3   ; first-aid kits
60B1  ret
```

A leftover developer resupply that sets five of the six ammunition counters to 3. Whether it is
still reachable from the keyboard in the shipped build is **[Inferred]** — the dispatcher block it
sits in was not traced back to its caller, and it could not be tested live. It is documented here
because it is exactly the shape of routine that tells you which variables the developers themselves
considered "the loadout", and it corroborates §2.2 independently.

---

## 4. `ROSTER.DAT` — the saved career file

495 bytes, plain text with binary tails, and completely decoded. Layout:

```
offset 0x000   6 bytes    header, all zero in every observed file
offset 0x006   6 × 81     ranger records
offset 0x1EC   3 bytes    trailer, all zero
```

Each **81-byte record**:

| Offset | Size | Content |
| --- | --- | --- |
| `+0x00` | 33 | Line 1: 4 spaces, rank mnemonic (3), space, name (19), score (6 ASCII digits) |
| `+0x21` | 2 | `0D FF` terminator |
| `+0x23` | 34 | Line 2: the decorations line, blanked where not earned |
| `+0x45` | 2 | `0D FF` terminator |
| `+0x47` | 10 | Binary tail |

Binary tail:

| Byte | Content |
| --- | --- |
| `+0` | `00` in every observed record |
| `+1` | **Rank index** into the rank table |
| `+2` | **Decoration bitmask** |
| `+3`, `+4` | **[Unknown]** — see below |
| `+5` | `00` in every observed record |
| `+6`…`+9` | `01 02 03 04` in every observed record |

The rank table is a literal at `DGROUP:0xBB64`, four characters per entry:

```
 0 PFC   1 CPL   2 SGT   3 SSG   4 PSG   5 SGM   6 2LT   7 1LT
 8 CPT   9 MAJ  10 LTC  11 COL  12 (blank)  13 KIA  14 POW
```

and the decoration line is the literal at `DGROUP:0xBBA6`,
`COM1 COM2 BSTR SSTR DSC CMH       (CMPN)`, with one bit per award:

| Bit | Mnemonic | Award |
| --- | --- | --- |
| `0x01` | `COM1` | Army Commendation Medal |
| `0x02` | `COM2` | Army Commendation Medal, second award |
| `0x04` | `BSTR` | Bronze Star |
| `0x08` | `SSTR` | Silver Star |
| `0x10` | `DSC` | Distinguished Service Cross |
| `0x20` | `CMH` | Congressional Medal of Honor |

The blank template a new ranger is created from is itself a literal, at `DGROUP:0x9F7E`:
`"    PFC                    000000"` — 4 spaces, `PFC`, 20 spaces, six zeros — which pins the field
widths independently of the sample file.

The shipped roster decodes as:

| Rank | Name | Score | Tail `+1`,`+2` | Decorations |
| --- | --- | --- | --- | --- |
| CPL | Daniel | 8,950 | `01 00` | — |
| COL | T. van der Beek | 581,350 | `0B 3F` | all six |
| SGT | loser | 18,893 | `02 00` | — |
| COL | Michel | 133,650 | `0B 3F` | all six |
| COL | Daniel | 131,724 | `0B 3F` | all six |
| PSG | General \*Daniel\* | 30,700 | `04 01` | COM1 |

Every rank index and every decoration bitmask agrees with the text line beside it, and the whole
table was checked against the game's own **Assign a Veteran Ranger** screen, which lists the six
rangers with exactly these ranks, names, scores and ribbons.

**Bytes `+3` and `+4` are deliberately not interpreted.** Across the six records they read
`00 00`, `01 A4`, `00 00`, `01 0F`, `02 0E`, `01 E2`, which correlates with neither the score nor
any simple mission count in either byte order, and six samples is not enough to close it. The
trainer round-trips them byte-for-byte rather than guessing.

`ID_A.DAT` and `ID_B.DAT` are eight bytes each — the literals `RANGER_A` and `RANGER_B` — and the
matching string sits at `DGROUP:0x0E37`. They are the disk-identity files the original two-disk
release used to tell the key disk from the play disk.

---

## 5. Copy protection

Two mechanisms, both visible in the data segment.

**Key-disk check.** `DGROUP:0x9F44` holds `AIRBOR.NE` immediately followed by `a:*.*` — a directory
search of drive A: for the marker file. The associated prompts are at `DGROUP:0xBADE`:
*You cannot write to the key disk. / Remove your key disk and insert your play disk. / Cancel. /
Play disk in - store Ranger.* On a hard-disk installation with no floppy this path is not exercised.

**Manual lookup.** `DGROUP:0xB3EE` holds *Which of the above campaign ribbons is the …?* followed by
a 23-entry table of ribbon names at `DGROUP:0xB4D7`:

> Army Achievement Medal, Army Commendation Medal, Army of Occupation Medal, Asiatic-Pacific
> Campaign, Bronze Star, Distinguished Service Cross, Distinguished Service Medal,
> European-African Campaign, Good Conduct Medal, Joint Meritorious Unit Award, Korean Service
> Medal, Legion of Merit, NCO Professional Development, Oversea Service, Presidential Unit
> Citation, Purple Heart, Silver Star, Soldier's Medal, United Nations Service Medal, Valorous
> Unit Award, Vietnam Pres. Unit Citation, Vietnam Service Medal, World War II Victory Medal

You are shown a strip of ribbon artwork (from `RIBBON.DTX`) and asked which one is named. Failing it
prints *Wrong. You are obviously too fatigued to undertake a mission at the present time. Please try
again after some R & R.* — the ribbon check is answered from the manual's colour plate, and the
answer key is the artwork rather than any text, so it cannot be tabulated here the way a
word-lookup protection could. **It did not fire in any of the sessions run for this document**,
all of which used a Practice Ranger.

---

## 6. Mission and briefing tables

`DGROUP:0xA35B` begins the mission-selection message: *Please select your mission.* followed by the
twelve mission names and `***CAMPAIGN***`, each terminated by `0x0D`.

Immediately before them, at **`DGROUP:0xA2D9`**, is the thirteen-character ASCII string
`2111332222333`. It is the **challenge level of each mission**, one digit per list entry including
the campaign: moving the highlight down the mission list changes the *Challenge Level* readout in
the top-right corner to exactly these values in this order (confirmed live — mission 1 shows 2,
mission 2 shows 1, mission 5 and 6 show 3). The rendered digit lands in the two bytes immediately
after the table, at `DGROUP:0xA2E6`.

| # | Mission | Terrain | Challenge |
| --- | --- | --- | --- |
| 1 | Destroy a Munitions Depot | Desert | 2 |
| 2 | Steal a Code Book | Temperate | 1 |
| 3 | Disable Enemy Aircraft | Arctic | 1 |
| 4 | Capture an Enemy Officer | Desert | 1 |
| 5 | Cut a Pipeline | Temperate | 3 |
| 6 | Knock Out Enemy Radar Array | Arctic | 3 |
| 7 | Disable SAM Site | Desert | 2 |
| 8 | Liberate a P.O.W. Camp | Temperate | 2 |
| 9 | Photograph an Experimental Aircraft | Arctic | 2 |
| 10 | Free the Hostages | Desert | 2 |
| 11 | Create a Diversion | Temperate | 3 |
| 12 | Delayed Sabotage | Arctic | 3 |
| 13 | \*\*\*CAMPAIGN\*\*\* | all | 3 |

The terrain column is not a stored table — the briefing for each mission ends with its own
*This is a Desert / Temperate / Arctic mission.* sentence, and the cycle is a clean
Desert → Temperate → Arctic repeat. The full briefing text for each mission is stored verbatim from
`DGROUP:0xA841` onwards and is reproduced in the strategy guide.

The award and promotion messages are equally legible: the promotion ladder at `DGROUP:0xD149`
(*Corporal.* through *Colonel.*) and the decorations at `DGROUP:0xD1E1` (*Commendation 1.*,
*Commendation 2.*, *Bronze Star.*, *Silver Star.*, *Distinguished Service Cross.*,
*Congressional Medal of Honor.*).

### 6.1 The mission-assessment screen

The assessment renderer at `0x7E30` uses a consistent calling convention — *value* into
`DGROUP:0xD723`, *destination* into `DGROUP:0x9730`, then `call 0x82DA` — which is what identifies
`0xA2D4`, `0xA2D6` and `0xA2D8` in §2.2:

```
7E5B  mov al,[0xA2D6] | … | dest 0xD32E   ; "…  soldiers and"
7E7A  mov al,[0xA2D8] | … | dest 0xD340   ; "…  military targets."
7E99  mov ax,[0xA2D4] | … | dest 0xD2CA   ; "…  merit points were earned for"
7EA8  mov ax,[0xD687] | … | dest 0xD43E   ; "you are awarded … merit points."
7EB7  mov ax,[0xD689] | … | dest 0xD3BF   ; "…of … points." (the penalty line)
```

The six-digit renderer these all funnel into is a **table of powers of two as decimal strings** at
`DGROUP:0xD6C3` — `032768`, `016384`, `008192` … `000001`, preceded by a pointer table at
`DGROUP:0xD6A0`. The routine walks the sixteen bits of the value and decimal-adds the corresponding
string, which is why the whole game's scoring is capped at a 16-bit range per rendered field.

---

## 7. Data files

Sixty-odd `*.DTX` files carry the artwork and screens. Every one of them begins with the byte
`0x0B` followed by compressed data with no plain-text runs, so the container is a single compression
scheme (the leading `0x0B` most plausibly a code-width or method byte) that has **not** been
decoded here — nothing in the trainer needs it. Their naming is completely systematic:

| Suffix | Meaning | Prefixes |
| --- | --- | --- |
| `…CHR` / `…SCR` | Character set + screen for one full-screen panel | `TTL` title, `CRED` credits, `MS` mission select, `RA` ranger assignment, `YM` your mission, `FT` (fatigue/ribbon quiz), `SP` supply pod, `MISC`, `ARC`/`DES`/`FARM` terrain briefings, `ANEG`/`DNEG`/`FNEG` failure screens, `MA_G`/`MA_B` assessment good/bad |
| `MAP…` | Terrain tile set | `MAPARC`, `MAPDES`, `MAPFARM` |
| `…SPR` | Sprites | `MENSPR`/`CMENSPR` soldiers, `MASPR`/`CMASPR`, `PODSPR`/`CPODSPR` supply pods, `MISCSPR` |
| `COL…` | Palette per adapter | `COLCGA`, `COLTAN` (Tandy), `COLEGA`, `COLMCG` (MCGA), `COLHRC` (Hercules) |
| `?COL_CH` / `?COL_TEM` | Per-terrain colour/template tables | `ACOL`, `DCOL`, `FCOL` (Arctic, Desert, Farmland) |
| `?FACTS` | Terrain briefing facts | `AFACTS`, `DFACTS`, `FFACTS` |
| `RIBBON` | The copy-protection ribbon artwork | |

The `C…` prefixed sprite files are the CGA variants; `SPEECH.DTX` is referenced by the file-name
table at `DGROUP:0x0246` but is not shipped. `LEES1.MIJ` is a MicroProse music file.
`IBMSNDS.EXE` and `TANDYSND.EXE` are the two sound drivers, named at `DGROUP:0x19C9`/`0x19D5` and
`exec`'d according to the graphics mode chosen.

---

## 8. What was not established

* **The ranger's map position.** No teleport is offered, because no coordinate pair was identified
  reproducibly. This was not attempted exhaustively — the live sessions were short, since the game
  gives you no pause and enemies engage within a minute of landing.
* **Fatigue.** The green bar in the action-view heads-up display was not traced to a variable. It is
  drawn as a bar rather than as digits, so the fill-template trick that cracked everything else does
  not apply to it.
* **The enemy alert level.** Several missions score you on avoiding premature contact, so a flag
  exists; it was not found.
* **`ROSTER.DAT` bytes `+3`/`+4`** of each record (§4).
* **The `.DTX` compression** (§7).
* **Which physical function keys** produce dispatcher codes `0x80`–`0x83`, `0x87`–`0x89`
  (§3.3).

Each of these is a starting point rather than a dead end; the segment map in §1.2 and the
template-fill trick in §2 are the tools that would close them.
