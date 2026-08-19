using System.Diagnostics;
using GameTrainers.Common.Memory;
using Roadwar2000Trainer.Game;

namespace Roadwar2000Trainer.Memory;

/// <summary>Where the game's data segment was found, and what it cost to find it.</summary>
public sealed record LocateResult(
    nuint DataSegmentHost,
    nuint SlabHost,
    int CandidatesSeen,
    long ElapsedMilliseconds,
    string Detail);

/// <summary>
/// Finds Roadwar 2000's data segment inside a running DOSBox without any value searching.
/// <para>
/// The anchor is the vehicle-type name block -- <c>"MOTORCYCLE\0SIDECAR\0COMPACT CONVERTIBLE\0"</c>
/// -- which START.EXE's initialised data places at <c>DS:0x2254</c> in every build and every
/// save. Subtracting that offset from a hit gives a candidate <c>DS:0000</c>.
/// </para>
/// <para>
/// A hit on the string is <b>not</b> enough on its own, and this was learned the hard way: while
/// an overlay is being paged in, a second copy of the same bytes is briefly present in the
/// emulator's RAM, and a write aimed at it lands nowhere the game will ever read. So every
/// candidate has to also satisfy the pointer table that indexes those names -- 19 words at
/// <c>DS:0x2366</c> holding absolute data-segment offsets, starting 0x2254, 0x225F, 0x2267 --
/// and carry a vehicle-type table whose first record is the motorcycle. Only a real data
/// segment has all three in the right places relative to one another.
/// </para>
/// </summary>
public sealed class GameLocator
{
    /// <summary>Process names worth looking at, in the order they are tried.</summary>
    public static readonly IReadOnlyList<string> EmulatorProcessNames = new[]
    {
        "DOSBox", "dosbox", "DOSBox-X", "dosbox-x", "DOSBox-notX", "DOSBox-X-SDL2", "dosbox-staging",
    };

    private static readonly byte[] Anchor =
        System.Text.Encoding.ASCII.GetBytes("MOTORCYCLE\0SIDECAR\0COMPACT CONVERTIBLE\0");

    /// <summary>Every running process that looks like a DOS emulator.</summary>
    public static IReadOnlyList<Process> FindEmulators()
    {
        var found = new List<Process>();
        foreach (var p in Process.GetProcesses())
        {
            bool match = false;
            foreach (var n in EmulatorProcessNames)
                if (p.ProcessName.Equals(n, StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessName.StartsWith("dosbox", StringComparison.OrdinalIgnoreCase))
                { match = true; break; }
            if (match) found.Add(p); else p.Dispose();
        }
        return found;
    }

    /// <summary>
    /// Scans the emulator for the data segment. Returns null when the game is not loaded yet --
    /// which, because the anchor lives in initialised data, means as soon as START.EXE is running
    /// the answer is available, whether or not a game has been started.
    /// </summary>
    public LocateResult? Locate(ProcessMemory memory, CancellationToken cancel = default)
    {
        var sw = Stopwatch.StartNew();
        var accepted = new List<nuint>();
        int candidates = 0;

        var buffer = new byte[1 << 20];
        foreach (var region in memory.EnumerateRegions())
        {
            cancel.ThrowIfCancellationRequested();

            // DOSBox holds guest RAM in one large allocation; skip the small housekeeping regions.
            if (region.Size < 0x10000) continue;

            nuint offset = 0;
            while (offset < region.Size)
            {
                cancel.ThrowIfCancellationRequested();
                int want = (int)Math.Min((ulong)buffer.Length, (ulong)(region.Size - offset));
                int read = memory.Read(region.Base + offset, buffer, want);
                if (read <= 0) break;

                for (int i = 0; i + Anchor.Length <= read; i++)
                {
                    if (buffer[i] != Anchor[0]) continue;
                    bool hit = true;
                    for (int j = 1; j < Anchor.Length; j++)
                        if (buffer[i + j] != Anchor[j]) { hit = false; break; }
                    if (!hit) continue;

                    candidates++;
                    nuint nameBlock = region.Base + offset + (nuint)i;
                    if (nameBlock < SaveFormat.DsVehicleNames) continue;
                    nuint ds = nameBlock - SaveFormat.DsVehicleNames;
                    if (Validate(memory, ds)) accepted.Add(ds);
                }

                // Overlap by the anchor length so a match spanning two buffers is not missed.
                if (read < want) break;
                offset += (nuint)Math.Max(1, read - Anchor.Length);
            }
        }

        sw.Stop();
        if (accepted.Count == 0) return null;

        // If a transient overlay copy did somehow validate, the lowest address is the real data
        // segment: DOSBox lays guest RAM out linearly from its base, and the program image sits
        // below any buffer the loader is filling.
        accepted.Sort();
        nuint chosen = accepted[0];
        string detail = accepted.Count == 1
            ? $"1 validated candidate from {candidates} anchor hit(s)"
            : $"{accepted.Count} validated candidates from {candidates} anchor hit(s); took the lowest";

        return new LocateResult(chosen, chosen + SaveFormat.DsBase, candidates, sw.ElapsedMilliseconds, detail);
    }

    /// <summary>
    /// The three-part check described on the class. All of it has to pass; each part alone is
    /// satisfiable by a scratch copy of the string block.
    /// </summary>
    private static bool Validate(ProcessMemory memory, nuint dataSegment)
    {
        // Pointer table: 19 absolute DS offsets that must index back into the name block.
        var table = memory.Read(dataSegment + SaveFormat.DsBase + SaveFormat.VehicleNamePointers,
                                SaveFormat.VehicleTypeCount * 2);
        if (table.Length != SaveFormat.VehicleTypeCount * 2) return false;

        int first = table[0] | (table[1] << 8);
        if (first != SaveFormat.DsBase + SaveFormat.VehicleNames) return false;

        int previous = first;
        for (int i = 1; i < SaveFormat.VehicleTypeCount; i++)
        {
            int p = table[i * 2] | (table[i * 2 + 1] << 8);
            // Names are stored back to back, so each pointer is a little past the last and all of
            // them land inside the slab.
            if (p <= previous || p >= SaveFormat.DsBase + SaveFormat.VehicleNamePointers) return false;
            previous = p;
        }

        // Vehicle-type table: record 0 is the motorcycle (mass 1, structure 3, 100 MPH, man. 4).
        var motorcycle = memory.Read(dataSegment + SaveFormat.DsBase + SaveFormat.VehicleTypeTable, 4);
        return motorcycle.Length == 4 &&
               motorcycle[0] == 1 && motorcycle[1] == 3 && motorcycle[2] == 10 && motorcycle[3] == 4;
    }

    /// <summary>
    /// Re-checks a previously located data segment without rescanning. Every write path calls
    /// this first: DOSBox can be closed, or a different program started inside it, between one
    /// edit and the next, and a stale base address would put bytes into a stranger's memory.
    /// </summary>
    public static bool StillValid(ProcessMemory memory, nuint dataSegment) =>
        memory.IsOpen && Validate(memory, dataSegment);
}

/// <summary>Reads and writes a slab that lives in an emulator's guest RAM.</summary>
public sealed class LiveSlabTarget : ISlabTarget
{
    private readonly ProcessMemory _memory;
    private readonly nuint _slabHost;

    public LiveSlabTarget(ProcessMemory memory, nuint dataSegmentHost)
    {
        _memory = memory;
        DataSegmentHost = dataSegmentHost;
        _slabHost = dataSegmentHost + SaveFormat.DsBase;
    }

    public nuint DataSegmentHost { get; }

    public bool IsAvailable => _memory.IsOpen;

    public byte[]? Read(int slabOffset, int count)
    {
        if (slabOffset < 0 || count < 0 || slabOffset + count > SaveFormat.SlabLength) return null;
        var data = _memory.Read(_slabHost + (nuint)slabOffset, count);
        return data.Length == count ? data : null;
    }

    public bool Write(int slabOffset, byte[] data)
    {
        if (slabOffset < 0 || slabOffset + data.Length > SaveFormat.SlabLength) return false;
        // Re-validate immediately before committing: between locating and writing, the emulator
        // may have moved on to something else entirely.
        if (!GameLocator.StillValid(_memory, DataSegmentHost)) return false;
        return _memory.Write(_slabHost + (nuint)slabOffset, data);
    }

    /// <summary>Reads the 2,016-byte overland map the engine currently has loaded.</summary>
    public byte[]? ReadOverlandMap()
    {
        var data = _memory.Read(DataSegmentHost + SaveFormat.DsOverlandMap, OverlandMap.CellCount);
        return data.Length == OverlandMap.CellCount ? data : null;
    }
}
