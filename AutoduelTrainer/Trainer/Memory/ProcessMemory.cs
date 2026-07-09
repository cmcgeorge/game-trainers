using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using static AutoduelTrainer.Memory.NativeMethods;

namespace AutoduelTrainer.Memory;

/// <summary>
/// Thin wrapper over OpenProcess / Read- / WriteProcessMemory that also exposes
/// the committed memory regions of the target, used to scan the DOSBox guest RAM.
/// </summary>
public sealed class ProcessMemory : IDisposable
{
    private IntPtr _handle;

    public int ProcessId { get; }
    public bool IsOpen => _handle != IntPtr.Zero;

    public ProcessMemory(int processId)
    {
        ProcessId = processId;
        _handle = OpenProcess(ProcessAccess.All, false, processId);
        if (_handle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            string hint = err switch
            {
                5 => " Access was denied — if DOSBox is running elevated, run the trainer as administrator too.",
                87 => " The process may have exited; refresh the process list and try again.",
                _ => ""
            };
            throw new InvalidOperationException(
                $"OpenProcess failed for PID {processId} (Win32 error {err}).{hint}");
        }
    }

    public readonly record struct Region(IntPtr Base, long Size, uint Protect, uint Type);

    /// <summary>Enumerate committed, readable, non-guard regions of the target.</summary>
    public IEnumerable<Region> EnumerateRegions(long minSize = 0)
    {
        IntPtr address = IntPtr.Zero;
        var mbiSize = (IntPtr)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>();
        while (VirtualQueryEx(_handle, address, out var mbi, mbiSize) != IntPtr.Zero)
        {
            long regionSize = mbi.RegionSize.ToInt64();
            if (regionSize <= 0) break;

            bool committed = mbi.State == MEM_COMMIT;
            bool guarded = (mbi.Protect & PAGE_GUARD) != 0;
            bool noAccess = (mbi.Protect & PAGE_NOACCESS) != 0;
            bool readable = (mbi.Protect & (PAGE_READWRITE | PAGE_EXECUTE_READWRITE |
                                            PAGE_WRITECOPY | PAGE_EXECUTE_WRITECOPY)) != 0;

            if (committed && readable && !guarded && !noAccess && regionSize >= minSize)
                yield return new Region(mbi.BaseAddress, regionSize, mbi.Protect, mbi.Type);

            long next = mbi.BaseAddress.ToInt64() + regionSize;
            if (next <= address.ToInt64()) break; // overflow / no progress guard
            address = (IntPtr)next;
        }
    }

    public byte[] Read(IntPtr address, int length)
    {
        var buffer = new byte[length];
        if (!ReadProcessMemory(_handle, address, buffer, (IntPtr)length, out var read) ||
            read.ToInt64() != length)
            throw new IOException(
                $"ReadProcessMemory failed at 0x{address.ToInt64():X} len {length} " +
                $"(Win32 error {Marshal.GetLastWin32Error()}).");
        return buffer;
    }

    /// <summary>Best-effort read that returns how many bytes were actually read.</summary>
    public bool TryRead(IntPtr address, byte[] buffer, out int read)
    {
        bool ok = ReadProcessMemory(_handle, address, buffer, (IntPtr)buffer.Length, out var r);
        read = (int)r.ToInt64();
        return ok && read == buffer.Length;
    }

    public void Write(IntPtr address, byte[] data)
    {
        if (!WriteProcessMemory(_handle, address, data, (IntPtr)data.Length, out var written) ||
            written.ToInt64() != data.Length)
            throw new IOException(
                $"WriteProcessMemory failed at 0x{address.ToInt64():X} len {data.Length} " +
                $"(Win32 error {Marshal.GetLastWin32Error()}).");
    }

    public byte ReadByte(IntPtr address) => Read(address, 1)[0];
    public void WriteByte(IntPtr address, byte value) => Write(address, new[] { value });

    public static Process[] FindDosBoxProcesses() =>
        Process.GetProcesses()
            .Where(p =>
            {
                try { return p.ProcessName.StartsWith("dosbox", StringComparison.OrdinalIgnoreCase); }
                catch { return false; }
            })
            .ToArray();

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }
}
