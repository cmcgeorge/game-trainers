using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Input;
using MightAndMagic1Trainer.Cluebooks;

namespace MightAndMagic1Trainer.ViewModels;

/// <summary>
/// The Cluebook tab: write a strategy guide for Might &amp; Magic 1, as one self-contained HTML page
/// and one plain-text file.
///
/// <para><b>This half of the trainer does not touch the running game.</b> Everything it needs is
/// either bundled — the 55 wall layouts, the item and monster tables, the spells, the walkthrough —
/// or read off disk, so the tab works with nothing attached and nothing installed. That is
/// deliberate: a cluebook is something you read before you play.</para>
///
/// <para>Pointing it at a game folder adds the two things that cannot be shipped: the exact maze
/// bytes, and the game's own words. The words are the whole reason the folder box is there — every
/// sign, riddle, offer and trap message lives in the 55 <c>.ovr</c> overlays of an installation, and
/// this reads them out of the player's own copy rather than carrying them.</para>
/// </summary>
public sealed class CluebookViewModel : ObservableObject
{
    private readonly RelayCommand _inspectCommand;
    private readonly RelayCommand _writeCommand;
    private readonly RelayCommand _openOutputCommand;

    private CluebookSources _sources = CluebookSources.Bundled();
    private string _gameFolder = "";
    private string _outputFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Might and Magic 1 cluebook");
    private string _status =
        "Ready. Press Write for a cluebook from the bundled data, or point this at your game folder first " +
        "to have every location's own text read out of your copy.";
    private string _found = "No game folder — the bundled reference will be used.";
    private bool _busy;
    private string? _lastWritten;

    private bool _includePlans = true;
    private bool _includeEventText = true;
    private bool _includeWalkthrough = true;
    private bool _includeRules = true;
    private bool _includeSpells = true;
    private bool _includeItems = true;
    private bool _includeBestiary = true;

    public CluebookViewModel()
    {
        _inspectCommand = new RelayCommand(_ => Inspect(), _ => !Busy && GameFolder.Length > 0);
        _writeCommand = new RelayCommand(_ => Write(), _ => !Busy && OutputFolder.Length > 0);
        _openOutputCommand = new RelayCommand(_ => OpenOutput(), _ => _lastWritten is not null);
    }

    /// <summary>The folder holding <c>MM.EXE</c>, <c>Mazedata.dta</c> and the 55 <c>.ovr</c> overlays.</summary>
    public string GameFolder
    {
        get => _gameFolder;
        set
        {
            if (!SetField(ref _gameFolder, value)) return;
            _inspectCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>Where the cluebook is written.</summary>
    public string OutputFolder
    {
        get => _outputFolder;
        set
        {
            if (!SetField(ref _outputFolder, value)) return;
            _writeCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>What the last action did.</summary>
    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    /// <summary>What was found in the game folder.</summary>
    public string Found
    {
        get => _found;
        private set => SetField(ref _found, value);
    }

    /// <summary>Whether a read or a write is under way.</summary>
    public bool Busy
    {
        get => _busy;
        private set
        {
            if (!SetField(ref _busy, value)) return;
            OnPropertyChanged(nameof(IsIdle));
            _inspectCommand.RaiseCanExecuteChanged();
            _writeCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>The inverse of <see cref="Busy"/>, for binding <c>IsEnabled</c>.</summary>
    public bool IsIdle => !Busy;

    /// <summary>Draw all 55 maps. Most of the document's size, and most of its use.</summary>
    public bool IncludePlans { get => _includePlans; set => SetField(ref _includePlans, value); }

    /// <summary>Include what each location says. Needs a game folder; without one there is nothing to include.</summary>
    public bool IncludeEventText { get => _includeEventText; set => SetField(ref _includeEventText, value); }

    /// <summary>Include the solution walkthrough.</summary>
    public bool IncludeWalkthrough { get => _includeWalkthrough; set => SetField(ref _includeWalkthrough, value); }

    /// <summary>Include the classes, the levelling tables and the combat rules.</summary>
    public bool IncludeRules { get => _includeRules; set => SetField(ref _includeRules, value); }

    /// <summary>Include both spell lists.</summary>
    public bool IncludeSpells { get => _includeSpells; set => SetField(ref _includeSpells, value); }

    /// <summary>Include the 255-entry item table.</summary>
    public bool IncludeItems { get => _includeItems; set => SetField(ref _includeItems, value); }

    /// <summary>Include the 195-entry bestiary.</summary>
    public bool IncludeBestiary { get => _includeBestiary; set => SetField(ref _includeBestiary, value); }

    public ICommand InspectCommand => _inspectCommand;
    public ICommand WriteCommand => _writeCommand;

    /// <summary>Opens the folder the cluebook was written to.</summary>
    public ICommand OpenOutputCommand => _openOutputCommand;

    /// <summary>Reads the game folder and says what is in it.</summary>
    public void Inspect()
    {
        Busy = true;
        try
        {
            _sources = CluebookSources.FromFolder(GameFolder, out string detail);
            Found = detail;
            Status = _sources.Overlays is { Count: > 0 }
                ? "Game folder read. The cluebook will carry what every one of those locations says."
                : "Nothing was read from that folder. Check it is the one holding MM.EXE — the cluebook can " +
                  "still be written from the bundled reference.";
        }
        finally
        {
            Busy = false;
        }
    }

    /// <summary>
    /// Writes the cluebook, and returns the HTML file's path.
    ///
    /// The two formats are written together on purpose: the HTML is the one to read, and the plain
    /// text is the one that greps, diffs between two decodes, and opens in a terminal beside DOSBox.
    /// </summary>
    public string? Write()
    {
        Busy = true;
        try
        {
            try
            {
                Directory.CreateDirectory(OutputFolder);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Status = $"Could not create {OutputFolder}: {e.Message}";
                return null;
            }

            // Re-read the folder when the user typed one and never pressed Inspect, so that pressing
            // Write alone still produces the book they asked for.
            if (GameFolder.Length > 0 && !string.Equals(_sources.GameFolder, GameFolder, StringComparison.OrdinalIgnoreCase))
            {
                _sources = CluebookSources.FromFolder(GameFolder, out string detail);
                Found = detail;
            }

            var cluebook = Cluebook.Build(_sources, new CluebookOptions
            {
                IncludePlans = IncludePlans,
                IncludeEventText = IncludeEventText,
                IncludeWalkthrough = IncludeWalkthrough,
                IncludeRules = IncludeRules,
                IncludeSpells = IncludeSpells,
                IncludeItems = IncludeItems,
                IncludeBestiary = IncludeBestiary,
            });

            string stem = Path.Combine(OutputFolder, FileStem);
            try
            {
                // The HTML declares its own charset; the plain text has nowhere to say so, and the
                // document is full of dashes and accents, so it gets a byte-order mark.
                File.WriteAllText(stem + ".html", HtmlCluebookWriter.Write(cluebook), Utf8);
                File.WriteAllText(stem + ".txt", TextCluebookWriter.Write(cluebook), Utf8WithMark);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Status = $"Could not write {stem}: {e.Message}";
                return null;
            }

            _lastWritten = stem + ".html";
            Status = $"Wrote {Path.GetFileName(stem)}.html and .txt to {OutputFolder} — " +
                     $"{cluebook.Chapters.Count} places" +
                     (cluebook.HasEventText ? $", {cluebook.MessageCount:N0} messages from your own files." : ".");
            return _lastWritten;
        }
        finally
        {
            Busy = false;
            _openOutputCommand.RaiseCanExecuteChanged();
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

    /// <summary>What both files are called, without an extension.</summary>
    public const string FileStem = "Might and Magic 1 cluebook";

    /// <summary>UTF-8 without a byte-order mark, for the HTML.</summary>
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>UTF-8 with a byte-order mark, for the plain text.</summary>
    private static readonly UTF8Encoding Utf8WithMark = new(encoderShouldEmitUTF8Identifier: true);
}
