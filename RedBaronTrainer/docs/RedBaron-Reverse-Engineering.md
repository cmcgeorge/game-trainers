# Red Baron (Dynamix, 1990) — Reverse-Engineering Notes

Everything here was established against the copy of the game in
`C:\Temp\Scratch\Win31DOSBox\C-DRIVE\GAMES\RED` (the 1992 Mission Builder release: `RB.EXE`
300,782 bytes, `PS.EXE` 260,214 bytes, dated 24 Apr 2004 on this install), running under
DOSBox-X. Static work used **Ghidra 12.1.2** headless for whole-program analysis and Capstone for
targeted 16-bit disassembly; dynamic work used direct reads and writes of the emulator's guest RAM
while the game ran.

Each claim below is marked with how it was established:

- **[verified]** — reproduced against the running game, usually by writing a value and watching the
  game's own display or behaviour change.
- **[derived]** — read straight out of the executable or a data file, unambiguous but not
  separately confirmed in play.
- **[open]** — noticed but not pinned down. Recorded so the next person does not have to rediscover
  the dead end.

---

## 1. Program architecture

Red Baron is not one program. `BARON.COM` is a 742-byte chain-loader; the game proper is two
executables that hand control back and forth through the filesystem.

```
BARON.COM  ──exec──►  PS.EXE   (front end: menus, career, roster, briefing, debriefing)
    │                    │
    │                    ├──writes SIM.IN──►  RB.EXE   (the flight simulator)
    │                    ◄──reads SIM.OUT──┘
    │
    └──exec──►  RBMB.EXE + RBMB.OVL   (Mission Builder, 1992 upgrade)
```

**[derived]** `BARON.COM` shrinks its own allocation to 0x3E paragraphs, stores a signature at
guest `0000:0510` / `0000:0512`, then `EXEC`s `PS.EXE`. It re-reads the child's exit code and
chains to `RB.EXE` or `RBMB.EXE` from a three-entry pointer table at COM offset 0x3BD:

| COM offset | String |
|---|---|
| 0x399 | `PS.EXE` |
| 0x3A5 | `RB.EXE` |
| 0x3B1 | `RBMB.EXE` |

It also saves and restores the BIOS equipment word's video bits (`40:10`), so a mode set by one
child does not leak into the next.

The two halves never run at the same time. A trainer therefore has to notice *which* program is
currently in the guest and re-locate when it changes — see §4.

### Interchange files

| File | Size | Role |
|---|---|---|
| `SIM.IN` | 12 bytes | Mission parameters written by `PS.EXE`, read by `RB.EXE`. **[verified]** it changes on every mission launch. |
| `SIM.OUT` | 62 bytes | Mission result written by `RB.EXE`, read back by `PS.EXE` for the debriefing. |
| `SCRIPT.DAT` | 334 bytes | The generated mission itself: object list with 32-bit world coordinates. |
| `PSVOLS.MAP` / `SIMVOLS.MAP` | 8 bytes | Which `VOLUME.00n` archive each half needs mounted. |

`VOLUME.001`–`VOLUME.007` plus `VOLUME.RMF` are Dynamix's resource archives (art, 3-D models,
sound). `*.TTM`/`*.TBL`/`*.CLG` are per-aircraft 3-D model, table and collision data.

---

## 2. Executable layout and how `DS` is found

Both executables are 16-bit real-mode MZ images. **[derived]** `PS.EXE`'s data group carries the
literal `Turbo C++ - Copyright 1990 Borland Intl.`, so both are Borland builds of that vintage.

| | `RB.EXE` | `PS.EXE` |
|---|---|---|
| File size | 300,782 | 260,214 |
| Header paragraphs (`e_cparhdr`) | 0x04A0 → 18,944 bytes | 0x0580 → 22,528 bytes |
| Relocations | 4,617 | 5,489 |
| Entry `CS:IP` | 0000:0000 | 0000:0000 |
| Load-relative DGROUP segment | **0x40A8** | **0x35AE** |
| DGROUP image in file at | 0x454C0 | 0x3B2E0 |
| Initialised data | 16,942 bytes | 17,814 bytes |

The DGROUP segment comes straight from the first instruction of each entry point:

```asm
0000:  ba a8 40      mov  dx, 0x40a8        ; relocated DGROUP segment
0003:  2e 89 16 3c02 mov  word ptr cs:[0x23c], dx
0008:  b4 30         mov  ah, 0x30
000a:  cd 21         int  0x21              ; DOS version
000c:  8b 2e 02 00   mov  bp, word ptr [2]
0014:  8e da         mov  ds, dx            ; DS = DGROUP
...
00b8:  8e d2         mov  ss, dx            ; SS = DGROUP too
```

`DS` is loaded once and never changed, and `SS == DS`, so **every global has a fixed `DS:` offset
for the life of the process** and only the load segment moves between runs. `RB.EXE` then zeroes
its BSS from `DS:0x4278` to `DS:0x8872`, which puts the data group's static extent at ~34.9 KB;
the near heap it allocates from lives above that, still inside the same 64 KB segment.

This is what makes a reliable trainer possible without hard-coded addresses: find `DS:0000` once,
and every offset in this document is valid.

---

## 3. Finding the game in guest RAM

Two steps, in this order.

**Guest linear 0.** DOSBox allocates guest RAM as one large private read/write region, but pads
the allocation, so the region base is not guest address 0. **[verified]** the emulated BIOS data
area pins it: `40:0000` holds the COM1 I/O port (`0x03F8`) and `40:0013` the conventional-memory
size in KB (640). On the test machine the pad was 0x40 bytes.

**The data group.** Sweep guest RAM for a literal that sits at a known `DS:` offset, subtract the
offset, and demand corroboration. A candidate whose implied `DS:0000` is not paragraph-aligned is
rejected outright — a real DOS segment base always is, so a non-aligned hit is the same string
sitting in a scratch buffer.

Anchors, all **[verified]** against the live game:

| Program | `DS:` offset | Literal |
|---|---|---|
| `RB.EXE` | 0x014C | `TIME COMPRESS DEACTIVATED.` (primary) |
| | 0x0233 | `YOU'RE LOW ON FUEL.` |
| | 0x026A | `YOU'RE DANGEROUSLY LOW.` |
| | 0x2163 | `JOYSTICK AND RUDDER PEDALS DISABLED` |
| | 0x2E10 | `VOLUME.RMF` |
| `PS.EXE` | 0x0A6F | `Red Baron ver. 1.0, Copyright 1990 Dynamix, Inc.` (primary) |
| | 0x0004 | `Turbo C++ - Copyright 1990 Borland Intl.` |
| | 0x0430 | `DOGFIGHT A FAMOUS ACE` |
| | 0x07DC | `BALLOON BUSTING!` |
| | 0x0AD8 | `PSVOLS.MAP` |

Measured on this machine: `PS.EXE` at `DS 3E59`, `RB.EXE` at `DS 4957`, both resolved with 4/4
corroborating literals in 3–18 ms over a 16 MB guest.

**Can both be present at once?** DOS does not scrub memory it frees, so in principle the program
that just exited is still lying there. In practice the two images overlap in the direction that
matters: `RB.EXE` (300 KB) covers `PS.EXE` (260 KB) entirely, and `PS.EXE`'s data group at
`DS 3E59` runs to `0x4E590`, which covers `RB.EXE`'s at `0x49570`. Observed directly — attaching at
the shell immediately after ending a mission resolved `PS.EXE`, with no stale simulator anchor
anywhere in the guest. The trainer does not rely on that holding: it scores both candidates rather
than stopping at the first, and says so when both stand up.

---

## 4. The Realism Panel — fully decoded

This is the most useful structure in the game, and the one the trainer is built around.

`MREAL.PRF` (single missions) and `CREAL.PRF` (careers) are each **26 bytes: thirteen little-endian
16-bit values**. `PS.EXE` keeps its working copy at **`DS:0x4FBE`** **[verified]**.

### How the order was pinned

The in-game panel (`Alt-R` in the sim, or the Realism Panel button at a briefing) has eleven tick
boxes and two three-way selectors — thirteen controls for thirteen values. Reading the panel on
screen at the **Novice** preset and again at **Expert**, then diffing the file the game wrote:

```
Novice: 1 1 1 0 1 1 0 0 0 0 0 1 0
Expert: 1 1 1 1 1 1 1 1 1 1 2 0 2
```

Exactly one value *falls* between the two presets, and on screen exactly one box is ticked at
Novice and clear at Expert — Midair Collisions, so that is index 11. Exactly two values go to 2,
and the only two three-state controls are Combat Level (Easy/Standard/Hard) and Flight Model
(Novice/Intermediate/Expert), giving indices 10 and 12. The remaining ten then fall out in the
panel's own reading order — left column, then right column — and every value matches in *both*
presets. **[verified]**

That the three later-added controls sit at the end is corroborated by the game's own `READ.ME`,
which documents Mid-Air Collisions and Combat Level as additions made after the manual was printed.

Index 11 looks anomalous — it is the only setting the Expert button turns *off* — but that is what
the game does, observed on screen at both presets: at Novice the MIDAIR COLLISIONS box is ticked, at
Expert it is clear. The `READ.ME` explains why it is not a realism setting like the others: with it
on you die instantly on contact, which at Expert difficulty the designers evidently judged a
coin-toss rather than a skill test.

### The layout

| Index | Setting | Values | Player-favouring value |
|---:|---|---|---|
| 0 | Realistic instruments | 0/1 | — |
| 1 | Sun blind spot | 0/1 | — |
| 2 | Realistic weather | 0/1 | — |
| 3 | Gun jams allowed | 0/1 | **0** |
| 4 | Blackouts allowed | 0/1 | — |
| 5 | Carburettor freezes | 0/1 | — |
| 6 | **Limited ammunition** | 0/1 | **0** |
| 7 | **Limited fuel** | 0/1 | **0** |
| 8 | Real navigation | 0/1 | **0** |
| 9 | **Aircraft may be damaged** | 0/1 | **0** |
| 10 | Combat level | 0 = Easy, 1 = Standard, 2 = Hard | — |
| 11 | Midair collisions | 0/1 | — |
| 12 | Flight model | 0 = Novice, 1 = Intermediate, 2 = Expert | — |

### Why this is the cheat

The five settings marked player-favouring in the table above — 3, 6, 7, 8 and 9 — are not difficulty
tuning; they switch whole subsystems off. With **Limited Ammunition** clear, the sim never
decrements a round counter; with **Limited Fuel** clear, the tank never empties; with **Aircraft May
Be Damaged** clear, hits, flak and heavy landings do nothing; with **Gun Jams Allowed** clear a long
burst never seizes a Vickers; and with **Real Navigation** clear the map keeps showing where you
are.

This also explains a false trail worth recording: differential scans for a decreasing ammunition
counter came back empty on the first several attempts. The default panel had Limited Ammunition
**off**, so there was no counter to find. Setting the panel to Expert and repeating the scan
produced one immediately (§6).

Career scoring is driven by Combat Level and Flight Model, *not* by those five, so clearing them
while leaving index 10 on Hard keeps the top score multiplier for a career that is no longer
actually at risk. The trainer's "No limits" preset is exactly that: identical to the game's own
Expert preset except at indices 3, 6, 7, 8 and 9, which the verification harness asserts so the
preset and this paragraph cannot drift apart.

**[open]** `RB.EXE` reads `?real.prf` (the filename template lives at `DS:0x0661`) but does **not**
keep the thirteen values as a contiguous block in its data group — searching the whole 64 KB for
the known 26-byte vector finds nothing. It presumably unpacks them into individual flags. Editing
the *file* is the reliable route for the sim, and the sim re-reads it at the start of every sortie.

---

## 5. Career, roster and pilot records

### `ROSTER.DAT` — 908 bytes **[verified]**

```
+0x00  8-byte header:  FF FF 00 00 00 00 0A 00
                       └── 0xFFFF = "no active pilot"   └── 0x000A = 10 slots
+0x08  10 x 90-byte pilot records
```

`PS.EXE` holds the same ten records, byte for byte, at **`DS:0x5610`** — a 900-byte read from that
address matched `ROSTER.DAT[8..908]` in 894 of 900 bytes on a live game, the six differences being
fields the shell had advanced since the file was last written. **[verified]**

The career currently being flown is a separate record of the same shape at **`DS:0x557E`**
**[verified]** — creating a career called "Zeno Zwick" made the name appear there, and the shell's
Pilot Record screen renders whatever is written into it.

### The pilot record — 90 bytes

| Offset | Size | Field | Status |
|---|---|---|---|
| +0x00 | 18 | Pilot name, NUL-terminated, not always NUL-padded | **[verified]** |
| +0x12 | 72 | Career state | **[open]** |

Only the name is safe to write. The rest is genuinely not decoded, and it is worth saying why
rather than guessing:

- Writing 32-bit values across +0x20…+0x2F and +0x42…+0x59 *does* change what the Pilot Record
  screen prints for SCORE, AIRCRAFT, BALLOONS and ZEPPELINS — so the fields are in there.
- But the screen shows **sums**, not single fields. A self-identifying probe (the word at offset
  *k* set to 200 + *k* for every even *k* from 32 to 88) produced AIRCRAFT 716, BALLOONS 768,
  ZEPPELINS 819 and TOTAL VICTORIES 2303 = 716 + 768 + 819. No single probe value appears, and one
  of the three sums is odd while every probe was even, so at least one term is read at a byte or
  misaligned offset. The victory totals are aggregated over a table, not stored as three counters.
- Writing into +0x30…+0x41 changes which medals are drawn on the pilot's tunic, so a medal table
  lives in that span, but the same probe did not resolve it to one flag per medal.

Fields that *are* readable by inspection, from the shipped roster and a freshly created career:

- +0x14 (`0xFF` on a new career, an index elsewhere) tracks **Plane Type**, which the Pilot Record
  screen shows as `NONE` for a pilot who has not flown yet. **[derived]**
- +0x17, +0x1B, +0x1C carry small indices that co-vary with squadron, aerodrome and date.
  **[open]**

The trainer therefore edits the name and shows the whole record as hex, and does not pretend to
know more than that.

### Reference data

| File | Contents |
|---|---|
| `ACE.DAT` | 11 famous aces: name, victories, biography, aircraft history. Victories are a byte at +3 of each `FE 00 00` record — Richthofen 0x50 = 80, Udet 0x3E = 62, Voss 0x30 = 48, Lothar 0x28 = 40, Goering 0x16 = 22. **[verified]** against the Dogfight-a-Famous-Ace list. |
| `FIGHTER.DAT` | 22 fighters: 22 × 9-byte records then a name table. Bytes +3/+4 of each record are month and two-digit year of service entry and decode correctly against history (Sopwith Camel 5/17, S.E.5a 4/17, Spad 13 8/17, Fokker D.VII 4/18, Sopwith Snipe 8/18). **[derived]** The remaining fields are **[open]**. |
| `ELITE.DAT`, `ORDINARY.DAT` | Squadron names — 10 elite (Jasta 11, JG 1, No.56 Squadron, The Black Flight, 94th Aero…) and 24 ordinary. |
| `HISTORIC.DAT` | The eight historic missions and their briefings. |
| `APLANE.DAT` | Per-ace aircraft descriptions used by View Airplanes. |
| `SKILL.DAT`, `EVADE.DAT`, `BFC.DAT`, `SDEF.DAT` | AI tuning: pilot skill bands, evasion tables, flight-control and squadron-definition data. |

### Other preference files

| File | Size | Contents |
|---|---|---|
| `CONTROL.PRF` | 6 | Three 16-bit values, `08 04 00 00 01 01` as shipped. Written when the shell exits, not when Preferences changes. **[open]** |
| `SIMPREFS.PRF` | 82 | 41 words: detail/graphics preferences, then thirteen repeats of `00 00 C2 01` — a per-slot view table. **[open]** |
| `MREAL.PRF`, `CREAL.PRF` | 26 each | The realism panels — §4. **[verified]** |

---

## 6. Live simulator state (`RB.EXE`)

### Stick and rudder enable — `DS:0x27B4`, mirrored at `DS:0x6932` **[verified]**

A 0/1 byte, and the flag the in-flight `Alt-J` toggle drives. Confirmed both ways: pressing `Alt-J`
flips both copies together and the sim prints `JOYSTICK AND RUDDER PEDALS ENABLED`/`DISABLED`; and
writing both copies from outside changes what the *next* `Alt-J` press reports, so the game reads
the flag it is given. Both copies must be written or the next toggle only flips one.

### Projectile list — `DS:0x54F0`, 40-byte records **[verified]**

Rounds in flight, not ammunition. Every record carries three 32-bit world coordinates and a set of
fields that are identical across the whole burst (the firing aircraft's velocity). The array
zeroes out completely a second or two after firing stops, which is what first made it clear these
were bullets rather than per-gun counters.

Worth recording as a false trail: the first differential scan flagged two "counters" 0x50 apart
that fell 704 → 688 → 360 while firing and held steady between bursts. They were slots in this
array, not ammunition.

### Ammunition — near-heap, **[verified]** but not statically addressable

With Limited Ammunition on, one 16-bit value decreases only while the trigger is held and holds
steady otherwise (120 → 103 → 95 → 86 → 79 → 72 over five 1.5-second bursts). Writing 500 into it
made the game count down from 500. On the test run it lived at `DS:0xCB58` inside an array of
8-byte nodes (`[value][0x0009][near pointer][flags]`, pointers ascending by 8).

That address is a `malloc` from the near heap and moves every run, and no static `DS:` word in the
data group pointed at it, so there is no stable path to it. This is why the trainer clears
**Limited Ammunition** in the realism panel instead of poking the counter: same outcome, and it
survives the sim being relaunched.

### Not located

**[open]** Fuel, per-system damage, altitude, airspeed and the mission score resisted the same
differential approach. Throttle sweeps (keys `1`–`9`) produced monotone candidates only in what
looked like instrument-rendering buffers below the data group, not in the flight model itself. The
sim keeps the aircraft state in far heap allocated out of a resource pool, and reaching it needs
the object-list pointer chain rather than a `DS:` offset.

---

## 7. The joystick, and why the game says it is not there

This is the part of the game with the most interesting code, and it explains the "controller not
detected" symptom completely.

### Red Baron does not use the BIOS

There is no `INT 15h AH=84h` anywhere in `RB.EXE`. The game talks to the game port directly. The
whole subsystem is a hand-written assembly module linked at load-relative segment **0x2D15**
(file offset 0x31B50), with these entry points:

| Segment offset | File offset | Routine |
|---|---|---|
| 0x000E | 0x31B5E | `joy_read_axes` — near; `BL` = axis mask, returns `SI`=X, `DI`=Y |
| 0x007D | 0x31BCD | `joy_scale` — centre/scale to ±0x7F with a deadzone of 8 |
| 0x00B7 | 0x31C07 | `joy_calibrate` — far; CPU-speed calibration + presence detection |
| 0x01C8 | 0x31D18 | `joy_read_scaled` — far; returns scaled X/Y for stick 0 or 1 |
| 0x0241 | 0x31D91 | `joy_direction` — far; ±30 threshold → up/down/left/right bits |
| 0x029C | 0x31DEC | `joy_button` — far; reads the button bits of port 0x201 |
| 0x02D9 | 0x31E29 | keyboard handler install (`INT 09`/`INT 1C`) |

### The detection algorithm

`joy_calibrate` runs once at start-up:

```asm
; 1. Free-run PIT channel 0, latch it, spin 1000 reads of port 0x201, latch again.
        mov  al, 0x36
        out  0x43, al
        xor  al, al
        out  0x40, al
        out  0x40, al
        mov  dx, 0x201
        mov  cx, 0x3e8            ; 1000 iterations
        out  0x43, al             ; latch counter 0
        in   al, 0x40
        mov  ah, al
        in   al, 0x40
        xchg ah, al
        mov  si, ax               ; start count
loop:   nop / nop / nop / nop
        in   al, dx
        test al, al
        loop loop                 ; always 1000 - this is a stopwatch, not a poll
        ...
        sub  si, di               ; elapsed PIT ticks
        mov  ax, 0x6fcc           ; 28,620 ticks ~ 24 ms
        div  si
        mov  [delay_count], ax    ; per-sample delay-loop iterations
```

So the game measures how fast *this* CPU runs a game-port read loop and derives a delay count from
it. Then, for each stick:

```asm
        mov  bp, [delay_count]
        mov  cx, 0x190            ; 400 - the give-up limit
wait:   mov  ax, cx
        mov  cx, bp
        loop $                    ; calibrated delay
        mov  cx, ax
        in   al, dx
        test bl, al               ; bl = 3 (stick 1 X/Y) or 0Ch (stick 2 X/Y)
        loopne wait               ; spin while the one-shot bits are still high
        jcxz saturated            ; ran out of tries -> 400
        ...
        out  dx, al               ; fire the one-shots
        ; then count delay-loop iterations until each bit falls, again capped at 400
```

and finally:

```asm
        cmp  si, 0x190            ; did X saturate at 400?
        je   .absent
        mov  byte ptr [joy1_present], 1
```

**A count of exactly 400 is how Red Baron concludes "no joystick".** The scale factors it stores
are `0x7F00 / count`, and the derived state lives at these data-group offsets (the module addresses
them off `DS`, and the layout is unambiguous from the code even though the block is not in
`RB.EXE`'s own DGROUP):

| Offset in the module's data | Meaning |
|---|---|
| +0x2BA4 / +0x2BA5 | Joystick 1 / 2 present flag |
| +0x2BA6 … +0x2BA9 | Centre counts, X and Y, per stick |
| +0x2BAA … +0x2BB1 | Scale factors `0x7F00 / centre` |
| +0x2BB4 | The calibrated delay-loop count |

`joy_direction` treats anything beyond ±30 (of ±127) as a deflection, and `joy_scale` zeroes
anything under 8 — so the stick has a built-in ~6 % deadzone and needs ~24 % travel before the game
calls it a direction.

### What actually goes wrong

Three independent things can each produce "no controller", and on this machine two of them were
true at once.

**(a) The pad is on Windows joystick slot 1, not slot 0.** **[verified]** Querying the same API
SDL 1.2 uses — `winmm`'s `joyGetDevCaps`/`joyGetPosEx`, which is what DOSBox and the SDL1 build of
DOSBox-X are linked against — gives:

```
joyGetNumDevs = 16
id=0  caps=165 (JOYERR_UNPLUGGED)
id=1  caps=0   'Microsoft PC-joystick driver'  5 axes, 16 buttons, X=32767 Y=32767   <- the pad
id=2..7  JOYERR_UNPLUGGED
```

The host has an Xbox-compatible pad (`USB\VID_2C16&PID_A2B3`, exposed as `HID\VID_045E&PID_02FF`,
an XInput device) and Windows has it on ID 1 with ID 0 empty. Windows assigns these IDs by device
arrival and never compacts them, so a controller that has been unplugged and replugged, or a second
one connected after a first, ends up above slot 0. DOSBox binds emulated stick 1 to the *first*
stick SDL enumerates and `joysticktype=auto` decides whether to emulate a game port at all from
what SDL saw **at start-up**.

**(b) `[cpu] cycles=max`.** This is the setting in the `dosbox.conf` this install uses. It is the
direct cause of the 400-count saturation: with an unbounded cycle count, the emulated CPU rips
through the calibrated delay loop far faster than the emulated one-shot decays, so
`joy_read_axes` exhausts its 400 tries before the axis line ever falls, and the game writes
"absent". Red Baron's calibration is designed to compensate for CPU speed, but it calibrates
against a *different* loop from the one it then times, and `cycles=max` varies from moment to
moment, so the compensation does not hold.

**(c) `[joystick] joysticktype=auto` with `timed=true`.** `auto` emulates a game port only if SDL
reported a stick when the emulator started — with (a) in play that is a coin toss. `timed=true`
drives the one-shot durations from wall-clock time rather than emulated cycles, which is exactly
the mismatch Red Baron's calibration cannot absorb.

### The fix

Verified working configuration — with this, `RB.EXE` detects the stick, the in-flight `Alt-J`
toggle reports `JOYSTICK AND RUDDER PEDALS ENABLED`, and the enable flag at `DS:0x27B4` reads 1:

```ini
[cpu]
core=normal
cputype=386
cycles=fixed 12000      ; NOT max/auto - this is the one that matters most

[joystick]
joysticktype=2axis      ; emulate a game port unconditionally
timed=false             ; one-shots track emulated cycles, not wall clock
autofire=false
swap34=false
buttonwrap=false
```

And on the Windows side: plug the controller in **before** starting the emulator (SDL only
enumerates joysticks during initialisation), and get it onto slot 0 — unplug every other HID game
device, or re-pair it as the only controller. Slot occupancy is visible in the trainer's Joystick
tab, which queries `winmm` exactly as SDL does.

Two further notes:

- `cycles=fixed 12000` is roughly a fast 286 / slow 386, which is what Red Baron was tuned for. It
  also stops the sim running at absurd speed, which is a playability fix in its own right.
- The `dosbox.conf` shipped with this Win31DOSBox install also has a typo'd `[#speaker]` section
  header, so its PC-speaker settings are being ignored. Unrelated to the joystick, but worth
  fixing while you are in there.

---

## 8. Verified offset summary

Everything the trainer relies on, in one place. All offsets are relative to the owning program's
`DS:0000`.

### `PS.EXE` (shell / career)

| Offset | Size | Contents |
|---|---|---|
| 0x0004 | — | `Turbo C++ - Copyright 1990 Borland Intl.` (locator validator) |
| 0x0430 | — | `DOGFIGHT A FAMOUS ACE` (locator validator) |
| 0x07DC | — | `BALLOON BUSTING!` (locator validator) |
| 0x0A6F | — | `Red Baron ver. 1.0, Copyright 1990 Dynamix, Inc.` (locator anchor) |
| 0x0AD8 | — | `PSVOLS.MAP` (locator validator) |
| **0x4FBE** | 26 | Single-mission realism panel (13 × 16-bit) |
| **0x557E** | 90 | Career currently being flown |
| **0x5610** | 900 | Roster: 10 × 90-byte pilot records |

### `RB.EXE` (simulator)

| Offset | Size | Contents |
|---|---|---|
| 0x014C | — | `TIME COMPRESS DEACTIVATED.` (locator anchor) |
| 0x0233 | — | `YOU'RE LOW ON FUEL.` (locator validator) |
| 0x026A | — | `YOU'RE DANGEROUSLY LOW.` (locator validator) |
| 0x2163 | — | `JOYSTICK AND RUDDER PEDALS DISABLED` (locator validator) |
| **0x27B4** | 1 | Stick and rudder enabled (0/1) |
| 0x2E10 | — | `VOLUME.RMF` (locator validator) |
| 0x54F0 | 40 × n | Projectiles in flight |
| **0x6932** | 1 | Stick and rudder enabled — second copy, must be kept in step |

### File formats

| File | Layout |
|---|---|
| `MREAL.PRF`, `CREAL.PRF` | 13 × `uint16`, order as §4 |
| `ROSTER.DAT` | 8-byte header + 10 × 90-byte pilot records; name = 18 bytes at +0 |

---

## 9. Method notes

Reproducing any of this needs three things, none of them exotic.

**Reading guest RAM.** Attach to the DOSBox process, find the one large private read/write region
(16 MB for `memsize=16`), locate the BIOS data area to pin guest linear 0, and read from there. The
trainer's `GameLocator` does exactly this; `.docs/guest.py` is the throwaway Python version used
during the investigation.

**Differential scanning.** Snapshot conventional memory (0 … 0xA0000 — the game never leaves it),
perform one action, snapshot again. The discriminator that actually worked for ammunition was a
*five*-snapshot pattern: idle, idle, fire, idle, fire, keeping only values that were unchanged
across both idle windows and fell across both firing windows. Three-snapshot patterns kept
producing projectile-array slots.

**Driving the game headlessly.** Red Baron's menus respond to arrow keys and Enter — the pointer
hops between controls — so the whole front end can be driven with `SendKeys` without any working
mouse. Sustained gunfire needs a real key hold (`keybd_event` down, sleep, up); `SendKeys` only
taps. `.docs/Send-Input.ps1` wraps both.

**Watch out for:** the shipped install has **Mouse** off in Preferences, which is why the emulated
mouse pointer appears frozen until you turn it on; and the Realism Panel defaults hide most of the
interesting flight-model state, so set Expert before hunting for it.
