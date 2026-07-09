using BardsTale1Trainer.Game;

namespace BardsTale1Trainer.ViewModels;

/// <summary>
/// One of a character's 8 inventory slots. Bard's Tale stores each slot as a u16:
/// bit 15 = "equipped" flag, the low bits = a 1-based item id into
/// <see cref="ItemBook.ItemNames"/> (0 = empty). Editing the item or the equipped flag
/// writes the whole word back through the owner.
/// </summary>
public sealed class ItemSlotViewModel : ObservableObject
{
    private readonly CharacterViewModel _owner;
    private readonly int _index;

    /// <summary>Record offset of this slot's 2-byte item word.</summary>
    public int WordOffset => PartyFormat.OffItems + _index * 2;

    public string Label { get; }

    public ItemSlotViewModel(CharacterViewModel owner, int index, string label)
    {
        _owner = owner;
        _index = index;
        Label = label;
    }

    /// <summary>Every selectable item (id 0 = empty, then 1..126), shared across all slots.</summary>
    public IReadOnlyList<ItemBook.ItemChoice> Choices => ItemBook.Choices;

    private ushort Word => _owner.Record.GetItemWord(_index);

    /// <summary>The 1-based item id occupying this slot (0 = empty), without the equipped flag.</summary>
    public int ItemId
    {
        get => Word & ~PartyFormat.ItemEquippedFlag;
        set
        {
            ushort cur = Word;
            ushort equipped = (ushort)(cur & PartyFormat.ItemEquippedFlag);
            ushort next = (ushort)((value & 0x7FFF) | equipped);
            if (value == 0) next = 0;   // empty slot clears the equipped flag too
            if (cur == next) return;
            WriteWord(next);
            OnPropertyChanged();
            OnPropertyChanged(nameof(ItemName));
            OnPropertyChanged(nameof(IsEquipped));
        }
    }

    /// <summary>Whether the item is equipped (in use), as opposed to carried in the pack.</summary>
    public bool IsEquipped
    {
        get => (Word & PartyFormat.ItemEquippedFlag) != 0;
        set
        {
            ushort cur = Word;
            if (ItemId == 0) { OnPropertyChanged(); return; }   // can't equip an empty slot
            ushort next = value
                ? (ushort)(cur | PartyFormat.ItemEquippedFlag)
                : (ushort)(cur & ~PartyFormat.ItemEquippedFlag);
            if (cur == next) return;
            WriteWord(next);
            OnPropertyChanged();
        }
    }

    /// <summary>Friendly name of the item currently in this slot.</summary>
    public string ItemName => ItemBook.ItemName(ItemId);

    private void WriteWord(ushort value)
    {
        _owner.Record.SetItemWord(_index, value);
        _owner.PushRange(WordOffset, 2);
        _owner.RaiseHex(WordOffset); _owner.RaiseHex(WordOffset + 1);
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(ItemId));
        OnPropertyChanged(nameof(ItemName));
        OnPropertyChanged(nameof(IsEquipped));
    }
}
