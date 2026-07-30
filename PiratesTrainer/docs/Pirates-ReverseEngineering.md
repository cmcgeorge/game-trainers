# Sid Meier's *Pirates!* — Reverse-Engineering Notes

**Target:** `C:\Temp\Scratch\Win31DOSBox\C-DRIVE\GAMES\PIRATES`
**Game:** *Sid Meier's Pirates!* — MicroProse, 1987. The program's own title credits identify it as
**IBM version 432.02**, written by Sid Meier, IBM version by Randall Don Masteller.
**Tools:** Ghidra 12.1.2 (headless import/analysis of `DISKP`), a purpose-written recursive-descent
disassembler over Capstone for the 16-bit real-mode code, and hand decoding of the disk images.
**Method:** entirely static. Nothing in this document was confirmed against a running game on this
machine — see [Confidence](#confidence) for what that means for each claim.

---

## 1. What is actually in the directory

| File | Size | What it is |
|---|---:|---|
| `pir.exe` | 1,983 | A DOS shim. Opens the disk images as ordinary files, installs three interrupt handlers that service the game's raw sector I/O out of them, then EXECs `DISKP`. |
| `disk1` | 368,640 | Raw 360 KB floppy image — "0-PIRATE GAME DISK". Contains the game program and all the data tables. |
| `disk2` | 202,752 | Raw floppy image (FAT12 boot sector, OEM `IBM  3.2`) — "1-PIRATE GAME DISK". Name tables and graphics. Truncated: the BPB describes 720 sectors but the file holds 396. |
| `diskp` | 163,952 | **The game.** A plain MZ executable — this is what actually runs. |
| `disks` | 18,432 | The save "disk". Currently 36 sectors of `0xF6` — an unformatted blank. |
| `Pirates!_Copy Protection Dates.txt` | 4,362 | A transcription of the manual's convoy chart (see §7). |

The original 1987 IBM release was **self-booting**: the disk was its own operating system, and the disk
itself was copy-protected with deliberately bad sectors. This distribution is a conversion of that
booter into an ordinary DOS program. `DISKP` is the booter's payload re-wrapped as an `.EXE`, and its
raw `INT 13h` disk calls have been redirected to `INT 80h`/`81h`/`82h`, which `pir.exe` implements
against the image files.

---

## 2. `PIR.EXE` — the loader and its virtual-disk protocol

Fully decoded; it is small enough to read end to end.

MZ header: 4 pages, no relocations, header 32 paragraphs (512 bytes), `SS:SP = 0x005C:0x01F4`,
`CS:IP = 0x0000:0x052C`. The load image therefore starts at file offset `0x200` and the entry point is
at file `0x72C`.

### 2.1 Startup

```
mov ah,4Ah ; bx=0x140          shrink our memory block to 5 KB
open "DISK1" read-only    -> handle at cs:[0x33]
open "DISK2" read-only    -> handle at cs:[0x35]
open "DISKS" read/write   -> handle at cs:[0x37]      (created 0-length if missing)
copy 1024 bytes from 0000:0000 to cs:[0x51]           save the whole interrupt vector table
set INT 80h -> cs:0x0451      sector read/write
set INT 81h -> cs:0x04F0      select disk
set INT 82h -> cs:0x04F6      keyboard poll / quit hook
EXEC "DISKP"
```

If any image is missing it prints `Files DISK1,DISK2,DISKP must be in current dir.` and exits.

### 2.2 `INT 81h` — select disk

```
sti ; mov cs:[0x32], al ; iret
```

One byte. `AL` = 1 → `DISK1`, 2 → `DISK2`, anything else → `DISKS`. The game issues `mov al,3; int 81h`
before touching the save disk and `mov al,1` before the game disks.

### 2.3 `INT 80h` — sector read / write

An `INT 13h` work-alike: `AH` = 2 read / 3 write, `AL` = sector count, `CH` = track, `CL` = sector,
`DH` = head, `ES:BX` = buffer. Anything other than `AH` 2 or 3 returns `CF=1, AH=0x80`.

The seek is computed as:

```
lba = ((track*2 + head) * sectorsPerTrack + sector - 1) * 512
```

with `sectorsPerTrack` = **9** for `DISK1` and `DISK2` and **4** for `DISKS`. `DISK2` additionally
subtracts 16 from tracks ≥ 34 before the multiply, which is how its shorter image is addressed. The
handler then does an ordinary `LSEEK`/`READ`/`WRITE` (`AH` = `42h`/`3Fh`/`40h`) on the image file.

**This is where the original disk-based copy protection dies.** A booter that checked for a deliberately
malformed sector cannot: every sector now comes from a file, and every read succeeds.

### 2.4 `INT 82h` — the quit hook

```
sti ; push ax ; in al,60h ; cmp al,44h ; je quit ; pop ax ; iret
quit: acknowledge the keyboard (port 61h strobe, EOI to 20h),
      restore the saved 1024-byte interrupt vector table,
      set video mode 2, INT 21h/4Ch
```

Scancode `0x44` is **F10**. So **F10 quits the game to DOS from anywhere** — a facility the original
booter did not have, added by whoever made this conversion.

---

## 3. `DISKP` — image layout

MZ header of 7 paragraphs (112 bytes), 321 pages, 20 relocations, `SS:SP = 0x2667:0x13FC`,
`CS:IP = 0x0000:0x0020`. Load image = 163,840 bytes.

The first 32 bytes of the image are a **segment table** — sixteen words, every one of them relocated:

| Image offset | Raw value | Role |
|---|---|---|
| `0x00` | `0x1124` | **DGROUP** — `DS` and `ES` are loaded from here |
| `0x02` | `0x0FE1` | second code segment |
| `0x04` | `0x1E5F` | graphics data |
| `0x06` | `0x27A7` | tail data |
| `0x08` | `0x2667` | stack (`SS`) |

The entry code is literally `mov cx, cs:[0]; mov ds,cx; mov es,cx; mov cx, cs:[8]; mov ss,cx;
mov sp,0x13FC`. So the memory map, in image-relative terms, is:

| Image range | Contents |
|---|---|
| `0x00000`–`0x1123F` | code (segments `0x0000` and `0x0FE1`) |
| `0x11240`–`0x1E5EF` | **DGROUP** — every global, ~54 KB |
| `0x1E5F0`–`0x2666F` | graphics |
| `0x26670`–`0x27A6F` | stack |

**Every global therefore sits at a constant DGROUP offset.** The absolute segment DOS chooses at EXEC
time varies with the DOS configuration, but it cancels out of `base = anchorHit − anchorOffset`.

File offset ↔ DGROUP offset: `dgroupOffset = fileOffset − 0x112B0`.

---

## 4. The text engine (how the offsets were found)

`DISKP` has no `printf`. All UI text lives in one `0xFF`-delimited table in DGROUP, and there are two
ways to reach it:

* **By address.** `mov si, <dgroupOffset>` then `call 0x329C` (start a message) or `call 0x32AA`
  (append). Those two routines copy bytes from `DS:SI` into a message buffer at `DGROUP:0x93EF` whose
  length lives at `DGROUP:0x93ED`; `call 0x3304` appends one character from `AL` and `call 0x34AD`
  appends a number from `AX`. This is what made the whole disassembly legible — every `mov si, imm`
  in the code resolves to a readable string.
* **By index.** `mov ax, <n>; call 0x1043` walks forward over `n` `0xFF` delimiters from
  `DGROUP:0x0BE2`, reads three bytes (column, row, colour) and draws the text that follows at that
  position. The table holds 589 such records.

Screen position for the address-based path is held in `DGROUP:0x3B78` (column) and `DGROUP:0x3B7A`
(row) — the two most-written globals in the program.

One useful side effect: the player's family name is stored **in the string table itself**, at
`DGROUP:0x104B`, nine characters wide (it ships as the placeholder `123456789`). The name-entry screen
writes characters straight into it.

---

## 5. Confirmed player state

Offsets are from `DGROUP:0000`. "Confirmed" below means a routine in the disassembly can only mean this
field; see [Confidence](#confidence).

### 5.1 Gold — `DGROUP:0x4847`, unsigned 16-bit

The clincher is a matched pair of tiny routines:

```
70DB: mov ax,[0x4847] ; add ax,[0x9A87] ; jae +      ; add gold
      mov ax,0FFFFh                                  ; ...saturating, not wrapping
70E7: mov [0x4847],ax ; ret

70EB: mov ax,[0x4847] ; sub ax,[0x9A87] ; jae +      ; spend gold
      mov word [0x9A87],0FFFFh
      mov si,269Eh   ; "Not enough gold."
      call 329Ch ; call 8862h ; ret
7104: mov [0x4847],ax ; mov word [0x9A87],0 ; ret
```

`DGROUP:0x9A87` is the general-purpose argument slot the whole program uses. Gold saturates at
`0xFFFF` = 65,535 — that is the real ceiling, not a wrap.

### 5.2 Personal wealth — `DGROUP:0x4742`, 16-bit, in **tens** of gold

The retirement screen prints it and then appends a literal `'0'`:

```
9AC3: mov ax,[0x4742] ; mov [0x9A8B],ax ; mov word [0x9A8D],0
      call 34ADh                    ; print the number
      mov ax,30h ; call 3304h       ; append ASCII '0'
      mov si,1A6Dh                  ; " gold pieces"
```

### 5.3 Land — `DGROUP:0x4745`, one byte, in units of **50 acres**

Immediately after the wealth print:

```
9AE4: mov al,[0x4745] ; xor ah,ah ; mov [0x9A87],ax
      mov bx,32h ; imul bx         ; x 50
      call 34ADh ; mov si,1A5Dh    ; " acres of land."
```

The monthly tick adds half the land byte to wealth (`mov al,[0x4745]; shr ax,1; call 7115h`), i.e. the
estate pays an income.

### 5.4 The calendar — `0x9A9F`, `0x9A9D`, `0x9A2B`

Two routines, and both are needed to read either one. The **day-advance** routine is called with a number
of days in the general argument slot `0x9A87` whenever time passes (a voyage leg, a stay in port):

```
91F2: mov ax,[0x9A9F] ; mov [0x9A4F],ax    ; remember the old day
91F8: mov bx,[0x9A87]
91FC: add word [0x9A9F],bx                 ; <-- 0x9A9F is the driven counter
9200: mov bx,1Eh ; call divide             ; oldDay / 30
9206: mov [0x3B4D],ax                      ; old month
9209: mov ax,[0x9A9F] ; mov bx,1Eh ; call divide
9212: mov [0x15CD],ax                      ; new month (scratch)
9215: cmp ax,[0x3B4D] ; jne + ; ret        ; same month -> nothing more to do
921C: mov ax,[0x3B4D] ; inc ax ; mov [0x9A2B],ax
9223: call 92FBh                           ; ...then fall into the month loop
```

and the **month loop** walks one month at a time to the new month, so each intervening month's events
fire, before resynchronising:

```
92CC: inc word [0x9A2B]                    ; step the month
92D3: cmp ax,[0x15CD] ; jge + ; jmp 9223h  ; ...until it reaches the target month
92DC: cmp word [0x9A9F],168h               ; 360
      jl  +
      inc word [0x9A9D]                    ; year++
      sub word [0x9A9F],168h
92EE: mov ax,[0x9A9F] ; mov bx,1Eh ; call divide   ; /30
92F7: mov [0x9A2B],ax                              ; month = dayOfYear / 30
```

So the game runs a flat **360-day year of twelve 30-day months**: `0x9A9F` is the day within the year
(0–359) and is the counter time actually advances, `0x9A9D` the whole years elapsed, and `0x9A2B` the
month index (0–11) — stepped through the loop and then recomputed from the day. Freezing `0x9A9F` is
therefore what stops the calendar; freezing `0x9A2B` achieves nothing, because line `92F7` overwrites it
from the day on the next tick.

The date display then computes the year:

```
9304: mov ax,[0x9A2B] ; mov bx,0Ch ; call divide
      add ax,[0x9A9D] ; add ax,618h        ; +1560
      mov [0x3B4D],ax
9317: mov ax,14h ; imul word [0x9A1F]      ; x20 per era
      add ax,[0x3B4D] ; call 34ADh
```

`year = 1560 + 20 × eraCode + yearsElapsed`.

### 5.5 The era code — `DGROUP:0x475A` (and `0x9A1F` while in play)

The era-selection handler is:

```
3949: mov ax,[0x93DB]      ; 1-based menu choice
      cmp ax,1 ; jne + ; xor ax,ax        ; choice 1 -> 0, 2..6 pass through
3953: mov [0x9A1F],ax
3925: mov word [0x9A1F],5                 ; "No thanks" -> 1660
397C: mov [0x475A],al
```

So the six offered periods store **0, 2, 3, 4, 5, 6** — *not* 0–5 — which is exactly what makes
`1560 + 20 × code` produce 1560 / 1600 / 1620 / 1640 / 1660 / 1680. Code 1 (1580) is arithmetically
valid but never offered. The default, if the player declines to choose, is code 5 = 1660.

This is also cross-checked by the disk loads: the era's data block is fetched with descriptor
`[0x9A1F] + 4` and its name table with `[0x9A1F] + 0x0B`, and the descriptor table (see §6) has exactly
the six matching entries at indices 4, 6, 7, 8, 9, 0x0A and 0x0B, 0x0D, 0x0E, 0x0F, 0x10, 0x11.

### 5.6 Party records — `DGROUP:0x4840`, four × 32 bytes

`Divide Party` / `Join Parties` split your force. The setup code clears `0x4840`, `0x4860`, `0x4880`,
`0x48A0` — a stride of 32 — and byte 0 of each is the in-use flag. Within a record, `+3` is a 16-bit
crew count (it is the divisor in the divide-plunder arithmetic) and `+7` is the gold word above.

### 5.7 Other offsets read out of the code

| Offset | Meaning | How |
|---|---|---|
| `0x473D` | rank/title index | indexes the title-string table at `0x3A1C` on the saved-game list screen |
| `0x473F` | settlement the Treasure Fleet is at | loaded from the fleet route row (§7) |
| `0x4759` | settlement you are at | passed to the index→pointer helper |
| `0x475B` | Treasure Fleet route slot | see §7 |
| `0x9A27` | Pirate Points, out of 100 | printed next to `"PIRATE POINTS: "` / `"/100"` |
| `0x104B` | player family name, 9 chars | §4 |

---

## 6. The disk-transfer descriptor table

Every disk access goes through one dispatcher:

```
27F0: mov [0x3B84],ax        ; descriptor index
2825: mov si,[0x3B84] ; shl si,1
282B: mov bx,[si+0x136B]     ; -> 12-byte descriptor
282F: [bx+0]  disk
      [bx+4]  track (+ [0x3BC8], a caller-set track offset)
      [bx+6]  sector
      [bx+8]  byte count
      [bx+0Ah] DGROUP buffer offset
```

Decoding the table at `DGROUP:0x136B` gives the whole on-disk data layout. The important entries:

| # | Track | Sector | Bytes | → DGROUP | What |
|---:|---:|---:|---:|---|---|
| 0 | 0 | 5 | 1,460 | `0x48C8` | settlement-name pointer block (138 pointers relocated after load) |
| 4, 6, 7, 8, 9, 0A | 0x25 | 7, 9, 0B, 0D, 0F, 11 | 1,024 | `0x4240` | **the six era blocks** |
| 0B, 0D–11 | 0 | 0D–12 | 512 | `0x568C` | per-era name tables |
| 12 | 0 | 9 | 1,920 | `0x4E8C` | famous-expedition templates (24 × 80 bytes) |
| 1F | 0 (+slot) | 1 | 1,940 | `0x4130` | **the saved game** |

Descriptor 4 lands exactly on `disk1` file offset `0x54000` (LBA 672 = track 37, head 0, sector 7),
which is where the era blocks are — an independent confirmation of both the seek formula and the era
block layout.

### 6.1 The saved game

Descriptor `0x1F` moves **1,940 bytes** between the save disk and `DGROUP:0x4130`. That block is the
whole persistent game state, and the slot-validity check is:

```
41FF: mov si,0x4130 ; mov di,0x4128 ; mov cx,8
      compare 8 bytes; equal -> valid
```

The image holds `PIRATES!PIRATES!` at `0x4128`, but note the compare is only **eight** bytes wide and
`0x4130` is the save block itself: the constant reference copy is the first `PIRATES!` at
`0x4128..0x412F`, and the second half *is* the loaded slot's header. A trainer must anchor on the first
eight bytes only — the other eight are run-time state.

Most of §5 sits inside `0x4130 .. 0x48C3`, which is why a single 1,940-byte transfer is enough to save a
game — but **not all of it**. The calendar (`0x9A9F`, `0x9A9D`, `0x9A2B`) and Pirate Points (`0x9A27`)
live well above the block. They are live working copies that the load path unpacks out of it: the
saved-game list screen reads a slot's day counter from the table at `0x4640` (which *is* inside the
block) and writes it to `0x9A9F`, then derives `0x9A9D` from it by dividing by 360.

The save disk is addressed at 4 sectors per track (§2.3) and the slot is selected by setting the track
offset `DGROUP:0x3BC8`. The shipped `disks` is blank, so the on-disk slot directory could not be
validated against a real save — this trainer therefore does **not** offer a save editor.

### 6.2 The settlement table

The index→pointer helper is a three-instruction giveaway:

```
A66E: mov bx,18h ; mul bx ; add ax,4240h ; ret
```

so `city[n]` is at `DGROUP:0x4240 + 24n`, and the era block loaded by descriptor 4 lands right there.
The City Information screen then reads the record field by field:

| Byte | Field | Evidence |
|---:|---|---|
| 0 | flags; bit `0x80` = "you have information about this town" | `and al,80h; jne` before printing anything |
| 1 | map column (0–255, west→east) | positions are constant across eras while everything else changes |
| 2 | map row (0–255, north→south) | same |
| 3 | nation: 0 Spanish, 1 English, 2 French, 3 Dutch | indexes the flag/nation table |
| 4 | low nibble = number of forts | `and ax,0Fh` then `" Fort"` (+ `'s'` if > 1) |
| 5 | garrison ÷ 10 | printed, then the literal `"0 Soldiers."` |
| 6 | (population ÷ 100) − 1 | `inc al`, printed, then `"00 Citizens."` |
| 7 | treasury in thousands | printed between `"Gold: "` and `",000"` |
| 8 | top two bits = prosperity band | `shr al,6`, indexes Struggling/Surviving/Prospering/Wealthy |
| 12–23 | name, 12 chars, space-padded | — |

Decoding all six era blocks this way yields **32, 32, 38, 41, 41, 41** settlements, with sane geography
throughout (Vera Cruz on the western edge, Bermuda far north, Barbados far east) and the right nations
in the right places — Eleuthera and Nassau English in 1560, Curaçao Dutch, Tortuga and Petit Goâve
French by 1660. That coherence is itself strong evidence the field map is right.

Two details worth knowing before anyone "corrects" the generated tables:

* **Towns are renamed when they change hands.** The same map position carries a different name in
  different eras: Borburata → Caracas, Isabella → La Vega, Santiago de la Vega → Port Royale,
  San Catalina → Providence, St. Kitts → St. Christoph. That is the game modelling history, not a
  decoding error, and `FormatCheck` pins the exact set.
* **`GRAN GRANDA` is MicroProse's typo.** The 1680 block spells Gran Granada with eleven letters where
  the other five eras spell it with twelve. `GRAN GRANADA` fits the twelve-column field, so it is not
  truncation — the shipped bytes really do read `GRAN GRANDA`. The generator reproduces it faithfully.

---

## 7. Copy protection

### 7.1 What the original scheme was

The 1987 release carried **two** protections: the disk was a booter with deliberately bad sectors, and
the game asked a manual-lookup question — name the month in which the **Treasure Fleet** or the
**Silver Train** reached a given port in a given year, from a chart printed in the manual. The file
`Pirates!_Copy Protection Dates.txt` in the game directory is a transcription of that chart.

### 7.2 What this build actually does

**Neither protection is active here.**

* The **disk check** is bypassed by construction: `pir.exe` services every sector read out of an
  ordinary file (§2.3), so there is no such thing as an unreadable sector any more.
* The **manual question** is not in the program. The complete `0xFF`-delimited display-string table was
  decoded — all 589 records, covering every screen from the title credits to the retirement epilogue —
  and there is no question text, no "wrong answer" message, and no month-entry prompt. Searching the
  raw bytes of `DISKP`, `DISK1` and `DISK2` for the obvious wording ("manual", "chart", "which month",
  "consult", "incorrect", "try again") also returns nothing.

So this copy runs straight into the game. **No answer is needed.** The rest of this section is the
answer key anyway, because the same tables drive where the convoys actually sail — which is the single
most useful piece of knowledge in the game.

### 7.3 Where the schedule lives

Each 1,024-byte era block ends with two sixteen-byte rows of settlement indices, one entry per
half-month:

| Era block offset | Live address | Row |
|---|---|---|
| `+0x3E0` | `DGROUP:0x4620` | **Silver Train** |
| `+0x3F0` | `DGROUP:0x4630` | **Treasure Fleet** |

An index outside the era's settlement table (`0x20`, `0xCA`, …) is the sentinel that means "the convoy
has left the Spanish Main".

The calendar phase is in the code, not guessed. The new-game setup computes each convoy's slot as:

```
39E0: mov ax,[0x93D7]        ; day within the year
      mov bx,0Fh ; call divide          ; /15  -> half-month 0..23
      sub ax,6                          ; Silver Train bias  (18 for the Treasure Fleet, 39A6)
      mov bx,[0x9A1F] ; and bx,1 ; shl bx,1
      add ax,bx                         ; odd-coded eras run one month earlier
      jns + ; add word [0x9A8B],18h     ; wrap into 0..23
39D0: mov bx,4240h ; add bx,3F0h ; add bx,[0x9A8B] ; mov al,[bx]   ; -> [0x473F]
```

A bias of 18 puts Treasure Fleet slot 0 at half-month 18 — day 270, the **first half of October** — and
a bias of 6 puts Silver Train slot 0 at **the first half of April**. The `+2 × (era & 1)` term shifts
both a month earlier for the odd era codes, which are exactly **1620 (code 3)** and **1660 (code 5)**.

### 7.4 Validation against the shipped chart

Reconstructing all twelve itineraries from the route rows and that phase reproduces the shipped answer
key **entry for entry in eleven of twelve cases**. The generated tables live in
`src/PiratesTrainer/Game/FleetSchedule.cs` and are shown in the trainer's Convoys tab.

*What is and is not checkable from this repository:* the chart that comparison was made against is
`Pirates!_Copy Protection Dates.txt` in the game directory, which is copyrighted material and is not
committed here — so nothing in the repo can re-run that comparison, and the "11 of 12" figure rests on
the one-off check recorded in this document. What `FormatCheck` *does* verify, on every build, is
internal consistency: that every stop names a settlement of its own era, that slots strictly increase
along each itinerary, and that every stop's month and half re-derive from the slot arithmetic in
`PiratesLayout` — plus spot-checks of the individual entries quoted below. The route bytes and the phase
formula are both in the binary, so the reconstruction stands on its own regardless of the chart.

The single disagreement is the last stop of the **1620 Silver Train**:

| | Panama | Puerto Bello |
|---|---|---|
| Shipped chart | Jul – late | **Aug – early** |
| Game data | Jul – late (slots 9–11) | **Sep – early** (slot 12) |

The route row for 1620 is `1F 05 02 13 0B 0E 17 1A 03 11 11 11 16 16 16 CA`: index `0x11` (Panama)
occupies slots 9, 10 and 11, and `0x16` (Puerto Bello) does not appear until slot 12. The other five
eras all show a gap of one to two months between those two ports, so the chart's half-month gap is the
outlier. The binary is authoritative for gameplay; if you are ever asked the question by some other
build, the manual's answer is the one the check would want.

---

## 8. Confidence

| Claim | Confidence | Basis |
|---|---|---|
| `pir.exe` protocol, F10 quit, seek formula | **Certain** | the whole shim was decoded, 1,983 bytes |
| DGROUP at image paragraph `0x1124`; anchor offsets | **Certain** | read out of the MZ segment table and the entry code |
| Gold at `0x4847`, unsigned 16-bit, saturating | **Confirmed** | dedicated add/spend routines, one of which prints "Not enough gold." |
| Wealth `0x4742` (×10), land `0x4745` (×50) | **Confirmed** | the retirement screen's own print sequence |
| Calendar `0x9A9F` / `0x9A9D` / `0x9A2B`, 360-day year | **Confirmed** | the monthly tick and the date display |
| Era codes 0, 2, 3, 4, 5, 6 | **Confirmed** | the menu handler, plus the matching disk-descriptor indices |
| Settlement table at `0x4240`, 24-byte records, field map | **Confirmed** | the index→pointer helper and the City Information screen |
| Convoy routes, phase, and answer key | **Confirmed** | the route rows plus the slot arithmetic; agrees with the shipped chart 11/12 |
| Save block `0x4130`, 1,940 bytes, `PIRATES!` magic | **Confirmed** | the transfer descriptor and the slot-validity compare |
| Crew at `0x4843`; party stride 32 | **Inferred** | consistent with the divide-plunder arithmetic and the setup code, but no routine names it |
| Rank `0x473D`, current city `0x4759`, Pirate Points `0x9A27` | **Inferred** | consistent use, not proved by a single routine |
| **Any of this against a live game** | **Untested** | no run was performed; the trainer validates three anchors and shows you what it read so you can check |

That last row is the important one. The trainer never trusts a single string match: it requires the
copyright literal, the eight-byte `PIRATES!` magic and the `JAN…DEC` month table to all sit at their known
offsets from the same base, then requires the era code, the date and the settlement table to decode
sanely — and then it shows you the captain's name, the date and the town list so you can check it
against the screen before poking anything. If that verification fails, use the value scanner.

---

## 9. Reproducing this

```bash
# strings, with offsets
python strings.py DISKP.EXE 5

# Ghidra headless (16-bit MZ; the loader splits blocks on the segment table)
analyzeHeadless.bat <proj> PiratesRE -import DISKP.EXE -postScript DumpInfo.java

# the useful part: recursive-descent disassembly with string annotation
python show.py 0 0x70DB 0x7110     # the gold routines
python show.py 0 0x92C0 0x9360     # the monthly tick
python show.py 0 0x3909 0x3A00     # era selection + convoy slot arithmetic

# decode the era blocks and check the answer key
python extract.py --dump
```

The scripts referenced above were working files, not deliverables; the decoded results they produced
are committed in `src/PiratesTrainer/Game/CityBook.cs` and `FleetSchedule.cs`, and every non-obvious
constant is repeated with its derivation in `src/PiratesTrainer/Game/PiratesLayout.cs`.
