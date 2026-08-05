namespace TheQuestTrainer.Game;

/// <summary>
/// Rules of the game the trainer has to respect, and the fingerprint of the build they were read
/// from. Nothing here is a memory address; <see cref="QuestLayout"/> owns those.
/// </summary>
public static class GameFacts
{
    /// <summary>Process name to attach to (no extension), as GOG and Steam both ship it.</summary>
    public const string ProcessName = "TheQuest";

    /// <summary>
    /// Substrings that make a process worth offering in the picker even when the name is not exact
    /// — an expansion launcher, say. Deliberately never auto-selected: the trainer's own process is
    /// <c>TheQuestTrainer</c>, which contains every one of these.
    /// </summary>
    public static readonly IReadOnlyList<string> TargetHints = new[] { "quest" };

    /// <summary>The build the offsets were measured on: v1.9.10, linked 2020-02-27 12:58:47 UTC.</summary>
    public const uint KnownTimeDateStamp = 0x5E57_BD07;

    /// <summary>Human-readable name of that build, for the status line.</summary>
    public const string KnownVersion = "v1.9.10 (GOG, 2020-02-27)";

    /// <summary>
    /// Entries in the per-level experience table embedded in every character record. The table
    /// covers levels 2..99, so its length is <see cref="MaxLevel"/> - 1.
    /// </summary>
    public const int ExperienceTableEntries = MaxLevel - 1;

    /// <summary>Highest level the experience table can express.</summary>
    public const int MaxLevel = 99;

    /// <summary>Attribute array width. Ids run 1..5; slot 0 is the game's "no attribute" and is unused.</summary>
    public const int AttributeSlots = 6;

    /// <summary>Skill array width. Ids run 1..20; slot 0 is the game's "no skill" and is unused.</summary>
    public const int SkillSlots = 21;

    /// <summary>Length of the skill display-order byte array that separates the two point counters.</summary>
    public const int SkillDisplayOrderBytes = 20;

    /// <summary>
    /// Ceiling the trainer clamps attributes and skills to.
    ///
    /// Both are stored as unsigned words, so the game would take far more, but health, mana, armour
    /// and damage are all derived from them inside 16-bit arithmetic. 250 leaves every derived
    /// value comfortably inside its range while being far above anything the game hands out.
    /// </summary>
    public const int MaxAttributeOrSkill = 250;

    /// <summary>Floor for an attribute. Zero is a legal word but not a legal character.</summary>
    public const int MinAttribute = 1;

    /// <summary>Health and mana are unsigned words; the game happily displays a current above the maximum.</summary>
    public const int MaxHealthOrMana = 60000;

    /// <summary>
    /// Ceiling the trainer clamps gold to. The field is a full <c>uint</c> and shop code compares it
    /// unsigned, but prices are summed into the same width, so leaving three decimal digits of
    /// headroom keeps a big purchase from wrapping.
    /// </summary>
    public const uint MaxGold = 999_999_999;

    /// <summary>Fame runs -100 (Demonic) to +100 (Saint).</summary>
    public const int MinFame = -100;

    /// <inheritdoc cref="MinFame"/>
    public const int MaxFame = 100;

    /// <summary>Unspent attribute and skill points are words; this is the trainer's own ceiling.</summary>
    public const int MaxPoints = 9999;

    /// <summary>
    /// The game's own rule, shown on the skills screen: a skill's base value may not exceed twice
    /// the base value of its governing attribute. The trainer uses it for "Max skills" rather than
    /// enforcing it on every edit — the game does not re-clamp a value written from outside, and a
    /// player who asks for more should get it.
    /// </summary>
    public static int SkillCapFor(int governingAttributeBase) =>
        Math.Clamp(governingAttributeBase * 2, 0, MaxAttributeOrSkill);
}
