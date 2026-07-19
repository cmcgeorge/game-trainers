namespace MoriaTrainer.Game;

/// <summary>One of the six UMoria character classes (Confirmed from <c>moria1.txt</c> §2.5 and <c>FEATURES.NEW</c>).</summary>
public sealed record ClassInfo(
    int Id,
    string Name,
    string PrimeStat,
    string HitDie,
    string ManaBasis,
    string Notes,
    int FightGain, int BowGain, int DeviceGain, int DisarmGain, int ThrowGain)
{
    /// <summary>Skill-gain rates from <c>FEATURES.NEW</c> (higher = faster; 3 = old baseline).</summary>
    public string SkillRow => $"{FightGain}/{BowGain}/{DeviceGain}/{DisarmGain}/{ThrowGain}";
}

/// <summary>The six playable classes (Confirmed from the manual + class ids in <c>constant.h</c>).</summary>
public static class ClassBook
{
    public const int ClassWarrior = 0;
    public const int ClassMage    = 1;
    public const int ClassPriest  = 2;
    public const int ClassRogue   = 3;
    public const int ClassRanger  = 4;
    public const int ClassPaladin = 5;

    public static readonly IReadOnlyList<ClassInfo> Classes = new[]
    {
        new ClassInfo(ClassWarrior, "Warrior", "STR", "d10", "none",
            "Best melee; fastest fighting-skill gain; no spells.",
            4, 4, 2, 2, 3),
        new ClassInfo(ClassMage, "Mage", "INT", "d4", "INT-based",
            "Best magic; slowest melee-skill gain. Must carry magic books.",
            2, 2, 4, 3, 3),
        new ClassInfo(ClassPriest, "Priest", "WIS", "d4", "WIS-based",
            "Blunt weapons only; strong vs undead; holy prayers.",
            2, 2, 4, 3, 3),
        new ClassInfo(ClassRogue, "Rogue", "DEX", "d6", "INT-based",
            "Best disarm/search/stealth; cheap spells. Ideal thief.",
            3, 4, 3, 4, 3),
        new ClassInfo(ClassRanger, "Ranger", "DEX", "d8", "INT-based",
            "Good bows + dual-class magic. Strong explorer.",
            3, 4, 3, 3, 3),
        new ClassInfo(ClassPaladin, "Paladin", "STR", "d8", "WIS-based",
            "Holy weapons + priest prayers. Tanky hybrid.",
            3, 3, 3, 2, 3),
    };

    public static ClassInfo? ById(int id) => id >= 0 && id < Classes.Count ? Classes[id] : null;
}
