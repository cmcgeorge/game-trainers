namespace BattleTech1Trainer.Game;

/// <summary>One character skill and what it governs.</summary>
public readonly record struct SkillInfo(int Index, string Name, string Governs);

/// <summary>
/// The character skill model, all <b>Confirmed</b> from <c>BTECH.EXE</c> (see
/// <c>.docs/ReverseEngineering.md</c> §3.3–§3.4): a character carries seven skills, each stored as a
/// 0–4 ordinal whose label runs <c>Unskilled → Amateur → Adequate → Good → Excellent</c>. The trainer
/// surfaces this so a player scanning a skill byte knows the value is a small ordinal (Byte scan) and
/// what each number means.
/// </summary>
public static class SkillSheet
{
    /// <summary>Number of skills each character tracks.</summary>
    public const int SkillCount = 7;

    /// <summary>The proficiency labels, indexed by the stored 0–4 ordinal.</summary>
    public static readonly IReadOnlyList<string> Levels = Array.AsReadOnly(new[]
    {
        "Unskilled", "Amateur", "Adequate", "Good", "Excellent",
    });

    /// <summary>Highest valid skill ordinal (Excellent).</summary>
    public static int MaxLevel => Levels.Count - 1;

    /// <summary>The seven skills, in Inspect-Character screen order.</summary>
    public static readonly IReadOnlyList<SkillInfo> Skills = Array.AsReadOnly(new SkillInfo[]
    {
        new(0, "Bow & Blade", "Melee and bows on foot"),
        new(1, "Pistol",      "Pistols on foot"),
        new(2, "Rifle",       "Rifles on foot"),
        new(3, "Gunnery",     "'Mech weapon accuracy"),
        new(4, "Piloting",    "'Mech movement and balance"),
        new(5, "Tech",        "Salvage and 'Mech repair"),
        new(6, "Medical",     "Healing characters"),
    });

    /// <summary>
    /// Maps a stored ordinal to its proficiency label. Values outside 0–<see cref="MaxLevel"/> are
    /// clamped-described so a stray scan byte never throws.
    /// </summary>
    public static string DescribeLevel(int ordinal)
    {
        if (ordinal < 0) return $"(invalid {ordinal})";
        if (ordinal >= Levels.Count) return $"{Levels[^1]}+ ({ordinal})";
        return Levels[ordinal];
    }
}
