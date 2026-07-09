using System.Text;
using MightAndMagic1Trainer.Game;

namespace MightAndMagic1Trainer.Memory;

/// <summary>
/// Locates Might &amp; Magic 1's data segment (DGROUP) inside the attached emulator's
/// memory and exposes typed reads/writes at <em>DS-relative offsets</em> recovered from
/// <c>Mm.exe</c> with Ghidra (see <c>docs/offset-map.md</c>).
///
/// DS-relative offsets are fixed for this build of the EXE, so once the segment base is
/// found every global is reachable as <c>base + offset</c> — no per-value memory scan.
/// The base is found by anchoring on a unique static string whose DS offset is known,
/// then validated against a second string so a stray copy (e.g. the on-disk image DOSBox
/// may also hold in memory) can't masquerade as the live segment.
/// </summary>
public sealed class DataSegment
{
    // Anchor + validation strings, with the DS offset each sits at (docs/offset-map.md).
    private static readonly byte[] AnchorBytes = Encoding.ASCII.GetBytes("FOR DEFEATING THE MONSTERS");
    private const int AnchorDsOffset = 0x3062;
    private static readonly byte[] ValidateBytes = Encoding.ASCII.GetBytes("ROUND #:");
    private const int ValidateDsOffset = 0x3632;

    // --- Known DS-relative offsets (see docs/offset-map.md) ----------------------
    public const int OffMessagePointer = 0x3BD4;  // u16: offset of string being printed   [Confirmed]
    public const int OffCursorColumn = 0x3BDC;    // u8 : text cursor X                     [Confirmed]
    public const int OffCursorRow = 0x3BDD;       // u8 : text cursor Y                     [Confirmed]
    public const int OffCurrentCharPtr = 0x3BD6;  // u16: DS offset of the active character record [Confirmed]
    public const int OffActiveCharIndex = 0x3987; // u8 : index of the active character     [Confirmed]
    public const int OffCombatFlags = 0xC5DC;     // u8[]: per-char flags; bit 1 (&2) = in combat [Confirmed]
    public const int OffRngState = 0x3BCE;        // u32: the LFSR random-number state (NOT a combat flag)
    public const int OffRngRetry = 0x3BD3;        // u8 : per-rejection shift count for rand() (game inits it to 4)
    // NOTE: 0x3BCF was previously mislabelled a "combat flag" — it is byte 1 of the RNG
    // state at 0x3BCE..0x3BD1; FUN_1000_45b9 forces it nonzero so the LFSR can't stall.

    private readonly ProcessMemory _mem;

    /// <summary>Host address of <c>DS:0x0000</c> in the attached process.</summary>
    public nuint BaseAddress { get; }

    private DataSegment(ProcessMemory mem, nuint baseAddress)
    {
        _mem = mem;
        BaseAddress = baseAddress;
    }

    /// <summary>
    /// Finds and validates the data segment in <paramref name="mem"/>, or returns null if
    /// it can't be located (not attached to MM1, the game isn't loaded, or the anchor
    /// strings aren't present — e.g. a different EXE build).
    /// </summary>
    public static DataSegment? Locate(ProcessMemory mem, CancellationToken ct = default)
    {
        foreach (var anchorHit in FindAll(mem, AnchorBytes, ct))
        {
            if (anchorHit < (nuint)AnchorDsOffset) continue;
            nuint dsBase = anchorHit - (nuint)AnchorDsOffset;

            var chk = mem.Read(dsBase + (nuint)ValidateDsOffset, ValidateBytes.Length);
            if (chk.Length == ValidateBytes.Length && chk.AsSpan().SequenceEqual(ValidateBytes))
                return new DataSegment(mem, dsBase);
        }
        return null;
    }

    // --- typed accessors ---------------------------------------------------------
    public byte ReadByte(int dsOffset)
    {
        var b = _mem.Read(BaseAddress + (nuint)dsOffset, 1);
        return b.Length == 1 ? b[0] : (byte)0;
    }

    public ushort ReadU16(int dsOffset)
    {
        var b = _mem.Read(BaseAddress + (nuint)dsOffset, 2);
        return b.Length == 2 ? (ushort)(b[0] | (b[1] << 8)) : (ushort)0;
    }

    public uint ReadU32(int dsOffset)
    {
        var b = _mem.Read(BaseAddress + (nuint)dsOffset, 4);
        return b.Length == 4 ? DecodeU32(b) : 0u;
    }

    private static uint DecodeU32(byte[] b) => (uint)(b[0] | (b[1] << 8) | (b[2] << 16) | (b[3] << 24));

    public bool WriteByte(int dsOffset, byte value) =>
        _mem.Write(BaseAddress + (nuint)dsOffset, new[] { value });

    public bool WriteU16(int dsOffset, ushort value) =>
        _mem.Write(BaseAddress + (nuint)dsOffset, new[] { (byte)(value & 0xFF), (byte)(value >> 8) });

    // --- named convenience members (the confirmed ones) --------------------------
    /// <summary>Offset of the string the game is currently printing (DS:0x3BD4).</summary>
    public ushort MessagePointer => ReadU16(OffMessagePointer);
    /// <summary>Text cursor column (DS:0x3BDC).</summary>
    public byte CursorColumn => ReadByte(OffCursorColumn);
    /// <summary>Text cursor row (DS:0x3BDD).</summary>
    public byte CursorRow => ReadByte(OffCursorRow);
    /// <summary>True when the party is in combat, using the game's own gate test:
    /// <c>[0xC5DC + activeCharIndex] &amp; 2</c> (the check that rejects non-combat-only
    /// actions). Reverse-engineered from <c>Mm.exe</c>; watch the game-state readout to
    /// confirm it tracks combat before trusting it unattended.</summary>
    public bool InCombat
    {
        get
        {
            int idx = ReadByte(OffActiveCharIndex);
            // The flags array has one byte per roster slot; a transient garbage index would
            // otherwise read unrelated DS memory and could spuriously arm auto-fight.
            if (idx >= RosterFormat.MaxSlots) return false;
            return (ReadByte(OffCombatFlags + idx) & 0x02) != 0;
        }
    }

    /// <summary>DS offset of the active character's record, as the game tracks it (DS:0x3BD6).</summary>
    public ushort CurrentCharRecordOffset => ReadU16(OffCurrentCharPtr);

    /// <summary>The live 32-bit RNG (LFSR) state at DS:0x3BCE, or null if unreadable.</summary>
    public uint? ReadRngState()
    {
        var b = _mem.Read(BaseAddress + (nuint)OffRngState, 4);
        return b.Length == 4 ? DecodeU32(b) : (uint?)null;
    }

    /// <summary>The rand() per-rejection shift count (DS:0x3BD3; the game initialises it to 4).</summary>
    public byte RngRetry => ReadByte(OffRngRetry);

    /// <summary>
    /// Reads the 127 bytes of the character record the game currently has selected (via the
    /// pointer at DS:0x3BD6), or null if that pointer doesn't land inside the data segment.
    /// The bytes follow <see cref="RosterFormat"/>, so existing parsing applies unchanged.
    /// </summary>
    public byte[]? ReadCurrentCharacterRecord()
    {
        ushort recOff = CurrentCharRecordOffset;
        if (recOff == 0) return null;
        var rec = _mem.Read(BaseAddress + recOff, RosterFormat.RecordSize);
        return rec.Length == RosterFormat.RecordSize ? rec : null;
    }

    // --- region scan for the anchor bytes ---------------------------------------
    private static IEnumerable<nuint> FindAll(ProcessMemory mem, byte[] needle, CancellationToken ct)
    {
        const int chunk = 1 << 20;
        byte[] buf = new byte[chunk];
        foreach (var region in mem.EnumerateRegions())
        {
            ct.ThrowIfCancellationRequested();
            for (nuint offset = 0; offset < region.Size;)
            {
                int want = (int)Math.Min((nuint)chunk, region.Size - offset);
                int read = mem.Read(region.Base + offset, buf, want);
                if (read < needle.Length) break;

                for (int i = 0; i + needle.Length <= read; i++)
                    if (Matches(buf, i, needle))
                        yield return region.Base + offset + (nuint)i;

                // Overlap by needle length so a match straddling a chunk edge is still seen.
                // Advance by the bytes actually read (not requested): on a short read the tail
                // [read, want) was never scanned, so keying off `want` could skip past it.
                nuint advance = (nuint)Math.Max(1, read - needle.Length);
                offset += advance;
            }
        }
    }

    private static bool Matches(byte[] buf, int i, byte[] needle)
    {
        for (int k = 0; k < needle.Length; k++)
            if (buf[i + k] != needle[k]) return false;
        return true;
    }
}
