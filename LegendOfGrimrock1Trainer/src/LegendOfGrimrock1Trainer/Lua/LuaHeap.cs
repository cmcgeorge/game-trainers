using LegendOfGrimrock1Trainer.Memory;

namespace LegendOfGrimrock1Trainer.Lua;

/// <summary>Header fields of a <c>GCtab</c>, as read from the target.</summary>
public readonly record struct LuaTable(uint Address, uint Array, uint Node, uint ArraySize, uint HashMask, uint Metatable)
{
    /// <summary>Number of hash nodes; LuaJIT stores <c>size - 1</c>, and an empty table uses a shared dummy node.</summary>
    public long HashNodeCount => HashMask == uint.MaxValue ? 0 : (long)HashMask + 1;
}

/// <summary>
/// A read/write view of a LuaJIT 2.0 heap living in another process.
///
/// Everything the trainer knows about Grimrock's state is reached through this class: the game keeps
/// its party, champions, stats, conditions, skills and dungeon in ordinary Lua tables, so "find the
/// party's health" is a table lookup by name rather than a value scan. Reads are pull-based and
/// nothing is cached between refreshes except interned strings, which LuaJIT never mutates.
/// </summary>
public sealed class LuaHeap
{
    private readonly IMemorySource _mem;
    private readonly Dictionary<uint, string?> _strings = new();

    /// <summary>Wraps a memory source. The source is not owned and is not disposed here.</summary>
    public LuaHeap(IMemorySource mem)
    {
        ArgumentNullException.ThrowIfNull(mem);
        _mem = mem;
    }

    /// <summary>Drops the interned-string cache. Call when re-attaching to a different process.</summary>
    public void ResetCache() => _strings.Clear();

    /// <summary>Reads <paramref name="count"/> bytes, or an empty array when the read fails.</summary>
    public byte[] Read(uint address, int count)
    {
        if (count <= 0) return Array.Empty<byte>();
        var buf = new byte[count];
        return _mem.Read(address, buf, count) == count ? buf : Array.Empty<byte>();
    }

    /// <summary>Reads a 32-bit word, or null when the read fails.</summary>
    public uint? ReadUInt32(uint address)
    {
        var b = Read(address, 4);
        return b.Length == 4 ? BitConverter.ToUInt32(b) : null;
    }

    /// <summary>Reads the <c>gct</c> discriminator of a collectable object, or null.</summary>
    public byte? ReadGcType(uint address)
    {
        if (address == 0) return null;
        var b = Read(address, LuaLayout.GcType + 1);
        return b.Length == 0 ? null : b[LuaLayout.GcType];
    }

    /// <summary>Reads the <c>TValue</c> at <paramref name="slot"/>.</summary>
    public LuaValue ReadValue(uint slot)
    {
        var b = Read(slot, LuaLayout.TValueSize);
        return b.Length == LuaLayout.TValueSize ? LuaValue.Parse(b, 0, slot) : LuaValue.Unreadable(slot);
    }

    /// <summary>
    /// Overwrites a numeric slot. Only ever used on slots whose current contents were just read back
    /// as a number, so no GC write barrier is involved: swapping one double for another cannot make
    /// the collector miss a reference.
    /// </summary>
    public bool WriteNumber(uint slot, double value)
    {
        if (slot == 0) return false;
        return _mem.Write(slot, BitConverter.GetBytes(value));
    }

    /// <summary>
    /// Materialises an interned string. Validates the object header before trusting <c>len</c>, so a
    /// stale or bogus pointer yields null instead of a multi-megabyte read.
    ///
    /// Only <i>successful</i> reads are cached. A failure here can be transient — a page that was
    /// momentarily unreadable, a read that raced the collector — and memoising it would permanently
    /// unmatch that key: <see cref="GetField"/> compares interned characters, so a cached null for
    /// the <c>health</c> string would make the stat vanish from the UI and turn every write to it
    /// into a silent no-op for the rest of the session.
    /// </summary>
    public string? ReadString(uint gcstr)
    {
        if (gcstr == 0) return null;
        if (_strings.TryGetValue(gcstr, out var cached)) return cached;

        var header = Read(gcstr, LuaLayout.StringHeaderSize);
        if (header.Length != LuaLayout.StringHeaderSize) return null;
        if (header[LuaLayout.GcType] != LuaLayout.GcTypeString) return null;

        uint len = BitConverter.ToUInt32(header, LuaLayout.StringLength);
        if (len > LuaLayout.MaxStringLength) return null;

        var chars = Read(gcstr + LuaLayout.StringHeaderSize, (int)len);
        if (chars.Length != len) return null;

        string result = System.Text.Encoding.Latin1.GetString(chars);
        if (result.Length <= MaxCachedStringKey) _strings[gcstr] = result;
        return result;
    }

    /// <summary>
    /// Longest string worth caching. Key names — which is all the lookup path reads — are short;
    /// caching a champion name or a monster description only wastes memory and widens the window in
    /// which a collected-and-reused address could still answer from the cache.
    /// </summary>
    private const int MaxCachedStringKey = 64;

    /// <summary>Resolves a value to its string, or null when it is not a readable string.</summary>
    public string? StringOf(LuaValue value) => value.IsString ? ReadString(value.Reference) : null;

    /// <summary>Reads a <c>GCtab</c> header, rejecting anything whose <c>gct</c> is not a table.</summary>
    public bool TryReadTable(uint address, out LuaTable table)
    {
        table = default;
        if (address == 0) return false;

        var b = Read(address, LuaLayout.TableSize);
        if (b.Length != LuaLayout.TableSize || b[LuaLayout.GcType] != LuaLayout.GcTypeTable) return false;

        uint asize = BitConverter.ToUInt32(b, LuaLayout.TableArraySize);
        uint hmask = BitConverter.ToUInt32(b, LuaLayout.TableHashMask);
        if (asize > LuaLayout.MaxTableEntries) return false;
        if (hmask != uint.MaxValue && hmask > LuaLayout.MaxTableEntries) return false;

        table = new LuaTable(
            address,
            BitConverter.ToUInt32(b, LuaLayout.TableArray),
            BitConverter.ToUInt32(b, LuaLayout.TableNode),
            asize,
            hmask,
            BitConverter.ToUInt32(b, LuaLayout.TableMetatable));
        return true;
    }

    /// <summary>Reads a table from a value, or false when the value is not a table.</summary>
    public bool TryReadTable(LuaValue value, out LuaTable table)
    {
        table = default;
        return value.IsTable && TryReadTable(value.Reference, out table);
    }

    /// <summary>
    /// Enumerates every non-nil entry of a table: the array part first (keys 0..asize-1), then the
    /// hash part in bucket order. Both key and value carry the slot they came from.
    /// </summary>
    public IEnumerable<(LuaValue Key, LuaValue Value)> Entries(LuaTable table)
    {
        if (table.ArraySize > 0 && table.Array != 0)
        {
            var arr = Read(table.Array, checked((int)(table.ArraySize * LuaLayout.TValueSize)));
            if (arr.Length > 0)
            {
                for (int i = 0; i < table.ArraySize; i++)
                {
                    uint slot = table.Array + (uint)(i * LuaLayout.TValueSize);
                    var v = LuaValue.Parse(arr, i * LuaLayout.TValueSize, slot);
                    if (v.Kind is LuaKind.Nil or LuaKind.Unreadable) continue;
                    yield return (new LuaValue(LuaKind.Number, 0, i, 0), v);
                }
            }
        }

        long nodes = table.HashNodeCount;
        if (nodes <= 0 || table.Node == 0) yield break;

        var hash = Read(table.Node, checked((int)(nodes * LuaLayout.NodeSize)));
        if (hash.Length == 0) yield break;

        for (int i = 0; i < nodes; i++)
        {
            int off = i * LuaLayout.NodeSize;
            uint nodeAddr = table.Node + (uint)off;
            var key = LuaValue.Parse(hash, off + LuaLayout.NodeKey, nodeAddr + LuaLayout.NodeKey);
            if (key.Kind is LuaKind.Nil or LuaKind.Unreadable) continue;
            var val = LuaValue.Parse(hash, off + LuaLayout.NodeValue, nodeAddr + LuaLayout.NodeValue);
            if (val.Kind is LuaKind.Nil or LuaKind.Unreadable) continue;
            yield return (key, val);
        }
    }

    /// <summary>Enumerates a table given by address; yields nothing when it is not a table.</summary>
    public IEnumerable<(LuaValue Key, LuaValue Value)> Entries(uint tableAddress) =>
        TryReadTable(tableAddress, out var t) ? Entries(t) : Array.Empty<(LuaValue, LuaValue)>();

    /// <summary>
    /// Looks up a string key by walking the hash part linearly and comparing the interned characters.
    ///
    /// LuaJIT's own lookup follows the chain from <c>hashmask(hash)</c>, which would need this side to
    /// reimplement the string hash exactly and get the same answer for every build. A linear walk of
    /// a few hundred nodes costs one read and cannot disagree with the VM about where a key lives,
    /// so it is used everywhere instead.
    /// </summary>
    public LuaValue GetField(LuaTable table, string key)
    {
        long nodes = table.HashNodeCount;
        if (nodes <= 0 || table.Node == 0) return new LuaValue(LuaKind.Nil, 0, 0, 0);

        var hash = Read(table.Node, checked((int)(nodes * LuaLayout.NodeSize)));
        if (hash.Length == 0) return LuaValue.Unreadable(0);

        for (int i = 0; i < nodes; i++)
        {
            int off = i * LuaLayout.NodeSize;
            uint keyIt = BitConverter.ToUInt32(hash, off + LuaLayout.NodeKey + LuaLayout.TValueIt);
            if (keyIt != LuaLayout.ItString) continue;
            uint keyRef = BitConverter.ToUInt32(hash, off + LuaLayout.NodeKey + LuaLayout.TValueLo);
            if (!string.Equals(ReadString(keyRef), key, StringComparison.Ordinal)) continue;

            uint nodeAddr = table.Node + (uint)off;
            return LuaValue.Parse(hash, off + LuaLayout.NodeValue, nodeAddr + LuaLayout.NodeValue);
        }

        return new LuaValue(LuaKind.Nil, 0, 0, 0);
    }

    /// <summary>Looks up a string key on a table given by address.</summary>
    public LuaValue GetField(uint tableAddress, string key) =>
        TryReadTable(tableAddress, out var t) ? GetField(t, key) : LuaValue.Unreadable(0);

    /// <summary>Follows a chain of string keys, e.g. <c>party → champions</c>.</summary>
    public LuaValue GetPath(uint tableAddress, params string[] keys)
    {
        var current = new LuaValue(LuaKind.Table, 0, 0, tableAddress);
        foreach (var key in keys)
        {
            if (!TryReadTable(current, out var t)) return new LuaValue(LuaKind.Nil, 0, 0, 0);
            current = GetField(t, key);
        }
        return current;
    }

    /// <summary>
    /// Looks up an integer key, checking the array part first and then the hash part. Lua arrays are
    /// 1-based, and LuaJIT's array part covers 0..asize-1, so index 1 is usually an array hit and a
    /// sparse or freshly built table falls through to the hash.
    /// </summary>
    public LuaValue GetIndex(LuaTable table, int index)
    {
        if (index >= 0 && index < table.ArraySize && table.Array != 0)
        {
            uint slot = table.Array + (uint)(index * LuaLayout.TValueSize);
            return ReadValue(slot);
        }

        long nodes = table.HashNodeCount;
        if (nodes <= 0 || table.Node == 0) return new LuaValue(LuaKind.Nil, 0, 0, 0);

        var hash = Read(table.Node, checked((int)(nodes * LuaLayout.NodeSize)));
        if (hash.Length == 0) return LuaValue.Unreadable(0);

        for (int i = 0; i < nodes; i++)
        {
            int off = i * LuaLayout.NodeSize;
            uint keyIt = BitConverter.ToUInt32(hash, off + LuaLayout.NodeKey + LuaLayout.TValueIt);
            if (keyIt >= LuaLayout.ItNumberBoundary) continue;
            double k = BitConverter.ToDouble(hash, off + LuaLayout.NodeKey);
            if (k != index) continue;

            uint nodeAddr = table.Node + (uint)off;
            return LuaValue.Parse(hash, off + LuaLayout.NodeValue, nodeAddr + LuaLayout.NodeValue);
        }

        return new LuaValue(LuaKind.Nil, 0, 0, 0);
    }

    /// <summary>Looks up an integer key on a table given by address.</summary>
    public LuaValue GetIndex(uint tableAddress, int index) =>
        TryReadTable(tableAddress, out var t) ? GetIndex(t, index) : LuaValue.Unreadable(0);

    /// <summary>Counts the contiguous 1..n integer keys of a table, stopping at the first gap.</summary>
    public int SequenceLength(LuaTable table, int limit = 64)
    {
        int n = 0;
        while (n < limit)
        {
            var v = GetIndex(table, n + 1);
            if (v.Kind is LuaKind.Nil or LuaKind.Unreadable) break;
            n++;
        }
        return n;
    }
}
