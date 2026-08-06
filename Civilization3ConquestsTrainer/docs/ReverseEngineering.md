# Civilization III: Conquests — reverse-engineering notes

How this trainer finds the game's state, what was proved and what was not, and what it would take to
open up the parts that are still closed.

---

## 1. The target

| | |
| --- | --- |
| Executable | `Civ3Conquests.exe`, Steam "Sid Meier's Civilization III Complete" |
| Ruleset | Conquests **v1.22** (per the shipped `readme.txt`) |
| Format | PE32, machine `0x014C` (i386), 4 sections, **ImageBase `0x00400000`** |
| ASLR | **Not set** — `DllCharacteristics` is `0x0000`, so the image is never relocated |
| Size / timestamp | **3,518,464 bytes**, PE `TimeDateStamp` **`0x550A3E1F`** (2015-03-19) |
| Mapped size | `0x8F7000` (sections `.text 0x401000`, `.rdata 0x682000`, `.data 0x69D000`, `.rsrc`) |
| Packing / DRM | None. Four normally-named sections, `.text` raw size ≈ virtual size, a plain `SteamAPI_Init` import. Not UPX/SteamStub/Themida. |
| RTTI | Effectively absent — only `type_info` and `exception` descriptors survive, so no class names can be recovered from vtables. |

That combination — native, unpacked, fixed base, no ASLR — is what makes a **one-click locator**
possible instead of a value scanner. Every static object sits at a constant offset from the module
base, and the only thing that moves between sessions is the heap.

The 2022 file date is Steam's download stamp; the PE timestamp is the real build date.

---

## 2. Where the layout came from, and what was re-proved here

The struct layout and the static-object addresses originate with the Civ3 modding community, not with
this trainer:

- **[C3X](https://github.com/maxpetul/C3X)** (Flintlock / maxpetul) — an executable mod for Conquests
  that ships `civ_prog_objects.csv`, a table of ~1,000 program objects with **a separate address
  column per shipped build** (GOG, Steam, PCGames.de), and `Civ3Conquests.h`, ~6,500 lines of C struct
  definitions.
- **[Antal1987](https://github.com/Antal1987/C3CPatchFramework)** — the origin of that header; C3X's
  own file credits it.

C3X identifies builds by file size, and its Steam entry is
`{"Steam", 3518464, .rdata 0x682000, size 0x1B000}` — **a byte-exact match for the installed
executable**, which is why the Steam address column applies here directly rather than by analogy.

Nothing was taken on trust. Every constant this trainer ships was re-read out of the running game and
cross-checked against an *independent* source — mostly `conquests.ini`, which the game writes and
which therefore cannot be circular with a memory read:

| Read from memory | Value | Independent check |
| --- | --- | --- |
| `p_preferences` @ `0xA74E70` | `0x1E3E300D` | ini `Preferences=00011110001111100011000000001101` → `0x1E3E300D` |
| `p_toggleable_rules` @ `0xA74E74` | `0x00006009` | ini `Rules=…0110000000001001` |
| `p_game_difficulty` @ `0xA74E7C` | `7` | ini `Difficulty=7` |
| `Map.Seed` (via BIC) | `283554621` | ini `WorldSeed=283554621` |
| `leaders[1..12].RaceID` | `2, 8, 1, 5, 4, 7, 6, 3, 9, 10, 11, 12` | ini `ActualCiv0..11`, in order |
| `Map.Width` × `Height` | 130 × 130 | ini world size |
| `p_player_bits` | `0x1FFF` | 13 slots = barbarians + the 12 civs the ini lists |

Five unrelated values landing exactly where the table predicts, each matching a file the game wrote
itself, is a much stronger argument than any single read.

### Ghidra

`Civ3Conquests.exe` was imported into Ghidra 12.1.2 headless with a small post-script
(`.docs/ghidra-scripts/ApplyC3XSymbols.java`) that reads the C3X CSV, takes the Steam column, and
labels each `define` row. Analysis succeeded and **339 data labels** were applied. Two practical notes
for anyone repeating this:

- `analyzeHeadless.bat` chokes on the apostrophe in `…\Sid Meier's Civilization III Complete\…`, and
  Ghidra refuses a project directory containing a dot-prefixed path element (so not `.docs/ghidra`).
  Copy the exe somewhere plain and put the project elsewhere.
- Importing `Civ3ConquestsEdit.exe` is worth doing for the BIQ/SAV serialisation code, but not for
  class names: its 97 RTTI descriptors are all MFC.

---

## 3. The finding that shapes everything: the treasury is obfuscated

**Civ3 never stores your gold as a number.** A `Leader` carries two fields:

```
Leader + 0x44   Gold_Decrement     (the key — a different random value per civ, per game)
Leader + 0x48   Gold_Encoded
treasury      = Gold_Decrement + Gold_Encoded
```

Read live at the start of a game, every civ held a *different* pair that summed to the same 10:

```
civ  1   key -12345   encoded 12355   → 10
civ  2   key -12342   encoded 12352   → 10
civ  5   key -12337   encoded 12347   → 10
```

Consequences, all of them load-bearing for how this trainer is built:

1. **An exact-value scan for your treasury cannot work.** The number on the top bar is not in memory.
   This is almost certainly why Civ3 has a reputation for being awkward to cheat with generic tools,
   and it is the single strongest argument for locating the player structurally instead.
2. **Writes must go to `Gold_Encoded` only.** `Gold_Decrement` is the game's key; rewriting it would
   desynchronise every other read of the same treasury. The trainer never touches it.
3. **A freeze must re-encode, not replay.** Holding a treasury means recomputing
   `encoded = desired − key` against the key *as it is at that moment*, not re-writing a captured
   byte pattern. `PlayerRowViewModel.PokeTreasury` re-reads the key on every tick for this reason.

Confirmed live end-to-end: with the key at `-12345`, writing `24690` into `Gold_Encoded` made the
treasury decode to exactly `12345`; the key was unchanged; restoring the original encoded value
returned it to `10`.

### 3.1 The codec, from the game's own side — and why the key moves

`Leader_set_treasury` @ `0x4C9B30` is eleven instructions and settles what §8 used to list as an open
question:

```
004C9B38   test esi, esi                ; esi = the amount to store
004C9B3A   jge  0x4C9B62
004C9B3C   xor  esi, esi                ;   negative -> clamp the treasury to zero
004C9B3E   call [0x682314]              ;   WINMM!timeGetTime
004C9B46   mov  ecx, 0xD431             ;   54321
004C9B4B   div  ecx
004C9B4D   sub  edx, 0x8235             ;   key = timeGetTime() % 54321 - 33333
004C9B55   mov  [edi+0x44], edx         ;   Gold_Decrement
004C9B58   sub  esi, eax
004C9B5A   mov  [edi+0x48], esi         ;   Gold_Encoded = amount - key
...
004C9B64   call [0x682314]              ; positive: same shape, keyed to the amount itself
004C9B6C   div  esi                     ;   key = timeGetTime() % amount - 12345
004C9B6E   sub  edx, 0x3039
```

**`Gold_Decrement` is re-seeded on every single treasury write**, from the millisecond clock. That is
the answer to "is the key ever re-seeded mid-game?", and it makes the trainer's re-encode-per-tick
freeze not merely defensive but *required* — a freeze that replayed a captured `Gold_Encoded` would
break the moment the game paid for anything.

It also explains the three keys recorded above exactly. At the start of a game the treasury is 10, so
the positive branch gives `timeGetTime() % 10 - 12345` — a value in `[-12345, -12336]`. The observed
keys were **-12345, -12342 and -12337**. All three, and the constants `54321 / 33333 / 12345` are the
same digit-pattern signature the whole scheme is built from.

The same code is inlined into `Unit_upgrade` (§4.8), which is where it was found.

---

## 4. Memory model

All addresses below are **VAs for the Steam build**. The trainer stores them as RVAs and adds the
module base the OS reports at run time, so nothing is hard-coded to `0x400000`.

### 4.1 Static objects

| Symbol | VA | Meaning |
| --- | --- | --- |
| `leaders` | `0xA75698` | inline `Leader[32]` — **the anchor for everything else** |
| `p_cities` | `0xA75668` | city container |
| `p_units` | `0xA75680` | unit container |
| `p_bic_data` | `0x9E5D08` | the loaded rules/scenario database; `Map` is embedded at `+0x3E64` |
| `p_main_screen_form` | `0xA1AF00` | `Player_CivID` at `+0x4DBC` |
| `p_current_turn_no` | `0xA74EA4` | |
| `p_human_player_bits` / `p_player_bits` | `0xA74EB4` / `0xA74EB8` | bit *N* = civ *N* |
| `p_debug_mode_bits` | `0xA74E78` | bits 2 and 3 are the two debug modes |
| `p_is_pbem_game` / `p_is_offline_mp_game` | `0xA74FAC` / `0xA75189` | writes are suppressed when set |

### 4.2 `Leader` — stride `0x20E4`

The stride is not taken on faith. It is confirmed three ways:

1. Summing the struct members in the C3X header gives `0x20E4`.
2. Brute-forcing every stride in `[0x1000, 0x9000]` under the constraint `leaders[i].ID == i` for
   `i ∈ {0,1,2,5,17,31}` leaves **exactly one** survivor.
3. The game's own array walk says so. At VA `0x50C1BD`:
   ```
   81 C5 E4 20 00 00    add  ebp, 0x20E4        ; sizeof(Leader)
   43                   inc  ebx
   81 FD 18 73 AB 00    cmp  ebp, 0x00AB7318    ; one past the end
   ```
   and `0xA75698 + 32 × 0x20E4 = 0xAB7318` exactly.

Fields the trainer uses (offsets from the leader base):

| Offset | Field | Status |
| --- | --- | --- |
| `+0x08` | `'LEAD'` class tag | Confirmed |
| `+0x1C` | `ID` — always equals the slot index | Confirmed |
| `+0x20` | `RaceID` — indexes `BIC.Races` | Confirmed |
| `+0x3C` | `Golden_Age_End` (used only as a write-test scratch field) | Inferred |
| `+0x44` / `+0x48` | `Gold_Decrement` / `Gold_Encoded` | **Confirmed** |
| `+0xA0` | `GovernmentType` | Inferred |
| `+0xF4` | `Era` | Confirmed |
| `+0xF8` | `Research_Bulbs` | Confirmed writable; see §8.2 on why banking points shortens research without finishing it |
| `+0x18C` / `+0x194` | `Unit_Count` / `Cities_Count` | Confirmed — re-checked later by tallying the whole unit container per civ: 13 of 13 agreed |
| `+0x1A4` / `+0x1A8` / `+0x1AC` | luxury / science / gold sliders | Confirmed |
| `+0x181C` | embedded `Culture` (`'CULT'` at `+0x1824`, level `+0x1838`, total `+0x183C`, income `+0x1840`, `CivID` `+0x1844`) | Confirmed |

The C3X header's `field_XX` placeholder names encode their own offsets, which makes the whole struct
self-checking: `field_4`, `field_34`, `field_4C`, `field_AC`, `field_E4`, `field_130`, `field_170`,
`field_198`, `field_10B0`, `field_15C0`, `field_18C0` all land exactly where arithmetic over the
preceding members puts them. Working backwards from the `field_D50` anchor also fixes
`sizeof(Reputation) = 0x4C`, and the `field_18C0` anchor fixes `sizeof(Culture) = 0x2C` and
`sizeof(Espionage) = 0x3C`. The Leader layout is trustworthy end to end.

### 4.3 Containers, units and cities

`Cities` and `Units` share one 0x18-byte shape:

```
+0x00 vtable      +0x04 Item*      +0x10 LastIndex (-1 when empty)      +0x14 Capacity
```

Each item is `{ int, Body* }` — eight bytes — so the body pointer is at `item + 4`. A body pointer
points *past* the object's `Base` header, which puts its four-character tag at `body − 0x14`
(`Unit = body − 0x1C`, and `Base.ClassName` is at `Base + 0x08`). A slot can be null after the object
it held was destroyed, so a null is skipped rather than treated as the end of the list.

`Unit_Body` fields, all confirmed against a live game (38 units, `LastIndex` 37, dense, `ID == index`,
and every unit's `RaceID` matching its owner's):

| Offset | Field | Note |
| --- | --- | --- |
| `+0x04` | `ID` | equals the slot index |
| `+0x08` / `+0x0C` | `X` / `Y` | |
| `+0x18` / `+0x1C` | `CivID` / `RaceID` | |
| `+0x24` | `UnitTypeID` | indexes `BIC.UnitTypes` |
| `+0x28` | `Combat_Experience` | conscript → elite |
| `+0x30` | **`Damage`** | hit points **lost**, not remaining |
| `+0x34` | **`Moves`** | movement **spent** this turn, not left |
| `+0x38` | **`Job_Value`** | worker-turns **done**, counting **up** toward the job's cost — see §4.7 |
| `+0x3C` | `Job_ID` | `enum Worker_Jobs`, or `-1` when idle |

Maximum hit points are **not a field** — the game computes them from the unit type plus the veteran
level (`Unit_get_max_hp` @ `0x5CD180`). "Full heal" therefore means writing zero damage.

Movement is stored in **thirds**, not whole points: `General.RoadsMovementRate` is 3, and a worker that
has spent its single move reads `Moves = 3`. Confirmed live — every AI unit that had already moved read
3 while the human's units, at the start of their own turn, read 0.

### 4.4 `City_Body` — and the one real gap

This is where the community header stops being reliable, and the trainer's UI reflects that.

Arithmetic over the header's members agrees with its own `field_XX` anchors up to `+0x54`:

```
+0x04 ID        +0x08 X (i16)   +0x0A Y (i16)   +0x0C CivID (i8)
+0x10 Improvements_Maintenance  +0x14 Status
+0x24 StoredFood                +0x28 StoredProduction
+0x30 Order_ID  +0x34 Order_Type
+0x38 field_38  ✔ anchor        +0x3C turns_of_flip_immunity
+0x40 cultural_level            +0x44 field_44 ✔ anchor
+0x50 DraftCount                +0x54 UnhappyTurnsDueToDrafting
```

The next member is named `field_70`, but arithmetic puts it at `+0x58` — **a 0x18 discrepancy** — and
every anchor after it (`field_84`, `field_A4`, `field_30`) is off too. So `Population`, `Corruption`,
`FoodIncome`, `CashIncome`, `Total_Cultures[32]`, the build queue and `CityName` are all at offsets
nobody has pinned.

The trainer therefore exposes **only the anchored prefix** — position, stored food, stored shields,
cultural level — and `Civ3Layout.CityTrustedPrefixEnd` marks the boundary in code. Displaying a
plausible-looking population read from an unconfirmed offset would be worse than not displaying it.

**The prefix itself is now confirmed.** A later session with 32 cities across 13 civs gave two
independent checks that agree:

- Every one of the 32 records passed `ValidateCity` — `ID == index`, coordinates inside the 130×130
  map, non-negative stores.
- Tallying `City_Body.CivID` (`+0x0C`) across the container reproduced **each leader's own
  `Cities_Count` (`Leader+0x194`) exactly, for all 13 civs** — 1, 3, 3, 3, 3, 3, 3, 2, 3, 3, 2, 3.
  Those are two unrelated structures in memory, so agreement is not self-confirming.
- `StoredFood`/`StoredProduction` were round-tripped: the trainer wrote them and the game held the
  written values.

`cultural_level` remains Inferred — plausible (1–2 early game) but not cross-checked against anything.
It is the border-expansion ladder rather than a culture total, and the level indexes the loaded
ruleset's own culture-level table, so the *Max culture* button writes a deliberately small level
(`GameFacts.MaxCityCulturePreset` = 6) rather than the 100 that `ValidateCity` would still accept:
past the end of that table, a bigger number is not a bigger bonus, just a longer reach into whatever
follows it.

Opening up the *rest* of the record still needs a decompile of `City_recompute_happiness` @ `0x4C4660`
/ `City_recompute_commerce` @ `0x4B7770`, which name their own field accesses.

### 4.5 Map and tiles

`Map` is embedded at `BIC + 0x3E64` (the surrounding `field_3CC8`/`field_3E3C` anchors both agree),
so it is at a fixed VA of `0x9E9B6C`:

| Offset | Field | Live value |
| --- | --- | --- |
| `+0x40` | `TileCount` | 8450 |
| `+0x148` | `Tiles` (`Tile**`) | heap |
| `+0x154` / `+0x168` | `Height` / `Width` | 130 / 130 |
| `+0x1EC` | `Seed` | 283554621 — matches `WorldSeed` |

`TileCount == Width × Height ÷ 2` because Civ3 uses a staggered ("isometric") grid where only every
other lattice point is a tile. That identity is used as a validator.

Each `Tile` carries its own `'TILE'` tag at `+0x44` (confirmed live). The per-civ visibility masks —
`Fog_Of_War +0x58`, `FOWStatus +0x5C`, `V3 +0x60`, `Visibility +0x64` — are **inferred** from the
header and have *not* been round-tripped through the game's display. "Reveal map" sets the last
three only: the community patch's own visibility test ORs `FOWStatus`, `V3` and `Visibility` together
and leaves `Fog_Of_War` alone, so that field appears to track something other than "this civ has
seen it". The Map tab says so and gates
"Reveal map" behind an explicit acknowledgement rather than offering it as a one-click button.

### 4.6 `BIC` tables — read, not curated

`BIC` holds whichever ruleset is loaded, which for Conquests may be the epic game, one of nine
scenarios, or any community mod. So the trainer reads the civilization and unit tables out of it
rather than shipping a table that would be right only for the unmodified game:

| Offset in BIC | Field |
| --- | --- |
| `+0x8A8` / `+0x8AC` | `UnitTypeCount` / `RacesCount` |
| `+0x3CC8` | `Race*` |
| `+0x3CD8` | `UnitType*` |

`Race` (stride `0x974`): `LeaderName +0x1C`, `AdjectiveName +0x74`, `CountryName +0x9C`,
`AggressionLevel +0x918`, `ID +0x91C`.
`UnitType` (stride `0x138`): `Name +0x08`, `Cost +0x54`, `Defence +0x58`, `ID +0x5C`, `Attack +0x60`,
`Movement +0x70`.

Both strides were recovered by brute force under `Table[i].ID == i`, and both are re-verified at run
time — if the expected stride fails, `GameTables` searches for one that holds rather than reading
garbage through a stale constant. Live, this yielded all 32 civilizations
(`Rome — Caesar`, `Egypt — Cleopatra`, … `Maya — Smoke-Jaguar`) and 141 unit types with correct
attack/defence/movement/cost.

Two more tables were mapped while answering "why does the AI have 30 units on turn 5?", and are read by
the probe rather than the trainer:

| Offset in BIC | Field |
| --- | --- |
| `+0x88C` / `+0xBB8` | `DifficultyLevelCount` / `Difficulty_Level*` (stride `0x7C`, `Name +0x04`) |

`Difficulty_Level` continues `Defencive_Land_Units +0x4C`, `Offencive_Type_Units +0x50`,
`Start_Units_1 +0x54`, `Start_Units_2 +0x58`, `Additional_Free_Support +0x5C`, `Cost_Factor +0x68`. The
pointer sits inside the block the `field_BCC` anchor closes, and reading it live produced the eight epic
difficulty names in order — Chieftain … Sid — with a monotonic bonus ladder, which is a strong enough
self-check for a probe-only table:

```
       name        defensive  offensive  startA  startB  extraSupport  AIcostFactor
  2    Regent              0          0       0       0             0            10
  3    Monarch             2          1       0       0             4             9
  6    Deity               8          4       1       2            16             6
  7    Sid                12          6       2       4            24             4
```

That settles the question: at Sid every AI civ opens with 18 free military units plus 2 extra settlers and
4 extra workers, so 28 units on turn 5 against the human's 3 is the handicap working as designed, not a
misread. Verified independently by tallying `Unit_Body.CivID` across the unit container against each
leader's own `Unit_Count` (`Leader+0x18C`) — **13 of 13 civs agreed exactly**, the same two-structures test
used for `Cities_Count` in §4.4.

A third table joins them, and it is the one the trainer **writes**:

| Offset in BIC | Field |
| --- | --- |
| `+0x8B8` | `WorkerJobCount` — 13 in the epic game |
| `+0x3E1C` | `Worker_Job*` (stride `0x74`): `Name +0x04`, `TurnToComplete +0x44`, `Order +0x54` |

`Worker_Job` is the **only** one of these tables with **no `ID` field**, so the `Table[i].ID == i` proof
that pins the others is unavailable. `Civ3Layout.ValidateWorkerJob` substitutes for it: a printable,
non-empty name and a cost inside a sane bound, required of *every* record rather than a sample. Thirteen
consecutive records satisfying that at a fixed spacing is not something arbitrary memory offers.

---

## 4.7 Worker jobs — how the cost is computed, and where the trainer can intervene

This one was settled from the game's own code rather than inferred, so it is worth writing down in full.
`get_worker_remaining_turns_to_complete` @ `0x5D5520` computes, for a unit and a job id:

```
005D553E   mov  ecx, 0x9E9B6C           ; Map — and 0x9E9B6C = p_bic_data + 0x3E64, the confirmed BicMap
005D555A   lea  ecx, [edx+edx*4+0x14]   ; Map.WorkerJobs[job_id].ID, remapping the job to a table row
005D5571   mov  esi, [0x9E9B24]         ; BIC.WorkerJobs      <- 0x9E9B24 - 0x9E5D08 = 0x3E1C
005D5577   mov  ebx, [esi + ecx*4 + 0x44]  ; Worker_Job[n].TurnToComplete, at stride 0x74
005D557D   imul ebx, eax                ; x a factor derived from the tile being worked
...
005D562B   lea  esi, [eax-0x1C]         ; Unit = Unit_Body - 0x1C
005D5636   mov  ebp, [esi+0x58]         ; Unit_Body + 0x3C = Job_ID
005D563B   jne  ...                     ;   ... skip units doing a different job
005D563D   mov  ebp, [esi+0x54]         ; Unit_Body + 0x38 = Job_Value
005D5640   sub  ebx, ebp                ; remaining = cost - work done
```

Five things fall out of those twenty instructions, all of them **[Confirmed]** and none of them
previously known here:

1. **`Job_Value` counts up.** It is *subtracted* from the total cost, so it is work done, not work left.
   Raising it finishes a job; zeroing it starts the work over.
2. **The table offsets are the game's own.** `BIC + 0x3E1C`, field `+0x44`, stride `0x74` are read out of
   the instruction stream, not derived — the same class of evidence as the `add ebp,0x20E4` array walk
   that pins the leader stride. (The arithmetic over the C3X header agrees independently, and so does the
   `BicMap` anchor `0x48` further along.)
3. **Cost = `TurnToComplete` × a terrain factor.** The multiply at `0x5D557D` takes its right-hand side
   from a lookup on the tile the worker is standing on. That factor is **not decoded here** — which is
   why "finish job" writes the base cost times a ceiling of 4 rather than reading the real threshold.
4. **Progress pools across the tile.** The loop walks every unit standing there and subtracts the
   `Job_Value` of each one whose `Job_ID` matches. This is why stacked workers finish a job together, and
   why writing the field on any one of them is enough. Observed live: two workers on the same tile with
   the same job both reading `Job_Value = 2`.
5. **`body − 0x1C` is confirmed** by `lea esi,[eax-0x1C]`, as are `Units.Items` at `p_units+4`,
   `LastIndex` at `+0x10`, the 8-byte item stride and the body pointer at `+4` — all read straight out
   of the same routine's container walk.

The rate a unit works at comes from `0x5C1D10`, which reads `leaders[unit.CivID].Government` at
`Leader+0xA0` (`mov ecx,[eax+0xA75738]` with `eax = CivID × 0x20E4`; `0xA75738 − 0xA75698 = 0xA0`) and
indexes `BIC.Governments` at `+0x3CD0` with a stride of `0x1E8`, loading a **float** — the despotism work
penalty. It also reads `BIC.Races` at `+0x3CC8` with stride `0x974`, both of which the trainer already
had as `[Confirmed]`. `Leader.GovernmentType` was `[Inferred]` before this and is now confirmed.

Live values for the epic ruleset, read through the shipped `GameTables`:

```
 0 Mine 12    1 Irrigation 8    2 Fortress 16    3 Road 6      4 Railroad 12
 5 Plant Forest 18   6 Clear Forest 4   7 Clear Wetlands 16   8 Clear Damage 24
 9 Airfield 1  10 Radar Tower 1  11 Outpost 1  12 Barricade 16
```

A worker contributes about two of those per turn, which is what makes a road on open ground the familiar
three turns and irrigation four. All thirteen names read back in `enum Worker_Jobs` order, and
`WorkerJobCount` reads 13 — two independent agreements with the community header.

**What the trainer does with it.** Two levers, deliberately different in blast radius:

- **Per unit** — write `Job_Value`. Touches one unit of one civ; the AI is unaffected. This is what
  *Finish worker jobs* does, and it declines on an idle unit rather than poking a field nothing reads.
- **Per ruleset** — write `TurnToComplete = 1` across the table. Simple and total, but the job table
  belongs to the *ruleset*, so **every civ's workers speed up** — the same objection that rules out
  buffing `UnitType.Defence` for invincibility (§6). It is therefore a **toggle** that captures the
  original costs on the way in and restores them when switched off, on detach, and on exit. Round-tripped
  live: all 13 costs → 1 → back to `12 8 16 6 12 18 4 16 24 1 1 1 16`, names intact.

Neither is instant on its own — see the completion test below, which is what decides when a banked job
actually lands.

### When the cost is read — which is what makes the toggle usable

`Unit_work_simple_job` @ `0x4638C0` is the routine that puts *one turn of work* into a job, and it settles
the question the toggle raises:

```
004639E7   mov  edx, [0x9E9B24]          ; BIC.WorkerJobs — read fresh on every tick
004639ED   mov  ecx, [edx+ecx*4+0x44]    ; TurnToComplete
004639F1   imul ecx, eax                 ; x terrain factor — the cost, computed now
00463A02   mov  esi, [edi+0x54]          ; existing Job_Value
00463A08   call 0x5C1D10                 ; this unit's work rate
00463A0D   add  eax, esi
00463A11   mov  [edi+0x54], eax          ; Job_Value += rate
00463A14   mov  [edi+0x58], ebp          ; Job_ID = job
00463A1C   mov  [edi+0x50], eax          ; and the unit's movement is spent
```

- **The cost is not cached when a job starts.** It is recomputed from the table at every work tick, so
  changing `TurnToComplete` mid-job takes effect on the next tick — and stops taking effect the moment the
  table is restored. A toggle is therefore a meaningful unit of control, not a one-way door.
- **Re-issuing a job accumulates rather than resets** — the write is `Job_Value + rate`, not `rate`. So
  telling a working unit to do the same job again is safe and adds a tick.
- **The unit's move is spent by the same routine**, which is what makes a worker's first tick land at the
  moment the order is given — during the player's own turn.

### The completion test, and why banking work is not instant

Further into the same routine is the test that actually finishes a job:

```
00463ADC   mov  eax, [eax+0x54]      ; each co-located unit's Job_Value …
00463AE3   add  edi, eax             ; … summed
00463B26   mov  ecx, [esp+0x38]      ; pooled work
00463B2A   mov  eax, [esp+0x28]      ; the cost computed at 0x4639F1
00463B2E   cmp  ecx, eax
00463B30   jl   0x464015             ; work < cost -> not finished; bail
...                                  ; otherwise, for every unit on the tile doing this job:
00463BBD   mov  [ecx+0x54], edi      ;   Job_Value = 0
00463BC0   mov  dword [ecx+0x58], -1 ;   Job_ID = -1      <- the job is done
```

**The completion test only runs inside a work tick.** Writing `Job_Value` does not finish a job by itself;
it makes the *next* tick finish it. And since a tick costs the worker its entire move
(`mov [edi+0x50], eax` at `0x463A1C`), a worker normally gets exactly one tick per turn — so banked work
lands at the start of the next turn, and **a job already due next turn cannot be shortened at all**. That
is the floor, and it is the same shape as the research floor in §8.2: the trainer can buy turns down to
one, not to zero.

Confirmed live end-to-end: a human worker building a road, given `Job_Value = 24` by the trainer, made the
game's own status line read *"will be done in **-6** turns"* — the estimate function consuming the written
value exactly as the disassembly predicts, while the job itself waited for the next tick.

**Which is why the movement hold matters.** Zeroing `Moves` returns the worker's move, the job can be
re-issued in the same turn, and that second tick runs the completion test again — with the work already
banked, it finishes on the spot. The two features are one mechanism: `Job_Value` supplies the work, the
movement hold supplies the tick.

**And why banking has to be standing rather than one-shot.** The completion path above sets `Job_Value = 0`
and `Job_ID = -1`, so a finished worker keeps nothing: the next job starts from `rate` and needs banking of
its own. A one-click action therefore has to be repeated once per job, which is what the *Keep worker jobs
banked* toggle removes — it re-banks on every poll, and skips a unit whose figure is already right, so the
cost is a read the poll loop was doing anyway. With both toggles on, the loop for the player is "order it,
order it again", repeatable as many times in a turn as they care to click, and the worker can relocate
between jobs because its movement keeps coming back.

The practical consequence, and the reason the UI now says so: **AI workers tick during the AI's turn, which
runs after the human ends theirs.** A toggle switched off before ending the turn does not reach them.
What has *not* been pinned down is where the *continuation* tick lands for a unit that is already working
— if that happens in the interturn rather than during the player's turn, switching off early would deny
the player the benefit as well. That is a one-game observation to make, not a decompile.

Scanning `.text` for the table pointer finds **60+ references**, well beyond the two routines above —
several in what look like AI evaluation paths. So while the toggle is on it changes what the AI *plans*,
not only how fast it digs.

What is **not** attempted: writing `Job_ID` to start or change a job. Beginning a job is more than
setting a number — the game also sets unit state, the tile's overlays (`Map.WorkerJobs[n]` carries
set/unset overlay masks) and the animation — so a poked job id would describe work the game never began.

---

## 4.8 Changing what a unit *is* — retyping, and where armies come from

Six routines settle this end to end, and all of it is read out of `.text` rather than inferred.

### The type is not a label — the game resolves everything through it

`Unit_has_ability` @ `0x5CB430`:

```
005CB44A   mov  eax, [esi+0x40]           ; Unit+0x40 = Unit_Body+0x24 = UnitTypeID
005CB44D   mov  edx, [0x9E99E0]           ; BIC.UnitTypes  <- 0x9E99E0 - 0x9E5D08 = 0x3CD8
005CB454   lea  ecx, [eax+eax*4] ...      ; x 39, then x 8  ->  stride 0x138
005CB45F   call 0x5F4750                  ; UnitType_has_ability
```

and the accessor it calls, in full:

```
005F4756   cmp  ecx, 0x20
005F4762   test dword [eax+0x88], edx     ; UnitAbilities, bit n
005F4778   test dword [eax+0x130], edx    ; Extra_Abilities, bit n-32
```

`Unit_can_perform_action` @ `0x5D0670` does the same for *orders*, indexing four action words from a
single base:

```
005D069A   mov  eax, [esi+0x40]           ; UnitTypeID again
005D06A9   shr  ebp, 0x1C                 ; the action constant's top nibble picks the word
005D06B7   mov  ecx, [eax+edx*4+0xA8]     ; Standard / Special / Worker / Air actions
```

Five things fall out, all **[Confirmed]**:

1. **A unit's abilities, orders, stats and maximum hit points are looked up from `UnitTypeID` every
   time they are needed.** Nothing is cached on the unit. So writing that one field really does change
   what a unit *is*, immediately and completely — which is what makes the Units tab's *Type* column a
   single four-byte write rather than a reconstruction.
2. `BIC.UnitTypes` at `+0x3CD8` and the `0x138` stride, previously brute-forced against
   `Table[i].ID == i`, are now the game's own numbers.
3. `UnitType.UnitAbilities` sits at `+0x88`, and abilities 32 and up live in a second word at `+0x130`
   — which is the second-to-last field in the record, so it independently re-confirms the stride.
4. The four action words start at `UnitType+0xA8`. `UCV_Build_Army` is `0x10000040`: word 1
   (`Special_Actions`, `+0xAC`), bit `0x40`.
5. `Unit_Body+0x24` as `UnitTypeID` moves from "indexes BIC.UnitTypes" to read-out-of-the-instruction-
   stream, alongside the `Unit = body − 0x1C` offset the same routines use.

### What a retype does *not* reach

`Leader_spawn_unit` @ `0x575900` is what creates a unit, and reading it says exactly what a retype
misses — as well as closing the question of whether a trainer could create one:

```
005759AE   push 0x404                     ; sizeof(Unit) — a heap allocation
005759B3   call 0x6683E1                  ; operator new
005759D4   mov  [esi], 0x68ADD0           ; vtable
005759DA   mov  [esi+0x1C], 0x68ADCC      ; the body's own vtable — body = object + 0x1C
00575A00   push 0x64 / call 0x4D0E40      ; grow p_units from empty
00575A31   mov  [ecx+edi*8+4], esi        ; link into the container's item array
00575CBB   mov  edx, [ebp+0x18C] / inc    ; Leader.Unit_Count++
00575D7C   inc  word [ecx+edx*2]          ; per-unit-type tally, Leader+0x15F0
00575D8B   call 0x5F4750 (ability 0x12)   ; if it is an army …
00575D94   inc  word [ebp+0x188]          ;   … the leader's army tally
00575DD6   call 0x5A6810                  ; build the animation name from type + Leader.Era + race
00575DFB   call 0x406810                  ; load it into the unit at Unit+0x27C = Unit_Body+0x260
```

So:

- **Creating a unit is not a value a trainer can write.** It is a `0x404`-byte heap allocation, a
  container link (with a growth path), several counters and an animation load. The trainer is data-only
  (§6), and this is firmly on the other side of that line.
- **The artwork is chosen at spawn**, from the unit type, its owner's era and its race, and stored
  *in the unit*. A retyped unit is therefore expected to keep the sprite it was born with while
  fighting as its new type. This is a code-derived expectation, **not observed on screen**.
- **The owner's tallies are incremental**, maintained at spawn and despawn rather than counted on
  demand, so a retype leaves them off by one. The trainer does not correct them: they are AI-facing
  bookkeeping, and writing `Leader+0x15F0` / `+0x188` to paper over a cosmetic drift is more risk than
  the drift is worth.

### The game itself never retypes in place

`Unit_upgrade` @ `0x5CF2E0` — the game's own "this unit becomes another type" — spawns a *new* unit and
destroys the old one:

```
005CF3B1   call 0x575900                 ; Leader_spawn_unit(type, x, y, …)
005CF3FF…  rep movsb                     ; copy Custom_Name  (Unit+0x74 = Body+0x58)
005CF42C   mov  eax, 2 / mov [ebx+0x44]  ; copy Combat_Experience, capped at Veteran
005CF437   mov  [ebx+0x38], ecx          ; copy RaceID
005CF474   mov  edx, [esi+0x60]          ; every unit whose Container_Unit …
005CF47A   cmp  edx, eax                 ;   … is the OLD unit's ID
005CF4A0   mov  [esi+0x60], eax          ;   is re-homed onto the new one
005CF4B7   push 0x12 / call 0x5F4750     ; and if the new type has the Army ability …
005CF4C7   call 0x5CB840                 ;   … Unit_load_into_army
005CF4EB   call 0x5CA720                 ; Unit_despawn — the original is destroyed
```

Two more confirmations from that: **`Container_Unit` at `Unit_Body+0x44` holds the *ID* of the unit
carrying this one** (army member or transport passenger), and **`0x12` is the Army ability bit** — the
game tests it itself to decide whether passengers should be loaded as an army.

### Armies: one instruction of `Unit_form_army` is the whole feature

```
005CB5BA   mov  ecx, [esi+0x28]          ; Y
005CB5C5   mov  edx, [esi+0x24]          ; X
005CB5CA   mov  edi, [0x9E9A90]          ; <- BIC + 0x3D88 = General.BuildArmyUnitID
005CB5DF   lea  ecx, [edx*4 + 0xA75698]  ; &leaders[this unit's CivID]
005CB5E6   call 0x575900                 ; Leader_spawn_unit(army type, same tile)
005CB5F1   mov  eax, [esi+0x38]
005CB5F4   mov  [edi+0x38], eax          ; the army inherits the leader's RaceID
005CB607   call 0x5CA720                 ; Unit_despawn — the leader is consumed
```

`0x9E9A90 − 0x9E5D08 = 0x3D88`, which is `BIC.General + 0xAC` — **`General.BuildArmyUnitID`, confirmed
from the instruction stream**, and the `General` block itself is anchored by `FoodPerCitizen` at
`BIC+0x3DAC` (`General+0xD0`, reading 2 live). Its neighbour `BattleCreatedUnitID` at `+0x3D84` is the
great-leader type by the same arithmetic.

And the gate that decides whether a unit is *offered* Build Army, inside `Unit_can_perform_action`:

```
005D095A   call 0x5CB430 (ability 0x13)  ; Leader — tested against the unit's CURRENT type
005D0961   je   0x5D096C                 ;   no ability -> the strict path -> refuse
005D0963   test byte [esi+0x1F4], 3      ; leader_kind (Unit_Body+0x1D8)
005D096A   je   0x5D098A                 ;   0 -> allowed
005D097D   test byte [esi+0x1F4], 1      ;   otherwise military leaders only
```

That last detail is what makes the whole feature work without a code patch. An ordinary unit is spawned
with `leader_kind = 0`, and `0 & 3 == 0` takes the **allowed** branch — the branch that refuses is the
one for a *scientific* leader, which is the game's own rule. So a unit retyped to the great-leader type
needs nothing else written to it: the ability test reads its new type, the kind test passes, and the
order appears.

### What the trainer does with all this

- **The *Type* column** writes `Unit_Body+0x24` and clears `Damage` with it, because maximum hit points
  come from the type and damage carried over from a larger one would put the unit past dead. The list
  it offers is filtered to the unit's own land/sea/air domain unless *Any domain* is ticked.
- ***Make great leader*** writes the ruleset's `BattleCreatedUnitID` onto the selected unit and then
  stops, deliberately. The player gives the game's own Build Army order, and `Unit_form_army` builds a
  real army through `Leader_spawn_unit` — correct container linkage, correct tallies, correct artwork,
  none of it imitated.
- **Neither type id is hard-coded, and neither is trusted on its offset alone.** `GameTables` reads
  both out of `BIC.General` and then requires the type each one names to actually carry the matching
  ability bit (`0x12` army, `0x13` leader). Two unrelated facts have to agree; a mod that moved the
  field yields -1 and the feature switches itself off rather than acting on a wrong number.
- **`Container_Unit` is deliberately not written.** Loading a unit into an army by hand would mean
  setting the member's `+0x44`, the army's member count and its top defender, and `Unit_load_into_army`
  @ `0x5CB840` demonstrably touches more than that. The great-leader route makes it unnecessary.

`UnitType.Unit_Class` (`+0x9C`, land/sea/air) is the one field here that is **[Inferred]**. It is
bracketed with no slack between the header's `field_98` anchor and `Standard_Actions` at `+0xA8`, which
the action indexing above confirms — exactly four fields fit that gap and this is the first. It is
still checked at run time rather than trusted: every type in the loaded ruleset must hold one of the
three domains and at least two distinct domains must appear, or the trainer stops filtering and offers
the whole table.

---

## 5. The locator

### Chain A — static globals (the normal path)

```
base    = MainModule.BaseAddress                (0x400000 in practice, but read, not assumed)
leaders = base + 0x675698
civ     = i32[base + 0x61FCBC]                  (Main_Screen_Form.Player_CivID)
player  = leaders + civ × 0x20E4
gold    = i32[player+0x48] + i32[player+0x44]
```

Nothing is trusted until **all 32 slots** pass, simultaneously:

| Validator | Why it is there |
| --- | --- |
| `'LEAD'` tag at `+0x08` | cheap rejection of unrelated memory |
| `ID == slot index`, for all 32 | **the load-bearing one** — only the true base *and* the true stride satisfy it 32 times running |
| identical vtable, inside `.rdata` | catches a shifted window of otherwise-plausible bytes |
| `RaceID` in `[-1, 32)` | unused slots read `-1` |
| sliders total exactly 10 | a rules invariant, free to check, very unlikely to hold by accident |
| `Era`, city and unit counts in range | catches garbage |
| embedded `'CULT'` tag **and** `Culture.CivID == slot` | a second, independent index agreeing with the first |
| decoded treasury plausible | exercises the gold codec as part of validation |

Then the human civ id must be in range *and* have its bit set in `p_player_bits`, otherwise the
locate is refused — that is what stops the trainer attaching confidently at the main menu, where the
leader array exists but holds no game.

The `.rdata` range used for the vtable check is **parsed from the PE header in the mapped image**
rather than hard-coded, which also supplies the `TimeDateStamp` build fingerprint.

Measured live: **~3 ms**, 32/32 slots, correct civ, correct map.

### Chain B — re-deriving the array from the game's own code

If the globals ever move, the array walk in §4.2 is still in `.text`. Chain B sweeps `.text` for
`add r32, imm32` followed within 16 bytes by `cmp r32, imm32`, keeps pairs whose implied base
(`end − 32 × stride`) lands in `.data`, takes the modal stride and the lowest base in that cluster,
and then runs **the same** validation as Chain A. The lowest base is the right one because higher
candidates in the cluster are field offsets within the first record.

`FormatCheck` exercises this by planting the array somewhere other than its known RVA and leaving only
the code idiom behind — and separately asserts that without the idiom the moved array is *not* found,
so the chain cannot be passing by accident.

### Chain C — the value scanner

If neither chain validates, the Scanner tab remains, with one prominent caveat repeated in the UI and
in the guided-scan text: **an exact scan for the treasury cannot work** (§3). The guide walks the user
through the relative scan — unknown value, change your gold, narrow by Changed — which converges on
the encoded half instead. City stores, unit damage and the turn counter are ordinary integers and
scan normally.

---

## 6. Confirmed vs Inferred

**Confirmed** — read live and cross-checked against an independent source, or written and read back:

- every static object in §4.1 (five cross-checks against `conquests.ini`)
- the `Leader` stride and every `Leader` field the trainer surfaces
- the gold codec, including a full write round-trip (`10 → 12345 → 10`, key untouched)
- `WriteProcessMemory` reaching the game at all (scratch field `-1 → 24301 → -1`)
- the container shape, and every `Unit_Body` field the trainer surfaces — including `Job_Value` and
  `Job_ID`, and the `body − 0x1C` header offset, all read out of the game's own instruction stream (§4.7)
- `Unit_Body.UnitTypeID` (`+0x24`) as the field the game resolves stats, abilities, orders and maximum
  hit points through, live, on every lookup — from `Unit_has_ability` and `Unit_can_perform_action` (§4.8)
- `Unit_Body.Container_Unit` (`+0x44`) as the *ID* of the unit carrying this one, from the passenger
  re-homing loop in `Unit_upgrade`
- `UnitType.UnitAbilities` (`+0x88`), the overflow word `Extra_Abilities` (`+0x130`), the four action
  words from `+0xA8`, and the ability bits `0x12` (Army) and `0x13` (Leader) — all from the game's own
  accessors, which also re-derive `BIC.UnitTypes` (`+0x3CD8`) and the `0x138` stride from code
- `General.BuildArmyUnitID` at `BIC + 0x3D88`, read as an absolute address by `Unit_form_army`, and with
  it the position of the `General` block
- the gold codec's key schedule: `Leader_set_treasury` re-seeds `Gold_Decrement` from `timeGetTime()`
  on **every** treasury write (§3.1), which both answers an open question and accounts for the three
  keys observed live
- the worker-job table: `BIC + 0x3E1C`, `TurnToComplete + 0x44`, stride `0x74`, count at `BIC + 0x8B8`
  — from the game's code, plus a live write round-trip of all 13 costs, restored exactly
- `Leader.GovernmentType` (`+0xA0`) and `BIC.Governments` (`+0x3CD0`), both read by the worker-rate
  routine at `0x5C1D10`; `GovernmentType` was previously Inferred
- the `BIC.General` block (embedded at `+0x3CDC`, size `0x138`), by five independent cross-checks against
  known epic-game rules: `FoodPerCitizen` 2, `RoadsMovementRate` 3, `MaximumSize_Town` 6,
  `MaximumSize_City` 12, `GoldenAgeTurns` 20
- `Map` width/height/tile-count/seed, and the `'TILE'` tag offset
- the `BIC` table pointers, counts and both strides, plus `Race` and `UnitType` fields
- the `City_Body` prefix — `ID`, `X`/`Y`, `CivID` (tallied against `Leader.Cities_Count` for 13 civs),
  and the food/shield stores (write round-trip)
- `Leader.Research_Bulbs` as writable (30,000 written and restored)

**Inferred** — derived from the community header and internally consistent, but never round-tripped
through the game's own display:

- `Leader` `CapitalID`, `Golden_Age_End`, `Tiles_Discovered`, research id/turns
- `General.ResearchTime_Min` / `ResearchTime_Max` (`BIC + 0x3E08` / `+0x3E04`, reading 4 and 50) — the
  *offsets* are as solid as the rest of the General block above, but that this field is what floors
  research has not been tested. See §8.2.
- `City_Body.cultural_level` — surfaced, and written by the Cities tab's *Max culture* button, but
  still Inferred; and every City field past the anchored prefix (not surfaced at all)
- all four tile visibility masks
- `Race.AggressionLevel`
- `UnitType.Unit_Class` (`+0x9C`) — bracketed with no slack between two confirmed anchors, and
  validated at run time before it is used to filter anything (§4.8)
- `General.BattleCreatedUnitID` (`BIC + 0x3D84`) as the great-leader type — the *offset* is its
  confirmed neighbour minus four, and the *meaning* is cross-checked against the Leader ability rather
  than assumed
- that a retyped unit keeps its old sprite. The mechanism is confirmed — the animation is built from
  the type at spawn and stored in the unit — but the consequence has not been watched on screen

**Not attempted / not shipped:**

- **Granting technologies.** C3X exposes only a function, `Leader_has_tech` @ `0x56D5A0`; no tech
  bit-array field is named anywhere in the header. It would need that function decompiled first.
- **Gold per turn.** Not stored — `Leader_recompute_economy` @ `0x56D420` recomputes it from the
  cities each turn, so there is nothing to poke and a poke would be undone immediately.
- **"Always your turn."** No such flag exists; turn flow is `perform_interturn` @ `0x4FF290`.
- **Unit invincibility.** Not reachable by writing data, for two independent reasons. Combat is
  *atomic*: `Fighter_begin` @ `0x4AB470` runs every round of a battle, the kill and `Unit_score_kill`
  before it returns, so a poll loop running between frames can never intervene — the damage freeze can
  only heal a unit that already survived. And maximum hit points are *not stored*: `Unit_get_max_hp` @
  `0x5CD180` computes them from the unit type and veteran level, so there is no per-unit ceiling to
  raise. `UnitType.Defence` (`+0x58`, confirmed) and `Hit_Point_Bonus` (`+0xA4`, arithmetic from the
  `field_94` anchor but **unconfirmable by observation** — every base unit reads 0 there) would work,
  but unit types are shared rules data, so the AI's units of the same type are buffed identically.
  Genuine per-unit invincibility would need a code cave and a `JMP` patched into `.text`; the trainer
  is deliberately data-only.
- **Creating a unit from nothing.** Settled by reading `Leader_spawn_unit` (§4.8) rather than assumed:
  a unit is a `0x404`-byte heap object the game allocates, links into a container it may have to grow,
  counts in three separate tallies, and loads an animation into. None of that is reachable with
  `WriteProcessMemory`. The game's own spawn routine would do it in one call, but reaching it means
  `VirtualAllocEx` plus a thunk plus `CreateRemoteThread` — running code inside a single-threaded,
  non-reentrant engine at a moment it did not choose. That is a different risk class from anything
  here, and it is not attempted.

  The data-only substitute has not been built yet but is real: `City_Body.Order_ID` (`+0x30`) and
  `Order_Type` (`+0x34`) sit inside the anchored, confirmed City prefix, are currently `[Inferred]` and
  unsurfaced, and are what the existing *Max shields* action would need in order to make a city build
  any unit next turn — through the game's own production path, correctly. What is missing is one probe
  session to learn the `Order_Type` encoding by reading a city building a unit against one building an
  improvement.
- **Loading units into an army by hand.** `Container_Unit` is confirmed (§4.8), but the army side of
  the linkage is not, and `Unit_load_into_army` @ `0x5CB840` touches more than two fields. The
  great-leader route makes it unnecessary — see §4.8.
- **A save editor.** See §7.

---

## 7. The save format

Civ3 `.SAV` files **are** decodable, and this was settled rather than guessed — though the trainer
still ships no save editor, because live memory is the verifiable path and the container has not been
mapped field-for-field.

`Chris01.SAV` begins `00 06`, which is a textbook **PKWare Data Compression Library ("implode")**
stream header: byte 0 = literal coding (`0` = uncoded), byte 1 = dictionary size in bits (`6` = 4 KiB).
Not zlib — there is no `78 9C`. A Blast-style decompressor
(`.docs/probe/Blast.cs`) run over the whole file from offset 0 expands 222,310 bytes to **2,153,850**
(9.69×), and the result is unmistakably a Civ3 save:

- `CIV3` as the first four bytes, then a GUID, then a `BIC ` section at `0x1E` carrying the scenario
  path `Scenarios\6B MP Age of Dusk\`.
- Record tags throughout: `LEAD` ×33, `RACE` ×14, `WRLD` ×3, `PLGI` ×2, `GAME` ×2, `CONT` ×51,
  `GOVT`, `ERAS`, `GOOD`, `CTZN`, `BLDG`, `CULT`, `ESPN`, `DATE`.

`LEAD ×33` is the telling one — consistent with the 32-slot leader array plus one. (`TILE` and `CITY`
occur far more often than there are objects, because that count is a raw four-uppercase-letter byte
scan and matches inside payloads too; it is not a record count.)

The container is a memory dump. `Base` is
`{ vtable, field_4, ClassName, DataLength, field_10, pStart, pEnd }`, and `pStart`/`pEnd` delimit the
serialised range — which is why **save offsets and RAM offsets are the same numbers**. A future save
editor would share the layout table in `Civ3Layout` rather than needing its own.

What is missing before one could be written safely: the record framing has not been walked
end-to-end, and re-compression would have to be either implemented or avoided (Civ3 will load an
uncompressed save, but that path is untested here).

---

## 8. Open questions

1. **`City_Body` past `+0x54`** — still the largest gap; the anchored prefix is now confirmed but
   population, corruption, the incomes and the build queue are not. Needs the two `City_recompute_*`
   decompiles.
2. **What an advance actually costs, and why banking points does not finish it now.** "Finish
   research" banks a flat 1,000,000 points into `Research_Bulbs` rather than reading the threshold,
   because the cost is derived from the rules database, the difficulty and how many civs already know
   the tech — none of which is decoded here. The field is confirmed writable; the completion rule
   (points compared at a turn boundary) is inferred from the game's behaviour, not from its code.

   Observed live: banking 30,000 **shortened** the research but the advance still took a few more
   turns, and raising the amount is not expected to change that — 30,000 already cleared any epic-game
   cost, so the remaining turns are not a shortfall of points. The likely explanation is a floor on how
   few turns an advance may take, which points cannot buy past.

   **That floor now has a name and an address.** The worker-job work (§4.7) required mapping
   `BIC.General`, and it contains `ResearchTime_Min` at `BIC + 0x3E08` and `ResearchTime_Max` at
   `+0x3E04` — reading **4** and **50** in a live epic game, which is exactly the shape of the observed
   behaviour ("a few more turns" rather than instant). The offsets sit inside a block confirmed five ways
   over (§6), so what is missing is only the causal test: **write 1 there, bank the points, and see
   whether the advance lands next turn.** That is one probe run, and it has not been done — so nothing in
   the UI mentions it yet, and `FinishResearchBulbs` is unchanged. The other leads stand if it fails:
   decompiling the interturn research step (`perform_interturn` @ `0x4FF290` reaches it), and probing
   `Research_Turns` (`Leader+0x100`, Inferred, not surfaced).
3. **Tile visibility** — four candidate masks, none confirmed on screen. Decompiling
   `Leader_reveal_tile` @ `0x567100` would say which must be set together.
4. **Tech storage** — see §6.
5. ~~**Whether `Gold_Decrement` is ever re-seeded mid-game.**~~ **Answered** — see §3.1.
   `Leader_set_treasury` @ `0x4C9B30` re-seeds it from `timeGetTime()` on every treasury write, and the
   three keys observed live are exactly what its arithmetic produces at a starting treasury of 10. The
   trainer's re-encode-per-tick freeze was the necessary design, not merely the cautious one.
6. **The save container's record framing**, if a save editor is ever wanted.
7. **Whether a retyped unit's artwork actually changes on screen** (§4.8). The mechanism says no — the
   animation is built from the type when the unit is spawned and stored in the unit — and a save and
   reload ought to correct it, since loading rebuilds the objects. Both halves are one game session's
   observation away, and neither is claimed in the UI beyond what the code proves.
8. **`City_Body.Order_ID` / `Order_Type`** (`+0x30` / `+0x34`) — the encoding that would let the
   trainer set a city's build order and, with the existing shield fill, have the game itself produce
   any unit next turn. See §6.

---

## 9. Reproducing this

The git-ignored `.docs/` workspace holds the tools:

```
.docs/c3x/                    the C3X CSV and header, downloaded
.docs/ghidra-scripts/         ApplyC3XSymbols.java — labels the exe from the CSV's Steam column
.docs/probe/                  a read-only console probe (plus one explicit write-test mode)
```

```powershell
dotnet run --project .docs\probe                 # dump globals, leaders, units, cities, map, BIC
dotnet run --project .docs\probe -- locate       # drive the shipped locator against the live game
dotnet run --project .docs\probe -- bic          # civilizations and unit types
dotnet run --project .docs\probe -- sav <file>   # decompress a .SAV and tally its record tags
dotnet run --project .docs\probe -- writetest    # write + read back + restore (the only writing mode)
```

Everything in `.docs/` is git-ignored, including the copied game executable and save.

**Attribution.** The layout knowledge here is the Civ3 community's, chiefly **Antal1987**
(C3CPatchFramework) and **Flintlock/maxpetul** (C3X). This trainer transcribes and re-verifies facts —
addresses and field offsets — rather than copying code, and neither project's source is vendored.
