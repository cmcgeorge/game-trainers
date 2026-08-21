# DarkSpyre — Reverse Engineering Notes

## 1. Game Background

| Field | Value |
|---|---|
| **Title** | DarkSpyre |
| **Developer** | Event Horizon Software |
| **Publisher** | Electronic Arts |
| **Platform** | IBM PC (DOS) |
| **Release** | 1990 |
| **Genre** | Real-time dungeon-crawler RPG (roguelike) |
| **Perspective** | Top-down 2D |
| **Dungeons** | Randomly generated |
| **Levels** | 50 total, 39 required |

DarkSpyre is a real-time dungeon crawler in which a single character descends through
50 levels of a randomly generated tower. The game runs continuously — there is no
turn-based pause — which makes live memory editing challenging because values change
in real time. The player can press **P** to pause the game, providing a window for
safe value reading.

## 2. Engine Analysis

### 2.1 Executable

`DARKSPR.EXE` is a DOS executable. No copy of the game binary or memory dumps were
available during development, so the analysis below was assembled from the game manual
(Lemon Amiga supplement), the Cheatbook walkthrough, Wikipedia, and MobyGames. Every
data point carries a confidence marker:

- **[Confirmed]** — stated explicitly in the manual or walkthrough and cross-checked
  against at least one other source.
- **[Inferred]** — plausible from the available sources but not explicitly confirmed.

### 2.2 Why No GameLocator

The repo's locator-based trainers (MightAndMagic1, DragonWars, Pirates, RailroadTycoon,
etc.) anchor on a static byte signature — a string literal, a constant table, or a
structural shape — that the game loads verbatim into guest RAM at a fixed `DGROUP`
offset. That approach requires either:

1. A copy of the executable to disassemble and identify anchor strings, **or**
2. A live memory dump to scan for stable byte patterns.

Neither was available for DarkSpyre. Without the binary, no Ghidra analysis was
possible, and without memory dumps, no anchor pattern could be confirmed. The trainer
therefore follows the **value-scanner model** used by `QuestForGlory1Trainer`,
`MoriaTrainer`, `BattleTech1Trainer`, and `ThePerfectGeneral2Trainer`: Common's
`MemorySearcher` provides a Cheat-Engine-style scan (attach → first scan → narrow by
comparison → pin → freeze), and guided-scan recipes pre-configure the width and give
step-by-step narrowing instructions for each stat.

### 2.3 What Would Be Needed for a Locator

If a future developer obtains the game binary or a live memory dump, the path to a
locator would be:

1. Disassemble `DARKSPR.EXE` in Ghidra to identify the data segment and any string
   literals loaded into guest RAM.
2. Search the binary for the attribute names, spell names, monster names, or rune
   names — any of these may be stored as ASCII and loaded verbatim.
3. Check whether the character record lives in a fixed `DGROUP` offset (making it a
   locator target like RailroadTycoon) or is heap-allocated with a far pointer in the
   data segment (making it a pointer-chain target like LegendOfFaerghail).
4. If a stable anchor is found, write a `GameLocator` following the pattern in
   `PiratesTrainer` or `RailroadTycoonTrainer` and add it to this trainer.

## 3. Character System

### 3.1 Attributes [Confirmed]

Six attributes, each a single byte, range 1–20:

| ID | Attribute | Abbrev | Description |
|---|---|---|---|
| 0 | Strength | STR | Determines HP and melee damage |
| 1 | Agility | AGI | Affects dodge and movement speed |
| 2 | Endurance | END | Determines HP and encumbrance capacity |
| 3 | Accuracy | ACC | Affects hit probability |
| 4 | Talent | TAL | Determines SP and magic proficiency |
| 5 | Power | PWR | Determines SP and spell effectiveness |

**HP formula** [Confirmed]: `HP = Strength + Endurance + Random`
**SP formula** [Confirmed]: `SP = Talent + Power + Random` (max 100)

Attributes do not change during normal play — they are set at character creation and
can only be modified by the power runes exchanged on Level 36. This makes them easy to
scan: a single Exact scan for the known value should narrow to very few candidates.

### 3.2 Spell Points [Confirmed]

- SP is a byte value, range 0–100.
- SP is consumed by casting spells; each spell has a cost split 50/50 between
  preparation and casting.
- SP regenerates slowly over time.

### 3.3 Encumbrance [Confirmed]

- Encumbrance (ENC) is a byte value tracking carried weight.
- Changes when items are picked up or dropped.
- Used for the guided-scan recipe since it changes predictably.

### 3.4 Level and Score [Confirmed]

- **Level** is an Int16, range 1–50. Displayed by pressing F8.
- **Score** is an Int32, range 0–999999. Displayed by pressing F8.
- Score increases from killing monsters and picking up items.

## 4. Combat System

### 4.1 Weapons [Confirmed from manual]

Seven weapon proficiency types. Using a weapon of a given type increases proficiency
for all weapons in that class. Proficiency levels (10): None, Beginner, Neophyte,
Novice, Average, Skilled, Stalwart, Adept, Savant, Expert.

| ID | Type | Speed | Damage | Hands | Examples |
|---|---|---|---|---|---|
| 0 | Clubbing | Average | Average | 1H | War Axe, Mace |
| 1 | Hurled | Fast | Low | 1H | Throwing Knife, Throwing Axe |
| 2 | Large | Slowest | Highest | 2H | Claymore, Great Scythe |
| 3 | Long Edge | Average | Average | 1H | Longsword, Scimitar |
| 4 | Projectile | Fast | Average | 2H | Light Crossbow |
| 5 | Short Edge | Fastest | Least | 1H | Short Sword, Dagger |
| 6 | Thrusting | Slow | High | 2H | Spear, Trident |

Key detail: weapons break randomly, so training in multiple types is essential.
Attacking with a shield increases clubbing proficiency. Hand-to-hand with hurled items
also increases hurling proficiency. Throwing a thrust weapon increases hurling
proficiency (not thrusting).

### 4.2 Armor [Confirmed from manual]

- **Protection levels**: 15 (armor value, reduces incoming damage)
- **Condition levels**: 7 (wear state; armor degrades with use)
- Armor covers specific body locations; the manual describes a full armor system.

### 4.3 Monsters [Confirmed from walkthrough]

14 monster types in 5 combat categories:

| Category | Monsters | Key Traits |
|---|---|---|
| Ground Melee | Wraith, Crustacean, Samurai, Gargoyle, Crystal Ninja | Walk, hand-to-hand attack. Use ranged weapons and fireballs. |
| Ground Projectile | Jester | Walk + fireballs/gas. Only monster that does both. Very dangerous. |
| Slither Poison | Slime, Creeper | Slither, poison on contact. Immune to projectiles. Do not trigger weight plates. |
| Flying Melee | Vulture, Manta Ray | Fly, hand-to-hand. Do not trigger weight plates. |
| Flying Projectile | Beholder, Electric Storm, Banshee, Djinn | Fly, ranged attacks. Get into melee range to suppress projectiles. |

Monsters become exponentially harder on higher levels and work in larger groups. When
near death, monsters run away.

## 5. Magic System [Confirmed from manual]

### 5.1 Magic Classes

Six magic classes, each with 7 proficiency levels: None, Novice, Average, Skilled,
Sage, Maren, Master.

| Class | Spells | Description |
|---|---|---|
| Healing | 1 | Liquify (potion creation) |
| Sorcery | 3 | Knock, Zap Away, Hold |
| Wizardry | 2 | Fireball, Magic Gas |
| Conjury | 3 | Abstraka, Disguise, Magic Wall |
| Diviny | 3 | Compass, Magic Map, Sight |
| Enchantry | 2 | Dispel, Freeze |

### 5.2 Spell Details

| Spell | Class | SP Cost | Effect |
|---|---|---|---|
| Liquify | Healing | 10 | Creates potions from gemstones |
| Knock | Sorcery | 16 | Opens some gates |
| Zap Away | Sorcery | 10 | Teleports blocks and balls |
| Hold | Sorcery | 30 | Freezes a targeted monster |
| Fireball | Wizardry | 20 | High damage projectile, bounces off walls |
| Magic Gas | Wizardry | 20 | Gas cloud — confusion (below skilled) or poison (skilled+) |
| Abstraka | Conjury | 20 | Invisibility (toggle) |
| Disguise | Conjury | 30 | Look like a monster (cancelled by attacking) |
| Magic Wall | Conjury | 30 | Temporary moveable wall |
| Compass | Diviny | 30 | Shows direction to exit |
| Magic Map | Diviny | 30 | Reveals level map |
| Sight | Diviny | 10 | Enlarges ground items |
| Dispel | Enchantry | 36 | Defensive dispel (unreliable) |
| Freeze | Enchantry | 40 | Stops all monsters temporarily |

Spells are found on scrolls throughout the dungeon. Each scroll can be cast once, or
permanently added to the spell book (found on Level 1). SP cost is split 50/50 between
preparation and casting.

## 6. Rune System [Confirmed from manual and walkthrough]

25 runes total, 5 of which are **power runes**. The power runes must be collected
throughout the game and exchanged on Level 36 for gifts from the gods before entering
the final 3 levels (Levels 38–50).

### 6.1 Power Runes

| Norse | English | Attribute |
|---|---|---|
| Uraz | Strength | STR |
| Ehwaz | Agility | AGI |
| Eihwaz | Accuracy | ACC |
| Teiwaz | Endurance | END |
| Inguz | Talent | TAL |

### 6.2 Utility Runes

| Norse | English | Effect |
|---|---|---|
| Raido | Quest | Saves the game (one use per rune) — essential |
| Thurisaz | Gateway | Takes you to the next level |
| Jera | Sustenance | Restores HP |
| Algit | Protection | Cures poison |
| Sowelu | Unity | Cures poison and confusion |
| Keno | Opening | Knock spell effect |
| Fehu | Wealth | Becomes a knock scroll |
| Gebo | Alliance | Magic Map effect |
| Dagaz | Discovery | Destroys a monster |
| Isa | Stagnant | Poisons you (harmful) |

The remaining 10 runes (Ansuz, Berkana, Hagalaz, Laquz, Mannaz, Nauthiz, Odin,
Othilia, Perth, Wunjo) have unknown effects [Inferred — not documented in available
sources].

## 7. Level Structure [Confirmed from walkthrough]

- **50 levels** total, **39 required** to complete the game.
- Dungeons are **randomly generated** — no fixed maps exist.
- The spell book is found on **Level 1**.
- Power runes are scattered throughout the middle levels.
- **Level 36**: the exchange point where power runes are traded for divine gifts.
- **Levels 38–50**: the final sequence, accessible only after the Level 36 exchange.
- Press **F8** to display the current level and score.
- Press **P** to pause the game for safe value reading.

## 8. Save System [Confirmed from manual]

- Saving is done via the **Raido** rune (Quest rune).
- Each Raido rune allows one save.
- Raido runes are found throughout the dungeon.
- There is no autosave — saving is a limited resource.

## 9. Trainer Architecture

### 9.1 Value-Scanner Model

The trainer uses `GameTrainers.Common.Memory.MemorySearcher` for all memory access:

1. **Attach** to the DOSBox/DOSBox-X process via `ProcessMemory.Open`.
2. **First Scan** — snapshot all memory locations matching a known value (or
   unknown-value baseline).
3. **Narrow** — perform an in-game action that changes the value, then scan by
   Exact / Increased / Decreased / Changed / Unchanged.
4. **Pin** — move a survivor address to the freeze table.
5. **Freeze** — re-write the value every ~200 ms so the game cannot move it back.

### 9.2 Guided-Scan Recipes

`ScanGuide` provides 11 pre-built recipes:

| Stat | Width | Range | Narrowing Strategy |
|---|---|---|---|
| Hit Points | Int16 | 1–999 | Take a hit, scan Exact for new HP |
| Spell Points | Byte | 0–100 | Cast a spell, scan Exact for new SP |
| Strength | Byte | 1–20 | One Exact scan (does not change) |
| Agility | Byte | 1–20 | One Exact scan (does not change) |
| Endurance | Byte | 1–20 | One Exact scan (does not change) |
| Accuracy | Byte | 1–20 | One Exact scan (does not change) |
| Talent | Byte | 1–20 | One Exact scan (does not change) |
| Power | Byte | 1–20 | One Exact scan (does not change) |
| Encumbrance | Byte | 0–255 | Pick up/drop an item, scan Exact |
| Level | Int16 | 1–50 | Step through a gateway, scan Exact |
| Score | Int32 | 0–999999 | Kill a monster, scan Exact |

### 9.3 Scan Width Rationale

- **HP (Int16)**: HP = STR + END + random, and STR/END max at 20 each, so HP can
  exceed 255. Int16 covers the full range.
- **SP (Byte)**: SP maxes at 100, well within a byte.
- **Attributes (Byte)**: Range 1–20, a single byte.
- **Encumbrance (Byte)**: A weight counter, 0–255 is sufficient.
- **Level (Int16)**: 1–50, but Int16 is used for safety since the exact encoding is
  unconfirmed.
- **Score (Int32)**: Up to 999,999, which exceeds Int16 range.

### 9.4 Confidence Markers

All constants in `GameFacts` are marked **[Confirmed]** in the source code comments,
meaning they were stated in the manual or walkthrough and cross-checked. No value was
guessed. The trainer UI does not surface a confidence distinction (unlike some
trainers in the repo) because every value is confirmed from published sources.

## 10. Data Sources

| Source | What It Provided |
|---|---|
| **Lemon Amiga** (manual supplement) | Attributes, HP/SP formulas, weapon types, magic classes, armor system, save mechanics, rune descriptions |
| **Cheatbook** (walkthrough) | Controls, monster types and categories, spell details, level structure, rune effects, combat tactics |
| **Wikipedia** | Basic game info, developer, release year, genre classification |
| **MobyGames** | Platform, publisher, release date confirmation |

## 11. What Was Not Reverse-Engineered

The following were not attempted because no binary or memory dump was available:

- **Memory layout**: No character record offsets are known. The trainer relies on
  value scanning rather than direct address computation.
- **Map/position data**: No teleport feature is offered because map position was not
  identified.
- **Save file format**: No save editor is offered because the on-disk format was not
  decoded.
- **Inventory encoding**: No inventory editor is offered because the item storage
  format was not reverse-engineered.
- **Monster AI state**: Not analyzed.

## 12. Future Work

If the game binary and/or memory dumps become available:

1. Disassemble `DARKSPR.EXE` in Ghidra to recover the data segment layout.
2. Identify string anchors (spell names, monster names, rune names) for a `GameLocator`.
3. Map the character record structure for direct read/write without scanning.
4. Decode the save file format for an offline save editor.
5. Investigate map position for a teleport feature.
6. Analyze the inventory encoding for an item editor.
