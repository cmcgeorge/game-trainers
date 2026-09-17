namespace Space1889Trainer.Files;

/// <summary>What a map square is, for drawing a schematic and for deciding whether a teleport may land there.</summary>
public enum TileClass
{
    /// <summary>Walkable ground, floor or road.</summary>
    Floor,
    /// <summary>Walls, buildings, trees, rock — anything the party cannot walk into.</summary>
    Blocked,
    /// <summary>Deep water (boats, or swimmers with water breathers on some maps).</summary>
    Water,
    /// <summary>A door (DEF row 8).</summary>
    Door,
    /// <summary>EXIT: back to the area's main map or to the planet map (DEF row 7 column 0).</summary>
    Exit,
    /// <summary>A shop or service sign (DEF row 7 columns 1..10).</summary>
    Service,
    /// <summary>An entrance to another map or city (DEF row 7 columns 11..19).</summary>
    Entrance,
    /// <summary>A void cell (code 0x1FF; not used by the shipped maps).</summary>
    Void,
}

/// <summary>
/// Tile semantics recovered from M.EXE: passability (1000:5AF2), trigger tiles (1000:5BDA / 1000:66F9) and doors
/// (1000:1902). Tiles live in 20-per-row sheets: sheet 0 is the shared DEF sheet, sheet 1 the planet's SPR sheet.
/// </summary>
public static class TileRules
{
    /// <summary>Service names for DEF row 7 columns 1..10 (dispatcher 1C60:5F7D).</summary>
    public static readonly IReadOnlyList<string> Services = new[]
    {
        "", "Pawn shop", "Archaeologist", "Pub", "Bank", "Ether port", "Market", "Weapon shop", "Alchemist", "Harbor", "Inn",
    };

    /// <summary>Short labels for the same services.</summary>
    public static readonly IReadOnlyList<string> ServiceAbbreviations = new[]
    {
        "", "PAWN", "ARCH", "PUB", "BANK", "PORT", "MKT", "WPN", "ALCH", "HARB", "INN",
    };

    /// <summary>The deep-water SPR tile (row 2, column 17).</summary>
    public const int DeepWaterTile = 57;

    /// <summary>Maps whose doors need an item (DS:0x023D), as 1-based item ids: (map, item, alternative or 0).</summary>
    public static readonly IReadOnlyList<(int MapId, int ItemId, int AlternativeItemId)> LockedDoorMaps = new[]
    {
        (414, 96, 0), (473, 99, 0), (164, 128, 0), (210, 100, 105), (478, 129, 0), (436, 103, 0),
        (153, 164, 0), (454, 117, 0), (446, 74, 0), (453, 74, 0), (523, 148, 0), (437, 77, 0),
    };

    public static TileClass Classify(MapCell cell)
    {
        if (cell.Sheet == 0x0F) return TileClass.Void;
        int row = cell.SheetRow, col = cell.SheetColumn;
        if (cell.Sheet == 0)
        {
            if (row == 7) return col == 0 ? TileClass.Exit : col <= 10 ? TileClass.Service : TileClass.Entrance;
            if (row == 8 && col <= 11) return TileClass.Door;
        }
        else if (cell.Tile == DeepWaterTile)
        {
            return TileClass.Water;
        }
        return IsWalkable(cell) ? TileClass.Floor : TileClass.Blocked;
    }

    /// <summary>
    /// Walking passability (M.EXE 1000:5AF2, travel mode 1): SPR rows 7..11; DEF rows 7, 10 and 11; DEF row 8
    /// columns 12..15; DEF row 1 columns 6 and 11.
    /// </summary>
    public static bool IsWalkable(MapCell cell)
    {
        int row = cell.SheetRow, col = cell.SheetColumn;
        if (cell.Sheet == 1) return row is >= 7 and <= 11;
        if (cell.Sheet != 0) return false;
        return row is 7 or 10 or 11
            || (row == 8 && col is >= 12 and <= 15)
            || (row == 1 && col is 6 or 11);
    }

    /// <summary>
    /// May a teleport land here? Only plain walkable ground. Trigger tiles are refused because the game acts on
    /// them only when you step onto them (landing on one leaves you standing in a shop sign), and doors because
    /// the door code animates them as you walk through.
    /// </summary>
    public static bool IsSafeLanding(MapCell cell) => Classify(cell) == TileClass.Floor;

    /// <summary>A label for a trigger tile: "EXIT", "BANK", "→ map 3" or "→ city 2"; null for anything else.</summary>
    public static string? TriggerLabel(MapCell cell, bool overlandMap)
    {
        if (cell.Sheet != 0 || cell.SheetRow != 7) return null;
        int col = cell.SheetColumn;
        if (col == 0) return "EXIT";
        if (col <= 10) return ServiceAbbreviations[col];
        return overlandMap ? $"→ area {col - 10}" : $"→ map {col - 10}";
    }
}
