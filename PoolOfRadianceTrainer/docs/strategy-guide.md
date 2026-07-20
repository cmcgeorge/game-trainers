# Pool of Radiance — Strategy Guide

A complete guide to **Pool of Radiance** (SSI, 1988), the first AD&D "Gold Box" CRPG, set in
the ruined city of **Phlan** on the Moonsea. Covers party creation, mechanics, combat tactics,
a district-by-district walkthrough with grid maps, the wilderness, and how to win.

Coordinate convention throughout: maps are **16×16** (districts) or **18×20** (some late areas),
`(x, y)` = **(column, row)**, origin **(0, 0) at the top-left / north-west**, x increases east,
y increases south. Map legend: `#` wall · `+` door/archway · `~` illusory wall (or water/pool per
that map's key) · `"` hedge · `'` rubble · `E` entrance/exit · `T`/`TP` teleporter · numbers are
keyed locations.

> Sourcing note: gameplay facts are drawn from the game Rule Book, Stephen S. Lee's code-level
> "Exhaustive Game Information" FAQ, GameBanshee, the CRPG Addict's playthroughs, and oldgames.sk
> maps. A few exact reward numbers vary between the DOS original and the NES port; where they
> conflict the DOS figure is preferred.

---

## Contents

1. [The story & goal](#1-the-story--goal)
2. [Party creation](#2-party-creation)
3. [Core mechanics](#3-core-mechanics)
4. [Combat tactics](#4-combat-tactics)
5. [Spells that win fights](#5-spells-that-win-fights)
6. [Economy & city services](#6-economy--city-services)
7. [Recommended order](#7-recommended-order)
8. [Walkthrough — the districts of Phlan](#8-walkthrough--the-districts-of-phlan)
9. [The wilderness](#9-the-wilderness)
10. [The endgame — Stojanow Gate & Valjevo Castle](#10-the-endgame--stojanow-gate--valjevo-castle)
11. [Appendix — XP tables & notable items](#11-appendix--xp-tables--notable-items)

---

## 1. The story & goal

Phlan, "the Jewel of the Moonsea," has fallen. Fifty years ago a monster army led by a possessed
dragon overran it; only a small **civilized quarter (New Phlan)**, governed by a **City Council**,
still stands. You are adventurers hired by the Council to reclaim **Old Phlan** one ruined district
at a time, and to find and destroy the mysterious "**Boss**" behind the occupation.

The Boss is **Tyranthraxus, "the Flamed One"** — a body-hopping possessing spirit currently
inhabiting an ancient **bronze dragon** in **Valjevo Castle**, drawn to the deceptive **Pool of
Radiance** (which he advertises as a fountain of power to lure and possess the strong; it is really
a gate to his home plane). You win by killing the dragon body: the spirit is expelled and drawn
back down through the draining Pool, and Phlan is freed.

---

## 2. Party creation

You control **up to 6 characters** (plus up to 2 temporary NPC slots).

**Races** — class options and the caps that matter *in this game*:

| Race | Can be | Perks |
|---|---|---|
| Human | Fighter, Cleric, Mage, Thief (single-class only) | No level limits; the only race that reaches Cleric 6. Best for importing through the whole 4-game saga. |
| Elf | Fighter, Mage, Thief; F/M, F/T, M/T, F/M/T | +1 DEX, infravision, 90% sleep/charm resist, finds secret doors. Cannot be a cleric. |
| Half-Elf | Fighter, Mage, **Cleric**, Thief; widest multiclass incl. **C/F/M** | 30% sleep/charm resist. The premier multiclass race. |
| Dwarf | Fighter, Thief; F/T | +1 CON, infravision, magic resistance, combat bonus vs goblinoids. No magic. |
| Gnome | Fighter, Thief; F/T | Magic resistance; bonus vs kobolds/goblins. |
| Halfling | Fighter, Thief; F/T | +1 DEX, magic resistance; no thief level limit. |

**Classes** — hit die, prime stat, and training-hall cap: Fighter d10/STR/**8**, Cleric d8/WIS/**6**,
Magic-User d4/INT/**6**, Thief d6/DEX/**9**. Only the Fighter can roll exceptional Strength 18/01–18/00.

**Recommended party:** two front-line fighters, two clerics, a thief (or Fighter/Thief), and at
least one magic-user. A strong, flexible six: Human Fighter · Elf Fighter/Mage · Half-Elf
Fighter/Cleric · Dwarf Fighter/Thief · Human Cleric · Human Mage. (The bundled sample party —
Thrender the dwarf fighter, Bakshi the half-elf C/F/M, Rhiannon the elf F/M, Brother Sean the human
cleric, Darkstar the human mage, Phineas the halfling thief — is exactly this template.)

**Rolling:** pick gender first (a male human fighter can reach STR 18/00 for +3 to hit / +6 damage;
female fighters cap at 18/50). Re-roll for a high prime stat, then use **Modify Character** to max
your prime requisite and **starting HP** — neither can be improved later. Multiclass HP is averaged
and XP is split, so multiclass characters are sturdier early but level more slowly.

**Alignment** barely matters mechanically; Lawful Good is a safe default (thieves can't be LG).

---

## 3. Core mechanics

- **Descending AC & THAC0** — *lower is better* for both. You hit if `1d20 ≥ (your THAC0 − target AC)`.
  Base AC 10 = unarmored; armor and magic lower it. No critical hits.
- **Training to level** — leveling is **not** automatic. Earn XP in the field, then return to the
  class **Training Hall** in New Phlan and pay **1,000 gp (= 200 pp)** per level. You gain only **one
  level per visit**; excess XP over the next threshold is shaved to 1 short (the classic fix is to
  brawl in a tavern to earn it back).
- **Gold ≈ XP** — treasure is converted to experience roughly 1 gp = 1 XP and is usually a *bigger*
  XP source than kills. Any magic item is worth ~400 XP per "+".
- **You only get XP for kills** — enemies that flee, surrender, or are charmed give nothing.
- **Vancian spells** — memorize only while **Encamped**; casters must rest to reload. A magic-user
  learns one new spell per level plus any scribed from found scrolls (100% success in PoR).
- **Coin weight** — 10 coins = 1 lb, and *every* coin counts. Over ~350 coins-equivalent your
  movement drops (12 → 9 → 6 → 3 squares). Convert up to platinum/gems, or just don't loot coins
  (you keep the XP whether you pick them up or not).
- **No food/rations** — flavor only; there is no hunger or starvation. Resting costs only time.
- **Raise Dead** costs a lot of gold **and permanently −1 Constitution**.

---

## 4. Combat tactics

- **Sleep is king early** — 4d4 hit-dice of creatures under 6 HD, **no saving throw**, and a
  sleeping enemy dies to a single hit. Aim it *behind* the enemy front rank so you don't catch your
  own fighters. It stops working on ogres / level-4 fighters and up.
- **Hold Person & Stinking Cloud** lock down humanoids; a held/nauseated foe is auto-killed. The AI
  refuses to walk into a Stinking Cloud, so drop it in a doorway to wall off a horde.
- **Fireball** clears packed groups (uncapped `(level)d6`, save for half). **Lightning Bolt** bounces
  off walls — great down a corridor, dangerous toward your own party.
- **Hold the line** — form a fighter wall anchored to walls so nothing gets behind you. Moving away
  from an adjacent enemy grants it a big free attack; use **Guard** to get the free swing instead.
- **Concentrate fire** — a wounded enemy hits as hard as a healthy one; kill things outright.
- **Interrupt casters** — even 1 point of damage cancels an enemy's spell that round.
- **Ranged is extra actions** — bows fire twice a round, darts three times. Open at range.
- **Trolls** regenerate and rise from death: stand a character on the corpse's square, or kill fast.
- **Level-drain undead** (wights, wraiths, spectres, vampires) need magic weapons; carry
  **Restoration scrolls** and let an already-capped character soak the drain.

---

## 5. Spells that win fights

**Magic-User:** *Sleep* (L1, the early game), *Magic Missile* (L1, unerring), *Stinking Cloud* (L2),
*Invisibility* / *Mirror Image* (L2 defense), *Knock* (L2, opens sealed doors), *Fireball* &
*Lightning Bolt* (L3, area damage), *Haste* (L3, doubles attacks — cast before the boss),
*Enlarge* (buffs a fighter's Strength). All PC mages begin knowing Detect Magic, Read Magic, Shield,
Sleep.

**Cleric:** *Cure Light Wounds* (L1, the **only** castable heal — keep several ready), *Bless* /
*Prayer* (party buffs), *Protection from Evil*, *Hold Person* (L2), *Dispel Magic* (counters charm/
hold), *Resist Fire* (halves the final dragon's aura), *Animate Dead* (raise fallen foes as allies).

---

## 6. Economy & city services

- **Shops** (open daytime only) — Buy/Sell/View/**Appraise**/**ID**. Magic items show only an
  asterisk; pay **200 gp to ID** an item's real enchantment, or **Appraise** gems/jewelry to sell.
  Equipping an unidentified item to test it is risky (cursed items can't be removed without Remove
  Curse). Save first.
- **Training Halls** — 1,000 gp per level, one level per visit (§3).
- **Temples** (Tyr / Sune / Tempus, in the corners of New Phlan) — expensive Cure Serious/Neutralize
  Poison/Cure Disease/Remove Curse/Raise Dead. Self-heal with clerics whenever you can.
- **The Pit / taverns** — rumors, gambling (~50/50), and brawls (a little XP; also the "regain 1 XP
  to re-train" trick).
- **City Hall** — the **Clerk of the City Council** issues **Commissions** (the quests) and pays gold
  + platinum + XP on completion; wall **proclamations** give coded clues you decode with the
  Adventurer's Journal.
- **POOL / SHARE / TAKE** — pool all money into one purse (e.g. to buy from a shared fund), share it
  back out evenly, or concentrate it on one carrier (a "mule"). There is no bank.

---

## 7. Recommended order

**Slums → Sokal Keep → Kuto's Well → Podol Plaza → Cadorna Textile House → Mendor's Library →
Kovel Mansion → Wealthy District → Temple of Bane → (wilderness quests) → Valhingen Graveyard →
Valjevo Castle.**

You *can* march to the final boss whenever you like — Pool of Radiance is the only Gold Box game
that lets you — but you'll want levels 6–8 and magic gear first. Rough level pacing: Slums at
level 1; the city districts take you to ~4–5; the wilderness and graveyard bring most characters to
their caps before the castle.

---

## 8. Walkthrough — the districts of Phlan

### 8.1 The Slums *(first commission — start here)*

Your first cleanup. Fight through goblins/kobolds/orcs; the marquee battle is a large group of
hobgoblins with a magic-user that drops the **Wand of Magic Missiles**, and the infamous
**ogres + trolls** fight in the SW corner (Sleep the ogres, use fire/oil on trolls, stand on their
corpses). Loot the **NW illusory-wall** cache (enter from the east). Talk to the mage **Ohlo** (13,10)
and run his Rope Guild errand (say "OHLO" to the guild merchant) for a monster-blasting necklace.

```
     0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15      ECL Script 20
   #################################################
 0 #12~        #        + 6   #     #        #     #  0
   ####  #++#++#        #     #     #        #     #
 1 #     #  #11#        #     #    2#       1+     #  1
   #++####  ####  ################++##########     #
 2 #10#           + 5  5#     +     +        +     #  2
   #  #  #######++####++#     ####++#######  #     #
 3 #  #  #    9#  #     +     #           #  #     #  3
   ####  ####++#  #     #     #           #  #######
 4 +E2      #     +     #     #           #  #   E1+  4
   #######  #++#  ##########++#++##########++#++####
 5 #  +14#  # 8#           #  #     #        #     #  5
   #  ####  ####     #######  #     #        #     #
 6 #  #     +        #     #  #     #        #     #  6
   #  ####++##########     #  #     #        #     #
 7 #13#        #     +     +  #     #        +     #  7
   #++#        #     ####  ####++###################
 8 #  +    7   +        #  #  #                    #  8
   #  #        #######  ####  #     ##########     #
 9 #  #        #     #        #     #        #     #  9
   #  ####++####     #        #     #        #     #
10 #        #  + 16  #E3      #     #       3+     # 10
   #        #  #     #++############################
11 +E2      +15#     #     #           #           # 11
   ###################  #############  #++#++#++####
12 #     #              #        #  +  +  #  #  +19# 12
   ####  #  ##########  #     #++#  ####  #  #  ####
13 #  #  #  #        #  #     #  #  +  #  #  +     # 13
   #  #++#++#++#  #++#  ####++#++#  ####  #  #     #
14 #20#        #  #           #        #  +  #     # 14
   #++#  ####++#  ##########++#######++#  #  #######
15 #     #              #     #           #        # 15
   #################################################
```
Key: **1**(13,1) orcs w/ scroll · **2**(10,1) goblins → Leather Armor +1 · **3**(13,10) mage Ohlo ·
**6**(7,0) kobolds → Bracers AC 6 · **9**(3,3) orc leaders → Chain Mail +1, Flail +1 · **10**(0,2)
hobgoblins → Ring of Protection +1 · **12**(0,0) illusory-wall treasure · **14**(1,5) monster leaders ·
**19**(15,12) Rope-Guild merchant (Ohlo's package) · **20**(0,14) 4 Trolls + 2 Ogres. **E1**(15,4)→New
Phlan, **E2** left→Kuto's Well, **E3**(6,10)→Rope Guild (automap off inside).

### 8.2 Sokal Keep *(opens the wilderness)*

Reach it by boat. **SEARCH** the dead elf at **(6,13)** for a rune scroll; decode it with the pack-in
code wheel to get **LUX / SHESTNI / SAMOSUD**. Give patrols **SHESTNI** inbound to skip fights. Parley
the crying elven ghosts (barracks, ~(6,2)) with **LUX** *before* dealing with Ferran, for a diary and
5 gems. Give the spectre **Ferran Martinez** (altar, ~(7,9)) **LUX**, then **tell the truth** to
complete the mission. **Never melee** Ferran or the spectres — 2-level drain. The big set-piece is an
orc/hobgoblin ambush (~31 orcs, 4 leaders, 15 hobgoblins); Sleep the archers. The NW armory has an
illusory-wall cache (Long Sword +1, Chain Mail +1, Mace +2, Shield +1).

```
     0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15      ECL Script 21
   #################################################
 0 #####################################17 14   +  #  0
   #####################################        ####
 1 #####################################14       13#  1
   ##############################################~~#
 2 #           #        +     +        +10         #  2
   #           #        #     #        #           #
 3 #           #    7   #     #        #           #  3
   ####        #        #     #        #        #++#
 4 ####        #16      # 9  9#        #        #  #  4
   ####     #############     #############     ####
 5 #        ####        + 9  9+        ####   10   #  5
   ####++#######        #++#++#        #######++####
 6 #        #           #11 11#           #    6 15#  6
   #        #        ####     ####        #        #
 7 #        #        #  #     #  #        + 6      #  7
   ####  #######     #~~#     #~~#     #######  ####
 8 ####  + 5   #     #           #     #######  ####  8
   ####  #     #     #  #######  #     #############
 9 ####  #     #     #           #     #######  ####  9
   ####  #######     #~~#     #~~#     #######  ####
10 #        #        #  #12 12#  #        + 4      # 10
   #        #        #############        #        #
11 #       8#                             #        # 11
   #######++####  ####           ####  #############
12 ####        +  ####           ####  +        #### 12
   ####        ##########+++++##########        ####
13 #              #### 1  2  2   ####              # 13
   #              ####           ####              #
14 #                 #           #                 # 14
   ####  #######  ####           ####  #######  ####
15 ####  #######  ####   E1 E1   ####  #######  #### 15
   ######################+++++######################
```
Key: **1**(6,13) dead elf / passwords · **12** altar / Ferran Martinez · **9** orc-hobgoblin ambush ·
**17**(12,0) armory illusory-wall cache · **8**(2,11) huge scorpions (poison — skip or Sleep).
**E1**(bottom) boat → New Phlan.

### 8.3 Kuto's Well

Force the door to free the **Wide-Eyed Woman** (banded mail +1, quarter staff +1, bracers AC 4);
she warns "an evil spirit from an unholy pool guides your enemies." Descend the **well (E4, 7,7)**
into the catacombs and defeat **Norris the Gray** (half-orc Fighter 5: Lizardman ×5 + Kobold Leader
×9); he drops a **Long Sword +1** and a note from the Boss. His hoard (NE of the catacombs) holds
~5,900 XP worth of gems/gold. Five kobolds at a table will, if parleyed twice, reveal the Textile
House boss's hiding spot. No formal commission, but clearing it earns a Council reward.

### 8.4 Podol Plaza *(auction commission)*

A spy mission: on entry choose **"disguise yourself as monsters"** to observe the **auction**. No
matter what you bid, the buyer **Garwin** (an agent of the Boss) casts darkness and escapes with the
item — witnessing it completes the commission (~1,200 XP). Duel the drunk buccaneer in **The Pit**
(2, 4,8) for a Long Sword +1 + Chain Mail +1. Kill the **orc priest of Bane** (6, 14,8) for **6 leather
holy symbols** you'll need to enter the Temple of Bane later. The hidden **Temple of Ilmater** (SW,
~1,15; Knock the doors) is a safe rest/heal.

### 8.5 Cadorna Textile House *(Cadorna's personal commission)*

Councilman **Cadorna** wants his family's **iron treasure box** and his missing man **Skullcrusher**.
Fight the hobgoblin priestess **Grishnak** (drops a **brass key** + scrolls) and free Skullcrusher (a
Level-4 fighter who joins). Behind an illusory east wall, an **ogre chief** guards the box, which
contains the **Gauntlets of Ogre Power** (sets STR to 18/00 — the best item in the game). A thief can
take the **"Thieves Only" well** (NW) to meet **Restal**, who sneaks you to the treasure.
**The seal dilemma:** opening the box breaks its seal and earns Cadorna's enmity (but more XP);
returning it sealed keeps him happy; Restal can re-forge the seal for a cut. Cadorna later betrays
you (§9, Zhentil Keep) and is finally hunted down.

### 8.6 Mendor's Library

Cast **Knock** to enter (the door won't reliably bash). Search the **Philosophy (2)** and
**History (4)** stacks for the journal books that reveal Tyranthraxus's origin. The **Rhetoric**
section (5) holds a **Basilisk** — **equip mirrors** to reflect its petrifying gaze — guarding a
**Cloak of Displacement** and **Restoration scrolls**. Under a floor jar (8, 8,11) are **3 Potions of
Extra Healing**; searching (13,13) yields the **Manual of Bodily Health** (permanent +CON, sells for
~25,000 gp). Leaving with any book triggers a **spectre** — hit it with a wand or magic weapons and
Protection from Evil. The **Mad Man** (11, 11,12) raves of "the castle of flowers on the hill" —
your first pointer to Valjevo Castle.

```
     0  1  2  3  4  5  6  7  8  9 10 11 12 13 14 15      ECL Script 15
   #############++###################++#############
 0 #            E1                   E1            #  0
   #                                               #
 1 #                        E3                     #  1
   #     ###################XX###################  #
 2 #     #  +  #        #        #        #  +  #  #  2
   #     #  ####  ####  #        #  ####  ####  #  #
 3 #     #  +  #  ####  #    9   #  ####  #  +  #  #  3
   #     #  ####  ####  #        #  ####  ####  #  #
 4 +E2   #  +  #    5   #        #    4   #  +  #  #  4
   #     #  #######++#######++#######++#######  #  #
 5 #     #                                      #  #  5
   #     ####++####++#++#++#  #++#++#++####++####  #
 6 #     #    3   #  #  #  #  #  #  #  #    2   #  #  6
   #     #  ####  ##########  ##########  ####  #  #
 7 #     #  ####  #       7+  + 6      #  ####  #  #  7
   #     #  ####  #        #  #        #  ####  #  #
 8 #     #        #        #  #        #        #  #  8
   #     ###################++###################  #
 9 #     #             1+        #14   #        #  #  9
   #     #              #######  #++####        #  #
10 #     #              #   15+10 10   +18      #  # 10
   #     #     ####     #######        #        #  #
11 +E2   #     #  #     #    8+        #        #  # 11
   #     #     ####     #     #        ##########  #
12 #     #              #     #      11+12      #  # 12
   #     #              #     #  #++####        #  #
13 #     #              #     #  #16   #   13   #  # 13
   #     ######################XX################  #
14 #                           E3                  # 14
   #################################################
```
Key: **2** Philosophy books · **4** History books · **5** Basilisk (mirrors!) · **8**(8,11) potions ·
**13**(13,13) Manual of Bodily Health · **11**(11,12) Mad Man · **18**(12,10) surrendering kobolds
(map). **E2**→Cadorna, **E1**→Kuto's Well, **E3** the library door (Knock).

### 8.7 Kovel Mansion *(thieves' guild)*

Traps everywhere — **move in Search Mode**, cast **Find Traps**, and disarm with a thief (bring
**Knock**; a mid-level or multiclass thief struggles with the locks). The only real entrance is the
double door at **(9,14)** from the north; the three west doors are fake. Thieves ambush and flee —
**don't chase**, clear room by room. Behind the deadliest trap at **(3,11)** is a weapons cache
(Short Sword +1, Hammer +2, etc.); four caskets at **(6,8)** hold 42 gems total; the guild leader
wears **Leather Armor +4**. Documents here reveal **"Cadorna employs thieves and mercenaries"** and
that the Boss is a **dragon in Valjevo Castle** (with a map of one hedge-maze quadrant).

### 8.8 Wealthy District & Temple of Bane

Endless-ish random battles (orcs/hobgoblins/ogres) for grinding. The **Temple of Bane** (enter using
the leather holy symbols from Podol Plaza) yields **Dust of Disappearance** — save it for the final
fight — plus a **Ring of Feather Falling** and other gear.

---

## 9. The wilderness

After clearing Sokal Keep, the boat also runs to the **overland Moonsea map**. Travel is square by
square with a 5% chance of an encounter per step; the *danger set* depends only on your X column
(Western X 2–15, Central 16–28, Eastern 29–41). Outdoor fights are large and choke-point-free —
avoid them unless you want the challenge.

```
                       1111111111222222222233333333334444  KEY:
     01234567890123456789012345678901234567890123
 0                                                         . plains
 1                                                         " swamp
 2 ...&&&^^&&&&&&&&&&&&&&&&..&&&&&&&&&&&&&&&&                + forest
 3 &&&^^^^^^^&&^^^^&^^&&&&..&&&&&&&&&&&&&&&&                & hills
 4 &^^^^^^^^^&&^^^^^^^^&&&&..&&&&&&&&&&&&&&                 ^ mountains
 5 ^^^^^^^^^&&&^^^^^^^^^&&&.&&&&&&&&&&&&&&&                 ~ river
 6 ^^^^^^^^&&&^^^^^^^^^^&&&..&&&&&&&&&&+&+&&&               = deep water
 7 ^^^^^^^^&^^^^^^^^^^^&&&&.&&&&&&&&+++++&&                 T teleporters (pyramid)
 8 ^^^^^^^^&&^^^^^^^^^&&&....&&&&&&+++m++&&
 9 ^^^^^^^^k&^^^^^^^^&&&&....&&&&&&+++++&&&
10 ^^^^^^^^&&^^&&&&&&.&&......&&&&&&+++&&&&
11 ^^^^^^^^&~&&&&&&++.....h....&&&&&&&&&&&&
12 ^^^^^^^^&&~~~~+++++....++......&&&&&&&&&
13 &&^^^^^&&&&&&&~+++++..++++.....&&&&&&&&&
14 &&&^^^&&&&&..++~+f+++.+++++...&&&&&&&&&&
15 .&&&&&.&&&..+++====++++++++..&l&&&&&&&&&
16 ..........+++++==g=.+++++++++.&~&&&&&&&&
17 .........+++++++===..+++++++++.~~&&&&&&&
18 .........+++++++.~~...++++++++..~~&&&&&&
19 ....""...++++++..~~...++++++++..&~&&&&&&
20 ....""....+++++..~....+++++++++.&~~&&&&&
21 ...."".....++++..~~...+++++++++.&~&&&&&&
22 ....""".....+++.++~~...+++++++&&&~&&&&&&
23 ....."".....++.+++.~...+++++++&&&~&..&&&
24 ....."j"......+++++~~...++++++&~~~&...&&
25 ....."""......+++++.~~~.+++++++~&...+++&
26 ....."".......++++++..~cc++++++~.++++++=
27 .....""........+++++++.ab.++.++~+++++===
28 .....""........+++++++d==.....+~~~~=====
29 ....."".........++e++++=========~e======
30 ....."".....==.=====+===================
31 ..........n=============================
32 .i........==============================
```
Landmarks: **a/b/c/d** city-edge squares back to Phlan · **e** boat landings · **f/g** rowboat to
**Yarash's Pyramid** on Sorcerer's Isle · **h** Nomad Camp · **i** Zhentil Keep Outpost · **k**
silver dragon **Diogenes** · **l** Kobold Caves · **m** Lizardman Keep · **n** Buccaneer Base.

**Optional wilderness quests worth doing before the endgame:**
- **Zhentil Keep Outpost (i)** — Cadorna's betrayal trap. Set a watch, survive the night ambush, and
  kill the **Commandant** for his **Javelin of Lightning** (a key weakness of the final dragon),
  Plate Mail +2, and a Ring of Fire Resistance.
- **Yarash's Pyramid (g)** — the mad wizard poisoning the Stojanow River. Skip the maze via
  teleporters (throw a rock through a portal to toggle its destination). The **color dial** at (5,0)
  sets the treasure teleporter: **Blue** = exit, **Copper/Silver/Gold** = three treasure rooms (3
  random magic items each). Password on level 3 is **NOKNOK**. Free the enslaved lizardmen ("be Nice")
  to get the friend-word **SAVIOR**, then kill **Yarash**.
- **Kobold Caves (l)** — enter the **Large** entrance. The throne room is 3 waves (heal but don't end
  each combat); the envoys drop **2× Two-Handed Sword +2**. Grab the **brass bottle** (Efreeti) at
  (12,0) — say "No" to keep the bottle (an ally in the vampire fight). Free **Princess Fatima** (1,3).
- **Lizardman Keep (m)** — an anti-magic zone. Give the old lizardman **SAVIOR** and champion him in a
  duel vs **Drythh** to secure the alliance without a bloodbath. Catacomb pools hide **3× Shield +2**.
- **Nomad Camp (h)** — defend against kobold waves alongside the nomads for **5,000 gp, a Two-Handed
  Sword +2, and a Wand of Magic Missiles**.
- **Silver dragon Diogenes (k)** — state your good intent; he sends you for the kobold bottle.

---

## 10. The endgame — Stojanow Gate & Valjevo Castle

### 10.1 Stojanow Gate

The fortified gate guarding the road to the castle. Buy the supply cart (**250 gp**) as a disguise:
the bugbear patrol takes **15 gp** and waves you through, letting you fight the two **guard towers**
(each a level-6 mage + aides + 3 ettins) separately. Without the disguise you bash both gates under
arrow/rock volleys and fight all the guards at once (2 mages + 6 ettins + fighters). Reward: up to
**3× Ring of Protection +2**. Use **Knock** to open the gate itself. (Note: the DOS game has *no*
"fire trap" or "flooded passage" here — those are NES-port inventions.)

### 10.2 Valjevo Castle

Four **18×20 quadrant** maps (SW entry, plus NW/NE/SE) ring a poisonous **hedge maze** around the
**Inner Tower** that holds the Pool. Get **disguises** from the **washerwomen** (SW #1). Passwords:
**HARASH** / **TYRANTHRAXUS** (checkpoints) and **RHODIA** (hedge-maze gate — learned from freed
slaves in NE, or from the imprisoned **Cadorna** in SE, whom you execute per the Council's writ).
**Do not steal from the Altar of Bane** or the whole castle turns hostile. Quadrant highlights:
Temple of Bane loot (Mace +3, Necklace of Missiles), a **Flame Tongue Long Sword +2** in the SE well
(15,10), a fake "Tyranthraxus" throne room (Long Sword +5, Ring of Protection +3, Gauntlets of Ogre
Power), and a capturable **mage** (NW #8) with a **Wand of Lightning**.

**The hedge maze** — each hedge you push through has a chance to kill a character (save vs poison or
201 damage), and teleporters (`TP`) shuffle you among the quadrants. Reach the Inner Tower stairs via
the NW quadrant (an **illusory wall** hides them) or the SE entrance.

### 10.3 The Inner Tower & Tyranthraxus

- **Medusa** (lower level) — **equip mirrors**; she has only ~30 HP, so hit her hard immediately.
- **Genheeris** — a level-7 mage who offers to join; take him for his **Wand of Lightning Bolt**.
- **The final confrontation** is two back-to-back battles with **no rest between**:
  1. **~12 eighth-level fighters** in Plate Mail +2 / Two-Handed Sword +2 / Ring of Protection +3
     (so you need magic weapons to hit them). Use **Hold Person** and **Stinking Cloud** to lock the
     group and kill them one-hit; **do not end the combat** until you've healed and buffed.
  2. **Tyranthraxus** in the bronze dragon (AC ~-4, ~80 HP). He is **immune to essentially all magic**
     — win by **melee and wands**. His **lightning breath** does up to ~80 damage, so **spread the
     party out**. Cast **Haste**, surround him, and pummel; **Protection from Good** works (the dragon
     body is Lawful Good); **Stinking Cloud** can make him cough (worsening his AC), **Resist Fire**
     halves his aura, and the **Javelin of Lightning** / lightning wands hurt him. The single best
     tool is **Dust of Disappearance** (from the Temple of Bane): invisible characters can't be
     targeted, denying his breath attack.
  - When he offers to let each character **join** him, answer **"attack"** for everyone — saying yes
    makes that character defect and fight you.
- On his death the flame-spirit is expelled from the dragon and drawn back into the **draining Pool**;
  you're teleported to New Phlan. The game doesn't hard-end — visit the Council Clerk for a final
  reward of **250,000+ XP**, raise your dead, and Phlan is free.

---

## 11. Appendix — XP tables & notable items

### XP to reach each level (training-hall caps: Fighter 8, Thief 9, Cleric 6, Mage 6)

| Level | Cleric | Fighter | Magic-User | Thief |
|---|---|---|---|---|
| 2 | 1,500 | 2,000 | 2,500 | 1,250 |
| 3 | 3,000 | 4,000 | 5,000 | 2,500 |
| 4 | 6,000 | 8,000 | 10,000 | 5,000 |
| 5 | 13,000 | 18,000 | 22,500 | 10,000 |
| 6 | **27,500** | 35,000 | **40,000** | 20,000 |
| 7 | — | 70,000 | — | 42,500 |
| 8 | — | **125,000** | — | 70,000 |
| 9 | — | — | — | **110,000** |

### Sample monster XP-per-kill (the game's own values)

Kobold 8 · Goblin 14 · Orc 15 · Orc Leader 44 · Hobgoblin 32 · Skeleton 19 · Giant Skeleton 270 ·
Ghoul 85 · Lizardman 98 · Giant Lizard 124 · Ogre 195 · Bugbear 199 · Juju Zombie 206 · Basilisk
~1,248 · Mummy 1,414 · Spectre 2,030 · Fire Giant 3,644 · Vampire 18,800.

### Best items to chase

- **Gauntlets of Ogre Power** (Cadorna box) — STR 18/00.
- **Wand of Magic Missiles** (Slums) — reliable damage for any class.
- **Fine Composite Long Bow** (Silver Shop, 25,000 gp) — adds full Strength damage to every arrow.
- **Bracers AC 2 + Ring of Protection +3** — endgame AC without armor weight.
- **Dust of Disappearance** (Temple of Bane) — the key to the final fight.
- **Javelin of Lightning** (Zhentil Keep Commandant) — hurts the final dragon.
- **Flame Tongue Long Sword +2** (Valjevo SE well) · **2× Two-Handed Sword +2** (Kobold Caves).
