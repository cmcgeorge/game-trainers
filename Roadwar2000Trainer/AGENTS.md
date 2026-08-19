# Roadwar 2000 Trainer — working notes

Read this and `README.md` before changing anything here. `docs/reverse-engineering.md` is the
authority for every offset; `docs/strategy-guide.md` is the authority for game behaviour.

## The one thing to understand first

Roadwar 2000 keeps its whole mutable world in one contiguous 6,512-byte slab of the Lattice C data
segment (`DS:0x21BA`–`DS:0x3B29`), and **its `.RWS` save file is a verbatim image of exactly that
slab** — no header, no checksum, no compression. That is why `SaveFormat` is a single offset table
serving both the live editor and the save editor, and why `GangRecord`/`VehicleRecord`/`CityRecord`
do not know or care which they are running over. Keep it that way: a new field belongs in
`SaveFormat` as a slab offset, and everything else follows.

The equivalence was measured, not assumed — recalling the shipped save and dumping guest RAM gave
a 6,509/6,512 match, and a save written after nine trainer pokes carried all nine. The three bytes
that always differ (`0x0008`, `0x0697`, `0x1262`) are rewritten by the save routine itself and are
listed in `SaveFormat.VolatileOffsets`.

## Do not weaken the locator

`GameLocator` requires **three** things of a candidate, not one:

1. the anchor string at `DS:0x2254`;
2. the 19-word pointer table at `DS:0x2366` holding absolute data-segment offsets starting `0x2254`
   and ascending;
3. a motorcycle (mass 1, structure 3, 100 MPH, manoeuvrability 4) as vehicle template 0.

The string alone is **not** sufficient, and this is not defensive programming for its own sake:
while an overlay is paged in, a second copy of the anchor is briefly present in the emulator's
RAM, and during development a poke aimed at it silently did nothing. Check 2 is the load-bearing
one, because the pointers carry the base offset with them.

Every write goes through `LiveSlabTarget.Write`, which re-runs the validation before committing.
Do not add a write path that bypasses it.

## Conventions that bite

* **The crew array is at an odd offset** (`0x1595`). Lattice C packs structs; these five 16-bit
  counts are not word-aligned. Reading them as aligned words is the easiest way to break this.
* **Maximum comes before current** for vehicle structure, manoeuvrability and tires — the reverse
  of the usual convention.
* **Interior capacity is stored one lower than displayed** (the driver). `TopsideCapacity` is not.
* **Fuel has two readings.** The stored word is the total; the Gang Status screen prints
  `stored − 2 × fuel consumption`. `GangRecord.DisplayedFuel` is the second one — do not edit it.
* **Carrying capacity is not stored.** It is `5 × mass²`, exact for all nineteen types.
* **Overland X is 1-based, Y is 0-based**, index `Y × 48 + (X − 1)`, and the engine ORs `0x80`
  into the party's square. Mask it before reading terrain.
* **`HOUSTON` has X = 0.** It is the only such record; the index wraps onto row 31, column 47 and
  the map data was authored to match, but the game prints a blank location line there.
  `OverlandMap.IsInside` deliberately excludes column 0 while the indexer still resolves it.
* **Terrain codes above 22 are scenery**, not terrain. The engine's name table has 23 entries and
  it does not bounds-check — standing on a code-31 square makes the status line read garbage.
  `TerrainBook.IsPassable` is what keeps teleport off them.

## Two data-transcription traps

Both of these were made once and are now pinned by the verifier. Read this before regenerating any
of the baked-in tables from the game files.

* **City sizes come from `START.EXE`, not from `CHICAGO.RWS`.** The shipped save is a game in
  progress: 30 of its towns have been looted below their starting level, and two of them
  (Ottawa, Greenville/Spartanburg) are at zero. Taking the size column from the save makes
  "restock to the shipped level" restore a *looted* level, and makes those two towns permanently
  unrestockable because the code skips a town already at or above its recorded original.
  `CheckShippedCityTable` pins the whole column against the EXE.
* **The loot payout columns are +4 food, +5 guns, +6 vehicle flag, +7 tires, +8 fuel, +9 medical.**
  There is no ammunition column. Labelling them by elimination puts them one position off and makes
  the Reference tab claim a tire store pays fuel. Each column is now asserted against a site that
  pays in it and in nothing else.

## Table order is load-bearing

`VehicleBook.All`, `CityBook.All`, `LootBook.All`, `RankBook.Names`, `TerrainBook.Names` and
`ResidentBook.Names` are all indexed by the byte stored in the game. Never reorder them, and never
"tidy" a name — the names came out of `START.EXE` verbatim and the reference tab is checked against
them.

## What is deliberately absent

* **No "give me this city".** The `E)mpire Status` mechanism was probed and not found; §10 of the
  RE doc lists what was ruled out. If you find it, that section is where the evidence goes.
* **No tactical-combat editing.** `MAP0`–`MAP22.R2K` were not decoded.
* **No `MemorySearcher`.** This target has a clean static anchor, so there is nothing to scan for.

## Testing

`.\Run.ps1 -Test -NoRun` runs 703 headless checks with no game installed, 762 when a Roadwar folder
is found, and `dotnet run --project test\FormatCheck -- --live` adds seven more against a running
DOSBox — skipped rather than failed when there is none, or when the harness is not elevated enough
to open it.

The folder is found via **`ROADWAR2000_DIR`** or the conventional locations; there are deliberately
no machine-specific paths in either the trainer or the harness. Pass the folder as the harness's
first argument to override both.

When the game folder is present the harness checks the shipped `CHICAGO.RWS` against the exact
figures the game's own screens print for that save (food 640, fuel 2035 stored / 1937 displayed,
crew 22/51/100/100/99, nine vehicles, day 261 at 2:00 PM, standing in Chicago) and verifies all
120 cities land on a city tile. Those numbers are the regression net for the whole offset table —
if you change an offset and they still pass, you have not broken anything.

Set `RW2K_SMOKETEST` to a file path and run a Debug build to load the window, walk every tab and
exit; it catches XAML and binding faults that only appear when a tab is first shown.
