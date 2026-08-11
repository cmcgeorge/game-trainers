using LegendOfFaerghailTrainer.Game;

namespace LegendOfFaerghailTrainer.ViewModels;

/// <summary>
/// One inventory slot. Editing the item, the "in use" flag, or the condition rewrites the slot's
/// four bytes and asks the owner to push them to the game.
///
/// Every setter is a no-op unless the slot's bytes actually change. That guard is load-bearing: a
/// virtualising <c>DataGrid</c> recycles its row containers as you scroll, and each recycle re-pushes
/// the bound values. Without the guard, scrolling the inventory would write into the emulator's
/// memory once per row per scroll step.
/// </summary>
public sealed class ItemRowViewModel : ObservableObject
{
    private readonly CharacterRecord _record;
    private readonly Action<int> _writeSlot;

    public int Slot { get; }
    public string SlotLabel => (Slot + 1).ToString();

    public ItemRowViewModel(int slot, CharacterRecord record, Action<int> writeSlot)
    {
        Slot = slot;
        _record = record;
        _writeSlot = writeSlot;
    }

    public int ItemId
    {
        get => _record.GetItem(Slot).ItemId;
        set
        {
            var cur = _record.GetItem(Slot);
            // The range accepted here is the byte the record can hold, not the length of the item
            // table: the record layer deliberately preserves an id the table does not cover, and
            // the two layers must agree on what is storable. The picker only offers catalogued
            // items, so this widens nothing the user can reach - it just stops the view model
            // refusing a value the record is happy to keep.
            if (value < 0 || value > byte.MaxValue || value == cur.ItemId) { RaiseAll(); return; }
            // A freshly placed item arrives in perfect condition; clearing a slot zeroes it.
            int condition = value == 0 ? 0 : (cur.ItemId == 0 ? 100 : cur.Condition);
            Commit(value, value != 0 && cur.Equipped, condition);
        }
    }

    public bool Equipped
    {
        get => _record.GetItem(Slot).Equipped;
        set
        {
            var cur = _record.GetItem(Slot);
            if (cur.ItemId == 0 || value == cur.Equipped) { RaiseAll(); return; }
            Commit(cur.ItemId, value, cur.Condition);
        }
    }

    public int Condition
    {
        get => _record.GetItem(Slot).Condition;
        set
        {
            var cur = _record.GetItem(Slot);
            if (cur.ItemId == 0 || value == cur.Condition) { RaiseAll(); return; }
            Commit(cur.ItemId, cur.Equipped, value);
        }
    }

    public string ItemName => ItemBook.NameOf(ItemId);
    public bool IsEmpty => ItemId == 0;

    public void Refresh() => RaiseAll();

    private void Commit(int itemId, bool equipped, int condition)
    {
        _record.SetItem(Slot, itemId, equipped, condition);
        _writeSlot(Slot);
        RaiseAll();
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(ItemId));
        OnPropertyChanged(nameof(Equipped));
        OnPropertyChanged(nameof(Condition));
        OnPropertyChanged(nameof(ItemName));
        OnPropertyChanged(nameof(IsEmpty));
    }
}

/// <summary>
/// One spell slot. "Uses" is what the sheet prints on the left of the slash — the casts left today.
/// The number on the right of the slash is not stored in the record and is not editable. Setters
/// carry the same no-op guard as <see cref="ItemRowViewModel"/>, for the same reason.
/// </summary>
public sealed class SpellRowViewModel : ObservableObject
{
    private readonly CharacterRecord _record;
    private readonly Action<int> _writeSlot;

    public int Slot { get; }
    public string SlotLabel => (Slot + 1).ToString();

    public SpellRowViewModel(int slot, CharacterRecord record, Action<int> writeSlot)
    {
        Slot = slot;
        _record = record;
        _writeSlot = writeSlot;
    }

    public int SpellId
    {
        get => _record.GetSpell(Slot).SpellId;
        set
        {
            var cur = _record.GetSpell(Slot);
            // Same reasoning as ItemRowViewModel.ItemId: match the record layer's byte range so an
            // uncatalogued spell id survives a round trip through the editor.
            if (value < 0 || value > byte.MaxValue || value == cur.SpellId) { RaiseAll(); return; }
            int uses = value == 0 ? 0 : (cur.SpellId == 0 ? 10 : cur.Uses);
            Commit(value, uses);
        }
    }

    public int Uses
    {
        get => _record.GetSpell(Slot).Uses;
        set
        {
            var cur = _record.GetSpell(Slot);
            if (cur.SpellId == 0 || value == cur.Uses) { RaiseAll(); return; }
            Commit(cur.SpellId, value);
        }
    }

    public string SpellName => SpellBook.NameOf(SpellId);
    public bool IsEmpty => SpellId == 0;

    public void Refresh() => RaiseAll();

    private void Commit(int spellId, int uses)
    {
        _record.SetSpell(Slot, spellId, uses);
        _writeSlot(Slot);
        RaiseAll();
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(SpellId));
        OnPropertyChanged(nameof(Uses));
        OnPropertyChanged(nameof(SpellName));
        OnPropertyChanged(nameof(IsEmpty));
    }
}
