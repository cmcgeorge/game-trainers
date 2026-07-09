using System.Collections.ObjectModel;
using System.Windows.Input;
using PoolOfRadianceTrainer.Game;
using PoolOfRadianceTrainer.Memory;
using PoolOfRadianceTrainer.Mvvm;

namespace PoolOfRadianceTrainer.ViewModels;

/// <summary>One carried item located in the running game, with its live process address.</summary>
public sealed class LiveItemViewModel : ObservableObject
{
    public nuint Address { get; private set; }
    public ItemEntry Item { get; private set; }

    public LiveItemViewModel(nuint address, ItemEntry item)
    {
        Address = address;
        Item = item;
    }

    /// <summary>Re-point this view model at the same item's new location after it moved in memory,
    /// adopting the freshly-read record bytes, and refresh the display.</summary>
    public void Rebind(nuint address, ItemEntry item)
    {
        Address = address;
        Item = item;
        Raise();
    }

    public string DisplayName => Item.DisplayName;
    public bool Identified => Item.Identified;
    public bool Readied => Item.Readied;
    public int Count => Item.Count;
    public int Value => Item.Value;
    public string Tags => Item.Tags;

    /// <summary>Charge count for wands/staves/rods, shown in the item list; blank for other items
    /// (which have no charges).</summary>
    public string Charges => Item.IsChargedItem ? Item.Charges.ToString() : "";

    public override string ToString() => DisplayName;

    /// <summary>Re-raise every displayed property after the backing record bytes change.</summary>
    public void Raise()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Identified));
        OnPropertyChanged(nameof(Readied));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(Value));
        OnPropertyChanged(nameof(Tags));
        OnPropertyChanged(nameof(Charges));
    }
}

/// <summary>A located party member and the carried items found next to it in the running game.</summary>
public sealed class LiveInventoryCharacterViewModel : ObservableObject
{
    public string Name { get; }
    public nuint Address { get; }

    /// <summary>Upper bound of this character's item region (the next located record's address). Its
    /// own record address is stable within a session, so re-scanning <c>[Address+record, ItemsLimit)</c>
    /// re-finds items that the game moved within the linked list since the last scan.</summary>
    public nuint ItemsLimit { get; set; }

    public ObservableCollection<LiveItemViewModel> Items { get; } = new();

    public LiveInventoryCharacterViewModel(string name, nuint address)
    {
        Name = name;
        Address = address;
    }

    /// <summary>The item to copy from / recharge.</summary>
    private LiveItemViewModel? _selectedItem;
    public LiveItemViewModel? SelectedItem { get => _selectedItem; set => SetProperty(ref _selectedItem, value); }

    /// <summary>The slot a duplicate is written onto (its item is replaced).</summary>
    private LiveItemViewModel? _duplicateTarget;
    public LiveItemViewModel? DuplicateTarget { get => _duplicateTarget; set => SetProperty(ref _duplicateTarget, value); }

    public string Label => $"{Name}  ({Items.Count} item{(Items.Count == 1 ? "" : "s")})";

    public override string ToString() => Label;
}

/// <summary>
/// Live-memory inventory editor: locates each party member's carried items in the running game
/// (see <see cref="ItemLocator"/>) and edits them in place. Unlike the offline "🎒 Inventory"
/// (save-file) editor, these actions poke the running DOSBox process directly, so changes take
/// effect immediately — do them out of combat.
/// </summary>
public sealed class LiveInventoryViewModel : ObservableObject
{
    /// <summary>Value written by "recharge" — a generous but sane top-up for both wand/staff/rod
    /// charges and ammunition stacks. Kept below the 255 byte maximum so ammo isn't absurdly heavy
    /// and charges stay within a range the game is comfortable rendering.</summary>
    private const int RechargeCount = 99;

    private ProcessMemory? _mem;

    public ObservableCollection<LiveInventoryCharacterViewModel> Characters { get; } = new();

    public LiveInventoryViewModel()
    {
        IdentifyAllCommand = new RelayCommand(_ => IdentifyAll(),
            _ => _mem != null && Characters.Any(c => c.Items.Count > 0));
        DuplicateItemCommand = new RelayCommand(_ => DuplicateItem(),
            _ => _mem != null && SelectedCharacter is { SelectedItem: not null, DuplicateTarget: not null } c
                 && !ReferenceEquals(c.SelectedItem, c.DuplicateTarget));
        RechargeItemCommand = new RelayCommand(_ => RechargeItem(),
            _ => _mem != null && SelectedCharacter?.SelectedItem?.Item.IsRechargeable == true);
        RechargeAllCommand = new RelayCommand(_ => RechargeAll(),
            _ => _mem != null && Characters.Any(c => c.Items.Count > 0));
    }

    // --- state ---------------------------------------------------------------
    private LiveInventoryCharacterViewModel? _selectedCharacter;
    public LiveInventoryCharacterViewModel? SelectedCharacter
    {
        get => _selectedCharacter;
        set => SetProperty(ref _selectedCharacter, value);
    }

    private string _status = "Attach and Scan on the toolbar above, then Re-scan after picking items up.";
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    public bool IsAttached => _mem != null;

    private bool _freezeAmmo;
    /// <summary>Poll-loop freeze: every tick, top every rechargeable item (ammunition stacks and
    /// wands/staves with charges) across the party back up to the max count, so ammo and charges
    /// never deplete. Needs a prior Scan; it re-scans each character's item range every tick, so it
    /// survives the item list moving in memory. Like other live-memory edits, prefer out of combat.</summary>
    public bool FreezeAmmo
    {
        get => _freezeAmmo;
        set
        {
            if (!SetProperty(ref _freezeAmmo, value)) return;
            Status = value
                ? $"Ammo & charge freeze ON — rechargeable items kept at {RechargeCount}."
                : "Ammo & charge freeze OFF.";
        }
    }

    // --- commands ------------------------------------------------------------
    public ICommand IdentifyAllCommand { get; }
    public ICommand DuplicateItemCommand { get; }
    public ICommand RechargeItemCommand { get; }
    public ICommand RechargeAllCommand { get; }

    // --- lifecycle -----------------------------------------------------------
    public void Attach(ProcessMemory mem)
    {
        _mem = mem;
        OnPropertyChanged(nameof(IsAttached));
    }

    public void Detach()
    {
        _mem = null;
        Characters.Clear();
        SelectedCharacter = null;
        _freezeAmmo = false; OnPropertyChanged(nameof(FreezeAmmo));
        OnPropertyChanged(nameof(IsAttached));
        Status = "Detached.";
    }

    /// <summary>Rebuilds the party's live item lists from a freshly-located, address-sorted party.
    /// Each party member's items are scanned from the end of its record up to the next located
    /// record (party or monster), so the scan never bleeds into an adjacent record.</summary>
    public void Load(IReadOnlyList<LocatedCharacter> located)
    {
        Characters.Clear();
        SelectedCharacter = null;
        if (_mem == null) return;

        int total = 0;
        for (int idx = 0; idx < located.Count; idx++)
        {
            var lc = located[idx];
            if (lc.IsMonster) continue;

            nuint start = lc.Address + (nuint)PorFormat.RecordSize;
            nuint limit = idx + 1 < located.Count
                ? located[idx + 1].Address
                : start + (nuint)ItemLocator.MaxSpan;

            var cvm = new LiveInventoryCharacterViewModel(lc.Record.Name, lc.Address) { ItemsLimit = limit };
            foreach (var li in ItemLocator.FindInRange(_mem, start, limit))
                cvm.Items.Add(new LiveItemViewModel(li.Address, li.Item));
            total += cvm.Items.Count;
            Characters.Add(cvm);
        }

        SelectedCharacter = Characters.FirstOrDefault();
        Status = Characters.Count == 0
            ? "No party members located — Attach and Scan first."
            : $"Located {total} carried item(s) across {Characters.Count} party member(s).";
    }

    // --- actions -------------------------------------------------------------
    private void IdentifyAll()
    {
        if (_mem == null) return;
        int n = 0, stale = 0;
        foreach (var c in Characters)
            foreach (var it in c.Items)
                if (!it.Item.Identified)
                {
                    if (!Resolve(c, it)) { stale++; continue; }
                    it.Item.Identify();
                    _mem.WriteRange(it.Address, it.Item.Raw, ItemEntry.OffHiddenNames, 1);
                    it.Raise();
                    n++;
                }
        Status = stale > 0
            ? $"Identified {n} live item(s); {stale} item(s) could no longer be found — Re-scan and run again."
            : n > 0
                ? $"Identified {n} live item(s) across the party."
                : "All party items are already identified.";
    }

    private void DuplicateItem()
    {
        if (_mem == null || SelectedCharacter is not { } c) return;
        var src = c.SelectedItem;
        var dst = c.DuplicateTarget;
        if (src == null || dst == null || ReferenceEquals(src, dst))
        {
            Status = "Pick a source item and a different target slot to overwrite.";
            return;
        }
        if (!Resolve(c, src) || !Resolve(c, dst))
        {
            Status = "The party's items could no longer be found. Re-scan, then try again.";
            return;
        }
        string replaced = dst.DisplayName;
        dst.Item.CopyFrom(src.Item);
        _mem.WriteRange(dst.Address, dst.Item.Raw, 0, ItemEntry.RecordSize);
        dst.Raise();
        Status = $"Copied '{src.DisplayName}' onto the '{replaced}' slot of {c.Name}.";
    }

    private void RechargeItem()
    {
        if (_mem == null || SelectedCharacter is not { SelectedItem: { } it } c) return;
        if (!it.Item.IsRechargeable)
        {
            Status = $"'{it.DisplayName}' has nothing to recharge — only wands/staves/rods and ammunition do.";
            return;
        }
        if (!Resolve(c, it))
        {
            Status = "That item could no longer be found. Re-scan, then try again.";
            return;
        }
        it.Item.Recharge(RechargeCount);
        _mem.WriteRange(it.Address, it.Item.Raw, it.Item.RechargeOffset, 1);
        it.Raise();
        Status = it.Item.IsChargedItem
            ? $"Recharged '{it.DisplayName}' to {RechargeCount} charges."
            : $"Restocked '{it.DisplayName}' to {RechargeCount}.";
    }

    private void RechargeAll()
    {
        if (_mem == null) return;
        int n = 0, miss = 0;
        foreach (var c in Characters)
            foreach (var it in c.Items)
                if (it.Item.IsRechargeable)
                {
                    if (!Resolve(c, it)) { miss++; continue; }
                    it.Item.Recharge(RechargeCount);
                    _mem.WriteRange(it.Address, it.Item.Raw, it.Item.RechargeOffset, 1);
                    it.Raise();
                    n++;
                }
        Status = n == 0
            ? "No rechargeable items (ammunition or wands) found in the party."
            : miss > 0
                ? $"Recharged {n} item(s) to {RechargeCount}; {miss} could no longer be found — Re-scan and run again."
                : $"Recharged {n} rechargeable item(s) across the party to {RechargeCount}.";
    }

    /// <summary>Called each poll tick. When ammo/charge freeze is on, re-scans every party member's
    /// item range and re-tops any rechargeable item to the max count, so ammunition stacks and wand
    /// charges never deplete. Re-scanning fresh each tick keeps it correct even as the item linked
    /// list shifts in memory.</summary>
    public void ApplyFreeze()
    {
        if (_mem == null || !_freezeAmmo) return;
        foreach (var c in Characters)
        {
            nuint start = c.Address + (nuint)PorFormat.RecordSize;
            foreach (var li in ItemLocator.FindInRange(_mem, start, c.ItemsLimit))
                if (li.Item.IsRechargeable && li.Item.RechargeValue != RechargeCount)
                {
                    li.Item.Recharge(RechargeCount);
                    _mem.WriteRange(li.Address, li.Item.Raw, li.Item.RechargeOffset, 1);
                }
        }
    }

    // --- safety --------------------------------------------------------------
    /// <summary>Confirms an item can be safely written: if it is still at its last-scanned address it
    /// passes through; otherwise it re-scans the owning character's item range (its record address is
    /// stable within a session) and rebinds to the matching record at its new location. Returns false
    /// only when the item can no longer be found at all — genuinely dropped, sold, or consumed.</summary>
    private bool Resolve(LiveInventoryCharacterViewModel c, LiveItemViewModel it)
    {
        if (_mem == null) return false;
        if (StillAt(it)) return true;

        nuint start = c.Address + (nuint)PorFormat.RecordSize;
        foreach (var li in ItemLocator.FindInRange(_mem, start, c.ItemsLimit))
            if (li.Item.Type == it.Item.Type && li.Item.DisplayName == it.Item.DisplayName)
            {
                it.Rebind(li.Address, li.Item);
                return true;
            }
        return false;
    }

    /// <summary>Re-reads the record at the item's last-scanned address and confirms it still holds
    /// the same item (a valid signature with a matching type and rendered name). Item records are a
    /// linked list with no fixed stride, so any pick-up/drop/move since the last scan can shift them;
    /// this guards every poke so a write can never land on a stale — now unrelated — address.</summary>
    private bool StillAt(LiveItemViewModel it)
    {
        if (_mem == null) return false;
        var buf = new byte[ItemEntry.RecordSize];
        if (_mem.Read(it.Address, buf, ItemEntry.RecordSize) < ItemEntry.RecordSize) return false;
        if (!ItemSignature.Looks(buf, 0)) return false;
        var live = new ItemEntry(buf, 0);
        return live.Type == it.Item.Type && live.DisplayName == it.Item.DisplayName;
    }
}
