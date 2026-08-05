namespace TheQuestTrainer.Game;

/// <summary>One PE section as it is mapped into the running process.</summary>
public readonly record struct PeSection(string Name, uint Rva, uint VirtualSize, uint Characteristics)
{
    private const uint MemExecute = 0x20000000;
    private const uint MemWrite = 0x80000000;

    /// <summary>Whether an RVA falls inside this section.</summary>
    public bool ContainsRva(uint rva) => rva >= Rva && rva < (long)Rva + VirtualSize;

    /// <summary>
    /// Whether this section can hold mutable globals — writable and not executable. Tested by
    /// characteristics rather than by name so a rebuild that renames <c>.data</c> still works.
    /// </summary>
    public bool IsWritableData => (Characteristics & MemWrite) != 0 && (Characteristics & MemExecute) == 0;
}

/// <summary>
/// The parts of a mapped PE header the trainer needs: the build fingerprint and the section table.
///
/// Reading the header out of the target rather than baking section ranges into constants keeps the
/// repo's rule that addresses are discovered at run time. Here it earns its keep twice over:
/// <c>TheQuest.exe</c> sets DYNAMICBASE, so the preferred image base in the header is <i>not</i>
/// where the module is, and the static game-object slot is only trusted when its RVA actually lands
/// in a writable data section of the image that is really mapped.
/// </summary>
public sealed class PeImage
{
    private const int MzSignature = 0x5A4D;         // "MZ"
    private const uint PeSignature = 0x00004550;    // "PE\0\0"
    private const ushort Pe32Magic = 0x010B;
    private const int SectionEntrySize = 40;

    /// <summary>Bytes of the mapped image the header parser needs; the whole header lives in page one.</summary>
    public const int HeaderBytes = 0x1000;

    /// <summary>Machine type from the COFF header (0x014C = i386).</summary>
    public ushort Machine { get; private init; }

    /// <summary>COFF characteristics — bit 0x2000 marks a DLL rather than an executable.</summary>
    public ushort Characteristics { get; private init; }

    /// <summary>Whether this image is a DLL. Used to pick the executable out of a module sweep.</summary>
    public bool IsDll => (Characteristics & 0x2000) != 0;

    /// <summary>Link timestamp — the cheapest way to tell two builds of the game apart.</summary>
    public uint TimeDateStamp { get; private init; }

    /// <summary>Preferred load address from the optional header. The game is relocated away from it.</summary>
    public uint ImageBase { get; private init; }

    /// <summary>Total mapped size of the image.</summary>
    public uint SizeOfImage { get; private init; }

    /// <summary>DLL characteristics — bit 0x0040 is DYNAMICBASE (ASLR).</summary>
    public ushort DllCharacteristics { get; private init; }

    /// <summary>Sections in file order.</summary>
    public IReadOnlyList<PeSection> Sections { get; private init; } = Array.Empty<PeSection>();

    /// <summary>Whether the image opted in to ASLR. The game does, which is why nothing is hard-coded.</summary>
    public bool HasAslr => (DllCharacteristics & 0x0040) != 0;

    /// <summary>Whether this is the 32-bit x86 image every offset in <see cref="QuestLayout"/> assumes.</summary>
    public bool IsWin32X86 => Machine == 0x014C;

    /// <summary>Finds a section by name (e.g. ".data"), or null.</summary>
    public PeSection? Section(string name)
    {
        foreach (var s in Sections)
            if (s.Name == name) return s;
        return null;
    }

    /// <summary>Whether <paramref name="rva"/> lands inside a writable, non-executable section.</summary>
    public bool IsWritableDataRva(uint rva)
    {
        foreach (var s in Sections)
            if (s.IsWritableData && s.ContainsRva(rva)) return true;
        return false;
    }

    /// <summary>
    /// Whether <paramref name="rva"/> lands inside a section that can plausibly hold a vtable:
    /// initialised, not writable at run time. The character record's first dword is such a pointer,
    /// and checking it is what stops a run of look-alike integers from being read as a character.
    /// </summary>
    public bool IsReadOnlyDataRva(uint rva)
    {
        foreach (var s in Sections)
            if (!s.IsWritableData && s.ContainsRva(rva)) return true;
        return false;
    }

    /// <summary>
    /// Parses the header at <paramref name="header"/>, which must be the first
    /// <see cref="HeaderBytes"/> bytes of the mapped image. Returns null if it is not a 32-bit PE.
    /// </summary>
    public static PeImage? Parse(ReadOnlySpan<byte> header)
    {
        if (header.Length < 0x40) return null;
        if (BitConverter.ToUInt16(header[..2]) != MzSignature) return null;

        // Every bound below is phrased as a subtraction rather than an addition. `Parse` is handed
        // arbitrary target memory by ModuleResolver's header sweep — any 64 KB-aligned page whose
        // first two bytes happen to be "MZ" — so the offset at +0x3C is an arbitrary int, and
        // `peOffset + 0x78 > header.Length` would wrap negative near int.MaxValue, pass the guard,
        // and throw out of Slice.
        int peOffset = BitConverter.ToInt32(header.Slice(0x3C, 4));
        if (peOffset <= 0 || peOffset > header.Length - 0x78) return null;
        if (BitConverter.ToUInt32(header.Slice(peOffset, 4)) != PeSignature) return null;

        int coff = peOffset + 4;
        ushort machine = BitConverter.ToUInt16(header.Slice(coff, 2));
        ushort sectionCount = BitConverter.ToUInt16(header.Slice(coff + 2, 2));
        uint stamp = BitConverter.ToUInt32(header.Slice(coff + 4, 4));
        ushort optSize = BitConverter.ToUInt16(header.Slice(coff + 16, 2));
        ushort characteristics = BitConverter.ToUInt16(header.Slice(coff + 18, 2));

        int opt = coff + 20;
        if (opt > header.Length - 2) return null;
        if (BitConverter.ToUInt16(header.Slice(opt, 2)) != Pe32Magic) return null;   // PE32 only
        if (opt > header.Length - 72) return null;      // DllCharacteristics is a ushort at +70

        uint imageBase = BitConverter.ToUInt32(header.Slice(opt + 28, 4));
        uint sizeOfImage = BitConverter.ToUInt32(header.Slice(opt + 56, 4));
        ushort dllChars = BitConverter.ToUInt16(header.Slice(opt + 70, 2));

        int table = opt + optSize;
        var sections = new List<PeSection>(sectionCount);
        for (int i = 0; i < sectionCount; i++)
        {
            int e = table + i * SectionEntrySize;
            if (e + SectionEntrySize > header.Length) break;
            string name = System.Text.Encoding.ASCII.GetString(header.Slice(e, 8)).TrimEnd('\0');
            uint vsize = BitConverter.ToUInt32(header.Slice(e + 8, 4));
            uint rva = BitConverter.ToUInt32(header.Slice(e + 12, 4));
            uint chars = BitConverter.ToUInt32(header.Slice(e + 36, 4));
            sections.Add(new PeSection(name, rva, vsize, chars));
        }

        return new PeImage
        {
            Machine = machine,
            Characteristics = characteristics,
            TimeDateStamp = stamp,
            ImageBase = imageBase,
            SizeOfImage = sizeOfImage,
            DllCharacteristics = dllChars,
            Sections = sections,
        };
    }
}
