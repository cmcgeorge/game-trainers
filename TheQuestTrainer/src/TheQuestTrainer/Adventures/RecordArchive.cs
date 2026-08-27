using System.Buffers.Binary;

namespace TheQuestTrainer.Adventures;

/// <summary>
/// The read side of the engine's own <c>SArchive</c>, which is how every object in a world database
/// is written and read back.
///
/// <b>The one thing that matters here is the alignment.</b> The game's primitives are not a flat
/// byte stream: a 16-bit read first skips forward until the cursor sits on an even offset, and a
/// 32-bit read until it sits on a multiple of four. Bytes and NUL-terminated strings are not aligned
/// at all. Miss that and a record decodes into plausible-looking rubbish a few fields in, because
/// every subsequent field is off by one.
///
/// The game computes the alignment from the record buffer's address (<c>start &amp; 3</c> plus the
/// cursor), which for a heap block is the same as the offset within the record. This reader works in
/// offsets from the record's first byte, which is that same thing without the pointer.
///
/// Reads past the end throw <see cref="ArchiveException"/> rather than returning junk: a record that
/// does not decode is a record this reader has misunderstood, and saying so is more useful than
/// printing garbage into a cluebook.
/// </summary>
public sealed class RecordArchive
{
    private readonly byte[] _data;
    private int _at;

    /// <summary>Wraps a record's bytes.</summary>
    public RecordArchive(ReadOnlySpan<byte> data)
    {
        _data = data.ToArray();
    }

    /// <summary>The cursor, as an offset into the record.</summary>
    public int Position => _at;

    /// <summary>The record's length.</summary>
    public int Length => _data.Length;

    /// <summary>Bytes not yet read.</summary>
    public int Remaining => _data.Length - _at;

    /// <summary>
    /// Whether the whole record has been consumed.
    ///
    /// "Whole" allows for the writer's own slack: a record is rounded up to a multiple of four and
    /// always carries at least one spare word, so every correctly parsed record in the shipped
    /// worlds ends two to five zero bytes from the end. Requiring those bytes to be <i>zero</i> is
    /// what makes this a real check — a schema that missed a field would leave content behind, not
    /// padding.
    /// </summary>
    public bool ConsumedWithinPadding
    {
        get
        {
            if (Remaining is < 0 or > MaxTrailingPadding) return false;
            for (int i = _at; i < _data.Length; i++)
                if (_data[i] != 0) return false;
            return true;
        }
    }

    /// <summary>The most zero bytes a correctly parsed record may end with.</summary>
    public const int MaxTrailingPadding = 7;

    /// <summary>Reads one unaligned byte.</summary>
    public byte ReadByte()
    {
        Need(1);
        return _data[_at++];
    }

    /// <summary>Reads one unaligned byte as a flag, the way the game's own <c>bool</c> fields are stored.</summary>
    public bool ReadBool() => ReadByte() != 0;

    /// <summary>Reads a 16-bit word, skipping to an even offset first.</summary>
    public ushort ReadUInt16()
    {
        Align(2);
        Need(2);
        ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(_at, 2));
        _at += 2;
        return value;
    }

    /// <summary>Reads a 32-bit word, skipping to a multiple of four first.</summary>
    public uint ReadUInt32()
    {
        Align(4);
        Need(4);
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(_at, 4));
        _at += 4;
        return value;
    }

    /// <summary>
    /// Reads a NUL-terminated Latin-1 string. Unaligned, and the terminator is always present — the
    /// game writes one even for an empty string, which is why empty fields cost a byte each and why
    /// most records begin with a run of them.
    /// </summary>
    public string ReadString()
    {
        int end = Array.IndexOf(_data, (byte)0, _at);
        if (end < 0) throw new ArchiveException($"unterminated string at {_at} of {_data.Length}");

        string value = System.Text.Encoding.Latin1.GetString(_data, _at, end - _at);
        _at = end + 1;
        return value;
    }

    /// <summary>Reads a length-prefixed opaque blob: a 16-bit length, then that many raw bytes.</summary>
    public byte[] ReadBlob()
    {
        int length = ReadUInt16();
        Need(length);
        var blob = _data.AsSpan(_at, length).ToArray();
        _at += length;
        return blob;
    }

    /// <summary>
    /// Reads the one-byte class tag every serialized object starts with and refuses anything else.
    /// The game does exactly this, and aborts on a mismatch; this is the check that makes a
    /// tag-driven walk safe.
    /// </summary>
    public void ExpectTag(byte tag, string what)
    {
        byte found = ReadByte();
        if (found != tag)
            throw new ArchiveException($"{what}: expected tag 0x{tag:X2} at {_at - 1}, found 0x{found:X2}");
    }

    /// <summary>Reads a 16-bit count and refuses one that could not fit in the record.</summary>
    public int ReadCount(string what)
    {
        int count = ReadUInt16();
        if (count > Remaining + 1)
            throw new ArchiveException($"{what}: {count} entries cannot fit in {Remaining} remaining bytes");
        return count;
    }

    private void Align(int to)
    {
        while (_at % to != 0)
        {
            Need(1);
            _at++;
        }
    }

    private void Need(int bytes)
    {
        if (_at + bytes > _data.Length)
            throw new ArchiveException($"read of {bytes} bytes at {_at} runs past the record's {_data.Length}");
    }
}

/// <summary>A record did not decode. Carries where and why, so a cluebook can say so.</summary>
public sealed class ArchiveException(string message) : Exception(message);
