namespace DarklandsTrainer.Game;

/// <summary>One primary attribute and what it governs, for the Reference tab.</summary>
public readonly record struct AttributeInfo(int Index, string Name, string Governs);

/// <summary>
/// The six primary attributes plus the party-wide Divine Favor, <b>Confirmed</b> from the executable's
/// stat-screen table and from the DEFAULT save (see <c>.docs/ReverseEngineering.md</c> §2.1, §3.2). The
/// six are stored as single bytes in a current block and an identical maximum block; Endurance and
/// Strength additionally take temporary combat damage (the combat data-dump pairs each with a Max).
/// </summary>
public static class AttributeBook
{
    /// <summary>Number of primary attributes in the current/max blocks.</summary>
    public const int PrimaryCount = 6;

    /// <summary>Typical usable ceiling for a primary attribute (the DEFAULT block caps entries with a 0x63/99 byte).</summary>
    public const int PracticalMax = 99;

    /// <summary>The six primary attributes, in save-file / stat-screen order.</summary>
    public static readonly IReadOnlyList<AttributeInfo> Primary = Array.AsReadOnly(new AttributeInfo[]
    {
        new(0, "Endurance",    "Health pool; takes combat/injury damage (current + max)."),
        new(1, "Strength",     "Melee damage; carry weight; takes temporary damage (current + max)."),
        new(2, "Agility",      "Speed, hit and dodge chance in tactical combat."),
        new(3, "Perception",   "Spotting ambushes, missile accuracy, awareness."),
        new(4, "Intelligence", "Alchemy, learning speed, Latin and reading."),
        new(5, "Charisma",     "NPC reactions, speech, leadership and recruiting."),
    });

    /// <summary>Party-wide seventh value that follows the six on the record.</summary>
    public static readonly AttributeInfo DivineFavor =
        new(6, "Divine Favor", "Standing with the saints; spent when praying for miracles.");
}
