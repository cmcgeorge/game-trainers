# Civilization III: Conquests — Live Trainer

A Windows-only C#/WPF live-memory trainer for **Sid Meier's Civilization III: Conquests**
(Firaxis Games / Atari, 2003 — this build is the Steam *Civilization III Complete* package, ruleset
v1.22). It attaches straight to `Civ3Conquests.exe` — no emulator — and **finds everything by itself**:
one click resolves the player, city and unit data with no value searching.

It edits treasury, the tax/science/luxury rates, culture, era and research points per civilization;
heals, refreshes and promotes units; finishes workers' terrain jobs; and fills city food and shield
stores — with freeze toggles that survive the turn tick.

Single-player cheat tool for your own game. It never modifies the game's files, and detaching leaves
nothing patched.

Reverse-engineering notes and a full strategy guide live in [`docs/`](docs/):
[ReverseEngineering.md](docs/ReverseEngineering.md) · [StrategyGuide.md](docs/StrategyGuide.md).

---

## Quick start

1. Start **Civilization III: Conquests** and load or begin a game. (At the main menu there is no game
   state to find, and the trainer will say so rather than attach to nothing.)
2. Run `.\Run.ps1` from this folder. A UAC prompt appears — the trainer needs administrator rights to
   `Read/WriteProcessMemory`.
3. The `Civ3Conquests` process is preselected and marked *← the game* in the list (the trainer only
   ever auto-selects an exact name match, and never offers its own process). Click **Attach**;
   auto-locate runs immediately.
4. The status bar should read something like
   *"Located via static globals: 32/32 leader slots validated, playing civ 1."*

If it does not locate, the message says why. The **Scanner** tab is the fallback — but read the
treasury caveat below before using it.

---

## Why there is no value searching

Civ III: Conquests is a native 32-bit Windows executable with a fixed image base and no ASLR, so every
static object sits at a constant offset from the module base. The trainer adds the recovered offsets
to the base Windows reports and then **proves** the result before trusting it: all 32 leader slots must
carry the `'LEAD'` tag, an `ID` equal to their own index, a shared vtable inside `.rdata`, rate
sliders totalling exactly 10, and an embedded culture object whose civ id agrees. Only the true array
base and the true record stride satisfy that 32 times running.

Measured against a running game: **~3 ms**, 32/32 slots.

If a future patch moves the globals, a second chain re-derives the leader array from the game's own
code — the compiler inlines the array walk as `add reg, sizeof(Leader)` / `cmp reg, end-of-array`, so
sweeping `.text` for that idiom recovers both numbers — and then runs the same validation.

---

## The treasury is obfuscated (read this before using the Scanner)

**Civ3 never stores your gold as a number.** It keeps two fields whose sum is your treasury, seeded
differently for every civ in every game:

```
treasury = Gold_Decrement + Gold_Encoded
```

So an exact-value scan for the number on your top bar finds **nothing**. This is not a bug in the
scanner — the value genuinely is not there. It is also the single best reason to use Auto-locate,
which decodes the pair directly.

When the trainer writes a treasury it re-encodes against the game's own key and writes only the
encoded half, never the key. Freezing re-encodes on every tick rather than replaying bytes, so it
stays correct even if the game changes its key.

If you must use the scanner for gold: leave the value box empty, **First Scan** for an unknown value,
change your gold in game, and narrow by **Changed**. The Treasury guide button says the same thing.

---

## What it edits

**Players** — every civ in the game, with yours marked:

| Field | Notes |
| --- | --- |
| Treasury | Decoded/encoded as above. **Max treasury** writes the amount in the box beside it — 100,000,000 to start with, but type any amount Civ3 can hold and the button keeps using it. The per-row **Freeze $** column holds a treasury against the turn's income. |
| Tax / Science / Luxury | Tens of percent, always totalling 10. Editing one rebalances the others, because Civ3 rejects any other combination. Your government's rate cap still applies, so the game may clamp further. |
| Era, Research bulbs | **Finish research** banks a million points — far past what any advance costs. Civ3 compares them at a turn boundary, so the tech never arrives instantly, and it may still take a few turns: the game appears to floor how few turns an advance can take, and banked points cannot buy past that floor. See [`docs/ReverseEngineering.md`](docs/ReverseEngineering.md) §8. |
| Culture (total) | Cultural level is shown read-only — it is derived. |
| City / unit counts | Read-only. |

**Max treasury, research + city shields** is three of those in one click, for the start of a session:
it sets the treasury to the amount in the box, banks the research, and fills the shield store of every
city you own. **Food is deliberately not part of it** — a city with a full granary grows every turn,
growth outruns happiness, and the city riots. *Max food* is its own button, for when you want it.

The city and unit lists keep themselves current: the trainer watches the game's own containers and
rebuilds automatically when units are built or killed and when cities are founded, captured or razed.
(*Refresh list* on the toolbar re-lists **processes**, not game data — you should never need it once
attached.)

**Units** — full heal, refresh movement (once, or held at zero all turn), promote to elite, finish worker
jobs, per-unit or all at once.

Two fields read backwards from the UI and the grid labels them accordingly: the record stores hit
points **lost** and movement **spent**, so zero is a fresh, undamaged unit. Maximum hit points are not
stored anywhere — the game derives them from the unit type and veteran level.

The per-unit **Heal** toggle re-zeroes damage and spent movement every poll. It **cannot make a unit
invincible**, and does not claim to: Civ3 resolves an entire battle inside a single call — every
round, the kill and the score update — so there is no instant during combat at which a trainer polling
between frames could intervene. Heal restores a unit that survived; a unit that lost dies anyway.
Promoting to Elite is the only per-unit durability lever the data model offers.

**Workers** — the grid shows what each worker is building and how much work is in it, and there are two
ways to speed that up.

**Finish worker jobs** banks enough worker-turns to complete the job every worker of yours is already
doing, and touches **your units only**. Job progress reads the opposite way to damage and movement on
the same row: it counts **up** toward the job's cost. It also **pools across the tile** — the game sums
the progress of every unit standing there doing the same job — so finishing it on one worker of a stack
finishes it for all of them. Idle workers are skipped: this completes work already under way rather
than starting it.

**Instant worker jobs** rewrites every terrain job in the loaded ruleset to cost one worker-turn. That
is **rules data, not a per-unit edit — the AI's workers get exactly the same speed-up**, which is why
it is a toggle rather than a button: the original costs are captured when you switch it on and put back
when you switch it off or detach. It is the same objection that rules out buffing `UnitType.Defence`
for invincibility, made survivable by being reversible.

The timing works in your favour, though. The game **re-reads a job's cost every time a worker puts in a
turn of work** rather than fixing it when the job starts, so the toggle only affects whoever is working
while it is on — and the AI's workers work during the AI's turn, which runs after you end yours.
Switching it off before you end the turn therefore keeps it away from them. Your own worker puts in its
first turn of work at the moment you give it the order, while the toggle is still on, and re-issuing a
job *adds* to its progress rather than resetting it. Save with the toggle off.

For an edge that is unambiguously yours alone, prefer **Finish worker jobs** — it writes to your units
only and needs no timing discipline at all.

**Neither is instant on its own, and the reason is worth knowing.** Civ3 tests whether a job is finished
*only* while a worker is putting a turn of work into it — and working costs the worker its whole move, so
that is one check per turn. Banked work therefore lands at the start of your next turn, and **a job
already due next turn cannot be shortened at all**. Buying turns down to one is the floor, the same way
banked research bottoms out a few turns short of instant.

**Hold my units' moves at 0** is the standing version of *Refresh all moves*: it re-zeroes spent movement
on every unit of yours on every poll, so they can keep moving, attacking and working all turn. For
workers it is also the other half of the mechanism above — with the move handed back you can re-issue a
worker's order in the same turn, which forces the completion check to run again, and the banked work
finishes the job **on the spot**. `Job_Value` supplies the work; the movement hold supplies the tick.

**Keep worker jobs banked** does what *Finish worker jobs* does, on every poll. Completing a job clears
the worker's banked work — the game zeroes `Job_Value` and sets `Job_ID` to -1 for every unit on the tile
— so nothing carries into the next job and the button would otherwise need clicking once per job. It is
cheap: a worker already topped up is skipped rather than rewritten.

With both toggles ticked, finishing terrain work is just **order it, then order it again**, and a worker
can move on and repeat as many times in one turn as you care to click. Only your units are touched.

**Cities** — position, stored food, stored shields, cultural level, with a freeze, plus three
all-my-cities buttons: **Max shields** (each city finishes what it is building next turn), **Max
food** (each city grows next turn — separate on purpose, because repeated growth outruns happiness and
tips a city into disorder), and **Max culture** (raises every one of them to cultural level 6).

*Max culture* moves the **border-expansion ladder**, not accumulated culture: the level indexes the
loaded ruleset's own culture-level table, so the preset is deliberately a small number rather than a
true ceiling — and `cultural_level` is one of the few offsets here that is inferred rather than
confirmed, so check the effect in game. For a cultural victory it is the empire-wide **Culture** column
on the Players tab that counts.

Deliberately narrow. Past this prefix the community struct header stops being reliable, so population,
corruption, per-turn incomes, the build queue and the city name are not shown at all — see
[`docs/ReverseEngineering.md`](docs/ReverseEngineering.md) §4.4.

**Map** — world size and the tile array, plus a "reveal map" action **gated behind an explicit
acknowledgement**: the map header itself is confirmed, but the per-tile visibility masks are inferred
and have never been round-tripped through the game's display. Every tile is checked for its own
`'TILE'` tag before anything is written to it, so a wrong pointer stops the sweep rather than
spraying writes across the heap. Back up your save first.

**References** — the nine conquests, the behaviour notes that explain the odd bits above, and the
civilization and unit tables **read live out of the loaded ruleset** rather than hard-coded. That
matters: a conquest or a community mod substitutes its own civs and units, and a baked-in table would
silently mislabel them.

**Not included, on purpose** — granting technologies (only a *function* is known, no field), editing
gold-per-turn (not stored; recomputed each turn from your cities), and an offline save editor. The
reasons are written up in the RE notes.

Writes are disabled automatically in play-by-email and offline-multiplayer games.

---

## Project structure

```
Civilization3ConquestsTrainer/
  Run.ps1                       build + launch (shared option surface with every trainer)
  docs/                         RE notes and the strategy guide
  src/Civilization3ConquestsTrainer/
    Game/
      GameFacts.cs              process name, build fingerprint, game-rule constants
      PeImage.cs                parses the mapped PE header — section ranges and build timestamp
      Civ3Layout.cs             every recovered offset + the pure validation predicates
      GameLocator.cs            chain A (static globals) → chain B (signature) → validate
      GameTables.cs             civilizations and unit types, read from the loaded BIC
      ConquestBook.cs           the nine conquests and the behaviour notes
    Memory/IMemorySource.cs     the seam that lets the locator be tested without a game
    ViewModels/                 hand-rolled MVVM on GameTrainers.Common
  test/FormatCheck/             299-check headless harness, no game required
```

Reverse-engineering scratch (the Ghidra project, the C3X reference data, a read-only probe, a PKWare
DCL decompressor) lives in the git-ignored `.docs/`.

---

## Build, test, and run

```powershell
.\Run.ps1                       # build Release and launch (UAC prompt)
.\Run.ps1 -Configuration Debug
.\Run.ps1 -Test -NoRun          # run the verification harness, no GUI
.\Run.ps1 -Clean
.\Run.ps1 -Publish              # self-contained win-x64 single file

dotnet build src\Civilization3ConquestsTrainer\Civilization3ConquestsTrainer.csproj -c Release
dotnet run --project test\FormatCheck
```

`FormatCheck` needs no game and no copyrighted files. It asserts the layout constants against the
absolute addresses the reverse engineering established, round-trips the gold codec, drives every
validation predicate over synthetic buffers including one-field-at-a-time corruptions, and runs the
locator over a synthetic address space — including a module relocated away from `0x400000`, an
unrecognised build, a single corrupted leader slot, an empty image, a 64-bit image it must refuse, and
a leader array moved so that only the signature chain can find it.

---

## Credits

The memory layout is the Civ3 modding community's work, chiefly **Antal1987**
([C3CPatchFramework](https://github.com/Antal1987/C3CPatchFramework)) and **Flintlock / maxpetul**
([C3X](https://github.com/maxpetul/C3X)), whose per-build symbol table made this tractable. This
trainer transcribes and independently re-verifies facts — addresses and field offsets — against a
running game; it does not vendor either project's code.
