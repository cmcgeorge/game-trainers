using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using AlternateRealityTrainer.Cluebooks;

namespace AlternateRealityTrainer.ViewModels;

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

    private bool _includeCityMap = true;
    public bool IncludeCityMap
    {
        get => _includeCityMap;
        set => SetField(ref _includeCityMap, value);
    }

    private bool _includeAttributes = true;
    public bool IncludeAttributes
    {
        get => _includeAttributes;
        set => SetField(ref _includeAttributes, value);
    }

    private bool _includePotions = true;
    public bool IncludePotions
    {
        get => _includePotions;
        set => SetField(ref _includePotions, value);
    }

    private bool _includeSurvival = true;
    public bool IncludeSurvival
    {
        get => _includeSurvival;
        set => SetField(ref _includeSurvival, value);
    }

    private bool _includeStrategy = true;
    public bool IncludeStrategy
    {
        get => _includeStrategy;
        set => SetField(ref _includeStrategy, value);
    }

    private void Save()
    {
        var cluebook = Cluebook.Build(new CluebookOptions
        {
            IncludeCityMap = _includeCityMap,
            IncludeAttributes = _includeAttributes,
            IncludePotions = _includePotions,
            IncludeSurvival = _includeSurvival,
            IncludeStrategy = _includeStrategy,
        });

        string html = HtmlCluebookWriter.Write(cluebook);
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        string path = Path.Combine(documents, "AlternateRealityCity-cluebook.html");
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
