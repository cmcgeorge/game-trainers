namespace ColonizationTrainer.Game;

/// <summary>
/// The reverse-engineered byte layout of a Sid Meier's Colonization <c>COLONYxx.SAV</c> file
/// (the 1994 DOS "Col1" format, signature <c>"COLONIZE"</c>, little-endian, no checksum). All
/// offsets and record sizes are constants here so the rest of the game layer never hard-codes a
/// magic number. Verified byte-for-byte against the shipped saves and three community sources —
/// see <c>docs/Colonization-Reverse-Engineering.md</c>.
///
/// The file is <b>not</b> a fixed layout: the header carries record <i>counts</i>, and every section
/// after the header is a run of fixed-size records whose absolute offset is computed from those
/// counts (see <see cref="NationBase"/>). Only the header offsets below are absolute.
/// </summary>
public static class SaveFormat
{
    // --- signature ---------------------------------------------------------------
    /// <summary>The 8-byte ASCII signature at offset 0. A load check, not a checksum.</summary>
    public const string Signature = "COLONIZE";

    /// <summary>
    /// Smallest sane file: the whole pre-colony header (HEAD + PLAYER×4 + OTHER = <see cref="ColonyStart"/>)
    /// plus the four nation records, with zero colonies and units — i.e. <c>NationSectionEnd(0, 0)</c>.
    /// </summary>
    public const int MinFileSize = ColonyStart + NationCount * NationRecordSize;

    // --- header (absolute offsets) -----------------------------------------------
    /// <summary>HEAD + PLAYER×4 + OTHER = 158 + 208 + 24 = 390; colonies begin here.</summary>
    public const int ColonyStart = 0x186;

    public const int Off_MapSizeX = 0x0C;   // u16
    public const int Off_MapSizeY = 0x0E;   // u16
    public const int Off_Year = 0x1A;       // u16
    public const int Off_Season = 0x1C;     // u16 (non-zero = autumn)
    public const int Off_Turn = 0x1E;       // u16
    public const int Off_HumanPlayer = 0x28;// u16 (0 England … 3 Netherlands)
    public const int Off_TribeCount = 0x2A; // u16 (Indian dwellings)
    public const int Off_UnitCount = 0x2C;  // u16
    public const int Off_ColonyCount = 0x2E;// u16
    public const int Off_TradeRouteCount = 0x30; // u16
    public const int Off_ManualSaveFlag = 0x54;  // u8 (1 = manual save, 0 = autosave)
    public const int Off_Difficulty = 0x36; // u8 (0 Discoverer … 4 Viceroy)
    public const int Off_ExpeditionaryForce = 0x6A; // 4 × u16 (the King's REF)
    public const int ExpeditionaryForceCount = 4;

    // --- player block (leader/country names) -------------------------------------
    public const int PlayerBlockStart = 0x9E;
    public const int PlayerRecordSize = 52;
    public const int Player_Name = 0;       // char[24]
    public const int Player_Country = 24;   // char[24]
    public const int PlayerNameMax = 24;

    // --- section geometry --------------------------------------------------------
    public const int HeaderSize = 0x9E;         // 158, before PLAYER×4
    public const int ColonyRecordSize = 0xCA;   // 202
    public const int UnitRecordSize = 0x1C;     // 28
    public const int NationRecordSize = 0x13C;  // 316
    public const int NationCount = 4;

    // --- nation record (offsets within a 316-byte record) ------------------------
    public const int Nat_TaxRate = 0x01;            // u8, percent
    public const int Nat_FoundingFathers = 0x07;    // u32 acquired bitfield (25 named bits)
    public const int Nat_LibertyBellsTotal = 0x0C;  // u16
    public const int Nat_LibertyBellsLastTurn = 0x0E;// u16
    public const int Nat_NextFoundingFather = 0x12; // s16 (-1 = none being elected)
    public const int Nat_FoundingFatherCount = 0x14;// u16
    public const int Nat_VillagesBurned = 0x18;     // u8
    public const int Nat_ArtilleryCount = 0x1E;     // u16
    public const int Nat_BoycottBitmap = 0x20;      // u16 (one bit per good)
    public const int Nat_RoyalMoney = 0x22;         // u32 (King's REF budget, NOT treasury)
    public const int Nat_Gold = 0x2A;               // u32 treasury  ← the headline field
    public const int Nat_Crosses = 0x2E;            // u16

    // --- colony record (offsets within a 202-byte record) ------------------------
    public const int Col_X = 0x00;              // u8
    public const int Col_Y = 0x01;              // u8
    public const int Col_Name = 0x02;           // char[24]
    public const int Col_NameMax = 24;
    public const int Col_Nation = 0x1A;         // u8
    public const int Col_Population = 0x1F;     // u8
    public const int Col_Buildings = 0x84;      // 6 bytes packed
    public const int Col_Hammers = 0x92;        // u16
    public const int Col_BuildingInProduction = 0x94; // u8
    public const int Col_WarehouseLevel = 0x95; // u8
    public const int Col_Stock = 0x9A;          // 16 × s16, the goods stockpile
    public const int Col_RebelDividend = 0xC2;  // u32
    public const int Col_RebelDivisor = 0xC6;   // u32

    /// <summary>The 16 tradeable goods; the stock array holds them in this order.</summary>
    public const int GoodsCount = 16;

    // --- practical caps ----------------------------------------------------------
    /// <summary>
    /// "Max gold" target. The field is a 32-bit int; the community caveat is that driving all four
    /// bytes toward <c>0xFF</c> pushes the on-screen tax display off screen. 999,999,999
    /// (<c>0x3B9AC9FF</c>) keeps a comfortably-rich, positive int32 whose high byte is far from
    /// <c>0xFF</c> — rich, no glitch.
    /// </summary>
    public const long MaxGoldTarget = 999_999_999;

    /// <summary>Hard cap the gold editor clamps to (keeps it a positive 32-bit value).</summary>
    public const long GoldCap = 2_000_000_000;

    /// <summary>Tax is a byte percentage; the game never shows more than two digits.</summary>
    public const int MaxTaxRate = 99;

    /// <summary>
    /// The colony's <c>occupation</c>/<c>profession</c> arrays hold 32 colonists, so population's
    /// meaningful ceiling is 32 — setting it higher describes more colonists than the record can hold.
    /// </summary>
    public const int MaxColonists = 32;

    /// <summary>A generous warehouse fill for the per-colony "Max goods" action.</summary>
    public const short GoodsFill = 300;

    /// <summary>The colony <c>stock</c> field is a signed 16-bit quantity.</summary>
    public const short GoodsMax = short.MaxValue;

    /// <summary>
    /// Absolute offset of nation <paramref name="index"/>'s record, computed from the header
    /// counts. Nation order is England, France, Spain, Netherlands (0..3).
    /// </summary>
    public static int NationBase(int colonyCount, int unitCount, int index)
    {
        int unitStart = ColonyStart + ColonyRecordSize * colonyCount;
        int nationStart = unitStart + UnitRecordSize * unitCount;
        return nationStart + NationRecordSize * index;
    }

    /// <summary>Absolute offset of colony <paramref name="index"/>'s 202-byte record.</summary>
    public static int ColonyBase(int index) => ColonyStart + ColonyRecordSize * index;

    /// <summary>Absolute end of the last nation record — the file must be at least this long.</summary>
    public static int NationSectionEnd(int colonyCount, int unitCount) =>
        NationBase(colonyCount, unitCount, NationCount);
}
