using System.Diagnostics;

namespace SyndicateTrainer;

/// <summary>
/// Thin wrapper around a target process handle providing read/write and
/// a signature (array-of-bytes) scanner across committed memory regions.
/// </summary>
public sealed class ProcessMemory : IDisposable
{
    private IntPtr _handle;
    public Process Process { get; }
    public bool IsOpen => _handle != IntPtr.Zero;

    public ProcessMemory(Process process)
    {
        Process = process;
        _handle = NativeMethods.OpenProcess(NativeMethods.PROCESS_ACCESS, false, (uint)process.Id);
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException(
                "Could not open the process for memory access. Try running the trainer as Administrator.");
    }

    public bool TryReadBytes(long address, byte[] buffer, int count)
    {
        if (!IsOpen) return false;
        bool ok = NativeMethods.ReadProcessMemory(_handle, new IntPtr(address), buffer,
            new IntPtr(count), out IntPtr read);
        return ok && (long)read == count;
    }

    public bool TryReadInt32(long address, out int value)
    {
        var buf = new byte[4];
        if (TryReadBytes(address, buf, 4)) { value = BitConverter.ToInt32(buf, 0); return true; }
        value = 0; return false;
    }

    public bool TryReadUInt16(long address, out ushort value)
    {
        var buf = new byte[2];
        if (TryReadBytes(address, buf, 2)) { value = BitConverter.ToUInt16(buf, 0); return true; }
        value = 0; return false;
    }

    public bool TryReadByte(long address, out byte value)
    {
        var buf = new byte[1];
        if (TryReadBytes(address, buf, 1)) { value = buf[0]; return true; }
        value = 0; return false;
    }

    public bool WriteInt32(long address, int value)
        => WriteBytes(address, BitConverter.GetBytes(value));

    public bool WriteBytes(long address, byte[] data)
    {
        if (!IsOpen) return false;
        return NativeMethods.WriteProcessMemory(_handle, new IntPtr(address), data,
            new IntPtr(data.Length), out IntPtr written) && (long)written == data.Length;
    }

    /// <summary>
    /// Scan all readable committed regions for <paramref name="pattern"/>.
    /// Invokes <paramref name="onMatch"/> for every hit; return false from it to stop.
    /// </summary>
    public void ScanSignature(byte[] pattern, Func<long, bool> onMatch,
        bool privateOnly = true, CancellationToken ct = default)
    {
        if (!IsOpen) return;
        long addr = 0;
        // 64-bit user space upper bound.
        const long maxAddr = 0x7FFFFFFFFFFF;
        int mbiSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MEMORY_BASIC_INFORMATION>();
        const int chunkSize = 8 * 1024 * 1024;
        byte[] chunk = new byte[chunkSize + pattern.Length];

        while (addr < maxAddr && !ct.IsCancellationRequested)
        {
            if (NativeMethods.VirtualQueryEx(_handle, new IntPtr(addr),
                    out var mbi, new IntPtr(mbiSize)) == IntPtr.Zero)
                break;

            long regionBase = mbi.BaseAddress.ToInt64();
            long regionSize = mbi.RegionSize.ToInt64();
            if (regionSize <= 0) break;

            bool committed = mbi.State == NativeMethods.MEM_COMMIT;
            bool readable = NativeMethods.IsReadable(mbi.Protect);
            bool typeOk = !privateOnly
                          || mbi.Type == NativeMethods.MEM_PRIVATE
                          || mbi.Type == NativeMethods.MEM_MAPPED;

            if (committed && readable && typeOk)
            {
                long pos = regionBase;
                long end = regionBase + regionSize;
                while (pos < end && !ct.IsCancellationRequested)
                {
                    long remaining = end - pos;
                    // Number of start positions this iteration is responsible for.
                    int advance = (int)Math.Min(chunkSize, remaining);
                    // Read those starts plus (pattern.Length - 1) trailing bytes so a
                    // pattern that begins near the end can still be fully compared.
                    int want = (int)Math.Min((long)advance + pattern.Length - 1, remaining);
                    if (!TryReadBytes(pos, chunk, want))
                    {
                        // unreadable page inside the region; skip a page and continue
                        pos += 0x1000;
                        continue;
                    }
                    // Accept only matches whose START lies in [0, advance). A start at
                    // exactly 'advance' is owned by the next iteration, so no offset is
                    // ever reported twice at a chunk boundary.
                    int maxStart = Math.Min(advance - 1, want - pattern.Length);
                    int idx = IndexOf(chunk, want, pattern);
                    while (idx >= 0 && idx <= maxStart)
                    {
                        if (!onMatch(pos + idx)) return;
                        idx = IndexOf(chunk, want, pattern, idx + 1);
                    }
                    pos += advance;
                }
            }

            addr = regionBase + regionSize;
        }
    }

    private static int IndexOf(byte[] haystack, int length, byte[] needle, int start = 0)
    {
        int last = length - needle.Length;
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
