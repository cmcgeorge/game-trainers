using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ShogunTrainer.Memory;

/// <summary>
/// Thin wrapper over a target process handle giving read/write access to its
/// virtual memory plus committed-region enumeration. Read-only by default; the
/// trainer opens it with write access as well so it can poke game state.
/// </summary>
public sealed class ProcessMemory : IDisposable
{
    private const uint PROCESS_VM_READ = 0x0010;
    private const uint PROCESS_VM_WRITE = 0x0020;
    private const uint PROCESS_VM_OPERATION = 0x0008;
    private const uint PROCESS_QUERY_INFORMATION = 0x0400;

    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_PRIVATE = 0x20000;

    private const uint PAGE_READWRITE = 0x04;
    private const uint PAGE_WRITECOPY = 0x08;
    private const uint PAGE_EXECUTE_READWRITE = 0x40;
    private const uint PAGE_EXECUTE_WRITECOPY = 0x80;
    private const uint PAGE_GUARD = 0x100;

    private IntPtr _handle;

    public int ProcessId { get; }
    public string ProcessName { get; }

    private ProcessMemory(IntPtr handle, int pid, string name)
    {
        _handle = handle;
        ProcessId = pid;
        ProcessName = name;
    }

    /// <summary>Open a process for read+write+query. Throws Win32Exception on failure.</summary>
    public static ProcessMemory Open(Process process)
    {
        IntPtr h = OpenProcess(
            PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION,
            false, (uint)process.Id);
        if (h == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"OpenProcess failed for pid {process.Id} ({process.ProcessName}). Try running as Administrator.");
        return new ProcessMemory(h, process.Id, process.ProcessName);
    }

    public bool IsOpen => _handle != IntPtr.Zero;

    /// <summary>A committed memory region in the target.</summary>
    public readonly record struct Region(ulong BaseAddress, ulong Size, uint Protect, uint Type)
    {
        public bool IsPrivate => Type == MEM_PRIVATE;
        public bool IsWritable =>
            Protect is PAGE_READWRITE or PAGE_WRITECOPY or PAGE_EXECUTE_READWRITE or PAGE_EXECUTE_WRITECOPY;
    }

    /// <summary>Enumerate committed, non-guard regions of the target.</summary>
    public IEnumerable<Region> EnumerateRegions()
    {
        ulong addr = 0;
        // 64-bit user address space upper bound.
        const ulong max = 0x7FFFFFFF0000UL;
        while (addr < max)
        {
            int written = VirtualQueryEx(_handle, (IntPtr)addr, out MEMORY_BASIC_INFORMATION mbi,
                (uint)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>());
            if (written == 0)
                break;

            ulong regionBase = (ulong)mbi.BaseAddress;
            ulong regionSize = (ulong)mbi.RegionSize;
            if (regionSize == 0)
                break;

            bool guard = (mbi.Protect & PAGE_GUARD) != 0;
            if (mbi.State == MEM_COMMIT && !guard && mbi.Protect != 0)
                yield return new Region(regionBase, regionSize, mbi.Protect, mbi.Type);

            addr = regionBase + regionSize;
        }
    }

    public byte[]? Read(ulong address, int size)
    {
        var buffer = new byte[size];
        if (!ReadProcessMemory(_handle, (IntPtr)address, buffer, (UIntPtr)size, out UIntPtr read)
            || (int)read != size)
            return null;
        return buffer;
    }

    /// <summary>Read up to <paramref name="size"/> bytes; returns however many succeeded.</summary>
    public byte[] ReadPartial(ulong address, int size)
    {
        var buffer = new byte[size];
        ReadProcessMemory(_handle, (IntPtr)address, buffer, (UIntPtr)size, out UIntPtr read);
        if ((int)read == size) return buffer;
        Array.Resize(ref buffer, (int)read);
        return buffer;
    }

    public byte ReadByte(ulong address)
    {
        var b = Read(address, 1);
        return b is null ? (byte)0 : b[0];
    }

    public bool WriteByte(ulong address, byte value) => Write(address, new[] { value });

    public bool Write(ulong address, byte[] data)
    {
        return WriteProcessMemory(_handle, (IntPtr)address, data, (UIntPtr)data.Length, out UIntPtr written)
            && (int)written == data.Length;
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
    }

    // ---- P/Invoke ----

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public uint __alignment1;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
        public uint __alignment2;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress,
        out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
        byte[] lpBuffer, UIntPtr nSize, out UIntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
        byte[] lpBuffer, UIntPtr nSize, out UIntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
