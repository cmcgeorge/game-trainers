using System.Text;
using Space1889Trainer.Game;
using Space1889Trainer.Memory;

namespace Space1889Trainer.FormatCheck;

/// <summary>
/// A synthetic emulator address space for driving <see cref="GameLocator"/> with no DOSBox and no
/// copyrighted files: a padded guest-RAM region with an emulated BIOS data area, a state block where
/// <c>1889.COM</c> would have allocated it, and optionally a Turbo C data group holding a far pointer
/// to that block.
/// </summary>
internal sealed class FakeMemory : IMemorySource
{
    private readonly List<(nuint Base, byte[] Bytes)> _regions = new();

    /// <summary>Host pages (base address) that fail to read.</summary>
    public HashSet<nuint> UnreadablePages { get; } = new();

    public nuint AddRegion(nuint baseAddress, byte[] bytes)
    {
        _regions.Add((baseAddress, bytes));
        _regions.Sort((a, b) => a.Base.CompareTo(b.Base));
        return baseAddress;
    }

    public IEnumerable<MemoryRegion> EnumerateRegions() =>
        _regions.Select(r => new MemoryRegion(r.Base, (nuint)r.Bytes.Length)).ToList();

    public int Read(nuint address, byte[] buffer, int count)
    {
        foreach (var (b, bytes) in _regions)
        {
            if (address < b || address >= b + (nuint)bytes.Length) continue;
            int start = (int)(address - b);
            int n = Math.Min(count, bytes.Length - start);
            // Like ReadProcessMemory (ERROR_PARTIAL_COPY) and ProcessMemory.Read over it: a range that touches any
            // unreadable page yields nothing at all, not the bytes in front of the hole.
            for (nuint page = address & ~(nuint)0xFFF; page < address + (nuint)n; page += 0x1000)
                if (UnreadablePages.Contains(page)) return 0;
            Array.Copy(bytes, start, buffer, 0, n);
            return n;
        }
        return 0;
    }

    public byte[] Read(nuint address, int count)
    {
        var buf = new byte[count];
        int n = Read(address, buf, count);
        if (n != count) Array.Resize(ref buf, n);
        return buf;
    }

    // ---- builders -------------------------------------------------------------------

    /// <summary>Host base of the fake guest-RAM allocation.</summary>
    public const ulong GuestRegionBase = 0x0600_0000;

    /// <summary>DOSBox pads its guest allocation; guest linear 0 sits this far in.</summary>
    public const int GuestPad = 0x20;

    /// <summary>Guest RAM size (16 MB, as memsize=16).</summary>
    public const int GuestSize = 16 << 20;

    /// <summary>Guest linear address of the state block (1889.COM's allocation in the live game).</summary>
    public const int BlockLinear = 0x2080;

    /// <summary>Guest linear address of M.EXE's data group in the live game.</summary>
    public const int DgroupLinear = 0x27020;

    /// <summary>
    /// Builds a guest with an emulated BIOS data area and <paramref name="block"/> at
    /// <paramref name="blockLinear"/>; when <paramref name="module"/> is given, also a data group with
    /// the Turbo C banner, the module's identity text and a far pointer to <paramref name="pointerTarget"/>.
    /// Returns the guest RAM bytes (live — callers may corrupt them further).
    /// </summary>
    public static (FakeMemory Memory, byte[] Guest) BuildGuest(
        byte[]? block,
        int blockLinear = BlockLinear,
        GameModule? module = null,
        int? pointerTarget = null,
        bool biosArea = true)
    {
        var guest = new byte[GuestSize + GuestPad];
        if (biosArea)
        {
            Put16(guest, GuestPad + 0x400, 0x03F8);   // 40:0000 COM1
            Put16(guest, GuestPad + 0x413, 640);      // 40:0013 memory size in KB
        }
        if (block is not null) Array.Copy(block, 0, guest, GuestPad + blockLinear, block.Length);
        if (module is not null)
        {
            int ds = GuestPad + DgroupLinear;
            var banner = Encoding.ASCII.GetBytes(GameFacts.TurboCBanner);
            Array.Copy(banner, 0, guest, ds + GameFacts.TurboCBannerOffset, banner.Length);
            var id = Encoding.ASCII.GetBytes(module.IdentityText);
            Array.Copy(id, 0, guest, ds + module.IdentityOffset, id.Length);
            int target = pointerTarget ?? blockLinear;
            Put16(guest, ds + module.StatePointerOffset, target & 0xF);          // offset
            Put16(guest, ds + module.StatePointerOffset + 2, target >> 4);       // segment
        }
        var mem = new FakeMemory();
        mem.AddRegion((nuint)GuestRegionBase, guest);
        // A small housekeeping region in front, as DOSBox has, which the locator must skip.
        mem.AddRegion((nuint)(GuestRegionBase - 0x10000), new byte[0x8000]);
        return (mem, guest);
    }

    public static void Put16(byte[] buf, int offset, int value)
    {
        buf[offset] = (byte)value;
        buf[offset + 1] = (byte)(value >> 8);
    }
}
