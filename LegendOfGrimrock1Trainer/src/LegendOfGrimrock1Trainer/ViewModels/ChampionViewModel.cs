using System.Collections.ObjectModel;
using LegendOfGrimrock1Trainer.Game;

namespace LegendOfGrimrock1Trainer.ViewModels;

/// <summary>
/// One of the party's four champions: identity, level and experience, the twelve stats, the trained
/// skills, and the condition list.
///
/// Rows are updated in place rather than rebuilt, so a grid keeps its selection and a half-typed
/// cell is not yanked away four times a second. The collections are only rebuilt when the set of
/// keys actually changes — which for stats and conditions never happens after character creation,
/// and for skills happens the first time a champion trains a new one.
/// </summary>
public sealed class ChampionViewModel : GameRowViewModel
{
    /// <summary>1-based slot in <c>party.champions</c>.</summary>
    public int Index { get; }

    /// <summary>Creates a view-model for one champion slot.</summary>
    public ChampionViewModel(IGameHost host, ChampionSnapshot snapshot) : base(host)
    {
        Index = snapshot.Index;
        Update(snapshot, initial: true);
    }

    private string _name = "";
    /// <summary>Champion name, as the game shows it.</summary>
    public string Name { get => _name; private set => SetField(ref _name, value); }

    private string _description = "";
    /// <summary>Race, class and sex on one line.</summary>
    public string Description { get => _description; private set => SetField(ref _description, value); }

    private bool _enabled;
    /// <summary>Whether this slot holds a living champion. A dead one still occupies its slot.</summary>
    public bool Enabled { get => _enabled; private set => SetField(ref _enabled, value); }

    /// <summary>Header for the champion's tab.</summary>
    public string TabHeader => Enabled ? $"{Index}. {Name}" : $"{Index}. {Name} (down)";

    private int _level;
    /// <summary>Character level. Setting it writes the class instance's level.</summary>
    public int Level
    {
        get => _level;
        set
        {
            if (!Allow(value < 1 || value > GameFacts.MaxChampionLevel,
                    $"A level must be between 1 and {GameFacts.MaxChampionLevel}.")) return;
            int previous = _level;
            if (!SetField(ref _level, value)) return;
            WithChampion((a, c) => a.SetLevel(c, value), ref _level, previous);
        }
    }

    private double _experience;
    /// <summary>Accumulated experience. Setting it writes the class instance's total.</summary>
    public double Experience
    {
        get => _experience;
        set
        {
            if (!Allow(value < 0, "Experience cannot be negative.")) return;
            double previous = _experience;
            if (!SetField(ref _experience, value)) return;
            WithChampion((a, c) => a.SetExperience(c, value), ref _experience, previous);
        }
    }

    private double _nextLevel;
    /// <summary>Experience the game wants for the next level.</summary>
    public double NextLevel { get => _nextLevel; private set => SetField(ref _nextLevel, value); }

    private double _food;
    /// <summary>Food, 0..1000. Setting it writes the champion's food.</summary>
    public double Food
    {
        get => _food;
        set
        {
            if (!Allow(value < 0 || value > GameFacts.MaxFood,
                    $"Food must be between 0 and {GameFacts.MaxFood}.")) return;
            double previous = _food;
            if (!SetField(ref _food, value)) return;
            WithChampion((a, c) => a.SetFood(c, value), ref _food, previous);
        }
    }

    private int _skillPoints;
    /// <summary>Unspent skill points. Setting it writes the count and the sheet's "Level Up" badge.</summary>
    public int SkillPoints
    {
        get => _skillPoints;
        set
        {
            if (!Allow(value < 0 || value > TrainerActions.MaxSkillPoints,
                    $"Skill points must be between 0 and {TrainerActions.MaxSkillPoints}.")) return;
            int previous = _skillPoints;
            if (!SetField(ref _skillPoints, value)) return;
            WithChampion((a, c) => a.SetSkillPoints(c, value), ref _skillPoints, previous);
        }
    }

    private string _talents = "";
    /// <summary>Talents and traits the champion carries, comma separated.</summary>
    public string Talents { get => _talents; private set => SetField(ref _talents, value); }

    /// <summary>The champion's stats, in the game's own order.</summary>
    public ObservableCollection<StatRowViewModel> Stats { get; } = new();

    /// <summary>The champion's trained skills.</summary>
    public ObservableCollection<SkillRowViewModel> Skills { get; } = new();

    /// <summary>The champion's conditions.</summary>
    public ObservableCollection<ConditionRowViewModel> Conditions { get; } = new();

    /// <summary>Stats currently frozen, with the value each should be held at.</summary>
    public IEnumerable<(string Name, double Value)> FrozenStats =>
        Stats.Where(s => s.Frozen).Select(s => (s.Name, s.FreezeTarget));

    /// <summary>
    /// Re-applies freezes that were ticked on a previous instance of this champion's rows.
    ///
    /// The row objects are thrown away whenever the champion list is rebuilt — which happens on any
    /// tick where a champion momentarily failed to parse, not only when the party really changed —
    /// and a freeze silently switching itself off is worse than useless.
    /// </summary>
    public void RestoreFreezes(IEnumerable<(string Name, double Value)> frozen)
    {
        foreach (var (name, value) in frozen)
            Stats.FirstOrDefault(s => s.Name == name)?.RestoreFreeze(value);
    }

    /// <summary>
    /// Pushes a fresh snapshot into every bound value. <paramref name="initial"/> forces the editable
    /// fields to take the game's numbers even while an editor has focus: a brand-new row has nothing
    /// half-typed to protect, and leaving it at zero would show the wrong sheet.
    /// </summary>
    public void Update(ChampionSnapshot snapshot, bool initial = false)
    {
        Refresh(() =>
        {
            Name = snapshot.Name;
            Enabled = snapshot.Enabled;
            Description = string.Join(' ', new[] { snapshot.Race, snapshot.ClassName }
                .Where(s => !string.IsNullOrWhiteSpace(s)))
                + (string.IsNullOrWhiteSpace(snapshot.Sex) ? "" : $" ({snapshot.Sex})");
            NextLevel = snapshot.NextLevel;
            if (initial || MayReplaceEditableValues)
            {
                SetField(ref _level, snapshot.Level, nameof(Level));
                SetField(ref _experience, snapshot.Experience, nameof(Experience));
                SetField(ref _food, snapshot.Food, nameof(Food));
                SetField(ref _skillPoints, snapshot.SkillPoints, nameof(SkillPoints));
            }
            Talents = snapshot.Talents.Count == 0
                ? "(none)"
                : string.Join(", ", snapshot.Talents.Select(GameTables.Humanise));
            OnPropertyChanged(nameof(TabHeader));
        });

        Sync(Stats, snapshot.Stats, s => s.Name, s => s.Name,
            s => new StatRowViewModel(Host, Index, s), (row, s) => row.Update(s, initial));
        Sync(Skills, snapshot.Skills, s => s.Name, s => s.Name,
            s => new SkillRowViewModel(Host, Index, s), (row, s) => row.Update(s, initial));
        Sync(Conditions, snapshot.Conditions, c => c.Name, c => c.Name,
            c => new ConditionRowViewModel(Host, Index, c), (row, c) => row.Update(c, initial));
    }

    /// <summary>
    /// Reconciles a bound collection with a fresh list: updates rows whose key survived, and rebuilds
    /// only when the key set itself changed.
    /// </summary>
    private static void Sync<TRow, TItem>(
        ObservableCollection<TRow> rows,
        IReadOnlyList<TItem> items,
        Func<TRow, string> rowKey,
        Func<TItem, string> itemKey,
        Func<TItem, TRow> create,
        Action<TRow, TItem> update)
    {
        bool sameShape = rows.Count == items.Count;
        if (sameShape)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rowKey(rows[i]) == itemKey(items[i])) continue;
                sameShape = false;
                break;
            }
        }

        if (!sameShape)
        {
            rows.Clear();
            foreach (var item in items) rows.Add(create(item));
            return;
        }

        for (int i = 0; i < rows.Count; i++) update(rows[i], items[i]);
    }

    /// <summary>
    /// Runs an edit against a freshly resolved champion, and puts <paramref name="field"/> back when
    /// it did not reach the game — the same discipline the stat and skill rows follow, so no bound
    /// control is left showing a number the game never received.
    /// </summary>
    private void WithChampion<T>(Func<TrainerActions, ChampionSnapshot, ActionResult> action,
                                 ref T field, T previous,
                                 [System.Runtime.CompilerServices.CallerMemberName] string property = "")
    {
        var actions = Host.Actions;
        var champion = Host.ResolveChampion(Index);
        if (actions is null || champion is null)
        {
            Reject("Not attached to a loaded game — the edit was not applied.", ref field, previous, property);
            return;
        }

        var result = action(actions, champion);
        Apply(result);
        if (!result.Complete)
            Reject("The game did not accept the edit.", ref field, previous, property);
    }
}
