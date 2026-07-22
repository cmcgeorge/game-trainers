# Imperialism II — reverse-engineering notes

Working notes behind the trainer's offsets and design decisions. Imperialism II is the odd one out in
this repo: a **native 32-bit Windows** game (Frog City / SSI, GOG re-release), not a DOS program under
DOSBox. That changes the mechanics (attach straight to `Imperialism II.exe`, no guest-address maths)
and hands us a rare gift — a shipped **linker map** — but also a trap that ruled out a one-click
locator. This file records what the map gave us, why the map's addresses don't work directly, and how
the trainer is anchored instead.

## The target

| | |
|---|---|
| Executable | `D:\Games\Imperialism II\Imperialism II.exe` (GOG, ~4 MB) |
| Toolchain | Microsoft Visual C++ + **MFC** (the map is full of `AFX_*` sections, `CObject`, `CRuntimeClass`) |
| Preferred load address | `0x00400000`, no ASLR (1999 binary) — statics *would* be at fixed addresses |
| Process name | `Imperialism II` (so `ProcessName.Contains("imperialism")` selects it) |
| Save file | `Save\slotA.imp`, magic `IBMA`, then a label (`- Autosave -`) and the map tile stream |

## The gift: `Imperialism II.map`

The game ships its own linker map (`Imperialism II.map`, 15,915 lines, "Publics by Value"). It names the
entire C++ data model in mangled form. The money model falls straight out of it:

- `?GetAvailableTreasury@TGreatPower@@QAEJXZ` — returns `J` = **`long` (signed int32)**. Treasury is a
  32-bit value on a `TGreatPower` (a playable "great power"), which derives from `TCountry`.
- `?AddToTreasury@TCountry@@UAEXJ@Z` — mutates the same `long`.
- `?GetStockpile@TCountry@@UAEFF@Z` — `GetStockpile(short commodity)` returns `F` = **`short` (int16)**.
  Warehouse resources are 16-bit, indexed by a commodity number.
- `?GetStockpile@TGreatPower@@UAEFF@Z`, `GetSpendingLimit`, `GetBottomLine`, `GetTotalLiquidatedRiches` …

Global singletons are named too (segment `0003:` = `.data`/`.bss`), e.g. `gSimMgr`, `gCountries`
(`TCountry**`), `gGreatPowers` (`TGreatPower**`), `gPlayerCountry` (`short` index of the human power),
`gCheatingIsEnabled`. In principle: `player = gCountries[gPlayerCountry]`, then treasury is a `long`
field on that object → a clean, structural, no-scan locator.

### The game even carries its own cheats

From `UCheaters.obj` / `USimMgr.obj` / `WAssetMgr.obj`:

- `settreasury <n>` — console command, strings `"players treasury set"`, `"settreasury unavailable …"`.
- `TAssetMgr::ShowMeTheMoney`, `TSimMgr::SetMinorMagicMoney`.
- A "Nova Console" scripting engine with `ScSetTreasury`, `ScSetWarehouse`, `ScSetLabor`,
  `ScSetCapacity`, `ScAddTech`, … and a `gCheatingIsEnabled` gate.

The trainer does **not** invoke these (that would mean calling into the process); they're documented in
the References tab because they confirm the data model (treasury set by value; warehouses/labour by
script).

## The trap: the map is a different build than the exe

The elegant locator dies on one fact: **the shipped exe is a later build than the map.**

| | PE timestamp |
|---|---|
| `Imperialism II.map` (header) | `0x36AE3822` → 1999-01-26 |
| `Imperialism II.exe` (PE header) | `0x3757FA06` → **1999-06-04** (the v1.03 patch; note `Readme103.txt`) |

Reading the map's global addresses out of the **live** June process returns garbage — the January
layout no longer holds:

```
gSimMgr    @0x0074A538 (Jan)  ->  0x696E6469  ("indi…", a string, not a TSimMgr*)
gCountries @0x00750360 (Jan)  ->  0x00000004  (not a pointer)
```

Both **code and data** shifted, by non-constant amounts (confirmed by dumping accessor functions at
their mapped file offsets — some land mid-function, some behind `CC` padding), so a single relocation
delta can't rescue it. And RTTI is stripped for the game's own classes — only MFC's `CObject` keeps a
type descriptor (`grep` for `.?AVTGreatPower@@` in the exe → 0 hits), so a vtable/RTTI object scan is
out too. There is **no stable static anchor** derivable from the shipped files alone.

### Consequence for the design

That puts Imperialism II in the same bucket as the repo's value-scanner trainers (Colonization,
Darklands, Perfect General 2): **no `GameLocator`.** The reliable primitive is a Cheat-Engine-style
value scan via `GameTrainers.Common.MemorySearcher` — attach, snapshot, narrow by what an on-screen
number does, pin + freeze. It works regardless of the build drift. The map's remaining value is
**knowledge**: it tells the guided scans which width to use (treasury = Int32, stockpile/labour =
Int16) and what each value means, so the user isn't scanning blind.

The scanner path was validated live against the running June build (PID observed at capture time):
`ProcessMemory.Open` → 717 committed regions / ~171 MiB → `FirstScanExact` + `NextScan` complete in
well under a second. Confirmed native-process attach + scan + narrow all work through the generic
`GameTrainers.Common` memory layer (no DOSBox assumptions).

## The commodity table (and a trap)

There *is* an internal 30-byte-per-entry name table in `Imperialism II.exe` (`Cotton` at file offset
`0x34F580`, each entry +0x1E): `Cotton, Wool, Timber, Coal, Iron, Horses, Oil, Food, Fabric, Lumber,
Paper, Steel, Fuel, Clothing, Furniture, Hardware, Arms, grain, produce, fish, livestock, gems, gold`.
An early version of `CommodityBook` used it — **and it was wrong.** That is the *industrial* commodity
set (Steel/Coal/Oil/Clothing/Hardware/Arms). Age of Exploration actually plays with the pre-industrial
set — **Cast Iron** and **Bronze (Tin + Copper)** instead of Steel/Oil — and those display names are
not stored as plain text anywhere in the game files (they're baked into the trade-screen art). A live
warehouse read exposed the discrepancy (see below), and the game manual's "Possible Commodities to
Transport" is the authority. The corrected book in
[`CommodityBook`](../src/ImperialismIITrainer/Game/CommodityBook.cs) holds the 28 AoE commodities:
8 raw (Wool, Cotton, Timber, Coal, Iron Ore, Tin, Copper, Horses) → 6 refined (Fabric, Lumber, Paper,
Cast Iron, Bronze, Steel), 3 food (Grain, Cattle, Fish), 6 luxuries (Sugar Cane/Tobacco/Furs and their
Refined Sugar/Cigars/Fur Hats), 5 riches (Spices, Silver, Gold, Gems, Diamonds). `GetStockpile(short)`
takes a commodity index; the trainer treats the catalogue as **labels**, not indices — the user reads a
real warehouse amount off-screen and scans for it.

## Live RE: treasury and warehouse located

Read against a running game (human power holding $50,000; warehouse Wool 10 / Timber 25 / Tin 12 /
Copper 12 / Iron Ore 40 / Fabric 4 / Lumber 20 / Paper 10 / Bronze 8 / Cast Iron 15):

- **Treasury** — `int32 == 50000` had exactly **two** writable-memory matches; one was 4-byte aligned
  inside a heap allocation, the other an unaligned coincidence. A write of `500000` to the aligned one
  round-tripped and changed the in-game cash → confirmed the player's treasury field.
- **Warehouse** — searching writable memory for a 32-slot `int16` window containing the full resource
  multiset `{4,8,10×2,12×2,15,20,25,40}` found it **in the same heap allocation** (~0xCA4 bytes after
  the treasury). It's a contiguous `int16` array, **raws then refined**, matching the user's two groups
  in order: `[Wool, Timber, Tin, Copper, Iron Ore, …zeros…, Fabric, Lumber, Paper, Bronze, Cast Iron]`.

So the human power's `TCountry`/`TGreatPower` object holds both the treasury (`long`) and the warehouse
(`int16[]`) a few KB apart — exactly what the January map implies structurally, just at a June-build
heap address that changes each session. These absolute addresses are **per-session** and are not baked
into the trainer; they're what the value scanner rediscovers in seconds.

## The one-click locator (built)

The pointer scan closed the loop. Searching all writable memory for dwords that point into the player
object turned up **two static globals in the fixed `.data` section** — `0x760650` and `0x7606A8` —
both holding a live pointer to the nation object (base `0xE9231B0`):

```
[0x760650] -> 0xE9231B0     (static .data → nation object)
[0x7606A8] -> 0xE9231B0
object +0x000 = 0x6FDAC8    (vtable, in .rdata)
object +0x130 = treasury (int32)
object +0xDD4 = warehouse (int16[] — Wool, Timber, Tin, Copper, Iron Ore, … then the refined block)
```

Because the exe has a fixed image base and **no ASLR**, `0x760650` is the same address every launch; the
field offsets are compile-time constants. So the locator chain `*( *(0x760650) + 0x130 )` resolves the
treasury with no scanning, and `[GameLocator](../src/ImperialismIITrainer/Game/GameLocator.cs)` +
[`NationLayout`](../src/ImperialismIITrainer/Game/NationLayout.cs) implement exactly that: read the
static global (or, as a fallback, scan the small `.data` range for any pointer to a valid nation object),
validate the target (vtable in `.rdata` + plausible treasury + non-negative warehouse), and expose the
addresses. Verified end-to-end against the running game — the locator resolved the treasury and all ten
warehouse slots to the correct addresses and values. If a different build fails validation, the trainer
falls back to the build-independent value scanner.

**Still open:** the full commodity→offset map (only the ten goods a live game held are labelled; the
`+0xDD0` pair `12720/3730` and the `+0xDFC` trio `99/69/30` are unmapped, likely food/riches/luxuries),
and cross-*session* confirmation (the offsets are stable in theory — no ASLR, compile-time fields — but
weren't re-checked across a game restart; the runtime validation guards against a mismatch).

## A future upgrade path (not done)

A genuine one-click locator is still possible, it just needs an interactive step this trainer doesn't
take: (1) value-scan the treasury once, (2) pointer-scan the `.data`/`.bss` range (~`0x744000`–`0x76B000`
in the June build) for a pointer to that object to recover the June `gCountries`/`gGreatPowers` and the
player index, (3) read the treasury field offset within the object. Bake those three constants in and
the trainer could locate treasury with no scan. Left as a TODO because it requires driving the live
game; the value scanner is complete and build-independent without it.
