using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using EyeOfTheBeholder1Trainer.Cluebooks;

namespace EyeOfTheBeholder1Trainer.ViewModels;

public sealed class CluebookViewModel : ObservableObject
{
    private readonly RelayCommand _openCommand;
    private string _status = "Press Save to write the cluebook, or Open to view it in your browser.";
    private string? _lastSaved;

    public CluebookViewModel()
    {
        SaveCommand = new RelayCommand(_ => Save());
        _openCommand = new RelayCommand(_ => Open(), _ => _lastSaved != null);
    }

    public ICommand SaveCommand { get; }
    public ICommand OpenCommand => _openCommand;
    public string Status { get => _status; private set => SetField(ref _status, value); }

    private bool _includeMaps = true;
    public bool IncludeMaps { get => _includeMaps; set => SetField(ref _includeMaps, value); }
    private bool _includeSpells = true;
    public bool IncludeSpells { get => _includeSpells; set => SetField(ref _includeSpells, value); }
    private bool _includeClasses = true;
    public bool IncludeClasses { get => _includeClasses; set => SetField(ref _includeClasses, value); }
    private bool _includeWalkthrough = true;
    public bool IncludeWalkthrough { get => _includeWalkthrough; set => SetField(ref _includeWalkthrough, value); }
    private bool _includeStrategy = true;
    public bool IncludeStrategy { get => _includeStrategy; set => SetField(ref _includeStrategy, value); }

    private void Save()
    {
        var cluebook = Cluebook.Build(new CluebookOptions
        {
            IncludeMaps = IncludeMaps,
            IncludeSpells = IncludeSpells,
            IncludeClasses = IncludeClasses,
            IncludeWalkthrough = IncludeWalkthrough,
            IncludeStrategy = IncludeStrategy,
        });
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EyeOfTheBeholder1-cluebook.html");
        File.WriteAllText(path, HtmlCluebookWriter.Write(cluebook));
        _lastSaved = path;
        Status = $"Saved to {path}";
        _openCommand.RaiseCanExecuteChanged();
    }

    private void Open()
    {
        if (_lastSaved != null && File.Exists(_lastSaved))
            Process.Start(new ProcessStartInfo(_lastSaved) { UseShellExecute = true });
    }
}
