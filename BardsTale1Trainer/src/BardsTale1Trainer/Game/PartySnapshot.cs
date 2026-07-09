namespace BardsTale1Trainer.Game;

/// <summary>
/// Builds and parses party snapshot files. A snapshot is deliberately seven .TPW-format
/// character blocks packed back-to-back in slot order (empty slots zero-filled) — the same
/// 109-byte layout the game saves a character to disk in — so each block carries the name
/// (which the live record itself does not) and the first block of a snapshot is even
/// loadable with the offline "Load .TPW" button.
/// </summary>
public static class PartySnapshot
{
    /// <summary>Total size of a snapshot file: one .TPW block per party slot.</summary>
    public const int FileSize = PartyFormat.PartySlots * PartyFormat.TpwFileSize;

    /// <summary>Serialises the given records into a snapshot buffer, each at its slot offset.
    /// Records with an out-of-range slot are skipped (they can't be placed in the file).</summary>
    public static byte[] Build(IEnumerable<CharacterRecord> records)
    {
        var buf = new byte[FileSize];
        foreach (var rec in records)
        {
            if (rec.Slot < 0 || rec.Slot >= PartyFormat.PartySlots) continue;
            rec.ToTpw().CopyTo(buf, rec.Slot * PartyFormat.TpwFileSize);
        }
        return buf;
    }

    /// <summary>
    /// Parses a snapshot back into per-slot characters. Slots whose block doesn't hold an
    /// occupied character (zero-filled or vacated) are omitted. The returned record bytes
    /// still carry the .TPW disk marker; the caller decides whether to clear it (live
    /// restore) or keep it (file restore).
    /// </summary>
    public static List<(int Slot, string Name, byte[] Record)> Read(byte[] file)
    {
        var result = new List<(int, string, byte[])>();
        for (int slot = 0; slot < PartyFormat.PartySlots; slot++)
        {
            int off = slot * PartyFormat.TpwFileSize;
            if (off + PartyFormat.TpwFileSize > file.Length) break;
            var rec = CharacterRecord.FromTpw(file.AsSpan(off, PartyFormat.TpwFileSize));
            if (rec == null || !rec.IsOccupied) continue;
            result.Add((slot, rec.Name, rec.ToArray()));
        }
        return result;
    }
}
