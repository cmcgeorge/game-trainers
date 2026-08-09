namespace Questron2Trainer.Game;

/// <summary>Information about a single Questron II armor type.</summary>
public sealed record ArmorInfo(int Id, string Name);

/// <summary>
/// The seven armor types of Questron II, extracted from START.EXE strings.
/// Order matches the armor table in the EXE; the equipped-armor byte at +0x11 indexes this table.
/// </summary>
public static class ArmorBook
{
    public static readonly ArmorInfo[] Armors =
    {
        new(0, "Rawhide"),
        new(1, "Studded Leather"),
        new(2, "Ring Mail"),
        new(3, "Bar Mail"),
        new(4, "Chain Mail"),
        new(5, "Plate Mail"),
        new(6, "Ribbed Plate"),
    };

    public static int Count => Armors.Length;

    public static string Name(int id) =>
        id >= 0 && id < Armors.Length ? Armors[id].Name : $"?({id})";
}
