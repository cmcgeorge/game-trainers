using SwordOfAragonTrainer.Game;

namespace SwordOfAragonTrainer.ViewModels;

/// <summary>
/// One row of the city grid: a bindable face on a <see cref="CityRecord"/>. Setting a property writes
/// straight through to the loaded save's line buffer (clamped by the record) and tells the host the
/// save is now dirty.
/// </summary>
public sealed class CityViewModel : ObservableObject
{
    private readonly CityRecord _record;
    private readonly IEditHost _host;

    public CityViewModel(CityRecord record, IEditHost host)
    {
        _record = record;
        _host = host;
        Info = CityBook.ByName(record.Name) ?? CityBook.ByIndex(record.Index);
    }

    /// <summary>Reference-book entry for this city, when the name is one the book knows.</summary>
    public CityInfo? Info { get; }

    /// <summary>Name as the rule book spells it where possible, otherwise as the save spells it.</summary>
    public string Name => Info?.DisplayName ?? _record.Name;

    /// <summary>Map position, or an em dash for a region with no city hex.</summary>
    public string Position => _record.HasCityHex ? $"{_record.X},{_record.Y}" : "—";

    /// <summary>Whether the save is carrying this city's "changed this month" figures — i.e. you own it.</summary>
    public bool IsPlayerOwned => _record.LooksPlayerOwned;

    /// <summary>"Yours" or "Foreign", for the grid.</summary>
    public string Owner => IsPlayerOwned ? "Yours" : "Foreign";

    /// <summary>Ruler at the start of the game, from the reference book.</summary>
    public string Ruler => Info?.Ruler ?? "";

    public int Population
    {
        get => _record.Population;
        set => Apply(() => _record.Population = value, nameof(Population), "population");
    }

    public int Morale
    {
        get => _record.Morale;
        set => Apply(() => _record.Morale = value, nameof(Morale), "morale");
    }

    public int Loyalty
    {
        get => _record.Loyalty;
        set => Apply(() => _record.Loyalty = value, nameof(Loyalty), "loyalty");
    }

    public int Health
    {
        get => _record.Health;
        set => Apply(() => _record.Health = value, nameof(Health), "health");
    }

    public int TaxRate
    {
        get => _record.TaxRate;
        set => Apply(() => _record.TaxRate = value, nameof(TaxRate), "tax rate");
    }

    public int Recruits
    {
        get => _record.Recruits;
        set => Apply(() => _record.Recruits = value, nameof(Recruits), "recruits");
    }

    public int CityGold
    {
        get => _record.CityGold;
        set => Apply(() => _record.CityGold = value, nameof(CityGold), "city treasury");
    }

    /// <summary>Gold the city produced last month. Recomputed by the game each turn.</summary>
    public double Income => _record.Income;

    /// <summary>Total development across the five revenue categories, as a quick "how built up is it" figure.</summary>
    public int RevenueDevelopment =>
        _record.Develop(DevelopmentCategory.Agriculture) + _record.Develop(DevelopmentCategory.Lumber) +
        _record.Develop(DevelopmentCategory.Mining) + _record.Develop(DevelopmentCategory.Manufacture) +
        _record.Develop(DevelopmentCategory.Commerce);

    /// <summary>The seven investment categories as editable rows.</summary>
    public IReadOnlyList<DevelopmentViewModel> Development { get; private set; } = Array.Empty<DevelopmentViewModel>();

    /// <summary>Builds the development rows. Called once the row is selected, to keep loading cheap.</summary>
    public void EnsureDevelopmentRows()
    {
        if (Development.Count > 0) return;
        Development = Enum.GetValues<DevelopmentCategory>()
            .Select(c => new DevelopmentViewModel(_record, c, _host, this))
            .ToArray();
        OnPropertyChanged(nameof(Development));
    }

    /// <summary>Raises every category's development to that category's own resource ceiling.</summary>
    public void DevelopToCeiling()
    {
        _record.DevelopToResourceCeiling();
        RefreshAll();
        _host.MarkDirty($"{Name}: development raised to each category's resource ceiling");
    }

    /// <summary>Sets morale, loyalty and health to the top of the game's natural scale.</summary>
    public void RestoreMood()
    {
        _record.RestoreMood();
        RefreshAll();
        _host.MarkDirty($"{Name}: morale, loyalty and health set to {CityRecord.FullMood}");
    }

    /// <summary>Re-reads the summary figure a development row just invalidated.</summary>
    internal void NotifyDevelopmentChanged() => OnPropertyChanged(nameof(RevenueDevelopment));

    /// <summary>Re-reads every displayed value from the record.</summary>
    public void RefreshAll()
    {
        foreach (var name in new[]
                 {
                     nameof(Population), nameof(Morale), nameof(Loyalty), nameof(Health),
                     nameof(TaxRate), nameof(Recruits), nameof(CityGold), nameof(Income),
                     nameof(RevenueDevelopment),
                 })
            OnPropertyChanged(name);
        foreach (var row in Development) row.Refresh();
    }

    private void Apply(Action write, string property, string what)
    {
        write();
        OnPropertyChanged(property);                 // the record clamps, so re-read for the UI
        OnPropertyChanged(nameof(RevenueDevelopment));
        _host.MarkDirty($"{Name}: {what}");
    }
}

/// <summary>One investment category of one city, as an editable grid row.</summary>
public sealed class DevelopmentViewModel : ObservableObject
{
    private readonly CityRecord _record;
    private readonly IEditHost _host;
    private readonly CityViewModel _owner;

    public DevelopmentViewModel(CityRecord record, DevelopmentCategory category, IEditHost host,
                               CityViewModel owner)
    {
        _record = record;
        Category = category;
        _host = host;
        _owner = owner;
    }

    public DevelopmentCategory Category { get; }

    public string Name => Category.ToString();

    /// <summary>What has been built.</summary>
    public int Develop
    {
        get => _record.Develop(Category);
        set
        {
            _record.SetDevelop(Category, value);
            Refresh();
            _host.MarkDirty($"{_record.Name}: {Name} development");
        }
    }

    /// <summary>The city's natural ceiling — while Develop is below it, investment stays cheap.</summary>
    public int Resource
    {
        get => _record.Resource(Category);
        set
        {
            _record.SetResource(Category, value);
            Refresh();
            _host.MarkDirty($"{_record.Name}: {Name} resource ceiling");
        }
    }

    /// <summary>Gold per investment step (a fixed property of the city).</summary>
    public int Cost => _record.Cost(Category);

    /// <summary>Gold produced last month. Recomputed by the game each turn.</summary>
    public int Production => _record.Production(Category);

    /// <summary>True once development has caught up with the ceiling, where further steps get expensive.</summary>
    public bool AtCeiling => Develop >= Resource;

    internal void Refresh()
    {
        OnPropertyChanged(nameof(Develop));
        OnPropertyChanged(nameof(Resource));
        OnPropertyChanged(nameof(Production));
        OnPropertyChanged(nameof(AtCeiling));
        // The city grid's summary column sums this city's revenue development, so it goes stale unless
        // the owning row is told as well.
        _owner.NotifyDevelopmentChanged();
    }
}
