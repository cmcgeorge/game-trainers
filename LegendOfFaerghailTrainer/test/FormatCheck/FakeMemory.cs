using LegendOfFaerghailTrainer.Game;
using LegendOfFaerghailTrainer.Memory;
using LegendOfFaerghailTrainer.ViewModels;
using System.Text;

namespace LegendOfFaerghailTrainer.FormatCheck;

/// <summary>
/// A synthetic address space that mimics DOSBox closely enough to drive <see cref="GameLocator"/>
/// with no emulator and no copyrighted game files present: one large private commit holding a
/// padded guest, a BIOS data area, a data group with the anchor literals at their real offsets,
/// and the two far pointers the game keeps to its party and roster arrays.
/// </summary>
public sealed class FakeGuest : IMemorySource
{
    private readonly List<(nuint Base, byte[] Data)> _regions = new();

    /// <summary>Bytes between the region base and guest linear 0 (DOSBox pads its allocation).</summary>
    public int GuestPad { get; }

    public nuint RegionBase { get; }
    public byte[] Guest { get; }

    /// <summary>Pages that fail to read, as guest-linear page-aligned offsets.</summary>
    public HashSet<long> PoisonedPages { get; } = new();

    public FakeGuest(nuint regionBase = 0x10000000, int guestBytes = 16 << 20, int guestPad = 0x20)
    {
        RegionBase = regionBase;
        GuestPad = guestPad;
        Guest = new byte[guestPad + guestBytes];
        _regions.Add((regionBase, Guest));
    }

    /// <summary>Adds a decoy region so the locator has to skip something before it finds the guest.</summary>
    public void AddDecoy(nuint baseAddress, int size) => _regions.Insert(0, (baseAddress, new byte[size]));

    public nuint HostOf(long guestLinear) => RegionBase + (nuint)(GuestPad + guestLinear);

    public void Write(long guestLinear, params byte[] bytes) =>
        Array.Copy(bytes, 0, Guest, GuestPad + guestLinear, bytes.Length);

    public void WriteAscii(long guestLinear, string text) =>
        Write(guestLinear, Encoding.ASCII.GetBytes(text));

    /// <summary>Emulated BIOS data area: COM1 port at 40:0000 and the 640 KB size word at 40:0013.</summary>
    public void WriteBios()
    {
        Write(0x400, 0xF8, 0x03);
        Write(0x413, 0x80, 0x02);
    }

    /// <summary>Lays the anchor literals into the data group at their real DGROUP offsets.</summary>
    public void WriteDgroup(long dgroupGuestLinear, int validators = 4)
    {
        WriteAscii(dgroupGuestLinear + GameFacts.PrimaryAnchorOffset, GameFacts.PrimaryAnchorText);
        int n = 0;
        foreach (var (text, offset) in GameFacts.SecondaryAnchors)
        {
            if (n++ >= validators) break;
            WriteAscii(dgroupGuestLinear + offset, text);
        }
    }

    /// <summary>
    /// Writes a DOS far pointer (offset word then segment word) resolving to a guest address.
    ///
    /// Throws rather than truncating an unreachable target. Silently narrowing the segment to 16
    /// bits turns any address at or above 1 MB into the far pointer <c>0000:0000</c>, which the
    /// locator rejects on its null-pointer branch — so a fixture meant to test "the pointer resolves
    /// to junk" would quietly become a second copy of the null-pointer test and the code it was
    /// written to cover would never run.
    /// </summary>
    public void WriteFarPointer(long at, long targetGuestLinear)
    {
        long seg = targetGuestLinear >> 4;
        if (seg > 0xFFFF)
            throw new ArgumentOutOfRangeException(nameof(targetGuestLinear),
                $"0x{targetGuestLinear:X} is out of reach of a real-mode far pointer (segment 0x{seg:X} > 0xFFFF).");
        int off = (int)(targetGuestLinear - (seg << 4));
        Write(at, (byte)(off & 0xFF), (byte)(off >> 8), (byte)((int)seg & 0xFF), (byte)((int)seg >> 8));
    }

    // --- IMemorySource ----------------------------------------------------------

    public IEnumerable<MemoryRegion> EnumerateRegions()
    {
        foreach (var (b, d) in _regions)
            yield return new MemoryRegion(b, (nuint)d.Length);
    }

    public int Read(nuint address, byte[] buffer, int count)
    {
        foreach (var (b, d) in _regions)
        {
            if (address < b || address >= b + (nuint)d.Length) continue;
            int off = (int)(address - b);
            int n = Math.Min(count, d.Length - off);
            if (n <= 0) return 0;

            // A poisoned page truncates the read at its start, the way an unreadable page does.
            if (ReferenceEquals(d, Guest))
            {
                foreach (long page in PoisonedPages)
                {
                    long pageStart = GuestPad + page;
                    if (pageStart >= off && pageStart < off + n) n = (int)(pageStart - off);
                }
                if (n <= 0) return 0;
            }

            Array.Copy(d, off, buffer, 0, Math.Min(n, buffer.Length));
            return Math.Min(n, buffer.Length);
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
}

/// <summary>
/// An <see cref="ICharacterHost"/> that records every write instead of performing one, so the
/// view-model write paths can be asserted headlessly: not just "the value changed" but "exactly
/// these byte ranges were sent to the game, and no others".
/// </summary>
public sealed class RecordingHost : ICharacterHost
{
    public List<(nuint Address, int Offset, int Length, byte[] Bytes)> Writes { get; } = new();

    public bool IsAttached => true;

    public bool WriteBytes(nuint recordAddress, byte[] source, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > source.Length - length)
            throw new ArgumentOutOfRangeException(nameof(length),
                $"write of {length} bytes at +0x{offset:X} is outside the {source.Length}-byte record");
        var slice = new byte[length];
        Array.Copy(source, offset, slice, 0, length);
        Writes.Add((recordAddress, offset, length, slice));
        return true;
    }

    public void Clear() => Writes.Clear();

    /// <summary>True if exactly one write was recorded, covering that range.</summary>
    public bool WroteOnly(int offset, int length) =>
        Writes.Count == 1 && Writes[0].Offset == offset && Writes[0].Length == length;

    public bool Wrote(int offset, int length) =>
        Writes.Any(w => w.Offset == offset && w.Length == length);
}

/// <summary>Builds plausible character records for the fixtures.</summary>
public static class FakeRecord
{
    public static byte[] Make(string name, int level = 1, int race = 0, int cls = 0,
        int maxHp = 12, int curHp = 12, long gold = 25, long xp = 30)
    {
        var rec = new CharacterRecord();
        rec.Occupied = true;
        rec.Name = name;
        rec.Level = level;
        rec.Race = race;
        rec.Class = cls;
        rec.Sex = 1;
        rec.Alignment = 0;
        rec.Status = 0;
        rec.MaxHp = maxHp;
        rec.CurHp = curHp;
        rec.Gold = gold;
        rec.Experience = xp;
        rec.Rations = 4;
        rec.MaxWeight = 500;
        rec.Bytes[CharacterFormat.OffCurWeight] = 0x2C;      // 30.0 lb
        rec.Bytes[CharacterFormat.OffCurWeight + 1] = 0x01;
        rec.SetLanguage(0, true);
        rec.SetItem(0, 27, true, 100);
        rec.SetSpell(0, 1, 8);
        return rec.Bytes;
    }
}
