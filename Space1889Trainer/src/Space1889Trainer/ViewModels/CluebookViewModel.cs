using System.Diagnostics;
using System.IO;
using Space1889Trainer.Cluebooks;

namespace Space1889Trainer.ViewModels;

/// <summary>The Cluebook tab: writes the strategy guide as one self-contained HTML page.</summary>
public sealed class CluebookViewModel : ObservableObject
{
    private readonly MainViewModel _main;
    private string _status = "Press Save to write the cluebook, then Open to view it in your browser.";
    private string? _lastSaved;
    private bool _includeControls = true, _includeWalkthrough = true, _includeSideQuests = true, _includeItems = true, _includeMaps = true;

    public CluebookViewModel(MainViewModel main)
    {
        _main = main;
        SaveCommand = new RelayCommand(Save);
        OpenCommand = new RelayCommand(Open, () => _lastSaved is not null && File.Exists(_lastSaved));
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand OpenCommand { get; }

    public string Status { get => _status; private set => SetField(ref _status, value); }

    public bool IncludeControls { get => _includeControls; set => SetField(ref _includeControls, value); }
    public bool IncludeWalkthrough { get => _includeWalkthrough; set => SetField(ref _includeWalkthrough, value); }
    public bool IncludeSideQuests { get => _includeSideQuests; set => SetField(ref _includeSideQuests, value); }
    public bool IncludeItems { get => _includeItems; set => SetField(ref _includeItems, value); }

    /// <summary>Maps are drawn from the game folder; without one the section explains how to add them.</summary>
    public bool IncludeMaps { get => _includeMaps; set => SetField(ref _includeMaps, value); }

    private void Save()
    {
        var options = new CluebookOptions
        {
            IncludeControls = _includeControls,
            IncludeWalkthrough = _includeWalkthrough,
            IncludeSideQuests = _includeSideQuests,
            IncludeItems = _includeItems,
            IncludeMaps = _includeMaps,
        };
        try
        {
            string html = HtmlCluebookWriter.Write(Cluebook.Build(options, _main.GameFolder));
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string path = Path.Combine(documents, "Space1889-cluebook.html");
            File.WriteAllText(path, html);
            _lastSaved = path;
            Status = $"Saved to {path}" + (Files.GameFolder.IsGameFolder(_main.GameFolder) ? "." : " (without maps: the game folder was not found).");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Status = "Could not save the cluebook: " + ex.Message;
        }
        OpenCommand.RaiseCanExecuteChanged();
    }

    private void Open()
    {
        if (_lastSaved is null || !File.Exists(_lastSaved)) return;
        try
        {
            Process.Start(new ProcessStartInfo(_lastSaved) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Status = $"Could not open {_lastSaved} ({ex.Message}); open it from Explorer instead.";
        }
    }
}
