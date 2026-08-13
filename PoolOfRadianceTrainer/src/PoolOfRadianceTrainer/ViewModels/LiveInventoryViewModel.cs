using System.Collections.ObjectModel;
using System.Windows.Input;
using PoolOfRadianceTrainer.Game;
using PoolOfRadianceTrainer.Memory;
using PoolOfRadianceTrainer.Mvvm;

namespace PoolOfRadianceTrainer.ViewModels;

/// <summary>Lets a live item row write its own edits back into the running game.</summary>
public interface ILiveItemHost
{
    /// <summary>Re-finds the item if it moved, writes <paramref name="length"/> bytes of its record
    /// from <paramref name="offset"/>, and reports what happened. False means the item is gone.</summary>
    bool WriteItem(LiveItemViewModel item, int offset, int length);
}

/// <summary>One carried item located in the running game, with its live process address.</summary>
public sealed class LiveItemViewModel : ObservableObject
{
    private readonly ILiveItemHost? _host;

    public nuint Address { get; private set; }
    public ItemEntry Item { get; private set; }

    public LiveItemViewModel(nuint address, ItemEntry item, ILiveItemHost? host = null)
    {
        Address = address;
        Item = item;
        _host = host;
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

    /// <summary>Whether the game treats this item as identified. Settable: ticking it clears the
    /// item's hidden-names byte in the running game, un-ticking restores the original masking. The
    /// listed name is the game's own cached render and only catches up when the game next draws the
    /// item screen, so this checkbox — not the name — is what says whether an item is identified.</summary>
    public bool Identified
    {
        get => Item.Identified;
        set
        {
            if (Item.Identified == value) return;
            if (_host == null || !Item.SetIdentified(value)) { OnPropertyChanged(); return; }
            if (!_host.WriteItem(this, ItemEntry.OffHiddenNames, 1)) Item.SetIdentified(!value);
            Raise();
        }
    }

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

    /// <summary>This character's record as located. Its address is stable for the session, so
    /// re-reading it gives a fresh item-list head pointer whenever the party's items change.</summary>
    public CharacterRecord Record { get; }

    public ObservableCollection<LiveItemViewModel> Items { get; } = new();

    public LiveInventoryCharacterViewModel(string name, nuint address, CharacterRecord record)
    {
        Name = name;
        Address = address;
        Record = record;
    }

    /// <summary>The item to copy from / recharge.</summary>
    private LiveItemViewModel? _selectedItem;
    public LiveItemViewModel? SelectedItem { get => _selectedItem; set => SetProperty(ref _selectedItem, value); }

    /// <summary>The slot a duplicate is written onto (its item is replaced).</summary>
    private LiveItemViewModel? _duplicateTarget;
    public LiveItemViewModel? DuplicateTarget { get => _duplicateTarget; set => SetProperty(ref _duplicateTarget, value); }

    public string Label => $"{Name}  ({Items.Count} item{(Items.Count == 1 ? "" : "s")})";

    /// <summary>Re-raise the label after the item list changes, so the party-member picker's count
    /// keeps up.</summary>
    public void RaiseLabel() => OnPropertyChanged(nameof(Label));

    public override string ToString() => Label;
}

/// <summary>
/// Live-memory inventory editor: locates each party member's carried items in the running game
/// (see <see cref="ItemLocator"/>) and edits them in place. Unlike the offline "🎒 Inventory"
/// (save-file) editor, these actions poke the running DOSBox process directly, so changes take
/// effect immediately — do them out of combat.
/// </summary>
public sealed class LiveInventoryViewModel : ObservableObject, ILiveItemHost
{
    /// <summary>Value written by "recharge" — a generous but sane top-up for both wand/staff/rod
    /// charges and ammunition stacks. Kept below the 255 byte maximum so ammo isn't absurdly heavy
    /// and charges stay within a range the game is comfortable rendering.</summary>
    private const int RechargeCount = 99;

    private ProcessMemory? _mem;

    /// <summary>Where the emulated guest's RAM sits in the host process, so the game's own far
    /// pointers can be followed. Fixed for a session; resolved on Scan.</summary>
    private nuint? _guestBase;

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
        _guestBase = null;
        Characters.Clear();
        SelectedCharacter = null;
        _freezeAmmo = false; OnPropertyChanged(nameof(FreezeAmmo));
        OnPropertyChanged(nameof(IsAttached));
        Status = "Detached.";
    }

    /// <summary>Rebuilds the party's live item lists by walking each character's own item list — the
    /// far pointer in its record, then link to link — so what is listed is exactly what the game
    /// lists, in the same order.</summary>
    public void Load(IReadOnlyList<LocatedCharacter> located)
    {
        Characters.Clear();
        SelectedCharacter = null;
        _guestBase = null;
        if (_mem == null) return;

        var party = located.Where(lc => !lc.IsMonster).ToList();
        _guestBase = ResolveGuestBase(party);

        int total = 0;
        foreach (var lc in party)
        {
            var cvm = new LiveInventoryCharacterViewModel(lc.Record.Name, lc.Address, lc.Record);
            if (_guestBase is { } b)
                foreach (var li in ItemLocator.FollowChain(_mem, b, lc.Record))
                    cvm.Items.Add(new LiveItemViewModel(li.Address, li.Item, this));
            total += cvm.Items.Count;
            Characters.Add(cvm);
        }

        SelectedCharacter = Characters.FirstOrDefault();
        Status = Characters.Count == 0
            ? "No party members located — Attach and Scan first."
            : _guestBase == null
                ? "Located the party, but couldn't follow its item lists. Re-scan out of combat, with the game past the title screen."
                : $"Located {total} carried item(s) across {Characters.Count} party member(s).";
    }

    /// <summary>Locates the guest's RAM inside the emulator once per scan, using the first party
    /// member that is carrying anything. It is the same for every character and for the whole
    /// session, so a later character with an odd list can't cost another search.</summary>
    private nuint? ResolveGuestBase(IReadOnlyList<LocatedCharacter> party)
    {
        if (_mem == null) return null;
        foreach (var lc in party)
            if (ItemLocator.ResolveGuestBase(_mem, lc) is { } b)
                return b;
        return null;
    }

    // --- actions -------------------------------------------------------------
    /// <summary>Told to the user after any identify, because the effect is easy to mistake for a
    /// failure: the names in this list (and in the game) are the game's own cached render, which it
    /// only rewrites when it next draws the item screen.</summary>
    private const string NameCacheNote =
        "Names here are the game's own cached text — open the item screen in-game (then Re-scan) to see the full names.";

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
                ? $"Identified {n} live item(s) across the party. {NameCacheNote}"
                : "Every party item is already identified (the ID'd column is ticked for all of them). " + NameCacheNote;
    }

    // --- ILiveItemHost -------------------------------------------------------
    /// <summary>Writes part of one item's record back into the running game, re-finding the item
    /// first if it has moved. Used by an item row's own editable fields (the ID'd checkbox).</summary>
    bool ILiveItemHost.WriteItem(LiveItemViewModel item, int offset, int length)
    {
        if (_mem == null) return false;
        var owner = Characters.FirstOrDefault(c => c.Items.Contains(item));
        if (owner == null || !Resolve(owner, item))
        {
            Status = $"'{item.DisplayName}' could no longer be found. Re-scan, then try again.";
            return false;
        }
        if (!_mem.WriteRange(item.Address, item.Item.Raw, offset, length))
        {
            Status = $"Write to '{item.DisplayName}' failed — Re-scan and try again.";
            return false;
        }
        Status = item.Item.Identified
            ? $"Identified '{item.DisplayName}'. {NameCacheNote}"
            : $"Marked '{item.DisplayName}' unidentified again.";
        return true;
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

    /// <summary>Called each poll tick: refreshes what the list shows, then applies the ammo freeze.</summary>
    public void Tick()
    {
        RefreshFromMemory();
        ApplyFreeze();
    }

    /// <summary>
    /// Re-reads each listed item's record in place so the list follows the running game without a
    /// Re-scan. This is what makes "identify" visibly land: the name shown for an item is the game's
    /// own cached render, and the game only rewrites it when it next draws the item screen — several
    /// seconds after the flag was cleared. Reading back each tick means the full name appears here as
    /// soon as it appears in-game.
    ///
    /// <para>Only a record that still passes the item signature <i>and</i> still has the same type is
    /// adopted, so an item that has moved (or been dropped, leaving unrelated bytes at that address)
    /// is left alone for <see cref="Resolve"/> to re-find rather than being replaced with junk.</para>
    /// </summary>
    private void RefreshFromMemory()
    {
        if (_mem == null || _guestBase is not { } b) return;
        foreach (var c in Characters)
        {
            var chain = ItemLocator.FollowChain(_mem, b, c.Record);
            // A chain that reads as empty is far more likely to be a transient (the game rewriting
            // its heap as a menu opens) than the party genuinely dropping everything, so leave the
            // list alone rather than blanking it and losing the selection.
            if (chain.Count == 0 && c.Items.Count > 0) continue;

            if (SameItems(c.Items, chain))
                for (int i = 0; i < chain.Count; i++) c.Items[i].Rebind(chain[i].Address, chain[i].Item);
            else
                Repopulate(c, chain);
        }
    }

    private static bool SameItems(IList<LiveItemViewModel> shown, List<LocatedItem> chain)
    {
        if (shown.Count != chain.Count) return false;
        for (int i = 0; i < chain.Count; i++)
            if (shown[i].Address != chain[i].Address) return false;
        return true;
    }

    /// <summary>Rebuilds one character's rows after its list itself changed — an item picked up,
    /// dropped, sold or used — keeping the selection on the same item where it survived.</summary>
    private void Repopulate(LiveInventoryCharacterViewModel c, List<LocatedItem> chain)
    {
        nuint? selected = c.SelectedItem?.Address;
        nuint? target = c.DuplicateTarget?.Address;

        c.Items.Clear();
        foreach (var li in chain) c.Items.Add(new LiveItemViewModel(li.Address, li.Item, this));

        c.SelectedItem = c.Items.FirstOrDefault(i => i.Address == selected);
        c.DuplicateTarget = c.Items.FirstOrDefault(i => i.Address == target);
        c.RaiseLabel();
    }

    /// <summary>When ammo/charge freeze is on, re-scans every party member's
    /// item range and re-tops any rechargeable item to the max count, so ammunition stacks and wand
    /// charges never deplete. Re-scanning fresh each tick keeps it correct even as the item linked
    /// list shifts in memory.</summary>
    public void ApplyFreeze()
    {
        if (_mem == null || !_freezeAmmo || _guestBase is not { } b) return;
        foreach (var c in Characters)
            foreach (var li in ItemLocator.FollowChain(_mem, b, c.Record))
                if (li.Item.IsRechargeable && li.Item.RechargeValue != RechargeCount)
                {
                    li.Item.Recharge(RechargeCount);
                    _mem.WriteRange(li.Address, li.Item.Raw, li.Item.RechargeOffset, 1);
                }
    }

    // --- safety --------------------------------------------------------------
    /// <summary>Confirms an item can be safely written: if it is still at its last-scanned address it
    /// passes through; otherwise it re-walks the owning character's item list (its record address is
    /// stable within a session) and rebinds to the matching record at its new location. Returns false
    /// only when the item can no longer be found at all — genuinely dropped, sold, or consumed.</summary>
    private bool Resolve(LiveInventoryCharacterViewModel c, LiveItemViewModel it)
    {
        if (_mem == null) return false;
        if (StillAt(it)) return true;
        if (_guestBase is not { } b) return false;

        foreach (var li in ItemLocator.FollowChain(_mem, b, c.Record))
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
