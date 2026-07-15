using System.Windows.Input;

namespace QuestForGlory1Trainer.ViewModels;

/// <summary>
/// Editable view over the located QFG1 hero stat block. Every property setter writes the new
/// value directly to DOSBox guest RAM so changes take effect immediately. Call
/// <see cref="Refresh"/> from the poll loop to keep the display current.
///
/// Memory layout (16-bit words, little-endian, relative to <see cref="_base"/>):
///   Word  0: STR    Word  5: Weapon Use   Word 10: Throwing
///   Word  1: INT    Word  6: Parry        Word 11: Climbing
///   Word  2: AGI    Word  7: Dodge        Word 12: Magic
///   Word  3: VIT    Word  8: Stealth      Word 13: XP
///   Word  4: LCK    Word  9: Pick Locks   Word 14: HP internal (shown × 2)
///                                         Word 15: Stamina internal (STR×3+VIT×4 when full)
///                                         Word 16: Mana (1:1 with shown)
///
/// Gold and Silver are at confirmed offsets -142 and -141 words from _base (= −284/−282 bytes),
/// established by cross-referencing the Day-1 Midday Thief dump with the located stat block.
/// </summary>
public sealed class HeroViewModel : ObservableObject
{
    private readonly IScanHost _host;
    private readonly nuint _base;
    private readonly nuint _goldAddr;
    private readonly nuint _silverAddr;
    private readonly bool _hasGold;
    private readonly nuint _actorHpAddr;
    private readonly nuint _actorStamAddr;

    // backing fields
    private short _str, _intel, _agi, _vit, _lck;
    private short _weaponUse, _parry, _dodge, _stealth;
    private short _pickLocks, _throwing, _climbing, _magic;
    private short _xp, _hpRaw, _stamRaw, _mana;
    private short _gold, _silver;

    // freeze flags
    private bool _freezeHp;
    private bool _freezeStamina;
    private bool _freezeMana;

    /// <summary>When true, HP is written to its formula maximum on every poll tick.</summary>
    public bool FreezeHp      { get => _freezeHp;      set => SetField(ref _freezeHp, value); }

    /// <summary>When true, Stamina is written to its formula maximum (STR×3+VIT×4) on every poll tick.</summary>
    public bool FreezeStamina { get => _freezeStamina; set => SetField(ref _freezeStamina, value); }

    /// <summary>When true, Mana is written to 200 on every poll tick.</summary>
    public bool FreezeMana    { get => _freezeMana;    set => SetField(ref _freezeMana, value); }

    // commands
    public ICommand MaxBaseStatsCommand { get; }
    public ICommand MaxSkillsCommand { get; }
    public ICommand FullHealCommand { get; }
    public ICommand MaxAllCommand { get; }

    public HeroViewModel(IScanHost host, LocatedStats located, nuint actorHpAddr = default)
    {
        _host = host;
        _base = located.StrAddress;
        _actorHpAddr   = actorHpAddr;
        _actorStamAddr = actorHpAddr != nuint.Zero ? actorHpAddr - 2 : nuint.Zero;

        // Gold / Silver: at -284 / -282 bytes from stat block (confirmed from dump analysis).
        _goldAddr   = _base - 284;
        _silverAddr = _base - 282;
        bool gOk = host.Read(_goldAddr,   ScanWidth.Int16, out long g) && g is >= 0 and < 10_000;
        bool sOk = host.Read(_silverAddr, ScanWidth.Int16, out long s) && s is >= 0 and < 10_000;
        _hasGold = gOk && sOk;
        if (_hasGold) { _gold = (short)g; _silver = (short)s; }

        var sv = located.SkillValues;
        _str = sv[0]; _intel = sv[1]; _agi = sv[2]; _vit = sv[3]; _lck = sv[4];
        _weaponUse = sv[5]; _parry = sv[6]; _dodge = sv[7]; _stealth = sv[8];
        _pickLocks = sv[9]; _throwing = sv[10]; _climbing = sv[11]; _magic = sv[12];
        _hpRaw   = located.HpRaw;
        _stamRaw = located.StaminaRaw;
        _mana    = located.ManaRaw;
        if (host.Read(_base + 26, ScanWidth.Int16, out long xp)) _xp = (short)xp;

        MaxBaseStatsCommand = new RelayCommand(_ => MaxBaseStats());
        MaxSkillsCommand    = new RelayCommand(_ => MaxSkills());
        FullHealCommand     = new RelayCommand(_ => FullHeal());
        MaxAllCommand       = new RelayCommand(_ => { MaxBaseStats(); MaxSkills(); FullHeal(); });
    }

    // ---- base stats (words 0–4) --------------------------------------------------

    /// <summary>Strength (1–200). Affects melee damage and carry weight.</summary>
    public short Str
    {
        get => _str;
        set { value = C1(value); if (SetField(ref _str, value)) Wr(0, value); }
    }

    /// <summary>Intelligence (1–200). Affects spell learning and puzzle hints.</summary>
    public short Intel
    {
        get => _intel;
        set { value = C1(value); if (SetField(ref _intel, value)) Wr(1, value); }
    }

    /// <summary>Agility (1–200). Affects hit chance, dodge, and Pick Locks.</summary>
    public short Agi
    {
        get => _agi;
        set { value = C1(value); if (SetField(ref _agi, value)) Wr(2, value); }
    }

    /// <summary>Vitality (1–200). Affects maximum Health and Stamina pools.</summary>
    public short Vit
    {
        get => _vit;
        set { value = C1(value); if (SetField(ref _vit, value)) Wr(3, value); }
    }

    /// <summary>Luck (1–200). Affects random event outcomes and treasure quality.</summary>
    public short Lck
    {
        get => _lck;
        set { value = C1(value); if (SetField(ref _lck, value)) Wr(4, value); }
    }

    // ---- combat skills (words 5–8) -----------------------------------------------

    /// <summary>Weapon Use (0–200). Melee accuracy; improves with combat practice.</summary>
    public short WeaponUse
    {
        get => _weaponUse;
        set { value = C0(value); if (SetField(ref _weaponUse, value)) Wr(5, value); }
    }

    /// <summary>Parry (0–200). Chance to block incoming attacks.</summary>
    public short Parry
    {
        get => _parry;
        set { value = C0(value); if (SetField(ref _parry, value)) Wr(6, value); }
    }

    /// <summary>Dodge (0–200). Chance to avoid missile and area attacks.</summary>
    public short Dodge
    {
        get => _dodge;
        set { value = C0(value); if (SetField(ref _dodge, value)) Wr(7, value); }
    }

    /// <summary>Stealth (0–200). Moving silently past creatures and NPCs.</summary>
    public short Stealth
    {
        get => _stealth;
        set { value = C0(value); if (SetField(ref _stealth, value)) Wr(8, value); }
    }

    // ---- other skills (words 9–12) -----------------------------------------------

    /// <summary>Pick Locks (0–200). Opens locked doors and chests. Thief emphasis.</summary>
    public short PickLocks
    {
        get => _pickLocks;
        set { value = C0(value); if (SetField(ref _pickLocks, value)) Wr(9, value); }
    }

    /// <summary>Throwing (0–200). Accuracy and range with thrown weapons.</summary>
    public short Throwing
    {
        get => _throwing;
        set { value = C0(value); if (SetField(ref _throwing, value)) Wr(10, value); }
    }

    /// <summary>Climbing (0–200). Scaling walls, trees, and cliff faces.</summary>
    public short Climbing
    {
        get => _climbing;
        set { value = C0(value); if (SetField(ref _climbing, value)) Wr(11, value); }
    }

    /// <summary>Magic (0–200). Mana pool size and spell accuracy. Magic-user emphasis.</summary>
    public short Magic
    {
        get => _magic;
        set { value = C0(value); if (SetField(ref _magic, value)) Wr(12, value); }
    }

    // ---- word 13: XP (editable) --------------------------------------------------

    /// <summary>Accumulated experience points. No upper cap in the engine.</summary>
    public short Xp
    {
        get => _xp;
        set { value = C0(value); if (SetField(ref _xp, value)) Wr(13, value); }
    }

    // ---- words 14–16: resources --------------------------------------------------

    /// <summary>
    /// Current Health as the in-game displayed value (internal raw / 2).
    /// Setting this writes raw = value × 2 to memory.
    /// </summary>
    public short Hp
    {
        get => (short)(_hpRaw / 2);
        set
        {
            short raw = (short)(Math.Clamp((int)value, 1, 200) * 2);
            if (_hpRaw == raw) return;
            _hpRaw = raw;
            _host.Write(_base + 28, raw, ScanWidth.Int16);
            OnPropertyChanged();
            OnPropertyChanged(nameof(HpNote));
        }
    }

    /// <summary>Tooltip annotation showing the internal raw value and computed maximum.</summary>
    public string HpNote => $"raw {_hpRaw}; max = {(_str + _vit + 1) / 2}  (edit to set displayed HP)";

    /// <summary>
    /// Current Stamina as the in-game displayed value (internal raw ÷ 4, rounded).
    /// Setting this writes raw = value × 4 to memory.
    /// Maximum stamina = STR × 3 + VIT × 4.
    /// </summary>
    public short Stamina
    {
        get => (short)Math.Round(_stamRaw / 4.0);
        set
        {
            short raw = (short)(Math.Clamp((int)value, 1, 2000) * 4);
            if (_stamRaw == raw) return;
            _stamRaw = raw;
            _host.Write(_base + 30, raw, ScanWidth.Int16);
            OnPropertyChanged();
            OnPropertyChanged(nameof(StaminaNote));
        }
    }

    /// <summary>Tooltip annotation for Stamina showing raw value and formula-derived max.</summary>
    public string StaminaNote => $"raw {_stamRaw}; max = STR×3+VIT×4 = {_str * 3 + _vit * 4}  (edit to set displayed Stamina)";

    /// <summary>Current Mana (1:1 with the displayed value; 0 for non-Magic-users).</summary>
    public short Mana
    {
        get => _mana;
        set { value = C0(value); if (SetField(ref _mana, value)) Wr(16, value); }
    }

    // ---- gold / silver -----------------------------------------------------------

    /// <summary>True when gold and silver addresses were validated at locate time.</summary>
    public bool HasGold => _hasGold;

    /// <summary>Gold coins (0–9999). Editable when <see cref="HasGold"/> is true.</summary>
    public short Gold
    {
        get => _gold;
        set
        {
            value = Math.Clamp(value, (short)0, (short)9999);
            if (!SetField(ref _gold, value) || !_hasGold) return;
            _host.Write(_goldAddr, value, ScanWidth.Int16);
        }
    }

    /// <summary>Silver coins (0–9999). Editable when <see cref="HasGold"/> is true.</summary>
    public short Silver
    {
        get => _silver;
        set
        {
            value = Math.Clamp(value, (short)0, (short)9999);
            if (!SetField(ref _silver, value) || !_hasGold) return;
            _host.Write(_silverAddr, value, ScanWidth.Int16);
        }
    }

    // ---- quick actions -----------------------------------------------------------

    private void MaxBaseStats()
    {
        Str = 100; Intel = 100; Agi = 100; Vit = 100; Lck = 100;
    }

    private void MaxSkills()
    {
        WeaponUse = 200; Parry = 200; Dodge = 200; Stealth = 200;
        PickLocks = 200; Throwing = 200; Climbing = 200; Magic = 200;
    }

    private void FullHeal()
    {
        Hp = 200;
        short maxStamRaw = (short)(_str * 3 + _vit * 4);
        _stamRaw = maxStamRaw;
        _host.Write(_base + 30, maxStamRaw, ScanWidth.Int16);
        OnPropertyChanged(nameof(Stamina));
        OnPropertyChanged(nameof(StaminaNote));
        Mana = 200;
    }

    // ---- poll refresh ------------------------------------------------------------

    /// <summary>Re-reads all stat values from live memory. Call from the poll timer tick.
    /// If a freeze flag is set, the corresponding resource is written to its maximum before reading.</summary>
    public void Refresh()
    {
        if (Rd(0,  out short v)) { _str       = v; OnPropertyChanged(nameof(Str)); }
        if (Rd(1,  out v))       { _intel     = v; OnPropertyChanged(nameof(Intel)); }
        if (Rd(2,  out v))       { _agi       = v; OnPropertyChanged(nameof(Agi)); }
        if (Rd(3,  out v))       { _vit       = v; OnPropertyChanged(nameof(Vit)); }
        if (Rd(4,  out v))       { _lck       = v; OnPropertyChanged(nameof(Lck)); }
        if (Rd(5,  out v))       { _weaponUse = v; OnPropertyChanged(nameof(WeaponUse)); }
        if (Rd(6,  out v))       { _parry     = v; OnPropertyChanged(nameof(Parry)); }
        if (Rd(7,  out v))       { _dodge     = v; OnPropertyChanged(nameof(Dodge)); }
        if (Rd(8,  out v))       { _stealth   = v; OnPropertyChanged(nameof(Stealth)); }
        if (Rd(9,  out v))       { _pickLocks = v; OnPropertyChanged(nameof(PickLocks)); }
        if (Rd(10, out v))       { _throwing  = v; OnPropertyChanged(nameof(Throwing)); }
        if (Rd(11, out v))       { _climbing  = v; OnPropertyChanged(nameof(Climbing)); }
        if (Rd(12, out v))       { _magic     = v; OnPropertyChanged(nameof(Magic)); }
        if (Rd(13, out v))       { _xp        = v; OnPropertyChanged(nameof(Xp)); }

        if (_freezeHp)
        {
            short maxHpRaw = (short)(_str + _vit + 1);
            _hpRaw = maxHpRaw;
            Wr(14, maxHpRaw);
            if (_actorHpAddr != nuint.Zero)
                _host.Write(_actorHpAddr, maxHpRaw, ScanWidth.Int16);
            OnPropertyChanged(nameof(Hp)); OnPropertyChanged(nameof(HpNote));
        }
        else if (Rd(14, out v)) { _hpRaw = v; OnPropertyChanged(nameof(Hp)); OnPropertyChanged(nameof(HpNote)); }

        if (_freezeStamina)
        {
            short maxStamRaw = (short)(_str * 3 + _vit * 4);
            _stamRaw = maxStamRaw;
            Wr(15, maxStamRaw);
            if (_actorStamAddr != nuint.Zero)
                _host.Write(_actorStamAddr, maxStamRaw, ScanWidth.Int16);
            OnPropertyChanged(nameof(Stamina)); OnPropertyChanged(nameof(StaminaNote));
        }
        else if (Rd(15, out v)) { _stamRaw = v; OnPropertyChanged(nameof(Stamina)); OnPropertyChanged(nameof(StaminaNote)); }

        if (_freezeMana)
        {
            _mana = (short)(_magic * 2);
            Wr(16, _mana);
            OnPropertyChanged(nameof(Mana));
        }
        else if (Rd(16, out v)) { _mana = v; OnPropertyChanged(nameof(Mana)); }

        if (_hasGold)
        {
            if (_host.Read(_goldAddr,   ScanWidth.Int16, out long g)) { _gold   = (short)g; OnPropertyChanged(nameof(Gold)); }
            if (_host.Read(_silverAddr, ScanWidth.Int16, out long s)) { _silver = (short)s; OnPropertyChanged(nameof(Silver)); }
        }
    }

    // ---- helpers -----------------------------------------------------------------

    private bool Rd(int word, out short result)
    {
        result = 0;
        if (!_host.Read(_base + (nuint)(word * 2), ScanWidth.Int16, out long v)) return false;
        result = (short)v;
        return true;
    }

    private void Wr(int word, short value)
        => _host.Write(_base + (nuint)(word * 2), value, ScanWidth.Int16);

    private static short C1(short v) => Math.Clamp(v, (short)1, (short)200);
    private static short C0(short v) => Math.Clamp(v, (short)0, (short)200);
}
