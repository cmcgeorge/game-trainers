using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using WastelandRemasteredTrainer.Cluebooks;

namespace WastelandRemasteredTrainer.ViewModels;

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

    public string Status { get => _status; private set => SetField(ref _status, value); }

    public bool IncludeMaps { get => _includeMaps; set => SetField(ref _includeMaps, value); }
    public bool IncludeAttributes { get => _includeAttributes; set => SetField(ref _includeAttributes, value); }
    public bool IncludeSkills { get => _includeSkills; set => SetField(ref _includeSkills, value); }
    public bool IncludeItems { get => _includeItems; set => SetField(ref _includeItems, value); }
    public bool IncludeWalkthrough { get => _includeWalkthrough; set => SetField(ref _includeWalkthrough, value); }
    public bool IncludeStrategy { get => _includeStrategy; set => SetField(ref _includeStrategy, value); }

    private bool _includeMaps = true;
    private bool _includeAttributes = true;
    private bool _includeSkills = true;
    private bool _includeItems = true;
    private bool _includeWalkthrough = true;
    private bool _includeStrategy = true;

    private void Save()
    {
        var html = HtmlCluebookWriter.Write(Cluebook.Build(new CluebookOptions
        {
            IncludeMaps = IncludeMaps,
            IncludeAttributes = IncludeAttributes,
            IncludeSkills = IncludeSkills,
            IncludeItems = IncludeItems,
            IncludeWalkthrough = IncludeWalkthrough,
            IncludeStrategy = IncludeStrategy,
        }));
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Wasteland-Remastered-cluebook.html");
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
