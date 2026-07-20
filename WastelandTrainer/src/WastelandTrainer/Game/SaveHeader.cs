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

    /// <summary>Active party's world X (map column). Setting it also re-centres the viewport.</summary>
    public int PartyX
    {
        get => U8(GroupBase + SaveFormat.GroupX);
        set => SetPosition(value, PartyY, MapId);
    }

    /// <summary>Active party's world Y (map row). Setting it also re-centres the viewport.</summary>
    public int PartyY
    {
        get => U8(GroupBase + SaveFormat.GroupY);
        set => SetPosition(PartyX, value, MapId);
    }

    /// <summary>Active party's map id. Setting it keeps the global current-map byte in step.</summary>
    public int MapId
    {
        get => U8(GroupBase + SaveFormat.GroupMap);
        set => SetPosition(PartyX, PartyY, value);
    }

    /// <summary>
    /// Places the active party at (<paramref name="x"/>, <paramref name="y"/>) on map
    /// <paramref name="mapId"/>, writing every field the game reads on load so the move is consistent:
    /// the group X/Y/map and its "previous" shadow, the global current-map byte, and the viewport origin
    /// (party position minus the fixed draw offset), coordinates clamped to the 0..63 map grid.
    /// </summary>
    public void SetPosition(int x, int y, int mapId)
    {
        x = Math.Clamp(x, 0, SaveFormat.MapCoordinateCeiling - 1);
        y = Math.Clamp(y, 0, SaveFormat.MapCoordinateCeiling - 1);
        mapId = Math.Clamp(mapId, 0, 255);

        int g = GroupBase;
        U8(g + SaveFormat.GroupX, x);
        U8(g + SaveFormat.GroupY, y);
        U8(g + SaveFormat.GroupMap, mapId);
        // Mirror the "previous" shadow so an in-progress transition can't snap the party back.
        U8(g + SaveFormat.GroupPrevX, x);
        U8(g + SaveFormat.GroupPrevY, y);
        U8(g + SaveFormat.GroupPrevMap, mapId);

        U8(SaveFormat.CurrentMap, mapId);
        // The viewport origin is a signed byte (party position minus the draw offset), so near the
        // top-left edge it is legitimately negative — write it raw rather than through U8, whose
        // 0..255 clamp would snap a negative origin to 0 and mis-centre the party.
        Payload[SaveFormat.ViewportX] = (byte)(sbyte)Math.Clamp(x - SaveFormat.ViewportOffsetX, sbyte.MinValue, sbyte.MaxValue);
        Payload[SaveFormat.ViewportY] = (byte)(sbyte)Math.Clamp(y - SaveFormat.ViewportOffsetY, sbyte.MinValue, sbyte.MaxValue);
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
