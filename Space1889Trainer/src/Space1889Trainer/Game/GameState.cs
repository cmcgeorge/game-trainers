using System.Text;

namespace Space1889Trainer.Game;

/// <summary>
/// Somewhere a 20,198-byte Space 1889 state block lives: the running game's shared allocation, a
/// <c>.SAV</c> file, or a synthetic buffer in the test harness.
/// </summary>
public interface IStateTarget
{
    /// <summary>Reads <paramref name="count"/> bytes at <paramref name="offset"/>, or null if it cannot.</summary>
    byte[]? Read(int offset, int count);

    /// <summary>Writes <paramref name="data"/> at <paramref name="offset"/>. Returns false on failure.</summary>
    bool Write(int offset, byte[] data);

    /// <summary>False once the target has gone away (emulator closed).</summary>
    bool IsAvailable { get; }
}

/// <summary>An in-memory block: what the save editor edits, and what the harness drives.</summary>
public sealed class BufferTarget : IStateTarget
{
    private readonly byte[] _bytes;

    public BufferTarget(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != StateFormat.BlockLength)
            throw new ArgumentException(
                $"A Space 1889 state block is exactly {StateFormat.BlockLength} bytes; got {bytes.Length}.", nameof(bytes));
        _bytes = bytes;
    }

    /// <summary>The backing bytes (edited in place).</summary>
    public byte[] Bytes => _bytes;

    public bool IsAvailable => true;

    /// <summary>When set, every write fails — lets the harness prove a refused write leaves the cache alone.</summary>
    public bool RefuseWrites { get; set; }

    public byte[]? Read(int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > _bytes.Length - count) return null;
        var slice = new byte[count];
        Array.Copy(_bytes, offset, slice, 0, count);
        return slice;
    }

    public bool Write(int offset, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (RefuseWrites) return false;
        if (offset < 0 || offset > _bytes.Length - data.Length) return false;
        Array.Copy(data, 0, _bytes, offset, data.Length);
        return true;
    }
}

/// <summary>
/// A cached, typed view over one state block.
/// <para>
/// The whole block is pulled in one read so a screen paints from one consistent snapshot, and every
/// setter writes only the bytes it owns straight through to the target — never the whole block, because
/// the game rewrites its clock and the party's position continuously and a full flush would race it.
/// An edit the target refuses leaves the cache untouched, so the UI never shows a value the game does
/// not actually hold.
/// </para>
/// </summary>
public sealed class GameState
{
    private byte[] _cache = new byte[StateFormat.BlockLength];

    public GameState(IStateTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        Target = target;
    }

    public IStateTarget Target { get; }

    /// <summary>The most recent snapshot. Treat as read-only; edits go through the setters.</summary>
    public byte[] Snapshot => _cache;

    /// <summary>True once <see cref="Refresh"/> has pulled a block.</summary>
    public bool IsLoaded { get; private set; }

    /// <summary>Re-reads the whole block. Returns false if the target could not supply one.</summary>
    public bool Refresh()
    {
        var data = Target.Read(0, StateFormat.BlockLength);
        if (data is null || data.Length != StateFormat.BlockLength) return false;
        _cache = data;
        IsLoaded = true;
        return true;
    }

    /// <summary>Does the cached snapshot still look like a Space 1889 state block?</summary>
    public bool LooksValid() => StateFormat.LooksLikeState(_cache);

    // ---- primitives ---------------------------------------------------------------

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

    public short GetInt16(int offset) => (short)GetUInt16(offset);

    public bool SetUInt16(int offset, int value)
    {
        int v = Math.Clamp(value, 0, ushort.MaxValue);
        return SetBytes(offset, new[] { (byte)v, (byte)(v >> 8) });
    }

    public bool SetInt16(int offset, int value)
    {
        int v = Math.Clamp(value, short.MinValue, short.MaxValue);
        return SetBytes(offset, new[] { (byte)v, (byte)(v >> 8) });
    }

    public uint GetUInt32(int offset) =>
        (uint)(_cache[offset] | (_cache[offset + 1] << 8) | (_cache[offset + 2] << 16) | (_cache[offset + 3] << 24));

    public bool SetUInt32(int offset, long value)
    {
        uint v = (uint)Math.Clamp(value, 0, uint.MaxValue);
        return SetBytes(offset, new[] { (byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24) });
    }

    public byte[] GetBytes(int offset, int count)
    {
        var slice = new byte[count];
        Array.Copy(_cache, offset, slice, 0, count);
        return slice;
    }

    /// <summary>Writes <paramref name="data"/> if it differs from the cache; an unchanged edit sends nothing.</summary>
    public bool SetBytes(int offset, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (offset < 0 || offset > _cache.Length - data.Length) return false;
        if (_cache.AsSpan(offset, data.Length).SequenceEqual(data)) return true;
        if (!Target.Write(offset, data)) return false;
        Array.Copy(data, 0, _cache, offset, data.Length);
        return true;
    }

    /// <summary>Reads a NUL-terminated, fixed-width ASCII field.</summary>
    public string GetString(int offset, int width)
    {
        int n = 0;
        while (n < width && _cache[offset + n] != 0) n++;
        return Encoding.ASCII.GetString(_cache, offset, n);
    }

    /// <summary>
    /// Writes a NUL-terminated, fixed-width field: upper-cased (the game's font has no lower case),
    /// non-ASCII replaced, truncated to leave room for the terminator, the rest zero-filled.
    /// </summary>
    public bool SetString(int offset, int width, string value)
    {
        var buf = new byte[width];
        var text = (value ?? "").ToUpperInvariant();
        int n = Math.Min(text.Length, width - 1);
        for (int i = 0; i < n; i++)
        {
            char c = text[i];
            buf[i] = c >= 0x20 && c <= 0x7E ? (byte)c : (byte)'?';
        }
        return SetBytes(offset, buf);
    }
}
