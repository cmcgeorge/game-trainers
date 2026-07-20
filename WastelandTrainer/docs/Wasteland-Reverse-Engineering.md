# Wasteland (1988) — Reverse-Engineering Notes

Live-memory layout of the 1988 Interplay / Electronic Arts post-apocalyptic RPG **Wasteland**,
as it runs under **DOSBox-X**. These notes back the `WastelandTrainer` WPF trainer: everything
below was recovered from captured DOSBox-X guest-RAM dumps and cross-checked against the game
manual, the community wiki, and the open-source `kayahr/wastelib` file-format project. Addresses
in the guest are never hard-coded — the trainer signature-scans for the party roster at runtime
(see *Locating the roster*) — but the **internal record layout** documented here is stable.

All multi-byte integers are **little-endian**. Offsets are relative to the start of a record
unless stated otherwise.

---

## 1. Source material

| Input | Use |
|-------|-----|
| `.data\dosbox-x-…-112525-175.bin` | Party standing inside the Ranger Center (baseline). |
| `.data\dosbox-x-…-113038-737.bin` | Same party just outside, in the desert (map change). |
| `.data\dosbox-x-…-113203-049.bin` | In the desert, in combat, end of a fight where Hell Razor fired one round. |
| `.data\dosbox-x-…-113306-646.bin` | Desert, two north and one west of the Ranger Center (movement delta). |
| `.game\manual.txt`, `.game\readme.txt`, `.game\paragraphs.txt` | Attributes, skills, controls, and the paragraph book. |
| wasteland.fandom.com, `kayahr/wastelib` | Item / skill ID cross-checks. |

The four dumps form a **differential set**: comparing the baseline against the in-combat and
post-movement dumps isolated which bytes are experience, ammunition, and party coordinates.
`memdump.md` records the ground-truth stats of the default party the dumps were taken with.

---

## 2. The party roster

The roster is an array of **7 contiguous fixed-size character records**, each **0x100 (256)
bytes**. Occupied members pack from slot 0; unused slots are zero-filled (their name byte is
`0x00`). The default party occupies four slots:

| Slot | Name | Notes |
|------|------|-------|
| 0 | Hell Razor | Male, US, MAXCON 28 |
| 1 | Angela Deth | Female, US, MAXCON 27 |
| 2 | Thrasher | Male, US, MAXCON 34 |
| 3 | Snake Vargas | Male, Mexican, MAXCON 31 |
| 4–6 | *(empty)* | Name byte `0x00` |

Directly **0x100 bytes before** roster slot 0 sits a 256-byte **party-state header** (party
position, marching order, and the current map name — see §5).

### Locating the roster

The record allocation moves every session, so the trainer finds it by **structure**, not by a
fixed address. It scans committed memory for a 7-record window where:

- occupied slots form a leading run (an occupied slot never follows an empty one);
- at least one slot is occupied;
- each occupied slot passes a strict validity test: a **2+-character** NUL-terminated
  printable-ASCII name starting with a letter, the seven attribute bytes each in `1..100`, a
  plausible MAXCON, current **CON not exceeding MAXCON**, a valid **gender** (`0`/`1`) and
  **nationality** (`0..4`).

The gender/nationality/CON-vs-MAXCON checks are what make the test specific: a lone letter followed
by unrelated numbers (which a name-plus-attributes-only test accepts as a one-member roster) is
rejected.

#### The stale-copy trap and the header discriminator

A member-count tiebreak ("keep the candidate with the **most** occupied members") is **not** safe on
its own. Wasteland keeps a **second image** of some character records in memory: every dump holds a
copy of `[Thrasher, Snake Vargas]` as two packed records **~18 KB before the live roster** (offset
delta `18481` in both the Ranger-Center and desert dumps), followed by an empty slot — a clean
2-member candidate. In the default-party dumps this is harmless: the live 4-member roster outvotes
it. But once the player deletes rangers down to a **1-member** live party, that lingering 2-member
stale copy wins the member-count vote, and the trainer locks onto the wrong records — showing deleted
characters and, because the party-state header is taken at `staleBase − 0x100`, a **wrong, frozen
live position**. (This was the reported bug: a fresh `CHRISTOPHER` party in Highpool displaying as
`Thrasher` + `Snake Vargas`.)

The discriminator is the **party-state header** (§5). The live roster is always preceded by a
plausible header — an in-range X/Y (`< 64`), a marching order of valid slot indices, and a printable
map name. The stale copy is **not**: the 256 bytes before it are the tail of another record
(confirmed: X reads `179`, the map-name field is inventory bytes). So a header-backed roster always
beats a headerless one, however many members the headerless one holds. See `PartyHeader.IsPlausible`.

#### The pre-made template and the address discriminator

Header validity alone is still not enough, because of a **second** decoy. The four factory rangers
(Hell Razor, Angela Deth, Thrasher, Snake Vargas) are a read-only **template** that Wasteland keeps
loaded at all times, **complete with its own valid party-state header** frozen at the Ranger Center
spawn (`X 55, Y 62`, map `"Ranger Ctr."`). Once the player has their own party, memory therefore holds
*two* header-backed rosters: the template, and the live/active party. A member-count tiebreak among
header-backed candidates picks the 4-member template over, say, a 1-member live party — reproducing the
same wrong-party/wrong-position bug even after a clean restart.

What separates them is **allocation order**: the game loads the template first and places the working
party at a fixed offset *above* it — a constant `+0x4A31` (18993 bytes) across independent live
sessions, and the active party sat above its decoy in every capture. So among header-backed candidates
the **highest base address** is the live party. Confirmed live: with the template at `…3D890` (4
members, `X 55 Y 62`) and the live party at `…422C1` (`CHRISTOPHER`, 1 member, `X 7 Y 1`), the
address rule picks `CHRISTOPHER`.

The full ranking (`PartyLocator.Outranks`, exercised in `FormatCheck` against the real captured
headers): **header-backed beats headerless; among header-backed, higher base address wins; among
headerless, more members wins.** This pins the live party without a static anchor.

> **Map name caveat.** The header's `0xD0` map-name field is *not* a reliable current-map label. The
> live party read `"Ranger Ctr."` while its position was `X 7 Y 1`, and no friendly town name (e.g.
> `"Highpool"`) appears anywhere in guest memory. Treat `0xD0` as a rough/last-set label, not ground
> truth; the trusted live signal is the X/Y pair.

---

## 3. Character record layout (0x100 bytes)

Confirmed by differential analysis of the dumps and cross-checked against `memdump.md`. Values
shown are Hell Razor's (roster slot 0) unless noted.

| Offset | Size | Field | Notes / evidence |
|-------:|:----:|-------|------------------|
| `0x00` | 14 | **Name** | Plain ASCII, NUL-terminated/padded. *(Not high-bit encoded like Dragon Wars.)* e.g. `48 65 6C 6C 20 52 61 7A 6F 72 00…` = "Hell Razor". |
| `0x0E` | 1 | **STR** (Strength) | 12 |
| `0x0F` | 1 | **IQ** (Intelligence) | 14 |
| `0x10` | 1 | **LCK** (Luck) | 13 |
| `0x11` | 1 | **SPD** (Speed) | 9 |
| `0x12` | 1 | **AGL** (Agility) | 14 |
| `0x13` | 1 | **DEX** (Dexterity) | 15 |
| `0x14` | 1 | **CHR** (Charisma) | 11 |
| `0x15` | 3 | **Money** | 24-bit LE. All test characters carried `$0`; treated as binary by the trainer. |
| `0x18` | 1 | **Gender** | `0` = Male, `1` = Female. Confirmed: Angela = 1, all others = 0. |
| `0x19` | 1 | **Nationality** | `0` US, `1` Russian, `2` Mexican, `3` Indian, `4` Chinese. Confirmed: Snake = 2 (Mexican). |
| `0x1A` | 1 | **Armor Class** | 0 for the unarmoured starting party. |
| `0x1B` | 2 | **MAXCON** (max constitution) | u16 LE. 28 / 27 / 34 / 31 across the party — matches `memdump.md`. |
| `0x1D` | 2 | **CON** (current constitution) | u16 LE. Equals MAXCON at full health. |
| `0x1F` | 1 | *weapon state* | Per-character weapon/equip byte (12/1/13/1 across the party); no consistent index mapping recovered. **Left untouched by the trainer.** |
| `0x20` | 1 | **SKP** (unspent skill points) | 1 for every starting character. |
| `0x21` | 3 | **Experience** | 24-bit LE, **binary**. Confirmed by the combat dump: rose from 0 to 14 after killing an iguana. |
| `0x24` | 1 | **Level** | 1 for every starting character. |
| `0x32` | ~10 | **Rank** | ASCII, NUL-terminated. `"Private"` for the starting party. |
| `0x80` | 60 | **Skills** | 30 slots × 2 bytes `(skillID, level)`, `0x00`-terminated. See §4. |
| `0xBD` | 60 | **Inventory** | 30 slots × 2 bytes `(itemID, ammo/qty)`. See §4. |

Bytes `0x25`–`0x31`, `0x3C`–`0x7F`, and `0xF9`–`0xFF` were zero in every capture and are not yet
identified.

---

## 4. Skills and inventory

Both lists are packed arrays of 2-byte entries, read until a `0x00` id terminator.

### Skills — `0x80`, 30 × `(id, level)`

The trainer edits skills by **id**: reading a skill's level scans the list for its id (0 if
absent); setting a non-zero level reuses the existing entry or appends a new one, and the whole
60-byte block is written back (read-validate-write).

Confirmed skill IDs (decoded from all four party members and matched to `memdump.md`):

| ID | Skill | ID | Skill | ID | Skill |
|---:|-------|---:|-------|---:|-------|
| 1 | Brawling | 13 | Acrobat | 25 | Medic |
| 2 | Climb | 14 | Gamble | 26 | Safecrack |
| 3 | Clip Pistol | 15 | Picklock | 27 | Cryptology |
| 4 | Knife Fight | 16 | Silent Move | 28 | Metallurgy |
| 5 | Pugilism | 17 | Combat Shooting | 29 | Helicopter Pilot |
| 6 | Rifle | 18 | Confidence | 30 | Electronics |
| 7 | Swim | 19 | Sleight of Hand | 31 | Toaster Repair |
| 8 | Knife Throw | 20 | Demolition | 32 | Doctor |
| 9 | Perception | 21 | Forgery | 33 | Clone Tech |
| 10 | Assault Rifle | 22 | Alarm Disarm | 34 | Energy Weapon |
| 11 | AT Weapon | 23 | Bureaucracy | 35 | Cyborg Tech |
| 12 | SMG | 24 | Bomb Disarm | | |

*Example (Hell Razor):* `01 02 02 01 03 01 07 01 09 02 0C 01 10 01 0D 01 08 01 00…` decodes to
Brawling 2, Climb 1, Clip Pistol 1, Swim 1, Perception 2, SMG 1, Silent Move 1, Acrobat 1,
Knife Throw 1 — exactly Hell Razor's manual sheet.

### Inventory — `0xBD`, 30 × `(id, ammo/qty)`

The second byte is ammunition (for weapons), a quantity/charge count (for consumables), or a
status byte; its **high bit marks a jammed weapon**.

*Example (Hell Razor):* `0D 07` = M1911A1 with 7 rounds (the ammo count dropped 7 → 6 in the
dump where he fired), followed by eight `1E 00` (.45 clips), `36 01` (Rope), `2C 00` (Canteen),
`2D 00` (Crowbar), `04 00` (Knife), `31 00` (Hand mirror), `34 28` (Matches). This matches his
`memdump.md` inventory item-for-item.

#### The full item-name table (decoded from WL.EXE)

The item **ids** in the record index the game's master item table. `WL.EXE` is **EXEPACK-compressed**
and stores its text as **5-bit dictionary-coded** strings (skill names, item names, UI text, etc.),
so none of the names appear as plain bytes in the file. Unpacking the executable and decoding the
"inventory" string group (at unpacked offset `SEG2 + 0xB270`, `SEG2 = 0xD020`) recovers them: the
first 35 strings are the skill names and **item name `N` sits at string index `N + 36`**. The
decoder and offsets are ported from the open-source `kayahr/wastelib` project.

This was cross-checked against the ten ids decoded from the four memory dumps — Knife 4, M1911A1 13,
VP91Z 16, .45 clip 30, 9mm clip 32, Canteen 44, Crowbar 45, Hand mirror 49, Matches 52, Rope 54 —
and **all ten line up at `index = id + 36`**, confirming the mapping. Item names are the game's own
singular forms (e.g. it stores `45 clip`, not `.45 clip`). Ids **1..94** are real (70..72 unused);
the trainer bakes the decoded table into `Game/ItemCatalog.cs`. A one-off extractor that reproduces
this decode is kept out of the shipped app.

---

## 5. Party-state header and teleport

A 256-byte header sits at **`rosterBase − 0x100`**. Relevant fields (offsets relative to the
header start):

| Header offset | Absolute (from roster) | Field | Evidence |
|--------------:|------------------------|-------|----------|
| `0x00` | `rosterBase − 0x100` | Marching order | `00 01 02 03 04 00 00 00` — slot indices in walking order. |
| `0x08` | `rosterBase − 0xF8` | **Party X** | 55 inside the Ranger Center. |
| `0x09` | `rosterBase − 0xF7` | **Party Y** | 62 inside the Ranger Center. |
| `0x0B` | `rosterBase − 0xF5` | Home X | 55 — the Ranger Center return coordinate. |
| `0x0C` | `rosterBase − 0xF4` | Home Y | 62. |
| `0xD0` | `rosterBase − 0x30` | **Map name** | 12-byte ASCII, e.g. `"Ranger Ctr. "`, `"   Animal   "`. |

**Movement proof:** walking two north and one west changed X `55 → 54` (1 west) and Y `62 → 60`
(2 north), confirming X = column, Y = row with north = decreasing Y. This proves the header **tracks**
movement — it does **not** prove the game reads it back.

### Teleport is not possible by writing memory (live-RE result)

An early version of the trainer wrote these two bytes to "teleport." **It never worked**, and a live
investigation with the DOSBox-X **AI debug server** (`C:\Solutions\Personal\DosBoxXModified`, TCP
`127.0.0.1:2999`) established why, at the instruction level:

- The party-state header is a **write-only shadow**. The game copies the party's position *into* it
  on every step (which is why the bytes above "track" movement and why the live-position readout
  works) but **never reads it back** to place the party. Writing X/Y there does nothing — confirmed on
  the overworld *and* inside a town, from a clean baseline (stepped one tile, moved exactly one tile;
  the write was overwritten by the game's own read).
- The on-map position is **virtualized**, with no single writable variable:
  - the movement handler reads the position at `0824:649E` (`mov dl,[464E]` / `mov bl,[464F]`), applies
    the step, and re-derives everything;
  - `[464E]/[464F]` (DS `1506`) is the **viewport origin**, from which the party's world position is
    computed as *viewport + screen-cell* (party drawn ~centered, map scrolls). The viewport is itself
    kept in sync with the world position, so writing it just recenters/reverts;
  - the sprite position is spilled to `DS:BA0F/BA10` (and reloaded around calls), plus copies at
    `DS:AA4A/AA4B`, `DS:4716/17`, and several `(x,y)` shadow pairs found by search — **writing any of
    them does not move the party** (all are re-synced from the true position each step);
  - the map repaints **incrementally** (per-step scroll, e.g. `0824:65FB inc byte [464F]`), so even
    changing a position value leaves the visible map stale.
- The only thing that sets position *and* repaints correctly is the game's own **map-load/placement
  routine** (what runs when you enter/exit a location). Driving that — set destination + invoke the
  placement + full redraw — is the sole viable teleport, and it requires executing game code, which a
  host-memory trainer (the WPF app) cannot do. It would be a debug-server / code-patch feature.

**Conclusion:** the trainer's Maps tab **reads** the live X/Y (a reliable "where am I") and offers the
Areas reference, but does **not** write position. Teleport was removed.

---

## 6. Trainer implications

- **Safe to edit:** name, the seven attributes, money, MAXCON/CON, SKP, experience, level,
  gender, nationality, armor class, the skill list, and the inventory list.
- **Read-validate-write:** every write re-reads/mutates the in-memory record and pushes only the
  changed bytes back, so a shifted or partially-loaded layout is never corrupted.
- **Party X/Y are read-only:** the Maps tab displays the live position but does not write it — the
  header is a write-only shadow the game never reads back (see §5), so there is no teleport.
- The `0x1F` weapon byte and the unidentified padding regions are deliberately left alone.
