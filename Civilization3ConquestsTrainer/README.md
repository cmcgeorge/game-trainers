# Civilization III: Conquests — Live Trainer

A Windows-only C#/WPF live-memory trainer for **Sid Meier's Civilization III: Conquests**
(Firaxis Games / Atari, 2003 — this build is the Steam *Civilization III Complete* package, ruleset
v1.22). It attaches straight to `Civ3Conquests.exe` — no emulator — and **finds everything by itself**:
one click resolves the player, city and unit data with no value searching.

It edits treasury, the tax/science/luxury rates, culture, era and research points per civilization;
heals, refreshes and promotes units; and fills city food and shield stores — with freeze toggles that
survive the turn tick.

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

**Units** — full heal, refresh movement, promote to elite, per-unit or all at once.

Two fields read backwards from the UI and the grid labels them accordingly: the record stores hit
points **lost** and movement **spent**, so zero is a fresh, undamaged unit. Maximum hit points are not
stored anywhere — the game derives them from the unit type and veteran level.

The per-unit **Heal** toggle re-zeroes damage and spent movement every poll. It **cannot make a unit
invincible**, and does not claim to: Civ3 resolves an entire battle inside a single call — every
round, the kill and the score update — so there is no instant during combat at which a trainer polling
between frames could intervene. Heal restores a unit that survived; a unit that lost dies anyway.
Promoting to Elite is the only per-unit durability lever the data model offers.

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
