using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using KnightsOfLegendTrainer.Cluebooks;

namespace KnightsOfLegendTrainer.ViewModels;

public sealed class CluebookViewModel : ObservableObject
{
    private readonly RelayCommand _openCommand;
    private string? _lastSaved;
    private string _status = "Press Save to write the cluebook, or Open to view it in your browser.";
    private bool _includeMaps = true;
    private bool _includeReferences = true;
    private bool _includeQuests = true;
    private bool _includeWalkthrough = true;
    private bool _includeStrategy = true;

    public CluebookViewModel()
    {
        SaveCommand = new RelayCommand(_ => Save());
        _openCommand = new RelayCommand(_ => Open(), _ => _lastSaved != null);
    }

    public ICommand SaveCommand { get; }
    public ICommand OpenCommand => _openCommand;

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public bool IncludeMaps { get => _includeMaps; set => SetField(ref _includeMaps, value); }
    public bool IncludeReferences { get => _includeReferences; set => SetField(ref _includeReferences, value); }
    public bool IncludeQuests { get => _includeQuests; set => SetField(ref _includeQuests, value); }
    public bool IncludeWalkthrough { get => _includeWalkthrough; set => SetField(ref _includeWalkthrough, value); }
    public bool IncludeStrategy { get => _includeStrategy; set => SetField(ref _includeStrategy, value); }

    private void Save()
    {
        var cluebook = Cluebook.Build(new CluebookOptions
        {
            IncludeMaps = IncludeMaps,
            IncludeReferences = IncludeReferences,
            IncludeQuests = IncludeQuests,
            IncludeWalkthrough = IncludeWalkthrough,
            IncludeStrategy = IncludeStrategy,
        });
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "KnightsOfLegend-cluebook.html");
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
