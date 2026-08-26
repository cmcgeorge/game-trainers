namespace KnightsOfLegendTrainer.Game;

/// <summary>One spell in Knights of Legend. [Manual]</summary>
public sealed record SpellEntry(
    string Name,
    string Order,
    string Description);

/// <summary>
/// Spells available in Knights of Legend, grouped by magic order. Spell components
/// are: race, subclass, effect, power suffix. Learn spells from all orders before
/// joining one, since joining fixes the race component. [Manual]
/// </summary>
internal static class SpellBook
{
    public static IReadOnlyList<SpellEntry> Spells { get; } = new[]
    {
        new SpellEntry("Heal", "White Pearl",
            "Restores body points to a single character."),
        new SpellEntry("Cure Disease", "White Pearl",
            "Removes disease from a character."),
        new SpellEntry("Bless", "White Pearl",
            "Temporarily raises a character's attributes."),
        new SpellEntry("Light", "White Pearl",
            "Illuminates dark areas and dungeons."),

        new SpellEntry("Lightning Bolt", "Blue Gem",
            "Electrical damage to a single target."),
        new SpellEntry("Frost Bite", "Blue Gem",
            "Cold damage to a target; may slow movement."),
        new SpellEntry("Stone Skin", "Blue Gem",
            "Temporarily increases armor class."),

        new SpellEntry("Fireball", "Black Onyx",
            "Fire damage to a single target."),
        new SpellEntry("Wall of Fire", "Black Onyx",
            "Creates a barrier of fire that damages creatures passing through."),
        new SpellEntry("Meteor", "Black Onyx",
            "High damage area effect spell."),

        new SpellEntry("Giant Strength", "Secret Storm",
            "Temporarily grants enormous Strength."),
        new SpellEntry("Crush", "Secret Storm",
            "Crushing damage spell."),
        new SpellEntry("Rock Skin", "Secret Storm",
            "Superior armor class enhancement."),

        new SpellEntry("Dragon Breath", "Red Mist",
            "Cone of fire damage based on legendary creatures."),
        new SpellEntry("Legend Lore", "Red Mist",
            "Reveals information about legendary creatures and items."),
        new SpellEntry("Mythic Heal", "Red Mist",
            "Powerful healing spell; restores more than basic Heal."),

        new SpellEntry("Death Touch", "Dark Stone",
            "Necromantic damage; may instantly slay weak creatures."),
        new SpellEntry("Animate Dead", "Dark Stone",
            "Raises fallen creatures as temporary allies."),
        new SpellEntry("Vampiric Drain", "Dark Stone",
            "Drains body points from target and transfers to caster."),
        new SpellEntry("Curse", "Dark Stone",
            "Reduces a target's attributes temporarily."),
    };

    public static IReadOnlyList<SpellEntry> ByOrder(string order) =>
        Spells.Where(s => s.Order == order).ToList();
}
