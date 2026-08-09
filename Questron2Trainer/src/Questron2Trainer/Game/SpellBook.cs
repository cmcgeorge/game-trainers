namespace Questron2Trainer.Game;

/// <summary>Information about a single Questron II spell.</summary>
public sealed record SpellInfo(int Id, string Name, string Description, bool Buyable);

/// <summary>
/// The five spells of Questron II, extracted from START.EXE strings and the game manual.
/// The first four are buyable in towns; Destruct is found in the EXE strings but not in the manual.
/// </summary>
public static class SpellBook
{
    public static readonly SpellInfo[] Spells =
    {
        new(0, "Magic Missile", "Single-target damage spell. Buyable in towns.", true),
        new(1, "Fireball", "More powerful single-target damage spell. Buyable in towns.", true),
        new(2, "Sonic Whine", "Attacks all adjacent enemies. Buyable in towns.", true),
        new(3, "Time Sap", "Slows enemies' sense of time to freeze them. Buyable in towns.", true),
        new(4, "Destruct", "Powerful spell found in EXE strings. Not listed in the manual.", false),
    };

    public static int Count => Spells.Length;
    public static int BuyableCount => 4;
}
