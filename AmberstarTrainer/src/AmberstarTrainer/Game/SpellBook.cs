namespace AmberstarTrainer.Game;

/// <summary>
/// Spell names for the four Amberstar spell schools, indexed by bit position.
/// Bit N (0-based) in the school's Long bitfield corresponds to spell N+1.
/// Derived from the Pyrdacor/Amberstar Spells file specification.
/// </summary>
public static class SpellBook
{
    public enum School { White = 0, Grey = 1, Black = 2, Special = 3 }

    public static readonly string[] SchoolNames = { "White", "Grey", "Black", "Special" };

    public static readonly string[] WhiteSpells =
    {
        "Healing 1", "Healing 2", "Healing 3", "Healing 4", "Healing 5",
        "Salvation", "Reincarnation", "Conversion of Ashes", "Conversion of Dust",
        "Neutralise Poison", "Heal Stun", "Heal Sickness", "Rejuvenation",
        "De-Petrification", "Wake Up", "Calm Panic", "Remove Irritation",
        "Heal Blindness", "Heal Madness", "Stun", "Sleep", "Fear",
        "Irritation", "Blind", "Destroy Undead", "Holy Word", "Remove Curse",
        "Provide Food"
    };

    public static readonly string[] GreySpells =
    {
        "Light 1", "Light 2", "Light 3",
        "Armour Protection 1", "Armour Protection 2", "Armour Protection 3",
        "Weapons Power 1", "Weapons Power 2", "Weapons Power 3",
        "Anti-Magic 1", "Anti-Magic 2", "Anti-Magic 3",
        "Clairvoyance 1", "Clairvoyance 2", "Clairvoyance 3",
        "Invisibility 1", "Invisibility 2", "Invisibility 3",
        "Magic Sphere", "Magic Compass", "Identification",
        "Levitation", "Haste", "Mass Haste", "Teleport", "X-Ray Vision"
    };

    public static readonly string[] BlackSpells =
    {
        "Beam of Fire", "Wall of Fire", "Fireball", "Fire Storm", "Fire Cascade",
        "Waterhole", "Waterfall", "Ice Ball", "Ice Shower", "Hail Storm",
        "Mud Catapult", "Falling Rock", "Bog", "Landslide", "Earthquake",
        "Strong Wind", "Storm", "Tornado", "Thunder", "Hurricane",
        "Desintegration", "Magic Arrows"
    };

    public static readonly string[] SpecialSpells =
    {
        "Stunned", "Poison", "Flesh to Stone", "Make Ill", "Aging",
        "Irritation", "Make Mad", "Sleep", "Panic", "Blinding Flash",
        "Flesh To Stone", "Mapshow", "Banish Demon", "Spellpoints 1",
        "Spellpoints 2", "Weapon Balm", "Youth", "Pick Lock",
        "Eagle Call", "Music"
    };

    public static string[] Spells(School school) => school switch
    {
        School.White => WhiteSpells,
        School.Grey => GreySpells,
        School.Black => BlackSpells,
        School.Special => SpecialSpells,
        _ => Array.Empty<string>(),
    };

    /// <summary>Total spell count across all schools.</summary>
    public static int TotalCount =>
        WhiteSpells.Length + GreySpells.Length + BlackSpells.Length + SpecialSpells.Length;
}
