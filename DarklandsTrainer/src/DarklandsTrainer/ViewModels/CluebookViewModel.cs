using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using DarklandsTrainer.Cluebooks;

namespace DarklandsTrainer.ViewModels;

public sealed class CluebookViewModel : ObservableObject
{
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _openCommand;
    private string _status = "Press Save to write the cluebook, or Open to view it in your browser.";
    private string? _lastSaved;
    private bool _includeMaps = true;
    private bool _includeAttributes = true;
    private bool _includeSaintsAndPotions = true;
    private bool _includeWalkthrough = true;
    private bool _includeStrategy = true;

    public CluebookViewModel()
    {
        _saveCommand = new RelayCommand(_ => Save(), _ => true);
        _openCommand = new RelayCommand(_ => Open(), _ => _lastSaved != null);
    }

    public ICommand SaveCommand => _saveCommand;
    public ICommand OpenCommand => _openCommand;
    public string Status { get => _status; private set => SetField(ref _status, value); }
    public bool IncludeMaps { get => _includeMaps; set => SetField(ref _includeMaps, value); }
    public bool IncludeAttributes { get => _includeAttributes; set => SetField(ref _includeAttributes, value); }
    public bool IncludeSaintsAndPotions { get => _includeSaintsAndPotions; set => SetField(ref _includeSaintsAndPotions, value); }
    public bool IncludeWalkthrough { get => _includeWalkthrough; set => SetField(ref _includeWalkthrough, value); }
    public bool IncludeStrategy { get => _includeStrategy; set => SetField(ref _includeStrategy, value); }

    private void Save()
    {
        var options = new CluebookOptions
        {
            IncludeMaps = IncludeMaps,
            IncludeAttributes = IncludeAttributes,
            IncludeSaintsAndPotions = IncludeSaintsAndPotions,
            IncludeWalkthrough = IncludeWalkthrough,
            IncludeStrategy = IncludeStrategy,
        };
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Darklands-cluebook.html");
        File.WriteAllText(path, HtmlCluebookWriter.Write(Cluebook.Build(options)));
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
