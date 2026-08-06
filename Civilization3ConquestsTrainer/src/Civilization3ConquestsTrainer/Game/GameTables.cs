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
/// <param name="Class">Land, sea or air — see <see cref="Civ3Layout.UnitTypeClass"/>.</param>
/// <param name="Abilities">
/// The ability bitfield, tested through <see cref="Civ3Layout.UnitTypeHasAbility"/>. What makes a type
/// an army or a great leader rather than an ordinary unit.
/// </param>
public sealed record UnitTypeInfo(int Id, string Name, int Attack, int Defence, int Movement, int Cost,
                                  int Class = Civ3Layout.UnitClassLand, int Abilities = 0)
{
    public string Stats => $"A{Attack} D{Defence} M{Movement}  {Cost} shields";

    /// <summary>Whether this type carries an <c>enum UnitTypeAbilities</c> bit.</summary>
    public bool Has(int abilityBit) => Civ3Layout.UnitTypeHasAbility(Abilities, abilityBit);

    /// <summary>"land", "sea", "air", or the raw number if the field held something else.</summary>
    public string ClassName => Class switch
    {
        Civ3Layout.UnitClassLand => "land",
        Civ3Layout.UnitClassSea => "sea",
        Civ3Layout.UnitClassAir => "air",
        _ => $"class {Class}",
    };
}

/// <summary>
/// One terrain job from the loaded rules database — what a worker can be told to do, and what it costs.
/// </summary>
/// <param name="TurnToComplete">
/// Base cost in worker-turns, before the game multiplies it by the terrain factor of the tile being
/// worked. See <see cref="Civ3Layout.WorkerJobTurnToComplete"/>.
/// </param>
public sealed record WorkerJobInfo(int Id, string Name, int TurnToComplete)
{
    public string Display => $"{Name} ({TurnToComplete})";
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

    /// <summary>Worker jobs, indexed by <c>Job_ID</c>. Empty when the table could not be read.</summary>
    public IReadOnlyList<WorkerJobInfo> WorkerJobs { get; }

    /// <summary>
    /// Address of the live <c>Worker_Job</c> table in the target, or 0 when it was not read. Held so the
    /// job costs can be written as well as displayed — this is the one table the trainer edits, and the
    /// address is kept here rather than re-derived at the call site so the read and the write cannot
    /// disagree about where the table is.
    /// </summary>
    public nuint WorkerJobsTable { get; }

    /// <summary>Stride the worker-job table was actually read at (normally <see cref="Civ3Layout.WorkerJobStride"/>).</summary>
    public int WorkerJobStride { get; }

    /// <summary>
    /// The unit type the loaded ruleset uses for an <b>army</b>, or -1 when it could not be established.
    ///
    /// <para>Read from <c>General.BuildArmyUnitID</c> — the same field <c>Unit_form_army</c> reads when
    /// the game builds one — and then cross-examined: the type it names must exist in the table and must
    /// actually carry the <c>Army</c> ability. Two unrelated things have to agree before this is
    /// believed, so a modded ruleset that moved the field produces -1 rather than a wrong answer.</para>
    /// </summary>
    public int ArmyUnitTypeId { get; }

    /// <summary>
    /// The unit type the loaded ruleset uses for a <b>great leader</b>, or -1 when it could not be
    /// established. Same two-sided check as <see cref="ArmyUnitTypeId"/>, against the <c>Leader</c>
    /// ability.
    /// </summary>
    public int GreatLeaderUnitTypeId { get; }

    /// <summary>
    /// Whether the unit types' land/sea/air field can be trusted enough to filter the type list with.
    ///
    /// <para>The offset is the one <see cref="Civ3Layout.UnitTypeClass"/> field that was not read out of
    /// the game's code, so it has to earn its use: every type in the loaded ruleset must hold one of the
    /// three domains, and at least two distinct domains must appear (any real Civ3 ruleset has land and
    /// sea units, and a field of all-zeros is exactly what a wrong offset most often looks like). When
    /// this is false the trainer offers every unit type rather than filtering on a field it cannot
    /// vouch for.</para>
    /// </summary>
    public bool UnitClassesUsable { get; }

    private GameTables(IReadOnlyList<RaceInfo> races, IReadOnlyList<UnitTypeInfo> unitTypes,
                       IReadOnlyList<WorkerJobInfo> workerJobs, nuint workerJobsTable, int workerJobStride,
                       int armyUnitTypeId, int greatLeaderUnitTypeId, bool unitClassesUsable)
    {
        Races = races;
        UnitTypes = unitTypes;
        WorkerJobs = workerJobs;
        WorkerJobsTable = workerJobsTable;
        WorkerJobStride = workerJobStride;
        ArmyUnitTypeId = armyUnitTypeId;
        GreatLeaderUnitTypeId = greatLeaderUnitTypeId;
        UnitClassesUsable = unitClassesUsable;
    }

    /// <summary>An empty set, used before a game is located.</summary>
    public static GameTables Empty { get; } = new(
        Array.Empty<RaceInfo>(), Array.Empty<UnitTypeInfo>(), Array.Empty<WorkerJobInfo>(), 0,
        Civ3Layout.WorkerJobStride, -1, -1, false);

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

    /// <summary>The record for a unit type, or null when the id is unknown or the table was not read.</summary>
    public UnitTypeInfo? UnitType(int typeId)
        => typeId >= 0 && typeId < UnitTypes.Count ? UnitTypes[typeId] : null;

    /// <summary>
    /// The unit types a unit of <paramref name="unitClass"/> may be turned into: its own domain when the
    /// class field earned its trust, and otherwise the whole table. Never a partial list built on a
    /// field that might not be the class — offering too much is recoverable, hiding the type someone
    /// wanted is not.
    /// </summary>
    public IReadOnlyList<UnitTypeInfo> TypesInClass(int unitClass)
    {
        if (!UnitClassesUsable) return UnitTypes;
        var list = new List<UnitTypeInfo>();
        foreach (var t in UnitTypes) if (t.Class == unitClass) list.Add(t);
        return list.Count > 0 ? list : UnitTypes;
    }

    /// <summary>Job name for a job id, empty for an idle unit (<c>-1</c>), or a placeholder.</summary>
    public string WorkerJobName(int jobId)
    {
        if (jobId < 0) return "";
        return jobId < WorkerJobs.Count ? WorkerJobs[jobId].Name : $"Job {jobId}";
    }

    /// <summary>The job a unit is doing, or null when it is idle or the table was not read.</summary>
    public WorkerJobInfo? WorkerJob(int jobId)
        => jobId >= 0 && jobId < WorkerJobs.Count ? WorkerJobs[jobId] : null;

    /// <summary>Reads the tables from a located game. Never throws; returns <see cref="Empty"/> on failure.</summary>
    public static GameTables Read(IMemorySource mem, Civ3Location loc)
    {
        try
        {
            var races = ReadRaces(mem, loc.BicData);
            var units = ReadUnitTypes(mem, loc.BicData);
            var (jobs, jobTable, jobStride) = ReadWorkerJobs(mem, loc.BicData);
            return new GameTables(races, units, jobs, jobTable, jobStride,
                ReadSpecialUnitType(mem, loc.BicData, Civ3Layout.BicArmyUnitType, Civ3Layout.UnitAbilityArmy, units),
                ReadSpecialUnitType(mem, loc.BicData, Civ3Layout.BicGreatLeaderUnitType, Civ3Layout.UnitAbilityLeader, units),
                ClassesLookReal(units));
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
                ReadInt(mem, u + (nuint)Civ3Layout.UnitTypeCost),
                ReadInt(mem, u + (nuint)Civ3Layout.UnitTypeClass),
                ReadInt(mem, u + (nuint)Civ3Layout.UnitTypeAbilities)));
        }
        return list;
    }

    /// <summary>
    /// Reads one of the ruleset's nominated unit types out of <c>BIC.General</c> and refuses to believe
    /// it unless the type it names carries the ability that role implies.
    ///
    /// <para>That second test is the point. The offsets come from arithmetic over the community header
    /// (one of the two is additionally confirmed by <c>Unit_form_army</c> reading it), but an ability
    /// bit set on the type the field points at is an <i>independent</i> fact — a wrong offset would have
    /// to land on a plausible type id whose record also happens to carry the right ability. Failing that
    /// test returns -1, which switches the feature off rather than acting on a guess.</para>
    /// </summary>
    private static int ReadSpecialUnitType(IMemorySource mem, nuint bic, int bicOffset, int requiredAbility,
                                           IReadOnlyList<UnitTypeInfo> types)
    {
        if (!TryReadInt(mem, bic + (nuint)bicOffset, out int id)) return -1;
        if (id < 0 || id >= types.Count) return -1;
        return types[id].Has(requiredAbility) ? id : -1;
    }

    /// <summary>
    /// Whether the unit types' class field looks like land/sea/air rather than like whatever else may
    /// live at that offset. See <see cref="UnitClassesUsable"/> for why both halves are needed.
    /// </summary>
    private static bool ClassesLookReal(IReadOnlyList<UnitTypeInfo> types)
    {
        if (types.Count == 0) return false;
        int seen = 0;
        foreach (var t in types)
        {
            if (!Civ3Layout.IsPlausibleUnitClass(t.Class)) return false;
            seen |= 1 << t.Class;
        }
        return seen != 0 && (seen & (seen - 1)) != 0;   // at least two distinct domains
    }

    /// <summary>
    /// Reads the worker-job table, and returns where it is so the costs can be edited later.
    ///
    /// <para>This is the one table with no <c>ID</c> field, so <see cref="FindStride"/> — which proves a
    /// stride by <c>Table[i].ID == i</c> — cannot be used. The substitute is
    /// <see cref="Civ3Layout.ValidateWorkerJob"/> applied to <i>every</i> record: a printable name and a
    /// sane cost, thirteen times running at a fixed spacing, is not something arbitrary memory offers.
    /// If the expected stride fails, the same predicate drives a search rather than the table being read
    /// through a stale constant.</para>
    /// </summary>
    private static (List<WorkerJobInfo> Jobs, nuint Table, int Stride) ReadWorkerJobs(IMemorySource mem, nuint bic)
    {
        var list = new List<WorkerJobInfo>();
        int count = ReadInt(mem, bic + (nuint)Civ3Layout.BicWorkerJobCount);
        nuint table = (nuint)ReadUInt(mem, bic + (nuint)Civ3Layout.BicWorkerJobs);
        if (count is <= 0 or > 256 || !Civ3Layout.LooksLikeHeapPointer((uint)table))
            return (list, 0, Civ3Layout.WorkerJobStride);

        int stride = FindWorkerJobStride(mem, table, count);
        if (stride <= 0) return (list, 0, Civ3Layout.WorkerJobStride);

        for (int i = 0; i < count; i++)
        {
            nuint j = table + (nuint)(i * stride);
            list.Add(new WorkerJobInfo(
                i,
                ReadString(mem, j + (nuint)Civ3Layout.WorkerJobName, 32),
                ReadInt(mem, j + (nuint)Civ3Layout.WorkerJobTurnToComplete)));
        }
        return (list, table, stride);
    }

    private static int FindWorkerJobStride(IMemorySource mem, nuint table, int count)
    {
        if (WorkerJobStrideHolds(mem, table, count, Civ3Layout.WorkerJobStride)) return Civ3Layout.WorkerJobStride;
        for (int s = 0x40; s <= MaxSearchStride; s += 4)
            if (s != Civ3Layout.WorkerJobStride && WorkerJobStrideHolds(mem, table, count, s)) return s;
        return -1;
    }

    private static bool WorkerJobStrideHolds(IMemorySource mem, nuint table, int count, int stride)
    {
        // Every record has to pass, not a sample of them: with no index to check, the only thing making
        // a false stride implausible is that it would have to produce a valid record every single time.
        for (int i = 0; i < count; i++)
        {
            byte[] rec = mem.Read(table + (nuint)(i * stride), Civ3Layout.WorkerJobRecordProbeBytes);
            if (!Civ3Layout.ValidateWorkerJob(rec)) return false;
        }
        return true;
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
