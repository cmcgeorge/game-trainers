# Legend of Grimrock — strategy guide

Every number in this guide was read out of the game's own tables while it was running, not
transcribed from a wiki. Where a mechanic is described rather than tabulated, it is because the
mechanic lives in Lua code rather than in a data table, and the reading is flagged as such.

Version 1.3.7 (the final patch), normal difficulty, the shipped thirteen-level campaign.

---

## 1. Read this first

Grimrock is a real-time grid crawler. You occupy one 3×3-metre tile, you turn in ninety-degree
steps, and everything that matters happens in the half-second between an enemy's wind-up and its
swing. Three rules carry the whole game:

1. **Never stand still in a fight.** Almost every monster telegraphs, swings at the tile in front of
   it, and pauses. Step sideways, attack the flank, step again. This "combat waltz" is not a
   degenerate exploit — it is how the game is designed to be played, and it is why a party that
   trades blows toe-to-toe dies on level 4 while one that circles clears level 13.
2. **Doorways and corners are weapons.** Monsters cannot cut corners. Retreat through a door, sidestep
   round a pillar, or pull one enemy out of a group of three.
3. **Food is a clock.** Resting is the only way to heal, resting burns food, and food is finite. A
   party that rests after every skirmish will starve before it reaches the Tomb.

---

## 2. Character creation

You get four champions, arranged two front and two back. **Only the front row can attack in melee**
(until someone takes Reach Attack), and **both rows can throw, shoot and cast**. That single fact
determines the party.

### Races

| Race | Str | Dex | Vit | Wil | Skill points | Food rate | Racial trait |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Human | 10 | 10 | 10 | 10 | **4** | 1.0 | — |
| Minotaur | high | low | high | low | 3 | high | Head Hunter (Attack Power +3 per skull carried) |
| Lizardman | — | high | — | — | 3 | 1.0 | — |
| Insectoid | — | — | — | high | 3 | 1.0 | Natural Armor (Protection +5) |

The Human row is exact — it was read from a live Human champion. The other three are described
rather than tabulated because Grimrock keeps race definitions as locals inside
`CharacterGeneration.lua` and only the races your party actually chose survive in memory; the two
racial traits, however, are exact, complete with their `requiredRace` fields.

**Humans get four skill points instead of three.** Over a full game that is one extra point per
character, and skill points buy milestone bonuses that dwarf a couple of attribute points. If you
are unsure, take Humans.

### Traits

Two at creation. Every trait, from the game's own talent table:

| Trait | Effect | Note |
| --- | --- | --- |
| **Skilled** | +3 skill points | the strongest general-purpose pick in the game |
| **Aura** | Energy +15 | near-mandatory on a caster |
| Athletic | Strength +2 | |
| Agile | Dexterity +2 | |
| Healthy | Vitality +2 | |
| Strong Mind | Willpower +2 | |
| Tough | Health +15 | |
| Aggressive | Attack Power +4 | flat, applies to everything |
| Evasive | Evasion +7 | |
| Fist Fighter | Attack Power +6 when unarmed | only for an unarmed build |
| Daemon Ancestor | Resist Fire +25 | |
| Cold-blooded | Resist Cold +25 | |
| Poison Resistant | Resist Poison +25 | |
| Head Hunter | Attack Power +3 per skull carried | **Minotaur only** |
| Natural Armor | Protection +5 | **Insectoid only** |
| Thunderstruck | — | hidden; not offered at creation |

Skilled is worth taking on all four characters. Three extra points at level 1 reach a milestone you
would otherwise not see until level 5 or 6, and milestones compound.

### A party that works

| Slot | Build | Starting skills |
| --- | --- | --- |
| Front left | Human Fighter, Skilled + Athletic | Swords 3, Armors 2, Athletics 2 |
| Front right | Minotaur Fighter, Skilled + Head Hunter | Maces or Axes 4, Armors 3 |
| Back left | Human Rogue, Skilled + Agile | Throwing Weapons 3, Missile Weapons 2, Dodge 2 |
| Back right | Human Mage, Skilled + Aura | Fire Magic 2, Spellcraft 2, Air Magic 2 |

Why: two melee bodies in front with real armour; a rogue who contributes damage from the back row
with thrown weapons (and can carry a bow later); one mage who does not need line of sight to matter.
The default party the game hands you is a near-identical shape and is perfectly playable — the
version above just spends the Skilled trait everywhere.

**One mage is enough.** Two mages means splitting scrolls and energy potions between them and having
only one front-liner, which is how parties die on level 6.

**Toorum** is a hidden single-character mode unlocked after finishing the game once. Fun; not a
first run.

---

## 3. Skills

One point per level, **0 to 50**, and every point is spent permanently. Milestones are the whole
point: the bonuses below are exactly what the game's own upgrade tables grant.

### The two everyone should consider

**Athletics** — the only skill that raises raw physical numbers on any build.

| Skill level | Grants |
| --- | --- |
| 2 | Strength +1 |
| 5 | Vitality +2 |
| 8 | Health +10 |
| **10** | **Endurance — food consumption −25 %** |
| 12 | Dexterity +2 |
| 16 | Strength +2 |
| **20** | **Porter — carrying capacity +15 kg** |
| 22 | Vitality +2 |
| 24 | Dexterity +2 |
| 28 | Health +10 |
| 30 | Resist Fire +10, Resist Cold +10 |
| 33 | Strength +2 |
| 38 | Vitality +2 |
| 40 | Resist Poison +10, Resist Shock +10 |
| 45 | Health +10 |
| **50** | **Iron Body — Health +100** |

Athletics 10 for Endurance is arguably the single best early investment in the game: a quarter less
food consumed is a quarter more resting, on every character who takes it.

**Armors** — for anyone who will wear metal.

| Skill level | Grants |
| --- | --- |
| 2 | Protection +1 |
| 5 | Health +10 |
| **8** | **Light Armor Proficiency — no penalties from light armour** |
| 12 | Health +10 |
| **16** | **Heavy Armor Proficiency — no penalties from heavy armour** |
| 19 | Health +15 |
| 22 | Protection +2 |
| **25** | **Shield Expert — doubles a shield's evasion bonus** |
| 28 | Health +15 |
| 33 | Evasion +5 |
| 35 | Health +25 |
| 38 | Protection +2 |
| 44 | Health +25 |
| **50** | **Armor Master — Protection +25** |

Armour without the matching proficiency carries a penalty. Plate on a character with Armors below
16 is worse than ring mail; get to 8 early and 16 before you find the good plate.

### Weapon skills

Each weapon skill raises Attack Power, Accuracy and special-attack frequency **only while wielding
that weapon type**, and each grants a special attack at listed levels. Spreading points across two
weapon types is the classic beginner mistake — the milestones, not the linear scaling, are where the
value is.

| Skill | Special attacks (skill level) | Mastery at 50 |
| --- | --- | --- |
| **Swords** | Slash 10, Parry 16 (Evasion +5 with a sword), Thrust 23, Flurry of Slashes 33 | **Sword Master — doubles attack speed** |
| **Axes** | Chop 10, Cleave 22, Rampage 33 | Axe Master — Attack Power +20 with an axe |
| **Maces** | Bash 10, Crushing Blow 20, Devastating Blow 33 | **Mace Master — attacks ignore enemy armour** |
| **Daggers** | Stab 10, Piercing Strike 22, Flurry of Slashes 33 | Death Strike — an extremely deadly attack |
| **Unarmed Combat** | Jab 11, Kick 23, Faster than Lightning 30 (Evasion +20) | Three Point Technique |
| **Assassination** | Backstab 8, Reach Attack 12, Improved Backstab 20, Quick Strike 23, Improved Critical 31, Piercing Attack 35, Improved Quick Strike 45 | Master Assassin |
| **Missile Weapons** | Quick Shot 12, Improved Quick Shot 24, Volley 32 (two missiles per shot) | Master Archer — doubles critical chance |
| **Throwing Weapons** | Quick Throw 12, Improved Quick Throw 24, Double Throw 32 | Throwing Master — doubles critical chance |

Two of these deserve calling out:

- **Sword Master at 50 doubles attack speed.** Nothing else in the game doubles a character's damage
  output outright. If a front-liner is going deep into one skill, swords is the one.
- **Assassination 12 gives Reach Attack — melee from the back row.** That single milestone changes
  party composition: a back-row character with a weapon and Assassination 12 fights like a
  front-liner without taking the hits.

### Defensive and magic skills

**Dodge** (rogues): Evasion +5 at 2, Evade at 5 (Resist Fire/Shock +5), Health +15 at 8, **Stealth
at 11** (doubles a cloak's evasion), Light Armor Proficiency at 17, **Improved Stealth at 24**,
Resist Poison +20 at 28, Greater Evade at 34, **Ninja Master at 50 (Evasion +50)**.

**Staff Defense** (mages — the skill is called `staves` internally but the game labels it Staff
Defense): Protection +1 at 2, Evasion +5 at 5, Health +10 at 8, Protection +2 at 11, Light Armor
Proficiency at 14, and **Staff Master at 50 (Protection +10, Evasion +30)**. A mage with nothing in
this dies to the first thing that reaches the back row.

**Spellcraft** — the mage's backbone. Willpower +1 at 2; **Light and Darkness at 5**; Energy +10 at
6; **Combat Caster at 10 (25 % faster casting)**; **Improved Combat Caster at 18 (50 % faster)**;
Energy +10 at 12, 22, 30, 38, 46; Willpower +1 at 8, 15, 26, 34, 42; and **Archmage at 50 — every
spell costs half energy**.

Get Spellcraft to 5 immediately. Light is the spell that lets you stop burning torches, and torches
are a consumable you will otherwise ration for the whole game.

**The four elemental schools** each follow the same shape: a cheap attack spell, an arrow enchant, a
big bolt, a shield, an attribute every few levels, a resistance every few levels, an "improved"
talent, a party-wide Circle of Protection at 32, and a Mastery at 50 worth +100 resistance.

| School | Spells (skill level) | Attribute gained | Circle at 32 |
| --- | --- | --- | --- |
| **Fire** | Fireburst 2, Enchant Fire Arrow 7, **Fireball 13**, Fire Shield 16; Greater Fireball at 28 | Strength | Resist Fire +25 to the party |
| **Air** | Shock 4, Enchant Lightning Arrow 9, **Lightning Bolt 14**, Invisibility 19, Shock Shield 22; Greater Lightning Bolt at 27 | Dexterity | Resist Shock +25 |
| **Ice** | Ice Shards 3, Enchant Frost Arrow 7, **Frostbolt 13**, Frost Shield 19; Improved Frostbolt at 24 | Willpower | Resist Cold +25 |
| **Earth** | Poison Cloud 3, **Poison Bolt 7**, Enchant Poison Arrow 11, Poison Shield 13; Improved Poison Bolt at 23 | Vitality | Resist Poison +25 |

**Fire Magic 13 for Fireball is the mage's first real power spike.** Before it, a mage contributes;
after it, a mage kills things. Earth's Poison Bolt arrives at 7, which makes Earth the cheapest
early damage — but poison is resisted by a lot of what lives in Grimrock, so it falls off.

---

## 4. Spells and the rune board

The board is 3×3. Reading left to right, top to bottom, the positions are:

```
A  B  C
D  E  F
G  H  I
```

To cast: click the runes in any order (order does not matter, the set does), then cast. You need the
listed skill level **and** you must have found the scroll.

| Spell | School | Skill level | Runes | Energy |
| --- | --- | --- | --- | --- |
| **Light** | Spellcraft | 5 | `B E` | 25 |
| **Darkness** | Spellcraft | 5 | `E H` | 25 |
| Fireburst | Fire | 2 | `A` | 15 |
| Enchant Fire Arrow | Fire | 7 | `A B F H` | 20 |
| **Fireball** | Fire | 13 | `A C F` | 33 |
| Fire Shield | Fire | 16 | `A E` | 50 |
| Shock | Air | 4 | `C` | 21 |
| Enchant Lightning Arrow | Air | 9 | `B C F H` | 20 |
| **Lightning Bolt** | Air | 14 | `C D` | 40 |
| Invisibility | Air | 19 | `C E H` | 35 |
| Shock Shield | Air | 22 | `C E` | 55 |
| Ice Shards | Ice | 3 | `G I` | 24 |
| Enchant Frost Arrow | Ice | 7 | `B F H I` | 20 |
| **Frostbolt** | Ice | 13 | `C I` | 29 |
| Frost Shield | Ice | 19 | `E I` | 45 |
| Poison Cloud | Earth | 3 | `G` | 17 |
| **Poison Bolt** | Earth | 7 | `C G` | 22 |
| Enchant Poison Arrow | Earth | 11 | `B F G H` | 20 |
| Poison Shield | Earth | 13 | `E G` | 35 |

Notes worth having:

- **Light is the quality-of-life spell of the run.** 25 energy for hands-free illumination beats
  carrying a torch in a hand you would rather have a weapon in.
- **Enchant arrows cost 20 energy and buff a stack of arrows.** Cast one before a fight, not during.
- **Shields grant +35 of one resistance to the whole party.** Situational, expensive, and worth it
  against the Uggardians (fire) and the Ice Lizards (cold).
- **Poison Cloud is one rune and 17 energy.** It is the cheapest area denial in the game and it works
  through a doorway you have retreated behind.
- **Powerbolt** exists in the spell table with no rune combination and no cost. It is not cast from
  the board.

---

## 5. Combat

### The waltz

Attack, sidestep, attack, sidestep. A monster that swings at where you were does no damage. Against
a single enemy in an open room you should take almost nothing. Practise it on the snails of level 1
where the penalty for getting it wrong is small.

### Rows, reach and rotation

Front row melees; back row throws, shoots and casts. Swap a wounded front-liner backwards mid-fight
— it costs no time and the character keeps fighting with thrown weapons. Assassination 12
(Reach Attack) removes the restriction entirely for that character.

### Attack power, protection and evasion

- **Attack Power** comes from the weapon plus the wielder's skill plus Strength. The weapon tables
  below are the base numbers.
- **Cool-down** is the number that actually decides damage per second. An Ancient Axe hits for 36
  every 6.3 seconds; a Cutlass hits for 19 every 3.3. Do the division before you decide a heavier
  weapon is better.
- **Protection** reduces damage taken. **Evasion** makes attacks miss. Heavy armour buys the first,
  a rogue's cloak-and-stealth build buys the second.
- **Starving halves Attack Power** and stops resting from healing. Never fight hungry.
- **Rage** is +10 Attack Power and −10 Evasion; **Blind** is −50 accuracy; **Slow** doubles your
  cool-downs and **Haste** halves them.

### Weapons, by skill

| Skill | Weapon | Attack Power | Cool-down | Weight |
| --- | --- | --- | --- | --- |
| **Swords** | Dismantler | 27 | 4.0 | 4.8 |
| | Nex Sword | 24 | 3.8 | 3.2 |
| | Cutlass | 19 | 3.3 | 3.5 |
| | Long Sword | 14 | 3.2 | 3.2 |
| | Fire Blade / Lightning Blade | 14 | 5.0 | 4.0 |
| | Machete | 9 | 3.2 | 2.2 |
| **Axes** | Ancient Axe | 36 | 6.3 | 7.3 |
| | Great Axe | 24 | 5.6 | 8.0 |
| | Battle Axe | 20 | 5.2 | 4.0 |
| | Hand Axe | 10 | 4.5 | 2.4 |
| **Maces** | Ogre Hammer | 36 | 6.0 | 13.0 |
| | Icefall Hammer | 35 | 6.0 | 6.5 |
| | Flail | 25 | 5.0 | 6.5 |
| | Warhammer | 18 | 4.5 | 5.7 |
| | Knoffer | 14 | 4.5 | 5.0 |
| | Cudgel | 12 | 5.0 | 3.8 |
| **Daggers** | Assassin Dagger | 15 | 2.8 | 1.0 |
| | Fist Dagger | 12 | 2.5 | 1.5 |
| | Dagger / Venom Edge | 7 | 2.5 | 0.8 |
| | Knife | 5 | 3.0 | 0.8 |
| **Missile** | Crossbow | 20 | 5.5 | 1.5 |
| | Longbow | 19 | 4.5 | 1.0 |
| | Short Bow | 12 | 4.0 | 1.0 |
| | Sling | 5 | 6.0 | 0.5 |
| **Throwing** | Throwing Axe | 15 | 5.5 | 0.5 |
| | Shuriken | 11 | 3.5 | 0.1 |
| | Throwing Knife | 8 | 4.0 | 0.2 |
| | Rock | 5 | 4.0 | 1.0 |

Two readings from that table:

- **The Longbow out-damages the Crossbow** (19 / 4.5 s beats 20 / 5.5 s) and weighs less.
- **Daggers are a damage-per-second skill, not a damage skill.** An Assassin Dagger at 2.8 seconds
  puts out more than a Battle Axe at 5.2, and it pairs with Assassination's backstab multipliers.

### Armour

| Set | Protection per piece | Notes |
| --- | --- | --- |
| Valor | **15** | the best in the game; helmet, cuirass, greaves, gauntlets, boots |
| Plate | 12 | heavy; needs Armors 16 |
| Chitin | 9 | mask, mail, greaves, boots |
| Ring | 6 | the reliable mid-game set |
| Leather | 4 | light |
| **Lurker** | 0, but **Evasion +5 each** | the rogue set — four pieces is Evasion +20 |

Wearing a complete set gives a set bonus, which is why the mixed "best protection per slot" approach
is usually worse than committing to one set. Notable singles: Full Helmet 12, Iron Basinet 8,
Legionary Helmet 6, Hide Vest 3, Pit Gauntlets 3.

**Lurker versus plate** is the real armour decision. Four Lurker pieces weigh 1.75 kg total and give
Evasion +20 (doubled to +40 with Dodge 24's Improved Stealth); four plate pieces weigh 34 kg and
give Protection +48. Heavy for the front row, Lurker for the back.

---

## 6. Bestiary

Straight from the game's monster archetypes. Health, experience, protection and evasion are exact.

| Monster | Health | Exp | Prot | Evasion | Attack Power | Fight it by |
| --- | --- | --- | --- | --- | --- | --- |
| Snail | 90 | 60 | — | −10 | — | walking away; they are slow |
| Herder (small) | 80 | 65 | — | — | 10 | killing before they split |
| Scavenger | 100 | 75 | — | +10 | — | waltzing; they are fast but fragile |
| Herder | 120 | 75 | — | −10 | 10 | area damage — they come in groups |
| Crowern | 120 | 90 | — | +10 | 15 | ranged; they close fast |
| Skeleton Warrior | 120 | 90 | 5 | — | — | maces (they ignore armour) |
| Skeleton Archer | 120 | 90 | 5 | — | — | breaking line of sight |
| Wyvern | 200 | 90 | 4 | +10 | — | corners |
| Herder (big) | 300 | 95 | — | −10 | 14 | keeping distance |
| Spider | 160 | 175 | — | — | — | poison resistance; they hit hard and fast |
| Shrakk Torr | 180 | 195 | — | **+25** | — | accuracy; they dodge a lot |
| Green Slime | 450 | 190 | — | −20 | 25 | anything; they are slow and cannot dodge |
| Tentacles | 420 | 320 | — | — | — | ranged from outside their reach |
| Crab | 410 | 450 | 8 | — | 40 | doorways; they hit like a truck |
| Uggardian | 235 | 500 | 5 | +10 | — | **cold**, and Fire Shield |
| Ice Lizard | 650 | 675 | 5 | +10 | 40 | **fire**, and Frost Shield |
| Ogre | 700 | 750 | **17** | −10 | — | Mace Master or Piercing Attack — 17 protection is a wall |
| **Warden** | **1200** | 750 | **20** | −10 | — | everything you have |
| Goromorg | 400 | **1000** | 0 | — | 40 | breaking their shield first |

Reading the table: the Goromorg gives more experience than the Warden despite a third of the health,
because experience in Grimrock tracks danger rather than durability — Goromorgs shield themselves and
hit for 40. The Ogre's 17 protection and the Warden's 20 are the two places where a Mace user's
armour-ignoring mastery, or Assassination's Piercing Attack (−10 to target protection), stops being
a nice-to-have.

---

## 7. Survival: food, light and resting

**Resting is the only healing.** It costs food and it can be interrupted. Rest to full before opening
a door you suspect, never after every skirmish.

**Food consumption** scales with the race's own `foodRate`, is cut 25 % by the Endurance talent
(Athletics 10), and rises while Burdened. Starving halves Attack Power and stops resting from
healing anything at all, which is a death spiral — eat before the bar empties, not after.

**Encumbrance** is `3 × Strength` kilograms of capacity, plus 15 kg with the Porter talent
(Athletics 20). At **85 % of capacity you become Burdened** (slower, hungrier); over capacity you are
Overloaded and cannot move. Drop the rocks.

**Light.** Torches burn down and are finite. The Light spell (Spellcraft 5, `B E`, 25 energy) is the
answer, and the reason to rush Spellcraft to 5 on your mage. Darkness (`E H`) exists for the few
puzzles that want it.

**Health and energy regenerate** slowly on their own — but **not at all while Diseased or Starving**.
Cure disease before resting or you will waste the food.

---

## 8. The thirteen levels

| # | Name | What it is for |
| --- | --- | --- |
| 1 | Into the Dark | Tutorial. Learn the waltz on snails and herders. |
| 2 | Old Tunnels | First real fights; first pressure-plate puzzles. |
| 3 | Pillars of Light | Skeletons. Bring a mace. |
| 4 | Archives | The biggest scripted level in the game — it carries twice the script content of any other. Expect puzzles rather than fights. |
| 5 | Hallways | Open space; ranged enemies punish standing still. |
| 6 | Trapped | Pits and traps. Read the floor. |
| 7 | Ancient Chambers | Uggardians. Cold damage and Fire Shield. |
| 8 | The Vault | Loot and a lock. |
| 9 | Goromorg Temple I | Shielded casters; break the shield, then burst. |
| 10 | Goromorg Temple II | More of the same, harder. |
| 11 | The Tomb | Undead in numbers. |
| 12 | The Prison | The Warden. 1200 health, 20 protection — bring every consumable. |
| 13 | The Cemetery | The end. |

All thirteen are 32×32 tiles. The level names and dimensions are exact; the per-level advice is
generalised from the enemy roster and the amount of script attached to each level, not from a
walkthrough.

### Secrets

The end-of-game statistics track **secrets found, treasures found, Toorum's notes found, skulls
found and iron doors opened** as separate counters, so a completionist run has five different things
to sweep for. Toorum's notes are scattered lore items; skulls matter mechanically if you have a
Minotaur with Head Hunter (+3 Attack Power per skull carried), which turns a collectible into a
build.

---

## 9. Twelve things worth knowing

1. **Take Skilled on everyone.** Three skill points at level 1 is worth more than any attribute pair.
2. **Athletics 10 (Endurance) early.** A quarter less food is a quarter more resting for the whole
   run.
3. **Spellcraft 5 before anything else on the mage.** Light ends the torch economy.
4. **Armors 8 then 16.** Armour without proficiency is a penalty, not a bonus.
5. **Commit to one weapon skill per character.** Milestones, not linear scaling, are where the value
   is.
6. **Assassination 12 gives a back-row character a melee attack.** It is the most build-changing
   milestone in the game.
7. **Divide attack power by cool-down before you swap weapons.** The Longbow beats the Crossbow; the
   Cutlass often beats heavier swords.
8. **Maces ignore armour at 50.** Against the Ogre (17 protection) and the Warden (20), that is the
   difference between a fight and a stalemate.
9. **Lurker armour on the back row, plate on the front.** 1.75 kg for Evasion +20 versus 34 kg for
   Protection +48.
10. **Poison Cloud costs one rune and 17 energy.** Cast it into the corridor you just retreated down.
11. **Rest to full before a suspicious door, not after every fight.** Food is the real difficulty
    setting.
12. **Throw everything.** Rocks, knives, shuriken, axes — a back-row character with Throwing Weapons
    contributes from turn one and never runs out of ammunition it cannot pick back up.

---

## 10. If you use the trainer

The trainer in this folder edits the live game. A few notes on using it without spoiling the game for
yourself:

- **Give skill points rather than setting skill levels.** Spending a point on the character sheet
  makes the game apply every milestone bonus — the +1 Strength, the +10 Health, the talent. Writing
  a skill level directly moves the number and nothing else, so the character ends up weaker than a
  legitimately trained one at the same level.
- **"Heal + restore energy" writes the champion's own maximum**, so it cannot leave a bar drawn past
  the end of its track. It is the safest button in the trainer.
- **Editing Health or Energy raises the maximum to fit, but never lowers it.** Typing a small number
  into the Value cell drops your current health without throwing away the maximum you earned — which
  matters, because Grimrock autosaves and nothing in the game could put it back.
- **"Cure" clears poison, disease, paralysis, blindness, curse, slow and starvation.** Burdened and
  Overloaded come straight back — the game recomputes them from carried weight every frame — so if
  you want those gone, drop something.
- **Reveal-map fills in the automap only.** Each tile's four sides are marked as walls only where
  the neighbouring tile really is one, so the map shows the floor plan rather than a box around every
  square. It does not open secret doors, it does not trip pressure plates, and it will not tell you
  where the puzzles are.
- **Level travel is not offered.** Use the stairs; the trainer explains why in its tooltip.
- The game has its own developer console (`console = true` and `consoleKey` in `grimrock.cfg`) with
  `gainExp`, `teleport`, `learnTalent`, `skipLevel` and `getStuff`. For the effects the trainer
  deliberately does not implement, that is the right route.
