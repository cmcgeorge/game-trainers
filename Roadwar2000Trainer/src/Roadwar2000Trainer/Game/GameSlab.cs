namespace Roadwar2000Trainer.Game;

/// <summary>
/// Somewhere a 6,512-byte Roadwar slab lives: the running game's data segment, a
/// <c>.RWS</c> file, or a synthetic buffer in the test harness.
/// </summary>
public interface ISlabTarget
{
    /// <summary>Reads <paramref name="count"/> bytes at <paramref name="slabOffset"/>, or null if it cannot.</summary>
    byte[]? Read(int slabOffset, int count);

    /// <summary>Writes <paramref name="data"/> at <paramref name="slabOffset"/>. Returns false on failure.</summary>
    bool Write(int slabOffset, byte[] data);

    /// <summary>False once the target has gone away (game closed, file deleted).</summary>
    bool IsAvailable { get; }
}

/// <summary>An in-memory slab: what the save editor edits, and what the test harness drives.</summary>
public sealed class BufferTarget : ISlabTarget
{
    private readonly byte[] _bytes;

    public BufferTarget(byte[] bytes)
    {
        if (bytes.Length != SaveFormat.SlabLength)
            throw new ArgumentException(
                $"A Roadwar slab is exactly {SaveFormat.SlabLength} bytes; got {bytes.Length}.", nameof(bytes));
        _bytes = bytes;
    }

    public byte[] Bytes => _bytes;

    public bool IsAvailable => true;

    public byte[]? Read(int slabOffset, int count)
    {
        if (slabOffset < 0 || count < 0 || slabOffset > _bytes.Length - count) return null;
        var slice = new byte[count];
        Array.Copy(_bytes, slabOffset, slice, 0, count);
        return slice;
    }

    public bool Write(int slabOffset, byte[] data)
    {
        if (slabOffset < 0 || slabOffset > _bytes.Length - data.Length) return false;
        Array.Copy(data, 0, _bytes, slabOffset, data.Length);
        return true;
    }
}

/// <summary>
/// A cached, typed view over one Roadwar slab.
/// <para>
/// The whole slab is pulled in one read so a screen paints from a single consistent snapshot,
/// and every setter writes the affected bytes straight back through to the target. That is the
/// read-validate-write shape the rest of the repository uses: an edit that the target rejects
/// leaves the cache untouched, so the UI never shows a value the game does not actually hold.
/// </para>
/// </summary>
public sealed class GameSlab
{
    private byte[] _cache = new byte[SaveFormat.SlabLength];

    public GameSlab(ISlabTarget target) => Target = target;

    public ISlabTarget Target { get; }

    /// <summary>The most recent snapshot. Treat as read-only; edits go through the setters.</summary>
    public byte[] Snapshot => _cache;

    /// <summary>True once <see cref="Refresh"/> has pulled a plausible slab.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>Re-reads the whole slab. Returns false if the target could not supply one.</summary>
    public bool Refresh()
    {
        var data = Target.Read(0, SaveFormat.SlabLength);
        if (data is null || data.Length != SaveFormat.SlabLength) return false;
        _cache = data;
        IsLoaded = true;
        return true;
    }

    // ---- primitive accessors -------------------------------------------------

    public byte GetByte(int offset) => _cache[offset];

    public bool SetByte(int offset, int value)
    {
        byte b = (byte)Math.Clamp(value, 0, 255);
        if (_cache[offset] == b) return true;
        if (!Target.Write(offset, new[] { b })) return false;
        _cache[offset] = b;
        return true;
    }

    public ushort GetUInt16(int offset) => (ushort)(_cache[offset] | (_cache[offset + 1] << 8));

    public bool SetUInt16(int offset, int value)
    {
        int v = Math.Clamp(value, 0, ushort.MaxValue);
        var bytes = new[] { (byte)(v & 0xFF), (byte)((v >> 8) & 0xFF) };
        if (_cache[offset] == bytes[0] && _cache[offset + 1] == bytes[1]) return true;
        if (!Target.Write(offset, bytes)) return false;
        _cache[offset] = bytes[0];
        _cache[offset + 1] = bytes[1];
        return true;
    }

    public byte[] GetBytes(int offset, int count)
    {
        var slice = new byte[count];
        Array.Copy(_cache, offset, slice, 0, count);
        return slice;
    }

    public bool SetBytes(int offset, byte[] data)
    {
        if (offset < 0 || offset > _cache.Length - data.Length) return false;
        if (!Target.Write(offset, data)) return false;
        Array.Copy(data, 0, _cache, offset, data.Length);
        return true;
    }

    /// <summary>Reads a NUL-terminated, fixed-width ASCII field.</summary>
    public string GetString(int offset, int maxLength)
    {
        int n = 0;
        while (n < maxLength && _cache[offset + n] != 0) n++;
        return System.Text.Encoding.ASCII.GetString(_cache, offset, n);
    }

    /// <summary>Writes a NUL-terminated, fixed-width ASCII field, padding the remainder with NULs.</summary>
    public bool SetString(int offset, int maxLength, string value)
    {
        var buf = new byte[maxLength];
        var ascii = System.Text.Encoding.ASCII.GetBytes(value);
        int n = Math.Min(ascii.Length, maxLength - 1);
        Array.Copy(ascii, buf, n);
        return SetBytes(offset, buf);
    }

    // ---- structural check ----------------------------------------------------

    /// <summary>
    /// Does this buffer actually look like a Roadwar slab? Checks the three things that are the
    /// same in every save ever written: the vehicle-name block, the pointer table that indexes
    /// it (whose entries are data-segment addresses, so they carry the base offset with them),
    /// and a vehicle-type table whose first record is the motorcycle.
    /// </summary>
    public static bool LooksLikeSlab(byte[] bytes)
    {
        if (bytes.Length != SaveFormat.SlabLength) return false;

        var motorcycle = System.Text.Encoding.ASCII.GetBytes("MOTORCYCLE\0SIDECAR\0");
        for (int i = 0; i < motorcycle.Length; i++)
            if (bytes[SaveFormat.VehicleNames + i] != motorcycle[i]) return false;

        // The name pointers are absolute DS offsets, so the first must resolve back to the
        // name block. This is what tells a real slab from a scratch copy of the same string.
        int firstPointer = bytes[SaveFormat.VehicleNamePointers] |
                           (bytes[SaveFormat.VehicleNamePointers + 1] << 8);
        if (firstPointer != SaveFormat.DsBase + SaveFormat.VehicleNames) return false;

        // Motorcycle: mass 1, structure 3, 100 MPH, manoeuvrability 4.
        int t = SaveFormat.VehicleTypeTable;
        return bytes[t] == 1 && bytes[t + 1] == 3 && bytes[t + 2] == 10 && bytes[t + 3] == 4;
    }

    public bool LooksValid() => LooksLikeSlab(_cache);
}
