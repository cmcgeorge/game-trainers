namespace MightAndMagic1Trainer.Game;

/// <summary>
/// Builds and parses party snapshot files. A snapshot is deliberately a plain roster-format
/// file (18 packed 127-byte records, empty slots zero-filled) — the same layout as
/// <c>ROSTER.DTA</c> — so a snapshot can also be opened with the offline "Browse for
/// Roster.dta" button, copied over a real save, or diffed with other tools.
/// </summary>
public static class PartySnapshot
{
    /// <summary>Serialises the given records into a roster-format buffer, each at its slot offset.
    /// Records with an out-of-range slot are skipped (they can't be placed in a roster file).</summary>
    public static byte[] Build(IEnumerable<CharacterRecord> records)
    {
        var buf = new byte[RosterFormat.MaxSlots * RosterFormat.FileStride];
        foreach (var rec in records)
        {
            if (rec.Slot < 0 || rec.Slot >= RosterFormat.MaxSlots) continue;
            Array.Copy(rec.Raw, 0, buf, rec.Slot * RosterFormat.FileStride, RosterFormat.RecordSize);
        }
        return buf;
    }

    /// <summary>
    /// Parses a snapshot (or any roster-format file) into per-slot record bytes. Slots whose
    /// bytes don't look like a character record (empty or corrupt) are omitted.
    /// </summary>
    public static List<(int Slot, byte[] Record)> Read(byte[] file)
    {
        var result = new List<(int, byte[])>();
        for (int slot = 0; slot < RosterFormat.MaxSlots; slot++)
        {
            int off = slot * RosterFormat.FileStride;
            if (off + RosterFormat.RecordSize > file.Length) break;
            if (!RosterFormat.LooksLikeRecord(file, off)) continue;
            result.Add((slot, file[off..(off + RosterFormat.RecordSize)]));
        }
        return result;
    }
}
