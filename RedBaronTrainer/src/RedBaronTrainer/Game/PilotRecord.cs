using System.Text;

namespace RedBaronTrainer.Game;

/// <summary>
/// One 90-byte pilot record, as it appears both in <c>ROSTER.DAT</c> (after the 8-byte file header)
/// and in PS.EXE's data group.
///
/// <para><b>How much of this is known.</b> The record starts with an 18-byte NUL-padded name — that
/// much is certain: the shell renders it verbatim on the Pilot Record screen, and writing over those
/// bytes changes what it draws. The remaining 72 bytes are <i>not</i> fully mapped. Score and the
/// aircraft/balloon/Zeppelin victory counts are visibly derived from them, but the Pilot Record
/// screen shows sums rather than single fields, so probing offset-by-offset never resolved to a
/// clean "score lives at +N". Rather than ship confident-looking labels over guesses, this type
/// exposes the name and hands back the rest as bytes.</para>
///
/// <para>Slot occupancy is judged from the name: an unused slot is all zeroes, and every name the
/// shell writes starts with a printable character.</para>
/// </summary>
public sealed class PilotRecord
{
    private readonly byte[] _bytes;

    public PilotRecord(ReadOnlySpan<byte> source)
    {
        if (source.Length < GameFacts.PilotRecordSize)
            throw new ArgumentException($"a pilot record is {GameFacts.PilotRecordSize} bytes", nameof(source));
        _bytes = source[..GameFacts.PilotRecordSize].ToArray();
    }

    /// <summary>A copy of the raw record.</summary>
    public byte[] ToArray() => (byte[])_bytes.Clone();

    /// <summary>The pilot's name, trimmed at the first NUL.</summary>
    public string Name
    {
        get
        {
            int end = 0;
            while (end < GameFacts.PilotNameLength && _bytes[end] != 0) end++;
            return Encoding.ASCII.GetString(_bytes, 0, end).TrimEnd();
        }
    }

    /// <summary>True when the slot holds a pilot rather than being free.</summary>
    public bool IsOccupied => IsOccupiedSlot(_bytes, 0);

    /// <summary>
    /// Overwrites the name in place. The field is padded with NULs to its full width so no tail of a
    /// previous, longer name is left behind — the shell itself does not always do this, which is why
    /// stale fragments show up after short names in a shipped <c>ROSTER.DAT</c>.
    /// </summary>
    public void SetName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var ascii = Encoding.ASCII.GetBytes(name);
        int n = Math.Min(ascii.Length, GameFacts.PilotNameLength - 1);
        Array.Clear(_bytes, 0, GameFacts.PilotNameLength);
        Array.Copy(ascii, _bytes, n);
    }

    /// <summary>Hex + ASCII of the whole record, for the raw view.</summary>
    public string ToHexDump()
    {
        var sb = new StringBuilder();
        for (int row = 0; row < _bytes.Length; row += 16)
        {
            int len = Math.Min(16, _bytes.Length - row);
            sb.Append($"+{row:X2}  ");
            for (int i = 0; i < 16; i++)
                sb.Append(i < len ? $"{_bytes[row + i]:X2} " : "   ");
            sb.Append(' ');
            for (int i = 0; i < len; i++)
            {
                byte b = _bytes[row + i];
                sb.Append(b >= 0x20 && b < 0x7F ? (char)b : '.');
            }
            sb.AppendLine();
        }
        return sb.ToString();
    }

    /// <summary>True when the record at <paramref name="offset"/> holds a pilot.</summary>
    public static bool IsOccupiedSlot(ReadOnlySpan<byte> buffer, int offset)
    {
        if (offset < 0 || offset > buffer.Length - GameFacts.PilotRecordSize) return false;
        byte first = buffer[offset];
        if (first < 0x20 || first >= 0x7F) return false;
        // The name must be printable up to its terminator, and terminated inside the field.
        bool terminated = false;
        for (int i = 0; i < GameFacts.PilotNameLength; i++)
        {
            byte b = buffer[offset + i];
            if (b == 0) { terminated = true; break; }
            if (b < 0x20 || b >= 0x7F) return false;
        }
        return terminated;
    }

    /// <summary>
    /// True when the record at <paramref name="offset"/> is a free slot.
    ///
    /// <para>Only the 18-byte name field has to be clear. Requiring all 90 bytes to be zero would be
    /// wrong: the shell does not scrub what it stops using — a shipped <c>ROSTER.DAT</c> still shows
    /// fragments of longer names behind shorter ones — so a slot a finished career vacated can
    /// perfectly well carry residue, and rejecting it would fail the whole roster over one slot.</para>
    /// </summary>
    public static bool IsEmptySlot(ReadOnlySpan<byte> buffer, int offset)
    {
        if (offset < 0 || offset > buffer.Length - GameFacts.PilotRecordSize) return false;
        for (int i = 0; i < GameFacts.PilotNameLength; i++)
            if (buffer[offset + i] != 0) return false;
        return true;
    }

    /// <summary>
    /// True when <paramref name="buffer"/> looks like the ten-slot roster: every slot is either a
    /// pilot or free, and at least one is a pilot. Slots do <b>not</b> have to pack from zero — the
    /// shell reuses whichever slot a finished career vacated.
    /// </summary>
    public static bool IsPlausibleRoster(ReadOnlySpan<byte> buffer)
    {
        if (buffer.Length < GameFacts.RosterSlots * GameFacts.PilotRecordSize) return false;
        int occupied = 0;
        for (int slot = 0; slot < GameFacts.RosterSlots; slot++)
        {
            int off = slot * GameFacts.PilotRecordSize;
            if (IsOccupiedSlot(buffer, off)) occupied++;
            else if (!IsEmptySlot(buffer, off)) return false;
        }
        return occupied > 0;
    }

    /// <summary>
    /// True when <paramref name="name"/> is safe to write into a slot: 1 to 17 printable ASCII
    /// characters, leaving room for the terminator.
    ///
    /// <para>An empty or non-printable name is not merely ugly — it clears the slot's first byte,
    /// which is exactly what the shell reads as "free". The 72 bytes of career state behind it would
    /// survive but become unreachable, and the next career created would reuse and overwrite the
    /// slot.</para>
    /// </summary>
    public static bool IsWritableName(string? name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        if (name.Length > GameFacts.PilotNameLength - 1) return false;
        foreach (char c in name)
            if (c < 0x20 || c >= 0x7F) return false;
        return true;
    }
}
