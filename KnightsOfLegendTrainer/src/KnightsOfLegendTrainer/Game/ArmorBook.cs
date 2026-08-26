namespace KnightsOfLegendTrainer.Game;

/// <summary>One armor type in Knights of Legend. [Manual]</summary>
public sealed record ArmorEntry(
    int Id,
    string Name,
    string Category,
    string Notes);

/// <summary>
/// Armor types in Knights of Legend. Armor covers head, torso, and legs separately.
/// Weight and encumbrance matter: heavier armor reduces Quickness and movement.
/// [Manual]
/// </summary>
internal static class ArmorBook
{
    public static IReadOnlyList<ArmorEntry> Armor { get; } = new[]
    {
        new ArmorEntry(0, "Leather Armor", "Torso",
            "Light armor; low protection but minimal encumbrance. Starting armor for many classes."),
        new ArmorEntry(1, "Chain Mail", "Torso",
            "Medium armor; balanced protection and weight."),
        new ArmorEntry(2, "Plate Armor", "Torso",
            "Heavy armor; best torso protection. High encumbrance. Buy from Brettle and trade to party."),
        new ArmorEntry(3, "Leather Cap", "Head",
            "Light head protection."),
        new ArmorEntry(4, "Chain Coif", "Head",
            "Medium head protection."),
        new ArmorEntry(5, "Plate Helm", "Head",
            "Heavy head protection; best available."),
        new ArmorEntry(6, "Leather Leggings", "Legs",
            "Light leg protection."),
        new ArmorEntry(7, "Chain Leggings", "Legs",
            "Medium leg protection."),
        new ArmorEntry(8, "Plate Leggings", "Legs",
            "Heavy leg protection; best available."),
        new ArmorEntry(9, "Small Shield", "Shield",
            "Light shield; minor bonus. Can be used with one-handed weapons."),
        new ArmorEntry(10, "Large Shield", "Shield",
            "Heavier shield; better protection. Great Shield from quests is even better."),
        new ArmorEntry(11, "Great Shield", "Shield",
            "Quest reward; excellent protection and lightweight. Best shield in the game."),
    };

    public static IReadOnlyList<ArmorEntry> ByCategory(string category) =>
        Armor.Where(a => a.Category == category).ToList();

    public static ArmorEntry? ById(int id) =>
        id >= 0 && id < Armor.Count ? Armor[id] : null;
}
