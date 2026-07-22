namespace ImperialismIITrainer.Game;

/// <summary>The located human-player nation object and the addresses derived from it.</summary>
public sealed class NationLocation
{
    /// <summary>Heap base of the player's nation object (session-specific).</summary>
    public nuint ObjectBase { get; }

    /// <summary>Treasury value read at location time (signed int32).</summary>
    public long Treasury { get; }

    public NationLocation(nuint objectBase, long treasury)
    {
        ObjectBase = objectBase;
        Treasury = treasury;
    }

    /// <summary>Absolute address of the treasury field (int32).</summary>
    public nuint TreasuryAddress => ObjectBase + (nuint)NationLayout.TreasuryOffset;

    /// <summary>Absolute address of a warehouse commodity slot (int16).</summary>
    public nuint SlotAddress(int offset) => ObjectBase + (nuint)offset;
}

/// <summary>
/// Auto-locates the human player's nation object in a running Imperialism II process by following a
/// static-global pointer (no ASLR → the global's address is constant every launch) to the heap object,
/// then validating it structurally. This is the "no scan" path: attach and the treasury and warehouse
/// resolve on their own. If nothing validates (e.g. a different game build), <see cref="Locate"/>
/// returns null and the caller falls back to the value scanner.
/// </summary>
public sealed class GameLocator
{
    private readonly ProcessMemory _mem;

    public GameLocator(ProcessMemory mem) => _mem = mem;

    /// <summary>Finds the player nation object, or null if it can't be validated.</summary>
    public NationLocation? Locate()
    {
        // 1) Fast path: the known static globals that hold the player-nation pointer.
        foreach (uint g in NationLayout.PlayerNationGlobals)
            if (TryResolveGlobal(g, out var loc))
                return loc;

        // 2) Fallback: scan the whole static .data/.bss region for any pointer to a valid nation object.
        //    (Covers a build whose globals shifted but whose object layout still matches.)
        byte[] data = _mem.Read((nuint)NationLayout.DataStart, (int)(NationLayout.DataEnd - NationLayout.DataStart));
        for (int i = 0; i + 4 <= data.Length; i += 4)
        {
            uint p = BitConverter.ToUInt32(data, i);
            if (NationLayout.LooksLikeHeapPointer(p) && TryValidateObject((nuint)p, out var loc))
                return loc;
        }
        return null;
    }

    private bool TryResolveGlobal(uint globalAddr, out NationLocation loc)
    {
        loc = null!;
        byte[] pb = _mem.Read((nuint)globalAddr, 4);
        if (pb.Length < 4) return false;
        uint p = BitConverter.ToUInt32(pb, 0);
        return NationLayout.LooksLikeHeapPointer(p) && TryValidateObject((nuint)p, out loc);
    }

    private bool TryValidateObject(nuint obj, out NationLocation loc)
    {
        loc = null!;
        byte[] hdr = _mem.Read(obj, NationLayout.HeaderBytes);
        if (!NationLayout.ValidateHeader(hdr)) return false;
        loc = new NationLocation(obj, BitConverter.ToInt32(hdr, NationLayout.TreasuryOffset));
        return true;
    }
}
