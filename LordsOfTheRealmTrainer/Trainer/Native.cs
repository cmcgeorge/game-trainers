using System.Runtime.InteropServices;

namespace LordsTrainer;

/// <summary>
/// Thin P/Invoke layer over the Win32 process-memory APIs used to read and
/// write the DOSBox-X emulator's address space.
///
/// The read/write buffers are passed as <c>ref byte</c> (the address of the first
/// element) rather than <c>byte[]</c>, so callers can read/write into a slice of a
/// reused buffer (or a <c>stackalloc</c> span) without allocating a throwaway array
/// per call and without needing an <c>unsafe</c> context.
/// </summary>
internal static class Native
{
    [Flags]
    public enum ProcessAccess : uint
    {
        VmOperation = 0x0008,
        VmRead = 0x0010,
        VmWrite = 0x0020,
        QueryInformation = 0x0400,

        RW = VmOperation | VmRead | VmWrite | QueryInformation,
    }

    // Memory state / type / protection constants.
    public const uint MEM_COMMIT = 0x1000;
    public const uint MEM_PRIVATE = 0x20000;
    public const uint PAGE_READWRITE = 0x04;
    public const uint PAGE_EXECUTE_READWRITE = 0x40;
    public const uint PAGE_WRITECOPY = 0x08;

    // Low byte holds the base protection; the high bits are modifiers
    // (PAGE_GUARD 0x100, PAGE_NOCACHE 0x200, PAGE_WRITECOMBINE 0x400).
    public const uint PAGE_PROTECTION_MASK = 0xFF;

    public const uint STILL_ACTIVE = 259;

    // Layout note (x64): the native struct has a `WORD PartitionId` slot between
    // AllocationProtect and RegionSize. We don't need PartitionId, and on x64 the
    // 8-byte alignment requirement of RegionSize forces the compiler to insert the
    // same 4 bytes of padding here — so every field offset (0/8/16/24/32/36/40) and
    // the total size (48) match the OS ABI exactly. Do not "tighten" this struct.
    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        // (implicit 4-byte gap == native WORD PartitionId + alignment padding)
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(ProcessAccess dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
        ref byte lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress,
        ref byte lpBuffer, IntPtr nSize, out IntPtr lpNumberOfBytesWritten);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress,
        out MEMORY_BASIC_INFORMATION lpBuffer, IntPtr dwLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);
}
