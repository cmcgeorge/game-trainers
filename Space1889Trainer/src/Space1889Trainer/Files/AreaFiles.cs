using System.Text;

namespace Space1889Trainer.Files;

/// <summary>An area record from A.SET/B.SET (0x47 bytes; loaded into state 0x3ACA whenever the party enters the area).</summary>
/// <param name="Name">City or site name.</param>
/// <param name="DarkMaps">Maps 1..9 that are dark (need a MINERS HAT, LANTERN or ELECTRIC LAMP in use).</param>
/// <param name="GoldMaps">Maps 1..9 where digging can turn up gold.</param>
/// <param name="EntryPositions">Where the party is placed on entering map 0..9: (row, column); (0,0) = unset.</param>
public sealed record AreaInfo(string Name, IReadOnlyList<int> DarkMaps, IReadOnlyList<int> GoldMaps, IReadOnlyList<(int Row, int Column)> EntryPositions);

/// <summary>An NPC placed on a map (A.NPC/B.NPC, 140-byte records).</summary>
public sealed record NpcInfo(string Name, int Id, int MapId, int Row, int Column, int Type, int LootItemId, int WeaponItemId);

/// <summary>Readers for the per-area files: <c>?.SET</c> and <c>?.NPC</c>.</summary>
public static class AreaFiles
{
    /// <summary>Which SET/NPC file covers a planet.</summary>
    public static string SetFileFor(int planet) => planet < 4 ? "A.SET" : "B.SET";

    public static string NpcFileFor(int planet) => planet < 4 ? "A.NPC" : "B.NPC";

    /// <summary>
    /// Reads the area record for (<paramref name="planet"/>, <paramref name="area"/>), area ≥ 1 (M.EXE 1000:23F0):
    /// a u16 offset table indexed planet × 10 + area (0xFFFF = none), then <c>char[16]</c> flags ('1' in position
    /// map−1 = dark; position map+7 = gold), ten (u16 row, u16 col) entry positions and a newline-terminated name.
    /// </summary>
    public static AreaInfo? ReadArea(byte[] set, int planet, int area)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (area < 1) return null;
        int offset = TableEntry(set, planet * 10 + area);
        if (offset < 0 || offset > set.Length - 0x47) return null;

        string flags = Encoding.ASCII.GetString(set, offset, 16);
        var dark = new List<int>();
        var gold = new List<int>();
        for (int m = 1; m <= 9; m++)
        {
            if (flags[m - 1] == '1') dark.Add(m);
            if (m + 7 < 16 && flags[m + 7] == '1') gold.Add(m);
        }
        var entries = new List<(int, int)>();
        for (int i = 0; i < 10; i++)
        {
            int p = offset + 16 + i * 4;
            entries.Add((set[p] | (set[p + 1] << 8), set[p + 2] | (set[p + 3] << 8)));
        }
        return new AreaInfo(ReadLine(set, offset + 0x38, 15), dark, gold, entries);
    }

    /// <summary>The planet record's overland start position (row, col), or null.</summary>
    public static (int Row, int Column)? ReadPlanetStart(byte[] set, int planet)
    {
        ArgumentNullException.ThrowIfNull(set);
        int offset = TableEntry(set, planet * 10);
        if (offset < 0 || offset > set.Length - 0x13) return null;
        return (set[offset] | (set[offset + 1] << 8), set[offset + 2] | (set[offset + 3] << 8));
    }

    private static int TableEntry(byte[] set, int index)
    {
        // The table length is implied by the first record's offset.
        int first = set.Length;
        for (int i = 0; i * 2 + 2 <= set.Length && i * 2 < first; i++)
        {
            int v = set[i * 2] | (set[i * 2 + 1] << 8);
            if (v != 0xFFFF) first = Math.Min(first, v);
        }
        if (index < 0 || index * 2 >= first) return -1;
        int o = set[index * 2] | (set[index * 2 + 1] << 8);
        return o == 0xFFFF ? -1 : o;
    }

    private static string ReadLine(byte[] b, int offset, int width)
    {
        int n = 0;
        while (n < width && offset + n < b.Length && b[offset + n] != 0 && b[offset + n] != (byte)'\n') n++;
        return Encoding.ASCII.GetString(b, offset, n);
    }

    /// <summary>
    /// NPCs on a map (M.EXE 1000:211C): a u16 offset table of 1,000 entries keyed by map id (0xFFFF = none), then
    /// a u16 count and 140-byte records — +0x00 name, +0x19 u16 global id, +0x1B u16 map id, +0x1D row, +0x1E col,
    /// +0x21 type, +0x24 weapon (id), +0x26 loot (id).
    /// </summary>
    public static IReadOnlyList<NpcInfo> ReadNpcs(byte[] npc, int mapId)
    {
        ArgumentNullException.ThrowIfNull(npc);
        var list = new List<NpcInfo>();
        if (mapId < 0 || mapId * 2 + 2 > npc.Length) return list;
        int offset = npc[mapId * 2] | (npc[mapId * 2 + 1] << 8);
        if (offset == 0xFFFF || offset > npc.Length - 2) return list;
        int count = npc[offset] | (npc[offset + 1] << 8);
        for (int k = 0; k < count; k++)
        {
            int r = offset + 2 + k * 140;
            if (r > npc.Length - 140) break;
            int nameLen = 0;
            while (nameLen < 25 && npc[r + nameLen] != 0) nameLen++;
            list.Add(new NpcInfo(
                Encoding.ASCII.GetString(npc, r, nameLen),
                npc[r + 0x19] | (npc[r + 0x1A] << 8),
                npc[r + 0x1B] | (npc[r + 0x1C] << 8),
                npc[r + 0x1D], npc[r + 0x1E], npc[r + 0x21], npc[r + 0x26], npc[r + 0x24]));
        }
        return list;
    }
}
