using Civilization3ConquestsTrainer.Memory;

namespace Civilization3ConquestsTrainer.Game;

/// <summary>Which route the locator took to find the game state.</summary>
public enum LocateChain
{
    /// <summary>Module base plus the recovered RVAs — the one-click path, no scanning.</summary>
    StaticGlobals,

    /// <summary>The <c>leaders</c> array re-derived from the game's own array-walk code.</summary>
    SignatureScan,
}

/// <summary>Everything a located game session exposes. All addresses are absolute and session-specific.</summary>
public sealed class Civ3Location
{
    public required nuint ModuleBase { get; init; }
    public required PeImage Pe { get; init; }
    public required LocateChain Chain { get; init; }

    /// <summary>Whether the running exe is the build the layout table was recovered against.</summary>
    public required bool IsKnownBuild { get; init; }

    /// <summary>Base of the inline <c>Leader[32]</c> array.</summary>
    public required nuint Leaders { get; init; }

    /// <summary>
    /// Size of one <c>Leader</c> record in this build. Normally <see cref="Civ3Layout.LeaderStride"/>,
    /// but chain B recovers it from the game's own code, so a build that resized the record still works.
    /// </summary>
    public required int LeaderStride { get; init; }

    /// <summary>
    /// Length of the leading run of leader slots that passed full validation. 32 is a clean locate;
    /// anything less is a failure, so this is only ever a diagnostic.
    /// </summary>
    public required int ValidatedLeaders { get; init; }

    /// <summary>A non-fatal note about this locate, or empty. Surfaced alongside the status line.</summary>
    public string Warning { get; init; } = "";

    /// <summary>Civ id the human is playing, from the main screen form.</summary>
    public required int HumanCivId { get; init; }

    /// <summary>Bit N set means civ N is present in this game.</summary>
    public required uint PlayerBits { get; init; }

    public required nuint CitiesContainer { get; init; }
    public required nuint UnitsContainer { get; init; }
    public required nuint BicData { get; init; }
    public required nuint Map { get; init; }
    public required int MapWidth { get; init; }
    public required int MapHeight { get; init; }
    public required int TileCount { get; init; }

    /// <summary>Absolute address of a leader record.</summary>
    public nuint Leader(int civId) => Leaders + (nuint)(civId * LeaderStride);

    /// <summary>Absolute address of a field within a leader record.</summary>
    public nuint LeaderField(int civId, int offset) => Leader(civId) + (nuint)offset;

    /// <summary>Absolute address of a module-relative global.</summary>
    public nuint Global(uint rva) => ModuleBase + (nuint)rva;
}

/// <summary>
/// Finds Civ3's game state in a running <c>Civ3Conquests.exe</c> with no value scanning.
///
/// <para>The exe is a native 32-bit image that never opted in to ASLR, so every static object sits at
/// a constant offset from the module base. <b>Chain A</b> therefore just adds the recovered RVAs to
/// the base the OS reports and then <i>proves</i> the result: all 32 leader slots must carry the
/// 'LEAD' tag, an <c>ID</c> equal to their own index, a shared vtable inside <c>.rdata</c>, sliders
/// that total 10, and an embedded 'CULT' object whose <c>CivID</c> agrees. That conjunction is what
/// makes a false positive implausible — <c>ID == index</c> across 32 records at a fixed stride pins
/// both the base and the stride uniquely.</para>
///
/// <para><b>Chain B</b> covers a build whose globals moved. Civ3's compiler inlines the leader array
/// walk as <c>add reg, sizeof(Leader)</c> / <c>cmp reg, end-of-array</c>, so sweeping <c>.text</c> for
/// that idiom re-derives both the stride and the array base from the program's own code. The result
/// goes through exactly the same validation as Chain A.</para>
///
/// <para>If neither validates, <see cref="Locate"/> returns null and the caller falls back to the
/// Cheat-Engine-style value scanner — with the caveat that Civ3's treasury is obfuscated and cannot
/// be found by an exact-value scan (see <see cref="Civ3Layout.DecodeGold"/>).</para>
/// </summary>
public sealed class GameLocator
{
    private readonly IMemorySource _mem;

    public GameLocator(IMemorySource mem) => _mem = mem;

    /// <summary>Human-readable note about the last attempt, for the status bar.</summary>
    public string LastError { get; private set; } = "";

    /// <summary>Locates the game state, or returns null with <see cref="LastError"/> set.</summary>
    public Civ3Location? Locate()
    {
        LastError = "";

        byte[] header = _mem.Read(_mem.ModuleBase, PeImage.HeaderReadSize);
        var pe = header.Length == PeImage.HeaderReadSize ? PeImage.Parse(header) : null;
        if (pe == null)
        {
            LastError = "Could not read a 32-bit PE header at the module base.";
            return null;
        }
        if (pe.Machine != 0x014C)
        {
            LastError = $"Target is not a 32-bit x86 image (machine 0x{pe.Machine:X4}) — " +
                        $"{GameFacts.ProcessName}.exe is. Pick the process marked \"the game\" in the list.";
            return null;
        }

        var rdata = pe.Section(".rdata");
        if (rdata == null)
        {
            LastError = "The image has no .rdata section to validate vtables against.";
            return null;
        }
        uint rdataStart = (uint)_mem.ModuleBase + rdata.Value.Rva;
        uint rdataEnd = rdataStart + rdata.Value.VirtualSize;

        bool knownBuild = pe.TimeDateStamp == GameFacts.KnownTimeDateStamp;

        // Chain A: the recovered RVAs, validated.
        nuint candidate = _mem.ModuleBase + (nuint)Civ3Layout.RvaLeaders;
        int stride = Civ3Layout.LeaderStride;
        int validated = CountLeadingValidLeaders(candidate, stride, rdataStart, rdataEnd);
        var chain = LocateChain.StaticGlobals;

        // Chain B: re-derive the array from the game's own code. Only adopted if it does better than
        // chain A — otherwise a chain B candidate that validates nothing would replace a chain A
        // result that was one slot short, and the error message would then claim no game is loaded.
        if (validated < GameFacts.MaxPlayers)
        {
            var derived = DeriveLeadersFromCode(pe, rdataStart, rdataEnd);
            if (derived is { } d)
            {
                // Both numbers come from the code, so a build that changed sizeof(Leader) is
                // recoverable rather than merely detectable.
                int derivedValid = CountLeadingValidLeaders(d.Base, d.Stride, rdataStart, rdataEnd);
                if (derivedValid > validated)
                {
                    candidate = d.Base;
                    stride = d.Stride;
                    validated = derivedValid;
                    chain = LocateChain.SignatureScan;
                }
            }
        }

        if (validated < GameFacts.MaxPlayers)
        {
            LastError = validated == 0
                ? "No leader array validated. Load a game first — the state does not exist at the main menu."
                : $"Only {validated} of {GameFacts.MaxPlayers} leader slots validated, so the layout does not match this build.";
            return null;
        }

        int humanCivId = ReadInt32(_mem.ModuleBase + (nuint)Civ3Layout.RvaMainScreenForm
                                                   + (nuint)Civ3Layout.MainScreenPlayerCivId);
        uint humanBits = ReadUInt32(_mem.ModuleBase + (nuint)Civ3Layout.RvaHumanPlayerBits);
        uint playerBits = ReadUInt32(_mem.ModuleBase + (nuint)Civ3Layout.RvaPlayerBits);

        if (!Civ3Layout.IsValidCivId(humanCivId) || !Civ3Layout.IsBitSet(playerBits, humanCivId))
        {
            LastError = $"The human player's civ id ({humanCivId}) is not one of the civs in this game. " +
                        "Load or start a game before locating.";
            return null;
        }

        // Not fatal — a hotseat or observer setup can leave this clear — but the caller should say so,
        // which is why it travels on the result rather than in LastError (which means "locate failed").
        string warning = Civ3Layout.IsBitSet(humanBits, humanCivId)
            ? ""
            : $"Civ {humanCivId} is not flagged as human-controlled — check you are editing the right civilization.";

        nuint bic = _mem.ModuleBase + (nuint)Civ3Layout.RvaBicData;
        nuint map = bic + (nuint)Civ3Layout.BicMap;
        int width = ReadInt32(map + (nuint)Civ3Layout.MapWidth);
        int height = ReadInt32(map + (nuint)Civ3Layout.MapHeight);
        int tiles = ReadInt32(map + (nuint)Civ3Layout.MapTileCount);
        if (!Civ3Layout.ValidateMap(width, height, tiles)) { width = height = tiles = 0; }

        return new Civ3Location
        {
            ModuleBase = _mem.ModuleBase,
            Pe = pe,
            Chain = chain,
            IsKnownBuild = knownBuild,
            Leaders = candidate,
            LeaderStride = stride,
            ValidatedLeaders = validated,
            Warning = warning,
            HumanCivId = humanCivId,
            PlayerBits = playerBits,
            CitiesContainer = _mem.ModuleBase + (nuint)Civ3Layout.RvaCities,
            UnitsContainer = _mem.ModuleBase + (nuint)Civ3Layout.RvaUnits,
            BicData = bic,
            Map = map,
            MapWidth = width,
            MapHeight = height,
            TileCount = tiles,
        };
    }

    /// <summary>
    /// Length of the leading run of slots at <paramref name="leaders"/> that validate as leader
    /// records. It stops at the first failure rather than counting the rest, which is all the caller
    /// needs: anything short of all 32 is a failed locate either way.
    /// </summary>
    private int CountLeadingValidLeaders(nuint leaders, int stride, uint rdataStart, uint rdataEnd)
    {
        if (stride < Civ3Layout.LeaderMinValidatableSize) return 0;
        int ok = 0;
        byte[] buf = new byte[stride];
        for (int i = 0; i < GameFacts.MaxPlayers; i++)
        {
            nuint at = leaders + (nuint)(i * stride);
            if (_mem.Read(at, buf, buf.Length) != buf.Length) break;
            if (!Civ3Layout.ValidateLeader(buf, i, rdataStart, rdataEnd)) break;
            ok++;
        }
        return ok;
    }

    // --- Chain B -------------------------------------------------------------------------------

    private const int PairWindow = 16;      // how far a `cmp` may sit behind its `add`
    private const int MinStride = 0x100;
    private const int MaxStride = 0x8000;

    /// <summary>
    /// Re-derives the leader array base by finding the compiler's array-walk idiom in <c>.text</c>:
    /// <c>add reg32, sizeof(Leader)</c> shortly followed by <c>cmp reg32, one-past-end</c>. Every hit
    /// whose implied base lands in a data section votes; the modal stride wins and the lowest base in
    /// that cluster is the array itself (higher ones are offsets of a field within the first record).
    /// </summary>
    private (nuint Base, int Stride)? DeriveLeadersFromCode(PeImage pe, uint rdataStart, uint rdataEnd)
    {
        var text = pe.Section(".text");
        if (text == null) return null;

        byte[] code = _mem.Read(_mem.ModuleBase + (nuint)text.Value.Rva, (int)text.Value.VirtualSize);
        if (code.Length < 32) return null;

        var votes = new Dictionary<(int Stride, uint Base), int>();
        for (int i = 0; i + 6 <= code.Length; i++)
        {
            if (code[i] != 0x81 || code[i + 1] < 0xC0 || code[i + 1] > 0xC7) continue;   // add r32, imm32
            int register = code[i + 1] - 0xC0;
            int stride = BitConverter.ToInt32(code, i + 2);
            if (stride < MinStride || stride > MaxStride || (stride & 3) != 0) continue;

            int limit = Math.Min(code.Length - 6, i + 6 + PairWindow);
            for (int j = i + 6; j <= limit; j++)
            {
                // The cmp must test the *same* register the add just advanced, otherwise unrelated
                // instruction pairs dilute the vote.
                if (code[j] != 0x81 || code[j + 1] != 0xF8 + register) continue;         // cmp r32, imm32
                uint end = BitConverter.ToUInt32(code, j + 2);
                long baseAddr = (long)end - (long)GameFacts.MaxPlayers * stride;
                if (baseAddr <= 0 || baseAddr > uint.MaxValue) continue;
                if (!IsInWritableData(pe, (uint)baseAddr, rdataStart, rdataEnd)) continue;

                var key = (stride, (uint)baseAddr);
                votes[key] = votes.TryGetValue(key, out int n) ? n + 1 : 1;
                break;
            }
        }
        if (votes.Count == 0) return null;

        // Pick the most-voted stride, then the lowest base among that stride's candidates.
        int bestStride = votes.GroupBy(v => v.Key.Stride)
                              .OrderByDescending(g => g.Sum(v => v.Value))
                              .First().Key;
        uint bestBase = votes.Where(v => v.Key.Stride == bestStride).Min(v => v.Key.Base);
        return ((nuint)bestBase, bestStride);
    }

    /// <summary>
    /// Whether an absolute address lands in a mapped section that can hold mutable globals. Tested by
    /// section characteristics (writable, not executable) rather than by the name ".data", since chain
    /// B exists precisely for builds that differ from the one the offsets came from.
    /// </summary>
    private bool IsInWritableData(PeImage pe, uint address, uint rdataStart, uint rdataEnd)
    {
        if (address >= rdataStart && address < rdataEnd) return false;
        uint moduleBase = (uint)_mem.ModuleBase;
        if (address < moduleBase || address >= moduleBase + pe.SizeOfImage) return false;
        uint rva = address - moduleBase;
        foreach (var s in pe.Sections)
            if (s.IsWritableData && s.ContainsRva(rva)) return true;
        return false;
    }

    // --- small readers -------------------------------------------------------------------------

    private int ReadInt32(nuint address)
    {
        byte[] b = _mem.Read(address, 4);
        return b.Length == 4 ? BitConverter.ToInt32(b) : 0;
    }

    private uint ReadUInt32(nuint address)
    {
        byte[] b = _mem.Read(address, 4);
        return b.Length == 4 ? BitConverter.ToUInt32(b) : 0;
    }
}
