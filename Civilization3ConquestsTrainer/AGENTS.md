# Repository Guidelines

A WPF (.NET 8, `net8.0-windows`) live-memory trainer for **Sid Meier's Civilization III: Conquests**
(Firaxis Games / Atari, 2003; this build is the Steam *Civilization III Complete* package, ruleset
v1.22). It is the repo's **third native-Windows target** after `ImperialismIITrainer` and
`BeachHead2000Trainer` — no DOSBox, no guest-address translation. Windows-only (WPF + Win32 memory
APIs); the app manifest requests administrator rights so it can `Read/WriteProcessMemory`.

## Project Structure & Module Organization

Three projects in `Civilization3ConquestsTrainer.sln`: the WPF app, its harness, and the shared
`GameTrainers.Common` library it references (pulling both `GameTrainers.Common.Memory` and
`GameTrainers.Common.Mvvm` via csproj `<Using>` items — note their `ObservableObject` uses `SetField`).

- `src/Civilization3ConquestsTrainer/` — the app (`AssemblyName` `Civ3ConqTrainer`, `RootNamespace`
  `Civilization3ConquestsTrainer`), layered by concern:
  - `Game/` — pure data, no process access, fully unit-testable.
    - `GameFacts.cs` — process name, the **build fingerprint** (file size 3,518,464 and PE
      `TimeDateStamp` `0x550A3E1F`), and the game-rule constants the UI clamps against.
    - `PeImage.cs` — parses the mapped PE header. Section ranges are **read from the target**, not
      baked in, which keeps the repo's "addresses are never hard-coded" rule and doubles as the
      build check.
    - `Civ3Layout.cs` — every recovered offset as an RVA or struct offset, each tagged
      `[Confirmed]`/`[Inferred]`, plus the pure predicates (`ValidateLeader`, `ValidateUnit`,
      `ValidateCity`, `ValidateMap`, `IsPlausibleSliderSet`, the gold codec) the harness exercises.
    - `GameLocator.cs` — chain A (module base + RVAs) → chain B (re-derive the array from the game's
      own code) → validate. Returns a `Civ3Location` or null with `LastError` set.
    - `GameTables.cs` — civilizations, unit types and worker jobs read out of the loaded `BIC`. The job
      table is the one the trainer *writes*, and the only one with no `ID` column — so its stride is
      proved by `ValidateWorkerJob` holding for every record rather than by `Table[i].ID == i`. **Do not replace
      this with a curated table**: Conquests ships nine scenarios and the community ships thousands
      of mods, each substituting its own civs and units, so a baked table would be right only for the
      unmodified epic game and would silently mislabel everything else.
    - `ConquestBook.cs` — the nine shipped conquests and the behaviour notes. The conquest list came
      from the shipped `.biq` filenames, not from memory — the fifth is **Mesoamerica**, which is
      commonly misremembered as an industrial-era scenario.
  - `Memory/IMemorySource.cs` — the seam that lets `GameLocator` be driven over a synthetic address
    space by the harness.
  - `ViewModels/` — hand-rolled MVVM. `MainViewModel` owns the process handle, the locator, the poll
    loop and every tab; row view-models (`PlayerRowViewModel`, `CityRowViewModel`, `UnitRowViewModel`)
    each own one record and write field-at-a-time through `IGameHost`. The scanner-row plumbing
    (`IScanHost`, `ScanValue`, `ScanResultViewModel`, `FrozenValueViewModel`) matches the repo's other
    value-scanner trainers.
- `test/FormatCheck/` — headless harness (console `Exe`, `net8.0-windows` + `UseWPF` because it
  references the WPF app for the view-model types). **369 checks**, no game and no copyrighted files
  needed.

It **has a `GameLocator`** and **no save editor**. The exe is native, unpacked, fixed-base and
ASLR-free, so the locator is the primary path and `MemorySearcher` is only the fallback.

## Build, Test, and Development Commands

- `.\Run.ps1` — build Release and launch (triggers a UAC prompt). Flags: `-Configuration Debug|Release`,
  `-Clean`, `-NoBuild`, `-NoRun`, `-Test`, `-Publish`.
- `.\Run.ps1 -Test -NoRun` — build and run the harness without launching the GUI.
- `dotnet build src\Civilization3ConquestsTrainer\Civilization3ConquestsTrainer.csproj -c Release`
- `dotnet run --project test\FormatCheck`
- `dotnet run --project .docs\probe -- locate` — drives the shipped locator against a running game.
  The probe is read-only except for its explicit `writetest` mode, which restores what it writes.

## Coding Style & Naming Conventions

C# with `Nullable` and `ImplicitUsings` enabled, `LangVersion` `latest`; 4-space indent. File-scoped
namespaces, XML `<summary>` docs on public types/members, `sealed` classes by default, `const` hex for
offsets, and `// --- section ---` divider comments. No linter or formatter config is committed; match
the surrounding file. Follow the read-validate-write pattern — every row rejects an out-of-range value
before poking RAM — so a mistyped edit cannot corrupt a neighbouring field.

Tooltips on the toggles are **instructions, not hints**: what the control does, when to switch it on,
when to switch it off, what to expect. `MainWindow.xaml` therefore carries an implicit `ToolTip` style
that wraps at 460px, plus `ToolTipService.ShowDuration` of 60 s on the `Button` and `CheckBox` styles —
without both, a WPF tooltip is a single unwrapped line that vanishes after five seconds. Keep new
multi-line tooltips in that shape and separate paragraphs with `&#10;&#10;`.

## Testing Guidelines

There is no xUnit/NUnit suite. `FormatCheck` runs `Check(...)`/`Equal(...)` assertions and exits 0
(pass) or 1 (fail). Keep it green. It asserts the layout constants **against the absolute addresses
the reverse engineering established** (written as `VA - ImageBase`, so a mistyped RVA cannot silently
shift the table), round-trips the gold codec including overflow refusal, drives every validation
predicate over synthetic buffers with one-field-at-a-time corruptions, parses a hand-built PE header,
and runs the locator over `FakeModule` address spaces — a module relocated away from `0x400000`, an
unrecognised build, one corrupted leader slot out of 32, an empty image, a module with no PE header, a
human civ id outside the player set, and a leader array moved so only the signature chain can find it
(plus the negative case proving that chain is not passing by accident), and a 64-bit image the locator
must refuse. What cannot be headless — the GUI and the live locate/write — was confirmed by hand
against a running game.

## Commit & Pull Request Guidelines

Imperative, sentence-case subjects; join related changes with a semicolon. Say which game state a
change reads or writes and how it was confirmed against the live game.

## Domain Notes

**The treasury is obfuscated and this drives the whole design.** Civ3 stores gold as
`Gold_Decrement + Gold_Encoded`, seeded differently per civ per game, so the displayed number is never
in memory and an exact-value scan for it cannot work. Writes go to the **encoded half only** — the
decrement is the game's key. Freezes **re-encode against a fresh read of the key each tick** rather
than replaying captured bytes. Confirmed live: `10 → 12345 → 10`, key untouched.

**`ID == index` is the load-bearing validator.** Only the true array base *and* the true stride
(`0x20E4`) satisfy it across all 32 leader slots, which is what makes a false positive implausible.
The game's own array walk at `0x50C1BD` (`add ebp,0x20E4` / `cmp ebp,0xAB7318`) confirms both numbers
independently, and `0xA75698 + 32 × 0x20E4 = 0xAB7318` exactly.

**Two fields read backwards.** `Unit_Body.Damage` is hit points *lost* and `Unit_Body.Moves` is
movement *spent* — "full heal" and "refresh moves" both write zero. Maximum hit points are not stored
at all; the game derives them from unit type plus veteran level.

**The `City_Body` gap is real and deliberate.** The C3X header's own `field_XX` anchors agree with
arithmetic up to `+0x54` and then drift by `0x18`, so population, corruption, incomes, the build queue
and the city name are at unconfirmed offsets. `Civ3Layout.CityTrustedPrefixEnd` marks the boundary and
nothing past it is surfaced. Do not "fix" this by guessing — close it by decompiling
`City_recompute_happiness` (`0x4C4660`) / `City_recompute_commerce` (`0x4B7770`). The prefix *itself*
is now `[Confirmed]`: a game with 32 cities across 13 civs validated every record, and tallying
`CityCivId` reproduced each leader's own `Cities_Count` exactly for all 13 — two unrelated structures
agreeing — with the food and shield stores additionally round-tripped. `cultural_level` stays
`[Inferred]`, which is why the Cities tab's **Max culture** button writes
`GameFacts.MaxCityCulturePreset` — a deliberately small level (6), since the field indexes the loaded
ruleset's own culture-level table and a huge value would index a long way past it. Do not raise it to
something that merely looks impressive.

**Worker job progress counts up, pools across a tile, and has two levers with very different blast
radii.** `Unit_Body.Job_Value` (`+0x38`) is worker-turns *done*, not left — the opposite reading from
`Damage` and `Moves` on the same record. This is not inferred: `get_worker_remaining_turns_to_complete`
(`0x5D5520`) computes `Worker_Job.TurnToComplete × a terrain factor` and then subtracts `Job_Value` at
`0x5D5640`, summing it over **every unit on the tile with a matching `Job_ID`** — which is why finishing
the job on one worker of a stack finishes it for all of them. The same routine reads the job table at
`BIC + 0x3E1C` (field `+0x44`, stride `0x74`), so those offsets are the game's own rather than derived;
see `docs/ReverseEngineering.md` §4.7. **"Finish worker jobs" writes `Job_Value` and touches only your
units. "Instant worker jobs" rewrites the ruleset's job costs and therefore speeds up the AI too** — the
same objection that rules out buffing `UnitType.Defence`, which is why it is a reversible toggle that
captures the original costs and restores them on switch-off, on detach and on exit. Do not turn it into a
one-way button, and do not remove the restore from `Teardown`: `Detach` promises nothing is left patched.
The terrain factor is **not decoded** — `GameFacts.WorkerJobTerrainFactorCeiling` (4) covers it, and
being wrong there shortens a job rather than failing to finish it. Do not write `Job_ID`: starting a job
also sets unit state, tile overlays and the animation, so a poked id describes work the game never began.

**"Finish worker jobs" cannot be instant on its own, and the UI must not claim it is.** The completion
test — `cmp pooled_work, cost` / `jl` at `0x463B2E`, clearing `Job_Value` to 0 and `Job_ID` to -1 on
success — lives *inside* `Unit_work_simple_job`, so it runs only while a worker is putting a turn of work
in, and that spends the unit's whole move. One tick per turn is one completion check per turn: banked work
lands next turn, and **a job already due next turn cannot be shortened**. This was mis-stated once as
"appears when you end the turn, because Civ3 applies worker output at the turn boundary" — true-sounding
but the wrong mechanism, and it hid the floor. The escape is `HoldMyUnitMoves`: returning the move lets
the job be re-issued in the same turn, which buys a second tick and finishes it on the spot. Treat the two
features as one mechanism — `Job_Value` supplies the work, the movement hold supplies the tick — and do
not document either without the other. Completion also *wipes* `Job_Value`/`Job_ID`, so banked work never
carries into the next job; `KeepWorkerJobsBanked` exists because of that wipe, not as a convenience. All
three of these are **standing writes** and `Teardown` clears them, so a fresh attach starts unarmed;
`FinishJob` must stay write-free when the figure is already banked, since the poll loop calls it per unit
per tick, and `FormatCheck` pins that.

**The job cost is re-read at every work tick, and that is what the toggle's usage advice rests on.**
`Unit_work_simple_job` (`0x4638C0`) reads `BIC.WorkerJobs` fresh each time a worker puts in a turn, does
`Job_Value += rate`, and spends the unit's move — it does not cache the cost when the job starts. So the
toggle takes effect on the next tick and stops the moment it is restored, AI workers (which tick during
the AI's turn, after the human ends theirs) are not reached by a toggle switched off first, and re-issuing
a job *adds* progress rather than resetting it. The tooltip and the References note both say this; keep
them in step with §4.7 if any of it is ever re-tested. What is still unobserved is where the
*continuation* tick lands for an already-working human unit — do not let the docs imply it is known.

**Banking research points shortens research without finishing it.** `Research_Bulbs` is confirmed
writable and "Finish research" now banks 1,000,000, but a live game still took a few more turns at
30,000 — an amount that already cleared any epic-game advance cost, so the shortfall is not points.
The likely cause is a floor on how few turns an advance may take. Do not "fix" this by inflating the
preset further. The mapping done for worker jobs turned up a strong candidate — `General.ResearchTime_Min`
at `BIC + 0x3E08`, reading 4 in a live epic game — but the causal test (write 1, bank points, see whether
the advance lands next turn) has **not** been run, so nothing in the UI mentions it. See
`docs/ReverseEngineering.md` §8.2.

**Food and shields are separate buttons, and the combined "max" action fills shields only.** A full
granary makes a city grow every turn, growth outruns happiness, and the city riots — so food is opt-in
per click rather than something a one-click action does to the whole empire. Do not fold it back in.

**Tile visibility is inferred**, which is why "Reveal map" is gated behind an explicit acknowledgement
instead of being a one-click button, and why every tile is checked for its own `'TILE'` tag before it
is written to.

**The trainer is data-only: it never writes to the game's code, and that is a decision, not an
oversight.** The obvious missing feature is making a unit invincible, and it cannot be done by writing
data. Civ3 resolves an entire battle inside one call to `Fighter_begin` (`0x4AB470`) — every round, the
kill, and `Unit_score_kill` — so a trainer polling between frames has no instant at which to intervene;
the damage freeze can only heal a survivor. And there is no per-unit hit-point ceiling to raise:
maximum HP is computed by `Unit_get_max_hp` (`0x5CD180`) from unit type plus veteran level, never
stored on the unit. The two ways forward were both considered and declined: buffing `UnitType.Defence`
works but is *shared rules data*, so every AI unit of the same type is buffed too; and a real
per-unit patch needs a code cave plus a `JMP` into `.text`, which is a different risk class from
anything else here. Elite promotion is the only per-unit durability lever, and the References tab says
so. Do not add a code patcher without asking.

**The process picker must prefer an exact name match, and must exclude the trainer itself.** This bit
once and the fix is pinned by `FormatCheck`: the trainer's own executable is `Civ3ConqTrainer.exe`,
whose process name contains the `"civ3"` hint **and** sorts *before* `Civ3Conquests` under an ordinal
comparison (`StringComparer.OrdinalIgnoreCase.Compare("Civ3ConqTrainer", "Civ3Conquests") < 0`). A
picker that merely substring-matched and then sorted by name therefore auto-selected the trainer,
attached to a 64-bit .NET process, and reported "not a 32-bit x86 image" — a correct diagnosis of the
wrong target. `ProcessPicker` now ranks Exact above Hint, drops `Environment.ProcessId`, and refuses to
auto-select a hint-only match rather than guessing. Do not "simplify" it back into a substring test.

**Saves are PKWare DCL ("implode"), not zlib** — confirmed by decompressing a shipped save from offset
0 (222 KB → 2.15 MB, `CIV3` magic, `LEAD` ×33). Save offsets equal RAM offsets, because the container
serialises objects through their `Base.pStart`/`pEnd`. A future save editor could share `Civ3Layout`.

Provenance and attribution: the layout knowledge is the Civ3 community's — **Antal1987**
(C3CPatchFramework) and **Flintlock/maxpetul** (C3X, whose `civ_prog_objects.csv` carries a per-build
address column that matches this exe byte-for-byte). Facts were transcribed and re-verified against a
running game and against `conquests.ini` (five independent cross-checks); no code was vendored.
