using WastelandRemasteredTrainer.Game;

namespace WastelandRemasteredTrainer.ViewModels;

/// <summary>
/// One packed skill slot, editable in place.
///
/// <para>Skill and item rows write through on edit rather than waiting for a Write button. They
/// are single, self-contained values — there is no cached snapshot of the whole record to go
/// stale — so writing immediately is both safe and what the DOS trainer does.</para>
/// </summary>
public sealed class SkillRowViewModel : ObservableObject
{
    private readonly CharacterRecord _record;
    private readonly ICharacterHost _host;
    private readonly bool _loaded;
    private int _level;

    public SkillRowViewModel(CharacterRecord record, ICharacterHost host, SkillEntry entry)
    {
        _record = record;
        _host = host;
        Id = entry.Id;
        _level = entry.Level;
        _loaded = true;
    }

    public int Id { get; }

    public string Name => SkillBook.SkillName(Id);

    /// <summary>The IQ needed to learn this skill, for the tooltip.</summary>
    public string Detail => SkillBook.Find(Id)?.Description ?? "";

    public int Level
    {
        get => _level;
        set
        {
            int clamped = Math.Clamp(value, 0, GameFacts.MaxSkillLevel);
            if (!SetField(ref _level, clamped)) return;
            if (!_loaded) return;

            if (_record.SetSkill(Id, clamped)) _host.OnMessage($"{Name} set to level {clamped}.");
            else _host.OnMessage($"Could not write {Name} — the skill array was not writable.");
        }
    }
}

/// <summary>One packed inventory slot, editable in place.</summary>
public sealed class ItemRowViewModel : ObservableObject
{
    private readonly CharacterRecord _record;
    private readonly ICharacterHost _host;
    private readonly bool _loaded;

    private int _itemId;
    private int _ammo;
    private bool _jammed;

    public ItemRowViewModel(CharacterRecord record, ICharacterHost host, ItemEntry entry)
    {
        _record = record;
        _host = host;
        Slot = entry.Slot;
        _itemId = entry.Id;
        _ammo = entry.Ammo;
        _jammed = entry.Jammed;
        _loaded = true;

        RemoveCommand = new RelayCommand(_ => Remove());
    }

    public int Slot { get; }

    public string SlotLabel => $"{Slot + 1,2}.";

    public RelayCommand RemoveCommand { get; }

    /// <summary>Every item the game knows about, for the row's drop-down.</summary>
    public static IReadOnlyList<ItemInfo> Catalog => ItemBook.Items;

    public string Detail => ItemBook.Find(ItemId) is { } info
        ? string.Join("  ", new[] { info.Category, info.Description, info.Damage }
            .Where(s => !string.IsNullOrEmpty(s)))
        : "";

    public int ItemId
    {
        get => _itemId;
        set
        {
            if (!SetField(ref _itemId, value)) return;
            OnPropertyChanged(nameof(Detail));
            if (!_loaded) return;

            // Choosing "(empty)" means "remove this item", not "write a 0 id here". Writing the
            // id in place would leave a 0x00 terminator mid-pack, orphaning every item behind it
            // — invisible to the game, to ReadItems, and to Max Ammo, with nothing to show for it.
            if (value == 0)
            {
                Remove();
                return;
            }

            WriteSlot($"slot {Slot + 1} set to {ItemBook.ItemName(value)}.");
        }
    }

    /// <summary>Ammo/charge count, already masked free of the jam bit.</summary>
    public int Ammo
    {
        get => _ammo;
        set
        {
            int clamped = Math.Clamp(value, 0, CharacterFormat.InventoryCountMask);
            if (!SetField(ref _ammo, clamped)) return;
            if (!_loaded) return;
            WriteSlot($"{ItemBook.ItemName(_itemId)} set to {clamped}.");
        }
    }

    /// <summary>The jammed-weapon flag carried in bit 7 of the quantity byte.</summary>
    public bool Jammed
    {
        get => _jammed;
        set
        {
            if (!SetField(ref _jammed, value)) return;
            if (!_loaded) return;
            WriteSlot(value
                ? $"{ItemBook.ItemName(_itemId)} marked jammed."
                : $"{ItemBook.ItemName(_itemId)} unjammed.");
        }
    }

    private void WriteSlot(string message)
    {
        if (_record.SetItem(Slot, _itemId, _ammo, _jammed)) _host.OnMessage(message);
        else _host.OnMessage($"Could not write inventory slot {Slot + 1}.");
    }

    private void Remove()
    {
        if (_record.RemoveItem(Slot))
        {
            _host.OnMessage($"Removed {ItemBook.ItemName(_itemId)}.");
            _host.RefreshSelected();
        }
        else
        {
            _host.OnMessage($"Could not clear inventory slot {Slot + 1}.");
        }
    }
}
