namespace WastelandTrainer.Game;

/// <summary>
/// A typed, mutable view over the party-state fields at the start of a decoded savegame payload
/// (see <see cref="SaveFormat"/>). Every setter writes straight into the backing <see cref="Payload"/>
/// buffer, so an edit is reflected the moment the file is re-encoded and saved.
///
/// <para>The active party is group <see cref="CurrentPartyIndex"/> of the four group headers; its
/// world position drives the map draw. Unlike the <b>live</b> party-state header — a write-only shadow
/// the running game never reads back, which is why in-memory teleport is impossible (see the RE notes
/// §5) — these save-file fields ARE read when the game loads, so writing them here <b>does</b> move the
/// party. That is the whole point of the save-editor teleport.</para>
/// </summary>
public sealed class SaveHeader
{
    /// <summary>The decoded 0x800-byte savegame payload this view reads and writes.</summary>
    public byte[] Payload { get; }

    public SaveHeader(byte[] payload) => Payload = payload;

    // --- primitive accessors -------------------------------------------------
    private int U8(int o) => Payload[o];
    private void U8(int o, int v) => Payload[o] = (byte)Math.Clamp(v, 0, 255);

    private long U32(int o) =>
        Payload[o] | ((long)Payload[o + 1] << 8) | ((long)Payload[o + 2] << 16) | ((long)Payload[o + 3] << 24);
    private void U32(int o, long v)
    {
        uint u = (uint)Math.Clamp(v, 0, uint.MaxValue);
        Payload[o] = (byte)(u & 0xFF);
        Payload[o + 1] = (byte)((u >> 8) & 0xFF);
        Payload[o + 2] = (byte)((u >> 16) & 0xFF);
        Payload[o + 3] = (byte)((u >> 24) & 0xFF);
    }

    // --- party-state fields --------------------------------------------------
    /// <summary>Which of the four group headers is the active party (0 in a normal single-group game).</summary>
    public int CurrentPartyIndex => Math.Clamp(U8(SaveFormat.CurrentPartyIndex), 0, SaveFormat.GroupCount - 1);

    public int CurrentMembers => U8(SaveFormat.CurrentMembers);
    public int TotalMembers => U8(SaveFormat.TotalMembers);

    /// <summary>Map id the whole save is currently on (kept in step with the active group's map).</summary>
    public int CurrentMap
    {
        get => U8(SaveFormat.CurrentMap);
        set => U8(SaveFormat.CurrentMap, value);
    }

    public int Hour { get => U8(SaveFormat.Hour); set => U8(SaveFormat.Hour, Math.Clamp(value, 0, 23)); }
    public int Minute { get => U8(SaveFormat.Minute); set => U8(SaveFormat.Minute, Math.Clamp(value, 0, 59)); }

    public long Serial { get => U32(SaveFormat.Serial); set => U32(SaveFormat.Serial, value); }

    // --- active group position ----------------------------------------------
    private int GroupBase => CurrentPartyIndex * SaveFormat.GroupSize;

    /// <summary>Signed viewport-origin byte (party position minus the fixed draw offset).</summary>
    private int Viewport(int o) => (sbyte)Payload[o];
    private void Viewport(int o, int v) =>
        Payload[o] = (byte)(sbyte)Math.Clamp(v, sbyte.MinValue, sbyte.MaxValue);

    /// <summary>
    /// Party's world X (map column) on the map it is <b>currently</b> on. This is derived from the
    /// viewport origin (<c>viewport + <see cref="SaveFormat.ViewportOffsetX"/></c>), the position the
    /// game re-derives on load, so it is correct inside a location as well as on the overworld — unlike
    /// the group-header X, which only tracks the overworld return position.
    /// </summary>
    public int PartyX
    {
        get => Viewport(SaveFormat.ViewportX) + SaveFormat.ViewportOffsetX;
        set => SetPosition(value, PartyY, MapId);
    }

    /// <summary>Party's world Y (map row) on the current map; see <see cref="PartyX"/>.</summary>
    public int PartyY
    {
        get => Viewport(SaveFormat.ViewportY) + SaveFormat.ViewportOffsetY;
        set => SetPosition(PartyX, value, MapId);
    }

    /// <summary>
    /// The map the party is <b>currently</b> on — the global current-map byte the game reads on load
    /// to choose which map to draw. This is <b>not</b> the group-header map (which holds the overworld
    /// map even while the party stands inside a location): reading the group map here made editing X/Y
    /// re-stamp the current map as the wilderness, dumping the party out of any interior on reload.
    /// </summary>
    public int MapId
    {
        get => U8(SaveFormat.CurrentMap);
        set => SetPosition(PartyX, PartyY, value);
    }

    /// <summary>
    /// Places the party at (<paramref name="x"/>, <paramref name="y"/>) on map <paramref name="mapId"/>,
    /// writing the two fields the game re-derives the on-map position from on load — the global
    /// current-map byte (<see cref="SaveFormat.CurrentMap"/>) and the viewport origin (party minus the
    /// fixed draw offset) — so the move works on the current map, interior or overworld.
    ///
    /// <para>The active group header (its X/Y and map) is kept in step because on the overworld it is
    /// the party position; the group's "previous"/home shadow (prev X/Y/map) is deliberately left
    /// untouched so a location's saved overworld return coordinate is not overwritten by an interior
    /// teleport. Coordinates are clamped to the 0..63 map grid.</para>
    /// </summary>
    public void SetPosition(int x, int y, int mapId)
    {
        x = Math.Clamp(x, 0, SaveFormat.MapCoordinateCeiling - 1);
        y = Math.Clamp(y, 0, SaveFormat.MapCoordinateCeiling - 1);
        mapId = Math.Clamp(mapId, 0, 255);

        // The current map + viewport origin are what the game reads on load to place the party.
        U8(SaveFormat.CurrentMap, mapId);
        Viewport(SaveFormat.ViewportX, x - SaveFormat.ViewportOffsetX);
        Viewport(SaveFormat.ViewportY, y - SaveFormat.ViewportOffsetY);

        // Keep the active group header in step (on the overworld this is the party position); leave the
        // prev/home shadow alone so an interior's overworld return coordinate survives the edit.
        int g = GroupBase;
        U8(g + SaveFormat.GroupX, x);
        U8(g + SaveFormat.GroupY, y);
        U8(g + SaveFormat.GroupMap, mapId);
    }

    /// <summary>Bumps the save serial by <see cref="SaveFormat.SerialBump"/> so the edited file wins the
    /// game's next "load the higher serial" decision. Returns the new serial.</summary>
    public long BumpSerial()
    {
        long next = (Serial + SaveFormat.SerialBump) & 0xFFFFFFFFL;
        Serial = next;
        return next;
    }
}
