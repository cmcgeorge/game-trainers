using System.Text;
using Civilization3ConquestsTrainer.Memory;

namespace Civilization3ConquestsTrainer.Game;

/// <summary>One civilization from the loaded rules database.</summary>
public sealed record RaceInfo(int Id, string Leader, string Country, string Adjective, int Aggression)
{
    /// <summary>"Rome — Caesar", or just the country when the slot has no leader (the barbarians).</summary>
    public string Display => string.IsNullOrEmpty(Leader) ? Country : $"{Country} — {Leader}";
}

/// <summary>One unit type from the loaded rules database.</summary>
public sealed record UnitTypeInfo(int Id, string Name, int Attack, int Defence, int Movement, int Cost)
{
    public string Stats => $"A{Attack} D{Defence} M{Movement}  {Cost} shields";
}

/// <summary>
/// The civilization and unit-type tables, read out of the game's own <c>BIC</c> database rather than
/// curated in source.
///
/// That choice is load-bearing rather than tidy: Conquests ships nine scenarios and the community
/// ships thousands more, and each one substitutes its own civs, leaders and unit roster. A hard-coded
/// table would be right only for the unmodified epic game and would silently mislabel everything
/// else, whereas <c>BIC</c> is by definition whatever ruleset is actually loaded.
///
/// Strides are recovered by brute force rather than trusted: the only stride that makes
/// <c>Table[i].ID == i</c> hold for every entry is the real one, which also detects a layout change
/// instead of reading garbage through it.
/// </summary>
public sealed class GameTables
{
    /// <summary>Civilizations, indexed by <c>RaceID</c>. Empty when the tables could not be read.</summary>
    public IReadOnlyList<RaceInfo> Races { get; }

    /// <summary>Unit types, indexed by <c>UnitTypeID</c>. Empty when the tables could not be read.</summary>
    public IReadOnlyList<UnitTypeInfo> UnitTypes { get; }

    private GameTables(IReadOnlyList<RaceInfo> races, IReadOnlyList<UnitTypeInfo> unitTypes)
    {
        Races = races;
        UnitTypes = unitTypes;
    }

    /// <summary>An empty set, used before a game is located.</summary>
    public static GameTables Empty { get; } = new(Array.Empty<RaceInfo>(), Array.Empty<UnitTypeInfo>());

    /// <summary>Civilization label for a race id, or a placeholder when it is unknown or unset.</summary>
    public string RaceName(int raceId)
    {
        if (raceId < 0) return "(none)";
        return raceId < Races.Count ? Races[raceId].Display : $"Race {raceId}";
    }

    /// <summary>Unit-type name for a type id, or a placeholder.</summary>
    public string UnitTypeName(int typeId)
    {
        if (typeId < 0) return "(none)";
        return typeId < UnitTypes.Count ? UnitTypes[typeId].Name : $"Type {typeId}";
    }

    /// <summary>Reads both tables from a located game. Never throws; returns <see cref="Empty"/> on failure.</summary>
    public static GameTables Read(IMemorySource mem, Civ3Location loc)
    {
        try
        {
            var races = ReadRaces(mem, loc.BicData);
            var units = ReadUnitTypes(mem, loc.BicData);
            return new GameTables(races, units);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException or OverflowException)
        {
            return Empty;
        }
    }

    private static List<RaceInfo> ReadRaces(IMemorySource mem, nuint bic)
    {
        var list = new List<RaceInfo>();
        int count = ReadInt(mem, bic + (nuint)Civ3Layout.BicRacesCount);
        nuint table = (nuint)ReadUInt(mem, bic + (nuint)Civ3Layout.BicRaces);
        if (count is <= 0 or > 256 || !Civ3Layout.LooksLikeHeapPointer((uint)table)) return list;

        int stride = FindStride(mem, table, count, Civ3Layout.RaceId, Civ3Layout.RaceStride);
        if (stride <= 0) return list;

        for (int i = 0; i < count; i++)
        {
            nuint r = table + (nuint)(i * stride);
            list.Add(new RaceInfo(
                i,
                ReadString(mem, r + (nuint)Civ3Layout.RaceLeaderName, 32),
                ReadString(mem, r + (nuint)Civ3Layout.RaceCountryName, 40),
                ReadString(mem, r + (nuint)Civ3Layout.RaceAdjective, 40),
                ReadInt(mem, r + (nuint)Civ3Layout.RaceAggression)));
        }
        return list;
    }

    private static List<UnitTypeInfo> ReadUnitTypes(IMemorySource mem, nuint bic)
    {
        var list = new List<UnitTypeInfo>();
        int count = ReadInt(mem, bic + (nuint)Civ3Layout.BicUnitTypeCount);
        nuint table = (nuint)ReadUInt(mem, bic + (nuint)Civ3Layout.BicUnitTypes);
        if (count is <= 0 or > 1024 || !Civ3Layout.LooksLikeHeapPointer((uint)table)) return list;

        int stride = FindStride(mem, table, count, Civ3Layout.UnitTypeRecordId, Civ3Layout.UnitTypeStride);
        if (stride <= 0) return list;

        for (int i = 0; i < count; i++)
        {
            nuint u = table + (nuint)(i * stride);
            list.Add(new UnitTypeInfo(
                i,
                ReadString(mem, u + (nuint)Civ3Layout.UnitTypeName, 32),
                ReadInt(mem, u + (nuint)Civ3Layout.UnitTypeAttack),
                ReadInt(mem, u + (nuint)Civ3Layout.UnitTypeDefence),
                ReadInt(mem, u + (nuint)Civ3Layout.UnitTypeMovement),
                ReadInt(mem, u + (nuint)Civ3Layout.UnitTypeCost)));
        }
        return list;
    }

    /// <summary>Widest stride the search will consider — comfortably past <c>Race</c>'s 0x974.</summary>
    private const int MaxSearchStride = 0x1000;

    /// <summary>Fewest entries that must satisfy <c>Table[i].ID == i</c> for a stride to be believed.</summary>
    private const int MinProbeEntries = 2;

    /// <summary>
    /// Confirms <paramref name="expected"/> is the real stride by checking <c>Table[i].ID == i</c>, and
    /// searches only if it is not — so a layout change is recovered from rather than misread.
    ///
    /// The search ceiling has to clear the largest stride we know about: <c>Race</c> is 0x974, so a
    /// ceiling of 0x800 would make the fallback unable to rediscover the very table it exists for.
    /// A single-entry table is rejected outright rather than probed, because <c>Table[0].ID == 0</c>
    /// holds for every stride and would accept an arbitrary one.
    /// </summary>
    private static int FindStride(IMemorySource mem, nuint table, int count, int idOffset, int expected)
    {
        int probe = Math.Min(count, 8);
        if (probe < MinProbeEntries) return -1;
        if (StrideHolds(mem, table, probe, idOffset, expected)) return expected;

        for (int s = 0x40; s <= MaxSearchStride; s += 4)
            if (s != expected && StrideHolds(mem, table, probe, idOffset, s)) return s;
        return -1;
    }

    private static bool StrideHolds(IMemorySource mem, nuint table, int probe, int idOffset, int stride)
    {
        for (int i = 0; i < probe; i++)
        {
            // A failed read must reject, not read as zero — otherwise unreadable memory "matches"
            // at index 0 and a bogus stride is accepted.
            if (!TryReadInt(mem, table + (nuint)(i * stride) + (nuint)idOffset, out int id)) return false;
            if (id != i) return false;
        }
        return true;
    }

    private static bool TryReadInt(IMemorySource mem, nuint at, out int value)
    {
        byte[] b = mem.Read(at, 4);
        value = b.Length == 4 ? BitConverter.ToInt32(b) : 0;
        return b.Length == 4;
    }

    private static int ReadInt(IMemorySource mem, nuint at) => TryReadInt(mem, at, out int v) ? v : 0;

    private static uint ReadUInt(IMemorySource mem, nuint at)
    {
        byte[] b = mem.Read(at, 4);
        return b.Length == 4 ? BitConverter.ToUInt32(b) : 0;
    }

    private static string ReadString(IMemorySource mem, nuint at, int max)
    {
        byte[] b = mem.Read(at, max);
        if (b.Length == 0) return "";
        int end = Array.IndexOf(b, (byte)0);
        if (end < 0) end = b.Length;
        return Encoding.ASCII.GetString(b, 0, end).Trim();
    }
}
