# Airborne Ranger — Strategy Guide

*Airborne Ranger* (MicroProse, 1988, IBM PC version **441.01**) drops one U.S. Army Ranger behind
enemy lines to complete a single objective and walk back out. There is **no pause**, no saving
mid-mission, and no second life: three wounds kill you, and death is permanent for that ranger.

Everything below that is quoted in `monospace` or in a *Briefing* block is the game's own text,
taken out of `AR.EXE`. Where a detail comes from the disassembly rather than from playing, it says
so. See [ReverseEngineering.md](ReverseEngineering.md) for how any of it was established.

---

## 1. Getting in

```
AR.EXE
```

Three prompts before you play:

1. **`SELECT GRAPHICS MODE`** — `1` CGA, `2` Tandy-1000, `3` EGA, `4` MCGA, `5` Hercules.
   Pick **4 (MCGA)** if your emulator supports it; the terrain reads much more clearly.
2. **`SELECT CONTROL DEVICE`** — `1` Joystick, `2` Keyboard – Directional, `3` Keyboard – Rotational.
   **Directional** means the arrow keys move the ranger in the direction you press. **Rotational**
   means left/right spin him and up/down walk him forward and back. Directional is far easier.
3. Title screen and credits — press **Enter** past them.

Then **`RANGER ASSIGNMENTS`**:

* **Assign a Practice Ranger** — a throwaway. Merit points are still tallied and shown on the
  assessment screen, but nothing is written to `ROSTER.DAT`. Use this to learn a mission.
* **Assign a Veteran Ranger** — pick a career ranger from the roster (or an empty slot to enlist a
  new one). Merit points accumulate, promotions and decorations are awarded, and death is written
  back to the file.

---

## 2. Controls

Recovered from the game's own keyboard handler and command dispatcher, not from a platform manual.

### Movement and firing — [Confirmed]

The game installs its own interrupt-9 handler with a scan-code table, so several keys map to the
same action:

| Action | Keys |
| --- | --- |
| North / South / West / East | `↑` `↓` `←` `→`, or keypad `8` `2` `4` `6` |
| Diagonals | keypad `7` `9` `1` `3` |
| **Fire / select / jump** | **`Enter`**, keypad `5`, keypad `0` (`Ins`) |

Because the handler tracks make and break codes, **movement keys must be held**, not tapped — a
key pressed and released inside one game tick is not seen.

### Commands — [Confirmed] from the dispatcher

| Key | Effect |
| --- | --- |
| `Space` | Toggle upright ⇄ crawling |
| `5` | Plant a time bomb with a **5-second** fuse |
| `6` | Plant a time bomb with a **10-second** fuse |
| `7` | Plant a time bomb with a **15-second** fuse |
| `Backspace` | Use a first-aid kit (removes exactly one wound; refused with no kit, no wound, or three wounds) |
| `1` | **Recall the aircraft** — clamps the countdown down to a minimum |

### Weapon selection and the map — [Inferred]

The dispatcher takes codes `0x80`–`0x83` for the four weapons and `0x88` for the map screen. That
`0x80 + n` shape is the classic function-key encoding, which makes the mapping:

| Key | Effect |
| --- | --- |
| `F1` | Carbine |
| `F2` | Hand grenade |
| `F3` | LAW rocket |
| `F4` | Knife |
| `F9` | Map / status screen |

The **weapon codes themselves are confirmed** (`0` carbine, `1` grenade, `2` LAW rocket,
`3` time bomb, `4` knife); which physical key produces each code was not verified on screen.
If `F1`–`F4` do not respond, try the other function keys — the codes exist, only the labelling is
uncertain.

After a grenade, rocket or bomb is used the ranger automatically reverts to the carbine.

---

## 3. Choosing the mission

The mission list shows a **Challenge Level** in the top-right that changes as you move the
highlight. It is not a difficulty you choose — it is a **property of the mission**, stored as the
thirteen-digit string `2111332222333` in the executable, one digit per list entry.

| # | Mission | Terrain | Challenge |
| --- | --- | --- | --- |
| 1 | Destroy a Munitions Depot | Desert | 2 |
| 2 | Steal a Code Book | Temperate | **1** |
| 3 | Disable Enemy Aircraft | Arctic | **1** |
| 4 | Capture an Enemy Officer | Desert | **1** |
| 5 | Cut a Pipeline | Temperate | 3 |
| 6 | Knock Out Enemy Radar Array | Arctic | 3 |
| 7 | Disable SAM Site | Desert | 2 |
| 8 | Liberate a P.O.W. Camp | Temperate | 2 |
| 9 | Photograph an Experimental Aircraft | Arctic | 2 |
| 10 | Free the Hostages | Desert | 2 |
| 11 | Create a Diversion | Temperate | 3 |
| 12 | Delayed Sabotage | Arctic | 3 |
| — | **\*\*\*CAMPAIGN\*\*\*** | all twelve in sequence | 3 |

**Start with 2, 3 or 4.** They are the game's own easiest rating, and two of the three
(*Code Book*, *Capture an Officer*) are "reach a thing and stand next to it" objectives that do not
require you to win a firefight.

After the mission you set a separate **`MISSION DIFFICULTY`** slider (`Easy ▬▬▬ Hard`) with the
arrow keys. This is the one you control, and it scales both the danger and the merit points.
Leave it at the far left until you know a map.

---

## 4. The equipment, and what it weighs

The supply-pod screen prices every item, and the carried-weight readout is exactly the sum of those
prices (verified against the running game — see the RE notes, §2.2).

| Item | Weight | Notes |
| --- | --- | --- |
| Carbine magazine | **1** | 30 rounds each |
| Hand grenade | **2** | Throw range grows with how long you hold fire |
| First-aid kit | **3** | Removes one wound |
| Time bomb | **3** | 5-, 10- or 15-second fuse |
| LAW rocket | **6** | Single-shot; effective against nearly everything |

A supply pod holds **21 points** of capacity, and the **`STANDARD`** loadout fills it exactly:
3 magazines + 3 grenades + 1 first-aid kit + 1 LAW rocket + 1 time bomb = `3 + 6 + 3 + 6 + 3 = 21`.

You start a mission carrying **4 magazines (one loaded, 3 spare), 3 grenades, 1 LAW rocket, 1 time
bomb and 1 first-aid kit** — 22 weight, because the loaded magazine counts 1 on top of the 21.

You can drop up to **three pods** during the airdrop by pressing fire before the jump light. They
land where you drop them, which is the whole point: **a pod is only useful if you can find it
again**. Drop them on a landmark you can navigate back to, not in open ground.

Weight matters because it governs how far you can run before the fatigue bar fills. In the Desert
missions heat drains you faster; in the Arctic, snow and wind muffle sound, so gunfire carries less
far and the enemy is slower to react.

---

## 5. The mission, step by step

```
     ┌───────────────────────────────────────┐
     │                                       │   ← the map is a tall north–south corridor
     │        OBJECTIVE AREA                 │     that scrolls as the aircraft flies
     │   ▣ ▣ ▣  tents / bunkers / depot      │
     │   ═══════  wire, mines, trenches      │
     │                                       │
     │        X   ← Pickup Point             │
     │                                       │
     │        NO-MAN'S LAND                  │
     │   ~ ~ ~  scattered cover              │
     │                                       │
     │        DROP ZONE                      │   ← you land here; your pods land
     │   ⊕ pod   ⊕ pod   ⊕ pod               │     wherever you released them
     └───────────────────────────────────────┘
```

**1 — The flight in.** A V-22 Osprey crosses the map. Steer it left/right with the arrow keys.
Press **fire** to release a supply pod; press fire again once the **jump light** in the
bottom-left corner turns from a dark box to a bright box with a down-arrow, and you jump.
**If you never jump, the mission is aborted** and you are returned to the assignment screen with
nothing scored — this is the single most common way to waste a mission.

**2 — The parafoil.** Steer with the arrow keys while descending. Landing in a minefield, a trench
or barbed wire wounds you immediately, so aim for open ground.

**3 — The map screen.** On landing the game shows the tactical map with the full status panel:

```
 ▬▬  CARBINE MAGS  04     WOUNDS      00
 ▬▬  GRENADES      03     FIRST AID   01
 ▬▬  LAW ROCKETS   01     WEIGHT      22
 ▬▬  TIME BOMBS    01     TIME       600
```

Press **fire** to drop into the close-up action view. The countdown does not run while the map is
up — this screen is the closest thing the game has to a pause, and it is worth using it to plan
your route before you commit.

**4 — The approach.** Move **crawling** (`Space`) wherever there is cover. Crawling is slow but the
enemy cannot see you behind low objects, and a ranger in a trench is invisible to anyone not also
in that trench. Walking regains stamina but leaves your head above cover. Running is fastest and
fills the fatigue bar; a wounded, heavily loaded ranger cannot run far at all.

**5 — The objective.** See §6.

**6 — Extraction.** The Osprey returns to the **Pickup Point (`X` on the map)** when the countdown
reaches zero, or earlier if you press **`1`** to recall it. It waits only briefly. Be standing
there. The one exception is *Create a Diversion*, where the aircraft cannot be recalled at all.

---

## 6. The twelve missions

Each briefing below is the game's own text.

### 1. Destroy a Munitions Depot — Desert, challenge 2

> The enemy depot consists of an ammunition shack, a bunker-like explosives magazine, and a fuel
> dump. All three should be destroyed.

Three separate targets. The magazine is bunker-like — save the **LAW rocket** for it and use time
bombs or grenades on the shack and the fuel dump. Plant, then get clear before the fuse runs.

### 2. Steal a Code Book — Temperate, challenge 1

> Infiltrate an enemy headquarters area, find the communications post, and move next to it to steal
> the code book. **WARNING: Enemy units are expecting trouble.**

No demolition needed — walk next to the communications post and the book is yours. The garrison is
already alert, so approach crawling and through cover. One of the best first missions.

### 3. Disable Enemy Aircraft — Arctic, challenge 1

> Avoid enemy contact until you arrive in the runway area. Premature contact may cause the enemy
> aircraft to leave. When you reach the runway, destroy all jet fighters stationed there.

Stealth first, violence second. **Every shot you fire before reaching the runway risks the aircraft
scrambling and the mission becoming unwinnable**, so crawl past anything you can. Snow deadens
sound, which is in your favour.

### 4. Capture an Enemy Officer — Desert, challenge 1

> Infiltrate the enemy headquarters area. Search among the tents until you find an enemy officer.
> Move next to him to capture, then recall your aircraft. Defend the prisoner until the aircraft
> arrives.

The officer wears a different-coloured uniform. Once captured, press **`1`** immediately — the
defence phase is a fixed fight and you want it short. Keep the prisoner between you and cover.

### 5. Cut a Pipeline — Temperate, challenge 3

> Penetrate the defenses around a pipeline pumping station and destroy it. **WARNING: Beware of
> enemy minitanks deployed near the pumping station.**

The pumping station is heavily armoured — a **time bomb** placed against it, not grenades.
Minitanks are automated and will kill you in the open; the LAW rocket is the only thing that
answers one.

### 6. Knock Out Enemy Radar Array — Arctic, challenge 3

> Advance north of the icy river and destroy all radar antennas deployed there. **Beware of unsafe
> ice patches.**

Crossing the river is the mission. Unsafe ice drops you through; crawling underwater for long
drowns you. Cross at a point you have looked at on the map screen first, and destroy every antenna,
not just the obvious one.

### 7. Disable SAM Site — Desert, challenge 2

> Destroy all SAM platforms at the launch site. Avoid enemy contact until you arrive in the launch
> site area. Premature contact will result in a penalty.

Explicit merit-point penalty for shooting early — this is a stealth-scored mission. One to four
platforms, all of which must go.

### 8. Liberate a P.O.W. Camp — Temperate, challenge 2

> Avoid enemy contact until you arrive in the prison area, or the prisoners may be removed. Ranger
> prisoners are being held in pit cells. To free them, blow up the central control module, then
> kick the exposed lever. Recall your aircraft and defend the prisoners until it arrives.

Two-stage objective: **destroy the central control module, then walk into the exposed lever** to
kick it. Contact before you arrive can empty the camp and make the mission unwinnable.

### 9. Photograph an Experimental Aircraft — Arctic, challenge 2

> Infiltrate an enemy airfield and sneak into the hangar. **Do not allow yourself to be seen
> entering the hangar!**

A pure stealth mission — being *seen entering* fails it, so clear or avoid every sentry with a
line of sight to the hangar door before you step through it. The knife is silent and does not
alert anyone; that is what it is for.

### 10. Free the Hostages — Desert, challenge 2

> Blow open the door on the Hostage Prison, then recall your aircraft. Defend the hostages until
> the aircraft arrives. **Beware of enemy attempts to destroy the prison and kill the hostages.**

The enemy actively tries to kill the hostages during the defence phase, so you cannot simply hide.
Position between the prison and the likeliest approach, and recall the aircraft the instant the
door is open.

### 11. Create a Diversion — Temperate, challenge 3

> Do not commence combat until the countdown buzzer sounds. Shoot whenever it sounds again. Fight
> your way to the border, causing combat as often as possible. Pickup Point is in the border zone.
> The aircraft cannot be recalled early — be there when the countdown clock reaches zero.

The one mission where noise *is* the objective, and the one where **`1` does nothing**. Manage the
clock yourself: fight on the way to the border, and be standing on the Pickup Point before zero.

### 12. Delayed Sabotage — Arctic, challenge 3

> Sneak past an enemy airfield's defense perimeter and plant a time bomb at the aviation fuel dump.
> The time bomb will not explode until long after you leave; if it is to remain undiscovered, it is
> essential that you not be seen in the vicinity of the fuel dump.

Being *seen near the dump* ruins it even if the bomb is planted. Take the longest, most covered
approach you can afford, use the **15-second fuse** (`7`) so you are well clear, and leave by a
different route.

### The Campaign

`***CAMPAIGN***` runs all twelve in sequence with one ranger. It is the only route to **Colonel**
and to the **Congressional Medal of Honor** — the promotion and award messages for both exist in
the executable, and the roster's `(CMPN)` marker records that a ranger has been through it.

---

## 7. Scoring, ranks and decorations

The assessment screen after every mission reads:

> *N* merit points were earned for elimination of enemy troops and installations, including
> *S* soldiers and *T* military targets.

and then either *Good work, Ranger — another successful mission! For achieving your mission goal,
you are awarded N merit points.* or *Intelligence reports mission not accomplished.* Firing early on
a stealth-scored mission adds *…alerted the enemy prematurely, incurring a merit point penalty of
N points.*

Merit points accumulate into a **career score** stored in `ROSTER.DAT`, which drives promotion:

| Index | Rank | | Index | Rank |
| --- | --- | --- | --- | --- |
| 0 | PFC — Private First Class | | 6 | 2LT — Second Lieutenant |
| 1 | CPL — Corporal | | 7 | 1LT — First Lieutenant |
| 2 | SGT — Sergeant | | 8 | CPT — Captain |
| 3 | SSG — Staff Sergeant | | 9 | MAJ — Major |
| 4 | PSG — Platoon Sergeant | | 10 | LTC — Lieutenant Colonel |
| 5 | SGM — Sergeant Major | | 11 | COL — Colonel |

(Indices 13 and 14 are `KIA` and `POW`, which is how the roster records a ranger who did not come
back.)

Six decorations, in ascending order, shown as a ribbon line on the roster:

| Mnemonic | Award |
| --- | --- |
| `COM1` | Army Commendation Medal |
| `COM2` | Army Commendation Medal, second award |
| `BSTR` | Bronze Star |
| `SSTR` | Silver Star |
| `DSC` | Distinguished Service Cross |
| `CMH` | Congressional Medal of Honor |
| `(CMPN)` | Campaign ribbon — the full twelve-mission campaign |

---

## 8. Maps and map symbols

The mission maps are **generated per mission**, so there is no fixed map to memorise — what is
fixed is the *structure* (§5) and the vocabulary of objects. The map screen (press fire to leave it,
and it does not consume mission time) is the only reliable way to plan a route.

| Desert | Temperate | Arctic |
| --- | --- | --- |
| MG nest | MG nest | MG nest |
| Bunker | Bunker | Bunker |
| Barbed wire | Barbed wire | Barbed wire |
| Minefield | Minefield | Minefield |
| Trench | Ditch | Ravine |
| Wall | Wall | Wall |
| Tent | Tent | Hangar |
| Guard house | Turret bunker | Guard house |
| Boulder, bush | Tree stump, pond | Fir tree, snow drift |
| SAM launcher | Manned turret | Radar |
| Ammo shack, explosives magazine, fuel dump | Communications post, tank traps, proximity mines, tiger pit, fuel dump | Aircraft, airstrip, aviation fuel dump, icy pond |
| Hostage prison | | |

Reading them:

* **Trenches, ditches and ravines** are the safest routes. An enemy who is not in the same trench
  cannot see you in it.
* **Barbed wire, minefields and proximity mines** wound on contact, including on landing.
  Never parafoil onto one.
* **Walls and bunkers** stop bullets. Grenades handle wooden doors and light armour; anything
  properly armoured needs the LAW rocket or a time bomb.
* **Icy ponds** in the Arctic are not solid. Crossing is a gamble; going round is not.

---

## 9. How to actually win

1. **Learn the map on the map screen before you move.** The countdown is stopped there.
2. **Crawl by default.** Time is generous (600 units); wounds are not.
3. **Fire only when you must.** Half the missions penalise or fail on premature contact, and every
   shot draws the garrison towards you.
4. **Match the tool to the target** — knife for silence, carbine for troops, grenade for wooden
   doors and light cover, LAW rocket for armour and minitanks, time bomb for structures.
5. **Heal early.** Two wounds is the point of no return; `Backspace` at two wounds still works,
   at three it does not.
6. **Drop supply pods on landmarks**, and only if you can plan a route past them.
7. **Recall the aircraft (`1`) as soon as the objective is met** — every extra second on the ground
   is a chance to be shot for no additional score.
8. **Practice Rangers cost nothing.** Learn each mission on one before you risk a veteran.

---

## 10. If you would rather not die

The trainer in this repository attaches to the running game, finds its data segment on its own and
edits the very variables this guide describes — freeze wounds at zero, refill magazines, grenades,
rockets, bombs and first-aid kits, and set the mission clock. It also edits `ROSTER.DAT` offline,
so a ranger who was killed learning *Cut a Pipeline* can be given their rank back.
See the trainer's `README.md`.
