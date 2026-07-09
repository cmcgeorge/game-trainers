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

    public int Index { get; }
    public string SlotLabel => $"{Index + 1,2}.";

    public ItemSlotViewModel(int index, ItemSlot slot, Action<int, int> poke)
    {
        Index = index;
        _slot = slot;
        _slotBase = InventoryFormat.OffInventory + index * InventoryFormat.SlotSize;
        _poke = poke;
    }

    public bool IsEmpty => _slot.IsEmpty;

    public string Name
    {
        get => _slot.Name;
        set
        {
            _slot.Name = value;
            _poke(_slotBase + InventoryFormat.OffItemName, InventoryFormat.ItemNameLength);
            OnPropertyChanged();
            RaiseDerived();
        }
    }

    public bool Equipped
    {
        get => _slot.Equipped;
        set { _slot.Equipped = value; _poke(_slotBase + InventoryFormat.OffFlags, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int Charges
    {
        get => _slot.Charges;
        set { _slot.Charges = value; _poke(_slotBase + InventoryFormat.OffFlags, 1); OnPropertyChanged(); RaiseDerived(); }
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
        OnPropertyChanged(nameof(TypeName));
        OnPropertyChanged(nameof(Mods));
        OnPropertyChanged(nameof(Detail));
    }
}
