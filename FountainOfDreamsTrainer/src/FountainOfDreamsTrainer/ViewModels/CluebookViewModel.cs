using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using FountainOfDreamsTrainer.Cluebooks;

namespace FountainOfDreamsTrainer.ViewModels;

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
    public bool IncludeMaps { get => _includeMaps; set => SetField(ref _includeMaps, value); }

    private bool _includeProfessions = true;
    public bool IncludeProfessions { get => _includeProfessions; set => SetField(ref _includeProfessions, value); }

    private bool _includeWalkthrough = true;
    public bool IncludeWalkthrough { get => _includeWalkthrough; set => SetField(ref _includeWalkthrough, value); }

    private bool _includeStrategy = true;
    public bool IncludeStrategy { get => _includeStrategy; set => SetField(ref _includeStrategy, value); }

    private void Save()
    {
        var cluebook = Cluebook.Build(new CluebookOptions
        {
            IncludeMaps = _includeMaps,
            IncludeProfessions = _includeProfessions,
            IncludeWalkthrough = _includeWalkthrough,
            IncludeStrategy = _includeStrategy,
        });
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FountainOfDreams-cluebook.html");
        File.WriteAllText(path, HtmlCluebookWriter.Write(cluebook));
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
