using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using HillsfarTrainer.Game;
using HillsfarTrainer.Memory;

namespace HillsfarTrainer.ViewModels;

/// <summary>A selectable target process.</summary>
public sealed class ProcessEntry
{
    /// <summary>Process id.</summary>
    public int Id { get; }

    /// <summary>Process name.</summary>
    public string Name { get; }

    /// <summary>True when the name looks like an emulator, which floats it to the top of the list.</summary>
    public bool IsEmulator { get; }

    /// <summary>Label for the combo box.</summary>
    public string Display => $"{Name}  (pid {Id})";

    /// <summary>Builds an entry.</summary>
    public ProcessEntry(int id, string name, bool isEmulator)
    {
        Id = id;
        Name = name;
        IsEmulator = isEmulator;
    }
}

/// <summary>
/// Root view-model: pick the emulator, attach, auto-locate the data segment, then poll it.
///
/// <para>There is no manual value search anywhere in this trainer, and there does not need to be.
/// Hillsfar's program has a single data segment in which every global sits at a fixed offset, so
/// attaching runs <see cref="GameLocator"/>, which anchors on the game's own 69-byte startup banner,
/// corroborates with four more literals, and reads the character record at its fixed offset past
/// <c>DGROUP:0000</c>.</para>
/// </summary>
public sealed class MainViewModel : ObservableObject, ICharacterHost, IDisposable
{
    private ProcessMemory? _mem;
    private IMemorySource? _source;
    private readonly DispatcherTimer _poll;
    private CancellationTokenSource? _scanCts;
    private int _readFailures;

    /// <summary>Consecutive failed poll reads before the user is told the game is gone.</summary>
    private const int ReadFailuresBeforeReporting = 5;

    /// <summary>Candidate processes to attach to.</summary>
    public ObservableCollection<ProcessEntry> Processes { get; } = new();

    /// <summary>The read-only reference tabs.</summary>
    public ReferenceViewModel Reference { get; } = new();

    /// <summary>The offline character-file editor.</summary>
    public FileEditorViewModel Files { get; }

    /// <summary>Race names for combo boxes.</summary>
    public IReadOnlyList<string> Races => RaceBook.Races;

    /// <summary>Gender names for combo boxes.</summary>
    public IReadOnlyList<string> Genders => RaceBook.Genders;

    /// <summary>Alignment names for combo boxes.</summary>
    public IReadOnlyList<string> Alignments => AlignmentBook.Alignments;

    /// <summary>Class combinations for combo boxes.</summary>
    public IReadOnlyList<ClassInfo> Classes => ClassBook.Classes;

    /// <summary>Builds the shell and enumerates processes.</summary>
    public MainViewModel()
    {
        Files = new FileEditorViewModel(msg => Status = msg);

        RefreshProcessesCommand = new RelayCommand(RefreshProcesses);
        AttachCommand = new RelayCommand(Attach,
            () => SelectedProcess != null && !IsAttached && !IsScanning);
        DetachCommand = new RelayCommand(Detach, () => IsAttached);
        LocateCommand = new RelayCommand(() => Locate(), () => IsAttached && !IsScanning);
        ExportCommand = new RelayCommand(Export, () => HasCharacter);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _poll.Tick += (_, _) => PollTick();

        RefreshProcesses();
        // Quiet: a failed guess must not replace the attach instructions above with
        // file-editor advice before the user has done anything.
        Files.LoadFolder(GuessGameFolder(), quiet: true);
    }

    // --- state ---------------------------------------------------------------

    private ProcessEntry? _selectedProcess;

    /// <summary>The process the user picked.</summary>
    public ProcessEntry? SelectedProcess
    {
        get => _selectedProcess;
        set { SetField(ref _selectedProcess, value); RaiseCommands(); }
    }

    private CharacterViewModel? _character;

    /// <summary>The located character, or null.</summary>
    public CharacterViewModel? Character
    {
        get => _character;
        private set
        {
            SetField(ref _character, value);
            OnPropertyChanged(nameof(HasCharacter));
            RaiseCommands();
        }
    }

    /// <summary>True once a character record has been located.</summary>
    public bool HasCharacter => _character != null;

    /// <summary>True while a process is attached.</summary>
    public bool IsAttached => _mem is { IsOpen: true };

    private bool _isScanning;

    /// <summary>True while an auto-locate is running.</summary>
    public bool IsScanning
    {
        get => _isScanning;
        private set { SetField(ref _isScanning, value); RaiseCommands(); }
    }

    private string _status =
        "Start Hillsfar in DOSBox, load or generate a character, then pick the process and Attach.";

    /// <summary>The status-bar text.</summary>
    public string Status { get => _status; set => SetField(ref _status, value); }

    // --- commands ------------------------------------------------------------

    /// <summary>Re-enumerates the process list.</summary>
    public ICommand RefreshProcessesCommand { get; }

    /// <summary>Opens the selected process and auto-locates.</summary>
    public ICommand AttachCommand { get; }

    /// <summary>Closes the process handle.</summary>
    public ICommand DetachCommand { get; }

    /// <summary>Re-runs the auto-locate.</summary>
    public ICommand LocateCommand { get; }

    /// <summary>Writes the live character out as a <c>.HIL</c> file.</summary>
    public ICommand ExportCommand { get; }

    // --- process management --------------------------------------------------

    /// <summary>Rebuilds <see cref="Processes"/>, keeping the current selection if it survives.</summary>
    public void RefreshProcesses()
    {
        int? previous = SelectedProcess?.Id;
        var list = new List<ProcessEntry>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string name = p.ProcessName;
                bool emu = GameFacts.EmulatorHints.Any(
                    h => name.Contains(h, StringComparison.OrdinalIgnoreCase));
                list.Add(new ProcessEntry(p.Id, name, emu));
            }
            catch
            {
                // The process exited between enumeration and the query — skip it.
            }
            finally
            {
                p.Dispose();
            }
        }

        Processes.Clear();
        foreach (var e in list.OrderByDescending(e => e.IsEmulator)
                              .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase))
            Processes.Add(e);

        SelectedProcess = Processes.FirstOrDefault(e => e.Id == previous)
                          ?? Processes.FirstOrDefault(e => e.IsEmulator)
                          ?? Processes.FirstOrDefault();
    }

    private void Attach()
    {
        if (SelectedProcess == null || IsScanning) return;
        try
        {
            _mem?.Dispose();   // never leak a handle if Attach is somehow reached twice
            _mem = ProcessMemory.Open(SelectedProcess.Id);
            _source = new ProcessMemorySource(_mem);
            _readFailures = 0;
            OnPropertyChanged(nameof(IsAttached));
            RaiseCommands();
            _poll.Start();
            Status = $"Attached to {SelectedProcess.Name} (pid {SelectedProcess.Id}). Locating the game…";
            Locate();
        }
        catch (Exception ex)
        {
            Status = "Attach failed: " + ex.Message;
        }
    }

    private void Detach()
    {
        _poll.Stop();
        _scanCts?.Cancel();
        _mem?.Dispose();
        _mem = null;
        _source = null;
        Character = null;
        _readFailures = 0;
        // An in-flight scan only notices cancellation at a chunk boundary, and its `finally` would
        // otherwise clear IsScanning long after the fact. Clear it here so the next Attach is not
        // silently swallowed by the `if (IsScanning) return;` guard in Locate.
        IsScanning = false;
        OnPropertyChanged(nameof(IsAttached));
        RaiseCommands();
        Status = "Detached.";
    }

    // --- auto-locate ---------------------------------------------------------

    private async void Locate()
    {
        // Capture the attachment up front and check it here rather than asserting it away later:
        // _mem and _source are set and cleared together, but stating the requirement once beats a
        // null-forgiving `!` the compiler cannot check.
        var mem = _mem;
        var source = _source;
        if (mem == null || source == null || IsScanning) return;

        IsScanning = true;
        Status = "Locating the game's data segment…";
        // Cancel the previous scan but do NOT dispose it: an earlier scan may still be polling that
        // token on the thread pool, and disposing a source another thread is still observing is
        // outside the type's documented contract. Let it be collected.
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();

        var ct = _scanCts.Token;
        try
        {
            var found = await Task.Run(() => GameLocator.Locate(source, ct), ct);
            if (mem != _mem) return;   // detached or re-attached while scanning

            if (!found.Found)
            {
                Character = null;
                Status = found.AnchorsMatchedButRecordDidNot
                    ? $"Found Hillsfar at 0x{(ulong)found.RejectedAddress:X}, but there is no character "
                      + "loaded — load or generate one at the camp menu, then click Locate again."
                    : "Hillsfar was not found in that process. Make sure MAIN.EXE is running past the "
                      + "graphics-mode and disk-drive prompts, then click Locate again.";
                return;
            }

            Character = new CharacterViewModel(this, found);
            _readFailures = 0;
            _poll.Start();   // PollTick stops the timer when it loses the address; restart it here
            // A differing digraph table means a different release, and the record offsets are
            // hard-coded — say so loudly rather than letting the user trust fields that may have moved.
            string buildNote = found.TextTableMatchesShipped == false
                ? "  WARNING: this build's text table differs from the one the trainer was written "
                  + "against, so the record offsets may not hold — check every value before editing."
                : string.Empty;
            Status = $"Found the data segment at 0x{(ulong)found.DgroupAddress:X} — {found.Method} "
                     + $"({found.ValidatorsMatched}/{CharacterFormat.Validators.Length} corroborating "
                     + $"literals matched, at least {CharacterFormat.MinValidators} required). "
                     + $"{Character.LiveSummary} — check that against the game's own status panel "
                     + $"before editing.{buildNote}";
        }
        catch (OperationCanceledException)
        {
            if (mem == _mem) Status = "Locate cancelled.";
        }
        catch (Exception ex)
        {
            if (mem == _mem) Status = "Locate error: " + ex.Message;
        }
        finally
        {
            // Only the scan that still owns the attachment may clear the flag. Detach already
            // cleared it, and a quick re-attach may have started a newer scan — a stale scan's
            // continuation landing here must not tell the UI that the live one has finished.
            if (mem == _mem)
            {
                IsScanning = false;
                RaiseCommands();
            }
        }
    }

    // --- poll loop -----------------------------------------------------------

    private void PollTick()
    {
        var source = _source;
        var character = Character;
        if (source == null || character == null) return;

        // Revalidating the shape on every tick is not belt-and-braces — it is the only thing that
        // notices a game restart. DOSBox allocates its guest RAM once for the emulator's lifetime, so
        // quitting MAIN.EXE and starting it again relocates DGROUP while leaving the old host memory
        // mapped and perfectly readable. A read-success test alone would keep returning true, and every
        // edit and freeze write would go to a stale address inside the guest — exactly the "wrong
        // address" failure this whole design exists to avoid.
        if (GameLocator.Reread(source, character.DgroupAddress, character.LiveBuffer)
            && CharacterFormat.LooksLikeRecord(character.LiveBuffer))
        {
            _readFailures = 0;
            character.OnPolled();
            return;
        }

        // The window stopped reading, or stopped looking like a character. Either way the address can
        // no longer be trusted: drop it so nothing else writes there, and say so instead of leaving a
        // stale mirror on screen that the user would keep editing.
        if (++_readFailures >= ReadFailuresBeforeReporting)
        {
            _poll.Stop();
            Character = null;
            _readFailures = 0;
            Status = "Lost the game — it exited, restarted, or reloaded, so its data segment has moved. "
                     + "Click Locate game to find it again.";
        }
    }

    // --- export --------------------------------------------------------------

    private void Export()
    {
        var character = Character;
        if (character == null) return;
        Files.ExportRecord(character.SnapshotEdited(), character.Name);
    }

    /// <summary>Environment variable that names the game folder, checked before the generic guesses.</summary>
    public const string GameFolderVariable = "HILLSFAR_DIR";

    /// <summary>
    /// A plausible default for the character-file folder, so the offline editor is usable without the
    /// user hunting for the path. Returns an empty string when nothing obvious is present.
    ///
    /// <para>Set <c>HILLSFAR_DIR</c> to point it at a non-standard install. There are deliberately no
    /// machine-specific paths baked in here — the folder is also editable in the UI.</para>
    /// </summary>
    private static string GuessGameFolder()
    {
        var candidates = new List<string>();
        var fromEnvironment = Environment.GetEnvironmentVariable(GameFolderVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment)) candidates.Add(fromEnvironment);
        candidates.Add(@"C:\GAMES\HILLSFAR");
        candidates.Add(@"C:\DOS\HILLSFAR");
        candidates.Add(@"C:\HILLSFAR");

        foreach (var candidate in candidates)
        {
            try
            {
                if (Directory.Exists(candidate) &&
                    File.Exists(Path.Combine(candidate, "MAIN.EXE"))) return candidate;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Unreadable path — try the next candidate.
            }
        }
        return string.Empty;
    }

    // --- ICharacterHost ------------------------------------------------------

    bool ICharacterHost.WriteBytes(nuint dgroupBase, int dgroupOffset, byte[] bytes)
    {
        // The base comes from the caller, not from whatever `Character` happens to be now. A
        // re-locate after a game restart installs a new view-model at a new address; taking the base
        // from the shell would let a write raised by the old instance land at the new instance's
        // address. For a component whose failure mode is "writes to the wrong address", the caller
        // naming its own base is the only safe shape.
        if (_mem == null || dgroupBase == 0) return false;
        return _mem.Write(dgroupBase + (nuint)dgroupOffset, bytes);
    }

    void ICharacterHost.ReportStatus(string message) => Status = message;

    // --- plumbing ------------------------------------------------------------

    private void RaiseCommands()
    {
        (AttachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (DetachCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (LocateCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _poll.Stop();
        // Cancel but do not dispose, for the same reason as in Locate: a scan may still be running
        // on the thread pool and observing this token. Let it be collected.
        _scanCts?.Cancel();
        _mem?.Dispose();
    }
}
