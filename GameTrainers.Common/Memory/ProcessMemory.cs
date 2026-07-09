using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using static GameTrainers.Common.Memory.NativeMethods;

namespace GameTrainers.Common.Memory;

/// <summary>A committed, readable memory region in the target process.</summary>
public readonly record struct MemoryRegion(nuint Base, nuint Size)
{
    public nuint End => Base + Size;
}

/// <summary>
/// Thin wrapper around OpenProcess / ReadProcessMemory / WriteProcessMemory and
/// region enumeration. Disposable; closes the process handle on dispose. The handle
/// is a SafeProcessHandle, so disposing while a pool-thread scan or dump is mid-read
/// is safe: the in-flight call completes on the live handle and subsequent calls
/// fail benignly (reads return 0) instead of touching a freed or recycled handle.
/// </summary>
public sealed class ProcessMemory : IDisposable
{
    private readonly SafeProcessHandle _handle;

    public int ProcessId { get; }
    public bool IsOpen => !_handle.IsClosed && !_handle.IsInvalid;

    private ProcessMemory(SafeProcessHandle handle, int pid)
    {
        _handle = handle;
        ProcessId = pid;
    }

    public static ProcessMemory Open(int processId)
    {
        var h = OpenProcess(ProcessAccess.All, false, processId);
        if (h.IsInvalid)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"OpenProcess failed for pid {processId}. Try running the trainer as administrator.");
        return new ProcessMemory(h, processId);
    }

    /// <summary>Reads <paramref name="count"/> bytes at <paramref name="address"/>. Returns bytes actually read.</summary>
    public int Read(nuint address, byte[] buffer, int count)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        if (count < 0 || count > buffer.Length)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be within the destination buffer.");
        if (count == 0) return 0;
        try
        {
            if (!ReadProcessMemory(_handle, address, buffer, (UIntPtr)count, out var read))
                return 0;
            return (int)read;
        }
        catch (ObjectDisposedException)
        {
            return 0;   // detached mid-read; the caller already treats 0 as "unreadable"
        }
    }

    public byte[] Read(nuint address, int count)
    {
        var buf = new byte[count];
        int read = Read(address, buf, count);
        if (read != count) Array.Resize(ref buf, read);
        return buf;
    }

    /// <summary>Writes <paramref name="buffer"/> at <paramref name="address"/>. Returns true if all bytes written.</summary>
    public bool Write(nuint address, byte[] buffer)
    {
        try
        {
            bool ok = WriteProcessMemory(_handle, address, buffer, (UIntPtr)buffer.Length, out var written);
            return ok && (int)written == buffer.Length;
        }
        catch (ObjectDisposedException)
        {
            return false;   // detached mid-write
        }
    }

    public bool WriteRange(nuint address, byte[] buffer, int offset, int length)
    {
        if (buffer == null) throw new ArgumentNullException(nameof(buffer));
        // Phrased to avoid an offset+length overflow for extreme arguments.
        if (offset < 0 || length < 0 || offset > buffer.Length - length)
            throw new ArgumentOutOfRangeException(nameof(length), "offset/length must lie within the source buffer.");
        var slice = new byte[length];
        Array.Copy(buffer, offset, slice, 0, length);
        return Write(address + (nuint)offset, slice);
    }

    /// <summary>
    /// Enumerates committed, readable, non-guard regions of the target. Walks the
    /// whole user address space by default; VirtualQueryEx reports free/reserved
    /// ranges in large strides and returns 0 at the end, so this terminates quickly
    /// for both 32-bit (WOW64) and 64-bit targets.
    /// </summary>
    public IEnumerable<MemoryRegion> EnumerateRegions(nuint maxAddress = 0)
    {
        if (maxAddress == 0) maxAddress = nuint.MaxValue;   // 0 = whole user space
        nuint addr = 0;
        while (addr < maxAddress)
        {
            if (!TryQuery(addr, out var mbi))
                break;

            nuint regionBase = mbi.BaseAddress;
            nuint regionSize = mbi.RegionSize;
            if (regionSize == 0) break;

            bool committed = mbi.State == MEM_COMMIT;
            bool guarded = (mbi.Protect & PAGE_GUARD) != 0 || (mbi.Protect & PAGE_NOACCESS) != 0;
            if (committed && !guarded)
                yield return new MemoryRegion(regionBase, regionSize);

            nuint next = regionBase + regionSize;
            if (next <= addr) break;        // guard against wrap / no progress
            addr = next;
        }
    }

    // Kept out of the iterator body because `yield return` can't live inside a try/catch;
    // a disposed handle (detach mid-enumeration) just ends the walk.
    private bool TryQuery(nuint addr, out MEMORY_BASIC_INFORMATION mbi)
    {
        var size = (UIntPtr)(uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>();
        try
        {
            return VirtualQueryEx(_handle, addr, out mbi, size) != UIntPtr.Zero;
        }
        catch (ObjectDisposedException)
        {
            mbi = default;
            return false;
        }
    }

    public void Dispose() => _handle.Dispose();
}
