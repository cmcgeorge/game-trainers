namespace PoolOfRadianceTrainer.Game;

/// <summary>
/// Heuristic that recognises a Pool of Radiance character/monster record in a byte buffer,
/// used to locate the party in a running game's memory regardless of where the OS mapped it.
/// Mirrors the invariants confirmed across the sample party and the combat monster records.
/// </summary>
public static class CharacterSignature
{
    /// <summary>
    /// Does the 285-byte span at <paramref name="i"/> look like a valid record?
    /// </summary>
    public static bool Looks(byte[] buf, int i)
    {
        if (i < 0 || i + PorFormat.RecordSize > buf.Length) return false;

        // Name: a Pascal string. Length 1..15; the text is printable ASCII up to the first
        // NUL, and the remainder of the 15-byte field is NUL padding only.
        int len = buf[i + PorFormat.OffNameLength];
        if (len < 1 || len > PorFormat.NameMaxLength) return false;

        byte first = buf[i + PorFormat.OffName];
        if (!((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z'))) return false;

        bool sawNull = false;
        for (int n = 0; n < PorFormat.NameMaxLength; n++)
        {
            byte b = buf[i + PorFormat.OffName + n];
            if (n >= len) { if (b != 0) return false; continue; }   // must be NUL past the length
            if (b == 0) { sawNull = true; continue; }
            if (sawNull) return false;
            bool ok = (b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') || (b >= '0' && b <= '9')
                      || b == ' ' || b == '\'' || b == '-' || b == '.';
            if (!ok) return false;
        }

        // Six ability scores in a plausible range (players 3..18(+); monsters use the same
        // slots and can read lower, e.g. an orc's Intelligence 6). Allow 1..30.
        for (int s = 0; s < PorFormat.StatCount; s++)
        {
            int v = buf[i + PorFormat.OffStr + s];
            if (v < 1 || v > 30) return false;
        }

        // Race 0..7, class 0..17 (see PorFormat enums).
        if (buf[i + PorFormat.OffRace] > 7) return false;
        if (buf[i + PorFormat.OffClass] > 17) return false;

        // A record always has positive max HP and a valid status enum (0..8).
        if (buf[i + PorFormat.OffHpMax] == 0) return false;
        if (buf[i + PorFormat.OffStatus] > 8) return false;

        return true;
    }
}
