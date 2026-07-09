using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using MightAndMagic1Trainer.Game;
using MightAndMagic1Trainer.Memory;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>
/// One row in the spellbook picker: a <see cref="Spell"/> plus whether the current
/// caster has reached the spell's level (and so can actually cast it). Used by the
/// "Cast a spell" panel, which shows the whole school but dims what's out of reach.
/// </summary>
public sealed class SpellEntryViewModel
{
    public Spell Spell { get; }
    public bool IsCastable { get; }

    public SpellEntryViewModel(Spell spell, bool castable)
    {
        Spell = spell;
        IsCastable = castable;
    }

    public string LevelGroup => $"Level {Spell.Level}";
    public string Display => Spell.Display;
    public string CostText => Spell.CostText;
    public string Description => Spell.Description;
    public double RowOpacity => IsCastable ? 1.0 : 0.4;
    public string Lock => IsCastable ? "" : "🔒";

    /// <summary>"Level X · spell #Y · 2 SP" — the selected-spell summary line.</summary>
    public string DetailLine => $"Level {Spell.Level} · spell #{Spell.Number} · {Spell.CostText}";

    /// <summary>Empty when castable; otherwise why it isn't (shown under the description).</summary>
    public string CastableNote => IsCastable
        ? ""
        : $"🔒 Requires spell level {Spell.Level} — raise this character's Spell level to cast it.";
}

/// <summary>One saved key macro: a friendly label and the key sequence it replays.</summary>
public sealed class MacroViewModel : ObservableObject
{
    private string _name;
    private string _keys;

    public MacroViewModel(string name, string keys)
    {
        _name = name;
        _keys = keys;
    }

    public string Name { get => _name; set => SetField(ref _name, value); }
    public string Keys
    {
        get => _keys;
        set { if (SetField(ref _keys, value)) OnPropertyChanged(nameof(KeysError)); }
    }

    /// <summary>Null when the sequence parses; otherwise a human-readable reason.</summary>
    public string? KeysError => KeyboardSender.Validate(_keys);
}

/// <summary>
/// "Quick-cast" panel: a list of saved key macros that replay a clunky in-game menu
/// walk (e.g. the spell-cast sequence "5 c 1 6 Enter") into the focused game window
/// with one click. Macros persist to %APPDATA%\MM1Trainer\macros.json.
/// </summary>
public sealed class SpellMacrosViewModel : ObservableObject
{
    private readonly Func<int?> _targetPid;     // attached emulator pid, or null
    private readonly Action<string> _setStatus;

    public SpellMacrosViewModel(Func<int?> targetPid, Action<string> setStatus)
    {
        _targetPid = targetPid;
        _setStatus = setStatus;

        SendCommand = new RelayCommand(p => Send(p as MacroViewModel), p => p is MacroViewModel);
        AddCommand = new RelayCommand(Add);
        RemoveCommand = new RelayCommand(p => Remove(p as MacroViewModel), p => p is MacroViewModel);
        SaveCommand = new RelayCommand(Save);
        CastSpellCommand = new RelayCommand(
            _ => CastSpell(),
            _ => _caster != null && _selectedSpell is { IsCastable: true } && !IsSending);

        SpellsView = CollectionViewSource.GetDefaultView(Spells);
        SpellsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SpellEntryViewModel.LevelGroup)));

        Load();
    }

    public ObservableCollection<MacroViewModel> Macros { get; } = new();

    private int _keyDelayMs = 500;
    public int KeyDelayMs { get => _keyDelayMs; set => SetField(ref _keyDelayMs, Math.Clamp(value, 0, 1000)); }

    private int _focusDelayMs = 120;
    public int FocusDelayMs { get => _focusDelayMs; set => SetField(ref _focusDelayMs, Math.Clamp(value, 0, 2000)); }

    public RelayCommand SendCommand { get; }
    public RelayCommand AddCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CastSpellCommand { get; }

    private bool _isSending;
    public bool IsSending
    {
        get => _isSending;
        private set { if (SetField(ref _isSending, value)) CastSpellCommand.RaiseCanExecuteChanged(); }
    }

    // ======================= Spellbook ("Cast a spell") =========================
    // Knows the currently-selected character, derives which spell list that class can
    // cast, and turns a chosen spell into the in-game key walk automatically:
    // "{party slot} c {spell level} {spell number} {ENTER}". The caller keeps us in
    // sync via SetCaster whenever the selected character (or its class/level) changes.

    private CharacterViewModel? _caster;
    // Last class / known spell level the list was built for, so a flood of (unchanged)
    // property notifications during live refresh doesn't rebuild and reset the picker.
    private int _builtForClass = -1, _builtForSpellLevel = -1;

    /// <summary>The spells of the caster's school (empty for non-casters); dims the unreachable ones.</summary>
    public ObservableCollection<SpellEntryViewModel> Spells { get; } = new();

    /// <summary>Grouped-by-level view of <see cref="Spells"/> for the picker.</summary>
    public ICollectionView SpellsView { get; }

    private SpellEntryViewModel? _selectedSpell;
    // Set while RebuildSpells reassigns the selection programmatically, so swapping
    // characters (or a live refresh) doesn't fire off an unwanted cast.
    private bool _suppressAutoCast;
    public SpellEntryViewModel? SelectedSpell
    {
        get => _selectedSpell;
        set
        {
            if (SetField(ref _selectedSpell, value))
            {
                OnPropertyChanged(nameof(SpellKeysPreview));
                CastSpellCommand.RaiseCanExecuteChanged();

                // Picking a castable spell from the dropdown casts it immediately;
                // the "Cast spell" button remains for re-casting the same selection.
                if (!_suppressAutoCast && value is { IsCastable: true })
                    CastSpell();
            }
        }
    }

    /// <summary>True when a character is selected (caster or not).</summary>
    public bool HasCaster => _caster != null;

    /// <summary>True when the selected character's class can cast spells at all.</summary>
    public bool CasterCanCast => _caster != null && Spellbook.SchoolForClass(_caster.Record.Class) != SpellSchool.None;

    /// <summary>"Casting as Crag Hack (party slot 1)" — or a prompt when nothing is selected.</summary>
    public string CasterHeader => _caster == null
        ? "Select a character on the left to cast as them."
        : $"Casting as {_caster.Record.Name} (party slot {_caster.Record.Slot + 1})";

    /// <summary>Class / school / known-level summary, or why this character can't cast.</summary>
    public string CasterClassLine
    {
        get
        {
            if (_caster == null) return "";
            var school = Spellbook.SchoolForClass(_caster.Record.Class);
            if (school == SpellSchool.None)
                return $"{_caster.Record.ClassName} characters can't cast spells.";
            return $"{_caster.Record.ClassName} · {Spellbook.SchoolName(school)} spells · "
                 + $"knows up to level {_caster.Record.SpellLevel}";
        }
    }

    /// <summary>The exact keystrokes the Cast button will send for the current selection.</summary>
    public string SpellKeysPreview =>
        _caster != null && _selectedSpell != null ? BuildSpellKeys(_caster, _selectedSpell.Spell) : "";

    /// <summary>Points the spellbook at a character (null clears it). Re-syncs on the character's edits.</summary>
    public void SetCaster(CharacterViewModel? caster)
    {
        if (ReferenceEquals(_caster, caster)) return;
        if (_caster != null) _caster.PropertyChanged -= OnCasterChanged;
        _caster = caster;
        if (_caster != null) _caster.PropertyChanged += OnCasterChanged;
        RebuildSpells();
    }

    // Class or spell-level edits change what's castable. Live refresh re-raises these
    // every tick with unchanged values, so RebuildSpells no-ops unless something moved.
    private void OnCasterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CharacterViewModel.ClassIndex)
            or nameof(CharacterViewModel.SpellLevel)
            or nameof(CharacterViewModel.Name)
            or null)
            RebuildSpells();
    }

    private void RebuildSpells()
    {
        var school = _caster == null ? SpellSchool.None : Spellbook.SchoolForClass(_caster.Record.Class);
        int known = _caster?.Record.SpellLevel ?? -1;
        int cls = _caster?.Record.Class ?? -1;

        // Only rebuild the (selection-resetting) list when the inputs actually changed;
        // the header/preview below always refresh cheaply so the name etc. stay current.
        if (cls != _builtForClass || known != _builtForSpellLevel)
        {
            _builtForClass = cls;
            _builtForSpellLevel = known;
            _suppressAutoCast = true;
            try
            {
                SelectedSpell = null;
                Spells.Clear();
                if (school != SpellSchool.None)
                {
                    foreach (var s in Spellbook.For(school))
                        Spells.Add(new SpellEntryViewModel(s, s.Level <= known));
                    SelectedSpell = Spells.FirstOrDefault(e => e.IsCastable) ?? Spells.FirstOrDefault();
                }
            }
            finally { _suppressAutoCast = false; }
        }

        OnPropertyChanged(nameof(HasCaster));
        OnPropertyChanged(nameof(CasterCanCast));
        OnPropertyChanged(nameof(CasterHeader));
        OnPropertyChanged(nameof(CasterClassLine));
        OnPropertyChanged(nameof(SpellKeysPreview));
        CastSpellCommand.RaiseCanExecuteChanged();
    }

    private static string BuildSpellKeys(CharacterViewModel caster, Spell spell) =>
        $"{caster.Record.Slot + 1} c {spell.Level} {spell.Number} {{ENTER}}";

    private async void CastSpell()
    {
        if (_caster == null || _selectedSpell == null) return;

        // Selecting a spell auto-casts, so this can be reached while a prior send is
        // still in flight (the button can't — its CanExecute blocks on IsSending).
        // Tell the user rather than silently dropping the cast.
        if (IsSending)
        {
            _setStatus($"Still sending the previous cast — pick {_selectedSpell.Spell.Name} again in a moment.");
            return;
        }

        var spell = _selectedSpell.Spell;

        // Defensive: the command's CanExecute already blocks uncastable spells, but guard
        // anyway in case CastSpell is ever invoked outside the button (and to explain why).
        if (!_selectedSpell.IsCastable)
        {
            _setStatus($"{_caster.Record.Name} can't cast {spell.Name} yet — needs spell level {spell.Level} "
                     + $"(knows up to {_caster.Record.SpellLevel}).");
            return;
        }

        await SendKeysAsync(
            BuildSpellKeys(_caster, spell),
            "Attach to the game first — spells are sent to the attached emulator's window.",
            $"Casting {spell.Name} — switching to the game window…",
            sent => $"Cast {spell.Name} ({sent.Trim()}).",
            reason => $"Couldn't cast {spell.Name}: {reason}");
    }

    /// <summary>
    /// Shared focus-and-replay path for both the spellbook "Cast" button and the saved
    /// macros: resolve the target pid, run <see cref="KeyboardSender.Send"/> on a
    /// background thread, and report the outcome via the supplied status formatters.
    /// Callers handle their own <see cref="IsSending"/> pre-guard and validation.
    /// </summary>
    private async System.Threading.Tasks.Task SendKeysAsync(
        string keys, string notAttachedStatus, string sendingStatus,
        Func<string, string> okStatus, Func<string, string> failStatus)
    {
        int? pid = _targetPid();
        if (pid == null) { _setStatus(notAttachedStatus); return; }

        IsSending = true;
        _setStatus(sendingStatus);
        int keyDelay = _keyDelayMs, focusDelay = _focusDelayMs, targetPid = pid.Value;
        try
        {
            bool ok = false; string reason = "";
            await System.Threading.Tasks.Task.Run(() =>
                ok = KeyboardSender.Send(targetPid, keys, keyDelay, focusDelay, out reason));
            _setStatus(ok ? okStatus(keys) : failStatus(reason));
        }
        catch (Exception ex)
        {
            // async void callers: swallow so a send fault can't tear down the app via the
            // Dispatcher's unhandled-exception path.
            _setStatus(failStatus(ex.Message));
        }
        finally
        {
            IsSending = false;
        }
    }

    private void Add()
    {
        Macros.Add(new MacroViewModel("New cast", "5 c 1 6 {ENTER}"));
        Save();
    }

    private void Remove(MacroViewModel? m)
    {
        if (m != null) { Macros.Remove(m); Save(); }
    }

    private async void Send(MacroViewModel? macro)
    {
        if (macro == null || IsSending) return;

        var err = KeyboardSender.Validate(macro.Keys);
        if (err != null) { _setStatus($"Macro \"{macro.Name}\" not sent — {err}"); return; }

        await SendKeysAsync(
            macro.Keys,
            "Attach to the game first — macros are sent to the attached emulator's window.",
            $"Casting \"{macro.Name}\" — switching to the game window…",
            _ => $"Sent \"{macro.Name}\" to the game.",
            reason => $"Couldn't send \"{macro.Name}\": {reason}");
    }

    // --- persistence ------------------------------------------------------------
    private sealed class Persisted
    {
        public int KeyDelayMs { get; set; } = 500;
        public int FocusDelayMs { get; set; } = 120;
        public List<MacroDto> Macros { get; set; } = new();
    }

    private sealed class MacroDto
    {
        public string Name { get; set; } = "";
        public string Keys { get; set; } = "";
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MM1Trainer", "macros.json");

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var data = new Persisted
            {
                KeyDelayMs = _keyDelayMs,
                FocusDelayMs = _focusDelayMs,
                Macros = Macros.Select(m => new MacroDto { Name = m.Name, Keys = m.Keys }).ToList(),
            };
            File.WriteAllText(FilePath, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch { /* best-effort; a failed save shouldn't break the UI */ }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var data = JsonSerializer.Deserialize<Persisted>(File.ReadAllText(FilePath));
                if (data != null)
                {
                    _keyDelayMs = Math.Clamp(data.KeyDelayMs, 0, 1000);
                    _focusDelayMs = Math.Clamp(data.FocusDelayMs, 0, 2000);
                    foreach (var m in data.Macros) Macros.Add(new MacroViewModel(m.Name, m.Keys));
                    if (Macros.Count > 0) return;
                }
            }
        }
        catch { /* fall through to defaults */ }

        foreach (var m in DefaultMacros()) Macros.Add(m);
    }

    private static IEnumerable<MacroViewModel> DefaultMacros() => new[]
    {
        new MacroViewModel("Example: char 5 casts L1 spell 6",  "5 c 1 6 {ENTER}"),
        new MacroViewModel("Example: char 1 casts L1 spell 1",  "1 c 1 1 {ENTER}"),
    };
}
