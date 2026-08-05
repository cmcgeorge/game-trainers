# The Quest — strategy guide

The Quest (Redshift, 2006) is a first-person, grid-and-turn RPG: you move one square at a time, you
turn in ninety-degree steps, and when something hostile is in front of you the world waits for you to
act. It is generous with systems and stingy with explanations. This guide is about the systems.

Almost everything with a number in it below was read out of the game's own tables rather than
estimated — the skill list and its governing attributes, the resistance caps, the reputation ladder,
the experience curve — so you can plan against it. Where something is judgement rather than data, it
says so.

---

## 1. The five things that decide your game

1. **Your race** is permanent and it is a large modifier, not a flavour choice.
2. **Your class** is only a label for *which six skills are "primary"*. Primary skills are cheaper to
   raise; nothing is forbidden.
3. **A skill can never exceed twice its governing attribute.** This is the single most important
   rule in the game and it is easy to miss.
4. **Levels come slowly and then stop mattering.** The curve is brutal past level 20.
5. **Money is a skill, not a reward.** Appraise and Persuasion move prices in both directions.

---

## 2. Character creation

### Races

Five playable races, and the differences are real. The racial ability is listed on the *Abilities*
tab of the character screen once you are in the game — read yours, because it explains most of the
gap between the numbers you think you have and the numbers on screen.

| Race | Character |
|---|---|
| **Rasvim** | The undead. High poison and infection resistance, vulnerable to spell damage. The only race that can learn **Undead Magic** — and the only one that **cannot** learn Healing Magic. Can eat rotten food and drink contaminated water, which quietly removes a whole logistics problem. |
| **Etherim** | Natural hunters: high Dexterity, good with ranged weapons, with an innate aptitude for magic. |
| **Seiry** | The best natural thieves and assassins. |
| **Derth** | Natural mages. Highest magic resistance in the game and a large Intelligence bonus, paid for with Strength, Dexterity and Endurance. Glass cannons. |
| **Nogur** | Battle-hardened warriors — the sturdy melee choice. |

A worked example of what "a large modifier" means. A Derth's racial ability is:

```
-5 Strength   -5 Dexterity   -5 Endurance   +10 Intelligence
+10 Healing Magic   +10 Mind Magic   +10 Attack Magic
```

With every base attribute at 23, that character's *screen* reads Strength 18, Intelligence 33. Both
numbers are real: damage uses 18, mana uses 33.

**Two consequences worth planning around.** The racial skill bonus is added on top of the cap, so a
Derth with Intelligence 23 is capped at base 46 in Attack Magic but *plays* at 56. And the racial
attribute penalty lowers everything derived from that attribute — a Derth's carry weight and melee
damage are genuinely worse, not cosmetically worse.

### Classes

Six: **fighter, thief, mage, battlemage, ranger, priest**. The class picks your six *primary*
skills — the ones shown at the top of the Skills screen. Everything else is an "other" skill you can
still raise, just less cheaply.

The class is not shown anywhere on the status screen after creation, so decide deliberately: you are
choosing which six skills you intend to live on.

### Attributes

Five, and the game's own descriptions are unusually precise about what each one touches:

| Attribute | What it actually affects |
|---|---|
| **Strength** | Melee damage; encumbrance. |
| **Dexterity** | Melee *and* ranged damage; armor; encumbrance. |
| **Endurance** | Health; resistances; encumbrance. |
| **Intelligence** | Mana; magic and paralysis resistance; the number of positions **Mark** can hold. |
| **Personality** | Item prices; paralysis resistance; how much others like you. |

Note how much work Dexterity does — damage from two weapon families, armor, *and* carry weight — and
that three separate attributes feed encumbrance. Note also that Personality is not a dump stat unless
you intend to pay full price for everything forever.

**Health and mana maxima are not stored anywhere.** The game recomputes them from Endurance and
Intelligence and your level, every frame. That is why there is no "set max HP" anywhere, including in
the trainer: raise the attribute instead.

---

## 3. The doubling rule

> *"The base value of a skill cannot be higher than double the base value of its governing
> attribute."*

Written out: with Dexterity 20, Lockpick stops at 40 no matter how many skill points you have. The
skill points do not go away, they simply cannot be spent.

This is why attribute points are scarcer and more valuable than skill points, and why a build that
spreads attributes thin ends up unable to spend anything. The practical rule: **raise the attribute
first, then spend skill points into the headroom it opened.**

Here is the whole map, so you can see how lopsided it is:

| Governing attribute | Skills it caps |
|---|---|
| **Dexterity** (8) | Block, Light Weapon, Dual Wield, Light Armor, Accuracy, Repair, Lockpick, Stealth |
| **Intelligence** (7) | Healing Magic, Protection Magic, Attack Magic, Undead Magic, Environment Magic, Alchemy, Disarm |
| **Personality** (3) | Mind Magic, Appraise, Persuasion |
| **Strength** (1) | Heavy Weapon |
| **Endurance** (1) | Heavy Armor |

Strength and Endurance gate exactly one skill each. They are still worth having — Strength is melee
damage and Endurance is your health pool — but if you are choosing where the *next* point goes and
you want more things unlocked, Dexterity and Intelligence do far more work.

Note the two surprises: **Disarm is Intelligence**, not Dexterity, and **Mind Magic is Personality**,
not Intelligence. A mage who dumped Personality has a Mind Magic ceiling they were not expecting.

---

## 4. The twenty skills, and what each is for

Primary skills sit at the top of the Skills screen; the rest are "Other". These are the game's own
descriptions, trimmed.

**Fighting**

| Skill | Governed by | Effect |
|---|---|---|
| Light Weapon | Dexterity | Damage with light weapons. |
| Heavy Weapon | Strength | Damage with heavy weapons. |
| Dual Wield | Dexterity | Damage while wielding a weapon in both hands. |
| Accuracy | Dexterity | Damage with ranged weapons. |
| Block | Dexterity | Extra defence against physical attacks **while wielding a shield**. |

**Armour**

| Skill | Governed by | Effect |
|---|---|---|
| Light Armor | Dexterity | The armor value provided by Light Armor items. |
| Heavy Armor | Endurance | The armor value provided by Heavy Armor items. |

Armour skills scale the armour you are *already wearing*. A high Heavy Armor with nothing heavy
equipped does nothing at all — this is the classic wasted-points mistake.

**Magic**

| School | Governed by | Effect |
|---|---|---|
| Attack Magic | Intelligence | Damage spells. |
| Healing Magic | Intelligence | Healing spells. **Rasvim cannot learn it.** |
| Protection Magic | Intelligence | Protective spells. |
| Environment Magic | Intelligence | Environmental spells — including Mark and Recall. |
| Mind Magic | **Personality** | Mind spells: paralysis, curse, persuasion-adjacent effects. |
| Undead Magic | Intelligence | Undead spells. **Only Rasvim can learn it.** |

**Trades and roguery**

| Skill | Governed by | Effect |
|---|---|---|
| Alchemy | Intelligence | Chance to create potions, and to recognise ingredient effects. |
| Repair | Dexterity | Effectiveness of repairing items, and how fast repair hammers wear out. |
| Appraise | Personality | Prices, buying **and** selling. |
| Persuasion | Personality | Chance to persuade in dialogs. **Also influences prices.** |
| Lockpick | Dexterity | Chance to open locked doors and containers. |
| Disarm | **Intelligence** | Chance to disarm traps on doors and containers. |
| Stealth | Dexterity | Chance to steal from passers-by; may affect breaking into places. |

**Appraise and Persuasion stack on price.** If you intend to fund yourself through trade rather than
loot, those two Personality skills are the investment, and Personality is the attribute that raises
both ceilings at once.

**Lockpick without Disarm is a trap, literally.** The game warns you separately: *"The door seems to
be trapped. Your disarm skill may not be enough."* Opening a trapped lock you cannot disarm is how
low-level characters die in corridors.

---

## 5. The experience curve

Read out of the game's own table. Thresholds are cumulative experience needed to *reach* that level.

| Level | Total XP | Level | Total XP | Level | Total XP |
|---|---|---|---|---|---|
| 2 | 400 | 12 | 60 000 | 22 | 1 150 000 |
| 3 | 900 | 13 | 90 000 | 23 | 1 410 000 |
| 4 | 1 500 | 14 | 130 000 | 24 | 1 700 000 |
| 5 | 2 500 | 15 | 180 000 | 25 | 2 020 000 |
| 6 | 4 000 | 16 | 240 000 | 30 | 4 130 000 |
| 7 | 7 000 | 17 | 320 000 | 40 | 11 230 000 |
| 8 | 11 000 | 18 | 420 000 | 50 | 23 150 000 |
| 9 | 17 000 | 19 | 570 000 | 60 | 41 770 000 |
| 10 | 25 000 | 20 | 730 000 | 80 | 107 110 000 |
| 11 | 40 000 | 21 | 920 000 | 99 | 215 990 000 |

The table has 98 entries, so **99 is the ceiling**.

What this shape means in practice: levels 1–10 arrive quickly and each one matters enormously.
Levels 11–20 take roughly as long as everything before them put together. After about level 25 the
curve stops being a plan and starts being a side effect of playing — a level costs more than the
entire first act. If you are still hoping to "level into" a fight at that point, you want gear,
potions and positioning instead.

---

## 6. Combat

Turn-based on a grid, one enemy in front of you at a time. The rhythm is: step in, act, step back if
the exchange is going badly. Because the world only advances when you do, **retreating costs nothing
but a turn**, and a corridor you can back down is worth more than any single item.

Things the game will tell you only once:

- **`You carry way too much - you can't move.`** Encumbrance is a hard stop, not a penalty. Three
  attributes feed it (Strength, Dexterity, Endurance), and the *Feather* spell and feather potions
  exist precisely for the trip home from a good dungeon.
- **`Paralyzed: you cannot attack or move.`** and **`Trying to attack will skip your turn.`**
  Paralysis is the most dangerous status in the game because it hands the initiative over completely.
  Paralysis resistance comes from **Intelligence and Personality** — an odd pair, and a reason not to
  leave Personality at its starting value.
- **`Cursed: your attack power has been reduced.`** Curse is a timed debuff; it ticks down in turns,
  so disengaging works.
- **`Poisoned: -N health per turn, until cured.`** Poison does **not** time out. It runs until cured,
  and it makes resting lethal. Carry a cure.
- **`Attacking is forbidden on this map.` / `Magic is forbidden on this map.`** Towns and certain
  interiors. Do not plan an escape that needs a spell.
- **`You've tried to attack peaceful people!`** and *"it looks peaceful, maybe it has something to
  say"* — some things that look like monsters are quest-givers. Attacking them is how you close a
  quest line you have not opened yet.

Resistance caps, from the status screen: **magic 80%**, **poison 95%**. Paralysis and disease have no
stated cap. Endurance raises resistances generally; Intelligence raises magic and paralysis.

---

## 7. Magic

Twenty-eight spells ship in the base game. The full list, from the game's own resource table:

**Attack** — Magic Missile · Burning Hand · Fireball · Lightning Bolt · Lightning Storm · Poison
Cloud · Poison Touch · Smite Foe · Damage Living · Harm Undead

**Undead (Rasvim only)** — Drain Health · Mass Drain Health · Drain Touch · Undead Curse · Unholy
Word · Unholy Pray · Plague · Infestation

**Healing / protection** — Cure Light Wounds · Stoneskin · Warrt

**Mind** — Curse · Thief Touch

**Environment / utility** — **Mark** · **Recall** · Feather · Enchant · Recharge Wand

Four of those are worth calling out:

- **Mark and Recall** are the game's fast travel. Mark a place, Recall to it later. **Intelligence
  sets how many positions Mark can hold** — that is what the attribute description means by "the
  maximum number of positions for Mark". This pair saves an enormous amount of ship fare and walking,
  especially between Freymore and the Islands of Ice and Fire.
- **Feather** is the answer to encumbrance, not a curiosity.
- **Enchant** and **Recharge Wand** are how a caster funds themselves late (see §8).
- **Poison Touch / Poison Cloud** apply the same never-times-out poison you hate receiving.

Wands and scrolls are separate item categories with their own effect table, so a character with no
magic skill at all can still carry a wand. A **wand of drain mana** is the classic way to refill a
caster mid-dungeon.

Mana costs are shown as `Costs N sp` on the spellbook screen. `You don't have enough mana.` is the
whole failure mode; there is no partial cast.

---

## 8. Money

There are five reliable incomes and only one of them is looting.

1. **Prices.** Appraise and Persuasion both move buy *and* sell prices, and Personality raises the
   ceiling on both. A trade-focused character makes more money standing still than a fighter makes
   clearing a dungeon.
2. **Alchemy.** Ingredients are cheap and stackable; potions are not. The alchemy screen refuses with
   *"Your alchemy skill seems too low to create this potion"*, so it is a skill gate rather than a
   luck gate. Feather potions and fortify-melee potions are the commonly recommended profit lines —
   things every adventurer needs and nobody wants to walk back to town for.
3. **Repair.** Buy damaged gear cheap, repair it, sell it whole. Repair also governs how fast your
   hammers wear out, so the skill pays for its own tools. Item condition runs
   `broken → … → average → … → perfect`.
4. **Enchanting.** Items carry an enchantability grade — `Unenchantable`, `Slightly`, `Moderately`,
   `Averagely`, `Strongly`, `Superiorly enchantable`. A strongly-enchantable base item plus the
   Enchant spell is the most valuable thing you can manufacture.
5. **Stealing.** Stealth governs pickpocketing (`Do you try to steal (chance: N)?`). It is real
   income and it is also the fastest way to acquire a crime record — see §9.

The card game in taverns is a sixth, streakier income. You bet gold, you and your opponent each play
as a **warrior**, **sorcerer** or **necromancer**, and the deck is a genuine little combat game with
damage, minions, auras and markers. It is not a coin flip: the decks have structure and knowing them
is worth money.

Inn rooms cost gold (`Do you want to rent a room for N?`), and food and water are consumables you
must actually carry.

---

## 9. Crime, fame and the law

Two independent numbers.

**Crime** is a bounty. It accumulates when you are caught doing something the game considers a crime
and it is what guards collect. Left to grow, it ends with `You are being transported to the prison
of %s` — or, if there is no prison nearby, `There is no prison nearby - you are free - at least for
the time being.` Serving the sentence costs you skill levels:

> `You have served your sentence. Your <skill> and <skill> skill have been decreased.`

That is a permanent loss of trained skill. Crime is the one status worth clearing proactively.

**Fame** is reputation, a signed value from −100 to +100, and the game maps it onto a ladder:

| Fame | Word |
|---|---|
| +100 | Saint |
| +80 … +99 | Blessed |
| +50 … +79 | Blameless |
| +20 … +49 | Virtuous |
| +1 … +19 | Good |
| 0 | Neutral |
| −1 … −19 | Immoral |
| −20 … −49 | Corrupt |
| −50 … −79 | Evil |
| −80 … −99 | Pure evil |
| −100 | Demonic |

Only exactly ±100 earns the extreme title, which is a nice detail: "Saint" is a real achievement
rather than a band.

Your **outfit** is a third, separate impression score, summed from what you are wearing:

`Threadbare (0–10) · Shabby (11–20) · Plain (21–40) · Regular (41–60) · Dressy (61–80) ·
Well dressed (81–90) · Fashionable (91–95) · Swell (96+)`

Since Personality already affects "how much the character is liked by others", dressing well is a
cheap supplement to a social build — and it costs nothing but wearing your good clothes into town.

---

## 10. Resting, food and disease

Resting is how you recover, and the game refuses it more often than it allows it:

- `There are monsters around - you cannot rest here.`
- `You cannot rest here.` — some maps simply forbid it.
- `You need food and water to rest.` — both, as items.
- `You are poisoned - resting would be lethal.`
- `You are seriously diseased - resting would be lethal.`

**As a Rasvim:** `As an undead, you can eat rotten food and drink contaminated water.` Everyone else
gets `Rotten food and contaminated water don't apply.` That is a standing logistics advantage worth
more than it sounds on a long dungeon.

Diseases are their own system with their own resistance and their own cures; the status screen lists
active ones under *Diseases* and the *Active effects* tab breaks down every temporary and permanent
modifier currently on you. Check that tab when a number does not match your expectation — it is
where the game explains itself.

One more limit that catches people out:

> `You cannot drink more potions with permanent effects until you level up.`

Permanent-effect potions are rationed **per level**. Do not save them all up expecting to drink them
in one sitting.

---

## 11. A dozen things worth knowing

1. Raise the attribute before the skill. The doubling rule wastes skill points otherwise.
2. Dexterity unlocks eight skills. Intelligence unlocks seven. Strength and Endurance unlock one
   each.
3. Disarm is Intelligence. Mind Magic is Personality. Both surprise people.
4. Armour skills multiply the armour you are wearing. They do nothing on their own.
5. Poison never expires. Curse and paralysis do.
6. Poison or serious disease makes resting lethal — cure first, then camp.
7. Mark/Recall pays for itself many times over. Intelligence buys you more Mark slots.
8. Feather solves encumbrance; encumbrance is a hard stop, not a slow-down.
9. Peaceful-looking monsters are usually quest content. The game warns you before you swing.
10. Serving a prison sentence permanently lowers skills. Pay or clear the bounty first.
11. Permanent-effect potions are one per level.
12. Autosave is aggressive. If you want a rollback point, make a manual save in a numbered slot.

---

## 12. If you use the trainer

`TheQuestTrainer` edits the live character record. A few things follow from how the game is built —
see `ReverseEngineering.md` for the details:

- **Attributes and skills in the trainer are *base* values.** The game's screens add racial and
  equipment modifiers on top. Setting Attack Magic to 40 on a Derth shows 50 in the game. That is
  correct, not a bug.
- **There is no maximum health or maximum mana to set**, because the game does not store them. Raise
  Endurance or Intelligence and the maxima follow. You *can* set current health above the maximum —
  the game will cheerfully display `500/72` — and freezing it there is as close to god mode as this
  game has.
- **Setting the level does three writes**, because the game caches the next-level threshold rather
  than recomputing it. The trainer writes level, raises experience to that level's floor, and
  rewrites the threshold from the record's own table, so the character stays consistent.
- **Setting experience alone does not level you up.** The game applies levels when it next awards
  experience. Use the level field if you want the level now.
- **"Max skills" respects the game's own ceiling** — twice the governing attribute — and skips the
  two race-locked schools. Raise the attributes first if you want a higher ceiling.
- **Crime is worth clearing** rather than freezing, unless you plan to keep committing crimes; a
  frozen crime of zero makes the law permanently uninterested in you.

If you are here to *play* rather than to sightsee, the least destructive intervention is usually a
modest gold top-up and clearing crime. Freezing health removes the game's only real pressure, which
is a choice, but it is a large one.
