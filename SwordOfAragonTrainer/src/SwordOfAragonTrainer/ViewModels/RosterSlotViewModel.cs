using SwordOfAragonTrainer.Game;

namespace SwordOfAragonTrainer.ViewModels;

/// <summary>
/// One roster slot as an editable row. Changing type or equipment recomputes the fields the game
/// derives from them (make/train/upkeep cost and stacking size) using the price tables, so an edited
/// record is the same shape the game would have written itself.
/// </summary>
public sealed class RosterSlotViewModel : ObservableObject
{
    private readonly RosterRecord _record;
    private readonly RosterFile _roster;
    private readonly IEditHost _host;

    public RosterSlotViewModel(RosterFile roster, RosterRecord record, IEditHost host)
    {
        _roster = roster;
        _record = record;
        _host = host;
        Equipment = UnitBook.Slots.Select(s => new EquipmentSlotViewModel(this, s)).ToArray();
    }

    /// <summary>Slot index 0..79.</summary>
    public int Slot => _record.Slot;

    /// <summary>"Char 0" / "Unit 12" — which half of the roster the slot lives in.</summary>
    public string SlotLabel => _record.IsCharacterSlot
        ? $"Char {_record.Slot}"
        : $"Unit {_record.Slot - RosterFormat.FirstUnitSlot}";

    /// <summary>True if the slot holds a real record.</summary>
    public bool IsOccupied => _record.IsOccupied;

    /// <summary>True for the player's own character, which the game keeps in slot 0.</summary>
    public bool IsPlayer => _record.Slot == RosterFormat.PlayerSlot;

    /// <summary>True for a troop-unit slot, where a strength above one figure makes sense.</summary>
    public bool IsUnitSlot => !_record.IsCharacterSlot;

    public string Name
    {
        get => _record.Name;
        set => Apply(() => _record.Name = value, nameof(Name), "name");
    }

    /// <summary>The unit/character types this slot may hold — characters in a character slot, troops elsewhere.</summary>
    public IReadOnlyList<UnitType> AllowedTypes => _record.IsCharacterSlot
        ? UnitBook.Types.Where(t => t.IsCharacter).ToArray()
        : UnitBook.Types.Where(t => !t.IsCharacter).ToArray();

    public UnitType? Type
    {
        get => UnitBook.Type(_record.TypeCode);
        set
        {
            if (value == null || value.Code == _record.TypeCode) return;
            // Writing the type is what makes a slot "occupied", so refuse it on an empty slot: the rest
            // of the record would stay zeroed, leaving a nameless 0-strength unit on hex (0,0) in the
            // middle of a range the game packs from the front. Every other Army-tab action already
            // requires IsOccupied; this is the one bound directly to a control.
            if (!_record.IsOccupied) return;
            _record.TypeCode = value.Code;
            if (IsPlayer)
            {
                // Slot 0's class is what UnitBook.Discount keys off, so changing it changes the
                // make/train/upkeep of every troop unit in the file, not just this record.
                _roster.RecomputeAllDerived();
                RefreshAll();
                _host.NotifyRosterRecomputed();
                _host.MarkDirty($"{DisplayName}: class set to {value.Name}; every unit's costs recomputed");
            }
            else
            {
                _record.RecomputeDerived(_roster.PlayerClassCode);
                RefreshAll();
                _host.MarkDirty($"{DisplayName}: type set to {value.Name}");
            }
        }
    }

    public string TypeName => _record.TypeName;

    public int Level
    {
        get => _record.Level;
        set => Apply(() => _record.Level = value, nameof(Level), "level");
    }

    public int Men
    {
        get => _record.Men;
        set => Apply(() => _record.Men = value, nameof(Men), "men");
    }

    public double Experience
    {
        get => _record.Experience;
        set => Apply(() => _record.Experience = value, nameof(Experience), "experience");
    }

    public int X
    {
        get => _record.X;
        set => Apply(() => _record.X = value, nameof(X), "map X");
    }

    public int Y
    {
        get => _record.Y;
        set => Apply(() => _record.Y = value, nameof(Y), "map Y");
    }

    public int Hits
    {
        get => _record.Hits;
        set => Apply(() => _record.Hits = value, nameof(Hits), "hits");
    }

    public string Position => $"{_record.X},{_record.Y}";

    // --- read-only derived figures ---------------------------------------------
    public int MakeCost => _record.MakeCost;
    public int TrainCost => _record.TrainCost;
    public double MaintGold => _record.MaintGold;
    public int SizePoints => _record.SizePoints;
    public int StackingCost => _record.StackingCost;
    public int ArmorClassHand => _record.ArmorClassHand;
    public int ArmorClassMissile => _record.ArmorClassMissile;
    public int HandDamage => _record.HandDamage;
    public int HandBonus => _record.HandBonus;
    public int MoveMax => _record.MoveMax;

    /// <summary>Warns when the unit alone would break the 200-point stacking limit for a hex.</summary>
    public bool ExceedsStackingLimit => StackingCost > GameFacts.StackingLimit;

    /// <summary>The spells this record can cast at its current level, for the detail panel.</summary>
    public string SpellList
    {
        get
        {
            var spells = SpellBook.Available(_record.TypeCode, _record.Level).Select(s => s.Name).ToArray();
            return spells.Length == 0 ? "—" : string.Join(", ", spells);
        }
    }

    /// <summary>The eight equipment slots as bindable rows.</summary>
    public IReadOnlyList<EquipmentSlotViewModel> Equipment { get; }

    /// <summary>Label for the detail panel header and status messages.</summary>
    public string DisplayName => _record.IsOccupied ? _record.Name : SlotLabel;

    /// <summary>Grid summary column.</summary>
    public string Summary => _record.Summary;

    // --- actions ---------------------------------------------------------------
    /// <summary>Sets the level to the trainer's ceiling.</summary>
    public void MaxLevel()
    {
        _record.Level = RosterFormat.MaxLevel;
        RefreshAll();
        _host.MarkDirty($"{DisplayName}: level {RosterFormat.MaxLevel}");
    }

    /// <summary>Fills the unit up to the largest strength that still fits one hex's stacking limit.</summary>
    public void FillToStackingLimit()
    {
        int size = Math.Max(1, _record.SizePoints);
        _record.Men = Math.Min(RosterFormat.MaxMen, GameFacts.StackingLimit / size);
        RefreshAll();
        _host.MarkDirty($"{DisplayName}: strength {_record.Men} ({GameFacts.StackingLimit} stacking points)");
    }

    /// <summary>Restores the month's movement allowance.</summary>
    public void RefillMovement()
    {
        _record.MoveLeft = _record.MoveMax;
        RefreshAll();
        _host.MarkDirty($"{DisplayName}: movement restored");
    }

    /// <summary>Moves the record to a map hex.</summary>
    public void MoveTo(int x, int y, string where)
    {
        _record.X = x;
        _record.Y = y;
        RefreshAll();
        _host.MarkDirty($"{DisplayName}: moved to {where}");
    }

    /// <summary>
    /// Gives every slot the best item the record's level allows, then recomputes the derived costs.
    ///
    /// Whether the record is mounted is <b>preserved</b>, not decided here: a unit that has no horse
    /// keeps none (and so keeps no barding), and one that has a horse gets the best horse and barding it
    /// qualifies for. Upgrading a foot unit onto a horse would produce a (type, horse) pairing that
    /// appears nowhere in the corpus the cost model was validated against — Infantry with a stacking
    /// size of 5, a make cost including a mount, and foot movement, because movement is one of the
    /// figures only the game recomputes.
    /// </summary>
    public void EquipBest()
    {
        bool mounted = _record.GetEquipment(EquipmentSlot.Horse) > 0;
        foreach (var slot in UnitBook.Slots)
        {
            if ((slot == EquipmentSlot.Horse || slot == EquipmentSlot.Barding) && !mounted) continue;
            _record.SetEquipment(slot, _record.HighestAllowedEquipment(slot));
        }
        _record.RecomputeDerived(_roster.PlayerClassCode);
        RefreshAll();
        _host.MarkDirty($"{DisplayName}: equipped with the best its level allows" +
                        (mounted ? "" : " (left on foot)"));
    }

    /// <summary>Called by an equipment row after it writes.</summary>
    internal void OnEquipmentChanged(EquipmentSlot slot, EquipmentItem item)
    {
        _record.RecomputeDerived(_roster.PlayerClassCode);
        RefreshDerived();
        _host.MarkDirty($"{DisplayName}: {UnitBook.SlotName(slot)} = {item.Name}");
    }

    internal int GetEquipment(EquipmentSlot slot) => _record.GetEquipment(slot);

    internal void SetEquipment(EquipmentSlot slot, int index) => _record.SetEquipment(slot, index);

    /// <summary>The record's level, so an equipment row can flag an item it does not yet qualify for.</summary>
    internal int CurrentLevel => _record.Level;

    /// <summary>Re-reads everything the UI shows.</summary>
    public void RefreshAll()
    {
        foreach (var name in new[]
                 {
                     nameof(Name), nameof(Type), nameof(TypeName), nameof(Level), nameof(Men),
                     nameof(Experience), nameof(X), nameof(Y), nameof(Hits), nameof(Position),
                     nameof(IsOccupied), nameof(Summary), nameof(DisplayName), nameof(SpellList),
                     nameof(MoveMax),
                 })
            OnPropertyChanged(name);
        RefreshDerived();
        foreach (var row in Equipment) row.Refresh();
    }

    private void RefreshDerived()
    {
        foreach (var name in new[]
                 {
                     nameof(MakeCost), nameof(TrainCost), nameof(MaintGold), nameof(SizePoints),
                     nameof(StackingCost), nameof(ExceedsStackingLimit), nameof(ArmorClassHand),
                     nameof(ArmorClassMissile), nameof(HandDamage), nameof(HandBonus),
                 })
            OnPropertyChanged(name);
    }

    private void Apply(Action write, string property, string what)
    {
        write();
        OnPropertyChanged(property);
        // DisplayName tracks Name, and the detail panel's header plus every later status message read
        // it, so it has to be raised here or a renamed row keeps reporting its old name.
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Position));
        OnPropertyChanged(nameof(SpellList));
        OnPropertyChanged(nameof(StackingCost));
        OnPropertyChanged(nameof(ExceedsStackingLimit));
        _host.MarkDirty($"{DisplayName}: {what}");
    }
}

/// <summary>One equipment slot of one roster record, as a combo-box row.</summary>
public sealed class EquipmentSlotViewModel : ObservableObject
{
    private readonly RosterSlotViewModel _owner;

    public EquipmentSlotViewModel(RosterSlotViewModel owner, EquipmentSlot slot)
    {
        _owner = owner;
        Slot = slot;
    }

    public EquipmentSlot Slot { get; }

    public string Name => UnitBook.SlotName(Slot);

    /// <summary>Every item the slot offers, "none" first.</summary>
    public IReadOnlyList<EquipmentItem> Items => UnitBook.Items(Slot);

    public EquipmentItem? Selected
    {
        get => UnitBook.Item(Slot, _owner.GetEquipment(Slot));
        set
        {
            if (value == null || value.Index == _owner.GetEquipment(Slot)) return;
            _owner.SetEquipment(Slot, value.Index);
            Refresh();
            _owner.OnEquipmentChanged(Slot, value);
        }
    }

    /// <summary>Flags an item the record's level does not yet permit — the game would refuse it.</summary>
    public bool AboveLevel => Selected is { MinLevel: > 0 } item && item.MinLevel > _owner.CurrentLevel;

    /// <summary>Level requirement text for the detail panel.</summary>
    public string Requirement => Selected is { MinLevel: > 0 } item ? $"needs level {item.MinLevel}" : "";

    internal void Refresh()
    {
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(AboveLevel));
        OnPropertyChanged(nameof(Requirement));
    }
}
