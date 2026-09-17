namespace Space1889Trainer.Game;

/// <summary>
/// A typed view over the party-level and world fields of a <see cref="GameState"/>: the bank account, food,
/// the calendar, where the party is, the ether flyer and the story flags.
/// </summary>
public sealed class WorldRecord
{
    private readonly GameState _state;

    public WorldRecord(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _state = state;
    }

    // ---- money, food, time ----------------------------------------------------------------------

    public long PartyAccount => _state.GetUInt32(StateFormat.PartyAccountOffset);

    public bool SetPartyAccount(long pennies) => _state.SetUInt32(StateFormat.PartyAccountOffset, pennies);

    public int Food => _state.GetUInt16(StateFormat.FoodOffset);

    public bool SetFood(int value) => _state.SetUInt16(StateFormat.FoodOffset, Math.Clamp(value, 0, StateFormat.MaxFood));

    public long Day => _state.GetUInt32(StateFormat.DayOffset);

    public bool SetDay(long value) => _state.SetUInt32(StateFormat.DayOffset, value);

    // ---- location ------------------------------------------------------------------------------

    public int Planet => (sbyte)_state.GetByte(StateFormat.PlanetOffset);

    public int Area => (sbyte)_state.GetByte(StateFormat.AreaOffset);

    public int Map => (sbyte)_state.GetByte(StateFormat.MapOffset);

    public int ReturnPlanet => (sbyte)_state.GetByte(StateFormat.ReturnPlanetOffset);

    public string AreaNameText => _state.GetString(StateFormat.AreaNameOffset, 15).Split('\n')[0];

    public string PlanetNameText => _state.GetString(StateFormat.PlanetNameOffset, 15).Split('\n')[0];

    /// <summary>The key the map files use for the current map (pubs and inns share pseudo-planet 9).</summary>
    public int MapId => Planet is >= 1 and <= 6 && Area > 0 && Map is 2 or 9 ? 900 + 90 + Map : PlaceBook.MapId(Planet, Area, Map);

    public string Describe => PlaceBook.Describe(Planet, Area, Map);

    /// <summary>Party row on the current map, in squares (the block stores it doubled).</summary>
    public int Row => _state.GetInt16(StateFormat.RowOffset) >> 1;

    /// <summary>Party column on the current map, in squares.</summary>
    public int Column => _state.GetInt16(StateFormat.ColumnOffset) >> 1;

    public bool IsLit => _state.GetUInt16(StateFormat.LitOffset) != 0;

    /// <summary>
    /// Moves the party to (<paramref name="row"/>, <paramref name="column"/>) on the map it is already on. The
    /// previous position is set to the same square, because M.EXE copies it into the map's return slot when you
    /// leave, and a stale one would put you back where you were before the jump. [Confirmed: the view followed]
    /// </summary>
    public bool Teleport(int row, int column)
    {
        if (row < 0 || column < 0 || row > 0x3FFF || column > 0x3FFF) return false;
        var bytes = new[] { (byte)(row * 2), (byte)((row * 2) >> 8), (byte)(column * 2), (byte)((column * 2) >> 8) };
        return _state.SetBytes(StateFormat.RowOffset, bytes) &&
               _state.SetBytes(StateFormat.PreviousRowOffset, bytes);
    }

    // ---- party order ---------------------------------------------------------------------------

    /// <summary>The character index in marching position <paramref name="position"/> (0 = leader).</summary>
    public int MarchingOrder(int position) => (sbyte)_state.GetByte(StateFormat.MarchingOrderOffset + position);

    // ---- ether flyer ---------------------------------------------------------------------------

    public bool HasFlyer => _state.GetByte(StateFormat.HasFlyerOffset) != 0;

    public bool AmmoniaLoaded => _state.GetByte(StateFormat.AmmoniaLoadedOffset) != 0;

    public bool SetAmmoniaLoaded(bool value) => _state.SetByte(StateFormat.AmmoniaLoadedOffset, value ? 1 : 0);

    public bool GlowCrystalsLoaded => _state.GetByte(StateFormat.GlowCrystalsLoadedOffset) != 0;

    public bool SetGlowCrystalsLoaded(bool value) => _state.SetByte(StateFormat.GlowCrystalsLoadedOffset, value ? 1 : 0);

    public int GetFlyer(int field) => _state.GetByte(StateFormat.FlyerOffset + field);

    public bool SetFlyer(int field, int value)
    {
        if ((uint)field >= StateFormat.FlyerRecordSize) throw new ArgumentOutOfRangeException(nameof(field));
        return _state.SetByte(StateFormat.FlyerOffset + field, value);
    }

    public int HullHits => _state.GetUInt16(StateFormat.FlyerHullHitsOffset);

    public int DamageBits => _state.GetUInt16(StateFormat.FlyerDamageBitsOffset);

    public int WDamage => _state.GetUInt16(StateFormat.FlyerWDamageOffset);

    /// <summary>Clears hull hits and component damage, as the ether port's paid repair does (M.EXE 1C60:5ADC).</summary>
    public bool RepairFlyer() =>
        _state.SetUInt16(StateFormat.FlyerDamageBitsOffset, 0) &
        _state.SetUInt16(StateFormat.FlyerHullHitsOffset, 0) &
        _state.SetUInt16(StateFormat.FlyerWDamageOffset, 0);

    /// <summary>The character manning a bridge station, or −1.</summary>
    public int BridgeStation(int station) => (sbyte)_state.GetByte(StateFormat.BridgeCrewOffset + station);

    // ---- story flags ---------------------------------------------------------------------------

    public int StoryFlags => _state.GetUInt16(StateFormat.StoryFlagsOffset);

    public bool HasStoryFlag(int bit) => (StoryFlags & (1 << bit)) != 0;

    public bool SetStoryFlag(int bit, bool value)
    {
        if ((uint)bit > 15) throw new ArgumentOutOfRangeException(nameof(bit));
        int flags = value ? StoryFlags | (1 << bit) : StoryFlags & ~(1 << bit);
        return _state.SetUInt16(StateFormat.StoryFlagsOffset, flags);
    }
}
