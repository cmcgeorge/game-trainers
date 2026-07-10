using DragonWarsTrainer.Game;

namespace DragonWarsTrainer.ViewModels;

/// <summary>
/// Editable view over one inventory slot of a character. Field setters mutate the backing record
/// and write just the changed bytes to live memory through the supplied poke delegate (which takes
/// a record-absolute offset and a length).
/// </summary>
public sealed class ItemSlotViewModel : ObservableObject
{
    private readonly ItemSlot _slot;
    private readonly int _slotBase;
    private readonly Action<int, int> _poke;
    private readonly Action<ItemSlotViewModel> _duplicate;
    private readonly Func<bool> _hasEmptySlot;
    private readonly Action _inventoryChanged;

    public int Index { get; }
    public string SlotLabel => $"{Index + 1,2}.";

    /// <summary>Shared item catalog backing the slot's drop-down picker.</summary>
    public IReadOnlyList<ItemTemplate> Templates => ItemTemplates.All;

    public ItemSlotViewModel(
        int index,
        ItemSlot slot,
        Action<int, int> poke,
        Action<ItemSlotViewModel> duplicate,
        Func<bool> hasEmptySlot,
        Action inventoryChanged)
    {
        Index = index;
        _slot = slot;
        _slotBase = InventoryFormat.OffInventory + index * InventoryFormat.SlotSize;
        _poke = poke;
        _duplicate = duplicate;
        _hasEmptySlot = hasEmptySlot;
        _inventoryChanged = inventoryChanged;
    }

    public bool IsEmpty => _slot.IsEmpty;

    /// <summary>True when the slot holds an item that consumes charges (enables "set infinite").</summary>
    public bool IsChargeable => _slot.IsChargeable;

    /// <summary>
    /// True when the slot holds an item and at least one other slot is free to receive the copy;
    /// gates the "Dup" button so it is never offered when the inventory is full.
    /// </summary>
    public bool CanDuplicate => !_slot.IsEmpty && _hasEmptySlot();

    public string Name
    {
        get => _slot.Name;
        set
        {
            if (value == _slot.Name) return;
            _slot.Name = value;
            _poke(_slotBase + InventoryFormat.OffItemName, InventoryFormat.ItemNameLength);
            OnPropertyChanged();
            RaiseDerived();
        }
    }

    // An empty slot's byte 0 can carry stale flag/charge bits (emptiness is decided by the name
    // byte alone), so report a clean unequipped / zero-charge state for empty rows rather than
    // surfacing that garbage in the UI.
    public bool Equipped
    {
        get => !_slot.IsEmpty && _slot.Equipped;
        set
        {
            if (value == _slot.Equipped) return;
            _slot.Equipped = value;
            _poke(_slotBase + InventoryFormat.OffFlags, 1);
            OnPropertyChanged();
            RaiseDerived();
        }
    }

    public int Charges
    {
        get => _slot.IsEmpty ? 0 : _slot.Charges;
        set
        {
            if (value == _slot.Charges) return;
            _slot.Charges = value;
            _poke(_slotBase + InventoryFormat.OffFlags, 1);
            OnPropertyChanged();
            RaiseDerived();
        }
    }

    public string TypeName => _slot.TypeName;

    public string Mods
    {
        get
        {
            string av = _slot.ArmorValueMod == 0 ? "" : $"AV {_slot.ArmorValueMod:+0;-0}";
            string ac = _slot.ArmorClassMod == 0 ? "" : $"AC {_slot.ArmorClassMod:+0;-0}";
            return string.Join("  ", new[] { av, ac }.Where(s => s.Length > 0));
        }
    }

    /// <summary>Read-only summary shown for occupied slots (type + modifiers + charges).</summary>
    public string Detail
    {
        get
        {
            if (IsEmpty) return "";
            var parts = new List<string> { TypeName };
            if (_slot.Charges > 0) parts.Add($"{_slot.Charges} charges");
            string mods = Mods;
            if (mods.Length > 0) parts.Add(mods);
            if (_slot.MinRank > 0) parts.Add($"min rank {_slot.MinRank}");
            return string.Join("  ·  ", parts);
        }
    }

    /// <summary>Empties the slot and writes the whole cleared slot back to memory.</summary>
    public void Clear()
    {
        _slot.Clear();
        _poke(_slotBase, InventoryFormat.SlotSize);
        RaiseAll();
        _inventoryChanged();
    }

    /// <summary>Replaces the slot with a catalog prototype and writes the whole slot to memory.</summary>
    public void ApplyTemplate(ItemTemplate template)
    {
        if (template is null) return;
        _slot.Apply(template);
        _poke(_slotBase, InventoryFormat.SlotSize);
        RaiseAll();
        _inventoryChanged();
    }

    /// <summary>Sets the item's charges to the game's "infinite" value (63) and writes byte 0.</summary>
    public void SetInfiniteCharges()
    {
        if (!_slot.IsChargeable) return;
        _slot.Charges = InventoryFormat.MaskCharges;
        _poke(_slotBase + InventoryFormat.OffFlags, 1);
        OnPropertyChanged(nameof(Charges));
        RaiseDerived();
    }

    /// <summary>Duplicates this item into the character's first empty slot, if any.</summary>
    public void Duplicate() => _duplicate(this);

    /// <summary>Re-raises every property after the backing slot's bytes changed underneath us.</summary>
    public void NotifyReloaded() => RaiseAll();

    /// <summary>
    /// Re-evaluates the duplicate affordance after another slot's occupancy changed (this slot's
    /// own bytes are untouched, so only <see cref="CanDuplicate"/> can flip).
    /// </summary>
    public void NotifyAvailabilityChanged() => OnPropertyChanged(nameof(CanDuplicate));

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Equipped));
        OnPropertyChanged(nameof(Charges));
        RaiseDerived();
    }

    /// <summary>
    /// Poll-tick refresh after a live read copied fresh bytes into the record: re-raises only the
    /// read-only display properties. The editable <see cref="Name"/>, <see cref="Equipped"/> and
    /// <see cref="Charges"/> are deliberately left alone so watching the game never overwrites a
    /// value the user is mid-edit (their controls commit on focus change).
    /// </summary>
    public void Refresh() => RaiseDerived();

    private void RaiseDerived()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsChargeable));
        OnPropertyChanged(nameof(CanDuplicate));
        OnPropertyChanged(nameof(TypeName));
        OnPropertyChanged(nameof(Mods));
        OnPropertyChanged(nameof(Detail));
    }
}
