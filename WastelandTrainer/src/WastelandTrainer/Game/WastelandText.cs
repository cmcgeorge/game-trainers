using System.Text;

namespace WastelandTrainer.Game;

/// <summary>
/// Wasteland's in-memory string codec for character names and ranks. Unlike Dragon Wars, the text
/// is plain 7-bit ASCII, NUL-terminated and zero-padded to the field width.
/// </summary>
public static class WastelandText
{
    /// <summary>Decodes up to <paramref name="maxLength"/> ASCII bytes at <paramref name="offset"/>.</summary>
    public static string Decode(byte[] buffer, int offset, int maxLength)
    {
        var sb = new StringBuilder(maxLength);
        for (int i = 0; i < maxLength && offset + i < buffer.Length; i++)
        {
            byte b = buffer[offset + i];
            if (b == 0) break;               // NUL terminator / padding
            sb.Append((char)(b & 0x7F));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Encodes <paramref name="value"/> into <paramref name="maxLength"/> ASCII bytes at
    /// <paramref name="offset"/>, zero-padding the remainder of the field. At most
    /// <c>maxLength - 1</c> characters are stored so the field always keeps a trailing NUL
    /// terminator — the locator and <see cref="CharacterRecord.IsOccupied"/> require a
    /// NUL-terminated name, so writing a full-width name would make the edited slot un-locatable.
    /// </summary>
    public static void Encode(byte[] buffer, int offset, int maxLength, string? value)
    {
        int max = Math.Max(0, maxLength - 1);   // always leave room for the NUL terminator
        string s = value ?? "";
        if (s.Length > max) s = s[..max];
        for (int i = 0; i < maxLength && offset + i < buffer.Length; i++)   // guard the destination like Decode
            buffer[offset + i] = i < s.Length ? (byte)(s[i] & 0x7F) : (byte)0;
    }
}
