using System.Collections.ObjectModel;
using Space1889Trainer.Game;

namespace Space1889Trainer.ViewModels;

/// <summary>
/// Editor for one character slot. Bound fields write straight through to the host's <see cref="GameState"/> as
/// they change (one field, its own bytes), and <see cref="Reload"/> repopulates them from the latest snapshot.
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly IStateHost _host;
    private bool _loading;

    private string _name = "";
    private bool _isFemale, _isEmpty = true, _hasHorse;
    private long _wealth, _income;
    private int _maxHealth, _health, _fatigue, _mental, _bodyWeight;
    private int _newItemId = ItemBook.Shot;
    private int _newItemRounds = 50;

    public CharacterViewModel(IStateHost host, int slot)
    {
        _host = host;
        Slot = slot;

        for (int i = 0; i < StateFormat.AttributeCount; i++)
        {
            int a = i;
            // STR and END move the HEALTH line's knock-out threshold, and STR/AGI/END the fatigue limits.
            Attributes.Add(new NamedValueViewModel(a, SkillBook.Attributes[a], "", SkillBook.AttributeAbbreviations[a],
                CharacterRecord.MinAttribute, CharacterRecord.MaxValue,
                v => { bool ok = Write(r => r.SetAttribute(a, v)); if (ok) RaiseDerived(); return ok; }, _ => { }));
        }
        for (int i = 0; i < StateFormat.SkillCount; i++)
        {
            int s = i;
            Skills.Add(new NamedValueViewModel(s, SkillBook.Skills[s], SkillBook.Attributes[SkillBook.AttributeOf(s)], SkillBook.SkillUses[s],
                0, CharacterRecord.MaxValue, v => Write(r => r.SetSkill(s, v)), _ => { }));
        }
        for (int i = 0; i < StateFormat.InventorySlots; i++) Inventory.Add(new InventoryRowViewModel(this, i));

        FullHealCommand = new RelayCommand(() => Act("Healed", r => r.FullHeal()), () => CanEdit);
        MaxAttributesCommand = new RelayCommand(() => Act("Attributes raised to at least 6", r => r.MaxAttributes()), () => CanEdit);
        MaxSkillsCommand = new RelayCommand(() => Act("Skills raised to at least 6", r => r.MaxSkills()), () => CanEdit);
        RefillAmmoCommand = new RelayCommand(() => Act("Firearms reloaded", r => r.RefillAmmunition()), () => CanEdit);
        AddMoneyCommand = new RelayCommand(() => Act("£1,000 added", r => r.SetWealth(r.Wealth + 1000L * GameFacts.PenniesPerPound)), () => CanEdit);
        MaxEverythingCommand = new RelayCommand(
            () => Act("Maxed out", r => r.MaxAttributes() & r.MaxSkills() & r.FullHeal() & r.RefillAmmunition()), () => CanEdit);
        AddItemCommand = new RelayCommand(AddItem, () => CanEdit && ItemCount < StateFormat.InventorySlots);
    }

    public int Slot { get; }

    public bool CanEdit => _host.CanEdit && !_isEmpty;

    private CharacterRecord? Record => _host.State is { IsLoaded: true } s ? new CharacterRecord(s, Slot) : null;

    // ---- commands -----------------------------------------------------------------------------

    public RelayCommand FullHealCommand { get; }
    public RelayCommand MaxAttributesCommand { get; }
    public RelayCommand MaxSkillsCommand { get; }
    public RelayCommand RefillAmmoCommand { get; }
    public RelayCommand AddMoneyCommand { get; }
    public RelayCommand MaxEverythingCommand { get; }
    public RelayCommand AddItemCommand { get; }

    // ---- collections ---------------------------------------------------------------------------

    public ObservableCollection<NamedValueViewModel> Attributes { get; } = new();

    public ObservableCollection<NamedValueViewModel> Skills { get; } = new();

    public ObservableCollection<InventoryRowViewModel> Inventory { get; } = new();

    /// <summary>Every item a character can carry, for the item pickers.</summary>
    public static IReadOnlyList<ItemInfo> ItemChoices { get; } = ItemBook.Holdable.ToList();

    /// <summary>The four ammunition types, for the loaded-ammo pickers.</summary>
    public static IReadOnlyList<ItemInfo> AmmoChoices { get; } = ItemBook.AmmunitionTypes.ToList();

    // ---- identity ------------------------------------------------------------------------------

    public bool IsEmpty
    {
        get => _isEmpty;
        private set { if (SetField(ref _isEmpty, value)) OnPropertyChanged(nameof(HasCharacter)); }
    }

    /// <summary>The slot holds a character; the editor panels are hidden otherwise, so no stale values show.</summary>
    public bool HasCharacter => !_isEmpty;

    public string Title => _isEmpty ? $"Slot {Slot + 1} (empty)" : $"{Slot + 1}. {_name}";

    public string Name
    {
        get => _name;
        set
        {
            string v = (value ?? "").Trim().ToUpperInvariant();
            if (v.Length > StateFormat.NameLength - 1) v = v[..(StateFormat.NameLength - 1)];
            if (!SetField(ref _name, v) || _loading) return;
            if (v.Length == 0) { Report("A name cannot be empty (the game treats a nameless slot as unused)."); Reload(); return; }
            Write(r => r.SetName(v));
            OnPropertyChanged(nameof(Title));
        }
    }

    public bool IsFemale
    {
        get => _isFemale;
        set { if (SetField(ref _isFemale, value) && !_loading) Write(r => r.SetFemale(value)); }
    }

    public string Careers { get; private set; } = "";

    public string SocialClass { get; private set; } = "";

    // ---- money ---------------------------------------------------------------------------------

    public long Wealth
    {
        get => _wealth;
        set
        {
            long v = Math.Clamp(value, 0, uint.MaxValue);
            if (!SetField(ref _wealth, v) || _loading) return;
            Write(r => r.SetWealth(v));
            OnPropertyChanged(nameof(WealthText));
        }
    }

    public string WealthText => GameFacts.FormatMoney(_wealth);

    public long MonthlyIncome
    {
        get => _income;
        set
        {
            long v = Math.Clamp(value, 0, uint.MaxValue);
            if (!SetField(ref _income, v) || _loading) return;
            Write(r => r.SetMonthlyIncome(v));
            OnPropertyChanged(nameof(IncomeText));
        }
    }

    public string IncomeText => GameFacts.FormatMoney(_income) + " a month";

    // ---- condition -----------------------------------------------------------------------------

    public int MaxHealth
    {
        get => _maxHealth;
        set
        {
            int v = Math.Clamp(value, 1, 127);
            if (!SetField(ref _maxHealth, v) || _loading) return;
            Write(r => r.SetMaxHealth(v));
            RaiseHealth();
        }
    }

    public int Health
    {
        get => _health;
        set
        {
            int v = Math.Clamp(value, 0, 127);
            if (!SetField(ref _health, v) || _loading) return;
            Write(r => r.SetHealth(v));
            RaiseHealth();
        }
    }

    public int Fatigue
    {
        get => _fatigue;
        set
        {
            int v = Math.Clamp(value, 0, 127);
            if (!SetField(ref _fatigue, v) || _loading) return;
            Write(r => r.SetFatigue(v));
            RaiseHealth();
        }
    }

    public int Mental
    {
        get => _mental;
        set
        {
            int v = Math.Clamp(value, 0, 127);
            if (SetField(ref _mental, v) && !_loading) Write(r => r.SetMental(v));
        }
    }

    public bool HasHorse
    {
        get => _hasHorse;
        set { if (SetField(ref _hasHorse, value) && !_loading) Write(r => r.SetHorse(value)); }
    }

    public int BodyWeight
    {
        get => _bodyWeight;
        set
        {
            int v = Math.Clamp(value, 0, 9999);
            if (SetField(ref _bodyWeight, v) && !_loading) Write(r => r.SetBodyWeight(v));
        }
    }

    /// <summary>"HEALTH: 8/4" exactly as the status panel prints it.</summary>
    public string HealthText { get; private set; } = "";

    public string ConditionText { get; private set; } = "";

    public int CarriedWeight { get; private set; }

    public int ItemCount { get; private set; }

    // ---- adding items ---------------------------------------------------------------------------

    public int NewItemId { get => _newItemId; set => SetField(ref _newItemId, value); }

    public int NewItemRounds { get => _newItemRounds; set => SetField(ref _newItemRounds, Math.Clamp(value, 0, ushort.MaxValue)); }

    private void AddItem()
    {
        if (Record is not { } r || !CanEdit || !Resync()) return;
        int slot = r.AddItem(NewItemId, NewItemRounds);
        if (slot < 0)
        {
            Report(r.ItemCount >= StateFormat.InventorySlots ? $"{_name} cannot carry more than 21 items." : "The game refused the write.");
            Reload();
            return;
        }
        _host.AfterWrite();
        Report($"{ItemBook.NameOf(NewItemId)} added to {_name}'s pack (slot {slot + 1}). USE it in the game to equip it.");
        Reload();
    }

    internal void RemoveItem(int index)
    {
        if (Record is not { } r || !CanEdit || !Resync()) return;
        string name = ItemBook.NameOf(r.GetEntry(index).ItemId);
        if (r.RemoveItem(index)) { _host.AfterWrite(); Report($"{name} removed from {_name}'s pack."); }
        else Report("The game refused the write.");
        Reload();
    }

    internal void UseItem(int index)
    {
        if (Record is not { } r || !CanEdit || !Resync()) return;
        var item = ItemBook.ById(r.GetEntry(index).ItemId);
        bool ok = item?.Kind == ItemType.Armor ? r.Wear(index) : r.Wield(index);
        if (ok) { _host.AfterWrite(); Report($"{_name} now {(item?.Kind == ItemType.Armor ? "wears" : "holds")} the {item?.Name}."); }
        else Report("That item cannot be equipped that way.");
        Reload();
    }

    internal bool WriteEntry(int index, InventoryEntry entry)
    {
        bool ok = Write(r => r.SetEntry(index, entry));
        if (ok) Reload();
        return ok;
    }

    /// <summary>Swaps a slot's item, normalising its ammunition and equipment references (<see cref="CharacterRecord.ReplaceItem"/>).</summary>
    internal bool ReplaceItem(int index, int itemId)
    {
        bool ok = Write(r => r.ReplaceItem(index, itemId));
        Reload();
        return ok;
    }

    internal void WriteInUse(int index, bool value)
    {
        Write(r => r.SetInUse(index, value));
        Reload();   // the row's Status column is derived from the flag
    }

    // ---- plumbing ------------------------------------------------------------------------------

    /// <summary>Repopulates every field from the host's snapshot without writing anything back.</summary>
    public void Reload()
    {
        _loading = true;
        try
        {
            var r = Record;
            IsEmpty = r is null || r.IsEmpty;
            // CanEdit depends on the host as well as the slot, and the editor panels bind IsEnabled to it.
            OnPropertyChanged(nameof(CanEdit));
            if (r is null || r.IsEmpty)
            {
                Name = "";
                Careers = SocialClass = HealthText = ConditionText = "";
                CarriedWeight = ItemCount = 0;
                foreach (var name in new[] { nameof(Title), nameof(Careers), nameof(SocialClass), nameof(HealthText),
                                             nameof(ConditionText), nameof(CarriedWeight), nameof(ItemCount) })
                    OnPropertyChanged(name);
                RaiseCommands();
                return;
            }

            Name = r.Name;
            IsFemale = r.IsFemale;
            Wealth = r.Wealth;
            MonthlyIncome = r.MonthlyIncome;
            MaxHealth = r.MaxHealth;
            Health = r.Health;
            Fatigue = r.Fatigue;
            Mental = r.Mental;
            HasHorse = r.HasHorse;
            BodyWeight = r.BodyWeight;
            for (int i = 0; i < Attributes.Count; i++) Attributes[i].Load(r.GetAttribute(i));
            for (int i = 0; i < Skills.Count; i++) Skills[i].Load(r.GetSkill(i));
            for (int i = 0; i < Inventory.Count; i++)
                Inventory[i].Load(r.GetEntry(i), r.IsInUse(i), r.WeaponSlot == i + 1, r.ArmorSlot == i + 1);

            string c1 = r.Career1, c2 = r.Career2;
            Careers = c2.Length == 0 ? c1 : $"{c1} / {c2}";
            SocialClass = r.SocialClass;
            CarriedWeight = r.CarriedWeight;
            ItemCount = r.ItemCount;
            OnPropertyChanged(nameof(Careers));
            OnPropertyChanged(nameof(SocialClass));
            OnPropertyChanged(nameof(CarriedWeight));
            OnPropertyChanged(nameof(ItemCount));
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(WealthText));
            OnPropertyChanged(nameof(IncomeText));
            RaiseHealth();
            RaiseCommands();
        }
        finally { _loading = false; }
    }

    /// <summary>After an attribute edit: the HEALTH line (STR, END) and the social class (SOC) are derived from it.</summary>
    private void RaiseDerived()
    {
        RaiseHealth();
        if (Record is { IsEmpty: false } r) SocialClass = r.SocialClass;
        OnPropertyChanged(nameof(SocialClass));
    }

    private void RaiseHealth()
    {
        if (Record is { IsEmpty: false } r)
        {
            HealthText = $"HEALTH: {r.Health}/{r.UnconsciousThreshold}";
            ConditionText = r.IsOutOfAction ? "OUT OF ACTION" : "able";
        }
        OnPropertyChanged(nameof(HealthText));
        OnPropertyChanged(nameof(ConditionText));
    }

    public void RaiseCommands()
    {
        FullHealCommand.RaiseCanExecuteChanged();
        MaxAttributesCommand.RaiseCanExecuteChanged();
        MaxSkillsCommand.RaiseCanExecuteChanged();
        RefillAmmoCommand.RaiseCanExecuteChanged();
        AddMoneyCommand.RaiseCanExecuteChanged();
        MaxEverythingCommand.RaiseCanExecuteChanged();
        AddItemCommand.RaiseCanExecuteChanged();
        foreach (var row in Inventory) row.RaiseCommands();
    }

    private bool Write(Func<CharacterRecord, bool> write)
    {
        if (_loading || _host.SuppressWriteBack) return true;
        if (!CanEdit || Record is not { } r)
        {
            Report("Not connected to a game or save — nothing was written.");
            Reload();
            return false;
        }
        if (!Resync()) { Reload(); return false; }
        if (!write(r))
        {
            Report($"The write to {_name} was refused (the game may have closed or moved on); values reloaded.");
            Reload();
            return false;
        }
        _host.AfterWrite();
        return true;
    }

    private void Act(string what, Func<CharacterRecord, bool> action)
    {
        if (!CanEdit || Record is not { } r || !Resync()) return;
        bool ok = action(r);
        _host.AfterWrite();
        Report(ok ? $"{what}: {_name}." : $"{what}: {_name} — some writes were refused.");
        Reload();
    }

    /// <summary>
    /// Re-reads the block just before a write. A live snapshot can be one poll old: without this, a quick action could
    /// compare against a health or pack the game has since changed, skip a write that was needed, or shift inventory
    /// entries that are no longer there.
    /// </summary>
    private bool Resync()
    {
        if (_host.State is { } state && state.Refresh()) return true;
        Report("Could not re-read the game state; nothing was written.");
        return false;
    }

    private void Report(string message) => _host.Report(message);
}
