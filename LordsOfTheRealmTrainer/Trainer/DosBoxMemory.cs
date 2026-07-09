using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LordsTrainer;

/// <summary>
/// Read/write access to the emulated PC's guest memory, as needed by the game and
/// scanner logic. Abstracting <see cref="DosBoxMemory"/> behind this interface lets
/// those layers be exercised against an in-memory fake, with no live DOSBox-X process.
/// </summary>
public interface IGuestMemory
{
    /// <summary>Reads <paramref name="length"/> bytes; the buffer is zero-filled on a
    /// failed or short read (best-effort — use the Try* helpers when failure matters).</summary>
    byte[] ReadGuest(uint linear, int length);

    /// <summary>Reads into <paramref name="buffer"/>; returns the number of bytes actually
    /// read (0 on failure or out-of-bounds).</summary>
    int ReadGuestInto(uint linear, Span<byte> buffer);

    bool WriteInt16(uint linear, short value);
    int ReadInt32(uint linear);

    bool TryReadInt32(uint linear, out int value);
    bool TryReadInt16(uint linear, out short value);
}

/// <summary>
/// Attaches to a running DOSBox-X process and exposes read/write access to the
/// emulated PC's <em>guest physical</em> memory (the address space the DOS game
/// LORDS.EXE sees).
///
/// Guest RAM is located by fingerprint, not by a fixed offset, so it works
/// across DOSBox-X versions, memsize configurations and process re-launches:
///   1. Find a committed, read/write, private region large enough to be guest RAM.
///   2. Inside it, find the BIOS Data Area serial-port table (COM1=0x3F8,
///      COM2=0x2F8 -> bytes F8 03 F8 02) which sits at guest offset 0x400.
///   3. guestBase(host VA) = fingerprintAddress - 0x400.
///   4. Verify by checking the INT 10h Interrupt Vector Table entry points into the
///      upper-memory ROM area (segment >= 0xC000: VGA option ROM or system BIOS).
/// See ../.docs/ReverseEngineering.md for the derivation.
/// </summary>
public sealed class DosBoxMemory : IGuestMemory, IDisposable
{
    // Candidate guest-RAM region size window. Deliberately wide (≈1 MB … 264 MB) so
    // non-default DOSBox-X `memsize` settings still attach; the BDA fingerprint +
    // IVT check below are what actually disambiguate the region, not the size.
    private const long MinRegion = 0x00100000;
    private const long MaxRegion = 0x10800000;

    private IntPtr _handle = IntPtr.Zero;
    private long _guestBaseHostVa;   // host virtual address of guest physical 0
    private long _guestSize;         // bytes of guest RAM available from the base

    public int ProcessId { get; private set; }
    public bool IsAttached => _handle != IntPtr.Zero && _guestBaseHostVa != 0;

    /// <summary>Size, in bytes, of the guest RAM window from guest 0. Zero when detached.</summary>
    public long GuestSize => _guestSize;

    /// <summary>A DOSBox-X process candidate (id + window title), decoupled from the
    /// underlying <see cref="Process"/> object so no OS handles are leaked.</summary>
    public readonly record struct Candidate(int Id, string Title);

    /// <summary>Returns DOSBox-X-style processes currently running.</summary>
    public static Candidate[] FindCandidates()
    {
        var list = new List<Candidate>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.ProcessName.StartsWith("dosbox", StringComparison.OrdinalIgnoreCase))
                    list.Add(new Candidate(p.Id, SafeTitle(p)));
            }
            catch { /* process exited between enumeration and access */ }
            finally { p.Dispose(); }   // release the handle GetProcesses() opened
        }
        return list.ToArray();
    }

    private static string SafeTitle(Process p)
    {
        try { return p.MainWindowTitle ?? ""; } catch { return ""; }
    }

    /// <summary>
    /// Attaches to the given process id and locates guest RAM.
    /// Throws <see cref="AttachException"/> with a human-readable reason on failure.
    /// </summary>
    public void Attach(int pid)
    {
        Dispose();

        var handle = Native.OpenProcess(Native.ProcessAccess.RW, false, pid);
        if (handle == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            throw new AttachException(
                $"OpenProcess failed (Win32 error {err}). " +
                "Try launching this trainer as Administrator.");
        }

        _handle = handle;
        ProcessId = pid;

        if (!TryLocateGuestBase(out _guestBaseHostVa, out _guestSize))
        {
            Dispose();
            throw new AttachException(
                "Attached to the process, but could not locate the emulated PC's RAM " +
                "(no BIOS Data Area fingerprint found). Make sure the game is actually " +
                "running inside this DOSBox-X instance.");
        }
    }

    private bool TryLocateGuestBase(out long guestBaseHostVa, out long guestSize)
    {
        guestBaseHostVa = 0;
        guestSize = 0;
        IntPtr address = IntPtr.Zero;
        IntPtr mbiSize = new IntPtr(Marshal.SizeOf<Native.MEMORY_BASIC_INFORMATION>());

        while (true)
        {
            if (Native.VirtualQueryEx(_handle, address, out var mbi, mbiSize) == IntPtr.Zero)
                break;

            long regionSize = mbi.RegionSize.ToInt64();
            if (regionSize <= 0)
                break;

            // Mask off modifier bits (PAGE_GUARD/NOCACHE/WRITECOMBINE) before comparing,
            // and skip guard pages outright — they cannot be read without faulting.
            uint baseProtect = mbi.Protect & Native.PAGE_PROTECTION_MASK;
            bool guardPage = (mbi.Protect & 0x100) != 0; // PAGE_GUARD
            bool writable = !guardPage && baseProtect is Native.PAGE_READWRITE
                                                       or Native.PAGE_EXECUTE_READWRITE
                                                       or Native.PAGE_WRITECOPY;

            if (mbi.State == Native.MEM_COMMIT
                && mbi.Type == Native.MEM_PRIVATE
                && writable
                && regionSize >= MinRegion
                && regionSize <= MaxRegion)
            {
                if (TryFingerprintRegion(mbi.BaseAddress.ToInt64(), regionSize, out guestBaseHostVa, out guestSize))
                    return true;
            }

            long next = mbi.BaseAddress.ToInt64() + regionSize;
            if (next <= address.ToInt64())
                break;             // guard against non-advancing scan
            address = new IntPtr(next);
        }
        return false;
    }

    // Scan the first 64 KB of a candidate region for the BDA serial fingerprint.
    private bool TryFingerprintRegion(long regionBase, long regionSize, out long guestBaseHostVa, out long guestSize)
    {
        guestBaseHostVa = 0;
        guestSize = 0;
        int scan = (int)Math.Min(0x10000, regionSize);
        var buf = new byte[scan];
        if (!Native.ReadProcessMemory(_handle, new IntPtr(regionBase), ref buf[0],
                new IntPtr(scan), out var read) || read.ToInt64() < 0x600)
            return false;

        int limit = (int)read.ToInt64() - 4;
        for (int i = 0; i <= limit; i++)
        {
            // COM1 = 0x03F8, COM2 = 0x02F8 stored little-endian at BDA 0x400/0x402.
            if (buf[i] == 0xF8 && buf[i + 1] == 0x03 && buf[i + 2] == 0xF8 && buf[i + 3] == 0x02)
            {
                long candidate = regionBase + i - 0x400;     // guest 0x400 -> guest 0
                if (candidate >= regionBase && VerifyIvt(candidate))
                {
                    guestBaseHostVa = candidate;
                    // Guest RAM runs from the base to the end of the committed region.
                    guestSize = regionBase + regionSize - candidate;
                    return true;
                }
            }
        }
        return false;
    }

    // Confirm a candidate base really is guest RAM by checking the INT 10h (video)
    // Interrupt Vector Table entry points into the ROM/BIOS area. A real-mode IVT
    // entry at guest N*4 is offset(word):segment(word). Depending on whether the
    // video BIOS has already hooked INT 10h, its segment is either 0xF000 (system
    // BIOS) or 0xC000 (VGA option ROM) — both observed live in the same session — so
    // we accept any segment in the upper-memory ROM range [0xC000, 0xFFFF]. Together
    // with the exact BDA serial fingerprint this makes a false positive vanishingly
    // unlikely, while not over-fitting to one boot state.
    private bool VerifyIvt(long guestBaseHostVa)
    {
        var v = new byte[4];
        if (!Native.ReadProcessMemory(_handle, new IntPtr(guestBaseHostVa + 0x10 * 4), ref v[0],
                new IntPtr(4), out var read) || read.ToInt64() < 4)
            return false;
        ushort seg = (ushort)(v[2] | (v[3] << 8));
        return seg >= 0xC000;
    }

    /// <summary>
    /// True if we are attached and the target process is still running. Cheap enough
    /// to poll each UI tick; lets the app notice DOSBox-X exiting instead of silently
    /// reading/writing a dead handle forever.
    /// </summary>
    public bool IsProcessAlive()
    {
        if (_handle == IntPtr.Zero) return false;
        if (!Native.GetExitCodeProcess(_handle, out uint code)) return false;
        return code == Native.STILL_ACTIVE;
    }

    /// <summary>
    /// Re-confirms the cached guest base still points at real guest RAM by re-checking
    /// the BDA fingerprint and IVT at <c>guestBase + 0x400</c>. Guards against DOSBox-X
    /// remapping or reconfiguring guest RAM (memsize change, machine reset) while the
    /// process stays alive — after which the cached host VA would be stale and every
    /// write would land at an arbitrary host address. Cheap enough to poll ~1×/second.
    /// </summary>
    public bool VerifyStillAttached()
    {
        if (!IsAttached) return false;
        Span<byte> bda = stackalloc byte[4];
        if (ReadGuestInto(0x400, bda) != 4) return false;
        if (!(bda[0] == 0xF8 && bda[1] == 0x03 && bda[2] == 0xF8 && bda[3] == 0x02)) return false;
        return VerifyIvt(_guestBaseHostVa);
    }

    // ---- Guest memory access (linear = segment*16 + offset) -------------------

    // Every access is bounded to [0, guestSize): a full-uint `linear` could otherwise
    // form a host address well past the emulated RAM (into DOSBox-X's own heap/code),
    // turning a mistyped scan range into a stray write into the emulator process.
    private bool InBounds(long linear, long length) =>
        linear >= 0 && length >= 0 && linear + length <= _guestSize;

    public byte[] ReadGuest(uint linear, int length)
    {
        var buf = new byte[length];
        ReadGuestInto(linear, buf);   // zero-filled on failure/out-of-bounds (best-effort)
        return buf;
    }

    /// <summary>Reads into a caller-supplied span; returns bytes actually read (0 on failure/OOB).</summary>
    public int ReadGuestInto(uint linear, Span<byte> buffer)
    {
        if (buffer.IsEmpty) return 0;
        if (!IsAttached || !InBounds(linear, buffer.Length)) return 0;
        if (!Native.ReadProcessMemory(_handle, new IntPtr(_guestBaseHostVa + linear),
                ref MemoryMarshal.GetReference(buffer), new IntPtr(buffer.Length), out var read))
            return 0;
        return (int)read.ToInt64();
    }

    public bool WriteGuest(uint linear, byte[] data)
    {
        if (!IsAttached || !InBounds(linear, data.Length)) return false;
        if (data.Length == 0) return true;
        return Native.WriteProcessMemory(_handle, new IntPtr(_guestBaseHostVa + linear),
            ref data[0], new IntPtr(data.Length), out _);
    }

    public int ReadInt32(uint linear) => BitConverter.ToInt32(ReadGuest(linear, 4), 0);
    public short ReadInt16(uint linear) => BitConverter.ToInt16(ReadGuest(linear, 2), 0);
    public bool WriteInt32(uint linear, int value) => WriteGuest(linear, BitConverter.GetBytes(value));
    public bool WriteInt16(uint linear, short value) => WriteGuest(linear, BitConverter.GetBytes(value));

    /// <summary>
    /// Reads a typed value, reporting failure instead of silently returning 0.
    /// Callers (e.g. the watch refresh and all game-layout validation) can then leave a
    /// stale value in place — or fail validation — rather than treating a failed read
    /// as a genuine 0. Uses a stack buffer, so it allocates nothing on the hot tick.
    /// </summary>
    public bool TryReadValue(uint linear, ValueType type, out long value)
    {
        value = 0;
        int w = type == ValueType.Int32 ? 4 : 2;
        Span<byte> buf = stackalloc byte[4];
        if (ReadGuestInto(linear, buf[..w]) != w) return false;
        value = type == ValueType.Int32 ? BitConverter.ToInt32(buf) : BitConverter.ToInt16(buf);
        return true;
    }

    public bool TryReadInt32(uint linear, out int value)
    {
        bool ok = TryReadValue(linear, ValueType.Int32, out long v);
        value = (int)v;
        return ok;
    }

    public bool TryReadInt16(uint linear, out short value)
    {
        bool ok = TryReadValue(linear, ValueType.Int16, out long v);
        value = (short)v;
        return ok;
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            Native.CloseHandle(_handle);
            _handle = IntPtr.Zero;
        }
        _guestBaseHostVa = 0;
        _guestSize = 0;
        ProcessId = 0;
    }
}

public sealed class AttachException(string message) : Exception(message);
