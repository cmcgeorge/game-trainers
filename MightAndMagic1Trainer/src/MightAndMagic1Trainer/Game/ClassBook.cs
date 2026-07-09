namespace MightAndMagic1Trainer.Game;

/// <summary>
/// Reference data for one of the six Might &amp; Magic 1 character classes: which prime
/// attributes a character needs (each at least <see cref="ClassBook.MinPrimeValue"/>) to
/// qualify, the hit-points-per-level range, the spell school, and a short blurb.
/// Transcribed from the MM1 manual ("Character Classes" / "Vital Statistics"); the prime
/// statistics and HP ranges are quoted, the 12-point minimum is the manual's stated rule.
/// </summary>
public sealed record GameClass(
    int Id,
    string Name,
    IReadOnlyList<string> PrimeStats,
    string HitPointsPerLevel,
    SpellSchool School,
    string Description)
{
    /// <summary>"Might, Personality, Endurance" or "—" for a class with no prime stat.</summary>
    public string PrimeStatsText => PrimeStats.Count == 0 ? "—" : string.Join(", ", PrimeStats);

    /// <summary>"Might ≥ 12, Personality ≥ 12, …", or a "no minimums" note for the Robber.</summary>
    public string RequirementText => PrimeStats.Count == 0
        ? "No attribute minimums — any roll qualifies."
        : string.Join(", ", PrimeStats.Select(s => $"{s} ≥ {ClassBook.MinPrimeValue}"));

    /// <summary>"Cleric spells", "Sorcerer spells" or "No spellcasting".</summary>
    public string SpellText => School switch
    {
        SpellSchool.Cleric => "Casts Cleric spells",
        SpellSchool.Sorcerer => "Casts Sorcerer spells",
        _ => "No spellcasting",
    };
}

/// <summary>One row of the (class-independent) experience-per-level table.</summary>
public sealed record ExperienceStep(int Level, long FromPrevious, long Cumulative)
{
    public string FromPreviousText => FromPrevious.ToString("N0");
    public string CumulativeText => Cumulative.ToString("N0");
}

/// <summary>
/// The six MM1 classes and the shared experience-per-level table, as read-only reference
/// data — independent of any attached game, mirroring <see cref="Spellbook"/> and
/// <see cref="MapBook"/>. Backs the Classes tab.
/// </summary>
public static class ClassBook
{
    /// <summary>Each prime statistic must be at least this to qualify for the class (manual rule).</summary>
    public const int MinPrimeValue = 12;

    private static GameClass C(int id, string name, string[] prime, string hp, SpellSchool school, string desc)
        => new(id, name, prime, hp, school, desc);

    /// <summary>The six classes in record order (id = the byte stored at record offset 0x14).</summary>
    public static readonly IReadOnlyList<GameClass> Classes = new[]
    {
        C(1, "Knight", new[] { "Might" }, "1–12", SpellSchool.None,
            "Pure fighter with the best hit points and no spell limits on weapons or armor. Trains fastest and hits hardest, but casts nothing."),
        C(2, "Paladin", new[] { "Might", "Personality", "Endurance" }, "1–10", SpellSchool.Cleric,
            "Fighter who also learns Cleric spells. The three prime requisites make a qualifying roll rare, but it blends front-line muscle with healing."),
        C(3, "Archer", new[] { "Intellect", "Accuracy" }, "1–10", SpellSchool.Sorcerer,
            "Fighter who learns Sorcerer spells and excels with ranged weapons. A flexible hybrid that attacks at a distance and casts in a pinch."),
        C(4, "Cleric", new[] { "Personality" }, "1–8", SpellSchool.Cleric,
            "Dedicated healer and buffer. Cleric spells cover healing, protection, and undead-turning; modest in melee."),
        C(5, "Sorcerer", new[] { "Intellect" }, "1–6", SpellSchool.Sorcerer,
            "Dedicated mage with the game's offensive and utility magic (Fly, Teleport, Astral). Fewest hit points, so keep it in the back ranks."),
        C(6, "Robber", Array.Empty<string>(), "1–8", SpellSchool.None,
            "No attribute minimums, so any character can become one. Finds and disarms traps and opens locks; a handy utility slot with middling combat."),
    };

    public static GameClass? ById(int id) => Classes.FirstOrDefault(c => c.Id == id);

    // The manual states ~2000 XP to advance from level 1 to 2, with the requirement
    // "generally doubling" each level thereafter. The exact in-game figures are not
    // published in the manual, so this table is the manual's stated approximation.
    private const long BaseExperience = 2000;

    /// <summary>Number of "reach level N" rows shown in the table.</summary>
    public const int LevelRows = 12;

    /// <summary>
    /// Approximate experience to reach levels 2..(LevelRows+1): 2000 for level 2, doubling each
    /// level after, with a running cumulative total. Approximate by the manual's own wording.
    /// </summary>
    public static readonly IReadOnlyList<ExperienceStep> ExperienceTable = BuildExperienceTable();

    private static IReadOnlyList<ExperienceStep> BuildExperienceTable()
    {
        var rows = new List<ExperienceStep>(LevelRows);
        long cumulative = 0;
        for (int i = 0; i < LevelRows; i++)
        {
            int level = i + 2;                       // first attainable level past the starting level 1
            long step = BaseExperience << i;         // 2000, 4000, 8000, … (doubling)
            cumulative += step;
            rows.Add(new ExperienceStep(level, step, cumulative));
        }
        return rows;
    }
}
