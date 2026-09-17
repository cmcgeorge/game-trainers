using System.Collections.ObjectModel;
using System.IO;
using Space1889Trainer.Game;

namespace Space1889Trainer.ViewModels;

/// <summary>
/// The Save Editor tab: opens a <c>*.SAV</c> (or <c>DEF.S</c>) and edits it with the game closed. A save is a verbatim
/// image of the live state block, so this tab is the same character and world editors over a different
/// <see cref="IStateHost"/>. Edits are made in memory; Save writes the file, backing the original up once.
/// </summary>
public sealed class SaveEditorViewModel : ObservableObject, IStateHost
{
    private readonly MainViewModel _main;
    private SaveGame? _save;
    private bool _dirty;
    private string? _discardArmedFor;   // the path a second Open would discard unsaved edits for
    private bool _overwriteArmed;       // a second Save overwrites a file that changed on disk since it was opened

    public SaveEditorViewModel(MainViewModel main)
    {
        _main = main;
        for (int i = 0; i < StateFormat.CharacterSlots; i++) Characters.Add(new CharacterViewModel(this, i));
        World = new WorldViewModel(this) { TeleportCheck = main.Maps.CheckTeleport };
        BrowseCommand = new RelayCommand(Browse);
        ScanCommand = new RelayCommand(Scan);
        OpenCommand = new RelayCommand(Open, () => SelectedFile is not null);
        SaveCommand = new RelayCommand(Save, () => _save is not null && _dirty);
        RevertCommand = new RelayCommand(Revert, () => _save is not null && _dirty);
        HealAllCommand = new RelayCommand(() => AllCharacters("Party healed", r => r.FullHeal()), () => CanEdit);
        MaxAllCommand = new RelayCommand(() => AllCharacters("Party maxed out",
            r => r.MaxAttributes() & r.MaxSkills() & r.FullHeal() & r.RefillAmmunition()), () => CanEdit);
        CompareCommand = new RelayCommand(Compare, () => _save is not null && _main.CanEdit);
    }

    public ObservableCollection<CharacterViewModel> Characters { get; } = new();

    private CharacterViewModel? _selectedCharacter;
    public CharacterViewModel? SelectedCharacter { get => _selectedCharacter; set => SetField(ref _selectedCharacter, value); }

    public WorldViewModel World { get; }

    public ObservableCollection<string> Files { get; } = new();

    private string? _selectedFile;
    public string? SelectedFile
    {
        get => _selectedFile;
        set { if (SetField(ref _selectedFile, value)) OpenCommand.RaiseCanExecuteChanged(); }
    }

    public RelayCommand BrowseCommand { get; }
    public RelayCommand ScanCommand { get; }
    public RelayCommand OpenCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand RevertCommand { get; }
    public RelayCommand HealAllCommand { get; }
    public RelayCommand MaxAllCommand { get; }
    public RelayCommand CompareCommand { get; }

    private string _status = "Open a saved game (NAME.SAV in the game folder). Quit to DOS first, or at least do not load the save while editing it.";
    public string Status { get => _status; private set => SetField(ref _status, value); }

    public string LoadedName => _save is null ? "No save open" : _save.FileName + (_dirty ? " (unsaved changes)" : "");

    public bool HasSave => _save is not null;

    // ---- IStateHost ---------------------------------------------------------------------------

    public GameState? State => _save?.State;

    public bool CanEdit => _save is not null;

    public bool SuppressWriteBack => false;

    public void Report(string message) => Status = message;

    public void AfterWrite()
    {
        _discardArmedFor = null;   // a fresh edit needs a fresh confirmation before it can be thrown away
        if (_dirty) return;
        _dirty = true;
        OnPropertyChanged(nameof(LoadedName));
        SaveCommand.RaiseCanExecuteChanged();
        RevertCommand.RaiseCanExecuteChanged();
    }

    // ---- files ----------------------------------------------------------------------------------

    internal void OnGameFolderChanged() => Scan();

    internal void OnAttachStateChanged() => CompareCommand.RaiseCanExecuteChanged();

    private void Browse()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open a Space 1889 saved game",
            Filter = "Space 1889 saves (*.SAV;DEF.S)|*.SAV;DEF.S|All files (*.*)|*.*",
            InitialDirectory = Directory.Exists(_main.GameFolder) ? _main.GameFolder : null,
        };
        if (dialog.ShowDialog() != true) return;
        string path = dialog.FileName;
        string folder = Path.GetDirectoryName(path) ?? "";
        if (Space1889Trainer.Files.GameFolder.IsGameFolder(folder)) _main.GameFolder = folder;   // rescans the list
        // A save kept outside the game folder is still listed, so the selection can show it.
        string? listed = Files.FirstOrDefault(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase));
        if (listed is null) Files.Add(listed = path);
        SelectedFile = listed;
        OpenPath(listed);
    }

    private void Scan()
    {
        Files.Clear();
        if (!Directory.Exists(_main.GameFolder)) return;
        foreach (var f in SaveGame.FindSaves(_main.GameFolder)) Files.Add(f);
        SelectedFile ??= Files.FirstOrDefault();
    }

    private void Open()
    {
        if (SelectedFile is { } path) OpenPath(path);
    }

    /// <summary>
    /// Opens <paramref name="path"/> in place of the current save. Unsaved edits are only discarded on a second request
    /// for the same file (or with <paramref name="discardChanges"/>). Returns false, with the reason in
    /// <see cref="Status"/>, when nothing was opened; the previous save then stays open.
    /// </summary>
    private bool OpenPath(string path, bool discardChanges = false)
    {
        if (_save is not null && _dirty && !discardChanges
            && !string.Equals(_discardArmedFor, path, StringComparison.OrdinalIgnoreCase))
        {
            _discardArmedFor = path;
            Status = $"{_save.FileName} has unsaved changes. Save them, or open {Path.GetFileName(path)} again to discard them.";
            return false;
        }
        var save = SaveGame.Load(path, out string error);
        if (save is null) { Status = error; return false; }
        _discardArmedFor = null;
        _overwriteArmed = false;
        _save = save;
        _dirty = false;
        Status = $"Opened {save.FileName}: {PartySummary(save.State)}.";
        SelectedCharacter ??= Characters[0];
        ReloadEditors();
        return true;
    }

    private void Revert()
    {
        if (_save is null) return;
        string path = _save.Path, name = _save.FileName;
        if (!OpenPath(path, discardChanges: true)) return;   // the reason is in Status; the edited copy stays open
        if (Files.Any(f => string.Equals(f, path, StringComparison.OrdinalIgnoreCase))) SelectedFile = path;
        Status = $"Reverted {name} to the copy on disk.";
    }

    private void Save()
    {
        if (_save is null) return;
        if (!StateFormat.LooksLikeState(_save.Target.Bytes))
        {
            Status = "Refusing to save: the edited block no longer validates (no named character, or the account record is damaged).";
            return;
        }
        if (_save.ChangedOnDisk && !_overwriteArmed)
        {
            _overwriteArmed = true;
            Status = $"{_save.FileName} has changed on disk since it was opened (did the game save over it?). Press Save again to "
                   + "replace it with this copy and your edits, or Revert to load the newer file.";
            return;
        }
        _overwriteArmed = false;
        if (_save.Save(out string error))
        {
            _dirty = false;
            _discardArmedFor = null;
            Status = $"Saved {_save.FileName} (original kept as {Path.GetFileName(_save.BackupPath)}). Load it in the game with GAME → LOAD.";
        }
        else Status = error;
        OnPropertyChanged(nameof(LoadedName));
        SaveCommand.RaiseCanExecuteChanged();
        RevertCommand.RaiseCanExecuteChanged();
    }

    private void AllCharacters(string what, Func<CharacterRecord, bool> action)
    {
        if (_save is null) return;
        for (int i = 0; i < StateFormat.CharacterSlots; i++)
        {
            var r = new CharacterRecord(_save.State, i);
            if (!r.IsEmpty) action(r);
        }
        AfterWrite();
        Status = what + ".";
        ReloadEditors();
    }

    /// <summary>Lists the fields where the open save and the running game differ (clock bytes excluded).</summary>
    private void Compare()
    {
        if (_save is null || _main.State is not { } live) return;
        var a = _save.Target.Bytes;
        var b = live.Snapshot;
        var ranges = new List<string>();
        int start = -1;
        for (int i = 0; i <= a.Length; i++)
        {
            bool differs = i < a.Length && a[i] != b[i] && !(i >= 0x3B2E && i < 0x3B32);
            if (differs && start < 0) start = i;
            if (!differs && start >= 0)
            {
                ranges.Add(start == i - 1 ? $"0x{start:X4}" : $"0x{start:X4}-0x{i - 1:X4}");
                start = -1;
            }
        }
        Status = ranges.Count == 0
            ? $"{_save.FileName} matches the running game byte for byte (ignoring the tick clock)."
            : $"{_save.FileName} differs from the running game in {ranges.Count} range(s): {string.Join(", ", ranges.Take(12))}{(ranges.Count > 12 ? ", …" : "")}.";
    }

    private void ReloadEditors()
    {
        foreach (var c in Characters) c.Reload();
        World.Reload();
        OnPropertyChanged(nameof(LoadedName));
        OnPropertyChanged(nameof(HasSave));
        SaveCommand.RaiseCanExecuteChanged();
        RevertCommand.RaiseCanExecuteChanged();
        HealAllCommand.RaiseCanExecuteChanged();
        MaxAllCommand.RaiseCanExecuteChanged();
        CompareCommand.RaiseCanExecuteChanged();
    }

    private static string PartySummary(GameState state)
    {
        var names = Enumerable.Range(0, StateFormat.CharacterSlots)
            .Select(i => new CharacterRecord(state, i))
            .Where(r => !r.IsEmpty)
            .Select(r => r.Name);
        var world = new WorldRecord(state);
        return $"{string.Join(", ", names)} — day {world.Day}, {world.Describe}";
    }
}
