namespace SwordOfAragonTrainer.Game;

/// <summary>
/// The terrain vocabulary of Aragon. <c>ARAGON.EXE</c> carries a 32-entry world-terrain name table as
/// four lines of eight, and those names are exactly the filenames in the <c>terrain\</c> directory —
/// so a world hex's terrain code selects the tactical map that a battle fought there loads.
/// <c>HEXWAR.EXE</c> carries the separate per-hex vocabulary that its Hex command prints.
/// </summary>
public static class TerrainBook
{
    /// <summary>World-terrain codes 0–31, in the order the table lists them.</summary>
    public static readonly IReadOnlyList<string> WorldTerrain = new[]
    {
        "Plain", "Rough", "Hill", "Mountain", "Plateau", "Brush", "CoastN", "Plain",
        "Brush", "BrshFrst", "Forest", "HillBrsh", "HillFrst", "Plain", "Plain", "Plain",
        "StreamNS", "StreamNS", "StrmFrst", "HillStrm", "BrookNS", "BrookEW", "Plain", "Plain",
        "PathNS", "PathEW", "PathStrm", "PathFrst", "HillPath", "Plain", "Plain", "Water",
    };

    /// <summary>Named tactical battlefields shipped alongside the generic templates.</summary>
    public static readonly IReadOnlyList<string> NamedBattlefields = new[]
    {
        "ALADDA", "BROCADA", "CHAR", "DERSH", "DERSH1", "ESTALLAH", "GERNOK", "LUCEDIA", "MARINIA",
        "MEDEVAL", "NURALIA", "PARITAN", "PUD1", "PUDAWALA", "SOTHOLD", "SURNOVA", "TENTULA",
        "TETRADA", "TETRADA2", "TRANAVAN", "XAFANTA", "ZARNIX", "ZARNIX1",
    };

    /// <summary>The words the tactical Hex command uses to describe a square.</summary>
    public static readonly IReadOnlyList<string> HexFeatures = new[]
    {
        "Water", "Plain", "Rough", "Hill", "Brush", "Forest", "Sand", "Town", "Fort", "City",
        "Trail", "Path", "Road", "Entrnch", "Sh.Wall", "Wall", "Block", "Current", "Stream",
        "Brook", "River",
    };

    /// <summary>The name for a world-terrain code, or a bare number if out of range.</summary>
    public static string World(int code) =>
        code >= 0 && code < WorldTerrain.Count ? WorldTerrain[code] : $"code {code}";
}
