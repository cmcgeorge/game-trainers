namespace AmberstarTrainer.Game;

/// <summary>Race lookup table for Amberstar characters.</summary>
public static class RaceBook
{
    public static readonly string[] Names =
        { "Human", "Elf", "Dwarf", "Gnome", "Halfling", "Half-Elf", "Half-Orc" };

    /// <summary>Race IDs used for party members (0..6). 13=Animal, 14=Monster.</summary>
    public static string Name(int race) => race switch
    {
        0 => "Human",
        1 => "Elf",
        2 => "Dwarf",
        3 => "Gnome",
        4 => "Halfling",
        5 => "Half-Elf",
        6 => "Half-Orc",
        13 => "Animal",
        14 => "Monster",
        _ => $"?({race})",
    };

    /// <summary>Selectable races for the trainer UI.</summary>
    public static readonly string[] Selectable = Names;
}
