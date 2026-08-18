using System.Diagnostics;
using System.Runtime.InteropServices;
using BardsTaleTrilogyTrainer.Memory;

namespace BardsTaleTrilogyTrainer.Game;

/// <summary>
/// The result of a successful locate: the base address of the game-state object
/// and the addresses of the individual character objects in the party array.
/// </summary>
public sealed class GameLocation
{
    public nuint GameStateObject { get; init; }
    public nuint PartyObject { get; init; }
    public List<nuint> CharacterAddresses { get; init; } = new();
    public int ValidatorCount { get; init; }
    public bool UsedFallback { get; init; }
    public string Summary { get; init; } = "";

    public int CharacterCount => CharacterAddresses.Count;
}

/// <summary>
/// Finds the party and character data in The Bard's Tale Trilogy remaster.
///
/// Primary chain: reads the global pointer at <c>GameAssembly.dll + 0xE40338</c>,
/// follows it to the party/economy object, then walks the character array.
///
/// Fallback: sweeps all committed memory for objects whose fields match the
/// shape of an IL2CPP character (plausible XP, HP, SP, race, class, level, stats).
/// </summary>
public static class GameLocator
{
    private const int ChunkSize = 1 << 20;
    private const int CharacterObjectSize = 0x100; // upper bound for the fields we check

    /// <summary>Find the game process by name.</summary>
    public static Process? FindGameProcess() =>
        Process.GetProcessesByName(GameFacts.ProcessName).FirstOrDefault();

    /// <summary>Find the base address of GameAssembly.dll in the target process.</summary>
    public static nuint FindModuleBase(Process process, string moduleName)
    {
        foreach (ProcessModule module in process.Modules)
        {
            if (string.Equals(module.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase))
                return (nuint)module.BaseAddress.ToInt64();
        }
        return 0;
    }

    /// <summary>
    /// Attempts to locate the party data. Tries the pointer chain first, then
    /// falls back to a structural scan of all committed memory.
    /// </summary>
    public static GameLocation? Locate(
        IMemorySource mem,
        nuint moduleBase,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        // --- Primary: pointer chain ---
        if (moduleBase != 0)
        {
            var chainResult = TryPointerChain(mem, moduleBase);
            if (chainResult != null)
                return chainResult;
        }

        // --- Fallback: structural scan ---
        var scanResult = StructuralScan(mem, progress, ct);
        return scanResult;
    }

    private static GameLocation? TryPointerChain(IMemorySource mem, nuint moduleBase)
    {
        // Read the global game-state pointer
        nuint globalPtr = ReadPtr(mem, moduleBase + (nuint)GameFacts.GlobalPointerRva);
        if (globalPtr == 0) return null;

        // Validate: the game-state object should have a non-null vtable pointer
        nuint vtable = ReadPtr(mem, globalPtr);
        if (vtable == 0) return null;

        // Follow to the party/economy sub-object
        nuint partyObj = ReadPtr(mem, globalPtr + (nuint)GameFacts.GameStatePartyOffset);
        if (partyObj == 0) return null;

        // Read gold to validate the party object
        int gold = ReadI32(mem, partyObj + (nuint)GameFacts.PartyGoldOffset);
        if (gold < 0 || gold > 100_000_000) return null;

        // Try to find the character array from the party object.
        // The party object likely has a pointer to an array of character pointers.
        // We scan the party object's fields for a pointer to a readable array.
        var characters = FindCharacterArray(mem, partyObj);
        if (characters.Count == 0)
        {
            // Try scanning from the game-state object directly
            characters = FindCharacterArray(mem, globalPtr);
        }

        if (characters.Count == 0) return null;

        return new GameLocation
        {
            GameStateObject = globalPtr,
            PartyObject = partyObj,
            CharacterAddresses = characters,
            ValidatorCount = characters.Count,
            UsedFallback = false,
            Summary = $"Pointer chain: {characters.Count} characters found"
        };
    }

    private static List<nuint> FindCharacterArray(IMemorySource mem, nuint objBase)
    {
        var result = new List<nuint>();

        // Scan the first 0x200 bytes of the object for a pointer to an array
        for (int off = 0x10; off < 0x200; off += 8)
        {
            nuint ptr = ReadPtr(mem, objBase + (nuint)off);
            if (ptr == 0) continue;

            // Check if this looks like an IL2CPP array of character pointers
            // IL2CPP array header: class ptr (8) + monitor (8) + bounds (8) + length (4)
            // Array elements start at +0x20 on x64
            var lenBuf = new byte[4];
            if (mem.Read(ptr + 0x18, lenBuf, 4) != 4) continue;
            int len = lenBuf[0] | (lenBuf[1] << 8) | (lenBuf[2] << 16) | (lenBuf[3] << 24);
            if (len < 1 || len > GameFacts.PartySlots) continue;

            // Read the first few elements and validate as character objects
            int validCount = 0;
            var candidates = new List<nuint>();
            for (int i = 0; i < len; i++)
            {
                nuint elemPtr = ReadPtr(mem, ptr + (nuint)(0x20 + i * 8));
                if (elemPtr == 0) { candidates.Add(0); continue; }

                // Validate as a character object
                var buf = new byte[CharacterObjectSize];
                if (mem.Read(elemPtr, buf, CharacterObjectSize) != CharacterObjectSize) { candidates.Add(0); continue; }
                if (CharacterFormat.LooksLikeCharacter(buf))
                {
                    validCount++;
                    candidates.Add(elemPtr);
                }
                else
                {
                    candidates.Add(0);
                }
            }

            // Require at least one of slots 1–6 to be valid (slot 0 is special/summon, often empty)
            bool hasMember = false;
            for (int i = 1; i < candidates.Count && i <= GameFacts.PartySlots - 1; i++)
            {
                if (candidates[i] != 0) { hasMember = true; break; }
            }
            if (validCount >= 1 && hasMember)
            {
                result.AddRange(candidates.Where(a => a != 0));
                if (result.Count > 0) break;
            }
        }

        return result;
    }

    private static GameLocation? StructuralScan(
        IMemorySource mem,
        IProgress<double>? progress,
        CancellationToken ct)
    {
        var hits = new List<nuint>();
        var regions = mem.EnumerateRegions().ToList();

        nuint totalBytes = 0;
        foreach (var (base_, size) in regions) totalBytes += size;
        nuint scanned = 0;

        byte[] buf = new byte[ChunkSize];

        foreach (var (regionBase, regionSize) in regions)
        {
            ct.ThrowIfCancellationRequested();

            for (nuint offset = 0; offset < regionSize;)
            {
                int want = (int)Math.Min((nuint)ChunkSize, regionSize - offset);
                int read = mem.Read(regionBase + offset, buf, want);
                if (read < CharacterObjectSize)
                {
                    scanned += (nuint)want;
                    break;
                }

                // Scan for character objects at 8-byte alignment (IL2CPP objects are 8-byte aligned)
                for (int i = 0; i + CharacterObjectSize <= read; i += 8)
                {
                    // Quick pre-filter: check XP at +0x50 is a plausible value
                    int xp = CharacterFormat.ReadI32(buf.AsSpan(i), CharacterFormat.OffExperience);
                    if (xp < 0 || xp > 100_000_000) continue;

                    // Check HP at +0x84
                    int hp = CharacterFormat.ReadI32(buf.AsSpan(i), CharacterFormat.OffHpCur);
                    if (hp < 0 || hp > 9999) continue;

                    // Check SP at +0x8C
                    int sp = CharacterFormat.ReadI32(buf.AsSpan(i), CharacterFormat.OffSpCur);
                    if (sp < 0 || sp > 9999) continue;

                    // Full validation
                    if (CharacterFormat.LooksLikeCharacter(buf.AsSpan(i)))
                    {
                        nuint addr = regionBase + offset + (nuint)i;
                        if (!hits.Contains(addr))
                            hits.Add(addr);
                    }
                }

                nuint advance = (nuint)Math.Max(1, read - CharacterObjectSize);
                offset += advance;
                scanned += advance;
                progress?.Report(totalBytes == 0 ? 0 : Math.Min(1.0, (double)scanned / totalBytes));
            }
        }

        if (hits.Count == 0) return null;

        // Limit to party-sized group
        var characters = hits.Take(GameFacts.PartySlots).ToList();

        return new GameLocation
        {
            GameStateObject = 0,
            PartyObject = 0,
            CharacterAddresses = characters,
            ValidatorCount = characters.Count,
            UsedFallback = true,
            Summary = $"Structural scan: {characters.Count} character objects found"
        };
    }

    // --- memory helpers ---------------------------------------------------------
    private static nuint ReadPtr(IMemorySource mem, nuint addr)
    {
        var buf = new byte[8];
        if (mem.Read(addr, buf, 8) != 8) return 0;
        return (nuint)(
            (long)buf[0] | ((long)buf[1] << 8) | ((long)buf[2] << 16) | ((long)buf[3] << 24) |
            ((long)buf[4] << 32) | ((long)buf[5] << 40) | ((long)buf[6] << 48) | ((long)buf[7] << 56));
    }

    private static int ReadI32(IMemorySource mem, nuint addr)
    {
        var buf = new byte[4];
        if (mem.Read(addr, buf, 4) != 4) return 0;
        return buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24);
    }
}
