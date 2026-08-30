using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using PoolOfRadianceTrainer.Cluebooks;
using PoolOfRadianceTrainer.Mvvm;

namespace PoolOfRadianceTrainer.ViewModels;

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
        private set => SetProperty(ref _status, value);
    }

    private bool _includeMaps = true;
    public bool IncludeMaps { get => _includeMaps; set => SetProperty(ref _includeMaps, value); }

    private bool _includeSpells = true;
    public bool IncludeSpells { get => _includeSpells; set => SetProperty(ref _includeSpells, value); }

    private bool _includeClasses = true;
    public bool IncludeClasses { get => _includeClasses; set => SetProperty(ref _includeClasses, value); }

    private bool _includeWalkthrough = true;
    public bool IncludeWalkthrough { get => _includeWalkthrough; set => SetProperty(ref _includeWalkthrough, value); }

    private bool _includeStrategy = true;
    public bool IncludeStrategy { get => _includeStrategy; set => SetProperty(ref _includeStrategy, value); }

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
        var html = HtmlCluebookWriter.Write(Cluebook.Build(options));
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PoolOfRadiance-cluebook.html");
        File.WriteAllText(path, html);
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
