using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BardsTaleTrilogyTrainer.Memory;

/// <summary>
/// Abstraction over process-memory reads/writes so the locator, character record and map
/// navigator can be exercised against a synthetic address space. The live implementation
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

    /// <summary>
    /// Reserves <paramref name="size"/> readable/writable bytes inside the target and returns
    /// their address, or 0 when the target cannot be allocated into. Used to hand the game a
    /// filled-in <c>TeleportTarget</c> without disturbing an object it already owns; the block
    /// is never freed while the trainer is attached, so the game can keep referencing it.
    /// </summary>
    nuint Allocate(int size);
}

/// <summary>Live <see cref="IMemorySource"/> backed by <see cref="ProcessMemory"/>.</summary>
public sealed class ProcessMemorySource : IMemorySource, IDisposable
{
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint PAGE_READWRITE = 0x04;
    private const uint PROCESS_VM_OPERATION = 0x0008;

    private readonly ProcessMemory _mem;
    private SafeProcessHandle? _allocHandle;

    public ProcessMemorySource(ProcessMemory mem) => _mem = mem;

    public int Read(nuint address, byte[] buffer, int count) => _mem.Read(address, buffer, count);
    public bool Write(nuint address, byte[] buffer) => _mem.Write(address, buffer);

    public IEnumerable<(nuint Base, nuint Size)> EnumerateRegions() =>
        _mem.EnumerateRegions().Select(r => (r.Base, r.Size));

    /// <summary>
    /// Commits a private read/write block in the game process. A separate handle is opened for
    /// this because <see cref="ProcessMemory"/> keeps its own; it is closed on dispose, which
    /// does not free the block (the game may still hold a reference to it).
    /// </summary>
    public nuint Allocate(int size)
    {
        if (size <= 0) return 0;
        try
        {
            _allocHandle ??= OpenProcess(PROCESS_VM_OPERATION, false, _mem.ProcessId);
            if (_allocHandle == null || _allocHandle.IsInvalid) return 0;
            return VirtualAllocEx(_allocHandle, 0, (nuint)size, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
        }
        catch (Exception)
        {
            return 0;   // no allocation rights: callers fall back to reusing a live object
        }
    }

    public void Dispose()
    {
        _allocHandle?.Dispose();
        _allocHandle = null;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nuint VirtualAllocEx(SafeProcessHandle hProcess, nuint lpAddress,
        nuint dwSize, uint flAllocationType, uint flProtect);
}

/// <summary>Synthetic <see cref="IMemorySource"/> for the verification harness. Backed by a dictionary of pages.</summary>
public sealed class FakeMemorySource : IMemorySource
{
    private readonly Dictionary<nuint, byte[]> _pages = new();
    private readonly List<(nuint Base, nuint Size)> _regions = new();
    private nuint _nextAllocation = 0x7000_0000;

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
            if (n < count) return 0;                 // a read spanning past a page is a failed read
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

    /// <summary>Maps a fresh zeroed page, mirroring what <c>VirtualAllocEx</c> gives the live source.</summary>
    public nuint Allocate(int size)
    {
        if (size <= 0) return 0;
        nuint addr = _nextAllocation;
        _nextAllocation += (nuint)((size + 0xFFF) & ~0xFFF);
        Map(addr, new byte[size]);
        return addr;
    }
}
