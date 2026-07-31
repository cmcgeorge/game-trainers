using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using AirborneRangerTrainer.Game;

namespace AirborneRangerTrainer.ViewModels;

/// <summary>One decoration checkbox on a ranger.</summary>
public sealed class DecorationToggle : ObservableObject
{
    private readonly RangerViewModel _owner;

    internal DecorationToggle(RangerViewModel owner, DecorationInfo info)
    {
        _owner = owner;
        Info = info;
    }

    /// <summary>The award this toggle covers.</summary>
    public DecorationInfo Info { get; }

    /// <summary>Label for the UI.</summary>
    public string Label => $"{Info.Mnemonic} — {Info.Name}";

    /// <summary>Whether the ranger has the award.</summary>
    public bool IsSet
    {
        get => _owner.Record.HasDecoration(Info.Bit);
        set
        {
            if (_owner.Record.HasDecoration(Info.Bit) == value) return;
            _owner.Record.SetDecoration(Info.Bit, value);
            OnPropertyChanged();
            _owner.NotifyEdited();
        }
    }

    /// <summary>Re-reads the underlying bit after an external change.</summary>
    internal void Refresh() => OnPropertyChanged(nameof(IsSet));
}

/// <summary>One ranger slot in the roster editor.</summary>
public sealed class RangerViewModel : ObservableObject
{
    private readonly Action _onEdited;

    internal RangerViewModel(RangerRecord record, Action onEdited)
    {
        Record = record;
        _onEdited = onEdited;
        var toggles = new List<DecorationToggle>();
        foreach (var d in DecorationBook.All) toggles.Add(new DecorationToggle(this, d));
        Decorations = toggles;
        Ranks = RankBook.All;
    }

    /// <summary>The record this view-model edits.</summary>
    public RangerRecord Record { get; }

    /// <summary>Slot label for the UI.</summary>
    public string SlotLabel => $"Slot {Record.Slot + 1}";

    /// <summary>The rank list to choose from.</summary>
    public IReadOnlyList<RankInfo> Ranks { get; }

    /// <summary>The six decoration toggles.</summary>
    public IReadOnlyList<DecorationToggle> Decorations { get; }

    /// <summary>The ranger's name.</summary>
    public string Name
    {
        get => Record.Name;
        set
        {
            string clean = RosterFormat.SanitiseName(value);
            if (clean == Record.Name)
            {
                // Sanitising changed the input (padding, or a character the game cannot render), so
                // the text box is showing something the file does not contain. Notify to snap it
                // back, as the rank and score setters do for a clamped value.
                if (clean != value) OnPropertyChanged();
                return;
            }
            Record.Name = clean;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(IsOccupied));
            _onEdited();
        }
    }

    /// <summary>Rank index into <see cref="RankBook"/>.</summary>
    public int RankIndex
    {
        get => Record.RankIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, RankBook.Count - 1);
            if (clamped == Record.RankIndex)
            {
                if (clamped != value) OnPropertyChanged();
                return;
            }
            Record.RankIndex = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Summary));
            _onEdited();
        }
    }

    /// <summary>Career merit points.</summary>
    public int Score
    {
        get => Record.Score;
        set
        {
            int clamped = Math.Clamp(value, 0, RosterFormat.MaxScore);
            if (clamped == Record.Score)
            {
                if (clamped != value) OnPropertyChanged();
                return;
            }
            Record.Score = clamped;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Summary));
            _onEdited();
        }
    }

    /// <summary>Whether the ranger carries the campaign ribbon.</summary>
    public bool HasCampaignRibbon
    {
        get => Record.HasCampaignRibbon;
        set
        {
            if (Record.HasCampaignRibbon == value) return;
            Record.HasCampaignRibbon = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Summary));
            _onEdited();
        }
    }

    /// <summary>True when the slot holds a real ranger.</summary>
    public bool IsOccupied => Record.IsOccupied;

    /// <summary>One-line summary, in the shape the game's roster screen prints.</summary>
    public string Summary => Record.IsOccupied
        ? $"{Record.RankMnemonic} {Record.Name}  —  {Record.Score:N0}" +
          (Record.DecorationLine.Length > 0 ? $"   [{Record.DecorationLine}]" : string.Empty)
        : "(empty slot)";

    internal void NotifyEdited()
    {
        OnPropertyChanged(nameof(Summary));
        _onEdited();
    }

    /// <summary>Re-reads every field after the file was reloaded or bulk-edited.</summary>
    public void RefreshAll()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(RankIndex));
        OnPropertyChanged(nameof(Score));
        OnPropertyChanged(nameof(HasCampaignRibbon));
        OnPropertyChanged(nameof(IsOccupied));
        OnPropertyChanged(nameof(Summary));
        foreach (var d in Decorations) d.Refresh();
    }
}

/// <summary>
/// The offline <c>ROSTER.DAT</c> editor.
///
/// <para>This is a file editor, not a memory editor: the game owns the roster and rewrites it when
/// a veteran ranger finishes a mission, so edits are made with the game <b>closed</b> and everything
/// the trainer does not understand — including the two undecoded bytes in each record's binary tail
/// — is round-tripped byte for byte. A one-shot <c>.bak</c> of the original is taken before the
/// first save.</para>
/// </summary>
public sealed class RosterViewModel : ObservableObject
{
    private readonly Action<string> _report;
    private RosterFile? _file;

    /// <summary>Builds the editor. <paramref name="report"/> receives status-bar messages.</summary>
    public RosterViewModel(Action<string> report)
    {
        ArgumentNullException.ThrowIfNull(report);
        _report = report;
        SaveCommand = new RelayCommand(Save, () => _file != null && IsDirty);
        RevertCommand = new RelayCommand(Revert, () => _file != null && IsDirty);
    }

    /// <summary>The six ranger slots, or empty when no file is loaded.</summary>
    public ObservableCollection<RangerViewModel> Rangers { get; } = new();

    /// <summary>True when a roster file is open.</summary>
    public bool HasFile => _file != null;

    /// <summary>Path of the open file.</summary>
    public string? FilePath => _file?.Path;

    private bool _isDirty;

    /// <summary>True when there are unsaved edits.</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set { if (SetField(ref _isDirty, value)) RaiseCommands(); }
    }

    /// <summary>Writes the roster back to disk.</summary>
    public ICommand SaveCommand { get; }

    /// <summary>Throws the edits away and re-reads the file.</summary>
    public ICommand RevertCommand { get; }

    /// <summary>
    /// True when opening another file would throw work away — the caller should confirm first.
    /// Unsaved roster edits have no backup to recover from, because the <c>.bak</c> is only taken
    /// when a save actually happens.
    /// </summary>
    public bool WouldDiscardEdits => _file != null && IsDirty;

    /// <summary>
    /// Loads a roster file. Returns false — and changes nothing — when the file is missing or does
    /// not match the expected 495-byte shape.
    /// </summary>
    public bool Load(string path)
    {
        var file = RosterFile.Load(path);
        if (file == null)
        {
            _report($"'{path}' is not a readable {RosterFormat.FileName} " +
                    $"({RosterFormat.FileLength} bytes, six 81-byte records).");
            return false;
        }

        _file = file;
        Rangers.Clear();
        foreach (var r in file.Records) Rangers.Add(new RangerViewModel(r, () => IsDirty = true));
        IsDirty = false;
        OnPropertyChanged(nameof(HasFile));
        OnPropertyChanged(nameof(FilePath));
        RaiseCommands();

        int occupied = 0;
        foreach (var r in file.Records) if (r.IsOccupied) occupied++;
        _report($"Loaded {path} — {occupied} of {RosterFormat.RecordCount} slots in use.");
        return true;
    }

    private void Save()
    {
        if (_file?.Path == null) return;
        try
        {
            string? backup = _file.Save(_file.Path);
            IsDirty = false;
            _report(backup != null
                ? $"Saved {_file.Path} (original backed up to {Path.GetFileName(backup)})."
                : $"Saved {_file.Path}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _report("Save failed: " + ex.Message);
        }
    }

    private void Revert()
    {
        if (_file?.Path == null) return;
        string path = _file.Path;
        if (Load(path)) _report($"Reverted to {path} as it is on disk.");
    }

    private void RaiseCommands()
    {
        (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RevertCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
