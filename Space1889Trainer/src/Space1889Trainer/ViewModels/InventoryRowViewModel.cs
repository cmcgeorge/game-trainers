using Space1889Trainer.Game;

namespace Space1889Trainer.ViewModels;

/// <summary>One of a character's 21 inventory entries.</summary>
public sealed class InventoryRowViewModel : ObservableObject
{
    private readonly CharacterViewModel _owner;
    private bool _loading;
    private InventoryEntry _entry;
    private bool _inUse, _inHand, _worn;

    public InventoryRowViewModel(CharacterViewModel owner, int index)
    {
        _owner = owner;
        Index = index;
        RemoveCommand = new RelayCommand(() => _owner.RemoveItem(Index), () => IsOccupied && _owner.CanEdit);
        UseCommand = new RelayCommand(() => _owner.UseItem(Index), () => IsOccupied && _owner.CanEdit && (IsHandWeapon || IsArmor));
    }

    public int Index { get; }

    public int SlotNumber => Index + 1;

    public RelayCommand RemoveCommand { get; }

    public RelayCommand UseCommand { get; }

    public bool IsOccupied => !_entry.IsEmpty;

    public ItemInfo? Item => ItemBook.ById(_entry.ItemId);

    public string ItemName => ItemBook.NameOf(_entry.ItemId);

    public string TypeName => Item?.TypeName ?? "";

    public bool UsesAmmunition => Item?.UsesAmmunition == true;

    public bool IsHandWeapon => Item?.IsHandWeapon == true;

    public bool IsArmor => Item?.Kind == ItemType.Armor;

    /// <summary>
    /// Item id (ITEMS.DAT index + 1). Changing it swaps the item in place — the pack stays packed, and the
    /// ammunition bytes and weapon/armour references are made consistent with the new item.
    /// </summary>
    public int ItemId
    {
        get => _entry.ItemId;
        set
        {
            if (value == _entry.ItemId || _loading || !IsOccupied || value <= 0) return;
            _owner.ReplaceItem(Index, value);   // the owner reloads every row from the block
            RaiseAll();
        }
    }

    /// <summary>Loaded ammunition, 0-based ITEMS.DAT index (SHOT 68 … SHRAPNEL 71).</summary>
    public int AmmoIndex
    {
        get => _entry.AmmoIndex;
        set
        {
            if (value == _entry.AmmoIndex || _loading || !IsOccupied) return;
            Commit(_entry with { AmmoIndex = value });
        }
    }

    public int Rounds
    {
        get => _entry.Rounds;
        set
        {
            int v = Math.Clamp(value, 0, ushort.MaxValue);
            if (v == _entry.Rounds || _loading || !IsOccupied) return;
            Commit(_entry with { Rounds = v });
        }
    }

    public int InGun
    {
        get => _entry.InGun;
        set
        {
            int v = Math.Clamp(value, 0, 255);
            if (v == _entry.InGun || _loading || !IsOccupied) return;
            Commit(_entry with { InGun = v });
        }
    }

    /// <summary>The "in use" flag USE toggles (lamps, clothing, water breathers need it).</summary>
    public bool InUse
    {
        get => _inUse;
        set
        {
            if (_inUse == value || _loading || !IsOccupied) return;
            _owner.WriteInUse(Index, value);    // reloads this row (and its Status) from what was stored
            RaiseAll();
        }
    }

    /// <summary>This entry is the weapon in hand.</summary>
    public bool InHand => _inHand;

    /// <summary>This entry is the armour worn.</summary>
    public bool Worn => _worn;

    public string Status => !IsOccupied ? "" : _inHand ? "in hand" : _worn ? "worn" : _inUse ? "in use" : "";

    private void Commit(InventoryEntry next)
    {
        if (_owner.WriteEntry(Index, next))
        {
            _entry = next;
            RaiseAll();
        }
        else
        {
            RaiseAll();   // snap the controls back to the value the game still holds
        }
    }

    /// <summary>Repopulates from the record without writing.</summary>
    public void Load(InventoryEntry entry, bool inUse, bool inHand, bool worn)
    {
        _loading = true;
        try
        {
            bool changed = entry != _entry || inUse != _inUse || inHand != _inHand || worn != _worn;
            _entry = entry;
            _inUse = inUse;
            _inHand = inHand;
            _worn = worn;
            if (changed) RaiseAll();
        }
        finally { _loading = false; }
    }

    public void RaiseCommands()
    {
        RemoveCommand.RaiseCanExecuteChanged();
        UseCommand.RaiseCanExecuteChanged();
    }

    private void RaiseAll()
    {
        foreach (var name in new[]
                 {
                     nameof(ItemId), nameof(ItemName), nameof(TypeName), nameof(AmmoIndex), nameof(Rounds), nameof(InGun),
                     nameof(InUse), nameof(InHand), nameof(Worn), nameof(Status), nameof(IsOccupied), nameof(UsesAmmunition),
                     nameof(IsHandWeapon), nameof(IsArmor), nameof(Item),
                 })
            OnPropertyChanged(name);
        RaiseCommands();
    }
}
