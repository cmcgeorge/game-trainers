using System.Diagnostics;
using static KeefTrainer.Core.NativeMethods;

namespace KeefTrainer.Core;

/// <summary>Read/write access to another process's memory plus region enumeration.</summary>
public sealed class ProcessMemory : IDisposable
{
    private nint _handle;

    public int ProcessId { get; }
    public string ProcessName { get; }

    private ProcessMemory(nint handle, int pid, string name)
    {
        _handle = handle;
        ProcessId = pid;
        ProcessName = name;
    }

    public static ProcessMemory? Open(Process process)
    {
        nint h = OpenProcess(
            PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION,
            false, process.Id);
        return h == 0 ? null : new ProcessMemory(h, process.Id, process.ProcessName);
    }

    /// <summary>
    /// Liveness via the handle we already hold — no per-call Process allocation.
    /// (STILL_ACTIVE as a real exit code would be a false positive, but the caller's
    /// anchor re-validation catches that case.)
    /// </summary>
    public bool IsAlive =>
        _handle != 0 && GetExitCodeProcess(_handle, out uint exitCode) && exitCode == STILL_ACTIVE;

    public bool Read(nuint address, byte[] buffer, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, buffer.Length);
        if (_handle == 0) return false;
        return ReadProcessMemory(_handle, address, buffer, (nuint)count, out nuint read) && read == (nuint)count;
    }

    public bool Write(nuint address, byte[] buffer)
    {
        if (_handle == 0) return false;
        return WriteProcessMemory(_handle, address, buffer, (nuint)buffer.Length, out nuint written)
               && written == (nuint)buffer.Length;
    }

    public bool WriteUInt16(nuint address, ushort value) =>
        Write(address, BitConverter.GetBytes(value));

    public bool WriteUInt32(nuint address, uint value) =>
        Write(address, BitConverter.GetBytes(value));

    /// <summary>Enumerate committed private read-write regions (candidate guest-RAM blocks).</summary>
    public IEnumerable<(nuint Base, nuint Size)> EnumerateRwPrivateRegions(nuint minSize, nuint maxSize)
    {
        nuint address = 0;
        while (true)
        {
            if (VirtualQueryEx(_handle, address, out MEMORY_BASIC_INFORMATION mbi,
                    (nuint)System.Runtime.InteropServices.Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                yield break;

            if (mbi.State == MEM_COMMIT && mbi.Type == MEM_PRIVATE && mbi.Protect == PAGE_READWRITE
                && mbi.RegionSize >= minSize && mbi.RegionSize <= maxSize)
            {
                yield return (mbi.BaseAddress, mbi.RegionSize);
            }

            nuint next = mbi.BaseAddress + mbi.RegionSize;
            if (next <= address) yield break; // overflow / end of address space
            address = next;
        }
    }

    public void Dispose()
    {
        if (_handle != 0)
        {
            CloseHandle(_handle);
            _handle = 0;
        }
    }
}
