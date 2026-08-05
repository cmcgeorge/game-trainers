using System.Text;
using TheQuestTrainer.Memory;

namespace TheQuestTrainer.Game;

/// <summary>
/// Reader for the 32-bit MSVC <c>std::string</c> the game stores names and resource ids in.
///
/// The layout is a 16-byte union — the characters inline when they fit, otherwise a pointer to a
/// heap buffer — followed by the size and the capacity:
///
/// <code>
/// +0x00  union { char buf[16]; char* ptr; }
/// +0x10  size_t size        // characters, not counting the terminator
/// +0x14  size_t capacity    // 15 while the value is inline, larger once it spills
/// </code>
///
/// This is the trainer's strongest validation signal. A run of integers that happens to look like a
/// level and a gold pile will not also satisfy "capacity is at least 15, size is at most capacity,
/// short strings are NUL-terminated inside the buffer and long ones point at readable characters",
/// which is why <see cref="CharacterLocator"/> leans on it rather than on value ranges alone.
/// </summary>
public static class StdString
{
    /// <summary>Size of the whole object: the buffer union plus the two size fields.</summary>
    public const int Bytes = 24;

    /// <summary>Characters that fit inline before the string spills to the heap.</summary>
    public const int InlineCapacity = 15;

    /// <summary>Longest string this reader will pull out of the target. Names and resource ids are tiny.</summary>
    public const int MaxLength = 256;

    /// <summary>
    /// Reads the string whose object starts at <paramref name="offset"/> inside
    /// <paramref name="record"/>. Returns null when the object is not a well-formed
    /// <c>std::string</c>, or when a spilled buffer cannot be read.
    /// </summary>
    public static string? Read(IMemorySource source, byte[] record, int offset)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(record);
        if (offset < 0 || offset + Bytes > record.Length) return null;

        uint size = BitConverter.ToUInt32(record, offset + 16);
        uint capacity = BitConverter.ToUInt32(record, offset + 20);

        if (capacity < InlineCapacity) return null;      // never shrinks below the inline buffer
        if (size > capacity) return null;
        if (size > MaxLength) return null;

        if (capacity == InlineCapacity)
        {
            // Inline. The characters live in the union and the terminator must be inside it.
            if (size > InlineCapacity) return null;
            if (record[offset + (int)size] != 0) return null;
            return Decode(record.AsSpan(offset, (int)size));
        }

        // Spilled: the union holds a pointer to size+1 readable bytes.
        uint pointer = BitConverter.ToUInt32(record, offset);
        if (pointer == 0) return null;

        var buffer = new byte[size + 1];
        if (source.Read(pointer, buffer, buffer.Length) != buffer.Length) return null;
        if (buffer[size] != 0) return null;
        return Decode(buffer.AsSpan(0, (int)size));
    }

    /// <summary>
    /// Whether the string at <paramref name="offset"/> is well-formed and, if
    /// <paramref name="requireNonEmpty"/>, actually holds text. The prototype record the game keeps
    /// beside the live one has an empty name, so "non-empty" is what separates them.
    ///
    /// "Text" means <i>no control characters</i>, not "ASCII". The Quest is a localised commercial
    /// release and the character name is free text the player types, so <c>Grün</c> and <c>José</c>
    /// are ordinary names; the bytes are decoded as Latin-1 and anything above <c>0x7E</c> is
    /// legitimate. Requiring printable ASCII here would make the trainer refuse to find a perfectly
    /// healthy character. The structural checks in <see cref="Read"/> — capacity at least 15, size
    /// within capacity, a terminator inside the inline buffer or a readable spilled one — are what
    /// actually reject a run of look-alike integers.
    /// </summary>
    public static bool IsPlausible(IMemorySource source, byte[] record, int offset, bool requireNonEmpty)
    {
        string? value = Read(source, record, offset);
        if (value is null) return false;
        if (!requireNonEmpty) return true;
        if (value.Length == 0) return false;

        foreach (char c in value)
            if (IsControl(c)) return false;
        return true;
    }

    /// <summary>C0 controls and DEL. Latin-1's C1 range (0x80..0x9F) is left alone deliberately —
    /// a mis-decoded but harmless byte should not cost the user their character.</summary>
    private static bool IsControl(char c) => c < ' ' || c == (char)0x7F;

    private static string Decode(ReadOnlySpan<byte> bytes) => Encoding.Latin1.GetString(bytes);
}
