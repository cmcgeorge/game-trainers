using Civilization3ConquestsTrainer.Game;

namespace Civilization3ConquestsTrainer.ViewModels;

/// <summary>
/// One unit on the map.
///
/// Two fields read backwards from what the UI shows and both are worth stating plainly: the record
/// stores hit points <i>lost</i> and movement points <i>spent</i>, not remaining. So "Full heal"
/// writes zero damage and "Refresh moves" writes zero movement — and the maximum hit points a unit
/// has are never stored at all, being derived from its type and veteran level.
/// </summary>
public sealed class UnitRowViewModel : ObservableObject
{
    private readonly IGameHost _host;
    private readonly nuint _body;

    /// <summary>Slot in the unit container; also the unit's own id.</summary>
    public int Slot { get; }

    private string _typeName = "";
    public string TypeName { get => _typeName; private set => SetField(ref _typeName, value); }

    private string _owner = "";
    public string Owner { get => _owner; private set => SetField(ref _owner, value); }

    /// <summary>Whether this unit belongs to the civ the human is playing.</summary>
    public bool IsMine { get; private set; }

    private int _x, _y;
    public int X { get => _x; private set => SetField(ref _x, value); }
    public int Y { get => _y; private set => SetField(ref _y, value); }
    public string Position => $"{_x}, {_y}";

    private int _damage;
    /// <summary>Hit points lost. Zero is a fully healthy unit.</summary>
    public int Damage
    {
        get => _damage;
        set
        {
            if (!Reject(value < 0, "Damage cannot be negative — edit rejected.")) return;
            if (!SetField(ref _damage, value)) return;
            _host.WriteInt32(_body + (nuint)Civ3Layout.UnitDamage, value);
        }
    }

    private int _movesUsed;
    /// <summary>Movement points already spent this turn. Zero is a unit that has not moved.</summary>
    public int MovesUsed
    {
        get => _movesUsed;
        set
        {
            if (!Reject(value < 0, "Movement used cannot be negative — edit rejected.")) return;
            if (!SetField(ref _movesUsed, value)) return;
            _host.WriteInt32(_body + (nuint)Civ3Layout.UnitMoves, value);
        }
    }

    private int _experience;
    /// <summary>Veteran ladder: 0 conscript, 1 regular, 2 veteran, 3 elite.</summary>
    public int Experience
    {
        get => _experience;
        set
        {
            // Clamped rather than rejected: an out-of-range veteran level has an obvious right answer.
            int clamped = Math.Clamp(value, 0, GameFacts.MaxCombatExperience);
            if (!Reject(false, "")) return;
            if (!SetField(ref _experience, clamped)) { OnPropertyChanged(); return; }
            OnPropertyChanged();   // snap the cell back if the typed value was clamped
            _host.WriteInt32(_body + (nuint)Civ3Layout.UnitExperience, clamped);
        }
    }

    private int _jobId = -1;
    /// <summary>The <c>Worker_Jobs</c> ordinal this unit is performing, or -1 when it is idle.</summary>
    public int JobId { get => _jobId; private set => SetField(ref _jobId, value); }

    private string _jobName = "";
    /// <summary>"Road", "Irrigation", … or empty for a unit that is not working.</summary>
    public string JobName { get => _jobName; private set => SetField(ref _jobName, value); }

    private int _jobProgress;
    /// <summary>
    /// Worker-turns already put into the current job. This counts <i>up</i> toward the job's cost, so a
    /// larger number is closer to done — the opposite reading from <see cref="Damage"/> and
    /// <see cref="MovesUsed"/> on the same row.
    /// </summary>
    public int JobProgress
    {
        get => _jobProgress;
        set
        {
            if (!Reject(value < 0, "Job progress cannot be negative — edit rejected.")) return;
            if (!SetField(ref _jobProgress, value)) return;
            _host.WriteInt32(_body + (nuint)Civ3Layout.UnitJobValue, value);
        }
    }

    /// <summary>Whether this unit is mid-job, and so has something for "Finish job" to do.</summary>
    public bool IsWorking => _jobId >= 0;

    /// <summary>
    /// What <see cref="FinishJob"/> would write: the job's base cost scaled to clear any terrain.
    /// Cached from the ruleset on each refresh, so the button never has to guess at a cost the loaded
    /// scenario or mod may have changed.
    /// </summary>
    private int _jobWorkToFinish;

    private bool _freeze;

    /// <summary>
    /// Re-zeroes damage and spent movement on every poll tick.
    ///
    /// <para><b>This heals; it does not shield.</b> Civ3 resolves a whole battle inside a single call
    /// to <c>Fighter_begin</c> — every round, the kill, and the score update happen before that call
    /// returns — so there is no instant during combat at which a 500 ms poll could intervene. A frozen
    /// unit that survives a battle is restored to full before the next one; a frozen unit that loses a
    /// battle dies exactly as it would have anyway. Making a unit genuinely unkillable is not possible
    /// by writing data alone: maximum hit points are not stored on the unit, they are computed by
    /// <c>Unit_get_max_hp</c> from the unit type and veteran level.</para>
    /// </summary>
    public bool Freeze { get => _freeze; set => SetField(ref _freeze, value); }

    /// <summary>Refuses an out-of-range edit, or any edit at all while writes are blocked.</summary>
    private bool Reject(bool outOfRange, string message, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (outOfRange) { _host.Report(message); OnPropertyChanged(name); return false; }
        if (!_host.WritesAllowed) { _host.Report("Writes are disabled — the edit was not applied."); OnPropertyChanged(name); return false; }
        return true;
    }

    public UnitRowViewModel(IGameHost host, nuint body, int slot)
    {
        _host = host;
        _body = body;
        Slot = slot;
    }

    /// <summary>Re-reads this unit. Returns false if the record no longer looks like a unit (it died).</summary>
    public bool Refresh(GameTables tables, Civ3Location loc)
    {
        byte[] b = _host.Read(_body, Civ3Layout.UnitRecordProbeBytes);
        // Same full predicate the locator uses, not just an id check — a slot recycled for something
        // else can still carry the right id while its position and damage are nonsense.
        if (!Civ3Layout.ValidateUnit(b, Slot, loc.MapWidth, loc.MapHeight)) return false;

        int civ = BitConverter.ToInt32(b, Civ3Layout.UnitCivId);
        IsMine = civ == loc.HumanCivId;
        Owner = tables.RaceName(BitConverter.ToInt32(b, Civ3Layout.UnitRaceId));
        TypeName = tables.UnitTypeName(BitConverter.ToInt32(b, Civ3Layout.UnitTypeId));

        int x = BitConverter.ToInt32(b, Civ3Layout.UnitX);
        int y = BitConverter.ToInt32(b, Civ3Layout.UnitY);
        if (x != _x || y != _y) { X = x; Y = y; OnPropertyChanged(nameof(Position)); }

        int dmg = BitConverter.ToInt32(b, Civ3Layout.UnitDamage);
        if (dmg != _damage) { _damage = dmg; OnPropertyChanged(nameof(Damage)); }

        int mv = BitConverter.ToInt32(b, Civ3Layout.UnitMoves);
        if (mv != _movesUsed) { _movesUsed = mv; OnPropertyChanged(nameof(MovesUsed)); }

        int exp = BitConverter.ToInt32(b, Civ3Layout.UnitExperience);
        if (exp != _experience) { _experience = exp; OnPropertyChanged(nameof(Experience)); }

        int job = BitConverter.ToInt32(b, Civ3Layout.UnitJobId);
        if (job != _jobId)
        {
            JobId = job;
            JobName = tables.WorkerJobName(job);
            OnPropertyChanged(nameof(IsWorking));
        }
        // Re-read the cost even when the job id is unchanged: "Instant worker jobs" rewrites the
        // ruleset underneath us, and a stale cost would make Finish job write the old, larger number.
        _jobWorkToFinish = tables.WorkerJob(job) is { } info
            ? Civ3Layout.WorkerJobWorkToFinish(info.TurnToComplete)
            : 0;

        int progress = BitConverter.ToInt32(b, Civ3Layout.UnitJobValue);
        if (progress != _jobProgress) { _jobProgress = progress; OnPropertyChanged(nameof(JobProgress)); }

        return true;
    }

    /// <summary>Re-applies the freeze. Called from the poll loop.</summary>
    public void ApplyFreeze()
    {
        if (!_freeze) return;
        _host.WriteInt32(_body + (nuint)Civ3Layout.UnitDamage, 0);
        _host.WriteInt32(_body + (nuint)Civ3Layout.UnitMoves, 0);
    }

    /// <summary>
    /// Re-zeroes spent movement and nothing else — the empire-wide movement hold, as distinct from the
    /// per-row <see cref="Freeze"/>, which also clears damage.
    ///
    /// <para>Worth knowing what this unlocks for workers: Civ3 spends a worker's whole move when it puts
    /// a turn of work into a job, and it only tests whether the job is finished during that same work
    /// tick. Giving the movement back lets the job be re-ordered, which forces another tick — which is
    /// what turns <see cref="FinishJob"/> from "done next turn" into "done now".</para>
    /// </summary>
    public void HoldMoves() => _host.WriteInt32(_body + (nuint)Civ3Layout.UnitMoves, 0);

    /// <summary>Clears all accumulated damage.</summary>
    public void FullHeal() => Damage = 0;

    /// <summary>Returns every movement point for this turn.</summary>
    public void RefreshMoves() => MovesUsed = 0;

    /// <summary>Promotes to elite, the top of the veteran ladder.</summary>
    public void MakeElite() => Experience = GameFacts.MaxCombatExperience;

    /// <summary>
    /// Banks enough worker-turns for the current job to complete. Returns false, without writing, for a
    /// unit that is not working — there is no job to finish, and a poked <c>Job_Value</c> on an idle unit
    /// would just be a number nothing reads.
    ///
    /// <para>The improvement appears at the <b>turn boundary</b>, not on the spot: the game applies
    /// accumulated work during the interturn. And because progress pools across everyone on the tile,
    /// doing this to one worker of a stack finishes the job for all of them.</para>
    /// </summary>
    public bool FinishJob()
    {
        if (!IsWorking || _jobWorkToFinish <= 0) return false;
        JobProgress = _jobWorkToFinish;
        return true;
    }
}
