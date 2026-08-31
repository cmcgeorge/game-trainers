namespace LegacyOfTheAncientsTrainer.Game;

/// <summary>Information about a single Legacy of the Ancients weapon.</summary>
public sealed record WeaponInfo(int Id, string Name);

/// <summary>
/// The weapons of Legacy of the Ancients, from the game manual and walkthrough.
/// Each weapon comes in five qualities: Shoddy, Fair, Good, Great, Superb.
/// </summary>
public static class WeaponBook
{
    public static readonly WeaponInfo[] Weapons =
    {
        new(0, "Bare Hands"),
        new(1, "Knife"),
        new(2, "Leaded Club"),
        new(3, "Bladed Staff"),
        new(4, "Flail"),
        new(5, "War Hammer"),
        new(6, "Bow & Arrow"),
        new(7, "Broadaxe"),
        new(8, "Compound Bow"),
    };

    public static int Count => Weapons.Length;

    public static readonly string[] Qualities =
        { "Shoddy", "Fair", "Good", "Great", "Superb" };

    public static string Name(int id) =>
        id >= 0 && id < Weapons.Length ? Weapons[id].Name : $"?({id})";
}
