namespace PoolOfRadianceTrainer.Game;

/// <summary>
/// Heuristic that recognises a Pool of Radiance carried-item instance (a 63-byte
/// <see cref="ItemEntry"/> record) in a byte buffer. Item instances live in the running game as a
/// linked list in the space between consecutive party records — with no fixed stride — so, like the
/// character scanner, they are located by <i>shape</i> rather than a known address. Kept strict
/// (Pascal name + tight flag domains) so a signature-scan of that inter-record space does not
/// misfire on the neighbouring combat-icon bitmap runs.
/// </summary>
public static class ItemSignature
{
    /// <summary>Does the 63-byte span at <paramref name="i"/> look like a valid item record?</summary>
    public static bool Looks(byte[] buf, int i)
    {
        if (i < 0 || i + ItemEntry.RecordSize > buf.Length) return false;

        // Name: a Pascal string (length byte + up to 41 chars) — the game's cached render, which can
        // embed NULs. Length 1..41; every name byte must be a printable ASCII char or NUL, and the
        // named portion must contain at least one letter.
        int len = buf[i];
        if (len < 1 || len > ItemEntry.NameMax - 1) return false;

        bool sawLetter = false;
        for (int n = 1; n < ItemEntry.NameMax; n++)
        {
            byte b = buf[i + n];
            if (b != 0 && (b < 0x20 || b > 0x7E)) return false;
            if (n <= len && ((b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z'))) sawLetter = true;
        }
        if (!sawLetter) return false;

        // A real item has a non-zero type and boolean readied/cursed flags. The count byte is NOT
        // checked: non-stackable single items (a sling, a worn shield) legitimately store 0 there.
        if (buf[i + ItemEntry.OffType] == 0) return false;
        if (buf[i + ItemEntry.OffReadied] > 1) return false;
        if (buf[i + ItemEntry.OffCursed] > 1) return false;

        return true;
    }
}
