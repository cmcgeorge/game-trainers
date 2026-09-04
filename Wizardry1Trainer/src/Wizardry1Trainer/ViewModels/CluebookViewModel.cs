using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Wizardry1Trainer.Cluebooks;

namespace Wizardry1Trainer.ViewModels;

/// <summary>
/// Backs the Cluebook tab: generates a self-contained HTML cluebook for Wizardry 1
/// and lets the user save or open it. All data is baked into the trainer's own
/// game-knowledge layer — no game installation or attached process is needed.
/// </summary>
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

    private bool _includeMaps = true;
    public bool IncludeMaps
    {
        get => _includeMaps;
        set => SetField(ref _includeMaps, value);
    }

    private bool _includeSpells = true;
    public bool IncludeSpells
    {
        get => _includeSpells;
        set => SetField(ref _includeSpells, value);
    }

    private bool _includeClasses = true;
    public bool IncludeClasses
    {
        get => _includeClasses;
        set => SetField(ref _includeClasses, value);
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
            IncludeMaps = _includeMaps,
            IncludeSpells = _includeSpells,
            IncludeClasses = _includeClasses,
            IncludeWalkthrough = _includeWalkthrough,
            IncludeStrategy = _includeStrategy,
        };

        var cluebook = Cluebook.Build(options);
        var html = HtmlCluebookWriter.Write(cluebook);

        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var path = Path.Combine(docs, "Wizardry1-cluebook.html");
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
