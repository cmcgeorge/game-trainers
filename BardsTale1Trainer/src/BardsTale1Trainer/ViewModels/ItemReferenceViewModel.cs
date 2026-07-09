using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using BardsTale1Trainer.Game;

namespace BardsTale1Trainer.ViewModels;

/// <summary>One item row in the reference tab.</summary>
public sealed class ItemEntryViewModel
{
    public int Id { get; }
    public string Name { get; }
    public string Category { get; }

    public ItemEntryViewModel(int id, string name, string category)
    {
        Id = id; Name = name; Category = category;
    }

    public string IdTag => $"#{Id}";
    public string SearchText => $"{Id} {Name} {Category}".ToLowerInvariant();
}

/// <summary>
/// Read-only item reference: the game's 126-item table grouped by inferred category,
/// with a search box. Ids match the Inventory tab's slot picker.
/// </summary>
public sealed class ItemReferenceViewModel : ObservableObject
{
    private readonly ObservableCollection<ItemEntryViewModel> _all = new();

    public ICollectionView Items { get; }

    public ItemReferenceViewModel()
    {
        for (int id = 1; id <= ItemBook.ItemNames.Length; id++)
            _all.Add(new ItemEntryViewModel(id, ItemBook.ItemName(id), ItemBook.CategoryOf(id)));

        Items = CollectionViewSource.GetDefaultView(_all);
        Items.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ItemEntryViewModel.Category)));
        Items.Filter = Matches;
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { if (SetField(ref _searchText, value)) Items.Refresh(); }
    }

    private bool Matches(object o)
    {
        if (string.IsNullOrWhiteSpace(_searchText)) return true;
        return o is ItemEntryViewModel e && e.SearchText.Contains(_searchText.Trim().ToLowerInvariant());
    }
}
