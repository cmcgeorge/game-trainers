namespace WastelandTrainer.Game;

/// <summary>
/// Structural plausibility test for the 256-byte party-state header that sits immediately before the
/// roster (at <c>rosterBase − <see cref="CharacterFormat.PartyHeaderSize"/></c>). The <b>live</b> party's
/// roster is always preceded by this header — a valid marching order, an in-range X/Y, and a printable
/// map name. Wasteland, however, keeps a second image of some character records elsewhere in memory
/// (confirmed ~18 KB before the live roster in every captured dump): a deleted-but-not-cleared copy of
/// old rangers that is <b>not</b> preceded by a header. That stale copy can hold more members than a
/// freshly-created live party, so a member-count-only tiebreak latches onto it; requiring a plausible
/// header is what tells the live roster apart from the stale copy.
///
/// Confirmed against live DOSBox-X memory: the live header reads X 55 / Y 62 / "Ranger Ctr." inside the
/// Ranger Center (and X 54 / Y 60 / "   Animal   " one screen into the desert), while the stale copy's
/// would-be header is unrelated record bytes (X 179, a non-printable name). See
/// <c>.docs\Wasteland-Reverse-Engineering.md §2</c>.
/// </summary>
public static class PartyHeader
{
    /// <summary>Wasteland maps are at most 64×64, so a live party coordinate is always 0..63.</summary>
    public const int MapCoordinateCeiling = 64;

    /// <summary>
    /// True when the <see cref="CharacterFormat.PartyHeaderSize"/>-byte window at <paramref name="offset"/>
    /// looks like a real party-state header: party X and Y both below <see cref="MapCoordinateCeiling"/>,
    /// the marching-order bytes (0x00..<see cref="CharacterFormat.HeaderPartyX"/>) all valid slot indices
    /// (0..<see cref="CharacterFormat.MaxSlots"/>−1 or a 0 pad), and a non-empty, all-printable map name.
    /// </summary>
    public static bool IsPlausible(byte[] buffer, int offset = 0)
    {
        // Need the whole header — through the 12-byte map name — inside the buffer.
        if (offset < 0 || buffer.Length - offset < CharacterFormat.HeaderMapName + CharacterFormat.MapNameLength)
            return false;

        int x = buffer[offset + CharacterFormat.HeaderPartyX];
        int y = buffer[offset + CharacterFormat.HeaderPartyY];
        if (x >= MapCoordinateCeiling || y >= MapCoordinateCeiling) return false;

        // Marching order fills the bytes before X (0x00..0x07). Each entry is a slot index or a 0 pad,
        // so all must be < MaxSlots — record bytes that happen to sit before a stale copy fail here.
        for (int k = 0; k < CharacterFormat.HeaderPartyX; k++)
            if (buffer[offset + k] >= CharacterFormat.MaxSlots) return false;

        // Map name: a NUL-padded ASCII string. Every byte up to the terminator must be printable and at
        // least one must be a non-space, so an all-zero field or a record tail (inventory bytes) is rejected.
        bool sawText = false;
        for (int i = 0; i < CharacterFormat.MapNameLength; i++)
        {
            byte b = buffer[offset + CharacterFormat.HeaderMapName + i];
            if (b == 0) break;                        // terminator / padding
            if (b < 0x20 || b > 0x7E) return false;   // non-printable => not a map name
            if (b != 0x20) sawText = true;
        }
        return sawText;
    }
}
