using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using DragonWarsTrainer.Cluebooks;

namespace DragonWarsTrainer.ViewModels;

public sealed class CluebookViewModel : ObservableObject
{
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _openCommand;
    private string _status = "Press Save to write the cluebook, or Open to view it in your browser.";
    private string? _lastSaved;

    public CluebookViewModel()
    {
        _saveCommand = new RelayCommand(_ => Save(), _ => true);
        _openCommand = new RelayCommand(_ => Open(), _ => _lastSaved != null);
    }

    public ICommand SaveCommand => _saveCommand;
    public ICommand OpenCommand => _openCommand;

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    private bool _includeAreas = true;
    public bool IncludeAreas
    {
        get => _includeAreas;
        set => SetField(ref _includeAreas, value);
    }

    private bool _includeSpells = true;
    public bool IncludeSpells
    {
        get => _includeSpells;
        set => SetField(ref _includeSpells, value);
    }

    private bool _includeSkills = true;
    public bool IncludeSkills
    {
        get => _includeSkills;
        set => SetField(ref _includeSkills, value);
    }

    private bool _includeWalkthrough = true;
    public bool IncludeWalkthrough
    {
        get => _includeWalkthrough;
        set => SetField(ref _includeWalkthrough, value);
    }

    private bool _includeStrategy = true;
    public bool IncludeStrategy
    {
        get => _includeStrategy;
        set => SetField(ref _includeStrategy, value);
    }

    private void Save()
    {
        var options = new CluebookOptions
        {
            IncludeAreas = _includeAreas,
            IncludeSpells = _includeSpells,
            IncludeSkills = _includeSkills,
            IncludeWalkthrough = _includeWalkthrough,
            IncludeStrategy = _includeStrategy,
        };
        var html = HtmlCluebookWriter.Write(Cluebook.Build(options));
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var path = Path.Combine(docs, "DragonWars-cluebook.html");
        try { File.WriteAllText(path, html); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { Status = $"Could not save: {e.Message}"; return; }
        _lastSaved = path;
        Status = $"Saved to {path}";
        _openCommand.RaiseCanExecuteChanged();
    }

    private void Open()
    {
        if (_lastSaved == null || !File.Exists(_lastSaved)) return;
        Process.Start(new ProcessStartInfo(_lastSaved) { UseShellExecute = true });
    }
}
