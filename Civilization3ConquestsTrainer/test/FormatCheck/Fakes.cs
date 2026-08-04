using Civilization3ConquestsTrainer.Game;
using Civilization3ConquestsTrainer.Memory;
using Civilization3ConquestsTrainer.ViewModels;
using GameTrainers.Common.Memory;

namespace FormatCheck;

/// <summary>
/// A synthetic 32-bit module: one flat byte array behind an <see cref="IMemorySource"/>, with a
/// hand-built PE header and whatever game structures a check wants to plant in it.
///
/// This is what lets the locator be tested with no game running — and, more usefully, tested against
/// situations a real game will not produce on demand: a module mapped somewhere other than 0x400000,
/// a leader array with one slot corrupted, an image whose globals moved so only the signature chain
/// can find them, and reads that fall off the end of the image.
/// </summary>
public sealed class FakeModule : IMemorySource
{
    private readonly byte[] _image;

    public nuint ModuleBase { get; }
    public int ModuleSize => _image.Length;

    public FakeModule(nuint moduleBase, int size)
    {
        ModuleBase = moduleBase;
        _image = new byte[size];
    }

    public byte[] Read(nuint address, int count)
    {
        if (!TryOffset(address, count, out int offset)) return Array.Empty<byte>();
        return _image.AsSpan(offset, count).ToArray();
    }

    public int Read(nuint address, byte[] buffer, int count)
    {
        if (!TryOffset(address, count, out int offset)) return 0;
        _image.AsSpan(offset, count).CopyTo(buffer);
        return count;
    }

    private bool TryOffset(nuint address, int count, out int offset)
    {
        offset = 0;
        if (count <= 0 || address < ModuleBase) return false;
        nuint delta = address - ModuleBase;
        if (delta > int.MaxValue) return false;
        offset = (int)delta;
        return offset + count <= _image.Length;
    }

    // --- planting ---------------------------------------------------------------------------------

    public void PutInt32(uint rva, int value) => BitConverter.TryWriteBytes(_image.AsSpan((int)rva, 4), value);
    public void PutUInt32(uint rva, uint value) => BitConverter.TryWriteBytes(_image.AsSpan((int)rva, 4), value);
    public void PutInt16(uint rva, short value) => BitConverter.TryWriteBytes(_image.AsSpan((int)rva, 2), value);
    public void PutByte(uint rva, byte value) => _image[rva] = value;
    public void PutBytes(uint rva, params byte[] bytes) => bytes.CopyTo(_image, rva);

    /// <summary>Absolute address of an RVA in this fake module.</summary>
    public nuint At(uint rva) => ModuleBase + (nuint)rva;

    // --- a believable PE header --------------------------------------------------------------------

    public const uint TextRva = 0x1000, TextSize = 0x280000;
    public const uint RdataRva = 0x282000, RdataSize = 0x1B000;
    public const uint DataRva = 0x29D000, DataSize = 0x656000;

    /// <summary>Writes an MZ/PE32 header describing a Civ3-shaped image with three sections.</summary>
    public void WritePeHeader(uint timeDateStamp, uint imageBase = 0x400000, ushort machine = 0x014C)
    {
        const int peOff = 0x80;
        const int optSize = 0xE0;

        PutInt16(0, 0x5A4D);                       // "MZ"
        PutInt32(0x3C, peOff);
        PutUInt32(peOff, 0x00004550);              // "PE\0\0"

        int coff = peOff + 4;
        PutInt16((uint)coff, unchecked((short)machine));
        PutInt16((uint)(coff + 2), 3);             // three sections
        PutUInt32((uint)(coff + 4), timeDateStamp);
        PutInt16((uint)(coff + 16), optSize);

        int opt = coff + 20;
        PutInt16((uint)opt, 0x010B);               // PE32
        PutUInt32((uint)(opt + 0x1C), imageBase);
        PutUInt32((uint)(opt + 0x38), (uint)_image.Length);
        PutInt16((uint)(opt + 0x46), 0);           // no DYNAMICBASE

        int table = opt + optSize;
        WriteSection(table, ".text", TextRva, TextSize, TextCharacteristics);
        WriteSection(table + 40, ".rdata", RdataRva, RdataSize, RdataCharacteristics);
        WriteSection(table + 80, ".data", DataRva, DataSize, DataCharacteristics);
    }

    // The real section flags, because the locator now decides what can hold globals from these
    // rather than from the section name.
    private const uint TextCharacteristics = 0x60000020;    // CNT_CODE | MEM_EXECUTE | MEM_READ
    private const uint RdataCharacteristics = 0x40000040;   // CNT_INITIALIZED_DATA | MEM_READ
    private const uint DataCharacteristics = 0xC0000040;    // CNT_INITIALIZED_DATA | MEM_READ | MEM_WRITE

    private void WriteSection(int at, string name, uint rva, uint size, uint characteristics)
    {
        foreach (var (b, i) in System.Text.Encoding.ASCII.GetBytes(name).Select((b, i) => (b, i)))
            _image[at + i] = b;
        PutUInt32((uint)(at + 8), size);
        PutUInt32((uint)(at + 12), rva);
        PutUInt32((uint)(at + 36), characteristics);
    }

    // --- a believable game state --------------------------------------------------------------------

    /// <summary>A vtable address inside the fake .rdata, so leader validation passes.</summary>
    public uint Vtable => (uint)ModuleBase + RdataRva + 0x100;

    /// <summary>
    /// Plants 32 valid leader records at <paramref name="leadersRva"/>, plus the globals the locator
    /// reads (human civ id, player bitmasks) and a self-consistent map header.
    /// </summary>
    public void PlantGame(uint leadersRva, int humanCivId = 1, int playerCount = 8, int stride = 0)
    {
        if (stride <= 0) stride = Civ3Layout.LeaderStride;
        for (int i = 0; i < GameFacts.MaxPlayers; i++)
            PlantLeader(leadersRva + (uint)(i * stride), i, gold: 100 + i * 7);

        uint bits = 0;
        for (int i = 0; i < playerCount; i++) bits |= 1u << i;
        PutUInt32(Civ3Layout.RvaPlayerBits, bits);
        PutUInt32(Civ3Layout.RvaHumanPlayerBits, 1u << humanCivId);
        PutInt32(Civ3Layout.RvaMainScreenForm + Civ3Layout.MainScreenPlayerCivId, humanCivId);

        uint map = Civ3Layout.RvaBicData + Civ3Layout.BicMap;
        PutInt32(map + Civ3Layout.MapWidth, 100);
        PutInt32(map + Civ3Layout.MapHeight, 80);
        PutInt32(map + Civ3Layout.MapTileCount, 100 * 80 / 2);
    }

    /// <summary>Plants one valid leader record.</summary>
    public void PlantLeader(uint rva, int slot, long gold)
    {
        int decrement = -12345 - slot;
        Civ3Layout.TryEncodeGold(gold, decrement, out int encoded);

        PutUInt32(rva, Vtable);
        PutUInt32(rva + Civ3Layout.BaseClassNameOffset, Civ3Layout.TagLead);
        PutInt32(rva + (uint)Civ3Layout.LeaderId, slot);
        PutInt32(rva + (uint)Civ3Layout.LeaderRaceId, slot == 0 ? 0 : slot);
        PutInt32(rva + (uint)Civ3Layout.LeaderGoldDecrement, decrement);
        PutInt32(rva + (uint)Civ3Layout.LeaderGoldEncoded, encoded);
        PutInt32(rva + (uint)Civ3Layout.LeaderEra, 0);
        PutInt32(rva + (uint)Civ3Layout.LeaderLuxurySlider, 0);
        PutInt32(rva + (uint)Civ3Layout.LeaderScienceSlider, 6);
        PutInt32(rva + (uint)Civ3Layout.LeaderGoldSlider, 4);
        PutInt32(rva + (uint)Civ3Layout.LeaderCitiesCount, 3);
        PutInt32(rva + (uint)Civ3Layout.LeaderUnitCount, 9);
        PutUInt32(rva + (uint)(Civ3Layout.LeaderCulture + Civ3Layout.BaseClassNameOffset), Civ3Layout.TagCult);
        PutInt32(rva + (uint)(Civ3Layout.LeaderCulture + Civ3Layout.CultureCivId), slot);
        PutInt32(rva + (uint)(Civ3Layout.LeaderCulture + Civ3Layout.CultureLevel), 2);
        PutInt32(rva + (uint)(Civ3Layout.LeaderCulture + Civ3Layout.CultureTotalAccumulated), 500 + slot);
    }

    /// <summary>
    /// Plants a BIC rules database with <paramref name="raceCount"/> races at <paramref name="stride"/>,
    /// so <see cref="GameTables"/>'s stride recovery can be exercised without a game.
    /// </summary>
    public void PlantRaces(uint tableRva, int raceCount, int stride)
    {
        PutInt32(Civ3Layout.RvaBicData + (uint)Civ3Layout.BicRacesCount, raceCount);
        PutUInt32(Civ3Layout.RvaBicData + (uint)Civ3Layout.BicRaces, (uint)At(tableRva));
        for (int i = 0; i < raceCount; i++)
        {
            uint r = tableRva + (uint)(i * stride);
            PutInt32(r + (uint)Civ3Layout.RaceId, i);
            foreach (var (b, j) in System.Text.Encoding.ASCII.GetBytes($"Country{i}").Select((b, j) => (b, j)))
                _image[r + Civ3Layout.RaceCountryName + j] = b;
            foreach (var (b, j) in System.Text.Encoding.ASCII.GetBytes($"Leader{i}").Select((b, j) => (b, j)))
                _image[r + Civ3Layout.RaceLeaderName + j] = b;
        }
    }

    /// <summary>
    /// Plants the compiler's array-walk idiom in .text so the signature chain can re-derive the
    /// leader array: <c>add ebp, stride</c> immediately followed by <c>cmp ebp, one-past-end</c>.
    /// </summary>
    public void PlantArrayWalk(uint codeRva, uint leadersAbsolute, int stride)
    {
        uint end = leadersAbsolute + (uint)(GameFacts.MaxPlayers * stride);
        PutBytes(codeRva, 0x81, 0xC5);
        PutInt32(codeRva + 2, stride);
        PutBytes(codeRva + 6, 0x43);                 // inc ebx
        PutBytes(codeRva + 7, 0x81, 0xFD);
        PutUInt32(codeRva + 9, end);
    }
}

/// <summary>Records what a row view-model tried to write, so the harness can assert on it.</summary>
public sealed class FakeGameHost : IGameHost
{
    private readonly Dictionary<nuint, int> _cells = new();

    public bool WritesAllowed { get; set; } = true;
    public List<(nuint Address, int Value)> Writes { get; } = new();
    public string LastReport { get; private set; } = "";

    /// <summary>When true every read fails, so the rows' short-read guards become reachable.</summary>
    public bool FailReads { get; set; }

    public void Seed(nuint address, int value) => _cells[address] = value;

    public byte[] Read(nuint address, int count)
    {
        if (FailReads) return Array.Empty<byte>();
        var buf = new byte[count];
        for (int i = 0; i + 4 <= count; i += 4)
            if (_cells.TryGetValue(address + (nuint)i, out int v))
                BitConverter.TryWriteBytes(buf.AsSpan(i, 4), v);
        return buf;
    }

    public bool ReadInt32(nuint address, out int value)
    {
        value = 0;
        return !FailReads && _cells.TryGetValue(address, out value);
    }

    public bool WriteInt32(nuint address, int value)
    {
        if (!WritesAllowed) return false;
        _cells[address] = value;
        Writes.Add((address, value));
        return true;
    }

    public void Report(string message) => LastReport = message;
}

/// <summary>Captures scanner writes without touching a process.</summary>
public sealed class FakeScanHost : IScanHost
{
    private readonly Dictionary<nuint, long> _cells = new();

    public bool AllowWrites { get; set; } = true;
    public int WriteCount { get; private set; }
    public int FailureReports { get; private set; }

    public bool Write(nuint address, long value, ScanWidth width)
    {
        if (!AllowWrites) return false;
        _cells[address] = value;
        WriteCount++;
        return true;
    }

    public bool Read(nuint address, ScanWidth width, out long value) => _cells.TryGetValue(address, out value);

    public void ReportWriteFailure(nuint address) => FailureReports++;
}
