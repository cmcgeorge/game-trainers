using System.Collections.ObjectModel;
using System.Windows.Input;
using DarkDesigns1Trainer.Game;

namespace DarkDesigns1Trainer.ViewModels;

/// <summary>
/// One editable item byte — a carried pack slot (keys A–J) or a readied-equipment slot.
/// Reads and writes through the caller-supplied accessors so the same type serves the live
/// party editor and the offline save editor.
/// </summary>
public sealed class ItemSlotViewModel : ObservableObject
{
    private readonly Func<int> _get;
    private readonly Action<int> _set;
    private readonly ItemBook.ReadySlot? _readySlot;
    private readonly IItemPack? _pack;

    /// <summary>"A".."J" for pack slots, or the game's own prompt for a readied slot.</summary>
    public string Label { get; }

    /// <summary>Items offered in the dropdown; always starts with "(empty)".</summary>
    public ObservableCollection<ItemBook.Item> Options { get; } = new();

    /// <summary>
    /// Copies this slot's item into the first free carried slot. Dark Designs destroys most
    /// items when you use them, so a spare of something rare is the practical substitute for
    /// the recharge the game has no notion of.
    /// </summary>
    public ICommand DuplicateCommand { get; }

    /// <summary>True when this slot can offer a duplicate button at all.</summary>
    public bool CanShowDuplicate => _pack is not null;

    public ItemSlotViewModel(string label, Func<int> get, Action<int> set,
                             ItemBook.ReadySlot? readySlot = null, IItemPack? pack = null)
    {
        Label = label;
        _get = get;
        _set = set;
        _readySlot = readySlot;
        _pack = pack;

        DuplicateCommand = new RelayCommand(
            _ => { if (_pack is not null && _pack.TryAddItem(_get())) Refresh(); },
            _ => _pack is not null && _get() != 0 && _pack.HasFreeSlot);

        var offered = readySlot is { } rs
            ? ItemBook.ReadyOptions(rs)
            : ItemBook.All.Where(i => i.Id == 0 || i.IsPlayerItem);
        foreach (var i in offered) Options.Add(i);
        EnsureOption(_get());
    }

    /// <summary>
    /// The item currently in this slot. The getter deliberately does not touch
    /// <see cref="Options"/> — mutating the bound collection while WPF is resolving
    /// <c>SelectedItem</c> is asking for trouble, so <see cref="Refresh"/> widens it first.
    /// </summary>
    public ItemBook.Item Selected
    {
        get => ItemBook.Get(_get());
        set
        {
            if (value == null) return;
            _set(value.Id);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Detail));
            OnPropertyChanged(nameof(IsLegal));
            (DuplicateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Stats line for the item in this slot, blank when empty.</summary>
    public string Detail
    {
        get
        {
            int raw = _get();
            var it = ItemBook.Get(raw);
            // A byte the game never issues: say so rather than silently showing it as empty.
            if (it.Id != raw) return $"#{raw} — not a known item id";
            if (it.Id == 0) return "";
            var bits = new List<string> { $"#{it.Id}", it.Type.ToString() };
            if (it.Protection > 0) bits.Add($"prot {it.Protection}");
            else if (it.Power > 0) bits.Add($"pow {it.Power}");
            if (it.ClassMask != 0) bits.Add(it.ClassLabel);
            else bits.Add("monster gear");
            return string.Join("  ", bits);
        }
    }

    /// <summary>
    /// False when a readied slot holds something the game would reject with "Wrong type!".
    /// Pack slots accept anything, so they are always legal.
    /// </summary>
    public bool IsLegal => _readySlot is not { } rs || ItemBook.CanReady(rs, _get());

    /// <summary>Re-reads the underlying byte after the poll loop refreshed the record.</summary>
    public void Refresh()
    {
        EnsureOption(_get());
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(Detail));
        OnPropertyChanged(nameof(IsLegal));
        (DuplicateCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Makes sure the current id is offered, so a value the game put there — a monster's
    /// hide, say, or an id this slot would normally reject — still shows in the dropdown
    /// instead of blanking it.
    /// </summary>
    private void EnsureOption(int id)
    {
        // ItemBook.Get falls back to entry 0 for anything outside 0–63, so an out-of-range byte
        // would never satisfy the guard below and would append a duplicate "(empty)" on every
        // poll tick. There is no entry to offer for such a value; Detail reports it instead.
        if (ItemBook.Get(id).Id != id) return;
        if (Options.Any(o => o.Id == id)) return;
        var item = ItemBook.Get(id);
        int at = Options.Count;
        for (int i = 0; i < Options.Count; i++)
        {
            if (Options[i].Id <= id) continue;
            at = i;
            break;
        }
        Options.Insert(at, item);
    }
}
