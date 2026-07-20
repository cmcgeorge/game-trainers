using System;
using System.Collections.Generic;
using SotSAgeTrainer.Core;

namespace SotSAgeTrainer.Core;

/// <summary>
/// A thin, disposable handle over another process's virtual memory: enumerate regions,
/// read, write, and pattern-scan. Knows nothing about the game — see <see cref="AgeTrainer"/>.
/// </summary>
public sealed class ProcessMemory : IDisposable
{
    private IntPtr _handle;

    public int ProcessId { get; }
    public bool IsOpen => _handle != IntPtr.Zero;

    private ProcessMemory(IntPtr handle, int pid)
    {
        _handle = handle;
        ProcessId = pid;
    }

    /// <summary>Open a process for read/write/query. Returns null if the handle can't be obtained.</summary>
    public static ProcessMemory? TryOpen(int pid)
    {
        IntPtr h = NativeMethods.OpenProcess(NativeMethods.ProcessAccess.ReadWriteQuery, false, pid);
        return h == IntPtr.Zero ? null : new ProcessMemory(h, pid);
    }

    public bool ReadInto(IntPtr address, byte[] buffer, int count)
    {
        if (!IsOpen) return false;
        return NativeMethods.ReadProcessMemory(_handle, address, buffer, (IntPtr)count, out IntPtr read)
               && (long)read == count;
    }

    public bool TryReadByte(IntPtr address, out byte value)
    {
        var one = new byte[1];
        if (ReadInto(address, one, 1)) { value = one[0]; return true; }
        value = 0;
        return false;
    }

    public bool WriteByte(IntPtr address, byte value)
    {
        if (!IsOpen) return false;
        var one = new byte[] { value };
        return NativeMethods.WriteProcessMemory(_handle, address, one, (IntPtr)1, out IntPtr written)
               && (long)written == 1;
    }

    /// <summary>
    /// Write every byte in <paramref name="buffer"/> in a single call, so a multi-byte value
    /// (e.g. a little-endian 16-bit stat) can never be left half-updated by a second syscall that
    /// fails after the first succeeded. Returns true only when the whole buffer was written.
    /// </summary>
    public bool WriteBytes(IntPtr address, byte[] buffer)
    {
        if (!IsOpen || buffer.Length == 0) return false;
        return NativeMethods.WriteProcessMemory(_handle, address, buffer, (IntPtr)buffer.Length, out IntPtr written)
               && (long)written == buffer.Length;
    }

    /// <summary>Query the protection/type/state of the region containing <paramref name="address"/>.</summary>
    public bool TryQuery(IntPtr address, out uint protect, out uint type, out uint state, out IntPtr regionBase, out long regionSize)
    {
        protect = type = state = 0; regionBase = IntPtr.Zero; regionSize = 0;
        if (!IsOpen) return false;
        int mbiSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORY_BASIC_INFORMATION>();
        if (NativeMethods.VirtualQueryEx(_handle, address, out var mbi, (IntPtr)mbiSize) == IntPtr.Zero) return false;
        protect = mbi.Protect; type = mbi.Type; state = mbi.State;
        regionBase = (IntPtr)(long)mbi.BaseAddress; regionSize = (long)mbi.RegionSize;
        return true;
    }

    /// <summary>A committed, readable region of the target's address space.</summary>
    public readonly record struct Region(IntPtr BaseAddress, long Size, uint Protect)
    {
        public bool IsWritable =>
            (Protect & (NativeMethods.PAGE_READWRITE | NativeMethods.PAGE_WRITECOPY |
                        NativeMethods.PAGE_EXECUTE_READWRITE | NativeMethods.PAGE_EXECUTE_WRITECOPY)) != 0
            && (Protect & NativeMethods.PAGE_GUARD) == 0;
    }

    /// <summary>Walk the target's committed regions from 0 up to <paramref name="maxAddress"/>.</summary>
    public IEnumerable<Region> EnumerateRegions(ulong maxAddress = 0x1_0000_0000UL)
    {
        if (!IsOpen) yield break;
        ulong addr = 0;
        int mbiSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORY_BASIC_INFORMATION>();
        while (addr < maxAddress)
        {
            if (NativeMethods.VirtualQueryEx(_handle, (IntPtr)addr, out var mbi, (IntPtr)mbiSize) == IntPtr.Zero)
                break;

            long regionSize = (long)mbi.RegionSize;
            if (regionSize <= 0) break;

            if (mbi.State == NativeMethods.MEM_COMMIT &&
                (mbi.Protect & NativeMethods.PAGE_GUARD) == 0 &&
                (mbi.Protect & NativeMethods.PAGE_NOACCESS) == 0)
            {
                yield return new Region((IntPtr)(long)mbi.BaseAddress, regionSize, mbi.Protect);
            }

            ulong next = (ulong)(long)mbi.BaseAddress + (ulong)regionSize;
            if (next <= addr) break; // guard against non-advancing walk
            addr = next;
        }
    }

    /// <summary>
    /// Scan committed regions for a byte pattern, returning the absolute address of every match.
    /// When <paramref name="writableOnly"/> is set only writable regions are scanned (the game's
    /// live state lives in DOSBox's writable emulated-RAM block, so this is both correct and fast).
    /// </summary>
    public List<IntPtr> ScanAll(byte[] pattern, bool writableOnly = true, int maxMatches = 64)
    {
        var hits = new List<IntPtr>();
        if (!IsOpen || pattern.Length == 0) return hits;

        const int chunk = 4 * 1024 * 1024;
        int overlap = pattern.Length - 1;
        var buffer = new byte[chunk];

        foreach (var region in EnumerateRegions())
        {
            if (writableOnly && !region.IsWritable) continue;

            long remaining = region.Size;
            long offset = 0;
            while (remaining > 0)
            {
                int want = (int)Math.Min(chunk, remaining);
                IntPtr readAt = (IntPtr)((long)region.BaseAddress + offset);
                if (ReadInto(readAt, buffer, want))
                {
                    int idx = 0;
                    while (true)
                    {
                        int found = IndexOf(buffer, want, pattern, idx);
                        if (found < 0) break;
                        hits.Add((IntPtr)((long)readAt + found));
                        if (hits.Count >= maxMatches) return hits;
                        idx = found + 1;
                    }
                }

                if (want < chunk) break;
                // step forward but keep an overlap so a pattern straddling a chunk boundary is caught
                offset += want - overlap;
                remaining -= want - overlap;
            }
        }
        return hits;
    }

    private static int IndexOf(byte[] haystack, int haystackLen, byte[] needle, int start)
    {
        int last = haystackLen - needle.Length;
        for (int i = start; i <= last; i++)
        {
            int j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j]) j++;
            if (j == needle.Length) return i;
        }
        return -1;
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            NativeMethods.CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
