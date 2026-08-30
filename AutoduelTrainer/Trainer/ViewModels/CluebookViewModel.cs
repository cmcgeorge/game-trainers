using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using AutoduelTrainer.Cluebooks;

namespace AutoduelTrainer.ViewModels;

public sealed class CluebookViewModel : ViewModelBase
{
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _openCommand;
    private string _status = "Press Save to write the cluebook, or Open to view it in your browser.";
    private string? _lastSaved;

    public CluebookViewModel()
    {
        _saveCommand = new RelayCommand(Save);
        _openCommand = new RelayCommand(Open, () => _lastSaved is not null);
    }

    public ICommand SaveCommand => _saveCommand;
    public ICommand OpenCommand => _openCommand;
    public string Status { get => _status; private set => Set(ref _status, value); }
    public bool IncludeMaps { get => _includeMaps; set => Set(ref _includeMaps, value); }
    public bool IncludeWeapons { get => _includeWeapons; set => Set(ref _includeWeapons, value); }
    public bool IncludeVehicles { get => _includeVehicles; set => Set(ref _includeVehicles, value); }
    public bool IncludeWalkthrough { get => _includeWalkthrough; set => Set(ref _includeWalkthrough, value); }
    public bool IncludeStrategy { get => _includeStrategy; set => Set(ref _includeStrategy, value); }

    private bool _includeMaps = true;
    private bool _includeWeapons = true;
    private bool _includeVehicles = true;
    private bool _includeWalkthrough = true;
    private bool _includeStrategy = true;

    private void Save()
    {
        var html = HtmlCluebookWriter.Write(Cluebook.Build(new CluebookOptions
        {
            IncludeMaps = IncludeMaps, IncludeWeapons = IncludeWeapons, IncludeVehicles = IncludeVehicles,
            IncludeWalkthrough = IncludeWalkthrough, IncludeStrategy = IncludeStrategy,
        }));
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Autoduel-cluebook.html");
        File.WriteAllText(path, html);
        _lastSaved = path;
        Status = $"Saved to {path}";
        _openCommand.RaiseCanExecuteChanged();
    }

    private void Open()
    {
        if (_lastSaved is not null && File.Exists(_lastSaved))
            Process.Start(new ProcessStartInfo(_lastSaved) { UseShellExecute = true });
    }
}
