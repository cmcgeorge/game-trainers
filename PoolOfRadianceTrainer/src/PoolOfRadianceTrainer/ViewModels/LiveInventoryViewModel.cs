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
    /// re-reading it gives a fresh item-list head pointer whenever the party's items change —
    /// see <see cref="RefreshRecord"/>, which every item walk goes through.</summary>
    public CharacterRecord Record { get; }

    /// <summary>
    /// Re-reads this character's 285-byte record from the game before its item list is walked.
    ///
    /// <para>Without this the walk starts from whatever the item-list head pointer
    /// (<see cref="PorFormat.OffItemsPtr"/>) held at scan time, and the game rewrites that pointer
    /// every time the list changes — looting, selling, dropping, or drinking a potion. A stale head
    /// means the walk misses newly acquired items, keeps showing ones that are gone, and lets the
    /// ammo freeze write down a chain the game has already rebuilt.</para>
    ///
    /// <para>The record is only adopted if it still decodes as this same character, so a heap slot
    /// that has been recycled since the scan is refused rather than followed.</para>
    /// </summary>
    public bool RefreshRecord(ProcessMemory mem, byte[] scratch)
    {
        if (!CharacterLocator.Reread(mem, Address, scratch, Record)) return false;
        Array.Copy(scratch, 0, Record.Bytes, 0, PorFormat.RecordSize);
        return true;
    }

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

    /// <summary>One scratch buffer for re-reading owner records, reused across the party and the
    /// poll tick — <see cref="LiveInventoryCharacterViewModel.RefreshRecord"/> copies out of it
    /// immediately, so nothing is allocated per tick.</summary>
    private readonly byte[] _recordBuf = new byte[PorFormat.RecordSize];

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
                : $"Located {total} carried item(s) across {Characters.Count} party member(s)." + _guestBaseCaveat;
    }

    /// <summary>Locates the guest's RAM inside the emulator once per scan, using the first party
    /// member that is carrying anything. It is the same for every character and for the whole
    /// session, so a later character with an odd list can't cost another search.</summary>
    private nuint? ResolveGuestBase(IReadOnlyList<LocatedCharacter> party)
    {
        _guestBaseCaveat = "";
        if (_mem == null) return null;

        // Take the best-corroborated answer any party member gives, not the first one that walks:
        // a chain whose length matches its owner's item count is the strong result, and one member
        // with an odd list shouldn't decide the offset for the whole session when another agrees.
        ItemLocator.GuestBase? best = null;
        foreach (var lc in party)
        {
            var found = ItemLocator.ResolveGuestBaseDetailed(_mem, lc);
            if (found is not { } g) continue;
            if (best is not { } b || (g.CountMatched && !b.CountMatched) ||
                (g.CountMatched == b.CountMatched && g.ChainLength > b.ChainLength))
                best = g;
            if (best is { CountMatched: true, Ambiguous: false }) break;
        }
        if (best is not { } r) return null;

        if (!r.CountMatched || r.Ambiguous)
            _guestBaseCaveat = " The item lists were matched without full corroboration " +
                               $"({r.ChainLength} item(s) walked against a recorded count of {r.ExpectedCount}" +
                               (r.Ambiguous ? ", and another location fitted equally well" : "") +
                               ") — check the list looks right before editing.";
        return r.Base;
    }

    /// <summary>Appended to the Scan status when the guest→host offset had to be settled on weaker
    /// evidence than an exact item-count match.</summary>
    private string _guestBaseCaveat = "";

    // --- actions -------------------------------------------------------------
    /// <summary>Told to the user after any identify, because the effect is easy to mistake for a
    /// failure: the names in this list (and in the game) are the game's own cached render, which it
    /// only rewrites when it next draws the item screen.</summary>
    private const string NameCacheNote =
        "Names here are the game's own cached text — open the item screen in-game (then Re-scan) to see the full names.";

    private void IdentifyAll()
    {
        if (_mem == null) return;
        int n = 0, stale = 0, failed = 0;
        foreach (var c in Characters)
            foreach (var it in c.Items)
                if (!it.Item.Identified)
                {
                    if (!Resolve(c, it)) { stale++; continue; }
                    // Edit a copy: if the write is refused the row must keep describing what the
                    // game still holds, not what the trainer wanted it to hold.
                    var edited = it.Item.Clone();
                    edited.Identify();
                    if (!_mem.WriteRange(it.Address, edited.Raw, ItemEntry.OffHiddenNames, 1)) { failed++; continue; }
                    it.Item.Identify();
                    it.Raise();
                    n++;
                }
        Status = Report($"Identified {n} live item(s)", n, stale, failed,
                        n > 0 ? " " + NameCacheNote : "",
                        "Every party item is already identified (the ID'd column is ticked for all of them). " + NameCacheNote);
    }

    /// <summary>
    /// One status line for the bulk actions, so a partial result reads the same whichever button
    /// produced it — and so a failed write is never counted as a success.
    /// </summary>
    private static string Report(string didText, int done, int stale, int failed, string suffix, string noneText)
    {
        if (done == 0 && stale == 0 && failed == 0) return noneText;
        var parts = new List<string>();
        if (done > 0) parts.Add(didText);
        if (stale > 0) parts.Add($"{stale} could no longer be found");
        if (failed > 0) parts.Add($"{failed} could not be written to the game");
        string text = string.Join("; ", parts) + ".";
        if (stale > 0 || failed > 0) text += " Re-scan and run again.";
        return text + suffix;
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
        // Build the replacement separately so a refused write leaves the row describing the slot
        // the game still has, rather than showing a copy that never landed.
        var edited = dst.Item.Clone();
        edited.CopyFrom(src.Item);
        if (!_mem.WriteRange(dst.Address, edited.Raw, 0, ItemEntry.RecordSize))
        {
            Status = $"Copying onto the '{replaced}' slot failed — Re-scan, then try again.";
            return;
        }
        dst.Item.CopyFrom(src.Item);
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
        var edited = it.Item.Clone();
        edited.Recharge(RechargeCount);
        if (!_mem.WriteRange(it.Address, edited.Raw, edited.RechargeOffset, 1))
        {
            Status = $"Recharging '{it.DisplayName}' failed — Re-scan, then try again.";
            return;
        }
        it.Item.Recharge(RechargeCount);
        it.Raise();
        Status = it.Item.IsChargedItem
            ? $"Recharged '{it.DisplayName}' to {RechargeCount} charges."
            : $"Restocked '{it.DisplayName}' to {RechargeCount}.";
    }

    private void RechargeAll()
    {
        if (_mem == null) return;
        int n = 0, miss = 0, failed = 0;
        foreach (var c in Characters)
            foreach (var it in c.Items)
                if (it.Item.IsRechargeable)
                {
                    if (!Resolve(c, it)) { miss++; continue; }
                    var edited = it.Item.Clone();
                    edited.Recharge(RechargeCount);
                    if (!_mem.WriteRange(it.Address, edited.Raw, edited.RechargeOffset, 1)) { failed++; continue; }
                    it.Item.Recharge(RechargeCount);
                    it.Raise();
                    n++;
                }
        Status = Report($"Recharged {n} item(s) to {RechargeCount}", n, miss, failed, "",
                        "No rechargeable items (ammunition or wands) found in the party.");
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
            if (!c.RefreshRecord(_mem, _recordBuf)) continue;
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
        int failed = 0;
        foreach (var c in Characters)
        {
            if (!c.RefreshRecord(_mem, _recordBuf)) continue;
            foreach (var li in ItemLocator.FollowChain(_mem, b, c.Record))
                if (li.Item.IsRechargeable && li.Item.RechargeValue != RechargeCount)
                {
                    li.Item.Recharge(RechargeCount);
                    if (!_mem.WriteRange(li.Address, li.Item.Raw, li.Item.RechargeOffset, 1)) failed++;
                }
        }
        // The freeze runs on the poll timer, so it must not shout every tick; say it once, when the
        // writes start failing, rather than leaving the toggle looking like it is still working.
        if (failed > 0 && !_freezeWriteFailed)
            Status = "Ammo & charge freeze couldn't write to the game — Re-scan, or Detach and Attach again.";
        _freezeWriteFailed = failed > 0;
    }

    private bool _freezeWriteFailed;

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
        if (!c.RefreshRecord(_mem, _recordBuf)) return false;

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
