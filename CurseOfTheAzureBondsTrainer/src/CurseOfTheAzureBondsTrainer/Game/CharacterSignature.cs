namespace CurseOfTheAzureBondsTrainer.Game;

/// <summary>
/// Heuristic that recognises a Curse of the Azure Bonds character/monster record in a byte buffer,
/// used to locate the party in a running game's memory regardless of where the OS mapped it.
/// Mirrors the invariants confirmed across the sample party and the combat monster records.
/// </summary>
public static class CharacterSignature
{
    /// <summary>
    /// Does the 422-byte span at <paramref name="i"/> look like a valid record?
    /// </summary>
    public static bool Looks(byte[] buf, int i)
    {
        if (i < 0 || i + CoabFormat.RecordSize > buf.Length) return false;

        // Name: a Pascal string. Length 1..15, first character a letter, and the 15-byte field holds
        // printable name characters followed by NUL padding.
        //
        // The padding rule is deliberately "once NUL, always NUL" rather than "everything past the
        // declared length is NUL". Those are not the same, and the difference is not hypothetical:
        // a character entered as "TRAVIS " is stored with length 6 and the trailing space still in
        // the buffer, so the stricter rule rejects a real party member and the scan quietly comes
        // back one character short. What actually rules out a false positive is that the field is
        // name characters up to a NUL and nothing but NULs after it.
        int len = buf[i + CoabFormat.OffNameLength];
        if (len < 1 || len > CoabFormat.NameMaxLength) return false;

        byte first = buf[i + CoabFormat.OffName];
        if (!((first >= 'A' && first <= 'Z') || (first >= 'a' && first <= 'z'))) return false;

        int textEnd = -1;                                           // index of the first NUL, if any
        for (int n = 0; n < CoabFormat.NameMaxLength; n++)
        {
            byte b = buf[i + CoabFormat.OffName + n];
            if (b == 0) { if (textEnd < 0) textEnd = n; continue; }
            if (textEnd >= 0) return false;                         // text after the terminator
            bool ok = (b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z') || (b >= '0' && b <= '9')
                      || b == ' ' || b == '\'' || b == '-' || b == '.';
            if (!ok) return false;
        }
        // The text may run past the declared length (a trimmed trailing space), but never stop short
        // of it — that would be a length byte describing characters that aren't there.
        if (textEnd >= 0 && textEnd < len) return false;

        // Six ability scores in a plausible range (players 3..18(+); monsters use the same
        // slots and can read lower, e.g. an orc's Intelligence 6). Allow 1..30.
        //
        // Curse stores each score as a (current, maximum) pair, and both halves are checked. That
        // makes this a far stronger filter than the single-byte version the sister game needs: a
        // run of arbitrary bytes has to be in range twelve times over, and the maximum must never
        // read below the current, since nothing in the game drains a score upwards.
        for (int s = 0; s < CoabFormat.StatCount; s++)
        {
            int cur = buf[i + CoabFormat.OffStats + s * CoabFormat.StatStride];
            int max = buf[i + CoabFormat.OffStats + s * CoabFormat.StatStride + CoabFormat.StatMaxDelta];
            if (cur < 1 || cur > 30) return false;
            if (max < 1 || max > 30) return false;
            if (max < cur) return false;
        }

        // Race 0..7, class 0..17 (see CoabFormat enums).
        if (buf[i + CoabFormat.OffRace] > 7) return false;
        if (buf[i + CoabFormat.OffClass] > 17) return false;

        // A record always has positive max HP and a valid status enum (0..8).
        if (buf[i + CoabFormat.OffHpMax] == 0) return false;
        if (buf[i + CoabFormat.OffStatus] > 8) return false;

        return true;
    }
}
