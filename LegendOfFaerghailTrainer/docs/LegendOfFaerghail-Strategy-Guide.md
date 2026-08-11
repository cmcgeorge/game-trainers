# Legend of Faerghail — strategy guide

For the PC/DOS conversion (Electronic Design Hannover / reLINE Software GmbH, 1990). Written from
the game's own manual (`lof.txt`, shipped in the game directory), from its executable's data tables,
and from playing the DOS build under DOSBox. Where a number came out of the binary rather than a
walkthrough, it says so.

---

## 0. First: slow the emulator down

Legend of Faerghail has **no frame limiter**. It redraws and polls the keyboard as fast as the CPU
allows, so under DOSBox's default `cycles=auto` on a modern host the wilderness scrolls past faster
than you can steer and message pages flash by before you can read them. Nothing in the game's own
options helps — this is an emulator setting.

- **In-game:** `Ctrl+F11` slows the emulator by about 10% per tap, `Ctrl+F12` speeds it up. DOSBox
  shows the new cycle count in its title bar. The trainer's **Slower / Faster** buttons send exactly
  these keys.
- **Permanently:** in `dosbox.conf`, set

  ```ini
  [cpu]
  core=auto
  cycles=fixed 3000
  ```

  Around 3,000 cycles plays comfortably. Combat and the intro tolerate more; the wilderness is what
  needs the brake.

The game is mouse-first (it is an Amiga port) but everything has a keyboard equivalent: the letter
in brackets on every menu line, cursor keys to move and turn, and space/return for `(C)ontinue`.

---

## 1. The mission, in one paragraph

The Count of Thyn sends you to the neighbouring county of Cyldane to ask Count Hagror for troops —
and, on the way, to find out why the normally peaceful Elves have started killing people. You start
alone in a tavern in Thyn with a purse and no companions.

---

## 2. Building the party

You get **six slots**. Two ways to fill them, both from the tavern:

- **(A)dd a new character** shows the tavern's Recruit list — characters already saved in the
  roster, ready-equipped, joining for a fee. Fastest start.
- **(L)ook for new character** rolls a fresh candidate against filters you choose (Sex, Race,
  Trade), then offers `(L)ook` → `(A)ccept` / `(R)eject`. You can re-roll indefinitely, so this is
  where good characters actually come from. Names are up to **10 letters** and must be unique. The
  roster holds at most **32** characters.

### Races

| Id | Race | Play notes |
| --- | --- | --- |
| 0 | Human | No professional limits, no bonuses. |
| 1 | Half-Elf | Some Elven advantages, not all. |
| 2 | Elf | Intelligent, near-immune to paralysis, excellent archers, weak in a melee. |
| 3 | Halfling | Thieves and scouts; good with bow and sling. |
| 4 | Dwarf | Strong and tough, **cannot be a Magician**, distrusts Elves. |
| 5 | Half-Orc | Strong, dim, quarrelsome — and starts speaking Orc tongue, which is a real asset when parleying. |

Race and trade ids are the ones the executable uses, and they are the ids the trainer's dropdowns
show.

### Trades

The manual and the program disagree on four names. The program is what you see on screen:

| Id | On screen | Manual calls it | Notes |
| --- | --- | --- | --- |
| 0 | Warrior | Warrior | Every party wants at least one. Breaks doors. |
| 1 | Barbarian | Barbarian | Born, not trained. Tougher than a warrior in the wild; detests magic. |
| 2 | Rogue | Thief | Traps, locks, pockets, and the **Stalk** combat action. Leather or Elven chain only. |
| 3 | Smith | Blacksmith | Repairs the party's weapons and armour in the field — needs anvil **and** hammer, and eats rations doing it. Casts a handful of trade spells. |
| 4 | Scout | Ranger | Sizes up an enemy group before the fight; casts from level 6. |
| 5 | Priest | Cleric | Wisdom-based; no spellbook needed. |
| 6 | Druid | Druid | Elemental magic; nature seldom turns on the party. |
| 7 | Magician | Magician | The heavy artillery. No metal armour. |
| 8 | Illusionist | Illusionist | Wants Intelligence *and* Dexterity. |
| 9 | Paladin | Paladin | Warrior who gains clerical magic at level 4. Will not attack a good creature. |
| 10 | Healer | Healer | Always female. Protective and curative magic only. |
| 11 | Monk | Monk | Blunt weapons, no armour, and some thief skills. |
| 12 | `??` | — | Not selectable: the slot the game gives **non-player characters** you pick up in the world. They carry Rnk 0. |

### A party that works

One Warrior or Barbarian, one Paladin, a Rogue, a Priest or Healer, a Magician, and a sixth slot for
whatever the run needs — a Scout for enemy intel, a Smith if you are heading somewhere far from a
town, or a Dwarf for the Dwarven tongue. Put the characters least likely to earn experience at the
**top** of the list; the manual says so outright, and experience is awarded per character on what
that character actually did, not split evenly.

### Attributes

Strength, Intelligence, Wisdom, Dexterity, Constitution — the manual's five. Note the record's own
storage order differs, which matters only if you are poking bytes by hand.

- **Strength** — damage and carrying capacity. Warriors, Barbarians, Paladins.
- **Intelligence** — magician-family power, and how fast a character learns languages.
- **Wisdom** — Priests, Druids, Rangers; casting from memory rather than from a book.
- **Dexterity** — armour class, reaction, traps, and the whole Rogue/Monk kit.
- **Constitution** — hit points, resistance to poison and illness.

Only Strength and Dexterity really respond to training; the rest are what you rolled, apart from the
occasional statue or potion that grants a point.

---

## 3. Town

Thyn and Cyldane are laid out the same way.

| Building | What it is for |
| --- | --- |
| **Inn / tavern** | Recruit, dismiss, rest, pick pockets, load a game. |
| **Emporium** (Steelstone trading post) | Buy and sell. Second-hand gear fetches a fair price here. |
| **Bank** | Somewhere to leave gold you do not want carried into a dungeon. |
| **House of trades** | Level up, learn spells, learn languages — all for money. |
| **Temple** | Healing, curing poison and disease, and resurrection. |

**The House of Trades is the money sink.** Advancing a level costs on the order of **1,500 gold**,
and early fights pay a few gold a head, so the first several hours are about money as much as
combat. Selling everything you do not need is not optional.

Town gates and tavern doors **lock at sundown**. Plan the day so you arrive somewhere with a roof
before dark; camping in the open invites bandits, and at full moon, werewolves.

---

## 4. In the field

- **Rations**: the party eats automatically, about six rations a day across six characters. Buy
  them in bulk. Running out during a rest means the rest does not restore properly.
- **Resting**: `(2) / (4) / (6) / (8)` periods — a quarter, half, three-quarters, or a full day.
  Post a watch; the sentry is what stops thieves stripping the baggage while you sleep, and a
  character who is not healthy cannot stand watch.
- **Spirits / morale** runs Good → Satisfactory → Mediocre → Alarming → Bad, and it sags with time
  away from town and with accumulated wounds. Resting outdoors is what pulls it back up.
- **Do not walk into walls or trees.** Collisions do real hit-point damage — an unusual rule and a
  genuinely common way to lose a low-level character.
- **The sun rises in the east.** There is no compass until you find one, and the status panel's
  "Facing …" line is your only other heading cue.
- **Map on paper.** The renderer does not draw a wall that is exactly perpendicular to you until you
  turn to face it, which makes dungeon mapping harder than it looks. The Magic Ball (the "mythical
  sphere", lost in the Dwarven mines) auto-maps once found, and the `Magic map` spell helps.

### Field commands

`(U)se item`, `(C)amp/rest`, `(F)ight`, `(R)epair` (a Smith works on one character's gear),
`(D)ismiss`, `(L)ure` (drop food or gold to shake off pursuit — the more intelligent the monster,
the better the bribe must be), `(M)agic ball`, `(P)ick lock` (a Rogue on a door in front of you),
`(O)ptions/files`. The right mouse button opens the same menu, and `P` also pauses.

---

## 5. Combat

Contact does not have to mean a fight. If someone in the party **speaks the enemy's language** and
has enough **Negotiating**, you can trade, parley, or bluff your way past. Evil creatures will not
parley — do not waste the round.

When it does come to a fight, each character picks a **rank** and an **action** for the round:

| Rank | Meaning |
| --- | --- |
| **Kil** — killing | Closest. Best chance to hurt, best chance to be hurt. |
| **Att** — attacking | Trades damage for safety. |
| **Def** — defending | Blocks incoming attacks. |
| **Ret** — retreating | Furthest back. |

The ranks work in reverse for casters: **the further back a magician stands, the better the spell**,
because concentration is what a spell costs. A Magician in the killing rank with an Orc on his back
is a wasted turn. Keep casters in `Ret`, front-liners in `Kil` or `Att`, and anyone hurt in `Def`.

Actions: **Defend**, **Attack** (front rank of the enemy only — nobody swings over heads),
**Stalk** (Rogues and Monks: vanish for a round, reappear behind the enemy line, and hit **any**
target, scaled by Dexterity and the Stalking skill), **Use object** (wands, magic weapons), and
**Cast spell**.

`(Q)uick combat` resolves a round instantly and reports it as a table: hit points lost, whether a
**W**eapon or **A**rmour took damage, and whether the attack succeeded. Weapons and armour degrade —
that condition percentage on the inventory page is not decoration, and it is why a Smith earns his
rations.

A Scout (Ranger) in the party adds a summary of the enemy's condition after each round.

---

## 6. Magic

Magic points are a daily budget: each spell costs concentration, and only **rest** restores them.
Each known spell also has its own count of uses left today, printed as `left / max` on sheet page 3.

Five spell families are woven through the game's 141-entry spell table: magician, illusionist,
druid, priest and healer lists, plus a tail of monster and event effects that no character learns
(ids 128–141 — still in German in this English build).

Early spells worth having: **Healing I**, **Light**, **Magic arrow**, **Shield I**, **Word of
sleep**. Utility spells that save real time: **Magic map**, **Open locks**, **Disarm traps**,
**Farsightedness**, **Cure poison**, **Remove curse**. Late game the Magician list gets
**Fireball**, **Lightning bolt**, **Disintegrate** and **Word of death**.

New spells are bought at the House of Trades; **Concentration** governs both how well spells work
and how quickly new spells and languages are learned, which makes it the most undervalued ability in
the game.

---

## 7. Languages

Eight of them: Common, Animal, Orc, Lizard, Dwarven, Elven, Dark, Magic. A character speaks a
language or does not — there is no partial credit at the parley table.

- **Common** — everyone starts with it.
- **Orc** — the single most useful extra, because Orcs are everywhere. A Half-Orc starts with it.
- **Dwarven** — a Dwarf starts with it; wanted in the mines.
- **Elven** — the whole plot is an Elven problem.
- **Dark** — everything that lives underground and shuns daylight.
- **Animal** — Druids and Rangers should have it.
- **Magic** — a Magician who cannot speak it will meet things he cannot control.

Languages are learned at the House of Trades and gated by Intelligence and Concentration. Spreading
them across the party is fine; you only need one speaker present.

---

## 8. Traps and locked doors

The game carries nine trap kinds: knee, arrow, stone, gas, small bomb, middle-sized bomb, pit,
flame, and a dummy. Some of them kill outright — a pit of upright spears, a ceiling collapse, an
acid drip. **Trap detecting** finds them, **Trap disarming** removes them, and both are Rogue and
Monk territory.

Doors: a Rogue with his tools opens most of them (`P`), but a nervous Rogue refuses, and a
magically locked door will not open at all. A strong Warrior can break a door down at the cost of
injury. Not every door is even locked — the game will happily let a Rogue struggle with an unlocked
one and then tell him so.

---

## 9. Route

Eight named regions appear in the executable's own table, in this order: **Valley of Faerghail**,
**Monastery of Sagacita**, **Sagacita catacombs**, **The Mines**, **The Pyramid**, **The Temple**,
**The Castle**, **The Mountain**.

The broad path most walkthroughs follow:

1. **Thyn** — recruit, equip, take the Amulet the town guard gives you on the way out.
2. **Valley of Faerghail** — the overland map. Taverns are the safe nodes.
3. **The Dwarven mines (Khazad Maran)** — the pass through the Dragon's Tail mountains to Cyldane,
   and where the lost Magic Ball is.
4. **Cyldane** — Count Hagror. He sends troops east and points you at the Monastery.
5. **Monastery of Sagacita**, then its **catacombs**.
6. **The Temple of the Dragon Servants** and the eastern wilderness.
7. **The Elven Pyramid** — the big puzzle dungeon.
8. **The derelict castle** — and a vampire.
9. Back to **the mines**, deeper.
10. **The inactive volcano** — the finish.

Riddle answers that walkthroughs agree on, for when you are stuck rather than for reading first:
the four Elementals at the maze north-east of Cyldane want **FIRE, DAUGHTER, ECHO, EYES**; mine
level 4 wants **PLOUGH**; the Dragon Temple **AND**; the Pyramid guard **SOMETHING**; the Black
Flame chamber **CIRCLE**; the castle **RULER**; the Dwarf **SPINGO**; the volcano's Earth Elemental
**ICEFLOWER**. The tavern riddle about Findal's family is answered from the names the game itself
carries — `holli`, `fiNDaiL`, `scAgNAR`, `AEGanoR`, `TeORlin`.

---

## 10. Where the trainer helps

Ordered by how much time each one actually saves:

1. **Emulator speed.** Do this first, before anything else.
2. **Gold.** Levelling costs ~1,500 gold a rank and spells and languages cost more. Grinding for
   money is the least interesting thing in the game. *Give gold* to the party and skip it.
3. **Rations, and freezing them.** Starvation is bookkeeping, not difficulty.
4. **Full heal, and freezing hit points.** A dead character means a walk back to a temple and a fee
   you cannot afford early.
5. **Refill spells / repair gear.** Restores every known spell's daily uses and mends everything in
   the pack — one click instead of a rest cycle plus a Smith plus his rations.
6. **Abilities.** Trap detecting, Trap disarming and Lock picking are what turn dungeons from a
   save-scummed crawl into a walk; Negotiating turns fights into conversations.
7. **Languages.** Ticking all eight removes a whole category of dead ends.
8. **Attributes and Rnk.** Available, but the bluntest instrument here — the game's fights get
   trivial fast.

What the trainer deliberately does not do: it will not teleport you (the party's map position was
never located), and it will not edit save files — this release cannot load a saved game at all,
including one it has just written, so any save editor would be an unverified write. See the
[reverse-engineering notes](LegendOfFaerghail-Reverse-Engineering.md) §6.

---

## Sources

- The game's own manual, `lof.txt`, shipped in the game directory.
- Reference tables (races, trades, spells, items, traps, regions) read directly out of `LOF.EXE`.
- [The CRPG Addict: Game 122: Legend of Faerghail (1990)](http://crpgaddict.blogspot.com/2013/11/game-122-legend-of-faerghail-1990.html) — combat, economy and survival mechanics.
- [Legend of Faerghail Lösung / Walkthrough — mogelpower.de](http://www.mogelpower.de/cheats/loesung.php?id=37962) — route and riddle answers.
- [Legend of Faerghail — MobyGames](https://www.mobygames.com/game/3436/legend-of-faerghail/)
- [Legend of Faerghail — Lemon Amiga](https://www.lemonamiga.com/games/docs.php?id=976)
