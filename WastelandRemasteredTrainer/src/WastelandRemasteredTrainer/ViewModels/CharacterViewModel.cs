using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using WastelandRemasteredTrainer.Game;

namespace WastelandRemasteredTrainer.ViewModels;

/// <summary>
/// Per-character view model. Wraps a <see cref="CharacterRecord"/> and exposes its properties
/// for two-way WPF binding, plus freeze toggles and quick action buttons.
///
/// <para><b>Edits are tracked, not snapshotted.</b> Each editable field remembers whether the
/// user has touched it since the last read. <see cref="Write"/> writes only the touched fields,
/// so committing a change to one stat cannot roll back the experience, money and level the
/// game has awarded since the character was located. <see cref="Refresh"/> likewise updates
/// only the untouched fields, so the live view keeps tracking the game without overwriting a
/// half-typed edit.</para>
///
/// <para>Skills and inventory rows are different: each is a single self-contained value with no
/// cached snapshot behind it, so they write through the moment they are edited.</para>
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly CharacterRecord _record;
    private readonly ICharacterHost _host;

    /// <summary>Names of the editable fields the user has changed but not yet written.</summary>
    private readonly HashSet<string> _dirty = new(StringComparer.Ordinal);

    private bool _loading;

    // Freeze flags
    private bool _freezeCon;
    private bool _freezeMoney;
    private bool _freezeAmmo;

    /// <summary>The money value a freeze pins to — captured when the freeze is switched on.</summary>
    private int _frozenMoney;

    /// <summary>
    /// Whether <see cref="_frozenMoney"/> holds a captured amount. A flag rather than a
    /// "greater than zero" test, so pinning money at zero works like any other amount.
    /// </summary>
    private bool _moneyPinned;

    // Editable fields (mirrors of the record, refreshed while untouched)
    private int _strength, _iq, _luck, _speed, _agility, _dextermity, _charisma;
    private int _money, _maxCon, _curCon, _uncCon;
    private int _experience, _level, _skillPoints;
    private int _ac, _weapon, _armor, _disease, _sex, _nationality;

    private string _name = "";
    private string _cName = "";
    private string _rank = "";
    private bool _isNpc;

    private int _learnLevel = GameFacts.MaxSkillLevel;
    private int _addItemId;
    private int _addItemAmmo = GameFacts.MaxAmmo;

    public CharacterViewModel(CharacterRecord record, ICharacterHost host)
    {
        _record = record;
        _host = host;

        FullHealCommand = new RelayCommand(_ => FullHeal());
        MaxAttributesCommand = new RelayCommand(_ => MaxAttributes());
        MaxMoneyCommand = new RelayCommand(_ => MaxMoney());
        MaxSkillsCommand = new RelayCommand(_ => MaxSkills());
        LearnAllSkillsCommand = new RelayCommand(_ => LearnAllSkills());
        MaxAmmoCommand = new RelayCommand(_ => MaxAmmoAction());
        ClearJamsCommand = new RelayCommand(_ => ClearJams());
        MaxEverythingCommand = new RelayCommand(_ => MaxEverything());
        WriteCommand = new RelayCommand(_ => Write(), _ => HasPendingEdits);
        RevertCommand = new RelayCommand(_ => Revert(), _ => HasPendingEdits);
        RefreshCommand = new RelayCommand(_ => Refresh());
        AddItemCommand = new RelayCommand(_ => AddItem(), _ => _addItemId > 0);

        Refresh();
    }

    /// <summary>Address of the underlying object, for diagnostics.</summary>
    public string AddressText => $"0x{_record.Address:X}";

    /// <summary>Slot label for display.</summary>
    public string SlotLabel => $"Slot {_record.Slot + 1}";

    /// <summary>Character name; falls back to the raw CName bytes when the managed string is empty.</summary>
    public string Name => _name.Length > 0 ? _name : _cName;

    /// <summary>The name as it appears in the list, with the slot number.</summary>
    public string ListLabel => $"{_record.Slot + 1}. {(Name.Length > 0 ? Name : "(unnamed)")}";

    public string Rank => _rank;

    public string NpcLabel => _isNpc ? "NPC" : "PC";

    /// <summary>True when the user has edits that have not been written to the game.</summary>
    public bool HasPendingEdits => _dirty.Count > 0;

    /// <summary>How many fields are edited but not yet written.</summary>
    public int PendingCount => _dirty.Count;

    /// <summary>The names of the fields currently edited but not written.</summary>
    public IReadOnlyCollection<string> PendingFieldNames => _dirty;

    /// <summary>
    /// Every field <see cref="Write"/> knows how to commit. This is the canonical list: the
    /// harness asserts that editing all of them marks exactly this set pending, so adding an
    /// editable property without a matching case in <see cref="WriteField"/> — which would
    /// leave it silently unwritten — fails the build's verification step.
    /// </summary>
    public static IReadOnlyList<string> EditableFieldNames { get; } = new[]
    {
        nameof(Strength), nameof(IQ), nameof(Luck), nameof(Speed),
        nameof(Agility), nameof(Dextermity), nameof(Charisma),
        nameof(MaxCon), nameof(CurCon), nameof(UncCon),
        nameof(Money), nameof(Experience), nameof(Level), nameof(SkillPoints),
        nameof(AC), nameof(Weapon), nameof(Armor), nameof(Disease),
        nameof(Sex), nameof(Nationality),
    };

    public string PendingLabel => _dirty.Count == 0
        ? "No unwritten edits."
        : $"{_dirty.Count} unwritten edit(s) — click Write.";

    // --- freeze toggles ---------------------------------------------------------
    public bool FreezeCon
    {
        get => _freezeCon;
        set => SetField(ref _freezeCon, value);
    }

    /// <summary>
    /// Pins money to the value showing when the box is ticked. Captured on the way in rather
    /// than read every tick, so the freeze holds a number the user chose.
    /// </summary>
    public bool FreezeMoney
    {
        get => _freezeMoney;
        set
        {
            if (!SetField(ref _freezeMoney, value)) return;
            if (value) { _frozenMoney = _money; _moneyPinned = true; }
            else _moneyPinned = false;
        }
    }

    /// <summary>Tops ammo up every tick, so a firefight never runs the pack dry.</summary>
    public bool FreezeAmmo
    {
        get => _freezeAmmo;
        set => SetField(ref _freezeAmmo, value);
    }

    // --- editable fields --------------------------------------------------------
    public int Strength { get => _strength; set => SetEditable(ref _strength, value); }
    public int IQ { get => _iq; set => SetEditable(ref _iq, value); }
    public int Luck { get => _luck; set => SetEditable(ref _luck, value); }
    public int Speed { get => _speed; set => SetEditable(ref _speed, value); }
    public int Agility { get => _agility; set => SetEditable(ref _agility, value); }
    public int Dextermity { get => _dextermity; set => SetEditable(ref _dextermity, value); }
    public int Charisma { get => _charisma; set => SetEditable(ref _charisma, value); }

    public int Money { get => _money; set => SetEditable(ref _money, value); }
    public int MaxCon { get => _maxCon; set => SetEditable(ref _maxCon, value); }
    public int CurCon { get => _curCon; set => SetEditable(ref _curCon, value); }
    public int UncCon { get => _uncCon; set => SetEditable(ref _uncCon, value); }
    public int Experience { get => _experience; set => SetEditable(ref _experience, value); }
    public int Level { get => _level; set => SetEditable(ref _level, value); }
    public int SkillPoints { get => _skillPoints; set => SetEditable(ref _skillPoints, value); }
    public int AC { get => _ac; set => SetEditable(ref _ac, value); }
    public int Weapon { get => _weapon; set => SetEditable(ref _weapon, value); }
    public int Armor { get => _armor; set => SetEditable(ref _armor, value); }
    public int Disease { get => _disease; set => SetEditable(ref _disease, value); }
    /// <summary>
    /// Sex and nationality are bound to <c>ComboBox.SelectedIndex</c>, which coerces an
    /// out-of-range index to -1 and pushes that back through the two-way binding. Refusing
    /// negatives stops a character whose stored byte falls outside the known table from being
    /// marked edited just by being selected — and then silently rewritten to 0 by the next Write.
    /// </summary>
    public int Sex
    {
        get => _sex;
        set { if (value >= 0) SetEditable(ref _sex, value); }
    }

    public int Nationality
    {
        get => _nationality;
        set { if (value >= 0) SetEditable(ref _nationality, value); }
    }

    public static IReadOnlyList<string> GenderChoices => CharacterFormat.Genders;
    public static IReadOnlyList<string> NationalityChoices => CharacterFormat.Nationalities;

    // --- skills & items ---------------------------------------------------------
    public ObservableCollection<SkillRowViewModel> Skills { get; } = new();
    public ObservableCollection<ItemRowViewModel> Items { get; } = new();

    public static IReadOnlyList<ItemInfo> ItemCatalog => ItemBook.Items;

    /// <summary>Level used by "Learn all skills".</summary>
    public int LearnLevel
    {
        get => _learnLevel;
        set => SetClamped(ref _learnLevel, value, 1, GameFacts.MaxSkillLevel);
    }

    /// <summary>
    /// Stores a clamped value and always notifies when the input was out of range. Plain
    /// <c>SetField</c> stays silent when the clamp lands on the value already held, which leaves
    /// the text box showing the number that was rejected.
    /// </summary>
    private void SetClamped(ref int field, int value, int min, int max,
        [CallerMemberName] string? name = null)
    {
        int clamped = Math.Clamp(value, min, max);
        if (!SetField(ref field, clamped, name) && clamped != value) OnPropertyChanged(name);
    }

    public int AddItemId
    {
        get => _addItemId;
        set { if (SetField(ref _addItemId, value)) AddItemCommand.RaiseCanExecuteChanged(); }
    }

    public int AddItemAmmo
    {
        get => _addItemAmmo;
        set => SetClamped(ref _addItemAmmo, value, 0, CharacterFormat.InventoryCountMask);
    }

    public string SkillSlotsLabel => $"{Skills.Count}/{GameFacts.SkillSlots} skill slots used";
    public string ItemSlotsLabel => $"{Items.Count}/{GameFacts.ItemSlots} item slots used";

    // --- commands ---------------------------------------------------------------
    public RelayCommand FullHealCommand { get; }
    public RelayCommand MaxAttributesCommand { get; }
    public RelayCommand MaxMoneyCommand { get; }
    public RelayCommand MaxSkillsCommand { get; }
    public RelayCommand LearnAllSkillsCommand { get; }
    public RelayCommand MaxAmmoCommand { get; }
    public RelayCommand ClearJamsCommand { get; }
    public RelayCommand MaxEverythingCommand { get; }
    public RelayCommand WriteCommand { get; }
    public RelayCommand RevertCommand { get; }
    public RelayCommand RefreshCommand { get; }
    public RelayCommand AddItemCommand { get; }

    /// <summary>
    /// Sets an editable field and marks it as needing a write. Marking happens only for real
    /// user edits — <see cref="Refresh"/> sets the same fields with <c>_loading</c> raised.
    /// </summary>
    private void SetEditable(ref int field, int value, [CallerMemberName] string? name = null)
    {
        if (!SetField(ref field, value, name)) return;
        if (_loading || name == null) return;

        _dirty.Add(name);
        RaisePendingChanged();
    }

    private void RaisePendingChanged()
    {
        OnPropertyChanged(nameof(HasPendingEdits));
        OnPropertyChanged(nameof(PendingLabel));
        WriteCommand.RaiseCanExecuteChanged();
        RevertCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Re-reads everything from the live record, including the skill and inventory lists.
    /// Fields the user has edited are left alone.
    /// </summary>
    public void Refresh()
    {
        RefreshScalars();
        _loading = true;
        try { RebuildLists(); }
        finally { _loading = false; }
    }

    /// <summary>
    /// Re-reads the simple fields only, leaving the skill and inventory collections alone.
    ///
    /// <para>This is what the poll timer calls. Rebuilding the item rows several times a second
    /// would reset any drop-down the user has open and churn the UI for no benefit — the lists
    /// only change when something adds or removes an entry, and those paths refresh in full.</para>
    /// </summary>
    public void RefreshScalars()
    {
        _loading = true;
        try
        {
            Load(ref _strength, nameof(Strength), _record.GetAttribute(0));
            Load(ref _iq, nameof(IQ), _record.GetAttribute(1));
            Load(ref _luck, nameof(Luck), _record.GetAttribute(2));
            Load(ref _speed, nameof(Speed), _record.GetAttribute(3));
            Load(ref _agility, nameof(Agility), _record.GetAttribute(4));
            Load(ref _dextermity, nameof(Dextermity), _record.GetAttribute(5));
            Load(ref _charisma, nameof(Charisma), _record.GetAttribute(6));

            Load(ref _money, nameof(Money), _record.Money);
            Load(ref _maxCon, nameof(MaxCon), _record.MaxCon);
            Load(ref _curCon, nameof(CurCon), _record.CurCon);
            Load(ref _uncCon, nameof(UncCon), _record.UncCon);
            Load(ref _experience, nameof(Experience), _record.Experience);
            Load(ref _level, nameof(Level), _record.Level);
            Load(ref _skillPoints, nameof(SkillPoints), _record.SkillPoints);
            Load(ref _ac, nameof(AC), _record.AC);
            Load(ref _weapon, nameof(Weapon), _record.Weapon);
            Load(ref _armor, nameof(Armor), _record.Armor);
            Load(ref _disease, nameof(Disease), _record.Disease);
            // Clamp into the drop-downs' range so the Selector never coerces and writes back.
            Load(ref _sex, nameof(Sex),
                Math.Clamp(_record.Sex, 0, CharacterFormat.Genders.Length - 1));
            Load(ref _nationality, nameof(Nationality),
                Math.Clamp(_record.Nationality, 0, CharacterFormat.Nationalities.Length - 1));

            _name = _record.Name;
            _cName = _record.CName;
            _rank = _record.Rank;
            _isNpc = _record.IsNPC;
        }
        finally
        {
            _loading = false;
        }

        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ListLabel));
        OnPropertyChanged(nameof(Rank));
        OnPropertyChanged(nameof(NpcLabel));
        OnPropertyChanged(nameof(SlotLabel));
        OnPropertyChanged(nameof(AddressText));
    }

    private void Load(ref int field, string name, int value)
    {
        if (_dirty.Contains(name)) return;    // an unwritten edit wins over the live value
        SetField(ref field, value, name);
    }

    private void RebuildLists()
    {
        Skills.Clear();
        foreach (var s in _record.ReadSkills()) Skills.Add(new SkillRowViewModel(_record, _host, s));

        Items.Clear();
        foreach (var i in _record.ReadItems()) Items.Add(new ItemRowViewModel(_record, _host, i));

        OnPropertyChanged(nameof(SkillSlotsLabel));
        OnPropertyChanged(nameof(ItemSlotsLabel));
    }

    /// <summary>
    /// Re-applies freezes. Runs on the poll thread, so it only reads and writes memory — it
    /// touches no collection and raises no change notification.
    /// </summary>
    public void ApplyFreezes()
    {
        if (_freezeCon)
        {
            // Pin to the game's own maximum, read live. Using the edit box's value would let a
            // half-typed "5" on the way to "500" drop the character to 5 CON.
            if (_record.TryGetMaxCon(out int max) && max > 0) _record.CurCon = max;
        }

        if (_freezeMoney && _moneyPinned)
        {
            if (_record.TryGetMoney(out int now) && now != _frozenMoney) _record.Money = _frozenMoney;
        }

        if (_freezeAmmo) _record.MaxAmmo();
    }

    /// <summary>
    /// Writes the fields the user has edited — and only those. Untouched fields are never
    /// written, so the game's own progress is left exactly as it is.
    /// </summary>
    public void Write()
    {
        if (_dirty.Count == 0)
        {
            _host.OnMessage($"{Name}: nothing to write.");
            return;
        }

        int attempted = _dirty.Count;
        int written = 0;
        var unhandled = new List<string>();

        // A field that fails to write stays pending. Clearing it would throw the edit away, and
        // the Refresh below would put the game's old value back with nothing to show it happened.
        foreach (var name in _dirty.ToList())
        {
            if (WriteField(name)) { _dirty.Remove(name); written++; }
            else if (!EditableFieldNames.Contains(name)) { _dirty.Remove(name); unhandled.Add(name); }
        }

        RaisePendingChanged();

        // If money was among the edits and it is frozen, re-pin to the new value.
        if (_freezeMoney) { _frozenMoney = _money; _moneyPinned = true; }

        Refresh();

        if (unhandled.Count > 0)
        {
            // A field was made editable without a case in WriteField. Say so rather than
            // reporting a quiet partial success.
            _host.OnMessage($"{Name}: {written} field(s) written; no write path for " +
                            $"{string.Join(", ", unhandled)} — this is a bug.");
            return;
        }

        _host.OnMessage(written == attempted
            ? $"{Name}: {written} field(s) written."
            : $"{Name}: {written} of {attempted} field(s) written — the rest were not writable " +
              "and are still pending.");
    }

    /// <summary>
    /// Commits one field, reporting whether the write actually reached the process. Every case
    /// uses a <c>TrySet*</c> form: a plain property setter discards the result, which would let a
    /// failed write be reported as success and the user's edit thrown away.
    /// </summary>
    private bool WriteField(string name) => name switch
    {
        nameof(Strength) => _record.SetAttribute(0, _strength),
        nameof(IQ) => _record.SetAttribute(1, _iq),
        nameof(Luck) => _record.SetAttribute(2, _luck),
        nameof(Speed) => _record.SetAttribute(3, _speed),
        nameof(Agility) => _record.SetAttribute(4, _agility),
        nameof(Dextermity) => _record.SetAttribute(5, _dextermity),
        nameof(Charisma) => _record.SetAttribute(6, _charisma),
        nameof(Money) => _record.TrySetMoney(_money),
        nameof(MaxCon) => _record.TrySetMaxCon(_maxCon),
        nameof(CurCon) => _record.TrySetCurCon(_curCon),
        nameof(UncCon) => _record.TrySetUncCon(_uncCon),
        nameof(Experience) => _record.TrySetExperience(_experience),
        nameof(Level) => _record.TrySetLevel(_level),
        nameof(SkillPoints) => _record.TrySetSkillPoints(_skillPoints),
        nameof(AC) => _record.TrySetAC(_ac),
        nameof(Weapon) => _record.TrySetWeapon(_weapon),
        nameof(Armor) => _record.TrySetArmor(_armor),
        nameof(Disease) => _record.TrySetDisease(_disease),
        nameof(Sex) => _record.TrySetSex(_sex),
        nameof(Nationality) => _record.TrySetNationality(_nationality),
        _ => false,
    };

    /// <summary>Throws away unwritten edits and shows the game's current values again.</summary>
    public void Revert()
    {
        _dirty.Clear();
        RaisePendingChanged();
        Refresh();
        _host.OnMessage($"{Name}: unwritten edits discarded.");
    }

    private void FullHeal()
    {
        if (_record.FullHeal())
        {
            // The heal supersedes any half-typed CON edit; leaving it pending would let a later
            // Write undo the heal.
            DiscardEdits(nameof(CurCon));
            Refresh();
            _host.OnMessage($"{Name}: full heal.");
        }
        else
        {
            _host.OnMessage($"{Name}: already at full CON.");
        }
    }

    private void MaxAttributes()
    {
        _record.MaxAttributes();
        DiscardEdits(nameof(Strength), nameof(IQ), nameof(Luck), nameof(Speed),
                     nameof(Agility), nameof(Dextermity), nameof(Charisma));
        Refresh();
        _host.OnMessage($"{Name}: attributes maxed.");
    }

    private void MaxMoney()
    {
        _record.MaxMoney();
        DiscardEdits(nameof(Money));
        Refresh();
        if (_freezeMoney) { _frozenMoney = _money; _moneyPinned = true; }
        _host.OnMessage($"{Name}: money maxed.");
    }

    private void MaxSkills()
    {
        int changed = _record.MaxSkills();
        Refresh();
        _host.OnMessage(changed > 0
            ? $"{Name}: {changed} skill(s) raised to level {GameFacts.MaxSkillLevel}."
            : $"{Name}: every skill was already at level {GameFacts.MaxSkillLevel}.");
    }

    private void LearnAllSkills()
    {
        var result = _record.LearnAllSkills(_learnLevel);
        Refresh();

        if (result.Learned == 0 && result.Complete)
        {
            _host.OnMessage($"{Name}: already knows every skill.");
            return;
        }

        string message = $"{Name}: learned {result.Learned} skill(s) at level {_learnLevel}.";
        if (!result.Complete)
        {
            message += $" {result.NotLearned.Count} did not fit in the " +
                       $"{GameFacts.SkillSlots} slots: {string.Join(", ", result.NotLearned)}.";
        }
        _host.OnMessage(message);
    }

    private void MaxAmmoAction()
    {
        int changed = _record.MaxAmmo();
        Refresh();
        _host.OnMessage(changed > 0
            ? $"{Name}: {changed} item(s) topped up to {GameFacts.MaxAmmo}."
            : $"{Name}: nothing to reload.");
    }

    private void ClearJams()
    {
        int changed = _record.ClearJams();
        Refresh();
        _host.OnMessage(changed > 0 ? $"{Name}: {changed} jam(s) cleared." : $"{Name}: nothing was jammed.");
    }

    private void AddItem()
    {
        if (_record.AddItem(_addItemId, _addItemAmmo))
        {
            Refresh();
            _host.OnMessage($"{Name}: added {ItemBook.ItemName(_addItemId)}.");
        }
        else
        {
            _host.OnMessage($"{Name}: no free inventory slot for {ItemBook.ItemName(_addItemId)}.");
        }
    }

    private void MaxEverything()
    {
        _record.MaxEverything();
        _dirty.Clear();
        RaisePendingChanged();
        Refresh();
        if (_freezeMoney) { _frozenMoney = _money; _moneyPinned = true; }
        _host.OnMessage($"{Name}: everything maxed.");
    }

    /// <summary>Drops pending edits that a quick action has just superseded.</summary>
    private void DiscardEdits(params string[] names)
    {
        foreach (var name in names) _dirty.Remove(name);
        RaisePendingChanged();
    }
}
