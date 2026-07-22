# Autoduel (PC/DOS, Origin Systems 1985) — Reverse-Engineering Notes

Findings obtained from six DOSBox-X full-process memory dumps (`Data/`), the game
binaries (`Game/`), and Ghidra 12.1.2 disassembly of the game code as loaded in
guest RAM. All values verified against the known game states recorded in
`Data/memdump.md` and cross-checked against the `Game/drivers` save file.

---

## 1. Methodology

1. **Dump anatomy.** Each `dosbox-x-*.bin` is a raw dump of the DOSBox-X process;
   the companion `.csv` maps `FileOffset → ProcessAddress` per memory region.
   The emulated guest RAM is the single largest private read-write region
   (size `0x1001000` = 16 MB + one page). *Guest physical address = offset from
   the start of that region.* The region's process address (`0x174D7156000`) was
   identical in all six dumps (same DOSBox-X instance).
2. **Differential analysis.** Known values from `memdump.md` (money deltas, the
   deliberately-set armor signature 1/2/3/4/5, skills 13/17/20, battery 99) were
   searched across dumps to locate and confirm structures.
3. **Save-file correlation.** The `Game/drivers` file contains the same records
   byte-for-byte, which independently confirmed every field and revealed the
   effects of transactions made after the last dump (weapons sold: weight,
   spaces, and value fields all moved by exactly the catalog amounts).
4. **Ghidra disassembly.** The first 640 KB of guest RAM (dump 5) was imported
   into Ghidra (`x86:LE:16 Real Mode`, image at `0:0`) and sweep-disassembled
   with a custom script (`AutoduelScan.java`); all code references to the
   player/car fields were extracted and read to confirm semantics.

## 2. Memory layout (guest physical addresses)

| Address | What | Notes |
|---|---|---|
| `0x08070` | `AUTODUEL.COM` file image | .COM loaded at PSP `0x07F7`, image = PSP:0100 |
| `0x13440` | `AUTODUEL.OVL` staging area | Phoenix/PLINK86 overlay loader (root in the .COM contains the loader; copyright string "Phoenix Software Associates Ltd.") |
| `0x166B0` | **Game data segment (DS = 0x166B)** | found via `MOV AX,seg / MOV DS,AX` and confirmed by field references |
| `0x1C92D` | **Active player record** (= `DS:0x627D`) | stable across all six dumps |
| `0x1C97D` | Active car record (= player + 0x50, `DS:0x62CD`) | |

The player record equals **COM image base + 0x148BD**. A robust way to find it
at runtime (used by the trainer): scan guest RAM for the 24 code bytes found at
file offset `0x8000` of `autoduel.com`
(`89 7D 21 8B 46 FE 3B 06 C8 89 7D 05 BE 07 00 EB 5B 8B 46 FE 3B 06 CA 89`),
which sits at `com_base + 0x8000`; then `player = hit − 0x8000 + 0x148BD`.

## 3. Number encoding — base-100 ("centimal") integers

Money and most multi-byte quantities are stored as **little-endian base-100
digits, one digit per byte (0–99)**:

```
value = b0 + b1*100 + b2*10000 ...
$649,999  →  63 63 40   (99 + 99*100 + 64*10000)
$2,000    →  00 14 00   (0 + 20*100)
2070 lbs  →  46 14      (70 + 20*100)
```

Confirmed in code (e.g. overlay `1000:56B5`):

```asm
MOV AL,[BX+0x628F]   ; high digit
CBW
MOV DX,0x64          ; ×100
MUL DX
MOV SI,AX
MOV AL,[BX+0x628E]   ; low digit
CBW
ADD SI,AX
```

Money is 3 digits → maximum **$999,999** (the casino's "you broke the bank"
cap is a consequence). A byte of `0xFF` in pair fields means *none/empty*
(code sign-extends and tests negative).

## 4. Player record (40 bytes, at guest `0x1C92D` / `DS:0x627D`)

Offsets relative to record start. "✔" = verified against multiple dumps
and/or disassembly; "~" = probable, partially confirmed.

| Off | Size | Field | Encoding / values | |
|---|---|---|---|---|
| +0x00 | 16 | Driver name | ASCII, NUL-terminated; bytes after NUL are garbage | ✔ |
| +0x10 | 3 | **Money** | base-100 LE, $0–$999,999 | ✔ |
| +0x13 | 1 | **Prestige** | 0–99 | ✔ |
| +0x14 | 1 | **Driving skill** | 0–99 | ✔ |
| +0x15 | 1 | **Marksmanship** | 0–99 | ✔ |
| +0x16 | 1 | **Mechanic ability** | 0–99 | ✔ |
| +0x17 | 1 | Flags | `0x20` = car is with player; `0x80` set while driver active in session (cleared in some save records) | ✔/~ |
| +0x18 | 2 | Unknown | 0 for new driver; premade drivers vary (clone data?) | |
| +0x1A | 1 | **Health** | 3 = healthy, lower = injured, 0 = dead | ✔ |
| +0x1B | 1 | **Current city** | 0–15, see city table | ✔ |
| +0x1C | 1 | Secondary city | destination/route while on the road, else = current city | ✔ |
| +0x1D | 1 | Hour of day | advances quickly while driving | ~ |
| +0x1E | 1 | **Day counter** | Jan 1 2030 = 0 | ✔ |
| +0x1F | 1 | Unknown (year?) | 0 for new driver; Alois has 5 | |
| +0x20 | 1 | Status/string index | constant `0x1D` observed; display code indexes a string-pointer table at `DS:0x1DCE` with it | ~ |
| +0x21 | 1 | **Day of week** | 0 = Sunday; equals `(day+5) % 7` for year 0 (Jan 1 2030 = Friday) | ✔ |
| +0x22 | 1 | **Body armor** | count of body-armor units | ✔ |
| +0x23 | 2 | Unknown pair | read as base-100 pair by travel/shop code; `C0 07` on new drivers | |
| +0x25 | 3 | Sentinels | `0xFF` = none (car/cargo slot references) | ~ |

A second 40-byte record follows at +0x28 (leftover of the previous driver in
the slot — "foo" in the analyzed save; the game reads the whole area in one
block, see §7).

#### Working copies of the current city (live relocation)

The `+0x1B` current-city byte in the record is **not** what the running game
renders the location banner / city menu from. The active session keeps working
copies elsewhere in the data segment; editing only `+0x1B` changes the saved
value (and the trainer's read-back) but does **not** relocate the live game.
Found by diffing the six city dumps — each byte equals the current city in
every stationary dump, and `DS:0x2AD4` reads `0xFF` while on the road:

| Guest phys | DS offset | vs record | Behaviour |
|---|---|---|---|
| `0x19184` | `DS:0x2AD4` | player − 0x37A9 | **active current city**; `0xFF` while driving |
| `0x1C015` | `DS:0x5965` | player − 0x918 | mirror of current city |
| `0x1CABE` | `DS:0x640E` | player + 0x191 | copy in the road/quest block |

A true teleport must set all three **and** `+0x1B`/`+0x1C` (and the car's city
at car`+0x21` when the car is with the player). The trainer's `Teleport` writes
all of these. Note the running game has already loaded the current city's world
into working memory, so the edit takes visual effect only when the game reloads
that world — **save then reload** (confirmed), or leave and re-enter the city;
it does not repaint the city you are currently standing in.

### City IDs (from `Game/citydat`, 16 × 120-byte records; verified vs dumps)

| ID | City | ID | City |
|---|---|---|---|
| 0x00 | Watertown | 0x08 | Providence |
| 0x01 | Manchester | 0x09 | Pittsburgh |
| 0x02 | Buffalo | 0x0A | Harrisburg |
| 0x03 | Syracuse | 0x0B | Philadelphia |
| 0x04 | Albany | 0x0C | Atlantic City |
| 0x05 | Boston | 0x0D | Baltimore |
| 0x06 | Scranton | 0x0E | Dover |
| 0x07 | New York | 0x0F | Washington |

## 5. Car record (0xC5 bytes, at player + 0x50)

| Off | Size | Field | Encoding / values | |
|---|---|---|---|---|
| +0x00 | 16 | Car name | ASCII, NUL-terminated | ✔ |
| +0x10 | 2 | Odometer? | base-100 pair; 0 on the newly-built car, 302 on premade "Solar" | ~ |
| +0x12 | 1 | Body type | 1 = Subcompact (observed); larger car had 2 | ~ |
| +0x13 | 2 | **Max weight** | base-100 pair (2070 for subcompact/light) | ✔ |
| +0x15 | 2 | **Weight left** | base-100 pair; rose exactly 175 lbs when MG (150) + Oil jet (25) were sold | ✔ |
| +0x17 | 1 | Max spaces | 7 observed ("Max Sp") | ✔ |
| +0x18 | 1 | **Spaces left** | rose 1→4 when MG (1) + Oil jet (2) sold | ✔ |
| +0x19 | 1 | Handling class | 2 observed | ✔ |
| +0x1A | 1 | Acceleration | 10 observed | ~ |
| +0x1B | 1 | Suspension | 1 = Improved (observed) | ~ |
| +0x1C | 1 | Chassis? | 0 = Light (observed) | ~ |
| +0x1D | 3 | **Car value $** | base-100 (0x1D..0x1F); dropped $2655→$1405 = exactly MG $1000 + Oil jet $250 + install margin. Originally read as a 2-byte pair — fine below $9,999, but a loaded car sets the +0x1F ×$10,000 digit, so a $26,000 car mis-read as $6,000 until the field was widened to 3 digits | ✔ |
| +0x20 | 1 | Flags | `0x80` observed | |
| +0x21 | 1 | Car's city | garage location / current city | ✔ |
| +0x22 | 1 | **Battery current** | 0–99 | ✔ |
| +0x23 | 1 | **Battery max** | 0–99 | ✔ |
| +0x24 | 160 | **Component table** — 20 records × 8 bytes | see below | ✔ |
| +0xC4 | 1 | `0xFF` terminator | | ~ |

### Component record (8 bytes)

```
[0] type   [1] current DP   [2] max DP   [3] location
[4] spaces used   [5] flags (0x80 = present)   [6] ammo lo   [7] ammo hi (×100)
```

Confirmed directly by the weapon-install routine (overlay `1000:6F8B`), which
writes type, DP (from the DP table), location, spaces and sets flags `0x80`;
lasers (type 5) and heavy rockets (type 11) get `0xDD` in the ammo byte
(self-powered sentinel).

Slot layout (fixed):

| Slot | Component | Location code |
|---|---|---|
| 0 | Power plant | 5 (= Center) |
| 1–4 | Tires | 6=FL, 7=FR, 8=BL, 9=BR |
| 5–9 | Armor facets | 0=Front, 1=Back, 2=Left, 3=Right, 4=Underbody |
| 10–19 | Weapons | facing 0=Front, 1=Back, 2=Left, 3=Right |

Type namespaces per slot kind:

* **Power plants**: 0 = Small (5 DP), 1 = Medium (8 DP, observed on "Solar"), higher = Large/Super.
* **Tires**: 0 = Standard (4 DP); type 2 observed with 9 DP (heavy-duty/solid).
* **Armor**: type byte 0; the DP value *is* the armor points.
* **Weapons**: index into the weapon list below; **0x0C = empty slot**.

### Weapon types and catalog data (tables recovered from the data segment)

Cost table `DS:0x1C2C`, weight `DS:0x1C44`, DP `DS:0x1C1E`, spaces `DS:0x1C73`:

| ID | Weapon | Cost $ | Weight lb | DP | Spaces |
|---|---|---|---|---|---|
| 0 | Machine gun | 1000 | 150 | 3 | 1 |
| 1 | Flamethrower | 550 | 465 | 3 | 3 |
| 2 | Rocket launcher | 1050 | 215 | 3 | 3 |
| 3 | Recoilless rifle | 1550 | 315 | 5 | 3 |
| 4 | Anti-tank gun | 2050 | 615 | 6 | 4 |
| 5 | Laser | 8000 | 500 | 2 | 2 |
| 6 | Minedropper | 550 | 165 | 3 | 3 |
| 7 | Spikedropper | 150 | 40 | 5 | 2 |
| 8 | Smokescreen | 300 | 40 | 5 | 2 |
| 9 | Paint sprayer | 400 | 25 | 2 | 1 |
| 10 | Oil jet | 250 | 25 | 3 | 2 |
| 11 | Heavy rocket | 200 | 100 | 2 | 1 |

(MG cost/weight and Oil jet cost/weight independently confirmed by the
save-file transaction deltas.)

## 6. Key code locations (guest linear addresses, dump session)

| Address | What |
|---|---|
| `0000:9123` | Save-record disk read: `DOS 3Fh read(handle, DS:0x628D, 0x140)` — reads the 320-byte driver data block |
| `0000:99F8–9A56` | Status-screen display: indexes string tables with +0x20, day-of-week table (`DS:0x1DDC`, "Sunday…Saturday") with +0x21, body armor +0x22 |
| `1000:56A9` | Special-cargo scan (compares cargo item id, computes pay = hi×100+lo) |
| `1000:58CF` | Money display/compare helper (3-digit base-100 expansion) |
| `1000:6F8B` | Weapon install: writes component record fields, `0xDD` ammo for laser/heavy rocket |
| `DS:0x1C8B` | Status-screen literals ("Money:", "Prestige:", "Health:", …) |

## 7. `drivers` save file format (18,800 bytes)

```
0x0000  4 × 0x14-byte roster index entries:
        name[16] + money[3, base-100] + prestige[1]
        (first byte of the most-recently-played entry is temporarily zeroed)
0x0050 + i*0x1248   driver block i (i = 0..3), 0x1248 bytes each:
        +0x000  driver record (0x28 bytes, §4)
        +0x028  leftover/aux record (0x28 bytes; old slot occupant observed)
        +0x050  car record (0xC5 bytes, §5) — active or first garaged car
        +0x115  further garage car slots / road & quest state (partially mapped)
```

`0x50 + 4×0x1248 = 18,800` — exact file size. The in-memory image of the
active block is byte-identical to the file block, so **live memory edits are
persisted when the game saves** (on entering a city/garage etc.), and editing
the file while the game is *not* running works too. No checksum was observed
in the save-read path (the code only verifies the byte count); file edits are
accepted directly.

## 8. Endgame / quest data (from overlay strings)

Quest cargo & passwords embedded in `autoduel.ovl`: `GREAT WHITE WHALE`
(cloned heart → Boston Gold Cross), `SAN ANTONIO ROSE` (decoy prize →
Manchester Arena), `LITTLE BIG HORN` (proof of fixed duels → FBI),
`RUMPLESTILTSKIN` (bootleg brain tape from the Outlaw HQ in Watertown → FBI
New York, triggers the win sequence "Congratulations for completing
AUTODUEL"). Details and the rumor chain are in the [strategy guide](strategy-guide.md).

## 9. Open questions

* Player +0x18/+0x19, +0x1F, +0x23/+0x24 exact semantics (clone bookkeeping is
  suspected in +0x18 area; "Clone?" display code wasn't traced).
* Cargo-slot storage layout (records with item id, destination, pay were seen
  being scanned via base `DS:0x628D + 0xC4`, record size ≥ 13 bytes).
* Garage car-slot stride beyond the first car in a block.
* Tire/plant type id ↔ name mapping beyond the observed values.

## 10. Trainer targeting recipe (implemented in the WPF app)

1. Find a process whose name starts with `dosbox`.
2. Enumerate committed private RW regions ≥ 8 MB; treat region start as guest
   physical 0.
3. Search the first 2 MB for the 24-byte signature (§2). `com_base = hit − 0x8000`.
4. `player = com_base + 0x148BD`, `car = player + 0x50`.
5. Sanity-check: health ≤ 10, skills ≤ 99, money digits ≤ 99, name printable.
6. Read/write with `ReadProcessMemory`/`WriteProcessMemory`; base-100 encode.
