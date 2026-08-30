using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using MinesOfTitanTrainer.Cluebooks;

namespace MinesOfTitanTrainer.ViewModels;

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

    public bool IncludeMaps { get => _includeMaps; set => SetField(ref _includeMaps, value); }
    private bool _includeMaps = true;
    public bool IncludeItems { get => _includeItems; set => SetField(ref _includeItems, value); }
    private bool _includeItems = true;
    public bool IncludeWalkthrough { get => _includeWalkthrough; set => SetField(ref _includeWalkthrough, value); }
    private bool _includeWalkthrough = true;
    public bool IncludeStrategy { get => _includeStrategy; set => SetField(ref _includeStrategy, value); }
    private bool _includeStrategy = true;

    private void Save()
    {
        var cluebook = Cluebook.Build(new CluebookOptions
        {
            IncludeMaps = _includeMaps,
            IncludeItems = _includeItems,
            IncludeWalkthrough = _includeWalkthrough,
            IncludeStrategy = _includeStrategy,
        });
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MinesOfTitan-cluebook.html");
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
