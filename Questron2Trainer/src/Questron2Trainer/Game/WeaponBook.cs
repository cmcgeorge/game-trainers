namespace Questron2Trainer.Game;

/// <summary>Information about a single Questron II weapon.</summary>
public sealed record WeaponInfo(int Id, string Name);

/// <summary>
/// The ten weapons of Questron II, extracted from START.EXE strings.
/// Order matches the weapon table in the EXE; the equipped-weapon byte at +0x10 indexes this table.
/// </summary>
public static class WeaponBook
{
    public static readonly WeaponInfo[] Weapons =
    {
        new(0, "Dagger"),
        new(1, "Hammer"),
        new(2, "Hatchet"),
        new(3, "Cudgel"),
        new(4, "Rapier"),
        new(5, "Fauchard"),
        new(6, "Weighted Spear"),
        new(7, "Shortbow"),
        new(8, "Broadsword"),
        new(9, "Crossbow"),
    };

    public static int Count => Weapons.Length;

    public static string Name(int id) =>
        id >= 0 && id < Weapons.Length ? Weapons[id].Name : $"?({id})";
}
