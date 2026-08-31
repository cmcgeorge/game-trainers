namespace LegacyOfTheAncientsTrainer.Game;

/// <summary>Information about a single Legacy of the Ancients spell.</summary>
public sealed record SpellInfo(int Id, string Name, string Description, int MaxCharges, bool DungeonOnly);

/// <summary>
/// The six spells of Legacy of the Ancients, from the game manual and walkthrough.
/// All are buyable at Magic Shops in towns; spells are used up when cast.
/// </summary>
public static class SpellBook
{
    public static readonly SpellInfo[] Spells =
    {
        new(0, "Magic Flame",
            "Ranged magical attack. Affects one target. Usable above or below ground.",
            99, false),
        new(1, "Firebolt",
            "More powerful ranged magical attack. Twice the potency of Magic Flame. One target.",
            99, false),
        new(2, "Befuddle",
            "Confuses and disables a target for 25-35 turns. Dungeon only. May backfire.",
            99, true),
        new(3, "Psycho Strength",
            "Gives superhuman strength for 20-30 attacks. Dungeon only. May not work.",
            99, true),
        new(4, "Kill Flash",
            "Ultimate killing spell. Eliminates the target and many nearby monsters. Dungeon only.",
            20, true),
        new(5, "Seek",
            "Transports you to the front doors of the Museum. Wilderness only (main continent).",
            99, false),
    };

    public static int Count => Spells.Length;
}
