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
| `+0xF8` | `Research_Bulbs` | Confirmed |
| `+0x18C` / `+0x194` | `Unit_Count` / `Cities_Count` | Confirmed (unit count agreed with the container) |
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

Maximum hit points are **not a field** — the game computes them from the unit type plus the veteran
level (`Unit_get_max_hp` @ `0x5CD180`). "Full heal" therefore means writing zero damage.

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

Closing this gap needs either a live game that actually has cities (the probed session was at turn 0
with `p_cities->LastIndex == -1`) or a decompile of `City_recompute_happiness` @ `0x4C4660` /
`City_recompute_commerce` @ `0x4B7770`, which name their own field accesses.

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
- the container shape, and every `Unit_Body` field the trainer surfaces
- `Map` width/height/tile-count/seed, and the `'TILE'` tag offset
- the `BIC` table pointers, counts and both strides, plus `Race` and `UnitType` fields

**Inferred** — derived from the community header and internally consistent, but never round-tripped
through the game's own display:

- `Leader` `CapitalID`, `Golden_Age_End`, `GovernmentType`, `Tiles_Discovered`, research id/turns
- every `City_Body` field (the prefix is anchor-bracketed, but there were no cities to read, so the
  constants are tagged `[Inferred]` in `Civ3Layout` and the Cities tab says so)
- all four tile visibility masks
- `Race.AggressionLevel`

**Not attempted / not shipped:**

- **Granting technologies.** C3X exposes only a function, `Leader_has_tech` @ `0x56D5A0`; no tech
  bit-array field is named anywhere in the header. It would need that function decompiled first.
- **Gold per turn.** Not stored — `Leader_recompute_economy` @ `0x56D420` recomputes it from the
  cities each turn, so there is nothing to poke and a poke would be undone immediately.
- **"Always your turn."** No such flag exists; turn flow is `perform_interturn` @ `0x4FF290`.
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

1. **`City_Body` past `+0x54`** — the largest gap. Needs a game with cities, or the two `City_recompute_*`
   decompiles.
2. **Tile visibility** — four candidate masks, none confirmed on screen. Decompiling
   `Leader_reveal_tile` @ `0x567100` would say which must be set together.
3. **Tech storage** — see §6.
4. **Whether `Gold_Decrement` is ever re-seeded mid-game.** The trainer is written to survive it
   (it re-encodes against a fresh read every tick), but it has not been observed happening.
   `Leader_set_treasury` @ `0x4C9B30` would answer it.
5. **The save container's record framing**, if a save editor is ever wanted.

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
