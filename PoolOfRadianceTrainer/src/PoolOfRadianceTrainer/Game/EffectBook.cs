namespace PoolOfRadianceTrainer.Game;

/// <summary>One assignable character effect ("power"): its type byte and display name.</summary>
public sealed record EffectInfo(byte Code, string Name, bool Beneficial)
{
    public string Hex => $"0x{Code:X2}";
}

/// <summary>
/// The Pool of Radiance effect/"power" dictionary. The type byte of an effect equals the
/// (1-based) line number of its name in the Gold Box Companion "Effects.txt" for Pool of
/// Radiance; those names are transcribed here so the trainer is self-contained (like
/// <see cref="MonsterBook"/>/<see cref="SpellBook"/>). Unknown/unused slots ($39, $3C, …) are
/// omitted. Effects are stored per-character in the save's CHRDATAn.SPC file — see
/// <see cref="SaveGame"/>. '+' effects are beneficial, '-' detrimental (per the source list).
/// </summary>
public static class EffectBook
{
    public static readonly IReadOnlyList<EffectInfo> All = new EffectInfo[]
    {
        new(1,  "blessed", true),
        new(2,  "cursed", false),
        new(3,  "sword vs undead", true),
        new(4,  "studying manual of bodily health", true),
        new(5,  "detecting magic", true),
        new(6,  "flame tongue weapon", true),
        new(7,  "training with manual of bodily health", true),
        new(8,  "protected from evil", true),
        new(9,  "protected from good", true),
        new(10, "cold resistant", true),
        new(11, "charmed", false),
        new(12, "enlarged", true),
        new(13, "reduced", false),
        new(14, "friendly", true),
        new(15, "slow poison", true),
        new(16, "reading magic", true),
        new(17, "shielded", true),
        new(18, "gnome THAC0 bonus", true),
        new(19, "find traps", true),
        new(20, "fire resistant", true),
        new(21, "silenced", false),
        new(22, "slow poison wears off", true),
        new(23, "spiritual hammer", true),
        new(24, "sees invisible", true),
        new(25, "invisible", true),
        new(26, "dwarf THAC0 bonus", true),
        new(27, "feather fall", true),
        new(28, "duplicated", true),
        new(29, "enfeebled", false),
        new(30, "nauseated", false),
        new(31, "helpless", false),
        new(32, "animating dead", true),
        new(33, "blind", false),
        new(34, "diseased", false),
        new(35, "prayer", true),
        new(36, "accursed", false),
        new(37, "blinking", true),
        new(38, "strengthened", true),
        new(39, "hasted", true),
        new(40, "in stinking cloud", false),
        new(41, "prot. normal missiles", true),
        new(42, "slowed", false),
        new(43, "diseased (strength)", false),
        new(44, "diseased (hp)", false),
        new(45, "prot. from evil, 10'", true),
        new(46, "prot. from good, 10'", true),
        new(47, "dwarf giant bonus", true),
        new(48, "gnome large monster bonus", true),
        new(49, "prayer", true),
        new(50, "diseased (mummy)", false),
        new(51, "charmed snake", false),
        new(52, "held", false),
        new(53, "asleep", false),
        new(54, "repulsed", false),
        new(55, "poisoned", false),
        new(56, "invisible (ring)", true),
        new(58, "paralyzed", false),
        new(59, "regenerating", true),
        new(61, "fire resistant (ring)", true),
        new(62, "regeneration", true),
        new(64, "poison attack", true),
        new(65, "poison attack (+4 to save)", true),
        new(66, "poison attack (+2 to save)", true),
        new(67, "paralysis melee attack", true),
        new(68, "paralysis melee attack (not elves)", true),
        new(69, "paralysis melee attack (-2 to save)", true),
        new(70, "poison melee attack (-2 to save)", true),
        new(71, "invisible", true),
        new(72, "camouflaged", true),
        new(73, "rear claw rake", true),
        new(76, "blood draining attack", true),
        new(77, "bite and hold attack", true),
        new(79, "fire touch attack", true),
        new(80, "ankheg acid melee attack", true),
        new(81, "dragon fear aura", true),
        new(82, "mummy fear aura", true),
        new(83, "petrifying gaze", true),
        new(84, "charming gaze", true),
        new(85, "drain 1 level", true),
        new(86, "drain 2 levels", true),
        new(87, "disease melee attack", true),
        new(88, "breathes electricity", true),
        new(89, "displaced", true),
        new(90, "halfling poison bonus", true),
        new(91, "immunity to electricity", true),
        new(93, "half damage from fire", true),
        new(94, "half damage from blunt/piercing weapons", true),
        new(95, "fighting on after 0 to -6 hit points", true),
        new(96, "immunity to non-silver/non-magical weapons", true),
        new(97, "dwarf save bonus", true),
        new(98, "regenerate 3 hp/round", true),
        new(99, "keeps fighting after becoming unconscious", true),
        new(100, "troll fire/acid vulnerability", false),
        new(101, "regenerate 3 hp/round, return from death in 3d6 rounds", true),
        new(103, "immune to non-magical weapons", true),
        new(104, "thri-kreen missile evasion", true),
        new(106, "resist magic 100%", true),
        new(107, "90% sleep/charm resist", true),
        new(108, "immunity to sleep/charm", true),
        new(109, "immunity to paralysis (hold person, wand)", true),
        new(110, "immunity to cold", true),
        new(111, "immunity to paralysis/poison", true),
        new(112, "immunity to fire", true),
        new(113, "efreeti fire resistance", true),
        new(114, "half damage from electricity", true),
        new(115, "half damage from piercing/slashing weapons", true),
        new(116, "half damage from magical weapons", true),
        new(117, "vulnerability to holy water", false),
        new(118, "half damage from cold", true),
        new(119, "immunity to non-magical weapons", true),
        new(120, "boulder evasion", true),
        new(121, "ankheg acid squirt attack", true),
        new(122, "vulnerability to fire", false),
        new(123, "immunity to non-silver / non-magical weapons", true),
        new(124, "30% sleep/charm resist", true),
        new(125, "immunity to sleep/charm/paralysis/poison", true),
        new(126, "immune to gaze attacks", true),
        new(127, "reflectable gaze", false),
    };

    // GroupBy + First so an accidental duplicate Code in a future edit can't throw at first use.
    private static readonly Dictionary<byte, EffectInfo> ByCode =
        All.GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.First());

    /// <summary>Display name for an effect type byte, or its hex code if unknown/unnamed.</summary>
    public static string Name(int code) =>
        ByCode.TryGetValue((byte)code, out var e) ? e.Name : $"unknown (0x{code:X2})";

    public static bool IsKnown(int code) => ByCode.ContainsKey((byte)code);

    public static IEnumerable<EffectInfo> Search(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return All;
        return All.Where(e => e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                              || e.Hex.Contains(filter, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A curated "survival" set: keeps characters fighting and self-healing without inflating HP.</summary>
    public static readonly byte[] SurvivalSet = { 99, 101, 62 };
}
