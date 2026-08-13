using CurseOfTheAzureBondsTrainer.Game;

namespace CurseOfTheAzureBondsTrainer.Memory;

/// <summary>A level found resident in the running game: its <c>GEO&lt;n&gt;:&lt;block&gt;</c> tag
/// and the host address its wall planes were found at.</summary>
public sealed record LocatedLevel(string Geo, nuint Address);

/// <summary>
/// Works out which level the game currently has loaded, by finding that level's wall data resident
/// in the emulated RAM.
///
/// <para>The game reads a level's geometry out of <c>GEO*.DAX</c> and holds the two wall planes —
/// 512 bytes, four wall-index nibbles per square — in memory unchanged. So the level the party is
/// standing in is identifiable without decoding any game logic: read the archives off disk, and look
/// for one of those 512-byte arrays in the process. A 512-byte exact match is not something a wrong
/// answer produces by accident.</para>
///
/// <para>This is what lets the Maps tab name the area for you. The level names in
/// <see cref="MapBook"/> are labels rather than decoded facts — Curse's printed maps are in an
/// Adventurer's Journal that isn't part of the install — so "which of these sixteen am I in?" is
/// answered by the geometry itself rather than by trusting a label.</para>
///
/// <para>Like the combat sweep, this searches the window around the party rather than the whole
/// process: the level data lives in the same 640 KiB of DOS conventional memory as the party
/// records, so a ±512 KiB sweep finds it in milliseconds where a full walk would take most of a
/// second.</para>
/// </summary>
public static class MapLocator
{
    private const int SweepChunk = 1 << 16;   // 64 KiB per read

    /// <summary>Length of the scratch buffer <see cref="Identify"/> requires.</summary>
    public const int SweepBufferSize = SweepChunk + DaxArchive.GeoWallLength;

    /// <summary>
    /// Finds every known level whose wall planes are resident in the window around the party.
    /// Normally that is one — the level being explored. Pass the levels read from the game folder
    /// (<see cref="DaxArchive.ReadLevels"/>); <paramref name="buffer"/> is a reusable scratch buffer
    /// of <see cref="SweepBufferSize"/> bytes.
    /// </summary>
    public static List<LocatedLevel> Identify(ProcessMemory mem, nuint partyLow, nuint partyHigh,
        IReadOnlyList<(string Geo, byte[] Walls)> levels, byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(mem);
        ArgumentNullException.ThrowIfNull(levels);
        ArgumentNullException.ThrowIfNull(buffer);
        if (buffer.Length < SweepBufferSize)
            throw new ArgumentException($"buffer must be at least {SweepBufferSize} bytes.", nameof(buffer));

        var found = new List<LocatedLevel>();
        if (levels.Count == 0) return found;

        // Index the levels by the first four bytes of their wall planes, so the sweep costs one
        // dictionary probe per byte rather than a comparison against every level.
        var byPrefix = new Dictionary<uint, List<int>>();
        for (int i = 0; i < levels.Count; i++)
        {
            byte[] w = levels[i].Walls;
            if (w.Length < DaxArchive.GeoWallLength) continue;
            uint key = Prefix(w, 0);
            if (!byPrefix.TryGetValue(key, out var list)) byPrefix[key] = list = new List<int>();
            list.Add(i);
        }
        if (byPrefix.Count == 0) return found;

        var seen = new HashSet<string>();
        nuint radius = (nuint)CharacterLocator.ArenaRadius;
        nuint start = partyLow > radius ? partyLow - radius : 0;
        nuint end = partyHigh + radius;
        if (end < partyHigh) end = nuint.MaxValue;

        for (nuint addr = start; addr < end;)
        {
            int want = (int)Math.Min((nuint)SweepChunk, end - addr);
            int read = mem.Read(addr, buffer, Math.Min(want + DaxArchive.GeoWallLength, buffer.Length));

            for (int i = 0; i + DaxArchive.GeoWallLength <= read; i++)
            {
                if (!byPrefix.TryGetValue(Prefix(buffer, i), out var candidates)) continue;
                foreach (int c in candidates)
                {
                    if (!buffer.AsSpan(i, DaxArchive.GeoWallLength).SequenceEqual(levels[c].Walls)) continue;
                    if (seen.Add(levels[c].Geo)) found.Add(new LocatedLevel(levels[c].Geo, addr + (nuint)i));
                }
            }

            // Unreadable pages are normal at the window's edges; skip the chunk rather than
            // abandoning the sweep, and on a short read advance only past what was scanned.
            addr += read >= want ? (nuint)want
                  : read > DaxArchive.GeoWallLength ? (nuint)(read - DaxArchive.GeoWallLength + 1)
                  : (nuint)want;
        }
        return found;
    }

    private static uint Prefix(byte[] b, int i) =>
        (uint)(b[i] | (b[i + 1] << 8) | (b[i + 2] << 16) | (b[i + 3] << 24));
}
