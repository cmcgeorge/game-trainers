using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using LegendOfFaerghailTrainer.Game;
using LegendOfFaerghailTrainer.Memory;

namespace LegendOfFaerghailTrainer.ViewModels;

/// <summary>A DOSBox-family process the trainer can attach to.</summary>
public sealed record ProcessChoice(int Pid, string Name, string Title)
{
    public string Display => $"{Name} (pid {Pid}){(string.IsNullOrWhiteSpace(Title) ? "" : " — " + Title)}";
}

/// <summary>
/// Attach, locate, poll, and the party-wide quick actions. The trainer never asks the user to
/// search for a value: one Attach resolves the data group by anchored sweep and then follows the
/// game's own far pointers to the party and the saved-character roster.
/// </summary>
public sealed class MainViewModel : ObservableObject, ICharacterHost
{
    private ProcessMemory? _mem;
    private ProcessMemorySource? _source;
    private readonly DispatcherTimer _poll;
    private readonly byte[] _scratch = new byte[CharacterFormat.RecordSize];
    private CancellationTokenSource? _locateCts;

    public ObservableCollection<ProcessChoice> Processes { get; } = new();
    public ObservableCollection<CharacterViewModel> Party { get; } = new();
    public ObservableCollection<CharacterViewModel> Roster { get; } = new();
    public ReferenceViewModel Reference { get; } = new();
    public MapsViewModel Maps { get; } = new();
    public CluebookViewModel Cluebook { get; } = new();

    public RelayCommand RefreshProcessesCommand { get; }
    public RelayCommand AttachCommand { get; }
    public RelayCommand DetachCommand { get; }
    public RelayCommand HealPartyCommand { get; }
    public RelayCommand MaxPartyCommand { get; }
    public RelayCommand GoldToPartyCommand { get; }
    public RelayCommand RestockPartyCommand { get; }
    public RelayCommand SlowerCommand { get; }
    public RelayCommand FasterCommand { get; }

    public MainViewModel()
    {
        // Re-enumerating while attached would reassign SelectedProcess out from under the live
        // handle, so it is only offered while detached.
        RefreshProcessesCommand = new RelayCommand(RefreshProcesses, () => !IsAttached);
        AttachCommand = new RelayCommand(Attach, () => !IsAttached && SelectedProcess != null);
        DetachCommand = new RelayCommand(Detach, () => IsAttached);
        HealPartyCommand = new RelayCommand(HealParty, () => IsAttached && Party.Count > 0);
        MaxPartyCommand = new RelayCommand(MaxParty, () => IsAttached && Party.Count > 0);
        GoldToPartyCommand = new RelayCommand(GoldToParty, () => IsAttached && Party.Count > 0);
        RestockPartyCommand = new RelayCommand(RestockParty, () => IsAttached && Party.Count > 0);
        SlowerCommand = new RelayCommand(() => AdjustSpeed(slower: true), () => IsAttached && !_speedBusy);
        FasterCommand = new RelayCommand(() => AdjustSpeed(slower: false), () => IsAttached && !_speedBusy);

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        _poll.Tick += (_, _) => Tick();

        RefreshProcesses();
    }

    /// <summary>
    /// Attaches straight away when exactly one emulator is running, matching the rest of the
    /// repository. Called from the window's Loaded event rather than the constructor: the locate
    /// walks the target's whole address space, and doing that from a field initialiser would run it
    /// before the window is on screen. Silent on failure — the game may not be past the intro yet,
    /// and the status line already says what to do.
    /// </summary>
    public void TryAutoAttach()
    {
        if (!IsAttached && Processes.Count == 1 && SelectedProcess != null) Attach();
    }

    // --- state ------------------------------------------------------------------

    private string _status = "Start Legend of Faerghail with START.BAT in DOSBox, play past the intro into the tavern, then Attach.";
    public string Status { get => _status; private set => SetField(ref _status, value); }

    private string _locateDetail = "";
    /// <summary>Where the locator landed, for eyeballing that the attach is sane before poking.</summary>
    public string LocateDetail { get => _locateDetail; private set => SetField(ref _locateDetail, value); }

    private bool _isAttached;
    public bool IsAttached
    {
        get => _isAttached;
        private set
        {
            if (!SetField(ref _isAttached, value)) return;
            OnPropertyChanged(nameof(IsDetached));
            RaiseCommands();
        }
    }

    public bool IsDetached => !IsAttached;

    private ProcessChoice? _selectedProcess;
    public ProcessChoice? SelectedProcess
    {
        get => _selectedProcess;
        set { SetField(ref _selectedProcess, value); AttachCommand.RaiseCanExecuteChanged(); }
    }

    // The Party and Saved-characters tabs need one selection each. Binding both list boxes to a
    // single property does not work: selecting in one leaves the other unable to find that item in
    // its own ItemsSource, so it pushes null straight back and the editor blanks.
    private CharacterViewModel? _selectedPartyMember;
    public CharacterViewModel? SelectedPartyMember
    {
        get => _selectedPartyMember;
        set => SetField(ref _selectedPartyMember, value);
    }

    private CharacterViewModel? _selectedRosterEntry;
    public CharacterViewModel? SelectedRosterEntry
    {
        get => _selectedRosterEntry;
        set => SetField(ref _selectedRosterEntry, value);
    }

    private long _goldToGive = 50000;
    /// <summary>The amount "Give gold to everyone" writes into each member's purse.</summary>
    public long GoldToGive { get => _goldToGive; set => SetField(ref _goldToGive, Math.Clamp(value, 0, 99999)); }

    private int _speedSteps = 3;
    /// <summary>How many cycle steps one Slower/Faster click sends (each is about 10%).</summary>
    public int SpeedSteps { get => _speedSteps; set => SetField(ref _speedSteps, Math.Clamp(value, 1, 20)); }

    // --- attach / detach --------------------------------------------------------

    public void RefreshProcesses()
    {
        var previous = SelectedProcess?.Pid;
        Processes.Clear();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                string name = p.ProcessName;
                if (!GameFacts.EmulatorProcessHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)))
                    continue;
                Processes.Add(new ProcessChoice(p.Id, name, p.MainWindowTitle));
            }
            catch
            {
                // A process can exit between enumeration and inspection; skip it.
            }
            finally
            {
                p.Dispose();
            }
        }

        SelectedProcess = Processes.FirstOrDefault(c => c.Pid == previous) ?? Processes.FirstOrDefault();
        if (Processes.Count == 0)
            Status = "No DOSBox process found. Start the game first, then press Refresh.";
    }

    private void Attach()
    {
        if (SelectedProcess == null) return;
        try
        {
            _mem = ProcessMemory.Open(SelectedProcess.Pid);
            _source = new ProcessMemorySource(_mem);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            Cleanup();
            return;
        }

        _locateCts = new CancellationTokenSource();
        LocatedGame? found;
        string why;
        try
        {
            found = GameLocator.Find(_source, out why, _locateCts.Token);
        }
        catch (OperationCanceledException)
        {
            Cleanup();
            Status = "Locate cancelled.";
            return;
        }
        catch (Exception ex)
        {
            // Never leave the process handle open on an unexpected failure — the next Attach
            // would open a second one and the first would live until the app exits.
            Cleanup();
            Status = "The locate failed: " + ex.Message;
            return;
        }

        if (found == null)
        {
            Cleanup();
            Status = why;
            LocateDetail = "";
            return;
        }

        Party.Clear();
        foreach (var c in found.Party) Party.Add(new CharacterViewModel(this, c));
        Roster.Clear();
        foreach (var c in found.Roster) Roster.Add(new CharacterViewModel(this, c, isRosterEntry: true));

        SelectedPartyMember = Party.FirstOrDefault();
        SelectedRosterEntry = Roster.FirstOrDefault();
        IsAttached = true;
        Status = why;
        string adjacency = found.Adjacency switch
        {
            AdjacencyResult.Holds => "holds",
            AdjacencyResult.Failed => "FAILED (roster not opened)",
            _ => "no roster pointer",
        };
        LocateDetail =
            $"DGROUP 0x{(ulong)found.DgroupAddress:X}   guest 0 0x{(ulong)found.GuestZero:X}   " +
            $"party 0x{(ulong)found.PartyAddress:X} ({found.Party.Count} of {CharacterFormat.PartySlots})   " +
            $"roster 0x{(ulong)found.RosterAddress:X} ({found.Roster.Count} of {CharacterFormat.RosterSlots})   " +
            $"{found.ValidatorsMatched}/4 validators   adjacency {adjacency}";

        if (Party.Count == 0)
            Status += " The party is empty — recruit someone in the tavern, then Attach again.";

        _poll.Start();
    }

    private void Detach()
    {
        Cleanup();
        Status = "Detached.";
        LocateDetail = "";
    }

    /// <summary>Called when the window closes: stops the poll loop and closes the process handle.</summary>
    public void Shutdown() => Cleanup();

    private void Cleanup()
    {
        _poll.Stop();
        _locateCts?.Cancel();
        _locateCts?.Dispose();
        _locateCts = null;
        _mem?.Dispose();
        _mem = null;
        _source = null;
        IsAttached = false;
        Party.Clear();
        Roster.Clear();
        SelectedPartyMember = null;
        SelectedRosterEntry = null;
        RaiseCommands();
    }

    // --- poll -------------------------------------------------------------------

    private void Tick()
    {
        if (_source == null || _mem == null || !_mem.IsOpen) { Detach(); return; }

        // Snapshot the lists: they are mutated below (a dismissed companion is dropped, and Detach
        // clears them outright), and walking a collection while it changes is not something to
        // rely on.
        foreach (var vm in Party.Concat(Roster).ToList())
        {
            if (!GameLocator.Reread(_source, vm.Address, _scratch))
            {
                Cleanup();
                Status = "Lost the game's memory (did DOSBox close?). Detached.";
                LocateDetail = "";
                return;
            }

            // A successful read proves nothing: every address in DOSBox's guest stays readable for
            // the whole session, so the slot has to be re-checked before anything is written to it.
            // Three outcomes, and they are genuinely different situations:

            // 1. The slot is now empty. That is an ordinary in-game action - a companion was left
            //    at a tavern, and both arrays pack from slot 0 - so drop the row and carry on
            //    rather than alarming the user about corruption.
            if (CharacterRecord.IsEmptySlot(_scratch, 0))
            {
                Party.Remove(vm);
                Roster.Remove(vm);
                if (ReferenceEquals(SelectedPartyMember, vm)) SelectedPartyMember = Party.FirstOrDefault();
                if (ReferenceEquals(SelectedRosterEntry, vm)) SelectedRosterEntry = Roster.FirstOrDefault();
                Status = $"{vm.Record.Name} is no longer in that slot; removed from the list.";
                RaiseCommands();
                continue;
            }

            // 2. The slot holds a *different* character. The party re-packs when someone leaves, so
            //    this address can legitimately come to hold somebody else - and then every freeze
            //    and every field edit would be applied to the wrong person. Stop rather than guess.
            var fresh = new CharacterRecord(_scratch);
            if (!CharacterRecord.IsValidRecord(_scratch, 0) || fresh.Name != vm.Record.Name)
            {
                Cleanup();
                Status = $"Slot {vm.Slot + 1} no longer holds {vm.Record.Name} - the party has been "
                       + "re-arranged, or the game moved its buffers. Detached; press Attach to pick it up again.";
                LocateDetail = "";
                return;
            }

            vm.UpdateFrom(_scratch);
        }
    }

    // --- party-wide actions -----------------------------------------------------

    private void HealParty()
    {
        foreach (var c in Party) c.FullHeal();
        Status = $"Healed {Party.Count} companion(s).";
    }

    private void MaxParty()
    {
        foreach (var c in Party)
        {
            c.MaxAttributes();
            c.MaxAbilities();
            c.LearnAllLanguages();
            c.FullHeal();
        }
        Status = $"Maxed attributes, abilities and languages for {Party.Count} companion(s).";
    }

    private void GoldToParty()
    {
        foreach (var c in Party) c.Gold = GoldToGive;
        Status = $"Gave {GoldToGive} gold to each of {Party.Count} companion(s).";
    }

    private void RestockParty()
    {
        foreach (var c in Party) c.RestockSpellsAndRepairItems();
        Status = $"Refilled spell uses and repaired equipment for {Party.Count} companion(s).";
    }

    // --- emulator speed ---------------------------------------------------------

    /// <summary>
    /// Sends the cycle hotkeys to the emulator the trainer is <b>attached to</b>, not to whatever is
    /// currently picked in the combo box — with two emulators running those can differ, and sending
    /// the keys to the wrong one silently changes the speed of an unrelated game.
    ///
    /// The burst runs off the UI thread: it holds Ctrl down across up to twenty taps at 40 ms each,
    /// plus up to 200 ms of focus polling, which would otherwise freeze the window for two seconds.
    /// </summary>
    private bool _speedBusy;

    private async void AdjustSpeed(bool slower)
    {
        int pid = _mem?.ProcessId ?? SelectedProcess?.Pid ?? 0;
        if (pid == 0 || _speedBusy) return;
        int steps = SpeedSteps;

        // One burst at a time. A burst holds Ctrl down for up to a second, and a second click
        // overlapping it would release Ctrl half way through the first, delivering the remaining
        // F11/F12 taps to the game bare instead of to DOSBox's mapper.
        _speedBusy = true;
        RaiseCommands();
        try
        {
            var (ok, error) = await Task.Run(() =>
            {
                bool sent = slower
                    ? DosBoxSpeed.Slower(pid, steps, out string err)
                    : DosBoxSpeed.Faster(pid, steps, out err);
                return (sent, err);
            }).ConfigureAwait(true);

            Status = ok
                ? $"Sent Ctrl+{(slower ? "F11" : "F12")} x{steps} to DOSBox — the emulator prints the new cycle count in its title bar."
                : error;
        }
        catch (Exception ex)
        {
            // async void: an escaping exception would reach the dispatcher rather than any caller.
            Status = "Could not send the cycle hotkey: " + ex.Message;
        }
        finally
        {
            _speedBusy = false;
            RaiseCommands();
        }
    }

    // --- ICharacterHost ---------------------------------------------------------

    public bool WriteBytes(nuint recordAddress, byte[] source, int offset, int length)
    {
        if (_mem == null || !_mem.IsOpen) return false;
        if (offset < 0 || length < 0 || offset > source.Length - length) return false;
        return _mem.WriteRange(recordAddress, source, offset, length);
    }

    private void RaiseCommands()
    {
        RefreshProcessesCommand.RaiseCanExecuteChanged();
        AttachCommand.RaiseCanExecuteChanged();
        DetachCommand.RaiseCanExecuteChanged();
        HealPartyCommand.RaiseCanExecuteChanged();
        MaxPartyCommand.RaiseCanExecuteChanged();
        GoldToPartyCommand.RaiseCanExecuteChanged();
        RestockPartyCommand.RaiseCanExecuteChanged();
        SlowerCommand.RaiseCanExecuteChanged();
        FasterCommand.RaiseCanExecuteChanged();
    }
}
