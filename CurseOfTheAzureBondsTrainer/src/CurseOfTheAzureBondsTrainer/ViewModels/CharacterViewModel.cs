using System.Collections.ObjectModel;
using System.Text;
using CurseOfTheAzureBondsTrainer.Game;
using CurseOfTheAzureBondsTrainer.Memory;
using CurseOfTheAzureBondsTrainer.Mvvm;

namespace CurseOfTheAzureBondsTrainer.ViewModels;

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
    public string[] RaceOptions => CoabFormat.Races;
    public string[] ClassOptions => CoabFormat.Classes;
    public string[] AlignmentOptions => CoabFormat.Alignments;
    public string[] GenderOptions => CoabFormat.Genders;
    public string[] StatusOptions => CoabFormat.Statuses;

    private bool _freezeHp;
    public bool FreezeHp { get => _freezeHp; set => SetProperty(ref _freezeHp, value); }

    private bool _freezeStatus;
    public bool FreezeStatus { get => _freezeStatus; set => SetProperty(ref _freezeStatus, value); }

    // Snapshot of the 84-byte memorized-spell block, captured when FreezeSpells is switched on and
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
                _spellSnapshot = new byte[CoabFormat.MemorizedSpellsLen];
                Array.Copy(Record.Bytes, CoabFormat.OffMemorizedSpells, _spellSnapshot, 0, CoabFormat.MemorizedSpellsLen);
            }
            else _spellSnapshot = null;
        }
    }

    public CharacterViewModel(ICharacterHost host, LocatedCharacter located)
    {
        _host = host;
        Address = located.Address;
        Record = located.Record;

        for (int i = 0; i < CoabFormat.StatCount; i++)
        {
            int idx = i;
            // Curse stores each score as a (current, maximum) pair, so both bytes are written and
            // both are poked — write only the current half and the next Restoration silently undoes
            // the edit; write only the maximum and nothing changes at all.
            Stats.Add(new StatViewModel(CoabFormat.Stats[i], CoabFormat.StatsShort[i],
                () => Record.GetStat(idx),
                v =>
                {
                    Record.SetStat(idx, v);
                    Poke(CoabFormat.OffStats + idx * CoabFormat.StatStride, CoabFormat.StatStride);
                    RaiseDerived();
                }));
        }

        for (int i = 0; i < CoabFormat.MoneyNames.Length; i++)
        {
            int idx = i;
            Coins.Add(new CoinViewModel(CoabFormat.MoneyNames[i],
                () => Record.GetMoney(idx),
                v => { Record.SetMoney(idx, v); Poke(CoabFormat.MoneyOffsets[idx], 2); }));
        }

        for (int i = 0; i < CoabFormat.ClassLevelCount; i++)
        {
            int idx = i;
            ClassLevels.Add(new ClassLevelViewModel(CoabFormat.ClassLevelNames[i],
                () => Record.GetClassLevel(idx),
                v => { Record.SetClassLevel(idx, v); Poke(CoabFormat.OffClassLevels + idx, 1); RaiseDerived(); }));
        }

        BuildRawBytes();
        RebuildClassOptions();
    }

    private void BuildRawBytes()
    {
        RawBytes.Clear();
        for (int off = 0; off < CoabFormat.RecordSize; off++)
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
        set { Record.StrengthPercent = value; Poke(CoabFormat.OffStrPercent, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int RaceIndex
    {
        get => Record.Race;
        set
        {
            Record.Race = value; Poke(CoabFormat.OffRace, 1); OnPropertyChanged(); RaiseDerived();
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
            Record.Class = value; Poke(CoabFormat.OffClass, 1); OnPropertyChanged(); RaiseDerived();
            OnPropertyChanged(nameof(ClassChangePreview));
        }
    }
    public int AlignmentIndex
    {
        get => Record.Alignment;
        set { Record.Alignment = value; Poke(CoabFormat.OffAlignment, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int GenderIndex
    {
        get => Record.Gender;
        set { Record.Gender = value; Poke(CoabFormat.OffGender, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int StatusIndex
    {
        get => Record.Status;
        set { Record.Status = value; Poke(CoabFormat.OffStatus, 1); OnPropertyChanged(); RaiseDerived(); }
    }

    public int Age
    {
        get => Record.Age;
        set { Record.Age = value; Poke(CoabFormat.OffAge, 2); OnPropertyChanged(); }
    }

    // --- hit points / combat -------------------------------------------------
    public int HpCurrent
    {
        get => Record.HpCurrent;
        set { Record.HpCurrent = value; Poke(CoabFormat.OffHpCur, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int HpMax
    {
        get => Record.HpMax;
        set { Record.HpMax = value; Poke(CoabFormat.OffHpMax, 1); OnPropertyChanged(); RaiseDerived(); }
    }
    public int ArmorClass
    {
        get => Record.ArmorClass;
        set
        {
            // Write both the effective and the base AC so an equipment recompute can't revert it.
            Record.ArmorClass = value; Poke(CoabFormat.OffAcCur, 1);
            Record.ArmorClassBase = value; Poke(CoabFormat.OffAcBase, 1);
            OnPropertyChanged(); RaiseDerived();
        }
    }
    public int Thac0
    {
        get => Record.Thac0;
        set
        {
            Record.Thac0 = value; Poke(CoabFormat.OffThac0Cur, 1);
            Record.Thac0Base = value; Poke(CoabFormat.OffThac0Base, 1);
            OnPropertyChanged(); RaiseDerived();
        }
    }
    public long Experience
    {
        get => Record.Experience;
        set { Record.Experience = value; Poke(CoabFormat.OffExperience, 4); OnPropertyChanged(); RaiseDerived(); }
    }

    // --- commands ------------------------------------------------------------
    public void MaxStats()
    {
        for (int i = 0; i < CoabFormat.StatCount; i++)
        {
            Record.SetStat(i, 18);
            Poke(CoabFormat.OffStats + i * CoabFormat.StatStride, CoabFormat.StatStride);
        }
        // Exceptional strength only benefits fighters; set it for anyone whose STR is 18.
        Record.StrengthPercent = 100; Poke(CoabFormat.OffStrPercent, CoabFormat.StatStride);
        RefreshEditors(); RaiseDerived();
    }

    /// <summary>
    /// Puts every drained ability score back to the maximum the record stores for it — what a
    /// Restoration does. Curse keeps both halves of every score, so a drain is visible in the record
    /// itself and undoing it needs no guesswork about what the score used to be.
    /// </summary>
    public void RestoreDrainedStats()
    {
        if (!Record.RestoreDrainedStats()) return;
        Poke(CoabFormat.OffStats, CoabFormat.StatCount * CoabFormat.StatStride);
        RefreshEditors(); RaiseDerived();
    }

    /// <summary>True while any ability score reads below its stored maximum.</summary>
    public bool IsDrained => Record.IsDrained;

    /// <summary>"STR 14/18, CON 15/17" for the drained scores, or an empty string.</summary>
    public string DrainSummary
    {
        get
        {
            var parts = new List<string>();
            for (int i = 0; i < CoabFormat.StatCount; i++)
            {
                int cur = Record.GetStat(i), max = Record.GetStatMax(i);
                if (cur < max) parts.Add($"{CoabFormat.StatsShort[i]} {cur}/{max}");
            }
            return parts.Count == 0 ? "" : "Drained: " + string.Join(", ", parts);
        }
    }

    public void FullHeal()
    {
        Record.HpCurrent = Record.HpMax; Poke(CoabFormat.OffHpCur, 1);
        Record.Status = 0; Poke(CoabFormat.OffStatus, 1);
        OnPropertyChanged(nameof(HpCurrent)); OnPropertyChanged(nameof(StatusIndex)); RaiseDerived();
    }

    public void MaxMoney()
    {
        // Fill the four counters worth caring about. Note: the game weighs every coin
        // (10 coins = 1 lb), so a maxed total will floor the character's movement in-game —
        // trim it if that matters. Copper/silver/electrum are left alone (low value, dead weight).
        Record.Gold = 0xFFFF; Poke(CoabFormat.OffGold, 2);
        Record.Platinum = 0xFFFF; Poke(CoabFormat.OffPlatinum, 2);
        Record.Gems = 0xFFFF; Poke(CoabFormat.OffGems, 2);
        Record.Jewelry = 0xFFFF; Poke(CoabFormat.OffJewelry, 2);
        RefreshEditors();
    }

    public void MaxEverything()
    {
        MaxStats();
        Record.HpMax = 255; Poke(CoabFormat.OffHpMax, 1);
        Record.HpCurrent = 255; Poke(CoabFormat.OffHpCur, 1);
        Record.ArmorClass = -10; Poke(CoabFormat.OffAcCur, 1);
        Record.ArmorClassBase = -10; Poke(CoabFormat.OffAcBase, 1);
        Record.Thac0 = 1; Poke(CoabFormat.OffThac0Cur, 1);
        Record.Thac0Base = 1; Poke(CoabFormat.OffThac0Base, 1);
        Record.Status = 0; Poke(CoabFormat.OffStatus, 1);
        MaxMoney();
        RefreshEditors(); RaiseDerived();
    }

    /// <summary>
    /// Give this character's combat icon a fresh random palette. Cosmetic only — it touches just
    /// the six icon-color bytes, so it's safe to use at any time (including in combat).
    /// </summary>
    public void RandomizeIconColors()
    {
        Record.RandomizeIconColors(Random.Shared);
        Poke(CoabFormat.OffIconColor, CoabFormat.IconColorLen);
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
        Record.HpCurrent = CharacterRecord.WeakenedHp; Poke(CoabFormat.OffHpCur, 1);
        Record.ArmorClass = CharacterRecord.WeakenedAc; Poke(CoabFormat.OffAcCur, 1);
        Record.ArmorClassBase = CharacterRecord.WeakenedAc; Poke(CoabFormat.OffAcBase, 1);
        Record.Thac0 = CharacterRecord.WeakenedThac0; Poke(CoabFormat.OffThac0Cur, 1);
        Record.Thac0Base = CharacterRecord.WeakenedThac0; Poke(CoabFormat.OffThac0Base, 1);
        OnPropertyChanged(nameof(HpCurrent)); OnPropertyChanged(nameof(ArmorClass));
        OnPropertyChanged(nameof(Thac0)); RaiseDerived();
    }

    /// <summary>
    /// Is this creature already out of the fight? True once it is dying, dead, petrified or off the
    /// battlefield (statuses 5, 6, 7, and 2/8) — the states an automatic sweep must leave alone,
    /// because writing a hit point back into a corpse is the one way these edits can put a creature
    /// the party already beat back on its feet.
    ///
    /// <para>Hit points settle it before status does, since the engine stamps the status a tick
    /// later than the blow: 0 is finished, and so is anything reading <i>above</i> max. That second
    /// test is not redundant — current HP is an unsigned byte, so a creature the engine has taken
    /// below zero reads back as 251 rather than -5, and <c>&lt;= 0</c> alone would wave it through
    /// as a healthy creature and stand it up on 1 HP.</para>
    ///
    /// <para>Status 4 (Unconscious) is deliberately <i>not</i> here. On a monster with hit points
    /// left it means slept or held, not beaten — it wakes up and goes on fighting, so an automatic
    /// pass has every reason to act on it. Unconsciousness that came from damage is already caught
    /// by the hit-point test above.</para>
    /// </summary>
    public bool IsOutOfTheFight =>
        Record.HpCurrent <= 0 || Record.HpCurrent > Record.HpMax ||
        Record.Status is 2 or 5 or 6 or 7 or 8;

    /// <summary>
    /// Does an automatic pass have anything to do to this creature? False for one already standing
    /// in the weakened state, so the auto sweep isn't re-writing five bytes per monster per tick,
    /// and false for one already out of the fight (see <see cref="IsOutOfTheFight"/>). Tested
    /// against <see cref="CharacterRecord.IsWeakened"/> rather than the looser mark the arena sweep
    /// goes by, so a creature healed off its last hit point is weakened again.
    /// </summary>
    public bool NeedsWeakening => !IsOutOfTheFight && !Record.IsWeakened;

    /// <summary>Does an automatic pass have anything to zero? False for a creature already out of
    /// the fight, so the sweep neither re-writes a dead record every tick nor rewrites a "gone"
    /// creature back into a body on the field.</summary>
    public bool NeedsKilling => !IsOutOfTheFight;

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
        Record.HpCurrent = 0; Poke(CoabFormat.OffHpCur, 1);
        Record.Status = 6; Poke(CoabFormat.OffStatus, 1);   // dead
        OnPropertyChanged(nameof(HpCurrent)); OnPropertyChanged(nameof(StatusIndex)); RaiseDerived();
    }

    // --- party generation -----------------------------------------------------
    /// <summary>
    /// Writes a <see cref="RolledCharacter"/> over this record, touching only
    /// <see cref="RolledCharacter.WrittenRanges"/>. The record keeps its money, its carried items,
    /// its equipped items, its effects, encumbrance and its place in the game's own linked lists,
    /// so the result is the same character sheet with a new person on it. After the write the record
    /// still occupies the same address in the live game, so the poll loop's
    /// <see cref="CharacterRecord.IsSameCreatureAs"/> check still recognises this address on the
    /// next tick and keeps refreshing it.
    /// </summary>
    public void ApplyGenerated(RolledCharacter rolled)
    {
        ArgumentNullException.ThrowIfNull(rolled);
        rolled.StampOnto(Record);
        foreach (var (offset, length) in RolledCharacter.WrittenRanges) Poke(offset, length);

        if (FreezeSpells)
        {
            _spellSnapshot = new byte[CoabFormat.MemorizedSpellsLen];
            Array.Copy(Record.Bytes, CoabFormat.OffMemorizedSpells, _spellSnapshot, 0, CoabFormat.MemorizedSpellsLen);
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
            ClassChangeOptions.Add(new ClassOption(cls, CoabFormat.ClassName(cls), ok));
        }

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

        if (FreezeSpells)
        {
            _spellSnapshot = new byte[CoabFormat.MemorizedSpellsLen];
            Array.Copy(Record.Bytes, CoabFormat.OffMemorizedSpells, _spellSnapshot, 0, CoabFormat.MemorizedSpellsLen);
        }

        RefreshAll();
        OnPropertyChanged(nameof(ClassChangePreview));

        string warnings = plan.Warnings.Count == 0 ? "" : " " + string.Join(" ", plan.Warnings);
        return $"{Record.Name} is now a {plan.ToName} ({plan.LevelText}). " +
               $"THAC0 {plan.Thac0}, saves {string.Join("/", plan.Saves)}. Hit points and experience unchanged." +
               warnings;
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
            Record.HpCurrent = Record.HpMax; Poke(CoabFormat.OffHpCur, 1);
            // With HP pinned, an already unconscious/dying character should also be roused.
            if (!FreezeStatus && Record.Status is 4 or 5) { Record.Status = 0; Poke(CoabFormat.OffStatus, 1); }
        }

        if (FreezeStatus && Record.Status != 0)
        {
            Record.Status = 0; Poke(CoabFormat.OffStatus, 1);   // 0 = Okay
        }

        if (FreezeSpells && _spellSnapshot != null)
        {
            // Re-stamp the memorized-spell slots so casting never spends them.
            Array.Copy(_spellSnapshot, 0, Record.Bytes, CoabFormat.OffMemorizedSpells, CoabFormat.MemorizedSpellsLen);
            Poke(CoabFormat.OffMemorizedSpells, CoabFormat.MemorizedSpellsLen);
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
        Array.Copy(fresh, 0, Record.Bytes, 0, CoabFormat.RecordSize);
        RaiseDerived();
        OnPropertyChanged(nameof(LiveHp));
    }

    /// <summary>
    /// Re-raises just the fields the Combat tab edits, so a monster's numbers follow the battle
    /// instead of showing whatever they were when the record was located. Only those five, so this
    /// stays cheap enough for the poll loop (unlike <see cref="RefreshEditors"/>, which also walks
    /// all 422 raw bytes). The caller skips it while those boxes have focus — see
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
    private void PokeName() => Poke(CoabFormat.OffNameLength, 1 + CoabFormat.NameMaxLength);

    private void RaiseDerived()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ListLabel));
        OnPropertyChanged(nameof(StrengthDisplay));
        OnPropertyChanged(nameof(IsDrained));
        OnPropertyChanged(nameof(DrainSummary));
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
    /// named <see cref="CoabFormat"/> constants so the labels can never drift from the record layout.
    /// </summary>
    private static string RawLabel(int o) => o switch
    {
        CoabFormat.OffNameLength => "name length",
        >= CoabFormat.OffName and <= CoabFormat.OffName + CoabFormat.NameMaxLength - 1 => "name",
        // Every ability score is a (current, maximum) pair — label both halves so the hex view makes
        // a drain obvious rather than showing two mystery bytes that happen to differ.
        CoabFormat.OffStr => "STR",
        CoabFormat.OffStr + 1 => "STR max",
        CoabFormat.OffInt => "INT",
        CoabFormat.OffInt + 1 => "INT max",
        CoabFormat.OffWis => "WIS",
        CoabFormat.OffWis + 1 => "WIS max",
        CoabFormat.OffDex => "DEX",
        CoabFormat.OffDex + 1 => "DEX max",
        CoabFormat.OffCon => "CON",
        CoabFormat.OffCon + 1 => "CON max",
        CoabFormat.OffCha => "CHA",
        CoabFormat.OffCha + 1 => "CHA max",
        CoabFormat.OffStrPercent => "STR %",
        CoabFormat.OffStrPercent + 1 => "STR % max",
        >= CoabFormat.OffMemorizedSpells and <= CoabFormat.OffMemorizedSpells + CoabFormat.MemorizedSpellsLen - 1 => "memorized spells",
        CoabFormat.OffThac0Base => "THAC0 base (60-x)",
        CoabFormat.OffRace => "race",
        CoabFormat.OffClass => "class",
        >= CoabFormat.OffAge and <= CoabFormat.OffAge + 1 => "age",
        CoabFormat.OffHpMax => "HP max",
        >= CoabFormat.OffKnownSpells and <= CoabFormat.OffKnownSpells + CoabFormat.KnownSpellsLen - 1 => "known spells",
        CoabFormat.OffAttackLevel => "attack level",
        CoabFormat.OffIconDimensions => "icon dimensions",
        >= CoabFormat.OffSaves and <= CoabFormat.OffSaves + CoabFormat.SavesLen - 1 => "saving throw",
        CoabFormat.OffMovementBase => "move base",
        CoabFormat.OffLevelHighest => "level (highest)",
        CoabFormat.OffDrainedLevels => "drained levels",
        CoabFormat.OffDrainedHp => "drained HP",
        CoabFormat.OffUndeadLevel => "undead level",
        >= CoabFormat.OffThiefSkills and <= CoabFormat.OffThiefSkills + CoabFormat.ThiefSkillsLen - 1 => "thief skill",
        >= CoabFormat.OffEffectsPtr and <= CoabFormat.OffEffectsPtr + 3 => "effects list ptr",
        CoabFormat.OffNpcFlag => "NPC flag",
        CoabFormat.OffModifiedFlag => "modified flag",
        >= CoabFormat.OffCopper and <= CoabFormat.OffCopper + 1 => "copper",
        >= CoabFormat.OffSilver and <= CoabFormat.OffSilver + 1 => "silver",
        >= CoabFormat.OffElectrum and <= CoabFormat.OffElectrum + 1 => "electrum",
        >= CoabFormat.OffGold and <= CoabFormat.OffGold + 1 => "gold",
        >= CoabFormat.OffPlatinum and <= CoabFormat.OffPlatinum + 1 => "platinum",
        >= CoabFormat.OffGems and <= CoabFormat.OffGems + 1 => "gems",
        >= CoabFormat.OffJewelry and <= CoabFormat.OffJewelry + 1 => "jewelry",
        >= CoabFormat.OffClassLevels and <= CoabFormat.OffClassLevels + CoabFormat.ClassLevelCount - 1 => "class level",
        CoabFormat.OffGender => "gender",
        CoabFormat.OffAlignment => "alignment",
        CoabFormat.OffAcBase => "AC base (60-x)",
        >= CoabFormat.OffExperience and <= CoabFormat.OffExperience + 3 => "experience",
        CoabFormat.OffHpRolled => "HP rolled",
        >= CoabFormat.OffClericSlots and <= CoabFormat.OffClericSlots + CoabFormat.SpellSlotLevels - 1 => "cleric spells/day",
        >= CoabFormat.OffMageSlots and <= CoabFormat.OffMageSlots + CoabFormat.SpellSlotLevels - 1 => "mage spells/day",
        >= CoabFormat.OffXpAward and <= CoabFormat.OffXpAward + 1 => "XP award (kill)",
        CoabFormat.OffOrderNumber => "marching order",
        CoabFormat.OffIconSize => "icon size",
        >= CoabFormat.OffIconColor and <= CoabFormat.OffIconColor + CoabFormat.IconColorLen - 1 => "icon color",
        CoabFormat.OffNumberOfItems => "item count",
        >= CoabFormat.OffItemsPtr and <= CoabFormat.OffItemsPtr + 3 => "items list ptr",
        >= CoabFormat.OffEquipWeapon and <= CoabFormat.OffEquipWeapon + 13 * 4 - 1 => "equipped item ptr",
        >= CoabFormat.OffEncumbrance and <= CoabFormat.OffEncumbrance + 1 => "encumbrance",
        >= CoabFormat.OffNextCharPtr and <= CoabFormat.OffNextCharPtr + 3 => "next character ptr",
        >= CoabFormat.OffCombatPtr and <= CoabFormat.OffCombatPtr + 3 => "combat struct ptr",
        CoabFormat.OffStatus => "status",
        CoabFormat.OffThac0Cur => "THAC0 cur",
        CoabFormat.OffAcCur => "AC cur",
        CoabFormat.OffHpCur => "HP current",
        CoabFormat.OffMovementCur => "move current",
        _ => ""
    };
}
