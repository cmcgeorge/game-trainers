using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using Roadwar2000Trainer.Cluebooks;

namespace Roadwar2000Trainer.ViewModels;

public sealed class CluebookViewModel : ObservableObject
{
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _openCommand;
    private string _status = "Press Save to write the cluebook, or Open to view it in your browser.";
    private string? _lastSaved;
    private bool _includeVehicles = true;
    private bool _includeCities = true;
    private bool _includeMaps = true;
    private bool _includeWalkthrough = true;
    private bool _includeStrategy = true;

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

    public bool IncludeVehicles
    {
        get => _includeVehicles;
        set => SetField(ref _includeVehicles, value);
    }

    public bool IncludeCities
    {
        get => _includeCities;
        set => SetField(ref _includeCities, value);
    }

    public bool IncludeMaps
    {
        get => _includeMaps;
        set => SetField(ref _includeMaps, value);
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
            IncludeVehicles = _includeVehicles,
            IncludeCities = _includeCities,
            IncludeMaps = _includeMaps,
            IncludeWalkthrough = _includeWalkthrough,
            IncludeStrategy = _includeStrategy,
        };

        string html = HtmlCluebookWriter.Write(Cluebook.Build(options));
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string path = Path.Combine(documents, "Roadwar2000-cluebook.html");
        File.WriteAllText(path, html);
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
