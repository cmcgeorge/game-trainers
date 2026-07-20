# War of the Lance — Reverse-Engineering Notes

Reverse-engineering findings for **War of the Lance** (SSI, 1989 — an *Advanced Dungeons & Dragons*
*Dragonlance* strategic wargame), as used by the trainer in this folder.

> **Method / caveat.** The proposal asked for Ghidra-assisted RE of the executable and the running
> game. In this environment **Ghidra and DOSBox were not available and the game was not running**, so
> the findings below were recovered by **static analysis of the shipped data files** in `.game/`
> (parsed with Python) and cross-checked against the bundled manual (`.game/warlance.txt`). Every
> file-format claim here is verified byte-for-byte by the `test/FormatCheck` harness. Live guest-RAM
> offsets (the locator's job) are derived from these file images but need the running game to confirm
> against — see [Live memory location](#live-memory-location).

---

## 1. The data files

The game ships a set of container files that all share one header format:

| File | Role |
| --- | --- |
| `NAT.DAT` | Nation / place name string table (28 entries) |
| `WL2.DAT` | Master label table (sides, unit types, terrain, champions — 158 strings) |
| `MENU.DAT` | UI / menu strings, ending in the five season labels |
| `WL.DAT` / `SCEN.DAT` | Campaign / scenario working state (unit strengths, qualities, …) |
| `WL.UNT` / `SCEN.UNT` | Unit **placement** table (map coordinates + type) |
| `WL.MAP` / `*.MAP` | Terrain maps |
| `Q1.DAT` / `Q2.DAT` | Quest data |
| `P*.BIN`, `MAP*.BIN` | Packed graphics / map bitmaps |

`WL.*` is the **CAMPAIGN** game (the war's opening: only Neraka allied to Highlord); `SCEN.*` is the
**SCENARIO** game (further into the war, both sides with allies and several nations already
conquered). This mirrors option **B** on the game's opening menu.

---

## 2. Container header (all `.DAT` / `.UNT` / `.MAP` / `.BIN`)

Every container begins with a **7-byte header**, little-endian:

```
+0  u8   magic        always 0xFD
+1  u16  checksum     differs per file
+3  u16  tag          0x0000 for most files; an ASCII pair for some MAP*.BIN
+5  u16  payloadLen   file length == payloadLen + 7
+7  ..   payload
```

The invariant **`fileLen == payloadLen + 7`** holds for **every** shipped file, so it doubles as a
cheap "is this really a WOTL container?" gate. Implemented in `Game/SaveContainer.cs`; asserted in
`FormatCheck` (magic `0xFD`, payload length, invariant, file length).

---

## 3. Text encoding — "high-bit ASCII"

All on-screen strings are stored as **ASCII with bit 7 set** on every character (`'A'` `0x41` →
`0xC1`, space `0x20` → `0xA0`), with **`0xFF`** as the separator between strings. A string table is
simply a container payload full of `0xFF`-delimited high-bit words.

Decoding: `char = byte & 0x7F`; a `0xFF` byte ends the current word. Implemented in `Game/GameText.cs`
(`Encode` / `DecodeWord` / `DecodeAll`); the round-trip is asserted in `FormatCheck`.

---

## 4. String tables

### 4.1 `NAT.DAT` — nation / place names (28 entries)

Decoded in order (this order is the engine's nation ordinal):

```
BLODE, CAERGOTH, GOODLUND, GUNTHAR, HYLO, KAOLYN, KERN, KHUR, KOTHAS, LEMISH,
MAELSTROM, MITHAS, NERAKA, NORDMAAR, N. ERGOTH, PALANTHUS, QUALINESTI, SANCTION,
SILVANESTI, SOLANTHUS, TARSIS, THORBARDIN, THROTYL, VINGAARD, ZHAKAR,
CLERIST TOWER, SOTH, -
```

The first 25 are the playable/allyable nations; the trailing three (`CLERIST TOWER`, `SOTH`, `-`) are
map objectives / a placeholder. Stored in `GameFacts.NationNames`; the head of this table is the
primary live-memory anchor.

### 4.2 `WL2.DAT` — master label table (158 strings)

Contains, in blocks: the three **sides** (`HIGHLORD`, `WHITESTONE`, `NEUTRAL`) and their two-letter
codes (`HL`, `WS`, `NE`); the **unit-type** labels (`INF`, `CAV`, `FLEET`, `PEGASUS`, `GRIFFON`,
`DRAGON`, `CITADEL`, `LEADER`, `DRACONIAN`, `WIZARD`, `DIPLOMAT`, `HERO`); the **21 terrain** names
(`GRASSLAND`, `STEPPE`, `FOREST`, `MOUNTAIN`, `MTN. PASS`, `TUNNEL`, `TUNNEL ENTR.`, `RIVER`,
`STREAM`, `PORT CITY`, `COAST`, `SEA`, `MAELSTROM`, `GLACIER`, `TOWER`, `DWARVEN FORT`, `FORTRESS`,
`BRIDGE`, `FORT. CITY`, `MARSH`, `DESERT`); and champion names. Stored across `GameFacts.SideNames`,
`SideCodes`, `UnitTypeNames`, `TerrainNames`.

### 4.3 `MENU.DAT` — season labels

Ends in the five per-year turn labels: `MAR/APR`, `MAY/JUN`, `JUL/AUG`, `SEP/OCT`, `WINTER`. That is
**5 turns/year**, and the manual states the campaign is **6 game years = 30 turns**
(`GameFacts.TotalTurns`). The fifth season (`WINTER`) is the recovery turn (units don't recover
fatigue in winter — manual, Recovery Phase).

---

## 5. Unit placement table — `WL.UNT` / `SCEN.UNT`

1607-byte file = 7-byte header + **1600-byte payload** = **400 slots × 4 bytes**:

```
+0  u8  X          map column (0xFF when the slot is empty)
+1  u8  Y          map row    (0xFF when the slot is empty)
+2  u8  TypeCode   engine unit-type / owner code (raw)
+3  u8  Flag       always 0x05 on shipped files (an "in play" marker)
```

Empty slots are `0xFF 0xFF ...`. `WL.UNT` has **28** occupied slots (campaign start); `SCEN.UNT` has
**80** (scenario start — more nations already in play). Implemented in `Game/UnitTable.cs`; the
400-slot count, the 0x05 flag on every occupied slot, and empty-slot detection are asserted in
`FormatCheck`.

---

## 6. Working state — `WL.DAT` / `SCEN.DAT`

The payload of `WL.DAT`/`SCEN.DAT` opens with a **current-strength array**: one byte of live strength
per unit, in a fixed order, for the Highlord assault force and Neraka's core:

| Count | Side | Nation | Type | Base number |
| --- | --- | --- | --- | --- |
| 9 | Highlord | Highlord | Baaz Draconian | 200 |
| 10 | Highlord | Highlord | Kapak Draconian | 150 |
| 8 | Highlord | Neraka | Mercenary Infantry | 200 |
| 2 | Highlord | Neraka | Mercenary Cavalry | 150 |

That is **29 leading cells**. These counts and base numbers come straight from the manual's unit
appendix (§ *SPECIAL UNITS* → HIGHLORD, and NERAKA).

**Why "current" and not a constant table:** in `WL.DAT` (campaign start) all 29 cells read their base
numbers (9×200, 10×150, 8×200, 2×150). In `SCEN.DAT` the *same 29 cells* are already battle-worn
(varied, lower values) — proving the block is **current** strength, mutated by play, not a static
data table. This is exactly the value the trainer edits.

Immediately after the 29 cells comes a short, **constant** qualities/base-number run that is
**byte-identical in both `WL.DAT` and `SCEN.DAT`**:

```
3,3,3,3,4,4,4,5, 110,110,110,110,20,20,20,20, 110,110,110,110,20,20,20,20
```

Because it does not change as the game is played, this run is the trainer's **locator signature**; the
editable strength block sits at a fixed **−29-byte delta** in front of it. Implemented in
`Game/StrengthTable.cs`; both the leading base-number run and the following signature are asserted
against the embedded `WL.DAT` head in `FormatCheck`.

**Scope limit (verified).** A `WL.DAT`-vs-`SCEN.DAT` byte diff showed the layout *past* the leading
block (the full per-nation, per-unit records: quality, fatigue, OP, fortification, carried item, map
link, …) is interleaved and **cannot be aligned reliably from the file images alone** — separating
those fields would need live guest-RAM dumps at known game states (which weren't available here).
The trainer is therefore deliberately scoped to the **verified** 29-cell current-strength block
rather than guessing the deeper record shape.

---

## 7. Live memory location

The buffer's address in the emulator's guest RAM changes every session, so nothing is hard-coded. The
engine copies the string tables and the working buffer into RAM essentially verbatim, so
`Memory/GameLocator.cs` finds them by **byte-signature scan** (via `BytePatternScanner` from
`GameTrainers.Common.Memory`):

- **Nation table anchor** — the high-bit-ASCII encoding of the first several `NAT.DAT` names
  (`BLODE`, `CAERGOTH`, …). Byte-identical to the shipped file and unique enough to confirm the game
  is loaded. This anchor is **required**: if it is not found the locator returns nothing and no
  strength block is exposed, so a coincidental signature match in an unrelated process can never
  produce writable rows pointing into arbitrary memory.
- **Strength block anchor** — scan for the constant qualities/base-number signature (§6), step back
  29 bytes, and read 29 candidate cells. A candidate is accepted only when **every byte is `≤ 240`
  and at least one byte is non-zero** (240 is the engine's highest base number; an all-zero run is
  never a live army). A single destroyed unit can legitimately read `0` mid-campaign, so — unlike an
  earlier draft that required all cells in `1..240` — a lone zero cell no longer defeats location.

Writes are single bytes, following the read-validate-write pattern so a shifted layout can never be
corrupted. A user-typed edit is clamped to `1..240`; a value observed in RAM keeps its real `0..240`
range so a destroyed unit (current strength `0`) is displayed — and, if frozen, re-written — as `0`
rather than being resurrected to `1`.

---

## 8. Constants summary (the `Game/` layer)

| Constant | Value | Source |
| --- | --- | --- |
| Container magic | `0xFD` | every shipped file |
| Header size | 7 bytes | container invariant |
| Text separator | `0xFF` | string tables |
| Nations | 28 | `NAT.DAT` |
| Sides | `HIGHLORD` / `WHITESTONE` / `NEUTRAL` | `WL2.DAT` |
| Unit types | 12 labels | `WL2.DAT` |
| Terrain types | 21 | `WL2.DAT` |
| Turns/year | 5 | `MENU.DAT` seasons |
| Game years | 6 | manual (Objective) |
| Total turns | 30 | 5 × 6 |
| `.UNT` slots | 400 × 4 bytes | `WL.UNT` / `SCEN.UNT` |
| Live-unit flag | `0x05` | `.UNT` slot +3 |
| Empty slot | `0xFF 0xFF` | `.UNT` slot X/Y |
| Strength cells | 29 leading | `WL.DAT` / `SCEN.DAT` |
| Max strength | 240 (Griffon base) | manual appendix |

---

## 9. How to re-verify

```powershell
.\Run.ps1 -Test -NoRun
# or
dotnet run --project test/FormatCheck
```

`FormatCheck` embeds verbatim base64 slices of `NAT.DAT`, `MENU.DAT`, `WL.UNT` and the head of
`WL.DAT`, and asserts every claim in §§2–6 above, exiting 0 (pass) or 1 (fail). To extend the RE (for
example to crack the deeper `WL.DAT` records), capture DOSBox-X memory dumps at known game states and
diff them the way the sibling Dragon Wars trainer's `.data/` notes describe.
