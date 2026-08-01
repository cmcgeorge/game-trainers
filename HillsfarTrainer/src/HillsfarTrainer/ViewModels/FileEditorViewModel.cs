using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using HillsfarTrainer.Game;

namespace HillsfarTrainer.ViewModels;

/// <summary>
/// The offline character-file editor — edit a <c>.HIL</c> or <c>.PRE</c> without the game running.
///
/// <para>This is safe precisely because the file format has nothing hidden in it: a character file is
/// a raw dump of the 188-byte record, with no header, no checksum and no encryption. Confirmed
/// end-to-end — a file edited on disk loads through the game's own <i>Load a character</i> menu with
/// every value showing on the character sheet. Bytes the trainer does not interpret are carried
/// through untouched, and a one-shot <c>.bak</c> is taken before the first write.</para>
/// </summary>
public sealed class FileEditorViewModel : ObservableObject
{
    private readonly Action<string> _report;

    /// <summary>Character files found in the chosen folder.</summary>
    public ObservableCollection<CharacterFile> Files { get; } = new();

    /// <summary>Builds the editor.</summary>
    public FileEditorViewModel(Action<string> report)
    {
        _report = report ?? (_ => { });
        LoadFolderCommand = new RelayCommand(_ => LoadFolder(FolderPath));
        SaveCommand = new RelayCommand(Save, () => Selected != null && IsDirty);
        RevertCommand = new RelayCommand(Revert, () => Selected != null && IsDirty);
        MaxAbilitiesCommand = new RelayCommand(
            () => Mutate(r => r.MaxAbilities(), "Abilities maxed (not yet saved)."));
        MaxConsumablesCommand = new RelayCommand(
            () => Mutate(r => r.MaxConsumables(), "Consumables maxed (not yet saved)."));
        HealCommand = new RelayCommand(
            () => Mutate(r => r.HealFully(), "Healed to full (not yet saved)."));
    }

    private string _folderPath = string.Empty;

    /// <summary>Folder to scan for character files — normally the game's own directory.</summary>
    public string FolderPath
    {
        get => _folderPath;
        set => SetField(ref _folderPath, value);
    }

    private CharacterFile? _selected;

    /// <summary>The file being edited.</summary>
    public CharacterFile? Selected
    {
        get => _selected;
        set
        {
            if (!SetField(ref _selected, value)) return;
            // Deliberately does NOT reset a dirty flag: dirtiness belongs to the file, so switching
            // away from an edited file and back again leaves Save enabled and the edits intact.
            RaiseAll();
            RaiseCommands();
        }
    }

    /// <summary>True when the selected file has unsaved edits.</summary>
    public bool IsDirty => Selected?.IsDirty ?? false;

    /// <summary>True when any loaded file has unsaved edits.</summary>
    public bool AnyDirty => Files.Any(f => f.IsDirty);

    /// <summary>True when a file is loaded.</summary>
    public bool HasSelection => Selected != null;

    // --- commands -------------------------------------------------------------

    /// <summary>Scans <see cref="FolderPath"/> for character files.</summary>
    public ICommand LoadFolderCommand { get; }

    /// <summary>Writes the edits back, taking a one-shot backup first.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>Re-reads the selected file from disk, discarding edits.</summary>
    public ICommand RevertCommand { get; }

    /// <summary>Sets every ability to 19.</summary>
    public ICommand MaxAbilitiesCommand { get; }

    /// <summary>Fills both consumables to 99.</summary>
    public ICommand MaxConsumablesCommand { get; }

    /// <summary>Restores hit points to maximum.</summary>
    public ICommand HealCommand { get; }

    /// <summary>
    /// Loads every character file in a folder.
    /// </summary>
    /// <param name="folder">Folder to scan.</param>
    /// <param name="quiet">
    /// When true, nothing is reported to the status bar. Used for the speculative scan at startup, so
    /// a failed guess does not overwrite the shell's "attach to the game" instructions with file-editor
    /// advice.
    /// </param>
    public void LoadFolder(string? folder, bool quiet = false)
    {
        // Reloading throws away in-memory edits, so say so rather than losing them silently.
        int discarded = Files.Count(f => f.IsDirty);

        Files.Clear();
        Selected = null;
        if (string.IsNullOrWhiteSpace(folder))
        {
            if (!quiet) _report("Choose the folder that holds MAIN.EXE and the .HIL character files.");
            return;
        }
        if (!Directory.Exists(folder))
        {
            if (!quiet) _report($"Folder not found: {folder}");
            return;
        }

        FolderPath = folder;
        foreach (var f in CharacterFile.LoadDirectory(folder)) Files.Add(f);
        Selected = Files.FirstOrDefault();
        OnPropertyChanged(nameof(AnyDirty));
        if (quiet) return;

        string discardedNote = discarded > 0
            ? $" Discarded unsaved edits to {discarded} file(s)."
            : string.Empty;
        _report(Files.Count == 0
            ? $"No .HIL or .PRE character files in {folder}.{discardedNote}"
            : $"Loaded {Files.Count} character file(s) from {folder}.{discardedNote}");
    }

    /// <summary>
    /// Writes a 188-byte record out as a <c>.HIL</c> in the current folder.
    ///
    /// <para>The filename comes from <see cref="CharacterFile.SuggestFileName"/>, which is the stem
    /// the <i>game</i> uses — so the target is very often the player's own save rather than a new
    /// file. <see cref="CharacterFile.SaveAs"/> therefore backs an existing target up before
    /// overwriting it, and the fact that it did is reported.</para>
    /// </summary>
    public void ExportRecord(ReadOnlySpan<byte> record, string characterName)
    {
        if (string.IsNullOrWhiteSpace(FolderPath) || !Directory.Exists(FolderPath))
        {
            _report("Pick the game folder on the Character files tab first.");
            return;
        }
        try
        {
            string name = CharacterFile.SuggestFileName(characterName);
            string path = Path.Combine(FolderPath, name);
            bool overwrote = CharacterFile.FromRecord(path, record).SaveAs(path);
            // Refresh quietly and report afterwards: a plain LoadFolder would overwrite the message
            // below, which is the only thing telling the user their own save was just overwritten.
            // Unsaved edits elsewhere in the list are preserved by refusing to reload over them.
            bool keptEdits = AnyDirty;
            if (!keptEdits) LoadFolder(FolderPath, quiet: true);

            _report($"Wrote {name}."
                    + (overwrote ? $" It already existed, so {name}.bak was kept." : string.Empty)
                    + (keptEdits
                        ? " The file list was not refreshed because it has unsaved edits."
                        : string.Empty)
                    + " The game caches its directory listing, so a new file may not appear in its"
                    + " load menu until Hillsfar is restarted.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _report("Export failed: " + ex.Message);
        }
    }

    private void Save()
    {
        var file = Selected;
        if (file == null) return;
        try
        {
            file.Save();
            RaiseDirty();
            _report($"Saved {file.FileName} (a one-shot {file.FileName}.bak was kept).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The backup is taken before the write, so a failure here leaves the file untouched and
            // the edits still in memory — keep the dirty flag so Save can be retried.
            _report("Save failed, the file was not changed: " + ex.Message);
        }
    }

    private void Revert()
    {
        var file = Selected;
        if (file == null) return;
        try
        {
            var fresh = CharacterFile.Load(file.Path);
            int index = Files.IndexOf(file);
            if (index >= 0) Files[index] = fresh;
            Selected = fresh;
            RaiseDirty();
            _report($"Reverted {fresh.FileName} from disk.");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            _report("Revert failed: " + ex.Message);
        }
    }

    private void Mutate(Action<CharacterRecord> action, string message)
    {
        var file = Selected;
        if (file == null) return;
        action(file.Record);
        file.MarkDirty();
        RaiseAll();
        // RaiseAll only raises property changes; RelayCommand is not wired to CommandManager, so
        // Save/Revert stay greyed out after a bulk action unless their CanExecute is re-queried.
        RaiseCommands();
        _report(message);
    }

    // --- edited fields --------------------------------------------------------

    private CharacterRecord? R => Selected?.Record;

    private void SetAndMark(Action<CharacterRecord> apply, string propertyName)
    {
        var file = Selected;
        if (file == null) return;
        apply(file.Record);
        file.MarkDirty();
        OnPropertyChanged(propertyName);
        // Summary is derived from the whole record, so it has to be re-read after any single edit —
        // otherwise the line at the top of the pane keeps showing the pre-edit character.
        OnPropertyChanged(nameof(Summary));
        RaiseDirty();
    }

    /// <summary>Re-reads the dirty-derived state and re-evaluates Save/Revert.</summary>
    private void RaiseDirty()
    {
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(AnyDirty));
        RaiseCommands();
    }

    /// <summary>Character name.</summary>
    public string Name
    {
        get => R?.Name ?? string.Empty;
        set => SetAndMark(r => r.Name = value, nameof(Name));
    }

    /// <summary>Race index.</summary>
    public int RaceIndex
    {
        get => R?.Race ?? 0;
        set => SetAndMark(r => r.Race = value, nameof(RaceIndex));
    }

    /// <summary>Gender index.</summary>
    public int GenderIndex
    {
        get => R?.Gender ?? 0;
        set => SetAndMark(r => r.Gender = value, nameof(GenderIndex));
    }

    /// <summary>Alignment index.</summary>
    public int AlignmentIndex
    {
        get => R?.Alignment ?? 0;
        set => SetAndMark(r => r.Alignment = value, nameof(AlignmentIndex));
    }

    /// <summary>Selected index into <see cref="ClassBook.Classes"/>.</summary>
    public int ClassChoiceIndex
    {
        get
        {
            var r = R;
            if (r == null) return -1;
            for (int i = 0; i < ClassBook.Classes.Count; i++)
                if (ClassBook.Classes[i].Mask == r.ClassMask) return i;
            return -1;
        }
        set
        {
            if (value < 0 || value >= ClassBook.Classes.Count) return;
            SetAndMark(r => r.ClassMask = ClassBook.Classes[value].Mask, nameof(ClassChoiceIndex));
        }
    }

    /// <summary>Age.</summary>
    public int Age
    {
        get => R?.Age ?? 0;
        set => SetAndMark(r => r.Age = value, nameof(Age));
    }

    /// <summary>Strength.</summary>
    public int Strength
    {
        get => R?.Strength ?? 0;
        set => SetAndMark(r => r.Strength = value, nameof(Strength));
    }

    /// <summary>Exceptional-strength percentile.</summary>
    public int StrengthPercentile
    {
        get => R?.StrengthPercentile ?? 0;
        set => SetAndMark(r => r.StrengthPercentile = value, nameof(StrengthPercentile));
    }

    /// <summary>Intelligence.</summary>
    public int Intelligence
    {
        get => R?.Intelligence ?? 0;
        set => SetAndMark(r => r.Intelligence = value, nameof(Intelligence));
    }

    /// <summary>Wisdom.</summary>
    public int Wisdom
    {
        get => R?.Wisdom ?? 0;
        set => SetAndMark(r => r.Wisdom = value, nameof(Wisdom));
    }

    /// <summary>Dexterity.</summary>
    public int Dexterity
    {
        get => R?.Dexterity ?? 0;
        set => SetAndMark(r => r.Dexterity = value, nameof(Dexterity));
    }

    /// <summary>Constitution.</summary>
    public int Constitution
    {
        get => R?.Constitution ?? 0;
        set => SetAndMark(r => r.Constitution = value, nameof(Constitution));
    }

    /// <summary>Charisma.</summary>
    public int Charisma
    {
        get => R?.Charisma ?? 0;
        set => SetAndMark(r => r.Charisma = value, nameof(Charisma));
    }

    /// <summary>Current hit points.</summary>
    public int HitPoints
    {
        get => R?.HitPoints ?? 0;
        set => SetAndMark(r => r.HitPoints = value, nameof(HitPoints));
    }

    /// <summary>Maximum hit points.</summary>
    public int HitPointsMax
    {
        get => R?.HitPointsMax ?? 0;
        set
        {
            SetAndMark(r => r.HitPointsMax = value, nameof(HitPointsMax));
            // Lowering the maximum clamps the current total down with it, so the current box has to
            // be re-read or it contradicts the summary line directly above it.
            OnPropertyChanged(nameof(HitPoints));
        }
    }

    /// <summary>Gold.</summary>
    public uint Gold
    {
        get => R?.Gold ?? 0;
        set => SetAndMark(r => r.Gold = value, nameof(Gold));
    }

    /// <summary>Experience.</summary>
    public uint Experience
    {
        get => R?.Experience ?? 0;
        set => SetAndMark(r => r.Experience = value, nameof(Experience));
    }

    /// <summary>Cleric level.</summary>
    public int ClericLevel
    {
        get => R?.ClericLevel ?? 0;
        set => SetAndMark(r => r.ClericLevel = value, nameof(ClericLevel));
    }

    /// <summary>Magic-User level.</summary>
    public int MagicUserLevel
    {
        get => R?.MagicUserLevel ?? 0;
        set => SetAndMark(r => r.MagicUserLevel = value, nameof(MagicUserLevel));
    }

    /// <summary>Fighter level.</summary>
    public int FighterLevel
    {
        get => R?.FighterLevel ?? 0;
        set => SetAndMark(r => r.FighterLevel = value, nameof(FighterLevel));
    }

    /// <summary>Thief level.</summary>
    public int ThiefLevel
    {
        get => R?.ThiefLevel ?? 0;
        set => SetAndMark(r => r.ThiefLevel = value, nameof(ThiefLevel));
    }

    /// <summary>Knock rings.</summary>
    public int KnockRings
    {
        get => R?.KnockRings ?? 0;
        set => SetAndMark(r => r.KnockRings = value, nameof(KnockRings));
    }

    /// <summary>Healing potions.</summary>
    public int HealingPotions
    {
        get => R?.HealingPotions ?? 0;
        set => SetAndMark(r => r.HealingPotions = value, nameof(HealingPotions));
    }

    /// <summary>Archery-range level.</summary>
    public int ArcheryLevel
    {
        get => R?.ArcheryLevel ?? 0;
        set => SetAndMark(r => r.ArcheryLevel = value, nameof(ArcheryLevel));
    }

    /// <summary>Hour of day, 1..24.</summary>
    public int Hour
    {
        get => R?.Hour ?? 1;
        set => SetAndMark(r => r.Hour = value, nameof(Hour));
    }

    /// <summary>A one-line description of the loaded file.</summary>
    public string Summary => R?.Summary() ?? "(no file loaded)";

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(AnyDirty));
        foreach (var name in new[]
                 {
                     nameof(Name), nameof(RaceIndex), nameof(GenderIndex), nameof(AlignmentIndex),
                     nameof(ClassChoiceIndex), nameof(Age), nameof(Strength), nameof(StrengthPercentile),
                     nameof(Intelligence), nameof(Wisdom), nameof(Dexterity), nameof(Constitution),
                     nameof(Charisma), nameof(HitPoints), nameof(HitPointsMax), nameof(Gold),
                     nameof(Experience), nameof(ClericLevel), nameof(MagicUserLevel),
                     nameof(FighterLevel), nameof(ThiefLevel), nameof(KnockRings),
                     nameof(HealingPotions), nameof(ArcheryLevel), nameof(Hour),
                 })
            OnPropertyChanged(name);
    }

    private void RaiseCommands()
    {
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RevertCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
