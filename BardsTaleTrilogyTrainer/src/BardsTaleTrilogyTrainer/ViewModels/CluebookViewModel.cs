using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using BardsTaleTrilogyTrainer.Cluebooks;

namespace BardsTaleTrilogyTrainer.ViewModels;

public sealed class CluebookViewModel : ObservableObject
{
    private readonly RelayCommand _saveCommand;
    private readonly RelayCommand _openCommand;
    private string _status = "Press Save to write the cluebook, or Open to view it in your browser.";
    private string? _lastSaved;
    private bool _includeSpells = true;
    private bool _includeClasses = true;
    private bool _includeItems = true;
    private bool _includeWalkthrough = true;
    private bool _includeStrategy = true;

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

    public bool IncludeSpells { get => _includeSpells; set => SetField(ref _includeSpells, value); }
    public bool IncludeClasses { get => _includeClasses; set => SetField(ref _includeClasses, value); }
    public bool IncludeItems { get => _includeItems; set => SetField(ref _includeItems, value); }
    public bool IncludeWalkthrough { get => _includeWalkthrough; set => SetField(ref _includeWalkthrough, value); }
    public bool IncludeStrategy { get => _includeStrategy; set => SetField(ref _includeStrategy, value); }

    private void Save()
    {
        var cluebook = Cluebook.Build(new CluebookOptions
        {
            IncludeSpells = _includeSpells,
            IncludeClasses = _includeClasses,
            IncludeItems = _includeItems,
            IncludeWalkthrough = _includeWalkthrough,
            IncludeStrategy = _includeStrategy,
        });

        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Bards-Tale-Trilogy-cluebook.html");
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
