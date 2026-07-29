namespace AmberstarTrainer.Game;

/// <summary>Class lookup table for Amberstar characters.</summary>
public static class ClassBook
{
    public static string Name(int cls) => cls switch
    {
        0 => "None",
        1 => "Warrior",
        2 => "Paladin",
        3 => "Ranger",
        4 => "Thief",
        5 => "Monk",
        6 => "White Mage",
        7 => "Grey Mage",
        8 => "Black Mage",
        9 => "Animal",
        10 => "Monster",
        _ => $"?({cls})",
    };

    /// <summary>Selectable classes for the trainer UI.</summary>
    public static readonly string[] Selectable =
        { "None", "Warrior", "Paladin", "Ranger", "Thief", "Monk",
          "White Mage", "Grey Mage", "Black Mage" };
}
