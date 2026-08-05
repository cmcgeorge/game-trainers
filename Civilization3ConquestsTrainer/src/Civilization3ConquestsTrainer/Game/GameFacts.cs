namespace Civilization3ConquestsTrainer.Game;

/// <summary>
/// Facts about the target that are true regardless of what is going on inside it: which process to
/// attach to, which build the layout table was recovered against, and the handful of game rules the
/// UI needs in order to clamp an edit sensibly.
/// </summary>
public static class GameFacts
{
    /// <summary><see cref="System.Diagnostics.Process.ProcessName"/> of the game (no ".exe").</summary>
    public const string ProcessName = "Civ3Conquests";

    /// <summary>Substrings that sort a process to the top of the picker. Cosmetic only.</summary>
    public static readonly string[] TargetHints = { "civ3", "conquests" };

    /// <summary>Preferred image base. The exe sets no DYNAMICBASE bit, so it is never relocated.</summary>
    public const uint ImageBase = 0x00400000;

    // --- build fingerprint ---------------------------------------------------------------------
    // The layout table was recovered against exactly one build. The fingerprint is *reported* rather
    // than used as a gate: what actually decides whether an address is trusted is the 32-slot
    // validation in GameLocator, which is strictly stronger than a timestamp comparison (a rebuild
    // that left the layout alone should still work; one that moved it fails validation whatever the
    // timestamp says). A mismatch here surfaces in the status bar so the user knows to distrust the
    // numbers, and the locator prefers whichever chain validates more slots.

    /// <summary>Size on disk of the Steam "Civilization III Complete" v1.22 executable.</summary>
    public const long KnownFileSize = 3_518_464;

    /// <summary>PE <c>TimeDateStamp</c> of that build (2015-03-19).</summary>
    public const uint KnownTimeDateStamp = 0x550A3E1F;

    /// <summary>Human-readable name of the build the offsets came from.</summary>
    public const string KnownBuildName = "Steam / Civilization III Complete, Conquests v1.22";

    // --- game rules the UI clamps against --------------------------------------------------------

    /// <summary>Leader slots in the static <c>leaders</c> array (index 0 is the barbarians).</summary>
    public const int MaxPlayers = 32;

    /// <summary>The barbarian pseudo-player always occupies slot 0.</summary>
    public const int BarbarianCivId = 0;

    /// <summary>
    /// Civ3's tax/science/luxury sliders are stored in tens of percent, so the three always sum to
    /// exactly 10. That invariant is both a UI clamp and one of the locator's validators.
    /// </summary>
    public const int SliderTotal = 10;

    /// <summary>
    /// Upper bound for an era edit. The epic game ships four eras (Ancient, Middle Ages, Industrial,
    /// Modern), but a conquest or a mod defines its own set in the loaded BIC, so this is a loose
    /// sanity bound rather than the rule — the game clamps to whatever its own era table holds.
    /// </summary>
    public const int MaxEraIndex = 15;

    /// <summary>
    /// Highest legal value of <c>Combat_Experience</c>: the ladder is 0 Conscript, 1 Regular,
    /// 2 Veteran, 3 Elite, so the ceiling is an <i>index</i>, not a count. Writing 4 would index past
    /// the game's own four-entry veteran table when it derives maximum hit points.
    /// </summary>
    public const int MaxCombatExperience = 3;

    /// <summary>The amount "Max treasury" starts out offering; the user can type any other amount into
    /// the toolbar box, bounded by <see cref="Civ3Layout.IsPlausibleTreasury"/>. Deliberately short of
    /// int32 range: the game adds income to this every turn, and a value near
    /// <see cref="int.MaxValue"/> would overflow into debt.</summary>
    public const int MaxTreasuryPreset = 100_000_000;

    /// <summary>
    /// What "Max food" and "Max shields" bank in each city. Far above any granary size or build cost,
    /// so the city grows and completes whatever it is building on its next turn, but small enough that
    /// the game's own per-turn arithmetic on it cannot overflow.
    ///
    /// <para>The two are separate buttons, and the combined action fills shields only, because food is
    /// not an unmixed blessing: a full granary makes the city grow every turn, and growth outruns
    /// happiness — new citizens arrive discontented, and a city can tip into disorder, which produces
    /// nothing at all. Shields have no such downside.</para>
    /// </summary>
    public const int MaxCityStorePreset = 5_000;

    /// <summary>
    /// What "Max culture" writes into each city's <c>cultural_level</c>. That field is the
    /// border-expansion <i>ladder</i>, not accumulated culture: the level indexes the loaded ruleset's
    /// own culture-level table, which the epic game fills with a handful of entries and a conquest or
    /// a mod resizes freely. So "max" here means "past anything the epic game reaches", not a true
    /// ceiling — and it is deliberately a small number, because the offset itself is <b>[Inferred]</b>
    /// (see <c>docs/ReverseEngineering.md</c> §4.4) and a huge level would index a long way past
    /// whatever table the game reads. The grid still accepts any level 0–100 typed by hand.
    /// </summary>
    public const int MaxCityCulturePreset = 6;

    /// <summary>
    /// What "Finish research" banks in <c>Research_Bulbs</c>. Civ3 completes an advance when the
    /// accumulated research points reach that advance's cost, and it does the comparison at the turn
    /// boundary — so the tech lands when you end the turn, not the instant this is written.
    ///
    /// <para>The threshold itself is not read: an advance's cost is derived from the rules database,
    /// the difficulty and how many civs already know it, none of which is confirmed here. This value
    /// is simply chosen to exceed any real Civ3 tech cost by a wide margin while staying nowhere near
    /// <see cref="int.MaxValue"/>, so whatever carry-over arithmetic the game does cannot overflow.</para>
    ///
    /// <para><b>Why it is this large.</b> It was 30,000 — already past any epic-game advance cost — and
    /// a live game still took a few more turns to hand the tech over. Raising it to a million costs
    /// nothing and rules out "the advance simply cost more than we banked" for even a modded or
    /// late-game tree. It does <i>not</i> make the tech arrive instantly, and the reason is very likely
    /// not the amount at all: Civ3 appears to enforce a floor on how few turns an advance can take, so
    /// banked points buy turns down to that floor and no further. That floor is <b>unconfirmed</b> —
    /// see <c>docs/ReverseEngineering.md</c> §4.2 for what would settle it.</para>
    /// </summary>
    public const int FinishResearchBulbs = 1_000_000;

    // --- worker jobs -----------------------------------------------------------------------------

    /// <summary>
    /// Loose upper bound on a job's <c>TurnToComplete</c>, used to reject a garbage record rather than
    /// to express a rule. The epic game's most expensive job is Clear Damage at 24; a mod is free to
    /// charge far more, so this only has to be low enough that random memory fails it.
    /// </summary>
    public const int MaxWorkerJobTurnToComplete = 10_000;

    /// <summary>
    /// What the real cost of a job can exceed its <c>TurnToComplete</c> by.
    ///
    /// <para>Civ3 multiplies the base cost by a factor it derives from the tile — mining a mountain
    /// costs more than mining grassland — and that factor is <b>not decoded here</b>: the trainer sees
    /// the multiply happen at <c>0x5D557D</c> but not what feeds it. Terrain movement cost tops out at 3
    /// in the epic rules, so 4 clears it with room to spare while keeping "finish this job" a number of
    /// worker-turns rather than an arbitrary large one. If a mod ever charged more, the job would
    /// shorten rather than finish, which is the harmless direction to be wrong in.</para>
    /// </summary>
    public const int WorkerJobTerrainFactorCeiling = 4;

    /// <summary>
    /// What "Instant worker jobs" writes into every job's <c>TurnToComplete</c>. One worker-turn is the
    /// floor: the cost is still multiplied by the terrain factor, and the game still applies the work at
    /// the turn boundary, so this makes jobs finish next turn rather than on the spot.
    ///
    /// <para>Unlike every other action here this is <b>rules data, shared with the AI</b> — the job table
    /// belongs to the loaded ruleset, not to a player, so every civ's workers speed up together. That is
    /// the same objection that rules out buffing <c>UnitType.Defence</c> for invincibility, which is why
    /// this is an explicit toggle that restores the original costs when it is switched off or the
    /// trainer detaches, rather than a one-way button.</para>
    /// </summary>
    public const int InstantWorkerJobTurns = 1;

    /// <summary>Poll/freeze interval. Civ3 recomputes economy and unit state at turn boundaries.</summary>
    public const int PollIntervalMs = 500;
}
