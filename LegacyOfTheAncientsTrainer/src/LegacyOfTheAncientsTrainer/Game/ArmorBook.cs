namespace LegacyOfTheAncientsTrainer.Game;

/// <summary>Information about a single Legacy of the Ancients armor type.</summary>
public sealed record ArmorInfo(int Id, string Name);

/// <summary>
/// The five armor types of Legacy of the Ancients, from the game manual and walkthrough.
/// Each armor comes in five qualities: Shoddy, Fair, Good, Great, Superb.
/// </summary>
public static class ArmorBook
{
    public static readonly ArmorInfo[] Armors =
    {
        new(0, "Studded Hide"),
        new(1, "Ring Mail"),
        new(2, "Double Mail"),
        new(3, "Plated Mail"),
        new(4, "Mythan Plate"),
    };

    public static int Count => Armors.Length;

    public static readonly string[] Qualities =
        { "Shoddy", "Fair", "Good", "Great", "Superb" };

    public static string Name(int id) =>
        id >= 0 && id < Armors.Length ? Armors[id].Name : $"?({id})";
}
