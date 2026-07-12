using System.Text;

namespace MinesOfTitanTrainer.Game;

/// <summary>
/// A typed, mutable view over a <see cref="CharacterFormat.RecordSize"/>-byte Mines of Titan
/// character record. The backing <see cref="Bytes"/> array can come from a save file, a memory
/// dump, or live process memory; edits mutate the buffer in place so the caller can write it back.
/// </summary>
public sealed class CharacterRecord
{
    public byte[] Bytes { get; }

    public CharacterRecord(byte[] buffer, int offset = 0)
    {
        Bytes = new byte[CharacterFormat.RecordSize];
        int n = Math.Min(CharacterFormat.RecordSize, buffer.Length - offset);
        if (n > 0) Array.Copy(buffer, offset, Bytes, 0, n);
    }

    // --- primitive accessors -------------------------------------------------
    private byte U8(int o) => Bytes[o];
    private void U8(int o, int v) => Bytes[o] = (byte)Math.Clamp(v, 0, 255);
    private long U32(int o) => (uint)(Bytes[o] | (Bytes[o + 1] << 8) | (Bytes[o + 2] << 16) | (Bytes[o + 3] << 24));
    private void U32(int o, long v)
    {
        uint u = (uint)Math.Clamp(v, 0, uint.MaxValue);
        Bytes[o] = (byte)(u & 0xFF);
        Bytes[o + 1] = (byte)((u >> 8) & 0xFF);
        Bytes[o + 2] = (byte)((u >> 16) & 0xFF);
        Bytes[o + 3] = (byte)((u >> 24) & 0xFF);
    }

    // --- name (plain ASCII, 0x00-padded) -------------------------------------
    public string Name
    {
        get
        {
            var sb = new StringBuilder(CharacterFormat.NameLength);
            for (int i = 0; i < CharacterFormat.NameLength; i++)
            {
                byte b = Bytes[CharacterFormat.OffName + i];
                if (b == 0) break;
                sb.Append((char)b);
            }
            return sb.ToString().TrimEnd();
        }
        set
        {
            var text = value ?? string.Empty;
            // Latch to padding on the first non-printable (or the end of the string): everything
            // after the terminator must stay 0x00, so the name still validates as occupied. A stray
            // control char therefore truncates the name rather than leaving text after a null.
            bool padding = false;
            for (int i = 0; i < CharacterFormat.NameLength; i++)
            {
                char c = !padding && i < text.Length ? text[i] : '\0';
                if (c is < (char)0x20 or > (char)0x7E) { c = '\0'; padding = true; }
                Bytes[CharacterFormat.OffName + i] = (byte)c;
            }
        }
    }

    // --- identity ------------------------------------------------------------
    public char Sex
    {
        get => (char)U8(CharacterFormat.OffSex);
        set => U8(CharacterFormat.OffSex, char.ToUpperInvariant(value));
    }
    public string SexName => CharacterFormat.SexName(Sex);

    public int Age
    {
        get => U8(CharacterFormat.OffAge);
        set => U8(CharacterFormat.OffAge, Math.Clamp(value, CharacterFormat.MinAge, CharacterFormat.MaxAge));
    }

    // --- attributes ----------------------------------------------------------
    public int GetAttribute(int index) => U8(CharacterFormat.OffAttributes + index);
    public void SetAttribute(int index, int value) =>
        U8(CharacterFormat.OffAttributes + index, Math.Clamp(value, 0, CharacterFormat.MaxAttribute));

    // --- skills --------------------------------------------------------------
    public int GetSkill(int index) => U8(CharacterFormat.OffSkills + index);
    public void SetSkill(int index, int value) =>
        U8(CharacterFormat.OffSkills + index, Math.Clamp(value, 0, CharacterFormat.MaxSkill));

    // --- money ---------------------------------------------------------------
    public long Credits { get => U32(CharacterFormat.OffCredits); set => U32(CharacterFormat.OffCredits, value); }

    /// <summary>
    /// True when this record holds a plausible character rather than a stray byte run.
    /// Delegates to <see cref="IsOccupiedAt"/> over the backing buffer.
    /// </summary>
    public bool IsOccupied => IsOccupiedAt(Bytes, 0);

    /// <summary>
    /// Allocation-free occupancy test over an arbitrary buffer window, used by the memory scanner's
    /// hot loop so probing a candidate offset never copies an 86-byte record. A window qualifies
    /// when it has a name that starts with a printable letter and is ASCII/0-padded, a Male/Female
    /// sex byte, an age in a sane range, six attributes each within the game's 0..15 range with at
    /// least one non-zero, and 27 skill bytes all within 0..15.
    /// </summary>
    public static bool IsOccupiedAt(byte[] buf, int offset)
    {
        if (offset < 0 || offset + CharacterFormat.RecordSize > buf.Length) return false;

        byte first = buf[offset + CharacterFormat.OffName];
        if (!IsNameStart(first)) return false;

        bool padding = false;
        for (int i = 0; i < CharacterFormat.NameLength; i++)
        {
            byte b = buf[offset + CharacterFormat.OffName + i];
            if (b == 0) { padding = true; continue; }
            if (padding) return false;                       // text after the null terminator
            if (b < 0x20 || b > 0x7E) return false;          // non-printable inside the name
        }

        char sex = (char)buf[offset + CharacterFormat.OffSex];
        if (sex is not ('M' or 'F' or 'm' or 'f')) return false;

        int age = buf[offset + CharacterFormat.OffAge];
        if (age < CharacterFormat.MinAge || age > CharacterFormat.MaxAge) return false;

        int sum = 0;
        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        {
            int a = buf[offset + CharacterFormat.OffAttributes + i];
            if (a > CharacterFormat.MaxAttribute) return false;
            sum += a;
        }
        if (sum == 0) return false;

        for (int i = 0; i < CharacterFormat.SkillCount; i++)
            if (buf[offset + CharacterFormat.OffSkills + i] > CharacterFormat.MaxSkill) return false;

        return true;
    }

    private static bool IsNameStart(byte b)
    {
        return (b >= 'A' && b <= 'Z') || (b >= 'a' && b <= 'z');
    }

    public CharacterRecord Clone() => new(Bytes);

    public override string ToString() => $"{Name} ({SexName}, {Age})";
}
