using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using LegendOfGrimrock1Trainer.Cluebooks;

namespace LegendOfGrimrock1Trainer.ViewModels;

public sealed class CluebookViewModel : ObservableObject
{
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _openCommand;
    private string _status = "Choose sections and save the cluebook, or open the last saved copy.";
    private string? _lastSaved;

    public CluebookViewModel()
    {
        _saveCommand = new RelayCommand(_ => Save(), _ => true);
        _openCommand = new RelayCommand(_ => Open(), _ => _lastSaved is not null);
    }

    public ICommand SaveCommand => _saveCommand;
    public ICommand OpenCommand => _openCommand;

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public bool IncludeOverview { get; set; } = true;
    public bool IncludeDungeon { get; set; } = true;
    public bool IncludeCharacters { get; set; } = true;
    public bool IncludeSpells { get; set; } = true;
    public bool IncludeSkills { get; set; } = true;
    public bool IncludeEquipment { get; set; } = true;
    public bool IncludeBestiary { get; set; } = true;
    public bool IncludeWalkthrough { get; set; } = true;
    public bool IncludeStrategy { get; set; } = true;

    private void Save()
    {
        var options = new CluebookOptions
        {
            IncludeOverview = IncludeOverview,
            IncludeDungeon = IncludeDungeon,
            IncludeCharacters = IncludeCharacters,
            IncludeSpells = IncludeSpells,
            IncludeSkills = IncludeSkills,
            IncludeEquipment = IncludeEquipment,
            IncludeBestiary = IncludeBestiary,
            IncludeWalkthrough = IncludeWalkthrough,
            IncludeStrategy = IncludeStrategy,
        };
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Legend-of-Grimrock-cluebook.html");
        try { File.WriteAllText(path, HtmlCluebookWriter.Write(Cluebook.Build(options))); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Status = $"Could not save: {e.Message}"; return; }
        _lastSaved = path;
        Status = $"Saved to {path}";
        _openCommand.RaiseCanExecuteChanged();
    }

    private void Open()
    {
        if (_lastSaved is null || !File.Exists(_lastSaved)) return;
        Process.Start(new ProcessStartInfo(_lastSaved) { UseShellExecute = true });
    }
}
