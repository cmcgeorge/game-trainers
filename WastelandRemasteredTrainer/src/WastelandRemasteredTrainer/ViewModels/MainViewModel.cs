using System.Collections.ObjectModel;
using System.Diagnostics;
using WastelandRemasteredTrainer.Game;
using WastelandRemasteredTrainer.Memory;

namespace WastelandRemasteredTrainer.ViewModels;

/// <summary>
/// Top-level view model. Handles process attachment, auto-locate, the character list,
/// freeze toggles, and quick action buttons.
/// </summary>
public sealed class MainViewModel : ObservableObject, ICharacterHost, IDisposable
{
    /// <summary>How often frozen values are re-written and the live view is re-read.</summary>
    private const int PollIntervalMs = 400;

    private Process? _process;
    private ProcessMemorySource? _mem;
    private GameLocation? _location;
    private nuint _moduleBase;
    private nuint _moduleSize;
    private System.Threading.Timer? _pollTimer;
    private CancellationTokenSource? _locateCts;

    /// <summary>Guards against a slow poll tick overlapping the next one.</summary>
    private int _pollBusy;

    /// <summary>
    /// Immutable copy of <see cref="Characters"/> for the poll thread. Republished by the UI
    /// thread on every change, so the timer never marshals just to enumerate the list.
    /// </summary>
    private volatile CharacterViewModel[] _snapshot = Array.Empty<CharacterViewModel>();

    private string _statusMessage = "Attach to the game to begin.";
    private string _partyStateText = "";
    private bool _isAttached;
    private bool _isLocating;
    private double _locateProgress;
    private int _selectedCharacterIndex = -1;

    public MainViewModel()
    {
        AttachCommand = new RelayCommand(_ => Attach(), _ => !IsAttached);
        LocateCommand = new RelayCommand(_ => Locate(), _ => IsAttached && !IsLocating);
        CancelLocateCommand = new RelayCommand(_ => CancelLocate(), _ => IsLocating);
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);

        RefreshAllCommand = new RelayCommand(_ => RefreshAll(), _ => HasCharacters);
        WriteAllCommand = new RelayCommand(_ => ForEachCharacter(c => c.Write(), "Pending edits written."),
            _ => Characters.Any(c => c.HasPendingEdits));
        FullHealAllCommand = new RelayCommand(
            _ => ForEachCharacter(c => c.FullHealCommand.Execute(null), "Party fully healed."), _ => HasCharacters);
        MaxAttributesAllCommand = new RelayCommand(
            _ => ForEachCharacter(c => c.MaxAttributesCommand.Execute(null), "Party attributes maxed."), _ => HasCharacters);
        MaxSkillsAllCommand = new RelayCommand(
            _ => ForEachCharacter(c => c.MaxSkillsCommand.Execute(null), "Party skills maxed."), _ => HasCharacters);
        MaxMoneyAllCommand = new RelayCommand(
            _ => ForEachCharacter(c => c.MaxMoneyCommand.Execute(null), "Party money maxed."), _ => HasCharacters);
        MaxEverythingAllCommand = new RelayCommand(
            _ => ForEachCharacter(c => c.MaxEverythingCommand.Execute(null), "All characters maxed."), _ => HasCharacters);
    }

    public ObservableCollection<CharacterViewModel> Characters { get; } = new();

    private bool HasCharacters => IsAttached && Characters.Count > 0;

    public CharacterViewModel? SelectedCharacter =>
        _selectedCharacterIndex >= 0 && _selectedCharacterIndex < Characters.Count
            ? Characters[_selectedCharacterIndex] : null;

    public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

    /// <summary>Live party position and clock, when the (unverified) save block can be read.</summary>
    public string PartyStateText { get => _partyStateText; set => SetField(ref _partyStateText, value); }

    public bool IsAttached
    {
        get => _isAttached;
        set { SetField(ref _isAttached, value); RaiseAllCanExecuteChanged(); }
    }

    public bool IsLocating
    {
        get => _isLocating;
        set { SetField(ref _isLocating, value); RaiseAllCanExecuteChanged(); }
    }

    /// <summary>Structural-scan progress, 0..1. Only moves during the fallback scan.</summary>
    public double LocateProgress { get => _locateProgress; set => SetField(ref _locateProgress, value); }

    public int SelectedCharacterIndex
    {
        get => _selectedCharacterIndex;
        set
        {
            SetField(ref _selectedCharacterIndex, value);
            OnPropertyChanged(nameof(SelectedCharacter));
            SelectedCharacter?.Refresh();
            RaiseAllCanExecuteChanged();
        }
    }

    // --- commands ---------------------------------------------------------------
    public RelayCommand AttachCommand { get; }
    public RelayCommand LocateCommand { get; }
    public RelayCommand CancelLocateCommand { get; }
    public RelayCommand DetachCommand { get; }
    public RelayCommand RefreshAllCommand { get; }
    public RelayCommand WriteAllCommand { get; }
    public RelayCommand FullHealAllCommand { get; }
    public RelayCommand MaxAttributesAllCommand { get; }
    public RelayCommand MaxSkillsAllCommand { get; }
    public RelayCommand MaxMoneyAllCommand { get; }
    public RelayCommand MaxEverythingAllCommand { get; }

    private void Attach()
    {
        var proc = GameLocator.FindGameProcess();
        if (proc == null)
        {
            StatusMessage = $"Process '{GameFacts.ProcessName}.exe' not found. Start the game first.";
            return;
        }

        try
        {
            _moduleBase = GameLocator.FindModuleBase(proc, GameFacts.GameModuleName);
            _moduleSize = GameLocator.FindModuleSize(proc, GameFacts.GameModuleName);
            _mem = new ProcessMemorySource(ProcessMemory.Open(proc.Id));

            // Notice if the game closes, rather than silently reading zeros forever.
            _process = proc;
            _process.EnableRaisingEvents = true;
            _process.Exited += OnGameExited;

            IsAttached = true;

            StatusMessage = _moduleBase != 0
                ? $"Attached to PID {proc.Id}. {GameFacts.GameModuleName} @ 0x{_moduleBase:X}. Click Locate."
                : $"Attached to PID {proc.Id}, but {GameFacts.GameModuleName} was not found.";

            _pollTimer = new System.Threading.Timer(_ => PollCallback(), null, PollIntervalMs, PollIntervalMs);
        }
        catch (Exception ex)
        {
            proc.Dispose();
            _process = null;
            StatusMessage = $"Attach failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Raised on a threadpool thread when the game process ends. Anything that escapes here
    /// terminates the trainer, so the whole body is guarded.
    /// </summary>
    private void OnGameExited(object? sender, EventArgs e)
    {
        try
        {
            OnUi(() =>
            {
                if (!IsAttached) return;
                Detach();
                StatusMessage = "The game exited. Re-launch it and click Attach.";
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Exit notification failed: {ex.Message}");
        }
    }

    private void Locate()
    {
        if (_mem == null) return;

        _locateCts?.Dispose();
        _locateCts = new CancellationTokenSource();

        IsLocating = true;
        LocateProgress = 0;
        StatusMessage = "Sweeping the game's data sections for its classes, then finding the party...";

        var mem = _mem;
        nuint moduleBase = _moduleBase, moduleSize = _moduleSize;
        var ct = _locateCts.Token;
        var progress = new Progress<double>(p => LocateProgress = p);

        Task.Run(() =>
        {
            try
            {
                var found = GameLocator.Locate(mem, moduleBase, moduleSize, progress, ct);
                OnUi(() => ApplyLocation(mem, found));
            }
            catch (OperationCanceledException)
            {
                OnUi(() => Report(mem, "Scan cancelled."));
            }
            catch (Exception ex)
            {
                OnUi(() => Report(mem, $"Scan failed: {ex.Message}"));
            }
        }, ct);

        void Report(IMemorySource source, string message)
        {
            if (!ReferenceEquals(source, _mem)) return;   // a stale scan must not clear a new one
            IsLocating = false;
            LocateProgress = 0;
            StatusMessage = message;
        }
    }

    private void CancelLocate()
    {
        _locateCts?.Cancel();
        StatusMessage = "Cancelling the scan...";
    }

    private void ApplyLocation(IMemorySource mem, GameLocation? found)
    {
        // Check staleness first: a scan from a previous attach must not touch this session's
        // state, including its progress and busy flags.
        if (!ReferenceEquals(mem, _mem)) return;

        IsLocating = false;
        LocateProgress = 0;
        _location = found;
        Characters.Clear();

        PublishSnapshot();

        if (found == null)
        {
            StatusMessage = "Nothing found. Load a saved game (or start one) so the party exists, then locate again.";
            RaiseAllCanExecuteChanged();
            return;
        }

        int slot = 0;
        foreach (var addr in found.CharacterAddresses)
            Characters.Add(new CharacterViewModel(new CharacterRecord(mem, addr, slot++), this));

        PublishSnapshot();
        if (Characters.Count > 0) SelectedCharacterIndex = 0;

        string caution = found.UsedFallback
            ? " (structural fallback — confirm the characters look right before editing)"
            : "";
        StatusMessage = $"{found.Summary}. {Characters.Count} character(s) loaded.{caution}";

        UpdatePartyState();
        RaiseAllCanExecuteChanged();
    }

    /// <summary>
    /// Poll tick. Freezes are applied straight from this thread — they only read and write
    /// process memory — while the UI refresh is posted to the dispatcher, because the character
    /// view models raise change notifications.
    ///
    /// <para>Nothing here ever <i>blocks</i> on the UI thread. The character array is a snapshot
    /// republished by the UI thread whenever the list changes, so the timer never has to marshal
    /// to read it, and the refresh is posted rather than invoked. Blocking would deadlock against
    /// <see cref="Detach"/>, which waits on the UI thread for this callback to finish.</para>
    /// </summary>
    private void PollCallback()
    {
        if (Interlocked.Exchange(ref _pollBusy, 1) == 1) return;   // previous tick still running

        try
        {
            var mem = _mem;
            var snapshot = _snapshot;
            if (mem == null || _location == null || snapshot.Length == 0) return;

            foreach (var chr in snapshot)
            {
                try { chr.ApplyFreezes(); }
                catch (Exception ex) { Debug.WriteLine($"Freeze failed: {ex.Message}"); }
            }

            PostToUi(() =>
            {
                if (!ReferenceEquals(mem, _mem)) return;
                foreach (var chr in Characters) chr.RefreshScalars();
                UpdatePartyState();
                WriteAllCommand.RaiseCanExecuteChanged();
            });
        }
        catch (Exception ex)
        {
            // An unhandled exception out of a timer callback would take the process down.
            Debug.WriteLine($"Poll tick failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _pollBusy, 0);
        }
    }

    /// <summary>
    /// Republishes the character snapshot the poll thread reads. Call from the UI thread after
    /// any change to <see cref="Characters"/>.
    /// </summary>
    private void PublishSnapshot() => _snapshot = Characters.ToArray();

    private void UpdatePartyState()
    {
        var mem = _mem;
        var location = _location;
        if (mem == null || location == null) { PartyStateText = ""; return; }

        var state = PartyStateReader.Read(mem, location.Classes);
        PartyStateText = state == null
            ? ""
            : $"Party: {state.PartyText}, {state.PositionText}, clock {state.Clock} (unconfirmed offsets)";
    }

    private void ForEachCharacter(Action<CharacterViewModel> action, string doneMessage)
    {
        foreach (var chr in Characters.ToList()) action(chr);
        OnMessage(doneMessage);
        RaiseAllCanExecuteChanged();
    }

    private void RefreshAll()
    {
        foreach (var chr in Characters) chr.Refresh();
        UpdatePartyState();
        OnMessage("Re-read every character from the game.");
    }

    private void Detach()
    {
        _locateCts?.Cancel();

        // Wait for an in-flight poll callback so nothing touches the handle after it closes.
        if (_pollTimer != null)
        {
            using var done = new ManualResetEvent(false);
            if (_pollTimer.Dispose(done)) done.WaitOne(TimeSpan.FromSeconds(2));
            _pollTimer = null;
        }

        // If that wait timed out, the busy flag would otherwise stay raised and silently disable
        // polling for the rest of the session, including after a re-Attach.
        Interlocked.Exchange(ref _pollBusy, 0);

        if (_process != null)
        {
            _process.Exited -= OnGameExited;
            _process.Dispose();
            _process = null;
        }

        _mem?.Dispose();
        _mem = null;
        _location = null;
        Characters.Clear();
        PublishSnapshot();
        SelectedCharacterIndex = -1;
        IsLocating = false;
        LocateProgress = 0;
        PartyStateText = "";
        IsAttached = false;
        StatusMessage = "Detached.";
    }

    public void OnMessage(string message) => StatusMessage = message;

    public void RefreshSelected() => SelectedCharacter?.Refresh();

    /// <summary>Runs an action on the UI thread, waiting for it. Safe to call from any thread.</summary>
    private static void OnUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        try
        {
            if (dispatcher == null || dispatcher.CheckAccess()) action();
            else dispatcher.Invoke(action);
        }
        catch (System.Threading.Tasks.TaskCanceledException)
        {
            // The dispatcher is shutting down (the window closed mid-scan). Nothing to update.
        }
        catch (InvalidOperationException)
        {
            // The dispatcher has already shut down. Invoke throws this rather than
            // TaskCanceledException, and letting it escape a threadpool callback — which is
            // where the process-exit notification arrives — would take the process down.
        }
    }

    /// <summary>
    /// Queues an action on the UI thread without waiting. Used by the poll tick, which must
    /// never block on the UI thread.
    /// </summary>
    private static void PostToUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) { action(); return; }
        try { dispatcher.BeginInvoke(action); }
        catch (System.ComponentModel.Win32Exception) { /* dispatcher gone */ }
        catch (InvalidOperationException) { /* dispatcher shut down */ }
    }

    private void RaiseAllCanExecuteChanged()
    {
        AttachCommand.RaiseCanExecuteChanged();
        LocateCommand.RaiseCanExecuteChanged();
        CancelLocateCommand.RaiseCanExecuteChanged();
        DetachCommand.RaiseCanExecuteChanged();
        RefreshAllCommand.RaiseCanExecuteChanged();
        WriteAllCommand.RaiseCanExecuteChanged();
        FullHealAllCommand.RaiseCanExecuteChanged();
        MaxAttributesAllCommand.RaiseCanExecuteChanged();
        MaxSkillsAllCommand.RaiseCanExecuteChanged();
        MaxMoneyAllCommand.RaiseCanExecuteChanged();
        MaxEverythingAllCommand.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _locateCts?.Cancel();
        _locateCts?.Dispose();
        _locateCts = null;
        _pollTimer?.Dispose();
        _pollTimer = null;
        if (_process != null)
        {
            _process.Exited -= OnGameExited;
            _process.Dispose();
            _process = null;
        }
        _mem?.Dispose();
        _mem = null;
    }
}
