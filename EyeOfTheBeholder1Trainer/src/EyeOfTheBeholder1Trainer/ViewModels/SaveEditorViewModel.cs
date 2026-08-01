using System.Collections.ObjectModel;
using System.Windows.Input;
using EyeOfTheBeholder1Trainer.Game;
using Microsoft.Win32;

namespace EyeOfTheBeholder1Trainer.ViewModels;

/// <summary>
/// View-model for the offline save-file editor tab. Loads <c>EOBDATA.SAV</c>, presents each
/// character slot for editing, and writes the file back — taking a one-shot <c>.bak</c> first.
/// No live process is needed.
/// </summary>
public sealed class SaveEditorViewModel : ObservableObject
{
    private SaveFile? _save;
    private string? _loadedPath;

    public ObservableCollection<CharacterViewModel> Characters { get; } = new();

    private CharacterViewModel? _selectedCharacter;
    public CharacterViewModel? SelectedCharacter
    {
        get => _selectedCharacter;
        set => SetField(ref _selectedCharacter, value);
    }

    private string _status = "Load an EOBDATA.SAV file to begin.";
    public string Status { get => _status; set => SetField(ref _status, value); }

    public bool IsLoaded => _save != null;

    public ICommand LoadCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand MaxAttributesCommand { get; }
    public ICommand MaxHpCommand { get; }
    public ICommand MaxEverythingCommand { get; }
    public ICommand HealAllCommand { get; }

    public SaveEditorViewModel()
    {
        LoadCommand = new RelayCommand(_ => LoadFile());
        SaveCommand = new RelayCommand(_ => SaveCurrentFile(), _ => IsLoaded);
        SaveAsCommand = new RelayCommand(_ => SaveFileAs(), _ => IsLoaded);
        MaxAttributesCommand = new RelayCommand(_ => ForEach(c => c.MaxAttributes()), _ => Characters.Count > 0);
        MaxHpCommand = new RelayCommand(_ => ForEach(c => c.MaxHp()), _ => Characters.Count > 0);
        MaxEverythingCommand = new RelayCommand(_ => ForEach(c => c.MaxEverything()), _ => Characters.Count > 0);
        HealAllCommand = new RelayCommand(_ => ForEach(c => c.FullHeal()), _ => Characters.Count > 0);
    }

    private void LoadFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "EOB1 Save File|EOBDATA.SAV|All Files|*.*",
            FileName = "EOBDATA.SAV",
            Title = "Open Eye of the Beholder Save File"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            _save = SaveFile.Load(dlg.FileName);
            if (!_save.IsValid)
            {
                Status = "File is too small to hold a valid party.";
                _save = null;
                return;
            }
            _loadedPath = dlg.FileName;
            Characters.Clear();
            foreach (var (idx, rec) in _save.GetOccupiedCharacters())
                Characters.Add(new CharacterViewModel(rec, idx));
            SelectedCharacter = Characters.FirstOrDefault();
            Status = Characters.Count == 0
                ? $"Loaded {dlg.FileName} — no active characters found."
                : $"Loaded {dlg.FileName} — {Characters.Count} character(s).";
            OnPropertyChanged(nameof(IsLoaded));
            RaiseCommands();
        }
        catch (Exception ex)
        {
            Status = "Load failed: " + ex.Message;
        }
    }

    /// <summary>Writes all edited character records back into the SaveFile buffer before persisting.</summary>
    private void SyncToSave()
    {
        if (_save == null) return;
        foreach (var cvm in Characters)
            _save.SetCharacter(cvm.Slot, cvm.Record);
    }

    private void SaveCurrentFile()
    {
        if (_save == null || _loadedPath == null) return;
        try
        {
            SyncToSave();
            _save.Backup(_loadedPath);
            _save.Save(_loadedPath);
            Status = $"Saved to {_loadedPath} (backup at .bak).";
        }
        catch (Exception ex)
        {
            Status = "Save failed: " + ex.Message;
        }
    }

    private void SaveFileAs()
    {
        if (_save == null) return;
        var dlg = new SaveFileDialog
        {
            Filter = "EOB1 Save File|EOBDATA.SAV|All Files|*.*",
            FileName = "EOBDATA.SAV",
            Title = "Save Eye of the Beholder Save File"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            SyncToSave();
            _save.Save(dlg.FileName);
            _loadedPath = dlg.FileName;
            Status = $"Saved to {dlg.FileName}.";
            OnPropertyChanged(nameof(IsLoaded));
        }
        catch (Exception ex)
        {
            Status = "Save failed: " + ex.Message;
        }
    }

    private void ForEach(Action<CharacterViewModel> action)
    {
        foreach (var c in Characters) action(c);
        Status = "Applied to all characters.";
    }

    private void RaiseCommands()
    {
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (SaveAsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxAttributesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxHpCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (MaxEverythingCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (HealAllCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
