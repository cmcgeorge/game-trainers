# Sid Meier's Colonization — Reverse-Engineering Notes

Teardown of **Sid Meier's Colonization** (MicroProse, 1994 — the original DOS "Col1", main
executable `VICEROY.EXE`) as it applies to the trainer in this folder. The trainer's headline
feature is an **offline save-game editor** for the `COLONYxx.SAV` files, so most of this document
maps that file format; a shorter section covers the live-memory picture and the Ghidra pass on
`VICEROY.EXE`.

Everything here was cross-checked three ways:

1. **Empirically**, byte-for-byte, against the two save files shipped in `.games/`
   (`COLONY00.SAV` = a manual save, `COLONY09.SAV` = its autosave — a fresh 1492 game, English
   player named *Christopher*). Both are exactly **24,537 bytes**.
2. Against the community's reverse-engineering of `VICEROY.EXE`: the canonical
   [`eb4x`/`hegemogy` **viceroy**](https://github.com/eb4x/viceroy) C struct (`savegame.h`),
   [`pavelbel/smcol_saves_utility`](https://github.com/pavelbel/smcol_saves_utility) (its
   `smcol_sav_struct.json` was itself extracted from the viceroy project), and
   [`nawagers/Colonization-SAV-files`](https://github.com/nawagers/Colonization-SAV-files/blob/master/Format.md)
   (`Format.md`).
3. Against the game's own data text files in `.games/` (`PEDIA.TXT`, `LABELS.TXT`, `MENU.TXT`,
   `COLONY.TXT`, `TRIBE.TXT`), which enumerate the goods, units, terrain, professions, buildings,
   and Founding Fathers in the exact index order the binary uses.

Where a value is `Confirmed` it was verified against the shipped saves; `Documented` means agreed by
the three community sources but not independently byte-verified here; `Tentative` means partially
understood.

> **Copyright.** The game, its data files, and the save files live in git-ignored `.games/`. Nothing
> copyrighted is committed. The trainer never embeds game assets; the save editor only reads/writes a
> user's own `COLONYxx.SAV`.

---

## 1. The `COLONYxx.SAV` format at a glance — `Confirmed`

Little-endian throughout (it is an x86 DOS game). The file is a flat serialization of the in-memory
game state with **no checksum and no encryption** — you can overwrite any field in place and the game
loads it. The header carries the record *counts*, and everything after the header is a run of
fixed-size records whose absolute offset must be **computed from those counts** (the file is not a
fixed layout — 24,537 bytes is just the size of *this* new game with 0 colonies).

| Section | Start | Size | Notes |
|---|---|---|---|
| **HEAD** | `0x000` | 158 (`0x9E`) | signature, map size, year/turn, difficulty, counts, REF |
| **PLAYER × 4** | `0x09E` | 52 each = 208 | leader name + country name + control flags |
| **OTHER** | `0x16E` | 24 | unidentified |
| **COLONY × `colony_count`** | **`0x186`** | 202 (`0xCA`) each | one per colony |
| **UNIT × `unit_count`** | `0x186 + 0xCA·C` | 28 (`0x1C`) each | map units (in-colony colonists are *not* here) |
| **NATION × 4** | `0x186 + 0xCA·C + 0x1C·U` | 316 (`0x13C`) each | England, France, Spain, Netherlands — **holds gold** |
| INDIAN villages × `tribe_count` | after NATION | 18 (`0x12`) each | |
| TRIBE × 8 | | 78 (`0x4E`) each = 624 | the eight native nations |
| Reports / "stuff" | | 727 (`0x2D7`) | viewport, counters |
| Terrain / Mask / Path / Visibility maps | | `(x+2)(y+2)` = 4176 each | four `58×72` byte layers |
| Sea-route / Land-route maps | | 270 (`0x10E`) each | |
| Unknown-F | | 74 (`0x4A`) | prime-resource seed |
| Trade routes × 12 | | 74 each = 888 (`0x378`) | |

`C` = `colony_count` (u16 at `0x2E`), `U` = `unit_count` (u16 at `0x2C`).

### Why 24,537 bytes here
The shipped saves are a brand-new game: `colony_count = 0`, `unit_count = 77`, `tribe_count = 65`
Indian dwellings, on the standard `58×72` map. The count-independent tail (four `4176`-byte map
layers, the four nation records, tribes, reports, route maps, trade routes) dominates the size; the
`0` colonies and `77` units fill the rest. **A save with founded colonies is larger than 24,537
bytes** — never hard-code the total size, and never hard-code any offset past the header.

---

## 2. HEAD — the 158-byte header — `Confirmed`

Offsets are absolute from the start of the file. Everything below was read straight out of
`COLONY00.SAV` and matches the viceroy `savegame.h` `struct head` field-for-field.

| Offset | Size | Field | Value in `COLONY00.SAV` | Notes |
|---|---|---|---|---|
| `0x00` | 9 | signature `"COLONIZE\0"` | `COLONIZE` | the load check; not a checksum |
| `0x0C` | 2 | `map_size_x` | 58 | includes the 1-tile border (visible 56) |
| `0x0E` | 2 | `map_size_y` | 72 | includes the 1-tile border (visible 70) |
| `0x10` | 1 | tutorial flags (`tut1`) | | bit-packed |
| `0x12` | 2 | game options | | `cheat` bit here = the Alt-W-I-N cheat menu |
| `0x14` | 2 | colony-report options | | the F-key report toggles |
| `0x1A` | 2 | **`year`** | **1492** | `0x05D4` |
| `0x1C` | 2 | `season` | 0 | non-zero = autumn |
| `0x1E` | 2 | **`turn`** | **0** | |
| `0x22` | 2 | `active_unit` | | index of the unit awaiting orders |
| `0x28` | 2 | **`human_player`** | **0** | 0 = England (matches "New England") |
| `0x2A` | 2 | **`tribe_count`** | **65** | number of Indian dwellings — matches `TRIBE.TXT` |
| `0x2C` | 2 | **`unit_count`** | **77** | map units of all powers + braves |
| `0x2E` | 2 | **`colony_count`** | **0** | none founded yet |
| `0x30` | 2 | `trade_route_count` | 0 | max 12 |
| `0x36` | 1 | **`difficulty`** | **4** | 0 Discoverer … 4 Viceroy |
| `0x39` | 25 | `founding_father[25]` | all 0 | global per-father state (see §5) |
| `0x6A` | 8 | `expeditionary_force[4]` | | the King's REF: regulars, dragoons, man-o-war, artillery |
| `0x72` | 8 | backup force | | grows as you produce bells |
| `0x9A` | 2 | `event` bitfield | | which "woodcut" intro events have fired |

`map_size_x/y`, `year`, `human_player`, the three counts, and `difficulty` were confirmed by direct
read. The `manual_save_flag` at **`0x54`** (`1` in the manual save `COLONY00`, `0` in the autosave
`COLONY09`) is the single most useful cross-check: it is one of only five bytes that differ between
the two files, exactly where the format predicts.

---

## 3. PLAYER × 4 — leader/country names — `Confirmed`

Immediately after the header, four 52-byte records (`struct player`): `char name[24]`,
`char country[24]`, then `unk00`, `control` (0 = human, 1 = AI, 2 = withdrawn), `founded_colonies`,
`diplomacy`. In `COLONY00.SAV` these decode exactly as the string scan found them:

| # | Offset | `name` | `country` |
|---|---|---|---|
| 0 | `0x09E` | Christopher | New England |
| 1 | `0x0D2` | Jacques Cartier | New France |
| 2 | `0x106` | Christopher Columbus | New Spain |
| 3 | `0x13A` | Michiel De Ruyter | New Netherlands |

`0x9E + 4·52 = 0x16E`, then a 24-byte `OTHER` block, so **colonies begin at exactly `0x186`** —
which is the anchor every community source quotes. This chain (header 158 + players 208 + other 24 =
390 = `0x186`) is the backbone of the whole format and it lines up to the byte.

---

## 4. NATION × 4 — where the gold lives — `Confirmed`

Four 316-byte (`0x13C`) records, **in the fixed order England, France, Spain, Netherlands**
(indices 0–3, matching `human_player`). Offsets are within the record; add
`nation_base = 0x186 + 0xCA·colony_count + 0x1C·unit_count + 0x13C·index`.

| Rec. offset | Size | Field | England value | Notes |
|---|---|---|---|---|
| `0x01` | 1 | **`tax_rate`** | 0 | percent, 0–99; the King raises it over time |
| `0x02` | 3 | `recruit[3]` | 1A 1A 08 | immigrant types waiting on the Europe docks |
| `0x06` | 1 | `recruit_count` | 0 | recruit-price penalty counter |
| `0x07` | 4 | **`founding_fathers`** | 0 | 32-bit acquired bitfield — 25 named bits (see §5) |
| `0x0C` | 2 | `liberty_bells_total` | 0 | accumulated bells |
| `0x0E` | 2 | `liberty_bells_last_turn` | 0 | |
| `0x12` | 2 | `next_founding_father` | `0xFFFF` | −1 = none currently being elected |
| `0x14` | 2 | `founding_father_count` | 0 | how many joined |
| `0x18` | 1 | `villages_burned` | 0 | scoring input |
| `0x1E` | 2 | `artillery_count` | 0 | Europe artillery price penalty counter |
| `0x20` | 2 | **`boycott_bitmap`** | 0 | one bit per good (Tea-Party boycotts) |
| `0x22` | 4 | `royal_money` | 42 | the King's REF budget (not your treasury) |
| **`0x2A`** | **4** | **`gold` (treasury)** | **0** | ← the field the trainer edits |
| `0x2E` | 2 | `crosses` | | religious-unrest / immigration progress |
| `0x38` | 8 | `indian_relation[8]` | | `0x00` not met, `0x20` war, `0x60` peace |
| `0x4C` | 240 | `trade` block | | per-good Europe price, volume, cumulative revenue |

### Gold — triple-confirmed
`gold` is a 4-byte little-endian integer at record offset **`0x2A`**:

- **viceroy `savegame.h`**: summing the `struct nation` fields lands `uint32_t gold;` at byte 42
  (`0x2A`), and the struct totals exactly 316 (`0x13C`).
- **nawagers `Format.md`**: *"Gold is 4 bytes at 0x2A (+ 0x13C · power offset)."*
- **pavelbel `smcol_sav_struct.json`**: `"gold": {"size": 4}` at the same computed byte.
- **Empirically**: in `COLONY00.SAV`, `colony_count = 0` and `unit_count = 77`, so
  `nation_base(England) = 0x186 + 0x1C·77 = 0x9F2`, and `gold` sits at `0x9F2 + 0x2A = 0xA1C`,
  which reads `00 00 00 00` — a fresh English colony correctly starts with **0 gold**. The
  neighbouring fields also read sensibly (tax 0, no Founding Fathers, `next_founding_father = 0xFFFF`,
  `royal_money = 42`), which validates the whole record alignment.

**Practical cap.** nawagers notes that filling all four gold bytes pushes the on-screen tax-rate
display off screen. The trainer therefore treats gold as an effectively **~3-byte** field and its
"Max Gold" button writes **999,999,999** (well within a signed 32-bit int, comfortably rich, no
display glitch). A **Treasure unit's** carried gold is *not* here — it lives in that unit's
`profession` byte as gold ÷ 100 (`0x32` = 5,000 gold).

---

## 5. Founding Fathers — the acquired bitfield — `Documented`

Each nation's `founding_fathers` field (record offset `0x07`, 32 bits) has one bit per Father, in the
index order the pedia uses (`PEDIA.TXT` `@FATHER0`…`@FATHER24`) and the viceroy `founding_father_list`:

```
 0 Adam Smith           5 Ferdinand Magellan   10 Hernan Cortes        15 Thomas Jefferson   20 William Brewster
 1 Jakob Fugger         6 Francisco Coronado    11 George Washington    16 Pocahontas         21 William Penn
 2 Peter Minuit         7 Hernando de Soto      12 Paul Revere          17 Thomas Paine        22 Jean de Brebeuf
 3 Peter Stuyvesant     8 Henry Hudson          13 Francis Drake        18 (unused slot 18)    23 Juan de Sepulveda
 4 Jan de Witt          9 Sieur de La Salle      14 John Paul Jones      19 Benjamin Franklin   24 Bartolomé de las Casas
```

(Bit 18 is a dead slot in the viceroy list — "founding18" — so 24 of the 25 bits are real Fathers.
The five categories are Trade 0–4, Exploration 5–9, Military 10–14, Political 15–19, Religious 20–24.)

The trainer exposes these as checkboxes and a **Grant All** button that sets the human nation's
bitfield. Setting a bit is `Documented`, not `Confirmed`: the game recomputes some derived state
(`founding_father_count`, the 25-byte global `founding_father[25]` array in the header) on load, so
the trainer also bumps `founding_father_count` to keep the count consistent, and treats Father edits
as a convenience rather than a guaranteed-clean edit. Gold and tax are the fully-verified edits.

---

## 6. COLONY × N — 202-byte colony record — `Documented`

Only relevant once you've founded a colony (the shipped saves have none, so these offsets are
`Documented`, not byte-verified here). Offsets are within the record.

| Rec. offset | Size | Field | Notes |
|---|---|---|---|
| `0x00` | 2 | `x`, `y` | map position |
| `0x02` | 24 | `name` | ASCIIZ (see `COLONY.TXT` for defaults) |
| `0x1A` | 1 | `nation` | owner |
| `0x1F` | 1 | `population` | number of colonists in the colony |
| `0x20` | 32 | `occupation[32]` | per-colonist current job |
| `0x40` | 32 | `profession[32]` | per-colonist learned profession |
| `0x70` | 8 | `tiles[8]` | which colonist works each surrounding tile |
| `0x84` | 6 | `buildings` | packed levels: stockade(3b), armory(3b), docks(3b), town_hall(3b), schoolhouse(3b), warehouse(2b), stables(1b), custom_house(1b), printing_press(2b), weaver/tobacconist/distiller houses (3b each), fur/carpenter/church/blacksmith … |
| `0x8A` | 2 | `custom_house` | one export bit per good |
| `0x92` | 2 | **`hammers`** | accumulated construction progress |
| `0x94` | 1 | `building_in_production` | |
| `0x95` | 1 | `warehouse_level` | (community-corrected: this byte is the warehouse level) |
| `0x9A` | 32 | **`stock[16]`** | the 16 goods, each a signed 16-bit quantity |
| `0xC2` | 4 | `rebel_dividend` | Sons-of-Liberty numerator |
| `0xC6` | 4 | `rebel_divisor` | Sons-of-Liberty denominator (SoL % ≈ dividend/divisor) |

The trainer's colony editor writes `name`, `population`, `hammers`, and the 16 `stock` quantities,
and offers a "fill warehouse" that sets every good to a chosen amount. Buildings are shown but edited
conservatively (the packed bitfield is easy to corrupt).

### The 16 goods — index order — `Confirmed` (from `PEDIA.TXT` `@CARGO0…15`)
`0 Food · 1 Sugar · 2 Tobacco · 3 Cotton · 4 Furs · 5 Lumber · 6 Ore · 7 Silver · 8 Horses ·
9 Rum · 10 Cigars · 11 Cloth · 12 Coats · 13 Trade Goods · 14 Tools · 15 Muskets`

This one order recurs everywhere: colony `stock`, `custom_house`, `boycott_bitmap`, and the nation
`trade` arrays. Production chains: Sugar→Rum, Tobacco→Cigars, Cotton→Cloth, Furs→Coats,
Ore→Tools→Muskets.

---

## 7. UNIT × N — 28-byte unit record — `Documented`

Map units (ships, soldiers, wagons, treasure, braves). In-colony colonists are stored in the colony's
`occupation`/`profession` arrays, not here. Key fields: `x,y` (`0x00`), `type` (`0x02`, index into the
24-entry unit list), `owner` nibble (`0x03`), `moves` (`0x05`), `order` (`0x08`; 8 = plow, 9 = road),
cargo hold (`0x0E`+), and `profession` (`0x1A`) which doubles as a Treasure unit's gold ÷ 100. The
trainer does not edit units in the save (their indices interlock with the colony/transport chains);
live unit tweaks are left to the value scanner.

---

## 8. Reference enumerations (from the game's own text) — `Confirmed`

These indices are the ones the binary and the save use; they drive the trainer's References tab.
Extracted verbatim from `.games/PEDIA.TXT` and `savegame.h`.

- **Nations** (`0..3` playable, then natives): England, France, Spain, Netherlands, Inca, Aztec,
  Arawak, Iroquois, Cherokee, Apache, Sioux, Tupi.
- **Difficulty** `0..4`: Discoverer, Explorer, Conquistador, Governor, Viceroy.
- **24 unit types** (`@UNIT0…`): Colonist, Soldier, Pioneer, Missionary, Dragoon, Scout, (Tory)
  Regular, Continental Cavalry, (Tory) Cavalry, Continental Army, Treasure Train, Artillery, Wagon
  Train, Caravel, Merchantman, Galleon, Privateer, Frigate, Man-O-War, Brave, Armed Brave, Mounted
  Brave, Mounted Warrior, (Indian Convert).
- **28 professions / experts** (`@JOB0…`): Expert Farmer, Master Sugar/Tobacco/Cotton Planter, Master
  Fur Trapper, Expert Lumberjack/Ore Miner/Silver Miner/Fisherman, Expert Distiller, Master
  Tobacconist/Weaver/Fur Trader, Expert Carpenter/Blacksmith, Master Gunsmith, Firebrand Preacher,
  Elder Statesman, *(Student)*, Free Colonist, Hardy Pioneer, Veteran Soldier, Seasoned Scout, Veteran
  Dragoon, Jesuit Missionary, Indentured Servant, Petty Criminal, Indian Convert.
- **Terrain**: the pedia enumerates **29 terrain slots** (`@TERRAIN0…28`), many of them forest
  overlays on a base terrain; the trainer's References tab condenses these to **21 distinct types**
  with their base yields (Tundra, Desert, Plains, Prairie, Grassland, Savannah, Marsh, Swamp, Hills,
  Mountains, Ocean, Sea Lane, Arctic, and the Boreal/Scrub/Mixed/Broadleaf/Conifer/Tropical/Wetland/
  Rain forests). Terrain is a map-tile attribute, not a save field the trainer edits.
- **42 building slots** (`@BUILDING0…`): Stockade/Fort/Fortress, Armory/Magazine/Arsenal,
  Docks/Drydock/Shipyard, Town Hall, Schoolhouse/College/University, Warehouse (+expansion), Stables,
  Custom House, Printing Press/Newspaper, the four processing chains
  (Weaver's/Tobacconist's/Distiller's/Fur Trader's House→Shop→Factory), Carpenter's Shop/Lumber Mill,
  Church/Cathedral, Blacksmith's House/Shop/Iron Works.
- **25 Founding Fathers** — see §5.

---

## 9. Live memory (`VICEROY.EXE` under DOSBox) — `Tentative`

The save is a *serialization* of the in-memory state, so the same structures exist in guest RAM while
you play — but at an address that changes every DOSBox session (the game's data segment is loaded
wherever DOS places it), and `VICEROY.EXE` is a large, overlaid real-mode program, so there is no
stable adjacent byte-run to anchor a locator to. Like the repo's other real-mode targets
(Darklands, BattleTech, Perfect General 2), the trainer therefore drives a **Cheat-Engine-style value
scanner** for the live side:

- **Gold** — read your treasury on the map's top bar, First-Scan that number, spend/earn some, scan
  the new value, repeat until one address remains, then pin and freeze it. (Gold is a 32-bit value
  in memory; if a 32-bit scan finds nothing, try 16-bit — early-game treasuries fit a word.)
- **Tax rate**, **liberty bells**, and a colony's goods can be pinned the same way.

The scanner is the same shared `GameTrainers.Common.Memory.MemorySearcher` the other value-scanner
trainers use; the trainer adds only Colonization-specific guided-scan hints. Because the save editor
is fully verified and the live scan is inherently manual, the **save editor is the recommended path**
for reliable edits.

### Ghidra pass on `VICEROY.EXE`
`VICEROY.EXE` is a plain (unpacked) MS-DOS MZ executable: 493,060 bytes, **2,252 relocations**, no
PKLITE/LZEXE/UPX signature. Ghidra 12.1.2 (`C:\Ghidra\ghidra_12.1.2_PUBLIC`) loads it with the
*Old-style DOS Executable (MZ)* loader at `x86:LE:16:Real Mode` and auto-analysis succeeds. The load
image is ~132 KB with ~360 KB of appended **overlay** code, which real-mode Ghidra maps but does not
fully resolve across overlay boundaries — so Ghidra was used to confirm the loader/segment shape and
the embedded string tables, while the *authoritative* structure came from the save-file analysis
above (which is directly testable against real files) rather than from decompiling the overlaid
save/load routines. This mirrors how the Darklands trainer handled its packed target.

---

## 10. What the trainer does with all this

- **Save editor (verified path).** Open a `COLONYxx.SAV`, validate the `COLONIZE` signature, parse
  the header counts, and locate the human nation record by the offset formula in §4. Edit **gold**
  and **tax** (fully confirmed), plus liberty bells, boycotts, Founding Fathers, the REF size, and —
  when colonies exist — per-colony name/population/hammers/goods. Saving writes the changed bytes
  back **in place** (no checksum to fix), after making a one-time `.bak`; an untouched save
  round-trips byte-for-byte.
- **Value scanner (live path).** Attach to DOSBox and pin gold / tax / bells with guided scans.
- **References.** The enumerations in §8 plus a condensed strategy digest.

## Sources

- viceroy (`eb4x`/`hegemogy`) `savegame.h` — <https://github.com/eb4x/viceroy>
- `pavelbel/smcol_saves_utility` (`smcol_sav_struct.json`) — <https://github.com/pavelbel/smcol_saves_utility>
- `nawagers/Colonization-SAV-files` `Format.md` — <https://github.com/nawagers/Colonization-SAV-files/blob/master/Format.md>
- CivFanatics decoding threads — <https://forums.civfanatics.com/threads/decoding-colonization-sav-files-1994-old-classic.674707/>
- The game's own `.games/PEDIA.TXT`, `LABELS.TXT`, `MENU.TXT`, `COLONY.TXT`, `TRIBE.TXT`
- The shipped `.games/COLONY00.SAV` / `COLONY09.SAV` (empirical ground truth)
