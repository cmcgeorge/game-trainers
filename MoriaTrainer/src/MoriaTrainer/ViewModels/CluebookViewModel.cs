using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using MoriaTrainer.Cluebooks;

namespace MoriaTrainer.ViewModels;

public sealed class CluebookViewModel : ObservableObject
{
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _openCommand;
    private string _status = "Press Save to write the cluebook, or Open to view it in your browser.";
    private string? _lastSaved;
    private bool _includeLevels = true;
    private bool _includeRacesAndClasses = true;
    private bool _includeSpells = true;
    private bool _includeItems = true;
    private bool _includeBestiary = true;
    private bool _includeWalkthrough = true;
    private bool _includeStrategy = true;

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

    public bool IncludeLevels
    {
        get => _includeLevels;
        set => SetField(ref _includeLevels, value);
    }

    public bool IncludeRacesAndClasses
    {
        get => _includeRacesAndClasses;
        set => SetField(ref _includeRacesAndClasses, value);
    }

    public bool IncludeSpells
    {
        get => _includeSpells;
        set => SetField(ref _includeSpells, value);
    }

    public bool IncludeItems
    {
        get => _includeItems;
        set => SetField(ref _includeItems, value);
    }

    public bool IncludeBestiary
    {
        get => _includeBestiary;
        set => SetField(ref _includeBestiary, value);
    }

    public bool IncludeWalkthrough
    {
        get => _includeWalkthrough;
        set => SetField(ref _includeWalkthrough, value);
    }

    public bool IncludeStrategy
    {
        get => _includeStrategy;
        set => SetField(ref _includeStrategy, value);
    }

    private void Save()
    {
        var options = new CluebookOptions
        {
            IncludeLevels = _includeLevels,
            IncludeRacesAndClasses = _includeRacesAndClasses,
            IncludeSpells = _includeSpells,
            IncludeItems = _includeItems,
            IncludeBestiary = _includeBestiary,
            IncludeWalkthrough = _includeWalkthrough,
            IncludeStrategy = _includeStrategy,
        };

        var html = HtmlCluebookWriter.Write(Cluebook.Build(options));
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var path = Path.Combine(docs, "Moria-cluebook.html");
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
