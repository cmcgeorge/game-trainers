using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WastelandRemasteredTrainer.Memory;

/// <summary>
/// Abstraction over process-memory reads/writes so the locator, character record and
/// tests can be exercised against a synthetic address space. The live implementation
/// wraps <see cref="ProcessMemory"/>.
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
public sealed class ProcessMemorySource : IMemorySource, IDisposable
{
    private readonly ProcessMemory _mem;

    public ProcessMemorySource(ProcessMemory mem) => _mem = mem;

    public int Read(nuint address, byte[] buffer, int count) => _mem.Read(address, buffer, count);
    public bool Write(nuint address, byte[] buffer) => _mem.Write(address, buffer);

    public IEnumerable<(nuint Base, nuint Size)> EnumerateRegions() =>
        _mem.EnumerateRegions().Select(r => (r.Base, r.Size));

    public void Dispose() => _mem.Dispose();
}

/// <summary>Synthetic <see cref="IMemorySource"/> for the verification harness. Backed by a dictionary of pages.</summary>
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
        foreach (var (baseAddr, data) in _pages)
        {
            if (address < baseAddr || address >= baseAddr + (nuint)data.Length) continue;
            int off = (int)(address - baseAddr);
            int n = Math.Min(count, data.Length - off);
            if (n < count) return 0;
            Array.Copy(data, off, buffer, 0, n);
            return n;
        }
        return 0;
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
}
