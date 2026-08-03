using System.Collections.ObjectModel;
using DarkDesigns1Trainer.Game;
using DarkDesigns1Trainer.Memory;

namespace DarkDesigns1Trainer.ViewModels;

/// <summary>
/// Editable view over a single located character record. Every setter mutates the backing
/// <see cref="Record"/> buffer and, when attached, writes just the changed field to the game's
/// live memory so edits take effect immediately.
/// </summary>
public sealed class CharacterViewModel : ObservableObject, IItemPack
{
    private readonly ICharacterHost _host;

    public nuint Address { get; }
    public int Slot { get; }
    public CharacterRecord Record { get; }

    /// <summary>Party working copies of this character; every edit is applied to these too.</summary>
    private readonly List<nuint> _mirrors;

    /// <summary>
    /// Name length, name bytes and class as they were when the roster was scanned. Every address
    /// this view-model writes to is checked against this before use. It is a snapshot rather than
    /// a look at <see cref="Record"/> because <see cref="Record"/> is refreshed from the game each
    /// tick — validating against it would let one bad read authorise the next write.
    /// </summary>
    private readonly byte[] _identity = new byte[CharacterFormat.NameLength + 2];

    private readonly byte[] _scratch = new byte[CharacterFormat.RecordSize];

    public ObservableCollection<NamedValueViewModel> Attributes { get; } = new();

    /// <summary>The ten carried pack slots (item screen keys A–J).</summary>
    public ObservableCollection<ItemSlotViewModel> Inventory { get; } = new();

    /// <summary>The four readied-equipment slots: right hand, left hand, armor, ring.</summary>
    public ObservableCollection<ItemSlotViewModel> Equipment { get; } = new();

    public string[] ClassOptions => CharacterFormat.ClassNames[1..];

    private bool _freezeBody;
    public bool FreezeBody { get => _freezeBody; set => SetField(ref _freezeBody, value); }

    private bool _freezeMagic;
    public bool FreezeMagic { get => _freezeMagic; set => SetField(ref _freezeMagic, value); }

    private bool _freezeStatus;
    public bool FreezeStatus { get => _freezeStatus; set => SetField(ref _freezeStatus, value); }

    public CharacterViewModel(ICharacterHost host, LocatedCharacter located)
    {
        _host = host;
        Address = located.Address;
        Slot = located.Slot;
        Record = located.Record;
        _mirrors = new List<nuint>(located.Mirrors);

        _identity[0] = Record.Bytes[CharacterFormat.OffNameLen];
        Array.Copy(Record.Bytes, CharacterFormat.OffName, _identity, 1, CharacterFormat.NameLength);
        _identity[^1] = Record.Bytes[CharacterFormat.OffClass];

        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        {
            int idx = i;
            Attributes.Add(new NamedValueViewModel(CharacterFormat.AttributeShort[i],
                () => Record.GetAttribute(idx),
                v => { Record.SetAttribute(idx, v); Poke(CharacterFormat.AttributeOffsets[idx], 2); RaiseDerived(); }));
        }

        for (int i = 0; i < CharacterFormat.ItemSlotCount; i++)
        {
            int slot = i;
            Inventory.Add(new ItemSlotViewModel(
                ((char)('A' + slot)).ToString(),
                () => Record.GetItem(slot),
                id => { Record.SetItem(slot, id); Poke(CharacterFormat.ItemOffset(slot), 1); RaiseDerived(); },
                pack: this));
        }

        foreach (ItemBook.ReadySlot rs in Enum.GetValues<ItemBook.ReadySlot>())
        {
            var slot = rs;
            Equipment.Add(new ItemSlotViewModel(
                ItemBook.ReadyLabel(slot),
                () => Record.GetReadied(slot),
                id => { Record.SetReadied(slot, id); Poke(ItemBook.ReadyOffset(slot), 1); },
                slot));
        }
    }

    // --- identity / summary --------------------------------------------------
    public string Name
    {
        get => Record.Name;
        set { Record.Name = value; Poke(CharacterFormat.OffNameLen, CharacterFormat.NameLength + 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public string Title => $"{Record.Name}  —  L{Record.Level} {Record.ClassName}";
    public string Summary =>
        $"Body {Record.BodyCurrent}/{Record.BodyMax}   Magic {Record.MagicCurrent}/{Record.MagicMax}   " +
        $"XP {Record.Experience}/{Record.NextLevel}   Gold {Record.Gold}   " +
        $"Pack {Record.ItemCount}/{CharacterFormat.ItemSlotCount}   [{Record.StatusName}]";
    public string ListLabel => $"{Record.Name}  (L{Record.Level} {Record.ClassName})";

    /// <summary>True when the game is holding live working copies of this character.</summary>
    public bool IsInParty => _mirrors.Count > 0;

    /// <summary>
    /// Set when the roster address stopped holding this character — the player deleted or
    /// reordered the roster in-game, so the address is no longer ours to write to. Every write is
    /// suppressed until the next scan.
    /// </summary>
    private bool _isStale;
    public bool IsStale
    {
        get => _isStale;
        private set { if (SetField(ref _isStale, value)) { OnPropertyChanged(nameof(SyncNote)); RaiseDerived(); } }
    }

    /// <summary>
    /// The copy the poll loop should read. For a party member that is the game's working copy —
    /// the roster record is stale until the game saves, so polling it would show frozen vitals
    /// and leave the freeze toggles with nothing to react to.
    /// </summary>
    public nuint LiveAddress => _mirrors.Count > 0 ? _mirrors[0] : Address;

    public string SyncNote => IsStale
        ? "This roster slot no longer holds this character — the roster changed in the game. Edits are suppressed; click Re-scan."
        : _mirrors.Count > 0
            ? $"In the active party — edits are written to the roster and to {_mirrors.Count} live working copy/copies, so they survive the game's next save."
            : "Roster slot only — this character is not in the active party, so edits apply the next time they are added.";

    public int ClassIndex
    {
        get => Record.Class - 1;
        set { Record.Class = value + 1; Poke(CharacterFormat.OffClass, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int Level
    {
        get => Record.Level;
        set { Record.Level = value; Poke(CharacterFormat.OffLevel, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    public long Experience
    {
        get => Record.Experience;
        set { Record.Experience = value; Poke(CharacterFormat.OffExperience, 4); OnPropertyChanged(); RaiseDerived(); }
    }

    public long NextLevel
    {
        get => Record.NextLevel;
        set { Record.NextLevel = value; Poke(CharacterFormat.OffNextLevel, 4); OnPropertyChanged(); RaiseDerived(); }
    }

    public long Gold
    {
        get => Record.Gold;
        set { Record.Gold = (int)value; Poke(CharacterFormat.OffGold, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- vitals --------------------------------------------------------------
    public int BodyCurrent
    {
        get => Record.BodyCurrent;
        set { Record.BodyCurrent = value; Poke(CharacterFormat.OffBodyCur, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int BodyMax
    {
        get => Record.BodyMax;
        set { Record.BodyMax = value; Poke(CharacterFormat.OffBodyMax, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int MagicCurrent
    {
        get => Record.MagicCurrent;
        set { Record.MagicCurrent = value; Poke(CharacterFormat.OffMagicCur, 2); OnPropertyChanged(); RaiseDerived(); }
    }
    public int MagicMax
    {
        get => Record.MagicMax;
        set { Record.MagicMax = value; Poke(CharacterFormat.OffMagicMax, 2); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- status --------------------------------------------------------------
    public int Status
    {
        get => Record.Status;
        set { Record.Status = value; Poke(CharacterFormat.OffStatus, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- quick actions -------------------------------------------------------
    public void FullHeal()
    {
        Record.BodyCurrent = Record.BodyMax; Poke(CharacterFormat.OffBodyCur, 2);
        Record.MagicCurrent = Record.MagicMax; Poke(CharacterFormat.OffMagicCur, 2);
        Record.Status = CharacterFormat.StatusFine; Poke(CharacterFormat.OffStatus, 1);
        OnPropertyChanged(nameof(BodyCurrent)); OnPropertyChanged(nameof(MagicCurrent));
        OnPropertyChanged(nameof(Status)); RaiseDerived();
    }

    // --- IItemPack -----------------------------------------------------------
    public bool HasFreeSlot => Record.ItemCount < CharacterFormat.ItemSlotCount;

    public bool TryAddItem(int itemId)
    {
        if (itemId == 0) return false;
        int slot = Record.AddItem(itemId);
        if (slot < 0) return false;
        Poke(CharacterFormat.ItemOffset(slot), 1);
        foreach (var s in Inventory) s.Refresh();
        RaiseDerived();
        return true;
    }

    /// <summary>Fills every empty carried slot with copies of <paramref name="itemId"/>.</summary>
    public int FillPack(int itemId)
    {
        int added = 0;
        while (TryAddItem(itemId)) added++;
        return added;
    }

    /// <summary>Empties all ten carried pack slots, leaving readied equipment alone.</summary>
    public void ClearPack()
    {
        for (int i = 0; i < CharacterFormat.ItemSlotCount; i++)
        {
            Record.SetItem(i, 0);
            Poke(CharacterFormat.ItemOffset(i), 1);
        }
        foreach (var s in Inventory) s.Refresh();
        RaiseDerived();
    }

    public void MaxAttributes()
    {
        for (int i = 0; i < CharacterFormat.AttributeCount; i++)
        { Record.SetAttribute(i, CharacterFormat.MaxAttribute); Poke(CharacterFormat.AttributeOffsets[i], 2); }
        foreach (var a in Attributes) a.Refresh();
        RaiseDerived();
    }

    public void MaxMoney()
    {
        Record.Gold = CharacterFormat.MaxGold; Poke(CharacterFormat.OffGold, 2);
        OnPropertyChanged(nameof(Gold));
    }

    public void MaxEverything()
    {
        MaxAttributes();
        // "Max" must never take anything away: a character who has outgrown these targets keeps
        // what they have.
        Record.BodyMax = Math.Max(Record.BodyMax, CharacterFormat.MaxVital); Poke(CharacterFormat.OffBodyMax, 2);
        Record.BodyCurrent = Record.BodyMax; Poke(CharacterFormat.OffBodyCur, 2);
        Record.MagicMax = Math.Max(Record.MagicMax, CharacterFormat.MaxVital); Poke(CharacterFormat.OffMagicMax, 2);
        Record.MagicCurrent = Record.MagicMax; Poke(CharacterFormat.OffMagicCur, 2);
        Record.Level = Math.Max(Record.Level, CharacterFormat.MaxLevel); Poke(CharacterFormat.OffLevel, 2);
        // Experience is left alone — level is set directly, and raising XP past the threshold only
        // invites the game to level the character again. The threshold goes out of reach instead.
        Record.NextLevel = CharacterFormat.MaxNextLevel; Poke(CharacterFormat.OffNextLevel, 4);
        Record.Gold = Math.Max(Record.Gold, CharacterFormat.MaxGold); Poke(CharacterFormat.OffGold, 2);
        Record.Status = CharacterFormat.StatusFine; Poke(CharacterFormat.OffStatus, 1);
        RefreshEditors(); RaiseDerived();
    }

    // --- freeze / live refresh ----------------------------------------------
    public void ApplyFreeze()
    {
        if (!_host.IsAttached) return;
        if (FreezeBody && Record.BodyCurrent != Record.BodyMax)
        { Record.BodyCurrent = Record.BodyMax; Poke(CharacterFormat.OffBodyCur, 2); }
        if (FreezeMagic && Record.MagicCurrent < Record.MagicMax)
        { Record.MagicCurrent = Record.MagicMax; Poke(CharacterFormat.OffMagicCur, 2); }
        if (FreezeStatus && Record.Status != CharacterFormat.StatusFine)
        { Record.Status = CharacterFormat.StatusFine; Poke(CharacterFormat.OffStatus, 1); }
    }

    /// <summary>
    /// One poll tick: drop any working copy that no longer holds this character, refresh from the
    /// copy the game is actually playing out of, and re-apply the freezes.
    /// </summary>
    public void Poll()
    {
        if (!_host.IsAttached) return;

        // The party array is a fixed set of slots the game reuses. Adding, removing or reordering
        // party members in-game hands our address to somebody else, so re-check before trusting it.
        _mirrors.RemoveAll(m => !HoldsThisCharacter(m));
        if (!HoldsThisCharacter(Address)) { IsStale = true; return; }
        IsStale = false;

        if (_host.ReadBytes(LiveAddress, _scratch, CharacterFormat.RecordSize))
            RefreshLiveSummary(_scratch);
        ApplyFreeze();
    }

    /// <summary>Reads the record at <paramref name="address"/> and checks it is still ours.</summary>
    private bool HoldsThisCharacter(nuint address)
    {
        if (!_host.ReadBytes(address, _scratch, CharacterFormat.RecordSize)) return false;
        if (_scratch[CharacterFormat.OffExists] != 1) return false;
        if (_scratch[CharacterFormat.OffNameLen] != _identity[0]) return false;
        if (_scratch[CharacterFormat.OffClass] != _identity[^1]) return false;
        for (int i = 0; i < CharacterFormat.NameLength; i++)
            if (_scratch[CharacterFormat.OffName + i] != _identity[1 + i]) return false;
        return true;
    }

    public void RefreshLiveSummary(byte[] fresh)
    {
        Array.Copy(fresh, 0, Record.Bytes, 0, CharacterFormat.RecordSize);
        RefreshEditors();
        RaiseDerived();
    }

    // --- write plumbing ------------------------------------------------------
    private void Poke(int offset, int length)
    {
        if (!_host.IsAttached || IsStale) return;
        _host.WriteBytes(Address, Record.Bytes, offset, length);
        // The game plays out of its party working copies and writes them back over the roster
        // when it saves, so an edit that only lands on the roster is quietly undone.
        foreach (var mirror in _mirrors)
            _host.WriteBytes(mirror, Record.Bytes, offset, length);
    }

    private void RaiseDerived()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ListLabel));
    }

    private void RefreshEditors()
    {
        foreach (var a in Attributes) a.Refresh();
        foreach (var s in Inventory) s.Refresh();
        foreach (var s in Equipment) s.Refresh();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(BodyCurrent)); OnPropertyChanged(nameof(BodyMax));
        OnPropertyChanged(nameof(MagicCurrent)); OnPropertyChanged(nameof(MagicMax));
        OnPropertyChanged(nameof(Level)); OnPropertyChanged(nameof(Experience));
        OnPropertyChanged(nameof(NextLevel)); OnPropertyChanged(nameof(Gold));
        OnPropertyChanged(nameof(ClassIndex));
        OnPropertyChanged(nameof(Status));
    }
}
