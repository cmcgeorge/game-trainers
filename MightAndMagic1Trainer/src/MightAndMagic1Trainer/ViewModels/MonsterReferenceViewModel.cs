using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using MightAndMagic1Trainer.Game;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>One monster in the reference list, exposing the fields the Monsters tab binds to.</summary>
public sealed class MonsterEntryViewModel
{
    private GameMonster Monster { get; }

    public MonsterEntryViewModel(GameMonster monster) => Monster = monster;

    public int Id => Monster.Id;
    public string Name => Monster.Name;
    public string Group => Monster.Group;
    public string ExpTag => Monster.ExpTag;
    public string Description => Monster.DetailText;
}

/// <summary>
/// Read-only reference for the Monsters tab: the game's complete 195-entry bestiary grouped
/// by difficulty tier, with a name/id search box. Independent of any attached game,
/// mirroring <see cref="ItemReferenceViewModel"/>.
/// </summary>
public sealed class MonsterReferenceViewModel : ObservableObject
{
    public ICollectionView Monsters { get; }

    public MonsterReferenceViewModel()
    {
        var items = new ObservableCollection<MonsterEntryViewModel>(
            MonsterBook.Bestiary.Select(m => new MonsterEntryViewModel(m)));
        Monsters = CollectionViewSource.GetDefaultView(items);
        Monsters.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MonsterEntryViewModel.Group)));
        Monsters.Filter = Matches;
    }

    private string _search = string.Empty;
    private string _query = string.Empty;   // _search trimmed once, reused by the per-monster filter
    /// <summary>Live filter text; matches a monster's name (substring) or id.</summary>
    public string SearchText
    {
        get => _search;
        set { if (SetField(ref _search, value)) { _query = value.Trim(); Monsters.Refresh(); } }
    }

    private bool Matches(object o)
    {
        if (_query.Length == 0) return true;
        var m = (MonsterEntryViewModel)o;
        return m.Name.Contains(_query, StringComparison.OrdinalIgnoreCase)
            || m.Id.ToString().Contains(_query, StringComparison.OrdinalIgnoreCase);
    }
}
