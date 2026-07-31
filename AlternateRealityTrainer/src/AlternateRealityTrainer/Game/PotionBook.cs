namespace AlternateRealityTrainer.Game;

/// <summary>How dangerous a single sip of a potion is.</summary>
public enum SipRisk
{
    Safe,
    Caution,
    Danger,
    Dangerous,
    Unsure,
}

/// <summary>One row of the potion identification table.</summary>
public readonly record struct PotionInfo(string Colour, string Taste, SipRisk Sip, string Effect)
{
    public string SipLabel => Sip switch
    {
        SipRisk.Safe => "Safe",
        SipRisk.Caution => "Caution",
        SipRisk.Danger => "Danger",
        SipRisk.Dangerous => "Dangerous",
        _ => "Unsure",
    };
}

/// <summary>
/// The potion colour/taste table from the <c>alternate.txt</c> hint file shipped with the game.
///
/// This is a guide, not a lookup: what a sealed potion turns out to be is decided randomly the
/// moment you unseal it. High Wisdom and Intelligence make identification easier.
/// </summary>
public static class PotionBook
{
    public static readonly IReadOnlyList<PotionInfo> All = new[]
    {
        new PotionInfo("Amber",  "Plain",    SipRisk.Caution,   "Cure Poison"),
        new PotionInfo("Amber",  "Plain",    SipRisk.Dangerous, "Poison"),
        new PotionInfo("Amber",  "Sour",     SipRisk.Safe,      "Spirits"),
        new PotionInfo("Amber",  "Sour",     SipRisk.Safe,      "Beer"),
        new PotionInfo("Black",  "Acidic",   SipRisk.Caution,   "Invulnerability Fire"),
        new PotionInfo("Black",  "Alkaline", SipRisk.Caution,   "Invulnerability Water"),
        new PotionInfo("Black",  "Bitter",   SipRisk.Caution,   "Invulnerability Mental"),
        new PotionInfo("Black",  "Bitter",   SipRisk.Unsure,    "Delusion"),
        new PotionInfo("Black",  "Dry",      SipRisk.Caution,   "Invulnerability Power"),
        new PotionInfo("Black",  "Plain",    SipRisk.Caution,   "Invulnerability Sharp"),
        new PotionInfo("Black",  "Plain",    SipRisk.Caution,   "Invulnerability Blunt"),
        new PotionInfo("Black",  "Plain",    SipRisk.Caution,   "Fleetness"),
        new PotionInfo("Black",  "Salty",    SipRisk.Caution,   "Invulnerability Air"),
        new PotionInfo("Black",  "Sour",     SipRisk.Safe,      "Beer"),
        new PotionInfo("Black",  "Sour",     SipRisk.Dangerous, "Strong Poison"),
        new PotionInfo("Black",  "Sour",     SipRisk.Caution,   "Invulnerability Earth"),
        new PotionInfo("Black",  "Sweet",    SipRisk.Caution,   "Invulnerability Cleric"),
        new PotionInfo("Clear",  "Acidic",   SipRisk.Safe,      "Cure"),
        new PotionInfo("Clear",  "Acidic",   SipRisk.Caution,   "Water"),
        new PotionInfo("Clear",  "Acidic",   SipRisk.Dangerous, "Acid"),
        new PotionInfo("Clear",  "Acidic",   SipRisk.Caution,   "Cleanse"),
        new PotionInfo("Clear",  "Bitter",   SipRisk.Caution,   "Unnoticeability"),
        new PotionInfo("Clear",  "Dry",      SipRisk.Caution,   "Mineral Water"),
        new PotionInfo("Clear",  "Dry",      SipRisk.Caution,   "Invisibility"),
        new PotionInfo("Clear",  "Plain",    SipRisk.Caution,   "Water"),
        new PotionInfo("Clear",  "Plain",    SipRisk.Caution,   "Invisibility"),
        new PotionInfo("Clear",  "Salty",    SipRisk.Safe,      "Salt Water"),
        new PotionInfo("Green",  "Sour",     SipRisk.Caution,   "Heal Minor Wounds"),
        new PotionInfo("Green",  "Sweet",    SipRisk.Dangerous, "Ugliness (−1 Charm)"),
        new PotionInfo("Orange", "Bitter",   SipRisk.Safe,      "Inebriation"),
        new PotionInfo("Orange", "Sour",     SipRisk.Caution,   "Protection +2"),
        new PotionInfo("Orange", "Sweet",    SipRisk.Caution,   "Protection +1"),
        new PotionInfo("Orange", "Sweet",    SipRisk.Dangerous, "Dumbness (−1 Intelligence)"),
        new PotionInfo("Red",    "Acidic",   SipRisk.Safe,      "Vinegar"),
        new PotionInfo("Red",    "Bitter",   SipRisk.Caution,   "Strength"),
        new PotionInfo("Red",    "Dry",      SipRisk.Safe,      "Wine"),
        new PotionInfo("Red",    "Sweet",    SipRisk.Caution,   "Treasure Finding"),
        new PotionInfo("Red",    "Sweet",    SipRisk.Dangerous, "Deadly Poison"),
        new PotionInfo("Red",    "Sweet",    SipRisk.Caution,   "Fruit Juice"),
        new PotionInfo("Silver", "Bitter",   SipRisk.Danger,    "Weak Poison"),
        new PotionInfo("Silver", "Bitter",   SipRisk.Caution,   "Intelligence"),
        new PotionInfo("Silver", "Plain",    SipRisk.Caution,   "Cure Major Wounds"),
        new PotionInfo("Silver", "Sweet",    SipRisk.Caution,   "Charisma"),
        new PotionInfo("White",  "Alkaline", SipRisk.Caution,   "Milk"),
        new PotionInfo("White",  "Alkaline", SipRisk.Caution,   "Healing"),
        new PotionInfo("White",  "Alkaline", SipRisk.Dangerous, "Poison"),
        new PotionInfo("White",  "Bitter",   SipRisk.Dangerous, "Slowness"),
        new PotionInfo("White",  "Salty",    SipRisk.Caution,   "Heal All"),
        new PotionInfo("Yellow", "Bitter",   SipRisk.Caution,   "Noticeability"),
        new PotionInfo("Yellow", "Dry",      SipRisk.Dangerous, "Weakness (−1 Strength)"),
        new PotionInfo("Yellow", "Plain",    SipRisk.Caution,   "Cure Wounds"),
    };

    /// <summary>The effects worth saving rather than drinking on the spot.</summary>
    public static readonly IReadOnlyList<string> WorthHoarding = new[]
    {
        "Fleetness", "Protection +1", "Protection +2", "Treasure Finding",
    };
}
