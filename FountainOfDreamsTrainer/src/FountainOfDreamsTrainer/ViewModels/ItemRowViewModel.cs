using FountainOfDreamsTrainer.Game;

namespace FountainOfDreamsTrainer.ViewModels;

/// <summary>
/// One inventory slot row: the 6-byte item entry at slot <see cref="Index"/>. The first byte is
/// the item ID (0xFF = empty); the remaining 5 bytes are item-specific data (ammo count for
/// weapons, quantity for consumables, etc.). Displayed read-only with a Clear button.
/// </summary>
public sealed class ItemRowViewModel : ObservableObject
{
    private readonly CharacterRecord _record;
    private readonly Action _commit;

    public int Index { get; }

    public IReadOnlyList<ItemInfo> Options => ItemBook.Items;

    public ItemRowViewModel(int index, CharacterRecord record, Action commit)
    {
        Index = index;
        _record = record;
        _commit = commit;
    }

    public string SlotLabel => $"{Index + 1,2}";

    public int Id => _record.GetItemId(Index);

    public bool IsEmpty => Id == CharacterFormat.InventoryEmpty;

    public string Name => Id == CharacterFormat.InventoryEmpty
        ? "(empty)"
        : ItemBook.ItemName(Id);

    /// <summary>The first data byte after the item ID (commonly ammo/quantity).</summary>
    public int Data0 => _record.GetItemData(Index)[1];

    public void Clear()
    {
        _record.ClearItem(Index);
        _commit();
    }

    /// <summary>Re-reads all bindings after a live refresh or a compaction.</summary>
    public void Refresh() => RaiseAll();

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Id));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Data0));
    }
}
