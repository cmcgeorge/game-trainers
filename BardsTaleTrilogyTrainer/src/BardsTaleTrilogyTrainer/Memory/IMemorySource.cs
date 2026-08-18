namespace BardsTaleTrilogyTrainer.Memory;

/// <summary>
/// Abstraction over process-memory reads/writes so the locator and character
/// record can be unit-tested against a synthetic address space. The live
/// implementation wraps <see cref="ProcessMemory"/>.
/// </summary>
public interface IMemorySource
{
    /// <summary>Reads <paramref name="count"/> bytes at <paramref name="address"/>. Returns bytes actually read.</summary>
    int Read(nuint address, byte[] buffer, int count);

    /// <summary>Writes <paramref name="buffer"/> at <paramref name="address"/>. Returns true if all bytes written.</summary>
    bool Write(nuint address, byte[] buffer);

    /// <summary>Enumerates committed, readable memory regions.</summary>
    IEnumerable<(nuint Base, nuint Size)> EnumerateRegions();
}

/// <summary>Live <see cref="IMemorySource"/> backed by <see cref="ProcessMemory"/>.</summary>
public sealed class ProcessMemorySource : IMemorySource
{
    private readonly ProcessMemory _mem;

    public ProcessMemorySource(ProcessMemory mem) => _mem = mem;

    public int Read(nuint address, byte[] buffer, int count) => _mem.Read(address, buffer, count);
    public bool Write(nuint address, byte[] buffer) => _mem.Write(address, buffer);

    public IEnumerable<(nuint Base, nuint Size)> EnumerateRegions() =>
        _mem.EnumerateRegions().Select(r => (r.Base, r.Size));
}

/// <summary>Synthetic <see cref="IMemorySource"/> for unit tests. Backed by a dictionary of byte arrays.</summary>
public sealed class FakeMemorySource : IMemorySource
{
    private readonly Dictionary<nuint, byte[]> _pages = new();
    private readonly List<(nuint Base, nuint Size)> _regions = new();

    public void Map(nuint baseAddr, byte[] data)
    {
        _pages[baseAddr] = data;
        _regions.Add((baseAddr, (nuint)data.Length));
    }

    public int Read(nuint address, byte[] buffer, int count)
    {
        int read = 0;
        for (int i = 0; i < count; i++)
        {
            byte b = ReadByte(address + (nuint)i);
            if (b == 0 && !_pages.Any(p => address + (nuint)i >= p.Key && address + (nuint)i < p.Key + (nuint)p.Value.Length))
                break;
            buffer[i] = b;
            read++;
        }
        return read;
    }

    public bool Write(nuint address, byte[] buffer)
    {
        foreach (var (baseAddr, data) in _pages)
        {
            if (address >= baseAddr && address + (nuint)buffer.Length <= baseAddr + (nuint)data.Length)
            {
                int off = (int)(address - baseAddr);
                Array.Copy(buffer, 0, data, off, buffer.Length);
                return true;
            }
        }
        return false;
    }

    public IEnumerable<(nuint Base, nuint Size)> EnumerateRegions() => _regions;

    private byte ReadByte(nuint addr)
    {
        foreach (var (baseAddr, data) in _pages)
        {
            if (addr >= baseAddr && addr < baseAddr + (nuint)data.Length)
                return data[(int)(addr - baseAddr)];
        }
        return 0;
    }
}
