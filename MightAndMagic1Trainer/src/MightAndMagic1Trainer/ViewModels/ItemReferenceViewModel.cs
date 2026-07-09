using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using MightAndMagic1Trainer.Game;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>One item in the reference list, exposing the fields the Items tab binds to.</summary>
public sealed class ItemEntryViewModel
{
    private GameItem Item { get; }

    public ItemEntryViewModel(GameItem item)
    {
        Item = item;
        Effect = ItemEffectBook.Describe(item.Id);
    }

    /// <summary>Raw item id (used by the search filter).</summary>
    public byte Id => Item.Id;

    /// <summary>Item name, e.g. "MACE +1".</summary>
    public string Name => Item.Name;

    /// <summary>Grouping bucket (the game's id-range category).</summary>
    public string Category => Item.Category;

    /// <summary>Right-aligned accent tag: the shop cost.</summary>
    public string CostTag => Item.CostText;

    /// <summary>Detail line beneath the name: id, category, damage/AC and charges.</summary>
    public string Description => Item.DetailText;

    /// <summary>
    /// Second detail line: the equip effect (resistance / attribute bonus / curse) and which
    /// classes &amp; alignments may use it. Computed once in the constructor; empty for the one
    /// item with no effect data on file.
    /// </summary>
    public string Effect { get; }
}

/// <summary>
/// Read-only reference for the Items tab: the game's complete 255-entry item table grouped by
/// category, each with name, cost and a damage/AC/charges detail line, with a name/id search box.
/// Independent of any character or attached game — it just shows everything the game offers,
/// mirroring <see cref="SpellReferenceViewModel"/>.
/// </summary>
public sealed class ItemReferenceViewModel : ObservableObject
{
    public ICollectionView Items { get; }

    public ItemReferenceViewModel()
    {
        var items = new ObservableCollection<ItemEntryViewModel>(
            ItemBook.Catalog.Select(i => new ItemEntryViewModel(i)));
        Items = CollectionViewSource.GetDefaultView(items);
        Items.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ItemEntryViewModel.Category)));
        Items.Filter = Matches;
    }

    private string _search = string.Empty;
    private string _query = string.Empty;   // _search trimmed once, reused by the per-item filter
    /// <summary>Live filter text; matches an item's name (substring) or id.</summary>
    public string SearchText
    {
        get => _search;
        set { if (SetField(ref _search, value)) { _query = value.Trim(); Items.Refresh(); } }
    }

    private bool Matches(object o)
    {
        if (_query.Length == 0) return true;
        var it = (ItemEntryViewModel)o;
        return it.Name.Contains(_query, StringComparison.OrdinalIgnoreCase)
            || it.Id.ToString().Contains(_query, StringComparison.OrdinalIgnoreCase)
            || it.Effect.Contains(_query, StringComparison.OrdinalIgnoreCase);
    }
}
