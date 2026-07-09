using System.Text;

namespace DragonWarsTrainer.Game;

/// <summary>
/// Dragon Wars' in-memory string codec, shared by character names and item names. Every
/// character but the last carries its high bit set (<c>| 0x80</c>); the final character has its
/// high bit clear; a <c>0</c> byte is padding / end-of-string.
/// </summary>
public static class DragonWarsText
{
    /// <summary>Decodes up to <paramref name="maxLength"/> encoded bytes at <paramref name="offset"/>.</summary>
    public static string Decode(byte[] buffer, int offset, int maxLength)
    {
        var sb = new StringBuilder(maxLength);
        for (int i = 0; i < maxLength; i++)
        {
            byte b = buffer[offset + i];
            if (b == 0) break;                  // padding: end of string
            sb.Append((char)(b & 0x7F));
            if ((b & 0x80) == 0) break;         // last character has its high bit clear
        }
        return sb.ToString();
    }

    /// <summary>
    /// Encodes <paramref name="value"/> into <paramref name="maxLength"/> bytes at
    /// <paramref name="offset"/>, high-bit flagging every character but the last and zero-padding
    /// the remainder.
    /// </summary>
    public static void Encode(byte[] buffer, int offset, int maxLength, string? value)
    {
        string s = value ?? "";
        if (s.Length > maxLength) s = s[..maxLength];
        for (int i = 0; i < maxLength; i++)
        {
            if (i >= s.Length) { buffer[offset + i] = 0; continue; }
            byte b = (byte)(s[i] & 0x7F);
            if (i < s.Length - 1) b |= 0x80;    // every character but the last carries its high bit
            buffer[offset + i] = b;
        }
    }
}
