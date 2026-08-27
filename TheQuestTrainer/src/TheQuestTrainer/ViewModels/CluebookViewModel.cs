using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using TheQuestTrainer.Adventures;
using TheQuestTrainer.Cluebooks;

namespace TheQuestTrainer.ViewModels;

/// <summary>One adventure in the list, with the counts filled in once it has been read.</summary>
public sealed class AdventureRowViewModel(AdventureSource source) : ObservableObject
{
    private string _detail = "Not read yet.";
    private bool _isSelected = true;

    /// <summary>Where the world came from.</summary>
    public AdventureSource Source { get; } = source;

    /// <summary>"Freymore — the base game".</summary>
    public string Display => Source.Display;

    /// <summary>The pak, for the second column.</summary>
    public string Pak => Path.GetFileName(Source.PakPath);

    /// <summary>Whether a cluebook should be written for this one.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>What was found in it, or what went wrong.</summary>
    public string Detail
    {
        get => _detail;
        set => SetField(ref _detail, value);
    }
}

/// <summary>
/// The Cluebook tab: find the adventures a Quest installation holds, and write a strategy guide for
/// each.
///
/// <b>This half of the trainer does not touch the running game.</b> It reads the adventure data out
/// of the paks on disk, so it works whether or not anything is attached — the folder is filled in
/// from the attached process when there is one, and typed or browsed for when there is not. That is
/// deliberate: a cluebook is something you read before you play.
/// </summary>
public sealed class CluebookViewModel : ObservableObject
{
    private readonly RelayCommand _findCommand;
    private readonly RelayCommand _writeCommand;

    private string _gameFolder = "";
    private string _outputFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "The Quest cluebooks");
    private string _status = "Point this at the folder holding TheQuest.exe, then press Find.";
    private bool _busy;
    private bool _includeItems = true;
    private bool _includeConversations = true;
    private bool _includeReference = true;
    private bool _includeEmptyMaps;
    private string? _lastWritten;

    public CluebookViewModel()
    {
        _findCommand = new RelayCommand(_ => Find(), _ => !Busy && GameFolder.Length > 0);
        _writeCommand = new RelayCommand(_ => Write(), _ => !Busy && Adventures.Any(a => a.IsSelected));
        OpenOutputCommand = new RelayCommand(_ => OpenOutput(), _ => _lastWritten is not null);
    }

    /// <summary>The adventures found in the folder.</summary>
    public ObservableCollection<AdventureRowViewModel> Adventures { get; } = [];

    /// <summary>The folder holding <c>TheQuest.exe</c>, <c>data.pak</c> and <c>expansions\</c>.</summary>
    public string GameFolder
    {
        get => _gameFolder;
        set
        {
            if (!SetField(ref _gameFolder, value)) return;
            _findCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Where the cluebooks are written.</summary>
    public string OutputFolder
    {
        get => _outputFolder;
        set => SetField(ref _outputFolder, value);
    }

    /// <summary>What just happened.</summary>
    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    /// <summary>Whether a read or a write is under way.</summary>
    public bool Busy
    {
        get => _busy;
        private set
        {
            if (!SetField(ref _busy, value)) return;
            OnPropertyChanged(nameof(IsIdle));
            _findCommand.RaiseCanExecuteChanged();
            _writeCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>The inverse of <see cref="Busy"/>, for binding <c>IsEnabled</c>.</summary>
    public bool IsIdle => !Busy;

    /// <summary>Include the whole item catalogue. Freymore's runs to 893 entries.</summary>
    public bool IncludeItems { get => _includeItems; set => SetField(ref _includeItems, value); }

    /// <summary>Include every conversation in full. This is most of the document.</summary>
    public bool IncludeConversations { get => _includeConversations; set => SetField(ref _includeConversations, value); }

    /// <summary>Include the bestiary, the spell list and the rules.</summary>
    public bool IncludeReference { get => _includeReference; set => SetField(ref _includeReference, value); }

    /// <summary>Give a chapter even to maps with nothing placed on them.</summary>
    public bool IncludeEmptyMaps { get => _includeEmptyMaps; set => SetField(ref _includeEmptyMaps, value); }

    public ICommand FindCommand => _findCommand;
    public ICommand WriteCommand => _writeCommand;

    /// <summary>Opens the output folder in Explorer.</summary>
    public ICommand OpenOutputCommand { get; }

    /// <summary>
    /// Fills the folder in from the attached game, unless the user has already typed one.
    ///
    /// The attached process <i>is</i> the game, so its own module path is the install — the same
    /// route <see cref="MainViewModel"/> uses to find the world map picture. Typing over it wins,
    /// because a player may want a cluebook for an install other than the one they are running.
    /// </summary>
    public void SuggestGameFolder(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || GameFolder.Length > 0) return;
        GameFolder = folder;
        Find();
    }

    /// <summary>Lists the adventures in <see cref="GameFolder"/>.</summary>
    public void Find()
    {
        Busy = true;
        try
        {
            Adventures.Clear();
            var found = AdventureCatalog.Find(GameFolder, out string detail);
            foreach (var source in found) Adventures.Add(new AdventureRowViewModel(source));
            Status = detail;
        }
        finally
        {
            Busy = false;
            _writeCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Writes a cluebook for every ticked adventure.</summary>
    public void Write()
    {
        Busy = true;
        try
        {
            Directory.CreateDirectory(OutputFolder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Status = $"Could not create {OutputFolder}: {e.Message}";
            Busy = false;
            return;
        }

        var options = new CluebookOptions
        {
            IncludeItems = IncludeItems,
            IncludeConversations = IncludeConversations,
            IncludeReference = IncludeReference,
            IncludeEmptyMaps = IncludeEmptyMaps,
        };

        int written = 0;
        try
        {
            foreach (var row in Adventures.Where(a => a.IsSelected))
            {
                var adventure = AdventureCatalog.Load(row.Source, out string why);
                if (adventure is null)
                {
                    row.Detail = $"Could not read: {why}";
                    continue;
                }

                var cluebook = Cluebook.Build(adventure, options);
                string stem = Path.Combine(OutputFolder, Sanitise(adventure.Name));

                try
                {
                    // The HTML declares its own charset; the plain text has nowhere to say so, and a
                    // world's prose is full of dashes and accents, so it gets a byte-order mark.
                    File.WriteAllText(stem + ".html", HtmlCluebookWriter.Write(cluebook), Utf8);
                    File.WriteAllText(stem + ".txt", TextCluebookWriter.Write(cluebook), Utf8WithMark);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    row.Detail = $"Could not write {stem}: {e.Message}";
                    continue;
                }

                _lastWritten = stem + ".html";
                written++;
                row.Detail =
                    $"{adventure.Maps.Count} maps, {adventure.Quests.Count} quests, " +
                    $"{cluebook.Speakers.Count} people who talk, {adventure.Items.Count} item types" +
                    (adventure.Warnings.Count > 0 ? $" — {adventure.Warnings.Count} records not understood" : "");
            }

            Status = written == 0
                ? "Nothing was written."
                : $"Wrote {written} cluebook{(written == 1 ? "" : "s")} to {OutputFolder}.";
        }
        finally
        {
            Busy = false;
            ((RelayCommand)OpenOutputCommand).RaiseCanExecuteChanged();
        }
    }

    private void OpenOutput()
    {
        try
        {
            Process.Start(new ProcessStartInfo(OutputFolder) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Status = $"Could not open {OutputFolder}: {e.Message}";
        }
    }

    /// <summary>UTF-8 without a byte-order mark, for the HTML.</summary>
    private static readonly System.Text.UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>UTF-8 with a byte-order mark, for the plain text.</summary>
    private static readonly System.Text.UTF8Encoding Utf8WithMark = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>A world name reduced to something a file system will take.</summary>
    public static string Sanitise(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        foreach (char bad in Path.GetInvalidFileNameChars()) name = name.Replace(bad, '_');
        name = name.Trim();
        return name.Length == 0 ? "adventure" : name;
    }
}
