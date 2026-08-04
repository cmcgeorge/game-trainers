namespace Civilization3ConquestsTrainer.Game;

/// <summary>One PE section as it is mapped into the running process.</summary>
public readonly record struct PeSection(string Name, uint Rva, uint VirtualSize, uint Characteristics)
{
    private const uint MemExecute = 0x20000000;
    private const uint MemWrite = 0x80000000;

    /// <summary>Whether an RVA falls inside this section.</summary>
    public bool ContainsRva(uint rva) => rva >= Rva && rva < Rva + VirtualSize;

    /// <summary>
    /// Whether this section can hold mutable globals — writable and not executable. Tested by
    /// characteristics rather than by name, so a rebuild that calls its data section something other
    /// than ".data" still works.
    /// </summary>
    public bool IsWritableData => (Characteristics & MemWrite) != 0 && (Characteristics & MemExecute) == 0;
}

/// <summary>
/// The parts of a mapped PE header the locator needs: the build fingerprint and the section table.
///
/// Reading the header out of the target rather than baking <c>.rdata</c>/<c>.data</c> ranges into a
/// constant keeps the repo's rule that addresses are discovered at run time, and it doubles as the
/// build check — a different Civ3 build has a different <see cref="TimeDateStamp"/>, so the locator
/// can refuse to trust the recovered offsets instead of reading a plausible-looking wrong address.
/// </summary>
public sealed class PeImage
{
    private const int MzSignature = 0x5A4D;         // "MZ"
    private const uint PeSignature = 0x00004550;    // "PE\0\0"
    private const int HeaderBytes = 0x1000;         // the whole header lives in the first page

    /// <summary>Machine type from the COFF header (0x014C = i386).</summary>
    public ushort Machine { get; private init; }

    /// <summary>Link timestamp — the cheapest way to tell two Civ3 builds apart.</summary>
    public uint TimeDateStamp { get; private init; }

    /// <summary>Preferred load address from the optional header.</summary>
    public uint ImageBase { get; private init; }

    /// <summary>Total mapped size of the image.</summary>
    public uint SizeOfImage { get; private init; }

    /// <summary>DLL characteristics — bit 0x0040 is DYNAMICBASE (ASLR).</summary>
    public ushort DllCharacteristics { get; private init; }

    /// <summary>Sections in file order.</summary>
    public IReadOnlyList<PeSection> Sections { get; private init; } = Array.Empty<PeSection>();

    /// <summary>Whether the image opted in to ASLR. Civ3 does not, which is what makes RVAs stable.</summary>
    public bool HasAslr => (DllCharacteristics & 0x0040) != 0;

    /// <summary>Finds a section by name (e.g. ".rdata"), or null.</summary>
    public PeSection? Section(string name)
    {
        foreach (var s in Sections)
            if (s.Name == name) return s;
        return null;
    }

    /// <summary>
    /// Parses the header at <paramref name="header"/>, which must be the first
    /// <see cref="HeaderBytes"/> bytes of the mapped image. Returns null if it is not a 32-bit PE.
    /// </summary>
    public static PeImage? Parse(ReadOnlySpan<byte> header)
    {
        if (header.Length < 0x40) return null;
        if (BitConverter.ToUInt16(header[..2]) != MzSignature) return null;

        int peOffset = BitConverter.ToInt32(header.Slice(0x3C, 4));
        if (peOffset <= 0 || peOffset + 0x78 > header.Length) return null;
        if (BitConverter.ToUInt32(header.Slice(peOffset, 4)) != PeSignature) return null;

        int coff = peOffset + 4;
        ushort machine = BitConverter.ToUInt16(header.Slice(coff, 2));
        ushort sectionCount = BitConverter.ToUInt16(header.Slice(coff + 2, 2));
        uint stamp = BitConverter.ToUInt32(header.Slice(coff + 4, 4));
        ushort optSize = BitConverter.ToUInt16(header.Slice(coff + 16, 2));

        int opt = coff + 20;
        if (opt + 0x5E > header.Length) return null;
        if (BitConverter.ToUInt16(header.Slice(opt, 2)) != 0x010B) return null;   // PE32 only

        uint imageBase = BitConverter.ToUInt32(header.Slice(opt + 0x1C, 4));
        uint sizeOfImage = BitConverter.ToUInt32(header.Slice(opt + 0x38, 4));
        ushort dllChars = BitConverter.ToUInt16(header.Slice(opt + 0x46, 2));

        int table = opt + optSize;
        var sections = new List<PeSection>(sectionCount);
        for (int i = 0; i < sectionCount; i++)
        {
            int e = table + i * 40;
            if (e + 40 > header.Length) break;
            string name = System.Text.Encoding.ASCII.GetString(header.Slice(e, 8)).TrimEnd('\0');
            uint vsize = BitConverter.ToUInt32(header.Slice(e + 8, 4));
            uint rva = BitConverter.ToUInt32(header.Slice(e + 12, 4));
            uint chars = BitConverter.ToUInt32(header.Slice(e + 36, 4));
            sections.Add(new PeSection(name, rva, vsize, chars));
        }

        return new PeImage
        {
            Machine = machine,
            TimeDateStamp = stamp,
            ImageBase = imageBase,
            SizeOfImage = sizeOfImage,
            DllCharacteristics = dllChars,
            Sections = sections,
        };
    }

    /// <summary>How many bytes of the mapped image a caller should read to parse the header.</summary>
    public static int HeaderReadSize => HeaderBytes;
}
