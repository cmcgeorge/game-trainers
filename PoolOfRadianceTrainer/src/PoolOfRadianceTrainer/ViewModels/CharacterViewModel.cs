using System.Collections.ObjectModel;
using System.Text;
using PoolOfRadianceTrainer.Game;
using PoolOfRadianceTrainer.Memory;
using PoolOfRadianceTrainer.Mvvm;

namespace PoolOfRadianceTrainer.ViewModels;

/// <summary>One class the change-class picker offers, and whether the character's race may take it.</summary>
public sealed record ClassOption(int Value, string Name, bool Legal)
{
    public string Display => Legal ? Name : Name + "   (not for this race)";
}

/// <summary>
/// Editable view over a single located character/monster record. Every setter mutates the
/// backing <see cref="Record"/> buffer and, when attached, writes just the changed field to
/// the game's live memory so edits take effect immediately.
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly ICharacterHost _host;

    public nuint Address { get; }
    public CharacterRecord Record { get; private set; }

    public ObservableCollection<StatViewModel> Stats { get; } = new();
    public ObservableCollection<CoinViewModel> Coins { get; } = new();
    public ObservableCollection<ClassLevelViewModel> ClassLevels { get; } = new();
    public ObservableCollection<HexByteViewModel> RawBytes { get; } = new();

    // Static option lists for the combo boxes.
    public string[] RaceOptions => PorFormat.Races;
    public string[] ClassOptions => PorFormat.Classes;
    public string[] AlignmentOptions => PorFormat.Alignments;
    public string[] GenderOptions => PorFormat.Genders;
    public string[] StatusOptions => PorFormat.Statuses;

    private bool _freezeHp;
    public bool FreezeHp { get => _freezeHp; set => SetProperty(ref _freezeHp, value); }

    private bool _freezeStatus;
    public bool FreezeStatus { get => _freezeStatus; set => SetProperty(ref _freezeStatus, value); }

    // Snapshot of the 21-byte memorized-spell block, captured when FreezeSpells is switched on and
    // re-written each poll tick so casting never depletes it. Capture it right after memorizing.
    private bool _freezeSpells;
    private byte[]? _spellSnapshot;
    public bool FreezeSpells
    {
        get => _freezeSpells;
        set
        {
            if (!SetProperty(ref _freezeSpells, value)) return;
            if (value)
            {
                _spellSnapshot = new byte[PorFormat.MemorizedSpellsLen];
                Array.Copy(Record.Bytes, PorFormat.OffMemorizedSpells, _spellSnapshot, 0, PorFormat.MemorizedSpellsLen);
            }
            else _spellSnapshot = null;
        }
    }

    public CharacterViewModel(ICharacterHost host, LocatedCharacter located)
    {
        _host = host;
        Address = located.Address;
        Record = located.Record;

        for (int i = 0; i < PorFormat.StatCount; i++)
        {
            int idx = i;
            Stats.Add(new StatViewModel(PorFormat.Stats[i], PorFormat.StatsShort[i],
                () => Record.GetStat(idx),
                v => { Record.SetStat(idx, v); Poke(PorFormat.OffStr + idx, 1); RaiseDerived(); }));
        }

        for (int i = 0; i < PorFormat.MoneyNames.Length; i++)
        {
            int idx = i;
            Coins.Add(new CoinViewModel(PorFormat.MoneyNames[i],
                () => Record.GetMoney(idx),
                v => { Record.SetMoney(idx, v); Poke(PorFormat.MoneyOffsets[idx], 2); }));
        }

        for (int i = 0; i < PorFormat.ClassLevelCount; i++)
        {
            int idx = i;
            ClassLevels.Add(new ClassLevelViewModel(PorFormat.ClassLevelNames[i],
                () => Record.GetClassLevel(idx),
                v => { Record.SetClassLevel(idx, v); Poke(PorFormat.OffClassLevels + idx, 1); RaiseDerived(); }));
        }

        BuildRawBytes();
        RebuildClassOptions();
    }

    private void BuildRawBytes()
    {
        RawBytes.Clear();
        for (int off = 0; off < PorFormat.RecordSize; off++)
        {
            int o = off;
            RawBytes.Add(new HexByteViewModel(o, RawLabel(o),
                p => Record.Bytes[p],
                (p, v) => { Record.Bytes[p] = (byte)v; Poke(p, 1); RefreshAll(); }));
        }
    }

    // --- identity / summary --------------------------------------------------
    public string Name
    {
        get => Record.Name;
        set { Record.Name = value; PokeName(); OnPropertyChanged(); RaiseDerived(); }
    }

    public string Title => $"{Record.Name}  —  {Record.GenderName} {Record.RaceName} {Record.ClassName}";
    public string Summary =>
        $"L{Record.EffectiveLevel} {Record.ClassName}   HP {Record.HpCurrent}/{Record.HpMax}   AC {Record.ArmorClass}   " +
        $"THAC0 {Record.Thac0}   XP {Record.Experience:N0}   [{Record.StatusName}]";
    public bool IsMonster => Record.LooksLikeMonster;
    public string ListLabel =>
        $"{Record.Name}  ({(IsMonster ? Record.ClassName : $"L{Record.EffectiveLevel} {Record.ClassName}")})";

    public int StrengthPercent
    {
        get => Record.StrengthPercent;
        set { Record.StrengthPercent = value; Poke(PorFormat.OffStrPercent, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int RaceIndex
    {
        get => Record.Race;
        set
        {
            Record.Race = value; Poke(PorFormat.OffRace, 1); OnPropertyChanged(); RaiseDerived();
            RebuildClassOptions();   // a different race allows a different set of classes
        }
    }
    /// <summary>The raw class byte. Writing it changes the label on the sheet and nothing else —
    /// see <see cref="ApplyClassChange"/> for the edit that also brings the derived numbers along.</summary>
    public int ClassIndex
    {
        get => Record.Class;
        set
        {
            Record.Class = value; Poke(PorFormat.OffClass, 1); OnPropertyChanged(); RaiseDerived();
            OnPropertyChanged(nameof(ClassChangePreview));
        }
    }
    public int AlignmentIndex
    {
        get => Record.Alignment;
        set { Record.Alignment = value; Poke(PorFormat.OffAlignment, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int GenderIndex
    {
        get => Record.Gender;
        set { Record.Gender = value; Poke(PorFormat.OffGender, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int StatusIndex
    {
        get => Record.Status;
        set { Record.Status = value; Poke(PorFormat.OffStatus, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int Age
    {
        get => Record.Age;
        set { Record.Age = value; Poke(PorFormat.OffAge, 2); OnPropertyChanged(); }
    }

    // --- hit points / combat -------------------------------------------------
    public int HpCurrent
    {
        get => Record.HpCurrent;
        set { Record.HpCurrent = value; Poke(PorFormat.OffHpCur, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int HpMax
    {
        get => Record.HpMax;
        set { Record.HpMax = value; Poke(PorFormat.OffHpMax, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int ArmorClass
    {
        get => Record.ArmorClass;
        set
        {
            // Write both the effective and the base AC so an equipment recompute can't revert it.
            Record.ArmorClass = value; Poke(PorFormat.OffAcCur, 1);
            Record.ArmorClassBase = value; Poke(PorFormat.OffAcBase, 1);
            OnPropertyChanged(); RaiseDerived();
        }
    }
    public int Thac0
    {
        get => Record.Thac0;
        set
        {
            Record.Thac0 = value; Poke(PorFormat.OffThac0Cur, 1);
            Record.Thac0Base = value; Poke(PorFormat.OffThac0Base, 1);
            OnPropertyChanged(); RaiseDerived();
        }
    }
    public long Experience
    {
        get => Record.Experience;
        set { Record.Experience = value; Poke(PorFormat.OffExperience, 4); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- commands ------------------------------------------------------------
    public void MaxStats()
    {
        for (int i = 0; i < PorFormat.StatCount; i++) { Record.SetStat(i, 18); Poke(PorFormat.OffStr + i, 1); }
        // Exceptional strength only benefits fighters; set it for anyone whose STR is 18.
        Record.StrengthPercent = 100; Poke(PorFormat.OffStrPercent, 1);
        RefreshEditors(); RaiseDerived();
    }

    public void FullHeal()
    {
        Record.HpCurrent = Record.HpMax; Poke(PorFormat.OffHpCur, 1);
        Record.Status = 0; Poke(PorFormat.OffStatus, 1);
        OnPropertyChanged(nameof(HpCurrent)); OnPropertyChanged(nameof(StatusIndex)); RaiseDerived();
    }

    public void MaxMoney()
    {
        // Fill the four counters worth caring about. Note: the game weighs every coin
        // (10 coins = 1 lb), so a maxed total will floor the character's movement in-game —
        // trim it if that matters. Copper/silver/electrum are left alone (low value, dead weight).
        Record.Gold = 0xFFFF; Poke(PorFormat.OffGold, 2);
        Record.Platinum = 0xFFFF; Poke(PorFormat.OffPlatinum, 2);
        Record.Gems = 0xFFFF; Poke(PorFormat.OffGems, 2);
        Record.Jewelry = 0xFFFF; Poke(PorFormat.OffJewelry, 2);
        RefreshEditors();
    }

    public void MaxEverything()
    {
        MaxStats();
        Record.HpMax = 255; Poke(PorFormat.OffHpMax, 1);
        Record.HpCurrent = 255; Poke(PorFormat.OffHpCur, 1);
        Record.ArmorClass = -10; Poke(PorFormat.OffAcCur, 1);
        Record.ArmorClassBase = -10; Poke(PorFormat.OffAcBase, 1);
        Record.Thac0 = 1; Poke(PorFormat.OffThac0Cur, 1);
        Record.Thac0Base = 1; Poke(PorFormat.OffThac0Base, 1);
        Record.Status = 0; Poke(PorFormat.OffStatus, 1);
        MaxMoney();
        RefreshEditors(); RaiseDerived();
    }

    /// <summary>
    /// Replaces this party member with a freshly generated character (see
    /// <see cref="PartyGenerator"/>), writing only the record ranges the generator owns — the
    /// character's money, carried items and place in the game's linked lists are left exactly as
    /// they were, so the new person inherits the old one's possessions rather than the game losing
    /// track of them.
    ///
    /// <para>The identity fields (name, race, class, gender) change together with the bytes in
    /// memory, so the poll loop's <see cref="CharacterRecord.IsSameCreatureAs"/> check still
    /// recognises this address on the next tick and keeps refreshing it.</para>
    /// </summary>
    public void ApplyGenerated(RolledCharacter rolled)
    {
        ArgumentNullException.ThrowIfNull(rolled);
        rolled.StampOnto(Record);
        foreach (var (offset, length) in RolledCharacter.WrittenRanges) Poke(offset, length);

        // A spell freeze holds a snapshot of the memorized-spell block taken from the character who
        // used to be here; re-take it from the new record so the freeze can't restore their spells.
        if (FreezeSpells)
        {
            _spellSnapshot = new byte[PorFormat.MemorizedSpellsLen];
            Array.Copy(Record.Bytes, PorFormat.OffMemorizedSpells, _spellSnapshot, 0, PorFormat.MemorizedSpellsLen);
        }
        RefreshAll();
    }

    // --- class change ---------------------------------------------------------
    /// <summary>The classes offered by the change-class picker: what this race may take, or every
    /// playable class when <see cref="AllowIllegalClasses"/> is on.</summary>
    public ObservableCollection<ClassOption> ClassChangeOptions { get; } = new();

    private bool _allowIllegalClasses;
    /// <summary>Offer class/race combinations the game itself would refuse (a dwarven magic-user).
    /// The record holds whatever is written, but the game's own screens may disagree with it.</summary>
    public bool AllowIllegalClasses
    {
        get => _allowIllegalClasses;
        set { if (SetProperty(ref _allowIllegalClasses, value)) RebuildClassOptions(); }
    }

    private int _classChangeTarget = -1;
    /// <summary>The class the picker is pointed at (a class byte).</summary>
    public int ClassChangeTarget
    {
        get => _classChangeTarget;
        set { if (SetProperty(ref _classChangeTarget, value)) OnPropertyChanged(nameof(ClassChangePreview)); }
    }

    private void RebuildClassOptions()
    {
        int previous = _classChangeTarget;
        var legal = ClassTables.LegalClasses(Record.Race);

        ClassChangeOptions.Clear();
        foreach (int cls in ClassTables.PlayableClasses)
        {
            bool ok = legal.Contains(cls);
            if (!ok && !_allowIllegalClasses) continue;
            ClassChangeOptions.Add(new ClassOption(cls, PorFormat.ClassName(cls), ok));
        }

        // Keep the picker where the user left it if that class is still offered; otherwise fall back
        // to the character's current class, and only then to whatever is first in the list.
        bool Offered(int cls) => ClassChangeOptions.Any(o => o.Value == cls);
        ClassChangeTarget = Offered(previous) ? previous
            : Offered(Record.Class) ? Record.Class
            : ClassChangeOptions.FirstOrDefault()?.Value ?? -1;

        OnPropertyChanged(nameof(ClassChangePreview));
    }

    /// <summary>What the picked class change would do, as the panel and the confirm dialog show it:
    /// the new levels and derived numbers, then any warnings, then the consequences worth knowing.</summary>
    public string ClassChangePreview
    {
        get
        {
            if (!ClassTables.IsPlayableClass(_classChangeTarget)) return "Pick a class.";
            try
            {
                var plan = ClassChange.Plan(Record, _classChangeTarget);
                var sb = new StringBuilder(plan.Summary);
                foreach (var w in plan.Warnings) sb.Append("\n⚠  ").Append(w);
                foreach (var n in plan.Notes) sb.Append("\n·  ").Append(n);
                return sb.ToString();
            }
            catch (Exception ex) { return "Can't plan that change: " + ex.Message; }
        }
    }

    /// <summary>
    /// Applies the picked class change: writes the new class and every number that depends on it —
    /// per-class levels, THAC0, saving throws, thief skills, spells known and spells per day — and
    /// leaves hit points, experience, abilities, money and items alone. Returns the status line.
    /// </summary>
    public string ApplyClassChange()
    {
        if (!ClassTables.IsPlayableClass(_classChangeTarget)) return "Pick a class first.";

        var plan = ClassChange.Plan(Record, _classChangeTarget);
        ClassChange.Apply(Record, plan);
        foreach (var (offset, length) in ClassChange.WrittenRanges) Poke(offset, length);

        // The freeze holds a snapshot of the old class's memorized spells; retake it from the record
        // the change just cleared, or the next tick would put them back.
        if (FreezeSpells)
        {
            _spellSnapshot = new byte[PorFormat.MemorizedSpellsLen];
            Array.Copy(Record.Bytes, PorFormat.OffMemorizedSpells, _spellSnapshot, 0, PorFormat.MemorizedSpellsLen);
        }

        RefreshAll();
        OnPropertyChanged(nameof(ClassChangePreview));

        string warnings = plan.Warnings.Count == 0 ? "" : " " + string.Join(" ", plan.Warnings);
        return $"{Record.Name} is now a {plan.ToName} ({plan.LevelText}). " +
               $"THAC0 {plan.Thac0}, saves {string.Join("/", plan.Saves)}. Hit points and experience unchanged." +
               warnings;
    }

    /// <summary>
    /// Give this character's combat icon a fresh random palette. Cosmetic only — it touches just
    /// the six icon-color bytes, so it's safe to use at any time (including in combat).
    /// </summary>
    public void RandomizeIconColors()
    {
        Record.RandomizeIconColors(Random.Shared);
        Poke(PorFormat.OffIconColor, PorFormat.IconColorLen);
        foreach (var b in RawBytes) b.Refresh();
    }

    /// <summary>
    /// Leaves this creature alive but on its last hit point, unable to hit back and impossible to
    /// miss (AC 20, THAC0 20) — so the party's very next blow kills it *through the game's own
    /// damage routine*.
    ///
    /// <para>This is the loot-safe way to win a fight. A death only counts to the engine when its
    /// damage routine processes it: that is what removes the creature from the battlefield, leaves
    /// the body, and banks what it was carrying for the post-battle treasure. Writing HP and status
    /// straight into the record (see <see cref="KillNow"/>) never runs that routine — the record is
    /// the character sheet, while the fight itself runs off a separate per-combatant block the
    /// engine rebuilds every round — so the creature keeps acting and the survivors' morale check
    /// ends the fight in a surrender, which pays no XP and no treasure.</para>
    /// </summary>
    public void WeakenNow()
    {
        Record.HpCurrent = 1; Poke(PorFormat.OffHpCur, 1);
        Record.ArmorClass = 20; Poke(PorFormat.OffAcCur, 1);
        Record.ArmorClassBase = 20; Poke(PorFormat.OffAcBase, 1);
        Record.Thac0 = 20; Poke(PorFormat.OffThac0Cur, 1);
        Record.Thac0Base = 20; Poke(PorFormat.OffThac0Base, 1);
        OnPropertyChanged(nameof(HpCurrent)); OnPropertyChanged(nameof(ArmorClass));
        OnPropertyChanged(nameof(Thac0)); RaiseDerived();
    }

    /// <summary>
    /// Zero this record's current HP and mark it dead — the combat panel uses it to drop a monster.
    /// Status 6 (dead) is deliberately *not* status 8 (gone): "dead" is the state a normal killing
    /// blow leaves behind — a body on the field whose carried items feed the post-battle treasure —
    /// while "gone" is the engine's remove-from-the-encounter state (fled off-map, disintegrated,
    /// undead destroyed by turning), which takes anything the creature was carrying with it.
    /// Monsters that carry gear (orc leaders' Chain Mail +1, Grishnak's brass key) would be looted
    /// of nothing. Pick "Gone" from the Status box by hand if you ever actually want that.
    ///
    /// <para>Even so this is <b>not</b> loot-safe, and cannot be made so from the record alone: the
    /// engine never processes the death, so the creature finishes the round, the fight tends to end
    /// in a surrender, and the encounter pays nothing. Use it to walk away from a hopeless fight;
    /// use <see cref="WeakenNow"/> when you want the treasure.</para>
    /// </summary>
    public void KillNow()
    {
        Record.HpCurrent = 0; Poke(PorFormat.OffHpCur, 1);
        Record.Status = 6; Poke(PorFormat.OffStatus, 1);   // dead
        OnPropertyChanged(nameof(HpCurrent)); OnPropertyChanged(nameof(StatusIndex)); RaiseDerived();
    }

    // --- freeze / live refresh ----------------------------------------------
    /// <summary>
    /// Called each poll tick. If HP is frozen, re-write it to max in the live game; if status
    /// is frozen, pin it back to "Okay" so the character can never be held, poisoned, knocked
    /// out, petrified or killed. Never touches monster records.
    /// </summary>
    public void ApplyFreeze()
    {
        if (!_host.IsAttached || IsMonster) return;

        if (FreezeHp)
        {
            Record.HpCurrent = Record.HpMax; Poke(PorFormat.OffHpCur, 1);
            // With HP pinned, an already unconscious/dying character should also be roused.
            if (!FreezeStatus && Record.Status is 4 or 5) { Record.Status = 0; Poke(PorFormat.OffStatus, 1); }
        }

        if (FreezeStatus && Record.Status != 0)
        {
            Record.Status = 0; Poke(PorFormat.OffStatus, 1);   // 0 = Okay
        }

        if (FreezeSpells && _spellSnapshot != null)
        {
            // Re-stamp the memorized-spell slots so casting never spends them.
            Array.Copy(_spellSnapshot, 0, Record.Bytes, PorFormat.OffMemorizedSpells, PorFormat.MemorizedSpellsLen);
            Poke(PorFormat.OffMemorizedSpells, PorFormat.MemorizedSpellsLen);
        }
    }

    /// <summary>Live HP string for the combat/party summary, e.g. "7/11".</summary>
    public string LiveHp => $"{Record.HpCurrent}/{Record.HpMax}";

    /// <summary>The displayed "18/xx" exceptional strength (or a plain score).</summary>
    public string StrengthDisplay => Record.StrengthDisplay;

    /// <summary>
    /// Lightweight poll-tick refresh: copy the latest game bytes into the record and raise
    /// only the read-only summary/HP display properties. Deliberately does NOT re-raise the
    /// editor fields, so it never clobbers a value the user is typing into a text box.
    /// <paramref name="fresh"/> is a reusable scratch buffer (length >= record size).
    /// </summary>
    public void RefreshLiveSummary(byte[] fresh)
    {
        Array.Copy(fresh, 0, Record.Bytes, 0, PorFormat.RecordSize);
        RaiseDerived();
        OnPropertyChanged(nameof(LiveHp));
    }

    /// <summary>
    /// Re-raises just the fields the Combat tab edits, so a monster's numbers follow the battle
    /// instead of showing whatever they were when the record was located. Only those five, so this
    /// stays cheap enough for the poll loop (unlike <see cref="RefreshEditors"/>, which also walks
    /// all 285 raw bytes). The caller skips it while those boxes have focus — see
    /// <c>MainViewModel.EnemyEditorFocused</c>.
    /// </summary>
    public void RefreshCombatEditors()
    {
        OnPropertyChanged(nameof(HpCurrent));
        OnPropertyChanged(nameof(HpMax));
        OnPropertyChanged(nameof(ArmorClass));
        OnPropertyChanged(nameof(Thac0));
        OnPropertyChanged(nameof(StatusIndex));
    }

    // --- write plumbing ------------------------------------------------------
    private void Poke(int offset, int length)
    {
        if (_host.IsAttached) _host.WriteBytes(Address, Record.Bytes, offset, length);
    }
    private void PokeName() => Poke(PorFormat.OffNameLength, 1 + PorFormat.NameMaxLength);

    private void RaiseDerived()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ListLabel));
        OnPropertyChanged(nameof(StrengthDisplay));
    }

    private void RefreshEditors()
    {
        foreach (var s in Stats) s.Refresh();
        foreach (var c in Coins) c.Refresh();
        foreach (var l in ClassLevels) l.Refresh();
        foreach (var b in RawBytes) b.Refresh();
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(HpCurrent)); OnPropertyChanged(nameof(HpMax));
        OnPropertyChanged(nameof(ArmorClass)); OnPropertyChanged(nameof(Thac0));
        OnPropertyChanged(nameof(Experience)); OnPropertyChanged(nameof(Age));
        OnPropertyChanged(nameof(StrengthPercent));
        OnPropertyChanged(nameof(RaceIndex)); OnPropertyChanged(nameof(ClassIndex));
        OnPropertyChanged(nameof(AlignmentIndex)); OnPropertyChanged(nameof(GenderIndex));
        OnPropertyChanged(nameof(StatusIndex));
    }

    private void RefreshAll() { RefreshEditors(); RaiseDerived(); }

    /// <summary>
    /// Human label for a raw byte offset, so the hex view annotates known fields. Built from the
    /// named <see cref="PorFormat"/> constants so the labels can never drift from the record layout.
    /// </summary>
    private static string RawLabel(int o) => o switch
    {
        PorFormat.OffNameLength => "name length",
        >= PorFormat.OffName and <= PorFormat.OffName + PorFormat.NameMaxLength - 1 => "name",
        PorFormat.OffStr => "STR",
        PorFormat.OffInt => "INT",
        PorFormat.OffWis => "WIS",
        PorFormat.OffDex => "DEX",
        PorFormat.OffCon => "CON",
        PorFormat.OffCha => "CHA",
        PorFormat.OffStrPercent => "STR %",
        >= PorFormat.OffMemorizedSpells and <= PorFormat.OffMemorizedSpells + PorFormat.MemorizedSpellsLen - 1 => "memorized spells",
        PorFormat.OffThac0Base => "THAC0 base (60-x)",
        PorFormat.OffRace => "race",
        PorFormat.OffClass => "class",
        >= PorFormat.OffAge and <= PorFormat.OffAge + 1 => "age",
        PorFormat.OffHpMax => "HP max",
        >= PorFormat.OffKnownSpells and <= PorFormat.OffKnownSpells + PorFormat.KnownSpellsLen - 1 => "known spells",
        >= PorFormat.OffSaves and <= PorFormat.OffSaves + PorFormat.SavesLen - 1 => "saving throw",
        PorFormat.OffMovementBase => "move base",
        PorFormat.OffLevelHighest => "level (highest)",
        PorFormat.OffDrainedLevels => "drained levels",
        PorFormat.OffDrainedHp => "drained HP",
        >= PorFormat.OffThiefSkills and <= PorFormat.OffThiefSkills + PorFormat.ThiefSkillsLen - 1 => "thief skill",
        >= PorFormat.OffCopper and <= PorFormat.OffCopper + 1 => "copper",
        >= PorFormat.OffSilver and <= PorFormat.OffSilver + 1 => "silver",
        >= PorFormat.OffElectrum and <= PorFormat.OffElectrum + 1 => "electrum",
        >= PorFormat.OffGold and <= PorFormat.OffGold + 1 => "gold",
        >= PorFormat.OffPlatinum and <= PorFormat.OffPlatinum + 1 => "platinum",
        >= PorFormat.OffGems and <= PorFormat.OffGems + 1 => "gems",
        >= PorFormat.OffJewelry and <= PorFormat.OffJewelry + 1 => "jewelry",
        >= PorFormat.OffClassLevels and <= PorFormat.OffClassLevels + PorFormat.ClassLevelCount - 1 => "class level",
        PorFormat.OffGender => "gender",
        PorFormat.OffAlignment => "alignment",
        PorFormat.OffAcBase => "AC base (60-x)",
        >= PorFormat.OffExperience and <= PorFormat.OffExperience + 3 => "experience",
        PorFormat.OffHpRolled => "HP rolled",
        >= PorFormat.OffXpAward and <= PorFormat.OffXpAward + 1 => "XP award (kill)",
        PorFormat.OffOrderNumber => "marching order",
        PorFormat.OffIconSize => "icon size",
        >= PorFormat.OffIconColor and <= PorFormat.OffIconColor + PorFormat.IconColorLen - 1 => "icon color",
        PorFormat.OffNumberOfItems => "item count",
        PorFormat.OffStatus => "status",
        PorFormat.OffThac0Cur => "THAC0 cur",
        PorFormat.OffAcCur => "AC cur",
        PorFormat.OffHpCur => "HP current",
        _ => ""
    };
}
