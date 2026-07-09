using MightAndMagic1Trainer.Game;
using MightAndMagic1Trainer.Mvvm;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>
/// One inventory slot of a character — an equipped item or a backpack item. Like
/// <see cref="StatViewModel"/>, it owns no bytes: the item id and its charge count
/// are two single-byte fields in the parent <see cref="CharacterViewModel"/>'s record
/// (item ids at 0x40/0x46, charge counts at 0x4C/0x52). Edits write straight into that
/// buffer and are pushed to the game; the optional <see cref="FreezeCharges"/> flag is
/// honoured by the owner's freeze pass each timer tick.
/// </summary>
public sealed class ItemSlotViewModel : ObservableObject
{
    private readonly CharacterViewModel _owner;

    /// <summary>Record offset of the item-id byte (e.g. 0x40 + slot).</summary>
    public int ItemOffset { get; }

    /// <summary>Record offset of the charge-count byte (e.g. 0x4C + slot).</summary>
    public int ChargeOffset { get; }

    /// <summary>Human label, e.g. "Equipped #1" / "Backpack #3".</summary>
    public string Label { get; }

    public ItemSlotViewModel(CharacterViewModel owner, int itemOffset, int chargeOffset, string label)
    {
        _owner = owner;
        ItemOffset = itemOffset;
        ChargeOffset = chargeOffset;
        Label = label;
    }

    /// <summary>Every selectable item (id 0 = empty, then 1..255), shared across all slots.</summary>
    public IReadOnlyList<ItemBook.ItemChoice> Choices => ItemBook.Choices;

    /// <summary>The game's raw item id occupying this slot (0 = empty).</summary>
    public byte ItemId
    {
        get => _owner.Record.GetByte(ItemOffset);
        set
        {
            if (_owner.Record.GetByte(ItemOffset) == value) return;
            _owner.Record.SetByte(ItemOffset, value);
            _owner.PushByte(ItemOffset);
            _owner.RaiseHex(ItemOffset);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ItemName));
            OnPropertyChanged(nameof(Enhancements));
            OnPropertyChanged(nameof(HasEnhancements));
            OnPropertyChanged(nameof(SelectedEnhancement));
        }
    }

    /// <summary>Friendly name of the item currently in this slot.</summary>
    public string ItemName => ItemBook.ItemName(ItemId);

    /// <summary>Enhancement levels (+1/+2/…) available for the current item's family.</summary>
    public IReadOnlyList<ItemBook.EnhancementOption> Enhancements => ItemBook.EnhancementsFor(ItemId);

    /// <summary>True when the current item has more than one enhancement level to choose from.</summary>
    public bool HasEnhancements => Enhancements.Count > 1;

    /// <summary>
    /// The enhancement level chosen for this slot, as the matching <see cref="ItemBook.EnhancementOption"/>
    /// from <see cref="Enhancements"/>. Setting it swaps the slot to the same-family item at that level
    /// — e.g. MACE → MACE +2 — leaving uniques unchanged. Bound via <c>SelectedItem</c> (a reference, not
    /// a value) so that replacing the ItemsSource when the item changes can't transiently null the binding.
    /// </summary>
    public ItemBook.EnhancementOption? SelectedEnhancement
    {
        get
        {
            int plus = ItemBook.PlusOf(ItemId);
            foreach (var e in Enhancements)
                if (e.Plus == plus) return e;
            return null;
        }
        set { if (value is not null && ItemBook.VariantId(ItemId, value.Plus) is byte vid) ItemId = vid; }
    }

    /// <summary>Remaining charges of the item (meaningful only for charged/magic items).</summary>
    public byte Charges
    {
        get => _owner.Record.GetByte(ChargeOffset);
        set
        {
            if (_owner.Record.GetByte(ChargeOffset) == value) return;
            _owner.Record.SetByte(ChargeOffset, value);
            _owner.PushByte(ChargeOffset);
            _owner.RaiseHex(ChargeOffset);
            OnPropertyChanged();
        }
    }

    // Pin the charge count: the owner's timer pass rewrites the live byte back to this
    // slot's buffered value whenever the game tries to change it (see ApplyFreezes).
    private bool _freezeCharges;
    public bool FreezeCharges { get => _freezeCharges; set => SetField(ref _freezeCharges, value); }

    public void Refresh()
    {
        OnPropertyChanged(nameof(ItemId));
        OnPropertyChanged(nameof(ItemName));
        OnPropertyChanged(nameof(Enhancements));
        OnPropertyChanged(nameof(HasEnhancements));
        OnPropertyChanged(nameof(SelectedEnhancement));
        OnPropertyChanged(nameof(Charges));
    }

    /// <summary>Raise only the charge count — used by the per-tick charge freeze to avoid churn.</summary>
    public void RefreshCharges() => OnPropertyChanged(nameof(Charges));
}
