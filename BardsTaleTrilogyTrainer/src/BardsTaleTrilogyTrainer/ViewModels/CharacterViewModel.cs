using System.Collections.ObjectModel;
using BardsTaleTrilogyTrainer.Game;
using BardsTaleTrilogyTrainer.Memory;

namespace BardsTaleTrilogyTrainer.ViewModels;

/// <summary>
/// View model for a single party member. Wraps a <see cref="CharacterRecord"/> and exposes
/// editable properties with change notification, plus freeze toggles for HP and SP, spell-level
/// editing, class changing and the class-specific statistics panel.
///
/// <para>The fields mirror the remaster's real <c>Character</c> layout: one set of attributes
/// rather than the original's current/maximum pairs, 64-bit experience and gold, and spell
/// levels held in an array indexed by class id. There is no armour-class field to edit — the
/// game derives armour class from equipment when it draws the sheet.</para>
/// </summary>
public sealed class CharacterViewModel : ObservableObject
{
    private readonly CharacterRecord _record;
    private readonly ICharacterHost _host;

    private bool _freezeHp;
    private bool _freezeSp;
    private int _hpCur, _hpMax, _spCur, _spMax;
    private long _experience, _gold;
    private int _level, _race, _class, _gender, _condition;
    private int _strength, _intelligence, _dexterity, _constitution, _luck;
    private int[] _spellLevels = new int[CharacterFormat.SpellLevelSlots];
    private ClassScores _scores;
    private int _targetClass;
    private bool _ignoreClassRequirements;
    private string _classChangeStatus = "";

    public CharacterViewModel(CharacterRecord record, ICharacterHost host)
    {
        _record = record;
        _host = host;

        foreach (var (classId, name) in CharacterFormat.CasterClasses)
            SpellLevels.Add(new SpellLevelViewModel(classId, name, this));

        Refresh();
        _targetClass = _class;
        UpdateClassChangeStatus();
    }

    public int Slot => _record.Slot;
    public string Name => _record.Name;
    public string DisplayClass => ClassBook.ClassName(_class);
    public string DisplayRace => CharacterFormat.RaceName(_race);
    public bool IsOccupied => _record.IsOccupied;
    public string Header => $"#{Slot}: {Name} ({DisplayRace} {DisplayClass} L{Level})";

    // --- editable fields --------------------------------------------------------
    public int HpCur { get => _hpCur; set => SetField(ref _hpCur, value); }
    public int HpMax { get => _hpMax; set => SetField(ref _hpMax, value); }
    public int SpCur { get => _spCur; set => SetField(ref _spCur, value); }
    public int SpMax { get => _spMax; set => SetField(ref _spMax, value); }

    /// <summary>Experience is a 64-bit field in the remaster.</summary>
    public long Experience { get => _experience; set => SetField(ref _experience, value); }

    /// <summary>Gold carried by this character; the party purse is edited separately.</summary>
    public long Gold { get => _gold; set => SetField(ref _gold, value); }

    public int Level
    {
        get => _level;
        set { if (SetField(ref _level, value)) OnClassInputsChanged(); }
    }

    public int Race { get => _race; set => SetField(ref _race, value); }
    public int Gender { get => _gender; set => SetField(ref _gender, value); }

    /// <summary>0 = Okay, 3 = Dead … see <see cref="CharacterFormat.Conditions"/>.</summary>
    public int Condition { get => _condition; set => SetField(ref _condition, value); }

    public int Class
    {
        get => _class;
        set { if (SetField(ref _class, value)) OnClassInputsChanged(); }
    }

    public int Strength { get => _strength; set => SetField(ref _strength, value); }
    public int Intelligence { get => _intelligence; set => SetField(ref _intelligence, value); }
    public int Dexterity
    {
        get => _dexterity;
        set { if (SetField(ref _dexterity, value)) OnClassInputsChanged(); }
    }
    public int Constitution { get => _constitution; set => SetField(ref _constitution, value); }
    public int Luck { get => _luck; set => SetField(ref _luck, value); }

    /// <summary>One row per casting school, bound to <c>m_spellLevel</c> by class id.</summary>
    public ObservableCollection<SpellLevelViewModel> SpellLevels { get; } = new();

    // --- class ------------------------------------------------------------------
    /// <summary>Every playable class of the trilogy, for the class pickers.</summary>
    public IReadOnlyList<ClassInfo> ClassChoices => ClassBook.Classes;

    /// <summary>What the character's current class is and does.</summary>
    public string ClassSummary => ClassBook.Find(_class) is { } info
        ? $"{info.Name} — {info.Description}"
        : $"Class id {_class} is not one of the trilogy's playable classes.";

    /// <summary>The class the user has selected to change to.</summary>
    public int TargetClass
    {
        get => _targetClass;
        set { if (SetField(ref _targetClass, value)) UpdateClassChangeStatus(); }
    }

    /// <summary>Writes the class even when the Review Board's rules say no.</summary>
    public bool IgnoreClassRequirements
    {
        get => _ignoreClassRequirements;
        set { if (SetField(ref _ignoreClassRequirements, value)) UpdateClassChangeStatus(); }
    }

    /// <summary>Whether the pending class change passes the Review Board's rules.</summary>
    public string ClassChangeStatus
    {
        get => _classChangeStatus;
        private set => SetField(ref _classChangeStatus, value);
    }

    /// <summary>The statistics that matter for this character's class, from the game's own fields.</summary>
    public ObservableCollection<ClassAbility> ClassAbilities { get; } = new();

    // --- class-specific scores --------------------------------------------------
    // Each of these is a real int32 field on the character. The four the game rolls
    // against run 0-255, where 255 is a certainty before the remaster's per-map
    // penalty; attacks and songs are plain counts. Edits stay in the view model
    // until Write scores, so a half-typed number is never sent to the game.

    /// <summary>Melee attacks per round (<c>m_nmbrOfAttacks</c>).</summary>
    public int Attacks
    {
        get => _scores.Attacks;
        set => SetScore(_scores.Attacks, value, v => _scores = _scores with { Attacks = v });
    }

    /// <summary>The Rogue's disarm-trap score (<c>m_disarmTrapBonus</c>).</summary>
    public int DisarmTrapBonus
    {
        get => _scores.DisarmTrapBonus;
        set => SetScore(_scores.DisarmTrapBonus, value, v => _scores = _scores with { DisarmTrapBonus = v });
    }

    /// <summary>The Rogue's hide-in-shadows score (<c>m_hideInShadowsBonus</c>).</summary>
    public int HideInShadowsBonus
    {
        get => _scores.HideInShadowsBonus;
        set => SetScore(_scores.HideInShadowsBonus, value, v => _scores = _scores with { HideInShadowsBonus = v });
    }

    /// <summary>The Rogue's item-identification score (<c>m_identifyBonus</c>).</summary>
    public int IdentifyBonus
    {
        get => _scores.IdentifyBonus;
        set => SetScore(_scores.IdentifyBonus, value, v => _scores = _scores with { IdentifyBonus = v });
    }

    /// <summary>The Hunter's critical-hit score (<c>m_criticalHit</c>).</summary>
    public int CriticalHit
    {
        get => _scores.CriticalHit;
        set => SetScore(_scores.CriticalHit, value, v => _scores = _scores with { CriticalHit = v });
    }

    /// <summary>Tunes the Bard can still play before a drink (<c>m_songsRemaining</c>).</summary>
    public int SongsRemaining
    {
        get => _scores.SongsRemaining;
        set => SetScore(_scores.SongsRemaining, value, v => _scores = _scores with { SongsRemaining = v });
    }

    /// <summary>Songs the Bard knows (<c>m_songsKnown</c>).</summary>
    public int SongsKnown
    {
        get => _scores.SongsKnown;
        set => SetScore(_scores.SongsKnown, value, v => _scores = _scores with { SongsKnown = v });
    }

    /// <summary>
    /// Applies one edited score and keeps the abilities panel — which reads the same
    /// numbers — in step with it.
    /// </summary>
    private void SetScore(int current, int value, Action<int> apply, [System.Runtime.CompilerServices.CallerMemberName] string? name = null)
    {
        if (current == value) return;
        apply(value);
        OnPropertyChanged(name);
        RebuildClassAbilities();
    }

    // --- freeze toggles ---------------------------------------------------------
    public bool FreezeHp { get => _freezeHp; set => SetField(ref _freezeHp, value); }
    public bool FreezeSp { get => _freezeSp; set => SetField(ref _freezeSp, value); }

    // --- operations -------------------------------------------------------------
    public void Refresh()
    {
        _hpCur = _record.HpCur;
        _hpMax = _record.HpMax;
        _spCur = _record.SpCur;
        _spMax = _record.SpMax;
        _experience = _record.Experience;
        _gold = _record.Gold;
        _level = _record.Level;
        _race = _record.Race;
        _class = _record.Class;
        _gender = _record.Gender;
        _condition = _record.Condition;
        _strength = _record.GetStat(0);
        _intelligence = _record.GetStat(1);
        _dexterity = _record.GetStat(2);
        _constitution = _record.GetStat(3);
        _luck = _record.GetStat(4);
        _spellLevels = _record.ReadSpellLevels();
        _scores = _record.ReadClassScores();

        foreach (var row in SpellLevels) row.Pull(_spellLevels);
        RefreshLearntSpells();
        RebuildClassAbilities();
        UpdateClassChangeStatus();
        OnPropertyChanged(string.Empty);
    }

    public void WriteAll()
    {
        _record.HpCur = _hpCur;
        _record.HpMax = _hpMax;
        _record.SpCur = _spCur;
        _record.SpMax = _spMax;
        _record.Experience = _experience;
        _record.Gold = _gold;
        _record.Level = _level;
        _record.Race = _race;
        _record.Class = _class;
        _record.Gender = _gender;
        _record.Condition = _condition;
        _record.SetStat(0, _strength);
        _record.SetStat(1, _intelligence);
        _record.SetStat(2, _dexterity);
        _record.SetStat(3, _constitution);
        _record.SetStat(4, _luck);
        foreach (var row in SpellLevels) row.Push();
        _host.OnMessage($"Wrote all fields for {Name}");
    }

    /// <summary>Raises every casting school to the highest level the game grants.</summary>
    public void LearnAllSpells()
    {
        _record.LearnAllClassSpells();
        Refresh();
        _host.OnMessage($"{Name}: every magical school set to spell level {CharacterFormat.MaxSpellLevel}");
    }

    // --- learnt spells ----------------------------------------------------------
    /// <summary>
    /// The spells held in <c>m_learntSpells</c> — the ones taught outright rather than earned
    /// through a school level. This is where ZZGO, NUKE and the chapter quest spells live.
    /// </summary>
    public ObservableCollection<LearntSpellViewModel> LearntSpells { get; } = new();

    /// <summary>A line describing the learnt list's state, including how much room is left in it.</summary>
    public string LearntSpellsSummary
    {
        get
        {
            if (LearntSpells.Count == 0)
                return "No spells granted outright. Anything this character casts comes from a school level.";
            return LearntSpells.Count == 1
                ? "1 spell granted outright, on top of whatever the school levels give."
                : $"{LearntSpells.Count} spells granted outright, on top of whatever the school levels give.";
        }
    }

    /// <summary>Re-reads the learnt-spell list and re-labels it against the game's spell table.</summary>
    public void RefreshLearntSpells()
    {
        LearntSpells.Clear();
        var catalog = _host.Spells;

        foreach (var id in _record.ReadLearntSpells())
        {
            var entry = catalog.Find(id);
            LearntSpells.Add(new LearntSpellViewModel(
                id,
                entry?.Code ?? "",
                entry?.Name ?? SpellCatalog.ReadableName(id),
                entry is { IsSpecial: false } ? $"also granted by {entry.SchoolName} level {entry.Level}" : ""));
        }

        OnPropertyChanged(nameof(LearntSpellsSummary));
    }

    /// <summary>
    /// Teaches the character a spell outright. Reports which route was taken, because appending
    /// into spare capacity and having the game allocate a bigger list are meaningfully different
    /// — the second one runs code inside the game.
    /// </summary>
    public bool GrantSpell(SpellId id, string label)
    {
        var result = _record.GrantSpell(id, _host.Runtime);
        RefreshLearntSpells();

        _host.OnMessage(result.Outcome switch
        {
            CharacterRecord.GrantOutcome.AlreadyKnown =>
                $"{Name}: already knows {label} — {result.Detail}.",
            CharacterRecord.GrantOutcome.AppendedInPlace =>
                $"{Name}: learnt {label} ({result.Detail}).",
            CharacterRecord.GrantOutcome.GrewList =>
                $"{Name}: learnt {label} — {result.Detail}.",
            _ => $"{Name}: could not learn {label} — {result.Detail}",
        });
        return result.Success;
    }

    /// <summary>Takes a granted spell back off the character.</summary>
    public void RevokeSpell(LearntSpellViewModel spell)
    {
        bool ok = _record.RevokeSpell(spell.Id);
        RefreshLearntSpells();
        _host.OnMessage(ok
            ? $"{Name}: {spell.Label} removed from the learnt-spell list."
            : $"{Name}: {spell.Label} could not be removed.");
    }

    /// <summary>Grants every cross-game spell the trilogy never sells — ZZGO, NUKE, GILL and DIVA.</summary>
    public void GrantSpecialSpells()
    {
        int granted = 0, already = 0, failed = 0;

        foreach (var special in SpecialSpells.All)
        {
            var result = _record.GrantSpell(special.Id, _host.Runtime);
            switch (result.Outcome)
            {
                case CharacterRecord.GrantOutcome.AlreadyKnown: already++; break;
                case CharacterRecord.GrantOutcome.Failed: failed++; break;
                default: granted++; break;
            }
        }

        RefreshLearntSpells();
        _host.OnMessage(failed == 0
            ? $"{Name}: {granted} cross-game spell(s) granted, {already} already known."
            : $"{Name}: {granted} granted, {already} already known, {failed} failed — " +
              "the learnt-spell list needs the game to allocate and that is unavailable.");
    }

    /// <summary>Writes one school's spell level straight through to the game.</summary>
    public bool WriteSpellLevel(int classId, int level)
    {
        bool ok = _record.SetSpellLevelClamped(classId, level);
        if (ok) _spellLevels = _record.ReadSpellLevels();
        return ok;
    }

    public void SetInfiniteItems()
    {
        bool ok = _record.SetAllItemsInfinite();
        _host.OnMessage(ok
            ? $"{Name}: item charges zeroed — the game stops consuming a charge once the count is 0"
            : $"{Name}: no items to edit (the inventory array was not readable)");
    }

    public void FullHeal()
    {
        _record.HpCur = _record.HpMax;
        _record.SpCur = _record.SpMax;
        _record.Condition = 0;
        Refresh();
        _host.OnMessage($"{Name}: full heal (HP {_hpCur}/{_hpMax}, SP {_spCur}/{_spMax})");
    }

    public void MaxAttributes()
    {
        int max = GameFacts.MaxAttribute;
        for (int i = 0; i < CharacterFormat.StatCount; i++) _record.SetStat(i, max);
        Refresh();
        _host.OnMessage($"{Name}: all attributes set to {max}");
    }

    /// <summary>
    /// Turns the character into <see cref="TargetClass"/>. The Review Board's rules are checked
    /// first and refused unless <see cref="IgnoreClassRequirements"/> is set — this is a
    /// trainer, but a class the game would never grant is worth flagging before it is written.
    /// </summary>
    public void ChangeClass()
    {
        var target = ClassBook.Find(_targetClass);
        if (target == null)
        {
            _host.OnMessage($"{Name}: class id {_targetClass} is not one of the trilogy's classes.");
            return;
        }

        var check = ClassBook.CanChangeTo(_record.Class, _targetClass, _record.Level, _record.ReadSpellLevels());
        if (!check.Allowed && !_ignoreClassRequirements)
        {
            _host.OnMessage($"{Name}: cannot become a {target.Name} — {check.Reason} " +
                            "Tick “Ignore requirements” to write it anyway.");
            return;
        }

        string what = _record.ChangeClass(_targetClass, grantSpellLevel: true);
        Refresh();
        _host.OnMessage(check.Allowed
            ? $"{Name}: {what}"
            : $"{Name}: {what} — requirements overridden ({check.Reason})");
    }

    /// <summary>Writes the edited class-specific scores back to the character.</summary>
    public void WriteClassScores()
    {
        _record.WriteClassScores(_scores);
        Refresh();
        _host.OnMessage($"{Name}: class scores written — " +
                        $"disarm {_scores.DisarmTrapBonus}, hide {_scores.HideInShadowsBonus}, " +
                        $"identify {_scores.IdentifyBonus}, critical {_scores.CriticalHit}, " +
                        $"attacks {_scores.Attacks}, songs {_scores.SongsRemaining}/{_scores.SongsKnown}");
    }

    /// <summary>
    /// Tops up the scores the game rolls against and refills the Bard's tunes. Attacks
    /// per round and songs known are left as they are — see
    /// <see cref="ClassBook.MaxAbilityScores"/> for why.
    /// </summary>
    public void MaxClassScores()
    {
        _scores = ClassBook.MaxAbilityScores(_scores, _level);
        _record.WriteClassScores(_scores);
        Refresh();
        _host.OnMessage($"{Name}: disarm, hide, identify and critical hit set to {ClassBook.MaxAbilityScore} " +
                        $"(a certainty before the game's per-map penalty); tunes refilled to {_scores.SongsRemaining}. " +
                        "Attacks per round and songs known left alone — they are counts, not chances.");
    }

    private void OnClassInputsChanged()
    {
        RebuildClassAbilities();
        UpdateClassChangeStatus();
        OnPropertyChanged(nameof(DisplayClass));
        OnPropertyChanged(nameof(ClassSummary));
        OnPropertyChanged(nameof(Header));
    }

    private void RebuildClassAbilities()
    {
        ClassAbilities.Clear();
        foreach (var ability in ClassBook.AbilitiesFor(_class, _level, _dexterity, _scores, _spellLevels, _host.Spells))
            ClassAbilities.Add(ability);
    }

    private void UpdateClassChangeStatus()
    {
        if (_targetClass == _class)
        {
            ClassChangeStatus = $"Already a {ClassBook.ClassName(_class)} — pick another class to change to.";
            return;
        }

        var check = ClassBook.CanChangeTo(_class, _targetClass, _level, _spellLevels);
        ClassChangeStatus = check.Allowed ? $"OK — {check.Reason}"
            : _ignoreClassRequirements ? $"Blocked, will be forced — {check.Reason}"
            : $"Blocked — {check.Reason}";
    }

    /// <summary>Called by the poll loop to apply freezes. Reads the current max from the record
    /// each tick rather than using a cached value, so a level drain or other max-reducing effect
    /// is respected immediately instead of over-healing past the real max.</summary>
    public void PollFreezes()
    {
        if (_freezeHp && _record.HpMax > 0) _record.HpCur = _record.HpMax;
        if (_freezeSp && _record.SpMax > 0) _record.SpCur = _record.SpMax;
    }
}

/// <summary>
/// One casting school's row in the spell-level grid. The game holds these in an
/// <c>int[16]</c> indexed by class id, so <see cref="ClassId"/> is both the school and its
/// index; editing a row writes straight through to the character.
/// </summary>
public sealed class SpellLevelViewModel : ObservableObject
{
    private readonly CharacterViewModel _owner;
    private int _level;
    private bool _suppressWrite;

    public SpellLevelViewModel(int classId, string name, CharacterViewModel owner)
    {
        ClassId = classId;
        Name = name;
        _owner = owner;
    }

    /// <summary>Class id of the school, which is also its index into <c>m_spellLevel</c>.</summary>
    public int ClassId { get; }

    public string Name { get; }

    /// <summary>Spell level 0–7; editing writes it to the game immediately.
    /// Out-of-range input is clamped rather than thrown, so a bad UI binding cannot crash the poll loop.</summary>
    public int Level
    {
        get => _level;
        set
        {
            int clamped = Math.Clamp(value, 0, CharacterFormat.MaxSpellLevel);
            if (!SetField(ref _level, clamped) || _suppressWrite) return;
            _owner.WriteSpellLevel(ClassId, clamped);
        }
    }

    /// <summary>
    /// Refreshes the row from a freshly read <c>m_spellLevel</c> array.
    ///
    /// <para>The value is clamped rather than trusted. The setter clamps anything outside
    /// 0–7 because a caller asking for level 9 is a bug, but this is the read path: the array
    /// comes out of the game, and on the structural-scan fallback it can come out of an object
    /// that only <em>looks</em> like a character, so junk is a normal input here. Letting the
    /// setter throw would unwind out of the view-model constructor and abort the whole locate,
    /// leaving a half-populated party. The <c>finally</c> matters for the same reason — a row
    /// left suppressed would silently stop writing the user's edits through to the game.</para>
    /// </summary>
    public void Pull(IReadOnlyList<int> spellLevels)
    {
        int raw = ClassId >= 0 && ClassId < spellLevels.Count ? spellLevels[ClassId] : 0;
        _suppressWrite = true;
        try
        {
            Level = Math.Clamp(raw, 0, CharacterFormat.MaxSpellLevel);
        }
        finally
        {
            _suppressWrite = false;
        }
    }

    /// <summary>Writes this row's level back to the character.</summary>
    public bool Push() => _owner.WriteSpellLevel(ClassId, _level);
}

/// <summary>
/// One entry of a character's <c>m_learntSpells</c> list, labelled against the game's spell
/// table where possible.
/// </summary>
/// <param name="Id">The stored <see cref="SpellId"/>.</param>
/// <param name="Code">The game's four-letter code, empty when the table has not been read.</param>
/// <param name="Name">A readable name for the spell.</param>
/// <param name="Note">Why it might also be known anyway, e.g. a school already grants it.</param>
public sealed record LearntSpellViewModel(SpellId Id, string Code, string Name, string Note)
{
    public string Label => Code.Length > 0 ? $"{Code} — {Name}" : Name;

    /// <summary>What to show in the list: the label, plus any caveat.</summary>
    public string Display => Note.Length > 0 ? $"{Label}  ({Note})" : Label;
}

/// <summary>Interface the view model uses to reach the host's shared state.</summary>
public interface ICharacterHost
{
    void OnMessage(string msg);

    /// <summary>The game's spell table, or an empty one before it has been read.</summary>
    SpellCatalog Spells { get; }

    /// <summary>
    /// The injection helper used to grow a full learnt-spell list, or null when it is
    /// unavailable or the user has turned it off.
    /// </summary>
    Il2CppRuntime? Runtime { get; }
}
