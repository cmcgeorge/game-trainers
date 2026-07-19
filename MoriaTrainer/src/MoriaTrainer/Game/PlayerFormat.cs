namespace MoriaTrainer.Game;

/// <summary>
/// The reverse-engineered in-process layout of UMoria 5.5.2's <c>player_type</c> struct and the
/// key globals around it. Offsets are <b>image-relative</b> (from the COFF image base), not absolute
/// live addresses — the DPMI heap places the image at a session-specific linear address, so the
/// trainer locates the mutable fields by value scanning rather than by adding to a fixed base.
///
/// Every field here is documented in <c>.docs/ReverseEngineering.md</c> with its confidence tag.
/// The numeric constants below are the <b>Confirmed</b> subset — the front of <see cref="Misc"/>
/// (HP/mana/level/exp/gold) and the stat encoding are stable across DJGPP builds; the tail
/// (<c>history</c>/<c>name</c>) is Inferred and intentionally not exposed for editing.
/// </summary>
public static class PlayerFormat
{
    /// <summary>UMoria 5.5.2 — the version this trainer targets.</summary>
    public const string GameVersion = "5.5.2";

    /// <summary>Maximum character level (Confirmed from <c>constant.h</c>).</summary>
    public const int MaxLevel = 40;

    /// <summary>Number of creatures in <c>c_list</c> / <c>c_recall</c> (Confirmed).</summary>
    public const int MaxCreatures = 279;

    /// <summary>Number of base items in <c>object_list</c> (Confirmed).</summary>
    public const int MaxObjects = 420;

    /// <summary>Dungeon grid rows (<c>MAX_M</c>, Confirmed).</summary>
    public const int CaveRows = 66;

    /// <summary>Dungeon grid columns (<c>MAX_N</c>, Confirmed).</summary>
    public const int CaveCols = 198;

    /// <summary>Number of backpack slots (Confirmed).</summary>
    public const int InvenPack = 22;

    /// <summary>Total <c>inventory[]</c> array size: pack + equipment (Confirmed).</summary>
    public const int InvenArraySize = 34;

    // --- inventory slot indices (Confirmed from constant.h) -----------------
    public const int InvenWield = 22;   // primary weapon
    public const int InvenHead  = 23;   // helmet
    public const int InvenNeck  = 24;   // amulet
    public const int InvenBody  = 25;   // body armor
    public const int InvenArm   = 26;   // shield
    public const int InvenHands = 27;   // gauntlets/gloves
    public const int InvenHand  = 28;   // ring (left hand)
    public const int InvenAux   = 29;   // ring (right hand)
    public const int InvenFeet  = 30;   // boots

    // --- stat encoding (Confirmed) ------------------------------------------
    /// <summary>Stats 3..18 are stored as-is; 18/01..18/100 use 18 in the low byte and /xx in the next.</summary>
    public const int StatBase   = 18;
    public const int StatMaxSub = 100;

    /// <summary>Decodes a 2-byte stat pair (low byte + /xx byte) into the 3..118 range (18/100 = 118).</summary>
    public static int DecodeStat(int lowByte, int subByte) =>
        lowByte < StatBase ? lowByte : StatBase + subByte;

    /// <summary>Encodes a stat in the 3..118 range back to a (low, sub) byte pair.</summary>
    public static (int Low, int Sub) EncodeStat(int value)
    {
        if (value < StatBase) return (value, 0);
        int sub = Math.Clamp(value - StatBase, 0, StatMaxSub);
        return (StatBase, sub);
    }

    /// <summary>Renders a stat value in the 3..118 range as the in-game "18/40" form (18 alone is "18", not "18/00").</summary>
    public static string FormatStat(int value) =>
        value <= StatBase ? value.ToString() : $"18/{value - StatBase:00}";

    // --- misc sub-struct offsets (image-relative, Confirmed for the front) ---
    // These are documented for reference; the value scanner does not add them to a base, it
    // searches for the field by value. Kept here so a future GameLocator can use them directly.
    public const int MiscMaxHpOff    = 0;   // int32
    public const int MiscChpOff      = 4;   // int32  (current HP)
    public const int MiscMhpOff      = 12;  // int32  (max mana)
    public const int MiscCmanaOff    = 16;  // int32  (current mana)
    public const int MiscLevOff      = 30;  // int16  (character level)
    public const int MiscExpOff      = 32;  // int32  (current experience)
    public const int MiscMaxExpOff   = 36;  // int32
    public const int MiscMaleOff     = 42;  // int16  (1 = male, 0 = female)
    public const int MiscAuOff       = 44;  // int32  (gold)
    public const int MiscFoodOff     = 84;  // int16  (food counter; 0 = starving)

    // --- stat sub-struct offsets (image-relative, after the misc sub-struct) -
    public const int StatSubOff = 340;      // misc sub-struct size; see ReverseEngineering.md §3.2
    public const int StatStrOff  = StatSubOff + 2;   // int16 (current STR)
    public const int StatIntOff  = StatSubOff + 6;   // int16
    public const int StatWisOff  = StatSubOff + 10;  // int16
    public const int StatDexOff  = StatSubOff + 14;  // int16
    public const int StatConOff  = StatSubOff + 18;  // int16
    public const int StatChrOff  = StatSubOff + 22;  // int16

    // --- cave grid ----------------------------------------------------------
    /// <summary>Bytes per <c>cave_type</c> cell (Confirmed: 4 bytes: fval/lr/fm/packed).</summary>
    public const int CaveCellSize = 4;

    /// <summary>Total byte size of the <c>cave[66][198]</c> grid.</summary>
    public const int CaveBytes = CaveRows * CaveCols * CaveCellSize;

    // --- cave_type sub-field byte offsets within a 4-byte cell (Confirmed) ---
    /// <summary><c>fval</c>: terrain type (0 = unknown, 1..14 = valid, see Fval* constants below).</summary>
    public const int CellFvalOff = 0;

    /// <summary><c>lr</c>: light-radius flag — 1 if the player's current light radius reaches this cell.</summary>
    public const int CellLrOff   = 1;

    /// <summary>
    /// <c>fm</c>: field-mark — 1 if the player has already seen this cell (controls map visibility on
    /// the 'm' command). Setting this to 1 on all cells reveals the full dungeon level.
    /// </summary>
    public const int CellFmOff   = 2;

    /// <summary><c>tl</c>: temporary-light flag from a torch/spell.</summary>
    public const int CellTlOff   = 3;

    // --- cave cell fval values (Confirmed from constant.h) ------------------
    public const byte FvalQuartzVein    = 1;
    public const byte FvalMagmaVein     = 2;
    public const byte FvalGraniteWall   = 3;
    public const byte FvalPermWall      = 4;
    public const byte FvalFloor         = 5;
    public const byte FvalCorrFloor     = 6;
    public const byte FvalWaterFloor    = 7;
    public const byte FvalDoorFloor     = 8;
    public const byte FvalObjectFloor   = 9;
    public const byte FvalStairFloor    = 10;
    public const byte FvalBlockedFloor  = 11;
    public const byte FvalVaultFloor    = 13;
    public const byte FvalRubble        = 14;
}
