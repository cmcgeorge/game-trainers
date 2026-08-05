using LegendOfGrimrock1Trainer.Lua;
using LegendOfGrimrock1Trainer.Memory;

namespace LegendOfGrimrock1Trainer.Game;

/// <summary>Which chain produced a located VM.</summary>
public enum LocateChain
{
    /// <summary>Nothing was found.</summary>
    None,

    /// <summary>The static <c>lua_State *</c> slot in the module's data section.</summary>
    StaticPointer,

    /// <summary>A structural scan of committed memory for the <c>GG_State</c> signature.</summary>
    HeapSignature,
}

/// <summary>A validated Lua VM inside the target, plus how it was found.</summary>
public sealed record LocateResult(
    LocateChain Chain,
    uint LuaState,
    uint Globals,
    uint ModuleBase,
    PeImage? Image,
    int RegionsScanned,
    long BytesScanned,
    double ElapsedMs,
    string Detail)
{
    /// <summary>Whether a usable VM was found.</summary>
    public bool Found => Chain != LocateChain.None && Globals != 0;

    /// <summary>Whether the mapped build matches the one the notes were taken against.</summary>
    public bool BuildMatches => Image is not null && Image.TimeDateStamp == GameFacts.KnownTimeDateStamp;
}

/// <summary>
/// Finds Legend of Grimrock's Lua VM in a running process, with no value searching and nothing
/// hard-coded that a rebuild could invalidate silently.
///
/// Two chains, in order:
///
/// <list type="number">
/// <item>
/// <b>Static pointer.</b> Read the word at <c>module + </c><see cref="GrimrockLayout.LuaStateSlotRva"/>.
/// Ghidra shows exactly one cross-reference to that slot — a WRITE during Lua bootstrap — so on this
/// build it is the <c>lua_State *</c> for the whole session. Costs one read.
/// </item>
/// <item>
/// <b>Heap signature.</b> Sweep committed memory for LuaJIT's own <c>GG_State</c> shape: a
/// collectable object whose <c>gct</c> is <c>LJ_TTHREAD</c>, whose <c>dummy_ffid</c> is <c>FF_C</c>,
/// and — the load-bearing part — whose <c>glref</c> equals its own address plus
/// <c>sizeof(lua_State)</c>. LuaJIT allocates the main thread and the global state as one block, so
/// only the main thread satisfies that equality; coroutines, of which Grimrock has several, do not.
/// This chain knows nothing about Grimrock and would work against any 32-bit LuaJIT 2.0 host.
/// </item>
/// </list>
///
/// Whichever chain answers first, the candidate is only believed after the same validation: its
/// environment must be a real <c>GCtab</c>, that table's <c>_G</c> key must point back at the table
/// itself, <c>_VERSION</c> must read "Lua 5.1", and the engine's own class tables (<c>Champion</c>,
/// <c>Party</c>, <c>Map</c>, …) must all be present. A stale static pointer therefore fails cleanly
/// and falls through to the scan rather than handing the UI a plausible-looking wrong address.
/// </summary>
public sealed class GameLocator
{
    /// <summary>
    /// How many of <see cref="GrimrockLayout.EngineClassKeys"/> must resolve before a globals table
    /// is believed. Derived from the array rather than written as a literal, so adding a key
    /// tightens validation instead of silently loosening it to "any six of seven".
    /// </summary>
    private static readonly int RequiredEngineClasses = GrimrockLayout.EngineClassKeys.Length;

    private readonly IMemorySource _mem;
    private readonly LuaHeap _heap;

    /// <summary>Wraps a memory source and the heap reader that shares it.</summary>
    public GameLocator(IMemorySource mem, LuaHeap heap)
    {
        ArgumentNullException.ThrowIfNull(mem);
        ArgumentNullException.ThrowIfNull(heap);
        _mem = mem;
        _heap = heap;
    }

    /// <summary>Runs both chains and returns the first validated VM, or a <see cref="LocateChain.None"/> result.</summary>
    public LocateResult Locate()
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var image = ReadImage();

        // The whole Lua layer assumes 32-bit x86 object sizes. Say so plainly rather than sweeping a
        // 64-bit process for a signature that its layout could never produce.
        if (image is not null && !image.IsWin32X86)
        {
            started.Stop();
            return new LocateResult(LocateChain.None, 0, 0, _mem.ModuleBase, image, 0, 0,
                started.Elapsed.TotalMilliseconds,
                $"the attached module is not a 32-bit x86 image (machine 0x{image.Machine:X4}) — " +
                "Legend of Grimrock 1 is x86 only");
        }

        if (TryStaticPointer(image, out uint state, out uint globals))
        {
            started.Stop();
            return new LocateResult(LocateChain.StaticPointer, state, globals, _mem.ModuleBase, image,
                0, 0, started.Elapsed.TotalMilliseconds,
                $"static lua_State slot at module+0x{GrimrockLayout.LuaStateSlotRva:X6}");
        }

        var scan = ScanForMainThread(started, image);
        started.Stop();
        return scan;
    }

    /// <summary>Parses the mapped PE header, or null when it cannot be read.</summary>
    public PeImage? ReadImage()
    {
        var buf = new byte[PeImage.HeaderBytes];
        return _mem.Read(_mem.ModuleBase, buf, buf.Length) == buf.Length ? PeImage.Parse(buf) : null;
    }

    /// <summary>Chain A: the module's static <c>lua_State *</c>, checked against the section table first.</summary>
    private bool TryStaticPointer(PeImage? image, out uint state, out uint globals)
    {
        state = 0;
        globals = 0;

        // Refuse the shortcut unless the RVA lands in a writable data section of the image that is
        // actually mapped: on a different build that word could be code, a string, or nothing. A
        // header that would not parse is a refusal too, not a waiver — an unverifiable shortcut is
        // worth less than the sweep that does not need one.
        if (image is null || !image.IsWritableDataRva(GrimrockLayout.LuaStateSlotRva)) return false;
        if (GrimrockLayout.LuaStateSlotRva + 4 > (uint)_mem.ModuleSize) return false;

        var slot = _heap.ReadUInt32(_mem.ModuleBase + GrimrockLayout.LuaStateSlotRva);
        if (slot is null or 0) return false;

        if (!IsMainThread(slot.Value)) return false;
        if (!TryValidateGlobals(slot.Value, out globals)) return false;

        state = slot.Value;
        return true;
    }

    /// <summary>Chain B: sweep committed memory for the <c>GG_State</c> signature.</summary>
    private LocateResult ScanForMainThread(System.Diagnostics.Stopwatch clock, PeImage? image)
    {
        int regions = 0;
        long bytes = 0;
        var buffer = Array.Empty<byte>();

        foreach (var region in _mem.Regions())
        {
            regions++;
            if (region.Size < LuaLayout.StateSize) continue;
            if (buffer.Length < region.Size) buffer = new byte[region.Size];

            int read = _mem.Read(region.Base, buffer, (int)region.Size);
            if (read != region.Size) continue;
            bytes += read;

            int limit = read - LuaLayout.StateSize;
            for (int i = 0; i <= limit; i += 4)
            {
                // gct == LJ_TTHREAD and dummy_ffid == FF_C, read as one 16-bit compare.
                if (buffer[i + LuaLayout.GcType] != LuaLayout.GcTypeThread) continue;
                if (buffer[i + LuaLayout.StateDummyFfid] != LuaLayout.FastFunctionC) continue;
                if (buffer[i + LuaLayout.StateStatus] > LuaLayout.MaxThreadStatus) continue;

                uint candidate = region.Base + (uint)i;
                uint glref = BitConverter.ToUInt32(buffer, i + LuaLayout.StateGlobalRef);
                if (glref != candidate + LuaLayout.MainThreadGlobalStateDelta) continue;

                if (!PlausibleStack(buffer, i)) continue;
                if (!TryValidateGlobals(candidate, out uint globals)) continue;

                return new LocateResult(LocateChain.HeapSignature, candidate, globals, _mem.ModuleBase, image,
                    regions, bytes, clock.Elapsed.TotalMilliseconds,
                    "GG_State signature (gct=LJ_TTHREAD, dummy_ffid=FF_C, glref == L + sizeof(lua_State))");
            }
        }

        return new LocateResult(LocateChain.None, 0, 0, _mem.ModuleBase, image,
            regions, bytes, clock.Elapsed.TotalMilliseconds,
            "no LuaJIT main thread found — is this really grimrock.exe?");
    }

    /// <summary>Re-reads a candidate thread and re-applies the <c>GG_State</c> signature.</summary>
    private bool IsMainThread(uint state)
    {
        var b = _heap.Read(state, LuaLayout.StateSize);
        if (b.Length != LuaLayout.StateSize) return false;
        if (b[LuaLayout.GcType] != LuaLayout.GcTypeThread) return false;
        if (b[LuaLayout.StateDummyFfid] != LuaLayout.FastFunctionC) return false;
        if (b[LuaLayout.StateStatus] > LuaLayout.MaxThreadStatus) return false;
        if (BitConverter.ToUInt32(b, LuaLayout.StateGlobalRef) != state + LuaLayout.MainThreadGlobalStateDelta) return false;
        return PlausibleStack(b, 0);
    }

    /// <summary>Cheap ordering checks on the thread's stack pointers, to shed false positives early.</summary>
    private static bool PlausibleStack(byte[] buffer, int offset)
    {
        uint stack = BitConverter.ToUInt32(buffer, offset + LuaLayout.StateStack);
        uint maxstack = BitConverter.ToUInt32(buffer, offset + LuaLayout.StateMaxStack);
        uint top = BitConverter.ToUInt32(buffer, offset + LuaLayout.StateTop);
        uint stackBase = BitConverter.ToUInt32(buffer, offset + LuaLayout.StateBase);
        uint size = BitConverter.ToUInt32(buffer, offset + LuaLayout.StateStackSize);
        uint env = BitConverter.ToUInt32(buffer, offset + LuaLayout.StateEnv);

        if (stack == 0 || env == 0) return false;
        if (maxstack <= stack) return false;
        if (top < stack || top > maxstack) return false;
        if (stackBase < stack || stackBase > maxstack) return false;
        if (size == 0 || size > 1 << 22) return false;
        return true;
    }

    /// <summary>
    /// Proves a thread's environment really is a Lua globals table: it must be a <c>GCtab</c>, its
    /// <c>_G</c> key must point at itself, <c>_VERSION</c> must be "Lua 5.1", and every engine class
    /// table Grimrock defines at start-up must be present. The self-reference alone rules out
    /// essentially any accidental match; the class tables then confirm it is <i>this</i> game.
    /// </summary>
    public bool TryValidateGlobals(uint state, out uint globals)
    {
        globals = 0;

        var env = _heap.ReadUInt32(state + LuaLayout.StateEnv);
        if (env is null or 0) return false;
        if (!_heap.TryReadTable(env.Value, out var table)) return false;

        var self = _heap.GetField(table, GrimrockLayout.GlobalsSelfKey);
        if (!self.IsTable || self.Reference != env.Value) return false;

        var version = _heap.GetField(table, GrimrockLayout.VersionKey);
        if (_heap.StringOf(version) != GrimrockLayout.ExpectedLuaVersion) return false;

        int classes = 0;
        foreach (var key in GrimrockLayout.EngineClassKeys)
            if (_heap.GetField(table, key).IsTable) classes++;
        if (classes < RequiredEngineClasses) return false;

        globals = env.Value;
        return true;
    }
}
