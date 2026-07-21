using System.Text;

namespace ColonizationTrainer.Game;

/// <summary>
/// Colonization stores names as plain, NUL-terminated ASCII inside fixed-width fields (leader and
/// country names, colony names). This is the codec for those fields — read stops at the first
/// <c>0x00</c>; write truncates to the field width and zero-fills the remainder so no stale bytes
/// leak past the terminator.
/// </summary>
public static class ColonyText
{
    /// <summary>Reads an ASCIIZ string from <paramref name="data"/> at <paramref name="offset"/>, up to <paramref name="maxLength"/> bytes.</summary>
    public static string ReadName(byte[] data, int offset, int maxLength)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || maxLength < 0 || offset > data.Length - maxLength)
            throw new ArgumentOutOfRangeException(nameof(offset), "name field lies outside the buffer.");

        int end = offset;
        int limit = offset + maxLength;
        while (end < limit && data[end] != 0) end++;
        return Encoding.ASCII.GetString(data, offset, end - offset);
    }

    /// <summary>
    /// Writes <paramref name="value"/> into the fixed <paramref name="maxLength"/>-byte field at
    /// <paramref name="offset"/> as NUL-terminated ASCII, zero-filling the rest of the field. The
    /// text is truncated to <paramref name="maxLength"/> − 1 so a terminator always fits.
    /// </summary>
    public static void WriteName(byte[] data, int offset, int maxLength, string value)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (offset < 0 || maxLength <= 0 || offset > data.Length - maxLength)
            throw new ArgumentOutOfRangeException(nameof(offset), "name field lies outside the buffer.");

        value ??= string.Empty;
        var ascii = Encoding.ASCII.GetBytes(value);
        int copy = Math.Min(ascii.Length, maxLength - 1);   // always leave room for the NUL
        for (int i = 0; i < maxLength; i++)
            data[offset + i] = i < copy ? ascii[i] : (byte)0;
    }
}
