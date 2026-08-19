# Roadwar 2000 — reverse-engineering notes

Roadwar 2000, Strategic Simulations Inc., 1987. PC version by Edward Haar from Jeffrey A.
Johnson's 1986 Apple II design. Everything below was recovered from the shipped files, from a
disassembly-level read of `START.EXE`'s data segment, and from driving the game under DOSBox
while dumping and poking its guest RAM. Each section says how the claim was established, and
§10 lists what was probed and *not* established, so nobody re-walks the same dead ends.

Confidence markers used throughout:

| Marker | Meaning |
| --- | --- |
| **[Confirmed]** | Written to live memory (or read back out of a save the game itself wrote) and the change was observed on the game's own screens. |
| **[Measured]** | Derived from data that matches across two independent sources — the shipped files and live RAM, or the engine tables and the printed manual. |
| **[Inferred]** | Consistent with everything seen, but not directly exercised. |

---

## 1. The shipped files

```
START.EXE     64,778   the whole game: loader, main loop, and all initialised data
SETUP.R2K     37,813   overlay
A.R2K         19,947   overlay: city scouting, empire status, RDF, save/load, supply screens
B.R2K         21,635   overlay
C.R2K         20,864   overlay
D.R2K         13,991   overlay
E.R2K          9,637   overlay
H.R2K         15,089   overlay
J.R2K         26,752   overlay
ROAD.R2K      19,687   overlay (road combat)
TRANS.R2K     11,841   overlay
WORK.R2K      10,683   overlay
WEST.MAP       2,024   western overland map
EAST.MAP       2,024   eastern overland map
MAP0/1/2/3/6/19/20/21/22.R2K   ~2,024 each   tactical-combat terrain maps
RWSHAPE.RGB / .CMP / ROADSH.2 / .3   4,808 each   sprite sheets, one per display mode
PRES, TPIC     4,438   title artwork
CHICAGO.RWS    6,512   a saved game (see §3)
rw2000.txt    54,578   the manual, transcribed by Soulblazer for Home of the Underdogs
```

`START.EXE` is a plain MZ executable — no compression, no packer. Header: 127 pages, 266 bytes in
the last, 2 relocations, a 512-byte header, `CS:IP = 0000:0002`, `SS:SP = 0FB1:0080`. The build
identifies itself in its own strings: **`Lattice C 2.1`**, small model. Its runtime strings
include `$*** STACK OVERFLOW ***`, `Invalid I/O redirection`, and the loader messages
`$SETUP.R2K` / `Insert Game Diskette in Drive A:`. **[Measured]**

Because there is no compression and no DOS extender, the data segment is at a fixed offset within
the load image, and every table in it can be read straight out of the file. That is what makes
this game unusually tractable compared with, say, the PKLITE'd Darklands or the DPMI titles
elsewhere in this repository.

### Locating the data segment inside the EXE

The save file (§3) turned out to be a verbatim image of part of the data segment, so aligning the
two locates everything at once. The best alignment of `CHICAGO.RWS` against `START.EXE` is at file
offset **`0xA48A`**, and the vehicle-name pointer table inside it holds absolute data-segment
offsets, which fixes the base:

```
load module starts at file 0x200 (32 header paragraphs)
slab at file 0xA48A  ->  load-module offset 0xA28A
DS:0000 is at load-module offset 0x80D0, i.e. DS = load segment + 0x80D
therefore  DS offset d  <->  START.EXE file offset 0x82D0 + d
```

Checked: `"MOTORCYCLE"` sits at file `0xA524`, which is `DS:0x2254`, which is what the first entry
of the pointer table at `DS:0x2366` says. **[Measured]**

### Overlay format

Each `.R2K` overlay is a 6-byte header — four bytes that vary per file, then the constant
`88 41` — followed by code that begins with the Lattice stack-check prologue
`55 83 EC nn 72 06 3B 26 17 00 77 03 E9 xx xx 8B EC` (`push bp; sub sp,n; jb …; cmp sp,_stklim;
ja …; jmp overflow; mov bp,sp`). The overlays share the data segment with `START.EXE` and copy
their own literal pools into it; a string from the loaded overlay was found in guest RAM at a
constant offset from its file position. **[Measured]** Overlay code was read only far enough to
identify which overlay owns which screen — the trainer never touches overlay memory.

---

## 2. The data segment map

Offsets are relative to `DS:0000`. The slab the save covers is `0x21BA`–`0x3B29`.

| DS range | Contents |
| --- | --- |
| `0x03C7`–`0x0BA6` | the loaded overland map, 2,016 terrain bytes read verbatim from `WEST.MAP` or `EAST.MAP` |
| `0x21BA`–`0x3B29` | **the saved slab** — everything in §3 |
| `0x3A50`–`0x3E0A` | reference string tables (terrain, ranks, residents, gangs, scientists, supplies) |
| `0x3E12`+ | scratch and stack |

### Reference string tables

Each is a block of NUL-terminated strings followed by a table of 16-bit pointers into it. Found
by scanning the segment for runs of four or more consecutive words that resolve to printable
strings. **[Measured]**

| Table at | Entries | Contents |
| --- | --- | --- |
| `0x2366` | 19 | vehicle type names |
| `0x26A2` | 28 | loot-site names |
| `0x2D82` | 120 | city names |
| `0x3ACE` | 10 | city residents, short form (`NO ONE.`, `LAWFUL NATL GD`, `RENEGADE NATL GD`, `A LOCAL GANG`, `BUREAUCRATS`, `SURVIVALISTS`, `REBORNERS`, `SATANISTS`, `INVADERS`, `THE MOB`) |
| `0x3B2E` | 10 | the same factions, long form |
| `0x3B84` | 7 | foot-gangs (`STREET GANGSTERS`, `ARMED RABBLE`, `MERCENARIES`, `NEEDY`, `CANNIBALS`, `SATANISTS`, `MUTANTS`) |
| `0x3BC2` | 5 | crew ranks (`ARMSMASTER`, `BODYGUARD`, `COMMANDO`, `DRAGOON`, `ESCORT`) |
| `0x3BFE` | 23 | terrain names (§6) |
| `0x3CA2` | 8 | the G.U.B. scientists |
| `0x3CE6` | 6 | vehicle upgrade shops (`SPEED`, `PERFORMANCE`, `FOUNDRY`, `BRAKE`, `WELDING`, `UNDERBODY`) |
| `0x3D16` | 6 | supplies (`FOOD`, `TIRES`, `FUEL`, `AMMO`, `GUNS`, `MEDICINES`) |
| `0x3DC2` | 11 | named road gangs (`FURIES`, `MUTHUH TRUCKERS`, `MOTORHEADS`, `HOT ROD LINCOLNS`, `HARD HATS`, `GREYHOUNDS`, `REDNECK YAHOOS`, `DUNE BUGGERS`, `SKULLS`, `ROUGHRIDERS`, `INVADER DEATH SQUAD`) |
| `0x3E00` | 5 | generic road-gang types |

The eight scientists are **Myron Smidlapp, Alec Trotier, Pedro Pintero, Gloria Mills, Gabriel
Washington, Donny Dade, Dorothy Macalister** and **Cheng Lu Sinh** — the same eight the journal in
the manual names.

Two strings in `A.R2K` give away the endgame outright: `THE G.U.B. IS LOCATED IN %s` and
`THE PASSWORD IS PANACEA.`

---

## 3. The save format

**`.RWS` files are a raw image of `DS:0x21BA`–`DS:0x3B29` — 6,512 bytes, no header, no length
field, no checksum, no compression.** [Confirmed]

Three independent observations establish it:

1. Recalling the shipped `CHICAGO.RWS` and dumping guest RAM immediately afterwards gave a slab
   that matched the file in **6,509 of 6,512 bytes**.
2. Nine values were poked into live memory through the trainer's own write path; the game was then
   asked to save. The written file carried **all nine**, and again differed from the live slab in
   the same three bytes.
3. The best alignment of the file against `START.EXE`'s initialised data is unique and exact, and
   the diff between them is precisely the set of fields that a game in progress would have changed.

The three volatile bytes are slab `0x0008`, `0x0697` and `0x1262`. They differ every time because
the save/load routine itself writes them; the trainer's save editor leaves them alone and its
"compare with running game" check ignores them.

Despite prompting `Place your SAVE Game Disk in Drive A` and `Insert Formatted Save Game Diskette`,
the PC build writes the file into **the current working directory** — normally the game folder.
Verified by saving as `TEST` inside DOSBox and finding `TEST.RWS` beside `START.EXE`. **[Confirmed]**

### Slab layout

| Slab offset | DS | Size | Field | Confidence |
| --- | --- | --- | --- | --- |
| `0x0004` | `0x21BE` | u16 | current overland map: 1 = `WEST.MAP`, 2 = `EAST.MAP` | Confirmed |
| `0x001C` | `0x21D6` | u16 | day of the year | Confirmed |
| `0x001E` | `0x21D8` | u16 | time of day; the clock reads `6 + value`, so 0 is 6:00 AM | Confirmed |
| `0x0022` | `0x21DC` | u8 | party X, 1-based | Confirmed |
| `0x0023` | `0x21DD` | u8 | party Y, 0-based | Confirmed |
| `0x0026`–`0x0099` | | 29 × 4 | array initialised to `FF FF 00 00`; see §10 | — |
| `0x009A` | `0x2254` | 274 | 19 vehicle type names — **the locator's anchor** | Confirmed |
| `0x01AC` | `0x2366` | 38 | 19 pointers into the name block | Confirmed |
| `0x01D2` | `0x238C` | 19 × 24 | vehicle type templates (§4) | Measured |
| `0x039A` | `0x2554` | 334 | 28 loot-site names | Measured |
| `0x04E8` | `0x26A2` | 56 | 28 pointers into them | Measured |
| `0x0520` | `0x26DA` | 28 × 12 | loot table (§7) | Measured |
| `0x0678` | `0x2832` | u8 | terrain code of the square the gang is on | Confirmed |
| `0x068E` | `0x2848` | u8 | Radio Direction Finder: non-zero = fitted | Confirmed |
| `0x068F` | `0x2849` | u8 | doctor's skill; 0 = no doctor | Confirmed |
| `0x0690` | `0x284A` | u16 | antitoxin doses | Confirmed |
| `0x0692` | `0x284C` | u8 | drill sergeant's skill; 0 = none | Confirmed |
| `0x0693` | `0x284D` | u8 | politician's skill; 0 = none | Confirmed |
| `0x0694` | `0x284E` | u8 | snow tires — the `*` beside TIRES | Confirmed |
| `0x0695` | `0x284F` | u8 | fuel special — the `*` beside FUEL; roughly halves consumption | Confirmed |
| `0x0698` | `0x2852` | u8 | maximum vehicles, 1–15 | Confirmed |
| `0x06D5` | `0x288F` | 1,267 | 120 city names | Measured |
| `0x0BC8` | `0x2D82` | 240 | 120 pointers into them | Measured |
| `0x0CB8` | `0x2E72` | 120 × 12 | city records (§5) | Confirmed |
| `0x1570` | `0x372A` | 20 | gang name, NUL-terminated | Confirmed |
| `0x1584` | `0x373E` | u8 | vehicles owned | Confirmed |
| `0x1586` | `0x3740` | u16 | food | Confirmed |
| `0x1588` | `0x3742` | u16 | tires | Confirmed |
| `0x158A` | `0x3744` | u16 | fuel (stored; see below) | Confirmed |
| `0x158C` | `0x3746` | u16 | ammo | Confirmed |
| `0x158E` | `0x3748` | u16 | guns | Confirmed |
| `0x1591` | `0x374B` | u8 | party X, mirrored | Confirmed |
| `0x1592` | `0x374C` | u8 | party Y, mirrored | Confirmed |
| `0x1595` | `0x374F` | 5 × u16 | crew by rank: armsmaster, bodyguard, commando, dragoon, escort | Confirmed |
| `0x159F` | `0x3759` | u16 | medical supplies | Confirmed |
| `0x15B2` | `0x376C` | 15 × 50 | vehicle records (§4) | Confirmed |

Note the crew array's **odd** address. Lattice C packs structures with no padding, so five 16-bit
counts sit at `0x1595`, `0x1597`, `0x1599`, `0x159B`, `0x159D` — misaligned, and perfectly legal
on an 8086. Reading them as aligned words is the single easiest way to get this format wrong.

**Fuel has two readings.** The stored word is the total. The Gang Status screen prints
`stored − 2 × fuel consumption`, because every vehicle keeps two moves' worth in its tank and that
reserve does not occupy cargo space; `X)amine Supplies` prints the stored figure. Confirmed by
poking 5,555: the G screen read 5,477 with a consumption of 39, and the X screen read 5,555.

Each of these was established the same way: dump guest RAM, change one thing in the game, dump
again, and diff. Moving one square west changed exactly `0x0022`, `0x1591`, the terrain byte, and
the fuel word — by 49, the gang's fuel consumption. Moving north changed `0x0023` and `0x1592`.

The write direction was then verified individually: poking `0x1588`/`0x158A`/`0x158C`/`0x158E`/
`0x1595` gave a Gang Status screen reading `TIRES 1111`, `FUEL 5477`, `AMMO 2222`, `GUNS 3333`,
`CREW 7/43/70/77/58`; poking `0x159F` moved MEDICAL SUPPLIES to 55; poking `0x284A` moved
ANTITOXIN to 99; poking `0x2852` moved MAX VEHICLES to 15.

The crony bytes were isolated one at a time. Zeroing `0x0692`–`0x0694` together removed DRILL
SERGEANT and POLITICIAN and the `*` beside TIRES while DOCTOR stayed; restoring `0x0692` alone
brought DRILL SERGEANT back; zeroing `0x068F` removed DOCTOR. Setting `0x0695` put a `*` beside
FUEL and dropped FUEL CONSUMPTION from 39 to 20. The RDF was narrowed by bisection over
`0x068B`–`0x068E` to `0x068E`. **[Confirmed]**

---

## 4. Vehicles

### The 19 type templates — 24 bytes each

| Offset | Field |
| --- | --- |
| +0 | mass |
| +1 | structure |
| +2 | maximum speed, in **tens** of MPH |
| +3 | manoeuvrability |
| +4 | braking |
| +5 | acceleration |
| +6..+9 | missile factor: left, right, front, back |
| +10..+14 | missile protection: left, right, front, back, **top** |
| +15 | volleys (1 or 2) |
| +16 | tires; **0 means treads**, which cannot be shot out |
| +17..+20 | boarding factor: left, right, front, back |
| +21 | interior crew capacity |
| +22 | topside crew capacity |
| +23 | fuel consumption per overland move |

Cross-checked field by field against the Vehicle Table printed in the manual. Three things fell
out of that comparison:

* **Interior capacity is stored one lower than it is displayed.** The engine holds 50 for a
  trailer truck and the Vehicle Stats screen prints 51 — the driver. Topside capacity is stored as
  displayed. This is why the manual's bus topside figure of 51 is a misprint: the engine has 50.
* **The `*` in the manual's table is the volley count.** Exactly the four types the manual marks
  (motorcycle, sidecar, tractor, construction vehicle) hold 1 in +15; every other type holds 2.
* **Carrying capacity is not stored at all — it is `5 × mass²`.** Exact for all nineteen types:
  motorcycle mass 1 → 5 spaces, midsize mass 5 → 125, bus mass 14 → 980, trailer truck mass 20 →
  2,000. Every value in the manual's carrying-capacity column is reproduced by that formula.
  **[Measured]** The formula also predicted the live game correctly: adding a trailer truck raised
  TOTAL CAPACITY by exactly 2,000. **[Confirmed]**

Two small disagreements with the manual, where the engine's numbers are what the game actually
uses: the **motorcycle**'s front missile protection is 1, not 2, and the **sidecar**'s front and
back protection are 1 and 1, not 2 and 2.

### The 15 instance records — 50 bytes each

| Offset | Field |
| --- | --- |
| +0x00 | type id, 0–18 |
| +0x01 | mass (copied from the template) |
| +0x02 | structure, **maximum** |
| +0x03 | structure, current |
| +0x04 | manoeuvrability, maximum |
| +0x05 | manoeuvrability, current |
| +0x06 | braking |
| +0x07 | acceleration |
| +0x08..+0x0B | missile factor L, R, F, B |
| +0x0C, +0x0D | weapon type per volley; 2 = firearm, 1 = crossbow |
| +0x0E..+0x12 | missile protection L, R, F, B, T |
| +0x13, +0x14 | **unidentified**; read 2/2 on every crewed vehicle seen except one, which read 0/2 |
| +0x15 | tires, maximum |
| +0x16 | tires, current |
| +0x17..+0x1A | boarding factor L, R, F, B |
| +0x1B | interior capacity (displayed value is this + 1) |
| +0x1C..+0x20 | interior crew by rank |
| +0x21 | topside capacity |
| +0x22..+0x26 | topside crew by rank |
| +0x27..+0x2B | zero in every record seen |
| +0x2C | fuel consumption |
| +0x2D | maximum speed, tens of MPH |
| +0x2E | current speed, tens of MPH |
| +0x2F | facing, 1–8, on the same rosette as the movement keys |
| +0x30, +0x31 | `01 00` in every live vehicle seen |

Note that maximum comes **before** current for structure, manoeuvrability and tires — the reverse
of the usual convention, and the sort of thing only a screen-by-screen comparison settles. It was
settled by reading the game's Vehicle Stats page for four vehicles across three types and matching
every printed figure: a sports car hardtop reading `15/15`, `3/3`, `3`, `3`, `F/F`, `4/4`, `3/14`,
facing `4`, protection `5/5/5/5/4`, interior `4` with crew `2/2/0/0/0`, topside `4` with
`1/3/0/0/0`; and a trailer truck reading `25/55`, `0/1`, `2`, `3`, `18/18`, `4/10`, facing `3`,
protection `5/5/5/4/4`, interior `51` with `2/3/10/18/18`, topside `50` with `1/3/10/18/18`.
**[Confirmed]**

**Adding a vehicle works.** Writing a complete 50-byte trailer-truck record into slot 8 and raising
the vehicle count from 8 to 9 produced, on the game's own screen: `VEHICLES NOW: 9`, TOTAL CAPACITY
up by 2,000, PASSENGER CAPACITY up by 101 (51 interior + 50 topside), and FUEL CONSUMPTION up
accordingly. **[Confirmed]**

---

## 5. Cities

120 records of 12 bytes at slab `0x0CB8`. The name table has exactly 120 entries, and the manual's
regional appendix lists exactly 120 cities.

| Offset | Field |
| --- | --- |
| +0 | supply/population figure; falls as the town is stripped |
| +1 | map: 1 = west, 2 = east |
| +2 | X |
| +3 | Y |
| +4..+8 | cache: food, tires, fuel, guns, medical — at most 255 each |
| +9 | resident faction |
| +10 | resident strength |
| +11 | zero in the shipped data and in the save |

Diffing the save's city table against `START.EXE`'s initial one shows changes at exactly four byte
positions: `+0` (30 cities, looted), `+4`/`+5`/`+6` (a handful, caches), `+9` (90 cities) and
`+10` (59 cities). So `+9`/`+10` are what the engine randomises at the start of a game, and the
cache columns are what the player fills. **[Measured]** The cache reading is corroborated by the
shipped save: Chicago — where that save was taken — holds 255 food and 255 tires and nothing else,
which is exactly what a player's stash looks like.

Sizes are recognisable, and it matters **which** table they are read from. `START.EXE`'s
initialised data holds the pristine figures — New York 228, Los Angeles 187, **Chicago 178**,
Philadelphia 118, Detroit 109, San Francisco/Oakland 82, Houston 76, Dallas/Fort Worth 75 — while
the shipped save holds a game in progress in which 30 of them have been looted down (Chicago to
150, Washington DC from 77 to 54, and Ottawa and Greenville/Spartanburg all the way to 0). The
trainer bakes in the EXE's column, because "restock a town to the level it shipped with" can only
mean the pristine one, and because a baked-in 0 would make those two towns unrestockable. The
verifier pins the whole column, plus map and position, against `START.EXE` byte for byte and
separately asserts that the save still differs in exactly 30 places — if those two ever agree, the
sources have been conflated again.

### One anomaly worth knowing about

`HOUSTON` is the only record whose X is **0** (map 2, `(0, 32)`). Under the engine's flat index
(§6) that wraps onto row 31, column 47 — and the east map does carry a large-metropolis tile at
precisely that wrapped square, so the map data was authored to match. But teleporting a gang there
in the running game prints a **blank** location line: the terrain name for a city code is empty
because the engine normally substitutes the city's name, and its city lookup does not name this
one. So the square is real and reachable and still not somewhere to send a gang. The trainer
indexes it the way the engine does but refuses it as a teleport target. **[Confirmed]**

---

## 6. The overland maps

`WEST.MAP` and `EAST.MAP` are an 8-byte header (`FD`, three bytes, one byte, a 16-bit length of
`0x07E0` = 2,016, one byte) followed by **2,016 terrain bytes**, which the engine reads verbatim
into `DS:0x03C7`. Confirmed by finding `EAST.MAP`'s body byte-for-byte at that address in guest
RAM while the game was on the eastern map. **[Confirmed]**

**The grid is 48 columns by 42 rows, and the square at `(X, Y)` is index `Y × 48 + (X − 1)`.**
X is 1-based; Y is 0-based; X grows east and Y grows south.

Three independent confirmations:

1. Stepping the gang one square west moved the party marker in guest RAM by exactly **−1**;
   stepping north moved it by exactly **−48**.
2. All **120** shipped city records land on a city tile (code 19, 20 or 21) of their own map under
   that rule — and only 9 of 120 do under any other offset.
3. Teleporting to `(20, 20)` on the east map made the status line read `FOREST`, and
   `EAST.MAP[20 × 48 + 19]` is code 3, forest.

While the gang is on a square, the engine ORs **`0x80`** into that map byte to mark it. Any reader
has to mask the top bit off. **[Confirmed]**

### Terrain codes

The name table at `DS:0x3BFE` has 23 entries:

| Code | Name | Code | Name |
| --- | --- | --- | --- |
| 0 | Plains | 7–18 | Road |
| 1 | Farmland | 19 | *(blank)* small metropolis |
| 2 | Desert | 20 | *(blank)* large metropolis |
| 3 | Forest | 21 | *(blank)* metroplex |
| 4 | *(blank)* water | 22 | Oilfield |
| 5 | Ruins | | |
| 6 | *(blank)* wilderness | | |

The blank entries are deliberate. For a city code the engine prints the city's name instead; for
water and wilderness the gang can never be standing there, so no name is needed.

**Codes above 22 exist in the map files** — 31, 37, 38, 43, 45, 46, 55 and others, about 260
squares across the two maps. They are coastline, mountain and open-water artwork. They are *not*
in the name table, and the engine does not bounds-check: teleporting onto a code-31 square made
the status line read `ASE!`, i.e. it followed a pointer read past the end of the table. So
codes above 22 are impassable scenery, and the trainer treats them as such and refuses to teleport
onto one. **[Confirmed]**

Column 47 of the east map is code 31 for 41 of its 42 rows — the seam where the two maps meet.

---

## 7. Loot

28 records of 12 bytes at slab `0x0520`, in the order of the name table
(`CONVENIENCE STORE`, `SUPERMARKET`, `SHOPPING MALL`, `MILITARY BASE`, `FARM`, `RANCH`,
`SPORTING GOODS STORE`, `GUN SHOP`, `ARMORY`, `RESTAURANT`, `BODY SHOP`, `HIGH SCHOOL/COLLEGE`,
`AUTO DEALER`, `TIRE STORE`, `JUNKYARD`, `GAS STATION`, `PARKING LOT`, `FUEL STORAGE TANK`,
`MEDICAL CENTER`, `HOSPITAL`, `VETERINARIAN`, `CACHE`, `POLICE STATION`, `BUS DEPOT`,
`TAXI GARAGE`, `SHELTER`, `DRUG STORE`, `RACING TRACK`).

Bytes +0..+3 are the site's relative frequency in four terrain classes; +4..+9 is the payout;
+10 and +11 are zero in every record.

The frequency reading is forced by the two agricultural sites, which are mirror images of each
other: `FARM` reads `26, 80, 1, 6` and `RANCH` reads `80, 26, 1, 6` — and the manual says farms are
common in farmland and ranches on the plains. **[Measured]**

Every payout column is pinned by at least one site that pays in it **and in nothing else**, which
is what makes the mapping forced rather than guessed:

| Byte | Column | Pinned by |
| --- | --- | --- |
| +4 | Food | `SUPERMARKET` 50, `CONVENIENCE STORE` 10, `RESTAURANT` 10 — none of which pay anything else |
| +5 | Guns | `GUN SHOP` 20, `ARMORY` 40, `SPORTING GOODS STORE` 5 — likewise |
| +6 | A vehicle (flag) | 1 at exactly eight sites — shopping mall, military base, body shop, high school/college, auto dealer, bus depot, taxi garage, racing track — and 0 at the other twenty **[Inferred]** |
| +7 | Tires | `TIRE STORE` 10, `JUNKYARD` 30 — neither pays in any other column |
| +8 | Fuel | `FUEL STORAGE TANK` 100, and nothing else |
| +9 | Medical | `MEDICAL CENTER` 2, `HOSPITAL` 3, `VETERINARIAN` 1, `DRUG STORE` 2 — all four pay only here |

`CACHE` corroborates all five supply columns at once: `10, 10, 0, 10, 10, 2`, a little of
everything, which is exactly what a stash is. **[Measured]**

`SHELTER` is the one site that pays in two columns without being a cache — 50 food and 1 medical
(`50, 0, 0, 0, 0, 1`). It is called out because it is the obvious counter-example to "each site
pays in one column", and because an earlier draft of this section wrongly cited it as a
food-only site.

There is **no ammunition column** — ammo arrives with guns.

This was got wrong on the first pass. The columns after guns were labelled tires/fuel/medical/ammo
by elimination and marked Inferred, which put them one position off and made the trainer's own
reference tab claim a tire store pays fuel and a hospital pays ammunition. The lesson is the
obvious one: "by elimination" is not a derivation. The verifier now asserts, for each of the five
supply columns, that the site which pins it pays there and nowhere else.

---

## 8. The clock

`DAY 261` in the shipped save is the u16 at slab `0x001C`, and the clock is `6 + ` the u16 at
`0x001E`: the save reads 8 and the game shows `2:00 PM`. One overland move advanced it to 10 and
the game showed `4:00 PM`; a night passed and it reset to 0 at `6:00 AM` with the day incremented.
So the day runs from 6 AM, a move costs about two hours, and the year is printed as a constant
1999. **[Confirmed]**

---

## 9. Finding the game in memory

The anchor is the vehicle-type name block —
`"MOTORCYCLE\0SIDECAR\0COMPACT CONVERTIBLE\0"` — at `DS:0x2254`. It is initialised data, so it is
present from the moment `START.EXE` loads, whether or not a game has been started.

**A hit on the string is not enough, and this was learned the hard way.** While an overlay is
being paged in, a second copy of those bytes is briefly present in the emulator's RAM. A write
aimed at that copy lands nowhere the game will ever read — during development a poke went to
a transient copy and silently did nothing, and the same poke a second later worked. So the locator
validates every candidate against three things that only a real data segment has in the right
places relative to one another:

1. the anchor string at `DS:0x2254`;
2. the 19-entry pointer table at `DS:0x2366`, whose entries are **absolute data-segment offsets**
   — the first must be exactly `0x2254`, and they must ascend and stay inside the slab. This is the
   check that separates the data segment from a scratch copy, because the pointers carry the base
   offset with them;
3. a vehicle-type table at `DS:0x238C` whose first record is the motorcycle (mass 1, structure 3,
   100 MPH, manoeuvrability 4).

Measured live: **1 validated candidate from 1 anchor hit in 702 ms** across a 168 MB DOSBox-X
working set. It has been confirmed on two different emulator builds — a plain DOSBox 0.74 running
the game directly, and a DOSBox-X hosting Windows 3.11 with the game running inside it.

Every write re-runs the validation immediately before committing, because DOSBox can be closed, or
a different program started inside it, between one edit and the next.

---

## 10. What was probed and not established

Recorded so nobody spends the time again.

**Controlling cities.** This is the win condition — the manual says a gang leader who controls
enough cities is contacted by a G.U.B. agent — and the `E` command prints a `CONTROLLED CITIES`
list. Locating what feeds that list was attempted and **failed**. What was ruled out, each by
poking and then re-reading the E screen with the gang holding no cities:

* city record `+9` (the resident faction) set to 10–15 and 18–23 — the list stayed empty, so the
  player is not simply another faction code;
* city record `+11`, the byte that is zero in every shipped record and every save — set to 1, 2
  and 255, no effect;
* the `FF FF 00 00` array at slab `0x0026` — the first entry set to Chicago's index (52) and to
  other values, no effect.

The `CONTROLLED CITIES` string lives in `A.R2K` at file offset 17,126, and the routine that uses it
could not be reached by searching the overlay for an immediate reference to that address, either
as a raw file offset or as the data-segment offset the overlay's literal pool maps to. Finding it
needs a proper disassembly of the overlay's relocation and literal-pool scheme, which was out of
scope here. The trainer therefore does **not** offer "give me this city" — it offers "clear this
town of residents", which is a different and genuinely useful thing (it stops residential
encounters), and says so in the UI rather than implying otherwise.

**Resident codes above 9.** The shipped save contains `+9` values of 10, 12, 14 and 17 for
Columbus, Cleveland, Flint and Jacksonville, but the faction name table has only ten entries.
They may index a second table (the eleven named road gangs would fit 10–20), but that was not
confirmed, so the trainer reports them by number rather than guessing.

**Vehicle record bytes `+0x13`/`+0x14`.** Read `02 02` on every crewed vehicle inspected except
the sports car hardtop in slot 0, which read `00 02`. Not correlated with anything on the Vehicle
Stats screen. Copied verbatim when the trainer creates a vehicle rather than being invented.

**Slab `0x0024`, `0x0010`, `0x0014`–`0x0018`.** Small constants in the world header (7, 5, 16, 1,
16) that never changed across any of the six live dumps taken. Not identified.

**The tactical-combat maps.** `MAP0`–`MAP22.R2K` are the road-combat terrain, and `MAP20.R2K`
is the odd one out at 2,016 bytes with no header while the rest carry the same 8-byte header as
the overland maps. Their layout was not pursued — the trainer does not touch tactical combat.

---

## 11. Method notes

Tooling used, for anyone repeating this:

* **Static:** the shipped files read directly. Aligning `CHICAGO.RWS` against `START.EXE` by
  brute-force best-match was the single most productive step — it located the data segment,
  identified the save format, and revealed which fields are mutable, all at once.
* **Live:** the game run under DOSBox 0.74 with a purpose-built config, driven by synthesised
  keystrokes, with screenshots read back to confirm state. Guest RAM was located by scanning the
  emulator process for the anchor string, then dumped 64 KB at a time and diffed between actions.
  Perturb-and-diff — move one square, take one action, poke one byte — did essentially all the
  work; almost nothing here was guessed from structure alone.
* **Not used:** Ghidra was set up but proved unnecessary for the data structures, which fell out of
  the file/RAM correlation. It would be the right tool for the one open question in §10, where the
  data is in overlay code rather than in the data segment.
