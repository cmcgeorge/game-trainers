using System.Runtime.CompilerServices;
using LegendOfGrimrock1Trainer.Game;

namespace LegendOfGrimrock1Trainer.ViewModels;

/// <summary>Shared plumbing for a grid row that writes into the game when a cell is committed.</summary>
public abstract class GameRowViewModel : ObservableObject
{
    /// <summary>The session this row belongs to.</summary>
    protected IGameHost Host { get; }

    /// <summary>Set while a refresh is pushing values in, so a setter does not write them straight back.</summary>
    protected bool Refreshing { get; private set; }

    /// <summary>Wraps the host.</summary>
    protected GameRowViewModel(IGameHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
    }

    /// <summary>Applies <paramref name="update"/> without triggering any write-back.</summary>
    protected void Refresh(Action update)
    {
        Refreshing = true;
        try { update(); }
        finally { Refreshing = false; }
    }

    /// <summary>
    /// Whether a refresh may overwrite a value the user can type into. False while an editor has
    /// focus, so a half-typed number is not replaced four times a second by a value that is still
    /// moving in the game.
    /// </summary>
    protected bool MayReplaceEditableValues => !Host.EditorHasFocus;

    /// <summary>
    /// Gate for every editable property: refuses the edit — reverting the bound cell — when writes are
    /// off or the value is out of range, so the grid never shows a number the game did not receive.
    /// </summary>
    protected bool Allow(bool outOfRange, string message, [CallerMemberName] string? name = null)
    {
        if (Refreshing) return false;
        if (outOfRange)
        {
            Host.Report(message);
            OnPropertyChanged(name);
            return false;
        }
        if (!Host.WritesAllowed)
        {
            Host.Report("Writes are disabled — the edit was not applied.");
            OnPropertyChanged(name);
            return false;
        }
        return true;
    }

    /// <summary>Gate for an edit with no range to check, such as a checkbox.</summary>
    protected bool Allow([CallerMemberName] string? name = null) => Allow(false, "", name);

    /// <summary>
    /// Reports that an edit did not reach the game and puts <paramref name="field"/> back to what it
    /// held before, so the bound control reverts rather than showing a number the game never
    /// received. Refusals discovered after the range check — a lost attachment, a slot that stopped
    /// resolving — go through here.
    /// </summary>
    protected void Reject<T>(string message, ref T field, T previous, string property)
    {
        Host.Report(message);
        field = previous;                       // the backing field, so no setter re-enters
        OnPropertyChanged(property);
    }

    /// <summary>Reports the outcome of an action and asks for a redraw.</summary>
    protected void Apply(ActionResult result)
    {
        Host.Report(result.Attempted == 0
            ? result.Summary
            : $"{result.Summary} ({result.Applied}/{result.Attempted} written)");
        Host.RequestRefresh();
    }
}

/// <summary>
/// One row of a champion's <c>stats</c> table.
///
/// Setting <see cref="Value"/> writes the game's <c>value</c>, and its <c>max</c> alongside where
/// that is what Grimrock's own model means: a score keeps the same number in both fields, while a bar
/// has its cap raised to fit a larger value but never lowered to fit a smaller one.
/// </summary>
public sealed class StatRowViewModel : GameRowViewModel
{
    private readonly int _championIndex;

    /// <summary>The game's own key, e.g. <c>resist_fire</c>.</summary>
    public string Name { get; }

    /// <summary>The label the game shows, e.g. "Resist Fire".</summary>
    public string UiName { get; }

    /// <summary>Whether this stat is a bar (health, energy) rather than a score.</summary>
    public bool IsResource { get; }

    /// <summary>Creates a row for one stat of one champion.</summary>
    public StatRowViewModel(IGameHost host, int championIndex, StatSnapshot stat) : base(host)
    {
        _championIndex = championIndex;
        Name = stat.Name;
        UiName = stat.UiName;
        IsResource = GameTables.ResourceStats.Contains(stat.Name);
        Update(stat, initial: true);
    }

    private double _value;
    /// <summary>Current value. Setting it writes the stat.</summary>
    public double Value
    {
        get => _value;
        set
        {
            if (!Allow(value < 0 || value > GameFacts.MaxStatValue,
                    $"{UiName} must be between 0 and {GameFacts.MaxStatValue}.")) return;
            double previous = _value;
            if (!SetField(ref _value, value)) return;
            if (!Write(value))
            {
                Reject("Not attached to a loaded game — the edit was not applied.",
                    ref _value, previous, nameof(Value));
                return;
            }
            // Retarget an active freeze, so editing a frozen stat holds the new number rather than
            // being dragged straight back to the old one on the next tick.
            if (_frozen) _freezeTarget = value;
        }
    }

    private double _max;
    /// <summary>Cap of the stat, as the game holds it.</summary>
    public double Max { get => _max; private set => SetField(ref _max, value); }

    private bool _frozen;
    private double _freezeTarget;

    /// <summary>Whether the session re-writes this stat every tick.</summary>
    public bool Frozen
    {
        get => _frozen;
        set
        {
            if (!Allow()) return;
            if (!SetField(ref _frozen, value)) return;
            if (value) _freezeTarget = _value;
            Host.Report(value ? $"Freezing {UiName} at {_freezeTarget:0}." : $"Released {UiName}.");
            Host.RequestRefresh();
        }
    }

    /// <summary>
    /// Value the freeze re-applies each tick.
    ///
    /// Latched when <see cref="Frozen"/> is switched on, deliberately <b>not</b> re-read from
    /// <see cref="Value"/>: the refresh overwrites <c>Value</c> with whatever the game currently
    /// holds, so a target derived from it would follow the damage down and the "freeze" would
    /// oscillate between the two numbers instead of holding one.
    /// </summary>
    public double FreezeTarget => _freezeTarget;

    /// <summary>Restores a freeze that survived a champion-list rebuild.</summary>
    public void RestoreFreeze(double target) => Refresh(() =>
    {
        _freezeTarget = target;
        SetField(ref _frozen, true, nameof(Frozen));
    });

    /// <summary>Pushes fresh numbers in without writing them back.</summary>
    public void Update(StatSnapshot stat, bool initial = false) => Refresh(() =>
    {
        // A frozen stat shows the number it is held at, not the reading that is about to be
        // overwritten — otherwise the cell flickers between the two every tick.
        // A brand-new row always takes the game's value: it has nothing half-typed to protect, and
        // leaving it at zero would both mis-report the stat and let a freeze latch onto that zero.
        if (_frozen) SetField(ref _value, _freezeTarget, nameof(Value));
        else if (initial || MayReplaceEditableValues) SetField(ref _value, stat.Value, nameof(Value));
        Max = stat.Max;
    });

    private bool Write(double value)
    {
        var actions = Host.Actions;
        var champion = Host.ResolveChampion(_championIndex);
        if (actions is null || champion is null) return false;
        var result = actions.SetStat(champion, Name, value);
        Apply(result);
        return result.Complete;
    }
}

/// <summary>One row of a champion's <c>skills</c> array.</summary>
public sealed class SkillRowViewModel : GameRowViewModel
{
    private readonly int _championIndex;

    /// <summary>The game's own key, e.g. <c>fire_magic</c>.</summary>
    public string Name { get; }

    /// <summary>The label the game shows, e.g. "Fire Magic".</summary>
    public string UiName { get; }

    /// <summary>Creates a row for one trained skill.</summary>
    public SkillRowViewModel(IGameHost host, int championIndex, SkillSnapshot skill) : base(host)
    {
        _championIndex = championIndex;
        Name = skill.Name;
        UiName = skill.UiName;
        Update(skill, initial: true);
    }

    private int _level;
    /// <summary>Trained level, 0..50. Setting it writes the skill.</summary>
    public int Level
    {
        get => _level;
        set
        {
            if (!Allow(value < 0 || value > GameFacts.MaxSkillLevel,
                    $"A skill level must be between 0 and {GameFacts.MaxSkillLevel}.")) return;
            int previous = _level;
            if (!SetField(ref _level, value)) return;

            var actions = Host.Actions;
            var champion = Host.ResolveChampion(_championIndex);
            var skill = champion?.Skills.FirstOrDefault(s => s.Name == Name);
            if (actions is null || skill is null)
            {
                Reject("Not attached to a loaded game — the edit was not applied.",
                    ref _level, previous, nameof(Level));
                return;
            }
            var result = actions.SetSkill(skill, value);
            Apply(result);
            if (!result.Complete)
                Reject($"{UiName} was not written — the game did not accept it.",
                    ref _level, previous, nameof(Level));
        }
    }

    /// <summary>Pushes a fresh level in without writing it back.</summary>
    public void Update(SkillSnapshot skill, bool initial = false) => Refresh(() =>
    {
        if (initial || MayReplaceEditableValues) SetField(ref _level, skill.Level, nameof(Level));
    });
}

/// <summary>One row of a champion's <c>conditions</c> table.</summary>
public sealed class ConditionRowViewModel : GameRowViewModel
{
    /// <summary>Longest duration the trainer will set, in seconds — an hour of game time.</summary>
    public const double MaxTimer = 3600;

    private readonly int _championIndex;

    /// <summary>The game's own key, e.g. <c>fire_shield</c>.</summary>
    public string Name { get; }

    /// <summary>The label the game shows, e.g. "Fire Shield".</summary>
    public string UiName { get; }

    /// <summary>Whether the condition helps, hurts, or is bookkeeping.</summary>
    public ConditionKind Kind { get; }

    /// <summary>Whether the game counts a timer down for this condition.</summary>
    public bool IsTimed { get; }

    /// <summary>Word form of <see cref="Kind"/>, for the grid.</summary>
    public string KindLabel => Kind switch
    {
        ConditionKind.Harmful => "harmful",
        ConditionKind.Beneficial => "helpful",
        _ => "status",
    };

    /// <summary>Creates a row for one condition.</summary>
    public ConditionRowViewModel(IGameHost host, int championIndex, ConditionSnapshot condition) : base(host)
    {
        _championIndex = championIndex;
        Name = condition.Name;
        UiName = condition.UiName;
        Kind = condition.Kind;
        IsTimed = GameTables.TimedConditions.Contains(condition.Name);
        Update(condition, initial: true);
    }

    /// <summary>Duration used when a condition is switched on without a timer already set.</summary>
    private const double DefaultTimer = 60;

    private bool _active;
    /// <summary>Whether the condition is currently on the champion. Setting it toggles the condition.</summary>
    public bool Active
    {
        get => _active;
        set
        {
            if (!Allow()) return;
            if (_active == value) return;
            Write(value, value && IsTimed ? (_timer > 0 ? _timer : DefaultTimer) : 0);
        }
    }

    private double _timer;
    /// <summary>Remaining seconds. Setting it re-arms the condition for that long.</summary>
    public double Timer
    {
        get => _timer;
        set
        {
            // Refuse rather than quietly switch the condition on: Burdened, Overloaded and the
            // Level Up marker carry no duration the game counts down, and typing a number into their
            // Seconds cell would apply the condition — Overloaded stops the champion moving — while
            // the cell snapped straight back to zero with no sign anything had happened.
            if (!Allow(!IsTimed, $"{UiName} has no timer — the game recomputes it every frame.")) return;
            if (!Allow(value < 0 || value > MaxTimer,
                    $"A condition timer must be between 0 and {MaxTimer:0} seconds.")) return;
            if (Math.Abs(_timer - value) < TimerTolerance) return;
            Write(value > 0, value);
        }
    }

    /// <summary>
    /// How close two durations must be to count as unchanged. The grid renders with
    /// <c>StringFormat=0</c>, so a value that round-trips through the cell comes back rounded — a
    /// tolerance of <c>double.Epsilon</c> would read as one but behave as exact equality and let
    /// every redraw fire a redundant write.
    /// </summary>
    private const double TimerTolerance = 0.5;

    /// <summary>Pushes fresh state in without writing it back.</summary>
    public void Update(ConditionSnapshot condition, bool initial = false) => Refresh(() =>
    {
        SetField(ref _active, condition.Value != 0, nameof(Active));
        if (initial || MayReplaceEditableValues) SetField(ref _timer, condition.Timer, nameof(Timer));
    });

    private void Write(bool active, double timer)
    {
        var actions = Host.Actions;
        var champion = Host.ResolveChampion(_championIndex);
        var condition = champion?.Condition(Name);
        if (actions is null || champion is null || condition is null)
        {
            Host.Report("Not attached to a loaded game — the edit was not applied.");
            OnPropertyChanged(nameof(Active));
            OnPropertyChanged(nameof(Timer));
            return;
        }

        int applied = 0, attempted = 0;
        if (condition.ValueSlot != 0)
        {
            attempted++;
            if (actions.SetConditionValue(condition, active ? 1 : 0)) applied++;
        }
        if (condition.TimerSlot != 0 && IsTimed)
        {
            attempted++;
            if (actions.SetConditionTimer(condition, active ? timer : 0)) applied++;
        }

        // Only adopt the requested state when it actually reached the game; otherwise re-raise so
        // the checkbox and the cell fall back to what the next refresh reads.
        if (applied > 0 && applied == attempted)
        {
            Refresh(() =>
            {
                SetField(ref _active, active, nameof(Active));
                SetField(ref _timer, active && IsTimed ? timer : 0, nameof(Timer));
            });
        }
        else
        {
            OnPropertyChanged(nameof(Active));
            OnPropertyChanged(nameof(Timer));
        }

        Apply(new ActionResult(applied, attempted,
            active ? $"{UiName} on{(IsTimed ? $" for {timer:0}s" : "")}" : $"{UiName} cleared"));
    }
}

/// <summary>One run statistic, e.g. "Secrets Found". Read-only: the game recomputes most of them.</summary>
public sealed class StatisticRowViewModel : ObservableObject
{
    /// <summary>The label the game shows.</summary>
    public string UiName { get; }

    /// <summary>Creates a row for one statistic.</summary>
    public StatisticRowViewModel(string uiName, double value)
    {
        UiName = uiName;
        _value = value;
    }

    private double _value;
    /// <summary>Current value.</summary>
    public double Value { get => _value; private set => SetField(ref _value, value); }

    /// <summary>Pushes a fresh value in.</summary>
    public void Update(double value) => Value = value;
}

/// <summary>One dungeon level, for the map tab.</summary>
public sealed class MapRowViewModel : ObservableObject
{
    private int _level;
    /// <summary>1-based dungeon level.</summary>
    public int Level { get => _level; private set => SetField(ref _level, value); }

    private string _name;
    /// <summary>
    /// Level name, e.g. "Pillars of Light". Refreshed rather than fixed at construction: the rows are
    /// only rebuilt when the level <i>count</i> changes, so loading a custom dungeon with the same
    /// number of levels would otherwise leave the previous campaign's names on screen.
    /// </summary>
    public string Name { get => _name; private set => SetField(ref _name, value); }

    /// <summary>Creates a row for one level.</summary>
    public MapRowViewModel(MapSnapshot map)
    {
        _level = map.Level;
        _name = map.Name;
        _size = $"{map.Width} x {map.Height}";
        _visited = map.Visited;
    }

    private string _size;
    /// <summary>Map dimensions in tiles.</summary>
    public string Size { get => _size; private set => SetField(ref _size, value); }

    private bool _visited;
    /// <summary>Whether the party has ever been on this level.</summary>
    public bool Visited { get => _visited; private set => SetField(ref _visited, value); }

    private bool _isCurrent;
    /// <summary>Whether the party is standing on this level right now.</summary>
    public bool IsCurrent { get => _isCurrent; set => SetField(ref _isCurrent, value); }

    /// <summary>Label for the list.</summary>
    public string Display => $"{Level}. {Name}{(IsCurrent ? "  ← party" : "")}";

    /// <summary>Pushes fresh state in.</summary>
    public void Update(MapSnapshot map, int partyLevel)
    {
        Level = map.Level;
        Name = map.Name;
        Size = $"{map.Width} x {map.Height}";
        Visited = map.Visited;
        IsCurrent = map.Level == partyLevel;
        OnPropertyChanged(nameof(Display));
    }
}
