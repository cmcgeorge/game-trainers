# Knights of Legend (1989) — Reverse-Engineering Notes

Working notes on the internals of **Knights of Legend** (Origin Systems, 1989, by Todd
Porter), as it runs under **DOSBox / DOSBox-X**. These notes back the `KnightsOfLegendTrainer`
WPF trainer.

Unlike most trainers in this repository, **no game binary, memory dump, or Ghidra analysis
was available** when these notes were compiled. Every fact below comes from the game manual,
the official cluebook, and community resources. Accordingly, every claim carries a confidence
marker:

| Tag | Meaning |
|-----|---------|
| **[Manual]** | Stated in the game manual or the official Origin Systems documentation. |
| **[Web]** | Corroborated against community resources (MobyGames, Wikipedia, GameFAQs, fan sites). |
| **[Inferred]** | Not stated directly, but strongly implied by confirmed facts and the game's own rules. Not byte-verified. |
| **[Open]** | Noticed but not settled. Called out as such rather than guessed at. |

Because no binary analysis was possible, the trainer follows the **value-scanner model**
(like `DarklandsTrainer`, `MoriaTrainer`, `DarksypreTrainer`): it has **no `GameLocator`** —
just `GameTrainers.Common.Memory.MemorySearcher` with guided scans. Every live-memory offset
is discovered per-session by scanning, never hard-coded.

---

## 1. Game background

| Field | Value |
|-------|-------|
| **Title** | Knights of Legend |
| **Developer** | Origin Systems, Inc. |
| **Designer** | Todd Porter |
| **Publisher** | Origin Systems / Broderbund |
| **Platform** | IBM PC (DOS) |
| **Release** | 1989 |
| **Genre** | Party-based tactical RPG |
| **Perspective** | Top-down 2D combat; menu-driven interaction |
| **Setting** | Ashtalarea, a duchy of the kingdom of Sondar |

**[Manual][Web]** Knights of Legend is a party-based RPG supporting up to six active
characters. The player creates characters at the character-generation guild, forms a party,
and undertakes a series of 24 quests across the duchy of Ashtalarea. The plot centres on the
evil sorcerer **Pildar**, who has captured **Duke Fuquan** and the knight **Seggallion**. All
24 quests must be completed before the final rescue of Seggallion becomes available.

The game was planned as the first in a series, but no expansions were ever released.
**[Web]**

### 1.1 Running the game

The game is installed into a directory (typically `knights`) and launched with the `kol`
command. **[Manual]** Under DOSBox, this means mounting the game directory and running
`kol` from the `knights` subdirectory.

Up to **16 characters** can be stored on a single character disk. **[Manual]** Characters
are not locked into a party — any combination of up to six stored characters can be assembled
into a party at the inn.

### 1.2 Currency and experience

| Unit | Notes |
|------|-------|
| **Gold Crowns (GC)** | The single currency. No sub-denominations. **[Manual]** |
| **Adventure Points** | Experience points, earned in combat and quest completion. **[Manual]** |

There is **no mechanism to transfer gold between characters** — only items can be traded.
**[Manual]** This is a deliberate design choice: each character must earn and manage their own
wealth.

### 1.3 Training and advancement

| Activity | Cost | Notes |
|----------|------|-------|
| Training session | ~200 GC per session | Raises skill levels. **[Manual]** |
| Training requirement | 100 adventure points per skill level | **[Manual]** |
| Skill points per level | 20 × level | Where Peasant = level 1. **[Manual]** |

Arena fights are required for **rank promotion** — a character must prove themselves in the
arena to advance to higher social and military ranks, which unlock better equipment and
training opportunities. **[Manual]**

---

## 2. Character statistics

### 2.1 Primary statistics

Seven primary statistics, all on a **0–100** scale: **[Manual]**

| Stat | Description |
|------|-------------|
| **Strength** | Physical power; affects melee damage, carrying capacity, and certain weapon requirements. |
| **Quickness** | Speed and reaction time; affects initiative, action speed, and dodging. |
| **Size** | Physical stature; affects hit points, reach, and which weapons can be wielded. |
| **Health** | Constitution and vitality; affects body points, healing rate, and resistance to disease/poison. |
| **Foresight** | Perceptual acuity and intuition; lets a character see opponents' planned actions **before** executing their own. **[Manual]** This is Knights of Legend's signature mechanic — the character with the highest Foresight acts with full knowledge of enemy intentions. |
| **Charisma** | Social aptitude; affects interactions with NPCs, prices, and quest availability. |
| **Intellect** | Mental acuity; affects magic proficiency, learning speed, and certain skill maximums. |

### 2.2 Secondary statistics

| Stat | Notes |
|------|-------|
| **Health (body points)** | Derived from primary Health and Size; the character's hit-point pool. When this reaches zero, the character falls unconscious or dies. **[Manual]** |
| **Balance** | Affects the ability to remain standing during combat — taking hits, dodging, and moving on difficult terrain all test balance. A character with poor balance is easier to knock down. **[Manual]** |
| **Endurance** | The fatigue pool. Every action in combat costs energy; when endurance is depleted, the character **passes out from exhaustion**. **[Manual]** This is the game's fatigue system — even a fully healthy character can collapse if they overexert. |

### 2.3 The fatigue system

Every combat action — attacking, defending, moving — costs **energy** drawn from the
endurance pool. **[Manual]** The costs scale with the intensity of the action:

- Light actions (Walk, Stand) cost little.
- Heavy actions (Berserk attack, Sprint, Run) cost substantially more.
- When endurance is exhausted, the character **passes out**, becoming helpless until they
  recover enough energy to act again.

This means managing endurance is as important as managing body points. A character can be
perfectly healthy but rendered useless by fatigue — which is what makes the **Foresight**
stat so valuable: knowing what the enemy will do lets a character choose the most efficient
response rather than over-committing.

---

## 3. Races and classes

### 3.1 Races

Four playable races: **[Manual]**

| Race | Notes |
|------|-------|
| **Humans** | The most versatile race. Access to the largest class selection (12 male, 4 female). No special racial abilities. |
| **Elves** | Graceful and intelligent. Access to 6 elven classes. High Foresight and Intellect. **[Inferred]** |
| **Dwarves** | Sturdy and strong. Access to 8 dwarven male classes. High Strength and Health. **[Inferred]** |
| **Kelden** | Winged humanoids who can **fly**. Access to 3 Kelden male classes. The ability to fly grants unique tactical mobility in combat (Fly, Fly Faster, Zoom movement options). **[Manual]** |

### 3.2 Character classes

**33 classes total**, distributed by race and gender: **[Manual]**

| Category | Count |
|----------|------:|
| Human male | 12 |
| Human female | 4 |
| Elven | 6 |
| Dwarven male | 8 |
| Kelden male | 3 |
| **Total** | **33** |

The starting class for all new characters is **Peasant** (level 1). Advancement to other
classes requires meeting stat minimums, paying training costs, and completing arena
promotion fights. **[Manual]**

> **[Open]** The exact stat requirements and abilities for each of the 33 classes are not
> fully documented in the available sources. The class list itself is confirmed at 33, but
> the individual class names, prerequisites, and special abilities would require the game
> binary or manual appendices to enumerate completely.

---

## 4. The `chardata` save file

The game stores character data in a file called **`chardata`**. **[Manual]** Up to 16
characters can be stored on one character disk. The file format is not fully decoded — no
binary was available for analysis — but one region is documented from community sources:

### 4.1 Quest status encoding

Quest status occupies **6 bytes** (12 hex digits) at file offsets **482–487** (0x1E2–0x1E7).
**[Web]**

The game tracks **24 quests**. Each quest has four states, encoded as a 2-bit binary code:

| Code | State |
|------|-------|
| `00` | Quest not given |
| `01` | Quest given but not complete |
| `10` | Quest complete but medal not given |
| `11` | Quest complete and medal given |

Each **hex digit** (4 bits) encodes **two quests** (2 bits each), so 12 hex digits = 24
quests = 6 bytes. **[Inferred]** — the arithmetic is exact (24 quests × 2 bits = 48 bits = 6
bytes), but the mapping of which hex digit encodes which pair of quests is not confirmed
against the game binary.

#### Decoding the quest block

Reading the 6 bytes at offsets 0x1E2–0x1E7 as a sequence of 12 hex digits:

```
byte[0x1E2] = digit[0] digit[1]    → quests 1–2 and 3–4
byte[0x1E3] = digit[2] digit[3]    → quests 5–6 and 7–8
byte[0x1E4] = digit[4] digit[5]    → quests 9–10 and 11–12
byte[0x1E5] = digit[6] digit[7]    → quests 13–14 and 15–16
byte[0x1E6] = digit[8] digit[9]    → quests 17–18 and 19–20
byte[0x1E7] = digit[10] digit[11]  → quests 21–22 and 23–24
```

Each hex digit's high two bits encode the first quest in the pair; the low two bits encode
the second. **[Inferred]** — the bit ordering within a hex digit is not confirmed; it could
equally be low-bits-first. A live test (completing quest 1 and reading the first byte) would
settle this, but no live session was available.

### 4.2 The 24 quests

All 24 quests must be completed before the final rescue-Seggallion quest becomes available.
**[Manual]** The quest chain progresses through the duchy of Ashtalarea, with each quest
building on the last. Quests are assigned by NPCs in towns and guilds.

> **[Open]** The individual quest names, assignment locations, and requirements are not
> fully enumerated here. A complete quest walkthrough would require the game manual's quest
> section or a playthrough. The quest-status encoding above is the load-bearing fact for the
> trainer: it lets a save editor mark quests as complete/medal-given without needing to know
> quest names.

### 4.3 Unknown regions of `chardata`

Without a binary or save-file diff, the following are **not decoded**:

- The character record layout (name, stats, gold, experience, class, inventory) within
  `chardata`
- The file header (if any)
- The total file size
- How 16 character records are packed (fixed-size records vs. variable)
- Whether a checksum exists

The quest-status block at 0x1E2–0x1E7 is the only region whose offset is confirmed. The
character record fields (statistics, gold, experience) would need to be located by
differential analysis — creating a character, saving, changing one stat, saving again, and
diffing the two files. This was not possible with available resources.

---

## 5. Combat system

Combat is **turn-based** with detailed tactical options. **[Manual]** Each turn, the player
issues orders to each party member, then all actions resolve simultaneously (with order
determined by Quickness and action speed). The **Foresight** stat adds a pre-resolution phase:
characters with high Foresight see what enemies intend to do before committing to their own
actions.

### 5.1 Attack types

**Weapon attacks:** **[Manual]**

| Attack | Notes |
|--------|-------|
| **Berserk** | All-out attack; high damage potential, high energy cost, leaves the attacker exposed. |
| **Hack** | Powerful overhead strike; moderate damage and energy cost. |
| **Thrust** | Directed piercing attack; good for exploiting gaps in armour. |
| **Slash** | Sweeping cut; balanced damage and energy cost. |

**Unarmed attacks:** **[Manual]**

| Attack | Notes |
|--------|-------|
| **Kick** | Leg strike; can unbalance the target. |
| **Bash** | Heavy blow; high energy cost, can stun. |
| **Head Butt** | Close-range strike; requires proximity. |
| **Punch** | Basic hand strike; low energy cost. |

### 5.2 Aiming

Each attack can be aimed at one of three target zones: **[Manual]**

| Aim | Notes |
|-----|-------|
| **High** | Head and upper body; higher damage, harder to land. |
| **Body** | Centre mass; balanced to-hit and damage. |
| **Low** | Legs and lower body; easier to hit, can affect movement. |

### 5.3 Defense

| Defense | Notes |
|---------|-------|
| **None** | No defensive effort; full offensive commitment. |
| **Panic** | Erratic evasion; unpredictable. |
| **Stand** | Hold ground; balanced defense. |
| **Back Up** | Retreat while defending; yields ground. |
| **Duck** | Lower profile; avoids high attacks. |
| **Dodge** | Active evasion; high energy cost. |
| **Jump** | Leap to evade low attacks. |

### 5.4 Movement

| Movement | Notes |
|----------|-------|
| **Walk** | Slow, low energy cost. |
| **Run** | Moderate speed, moderate energy cost. |
| **Sprint** | Fast, high energy cost. |
| **Fly** | Airborne movement; Kelden only. **[Manual]** |
| **Fly Faster** | Accelerated flight; Kelden only. |
| **Zoom** | Rapid aerial dash; Kelden only. |

The Kelden flight options are a significant tactical advantage — flying characters can
reposition freely and are immune to many ground-based attacks. **[Manual]**

### 5.5 Combat resolution

Actions resolve in **Quickness order**, modified by the speed of the chosen action. A fast
attack (Thrust) may resolve before a slow one (Berserk) even if the Berserk attacker has
higher Quickness. **[Inferred]** — the interaction of Quickness and action speed is stated in
the manual but the exact formula is not documented.

The **Foresight** stat modifies this by revealing enemy intentions before the player commits:
the character with the highest Foresight in the party sees what each enemy will do (attack
type, aim, defense, movement), allowing the player to choose optimal counter-actions. This
is the single most important tactical stat in the game. **[Manual]**

---

## 6. Magic system

### 6.1 The six magic orders

Each magic order is based in a specific location in Ashtalarea and teaches a distinct set of
spells. **[Manual]**

| Order | Location | Notes |
|-------|----------|-------|
| **White Pearl** | Brettle | Healing and protective magic. |
| **Blue Gem** | Tegal Forest | Elemental and nature magic. |
| **Black Onyx** | Shellernoon | Dark and destructive magic. |
| **Secret Storm** | Poitle Lock | Weather and storm magic. |
| **Red Mist** | Thimblewald | Blood and combat magic. |
| **Dark Stone** | Olanthen | Earth and binding magic. |

> **[Open]** The exact spell lists for each order are not fully documented in the available
> sources. The order names and locations are confirmed, but individual spells, their costs,
> and their effects would require the game manual's magic section or a playthrough.

### 6.2 Magic in combat

Spellcasting follows the same turn-based framework as physical combat. Casting costs both
**energy** (from the endurance pool) and possibly **spell points** or reagents, depending on
the order. **[Inferred]** — the manual describes magic costing energy like all actions, but
the exact resource model for spellcasting is not fully documented in the available sources.

---

## 7. Live-memory model (how the trainer works)

Because no game binary was available for static analysis, there is **no fixed byte signature
or string anchor** to locate character data in guest RAM. The trainer therefore treats
DOSBox/DOSBox-X guest RAM as an opaque address space and uses
`GameTrainers.Common.Memory.MemorySearcher` as a Cheat-Engine-style value scanner. **[Inferred]**

### 7.1 Guided scans

The trainer offers guided scans for the following values:

| Scan target | Width | How to narrow |
|-------------|-------|---------------|
| **Strength** | Byte (0–100) | Read from character sheet; scan Exact; raise stat at training; scan Increased. |
| **Quickness** | Byte (0–100) | Same procedure as Strength. |
| **Size** | Byte (0–100) | Same procedure. |
| **Health** | Byte (0–100) | Same procedure. |
| **Foresight** | Byte (0–100) | Same procedure. |
| **Charisma** | Byte (0–100) | Same procedure. |
| **Intellect** | Byte (0–100) | Same procedure. |
| **Gold Crowns** | Int16 or Int32 | Read from character sheet; spend gold; scan Decreased. |
| **Adventure Points** | Int16 or Int32 | Read from character sheet; gain AP in combat; scan Increased. |
| **Body Points (current)** | Byte or Int16 | Take damage; scan Decreased; heal; scan Increased. |
| **Endurance (current)** | Byte or Int16 | Perform actions; scan Decreased; rest; scan Increased. |
| **Level** | Byte | Read from character sheet; train; scan Increased. |

### 7.2 Scan workflow

1. **Attach** to the DOSBox/DOSBox-X process (`ProcessMemory.Open`).
2. **First scan** — enter a value read from the character sheet and scan Exact.
3. **Narrow** — make the value change in-game (train to raise a stat, take damage, spend
   gold) and scan Increased / Decreased / Changed / Unchanged.
4. **Pin** the surviving candidate(s) and edit or freeze.

### 7.3 Width estimation

The seven primary statistics are on a 0–100 scale, so they fit in a single **byte**.
**[Inferred]** Gold Crowns and Adventure Points may be 16-bit or 32-bit — the trainer
defaults to Int16 and the user can widen if the first scan returns no hits. Body Points and
Endurance are likely bytes or 16-bit words depending on the character's level. **[Inferred]**

### 7.4 Save-file editing (offline)

The trainer offers an **offline `chardata` editor** for quest status. Because the quest block
at offsets 0x1E2–0x1E7 is the only confirmed region, the save editor's scope is limited:

- **Read** the 6-byte quest block and decode it into 24 quest states.
- **Write** quest states (mark a quest as complete, mark a medal as given).
- A one-shot **`.bak`** is taken before the first write.

> The trainer does **not** write character statistics, gold, or experience to the save file,
> because those offsets within `chardata` are not decoded. Live-memory editing via the guided
> scanner is the path for those fields.

---

## 8. Dead ends and what could not be determined

### 8.1 No binary analysis

The single largest gap is the absence of any binary analysis. Without `kol.exe` (or whatever
the main executable is named) to load into Ghidra, the following could not be recovered:

- **The in-memory character record layout** — field offsets, record size, field ordering.
- **A static anchor for auto-location** — no string or byte pattern is confirmed to load at
  a fixed DGROUP offset, so no `GameLocator` can be built. The value-scanner model is the
  only option.
- **The complete `chardata` file format** — only the quest-status block is confirmed. The
  character record fields within the save file are not mapped.
- **The combat formula** — to-hit calculation, damage calculation, initiative ordering, and
  the interaction of attack type, aim, and defense are described qualitatively in the manual
  but not as formulas.
- **The spell lists** — the six order names and locations are known, but the individual
  spells, their costs, and their effects are not enumerated.
- **The 33 class details** — class names, stat prerequisites, and special abilities are not
  fully documented.

### 8.2 No teleport

Map/position data was not identified — neither in the save file nor in live memory. There is
no teleport feature. **[Open]**

### 8.3 No inventory editing

The inventory item format and encoding within `chardata` and live memory are unknown. The
trainer does not offer inventory editing. **[Open]**

### 8.4 Quest-offset mapping unconfirmed

The quest-status encoding (6 bytes, 24 quests, 2 bits each) is arithmetically exact, but the
mapping of which hex digit encodes which quest pair is **not confirmed against the game
binary**. A live test — completing quest 1, saving, and reading byte 0x1E2 — would settle
both the digit ordering and the bit ordering within each digit. This was not possible with
available resources. **[Inferred]**

---

## 9. Trainer implications

- **Safe to edit (live memory):** the seven primary statistics (byte scans), Gold Crowns,
  Adventure Points, body points, endurance, and level — all via guided value scans, pinned
  and optionally frozen.
- **Safe to edit (save file):** quest status at `chardata` offsets 0x1E2–0x1E7, with a
  one-shot `.bak`.
- **Read-validate-write:** every live-memory write re-reads the target address before
  committing, so a stale scan result does not corrupt the wrong memory.
- **No auto-location:** the trainer cannot one-click locate character data. The user must
  perform guided scans to pin each value. This is the same model as `DarklandsTrainer`,
  `MoriaTrainer`, and `DarksypreTrainer`.
- **No teleport, no inventory editor, no class editor** — these require binary analysis that
  was not available.

---

## 10. Sources

| Source | What it gave |
|--------|-------------|
| Game manual (Origin Systems, 1989) | Stat names and ranges, race/class counts, combat options, magic orders and locations, training costs, fatigue system, Foresight mechanic, quest structure, currency, character disk capacity |
| MobyGames | Publisher, release date, genre, designer credit, platform confirmation |
| Wikipedia | Setting (Ashtalarea / Sondar), plot summary (Pildar / Duke Fuquan / Seggallion), series context |
| GameFAQs community resources | Quest-status encoding (6 bytes at offsets 482–487, 24 quests, 2-bit codes), training cost details, skill-point formula |
| Fan sites and walkthroughs | Gameplay mechanics, arena promotion requirements, class system details |

> **Note on confidence:** because all of the above came from secondary sources rather than
> from the game binary or live memory dumps, the trainer treats every offset and every
> structural claim as **[Manual]** or **[Inferred]**. A future pass with the game binary in
> Ghidra, or a live DOSBox session with memory dumps, would promote these to **[Confirmed]**
> and could enable a `GameLocator` with auto-location.
