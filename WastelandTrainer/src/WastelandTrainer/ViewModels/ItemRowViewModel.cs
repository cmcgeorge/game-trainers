using WastelandTrainer.Game;

namespace WastelandTrainer.ViewModels;

/// <summary>
/// One inventory slot row: the packed (itemId, ammo/qty) pair at slot <see cref="Index"/>. The item
/// is chosen from the editable drop-down (<see cref="Options"/>) — pick a known item by name or type
/// a raw item id (0 = empty) directly, since there is no longer a separate id box. Every edit runs
/// through the commit callback, which compacts the whole list and writes it back to live memory, so a
/// change always lands inside the run the game reads.
/// </summary>
public sealed class ItemRowViewModel : ObservableObject
{
    private readonly CharacterRecord _record;
    private readonly Action _commit;   // compact inventory + write the whole block + refresh all rows

    public int Index { get; }

    public IReadOnlyList<ItemInfo> Options => ItemCatalog.Items;

    public ItemRowViewModel(int index, CharacterRecord record, Action commit)
    {
        Index = index;
        _record = record;
        _commit = commit;
    }

    public string SlotLabel => $"{Index + 1,2}";

    public int Id
    {
        get => _record.GetItemId(Index);
        set
        {
            _record.SetItem(Index, value, _record.GetItemQty(Index));
            _commit();
        }
    }

    public int Quantity
    {
        get => _record.GetItemQty(Index);
        set
        {
            _record.SetItem(Index, _record.GetItemId(Index), value);
            _commit();
        }
    }

    /// <summary>The known-item drop-down selection; setting it applies that id.</summary>
    public ItemInfo? SelectedOption
    {
        get => ItemCatalog.Find(Id);
        set { if (value != null) Id = value.Id; }
    }

    /// <summary>
    /// The editable drop-down's text. Displays the current slot as "id — name" and, when set, accepts
    /// either a catalog item name/label or a raw numeric id (0 or blank = empty) — this is what keeps
    /// every item id reachable now that the standalone id box is gone. Text that matches nothing is
    /// ignored and the box snaps back to the stored value on the next refresh.
    /// </summary>
    public string Selection
    {
        get => Id == 0 ? "" : ItemCatalog.Find(Id)?.Label ?? $"{Id}  Item #{Id}";
        set
        {
            int id = ItemCatalog.ParseSelection(value);
            if (id < 0) { OnPropertyChanged(); return; }   // unparseable — revert the box
            if (id == Id) { OnPropertyChanged(); return; }  // no change
            Id = id;                                        // commits + refreshes every row
        }
    }

    /// <summary>Applies a drop-down pick (raised from the combo's SelectionChanged in code-behind).</summary>
    public void Apply(ItemInfo option)
    {
        if (option != null && option.Id != Id) Id = option.Id;
    }

    public string Name => Id == 0 ? "(empty)" : ItemCatalog.ItemName(Id);
    public bool IsEmpty => Id == 0;

    public void Clear()
    {
        _record.SetItem(Index, 0, 0);
        _commit();
    }

    /// <summary>Re-reads all bindings after a live refresh or a compaction.</summary>
    public void Refresh() => RaiseAll();

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(Quantity));
        OnPropertyChanged(nameof(SelectedOption));
        OnPropertyChanged(nameof(Selection));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
