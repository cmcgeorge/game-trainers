using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using RedBaronTrainer.Game;
using RedBaronTrainer.Memory;

namespace RedBaronTrainer.ViewModels;

/// <summary>
/// Drives the whole window: attaching to the emulator, keeping the located structures fresh, and
/// applying edits to live memory and to the game's preference files.
///
/// <para>The trainer follows the game between its two executables. <c>PS.EXE</c> and <c>RB.EXE</c>
/// hand control back and forth, and each chain replaces the process in the guest, so a locate that
/// succeeded a moment ago can stop being true without the emulator going away. Two things follow
/// from that, and both matter: the poll loop re-checks the anchor every tick and re-runs the full
/// sweep when it no longer matches, and — because there is always a window between the game moving
/// and the next tick noticing — every write re-checks the anchor immediately before committing
/// rather than trusting an address that was validated up to a second ago.</para>
/// </summary>
public sealed class MainViewModel : ObservableObject, IDisposable
{
    private const int PollIntervalMs = 750;

    /// <summary>
    /// How long to wait before trying again after a locate fails. The sweep is a byte scan over
    /// megabytes of guest RAM on the UI thread, so running it at the poll rate while the game is at
    /// a DOS prompt would make the window sluggish for no benefit.
    /// </summary>
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(3);

    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _sinceLastSweep = Stopwatch.StartNew();
    private ProcessMemory? _mem;
    private ProcessMemorySource? _source;
    private Process? _emulator;
    private LocatedGame? _game;
    private GameFolder? _folder;

    /// <summary>True once the located addresses stopped being trustworthy and a re-locate has not yet succeeded.</summary>
    private bool _stale;

    private bool _suppressRealismEdits;
    private bool _realismEdited;
    private bool _disposed;

    public MainViewModel()
    {
        AttachCommand = new RelayCommand(_ => Attach());
        DetachCommand = new RelayCommand(_ => Detach(), _ => IsAttached);
        ApplyRealismToGameCommand = new RelayCommand(_ => ApplyRealismToGame(), _ => CanWriteLiveRealism);
        ApplyRealismToMissionFileCommand = new RelayCommand(_ => ApplyRealismToFile(career: false), _ => HasGameFolder);
        ApplyRealismToCareerFileCommand = new RelayCommand(_ => ApplyRealismToFile(career: true), _ => HasGameFolder);
        ReadRealismFromMissionFileCommand = new RelayCommand(_ => ReadRealismFromFile(career: false), _ => HasGameFolder);
        ReadRealismFromCareerFileCommand = new RelayCommand(_ => ReadRealismFromFile(career: true), _ => HasGameFolder);
        ApplyPresetCommand = new RelayCommand(p => ApplyPreset(p as string));
        ApplyPilotNamesCommand = new RelayCommand(_ => ApplyPilotNames(), _ => CanWritePilots);
        ReloadCommand = new RelayCommand(_ => Refresh(force: true), _ => IsAttached);
        ToggleJoystickCommand = new RelayCommand(_ => ToggleJoystick(), _ => CanToggleJoystick);
        RefreshDiagnosticsCommand = new RelayCommand(_ => RefreshDiagnostics());
        BrowseGameFolderCommand = new RelayCommand(_ => BrowseGameFolder());

        foreach (var setting in RealismSettings.All)
            Realism.Add(new RealismSettingViewModel(setting, 0, OnRealismEdited));

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(PollIntervalMs),
        };
        _timer.Tick += (_, _) => Tick();

        RefreshDiagnostics();
        Attach();
        _timer.Start();
    }

    // ------------------------------------------------------------------ commands

    public RelayCommand AttachCommand { get; }
    public RelayCommand DetachCommand { get; }
    public RelayCommand ApplyRealismToGameCommand { get; }
    public RelayCommand ApplyRealismToMissionFileCommand { get; }
    public RelayCommand ApplyRealismToCareerFileCommand { get; }
    public RelayCommand ReadRealismFromMissionFileCommand { get; }
    public RelayCommand ReadRealismFromCareerFileCommand { get; }
    public RelayCommand ApplyPresetCommand { get; }
    public RelayCommand ApplyPilotNamesCommand { get; }
    public RelayCommand ReloadCommand { get; }
    public RelayCommand ToggleJoystickCommand { get; }
    public RelayCommand RefreshDiagnosticsCommand { get; }
    public RelayCommand BrowseGameFolderCommand { get; }

    // ------------------------------------------------------------------ state

    public ObservableCollection<RealismSettingViewModel> Realism { get; } = new();
    public ObservableCollection<PilotViewModel> Pilots { get; } = new();
    public ObservableCollection<HostJoystick> HostJoysticks { get; } = new();
    public ObservableCollection<ConfigFinding> ConfigFindings { get; } = new();

    private string _status = "Starting up...";
    public string Status { get => _status; private set => SetField(ref _status, value); }

    private string _attachment = "Not attached.";
    public string Attachment { get => _attachment; private set => SetField(ref _attachment, value); }

    private string _joystickSummary = "";
    public string JoystickSummary { get => _joystickSummary; private set => SetField(ref _joystickSummary, value); }

    private string? _gameFolderPath;
    public string? GameFolderPath
    {
        get => _gameFolderPath;
        private set
        {
            if (!SetField(ref _gameFolderPath, value)) return;
            _folder = value != null ? new GameFolder(value) : null;
            OnPropertyChanged(nameof(HasGameFolder));
            OnPropertyChanged(nameof(GameFolderDisplay));
            RaiseCommandStates();
        }
    }

    public bool HasGameFolder => _folder != null;

    public string GameFolderDisplay => _gameFolderPath ?? "(not found - use Browse to point at the folder holding RB.EXE)";

    public bool IsAttached => _game != null;

    public GameModule Module => _stale ? GameModule.None : _game?.Module ?? GameModule.None;

    public bool IsShell => Module == GameModule.Shell;
    public bool IsSimulator => Module == GameModule.Simulator;

    public bool CanWriteLiveRealism => !_stale && _game is { RealismAddress: not 0 };
    public bool CanWritePilots => !_stale && _game is { RosterAddress: not 0 } or { ActivePilotAddress: not 0 };
    public bool CanToggleJoystick => !_stale && _game is { JoystickFlagAddress: not 0 };

    private bool _joystickEnabled;
    public bool JoystickEnabled { get => _joystickEnabled; private set => SetField(ref _joystickEnabled, value); }

    private string _realismSource = "not read yet";
    public string RealismSource { get => _realismSource; private set => SetField(ref _realismSource, value); }

    // ------------------------------------------------------------------ attach

    private void Attach()
    {
        Detach();
        _sinceLastSweep.Restart();

        var emulators = DosBoxInspector.FindEmulators();
        try
        {
            if (emulators.Count == 0)
            {
                Status = "No DOSBox process is running. Start Red Baron (BARON.COM) in DOSBox or DOSBox-X; "
                       + "the trainer keeps looking.";
                return;
            }

            var failures = new List<string>();
            foreach (var proc in emulators)
            {
                ProcessMemory? mem = null;
                try
                {
                    mem = ProcessMemory.Open(proc.Id);
                    var source = new ProcessMemorySource(mem);
                    var located = GameLocator.Find(source, out string why);
                    if (located == null)
                    {
                        failures.Add($"pid {proc.Id}: {why}");
                        mem.Dispose();
                        continue;
                    }

                    // Publish only once everything that can throw has run, so a failure part-way
                    // through cannot leave the view model claiming an attach it does not have.
                    string attachment = $"{proc.ProcessName} (pid {proc.Id}) - {DescribeModule(located.Module)}, "
                                      + $"DS {located.DgroupSegment:X4}, {located.ValidatorsMatched} validators";
                    DiscoverGameFolder(proc);

                    _mem = mem;
                    _source = source;
                    _emulator = Process.GetProcessById(proc.Id);
                    _game = located;
                    _stale = false;
                    Attachment = attachment;
                    Status = why;
                    LoadFromGame();
                    NotifyAttachmentChanged();
                    return;
                }
                catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException
                                            or ArgumentException)
                {
                    failures.Add($"pid {proc.Id}: {e.Message}");
                    mem?.Dispose();
                    _mem = null;
                    _source = null;
                    _game = null;
                }
            }

            Status = "Red Baron was not found in any running emulator. " + string.Join("  |  ", failures);
        }
        finally
        {
            foreach (var p in emulators) p.Dispose();
        }
    }

    private static string DescribeModule(GameModule module) => module switch
    {
        GameModule.Shell => "shell / career (PS.EXE)",
        GameModule.Simulator => "simulator (RB.EXE)",
        _ => "unknown",
    };

    private void Detach()
    {
        _mem?.Dispose();
        _mem = null;
        _source = null;
        _emulator?.Dispose();
        _emulator = null;
        _game = null;
        _stale = false;
        Pilots.Clear();
        Attachment = "Not attached.";
        JoystickEnabled = false;
        NotifyAttachmentChanged();
    }

    private void NotifyAttachmentChanged()
    {
        OnPropertyChanged(nameof(IsAttached));
        OnPropertyChanged(nameof(Module));
        OnPropertyChanged(nameof(IsShell));
        OnPropertyChanged(nameof(IsSimulator));
        OnPropertyChanged(nameof(CanWriteLiveRealism));
        OnPropertyChanged(nameof(CanWritePilots));
        OnPropertyChanged(nameof(CanToggleJoystick));
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        DetachCommand.RaiseCanExecuteChanged();
        ApplyRealismToGameCommand.RaiseCanExecuteChanged();
        ApplyRealismToMissionFileCommand.RaiseCanExecuteChanged();
        ApplyRealismToCareerFileCommand.RaiseCanExecuteChanged();
        ReadRealismFromMissionFileCommand.RaiseCanExecuteChanged();
        ReadRealismFromCareerFileCommand.RaiseCanExecuteChanged();
        ApplyPilotNamesCommand.RaiseCanExecuteChanged();
        ReloadCommand.RaiseCanExecuteChanged();
        ToggleJoystickCommand.RaiseCanExecuteChanged();
    }

    private void DiscoverGameFolder(Process emulator)
    {
        // The config is re-read every attach even when the folder is already known, because the
        // Joystick tab's findings come from it and that tab is usually why the trainer is open.
        string? conf = DosBoxInspector.FindConfigFile(emulator);
        if (!HasGameFolder)
        {
            string? folder = DosBoxInspector.FindGameFolder(conf);
            if (folder != null) GameFolderPath = folder;
        }
        RefreshDiagnostics(conf);
    }

    // ------------------------------------------------------------------ polling

    private void Tick()
    {
        if (_game == null)
        {
            // Not attached: keep looking, but at the retry rate rather than the poll rate.
            if (_sinceLastSweep.Elapsed >= RetryInterval) Attach();
            return;
        }
        Refresh(force: false);
    }

    /// <summary>
    /// Re-reads the live values. When <paramref name="force"/> is false this is the background tick,
    /// so it does the cheap check first: if the anchor is still where it was, nothing moved.
    /// </summary>
    private void Refresh(bool force)
    {
        if (_source == null || _mem == null || _game == null) return;

        if (_emulator is { HasExited: true })
        {
            Detach();
            Status = "The emulator exited. The trainer will attach again when it comes back.";
            return;
        }

        if (!force && AnchorIntact())
        {
            // Coming back from stale: whatever made the addresses untrustworthy has passed, so put
            // the write commands back rather than leaving the trainer inert until the user notices.
            if (_stale)
            {
                _stale = false;
                Status = $"Red Baron's {(_game.Module == GameModule.Simulator ? "simulator" : "shell")} is back.";
                NotifyAttachmentChanged();
                LoadFromGame();
                return;
            }

            // The structures live in BSS, which is zeroed at load, so a locate that happened a moment
            // too early resolves the data group but none of its contents - and the anchor never stops
            // matching, so nothing would ever try again. Re-resolve (cheaply, no sweep) until they
            // turn up.
            if (GameLocator.HasUnresolvedStructures(_game))
            {
                var again = GameLocator.Reresolve(_source, _game);
                if (!GameLocator.HasUnresolvedStructures(again) || again.RosterAddress != _game.RosterAddress
                    || again.RealismAddress != _game.RealismAddress
                    || again.ActivePilotAddress != _game.ActivePilotAddress
                    || again.JoystickFlagAddress != _game.JoystickFlagAddress)
                {
                    _game = again;
                    NotifyAttachmentChanged();
                    LoadFromGame();
                    return;
                }
            }

            ReadLiveJoystick();
            ReadLivePilots(refreshOnly: true);
            return;
        }

        if (!force && _sinceLastSweep.Elapsed < RetryInterval)
        {
            // The anchor is gone but the last sweep was very recent - the game is mid-chain. Mark the
            // addresses untrustworthy so nothing writes through them, and wait rather than re-sweeping
            // megabytes of guest RAM four times a second.
            MarkStale("Red Baron is between programs; waiting for it to settle.");
            return;
        }

        _sinceLastSweep.Restart();
        var located = GameLocator.Find(_source, out string why);
        if (located == null)
        {
            MarkStale(why);
            return;
        }

        _game = located;
        _stale = false;
        Status = why;
        NotifyAttachmentChanged();
        LoadFromGame();
    }

    private bool AnchorIntact() =>
        _source != null && _game != null && GameLocator.AnchorStillMatches(_source, _game);

    private void MarkStale(string reason)
    {
        Status = reason;
        if (_stale) return;
        _stale = true;
        Pilots.Clear();
        JoystickEnabled = false;
        NotifyAttachmentChanged();
    }

    private void LoadFromGame()
    {
        ReadLiveRealism();
        ReadLiveJoystick();
        ReadLivePilots(refreshOnly: false);
    }

    // ------------------------------------------------------------------ realism

    private void ReadLiveRealism()
    {
        if (_source == null || _game == null) return;

        if (_game.RealismAddress == 0)
        {
            // RB.EXE reads ?REAL.PRF but does not keep the thirteen values together anywhere in its
            // data group - a search of the whole 64 KB for the on-disk vector finds nothing - so the
            // sim has no live block to offer. The file buttons are the route there.
            RealismSource = _game.Module == GameModule.Simulator
                ? "the simulator keeps no live copy - read it from MREAL.PRF/CREAL.PRF instead"
                : "not located";
            return;
        }

        var values = RealismSettings.Decode(_source.Read(_game.RealismAddress, GameFacts.RealismBlockSize));
        if (values == null) { RealismSource = "live block did not validate"; return; }

        if (_realismEdited)
        {
            // The panel is a staging area: presets and tick-box changes sit there until a Write
            // button is pressed. A background re-read must not throw those away.
            RealismSource = $"live memory at 0x{(ulong)_game.RealismAddress:X} (edited, not written)";
            return;
        }

        SetRealism(values);
        RealismSource = $"live memory at 0x{(ulong)_game.RealismAddress:X}";
    }

    private void SetRealism(IReadOnlyList<ushort> values)
    {
        _suppressRealismEdits = true;
        try
        {
            for (int i = 0; i < Realism.Count && i < values.Count; i++)
                Realism[i].SetQuietly(values[i]);
        }
        finally { _suppressRealismEdits = false; }
        _realismEdited = false;
    }

    private ushort[] CurrentRealism()
    {
        var values = new ushort[GameFacts.RealismSettingCount];
        for (int i = 0; i < values.Length && i < Realism.Count; i++) values[i] = Realism[i].Value;
        return values;
    }

    private void OnRealismEdited()
    {
        if (_suppressRealismEdits || _realismEdited) return;
        _realismEdited = true;
        RealismSource += " (edited, not written)";
    }

    private void ApplyPreset(string? name)
    {
        var preset = name switch
        {
            "novice" => RealismSettings.Novice,
            "expert" => RealismSettings.Expert,
            "invulnerable" => RealismSettings.Invulnerable,
            _ => null,
        };
        if (preset == null) return;
        SetRealism(preset);
        _realismEdited = true;
        RealismSource = $"{name} preset (not written yet)";
    }

    private void ApplyRealismToGame()
    {
        if (_source == null || _game == null) return;
        if (_game.RealismAddress == 0) { Status = "The live realism panel is not located, so it was not written."; return; }
        if (!AnchorIntact())
        {
            MarkStale("The game moved before the write went out; nothing was written. Wait for it to settle, then try again.");
            return;
        }

        nuint address = _game.RealismAddress;
        var block = RealismSettings.Encode(CurrentRealism());
        if (_source.Write(address, block))
        {
            Status = $"Wrote the realism panel to live memory at 0x{(ulong)address:X}.";
            _realismEdited = false;
        }
        else
        {
            Status = "Writing the live realism panel failed - is the trainer running as administrator?";
        }
        ReadLiveRealism();
    }

    private void ApplyRealismToFile(bool career)
    {
        if (_folder == null) return;
        string name = career ? GameFacts.CareerRealismFileName : GameFacts.MissionRealismFileName;
        try
        {
            _folder.BackUpOnce(name);
            _folder.WriteRealism(career, CurrentRealism());
            _realismEdited = false;
            Status = $"Wrote {name}. The simulator re-reads it when the next mission starts.";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not write {name}: {e.Message}";
        }
    }

    private void ReadRealismFromFile(bool career)
    {
        if (_folder == null) return;
        string name = career ? GameFacts.CareerRealismFileName : GameFacts.MissionRealismFileName;
        var values = _folder.ReadRealism(career);
        if (values == null) { Status = $"Could not read {name}."; return; }
        SetRealism(values);
        RealismSource = name;
        Status = $"Read the realism panel from {name}.";
    }

    // ------------------------------------------------------------------ pilots

    private void ReadLivePilots(bool refreshOnly)
    {
        if (_source == null || _game == null || _game.Module != GameModule.Shell || _stale)
        {
            if (!refreshOnly) Pilots.Clear();
            return;
        }

        var wanted = new List<(int Slot, PilotRecord Record, bool Active)>();

        if (_game.ActivePilotAddress != 0)
        {
            var buf = _source.Read(_game.ActivePilotAddress, GameFacts.PilotRecordSize);
            if (buf.Length == GameFacts.PilotRecordSize && PilotRecord.IsOccupiedSlot(buf, 0))
                wanted.Add((-1, new PilotRecord(buf), true));
        }

        if (_game.RosterAddress != 0)
        {
            int bytes = GameFacts.RosterSlots * GameFacts.PilotRecordSize;
            var buf = _source.Read(_game.RosterAddress, bytes);
            if (buf.Length == bytes)
            {
                for (int slot = 0; slot < GameFacts.RosterSlots; slot++)
                {
                    int off = slot * GameFacts.PilotRecordSize;
                    if (!PilotRecord.IsOccupiedSlot(buf, off)) continue;
                    wanted.Add((slot, new PilotRecord(buf.AsSpan(off, GameFacts.PilotRecordSize)), false));
                }
            }
        }

        // On the background tick, refresh in place so a half-typed name is not thrown away. The
        // record handed to Reload is the one that was read, not a round-trip through ToRecord() -
        // the hex dump is the trainer's evidence surface for the bytes it will not label, so it has
        // to show what is actually in guest memory.
        if (refreshOnly && Pilots.Count == wanted.Count)
        {
            bool sameShape = true;
            for (int i = 0; i < Pilots.Count; i++)
                if (Pilots[i].Slot != wanted[i].Slot) { sameShape = false; break; }
            if (sameShape)
            {
                for (int i = 0; i < Pilots.Count; i++)
                    if (!Pilots[i].IsDirty) Pilots[i].Reload(wanted[i].Record);
                return;
            }
        }

        Pilots.Clear();
        foreach (var (slot, record, active) in wanted)
            Pilots.Add(new PilotViewModel(slot, record, active));
    }

    private void ApplyPilotNames()
    {
        if (_source == null || _game == null || _game.Module != GameModule.Shell) return;

        var dirty = Pilots.Where(p => p.IsDirty).ToList();
        if (dirty.Count == 0) { Status = "No pilot names had been changed."; return; }

        var rejected = dirty.Where(p => !PilotRecord.IsWritableName(p.Name)).ToList();
        if (rejected.Count > 0)
        {
            // A blank or non-printable name would clear the slot's first byte, which is what the
            // shell reads as "free" - the career state behind it would survive but become
            // unreachable, and the next career created would reuse the slot.
            Status = $"Not written: {string.Join(", ", rejected.Select(p => p.SlotLabel))} "
                   + "must have a name of 1-17 printable ASCII characters.";
            return;
        }

        if (!AnchorIntact())
        {
            MarkStale("The game moved before the write went out; nothing was written. Wait for it to settle, then try again.");
            return;
        }

        int written = 0, failed = 0;
        foreach (var pilot in dirty)
        {
            nuint address = pilot.IsActiveCareer
                ? _game.ActivePilotAddress
                : _game.RosterAddress == 0 ? 0 : _game.RosterAddress + (nuint)(pilot.Slot * GameFacts.PilotRecordSize);
            if (address == 0) { failed++; continue; }

            // Re-read and re-validate the slot rather than trusting the locate: this is the last
            // moment before the bytes go out, and the roster is next door to state the game rewrites.
            var current = _source.Read(address, GameFacts.PilotRecordSize);
            if (current.Length != GameFacts.PilotRecordSize || !PilotRecord.IsOccupiedSlot(current, 0))
            {
                failed++;
                continue;
            }

            var record = pilot.ToRecord();
            // Only the name field is ours to change; writing the whole record would put back a stale
            // copy of everything the game has updated since the last read.
            var name = record.ToArray().AsSpan(0, GameFacts.PilotNameLength).ToArray();
            if (_source.Write(address, name))
            {
                written++;
                var refreshed = _source.Read(address, GameFacts.PilotRecordSize);
                pilot.Reload(refreshed.Length == GameFacts.PilotRecordSize ? new PilotRecord(refreshed) : record);
            }
            else failed++;
        }

        Status = failed == 0
            ? $"Wrote {written} pilot name(s)."
            : $"Wrote {written} pilot name(s); {failed} failed - the slot moved, or the trainer is not "
            + "running as administrator.";
    }

    // ------------------------------------------------------------------ simulator

    private void ReadLiveJoystick()
    {
        if (_source == null || _game == null || _game.JoystickFlagAddress == 0 || _stale)
        {
            JoystickEnabled = false;
            return;
        }
        var b = _source.Read(_game.JoystickFlagAddress, 1);
        JoystickEnabled = b.Length == 1 && b[0] != 0;
    }

    private void ToggleJoystick()
    {
        if (_source == null || _game == null || _game.JoystickFlagAddress == 0) return;
        if (!AnchorIntact())
        {
            MarkStale("The game moved before the write went out; nothing was written. Wait for it to settle, then try again.");
            return;
        }

        byte value = JoystickEnabled ? (byte)0 : (byte)1;
        bool ok = _source.Write(_game.JoystickFlagAddress, new[] { value });
        if (_game.JoystickMirrorAddress != 0)
            ok &= _source.Write(_game.JoystickMirrorAddress, new[] { value });
        Status = ok
            ? $"Stick and rudder {(value != 0 ? "enabled" : "disabled")} in the simulator."
            : "Writing the joystick flag failed - is the trainer running as administrator?";
        ReadLiveJoystick();
    }

    // ------------------------------------------------------------------ diagnostics

    private void RefreshDiagnostics(string? configFile = null)
    {
        var sticks = JoystickProbe.Enumerate();
        HostJoysticks.Clear();
        foreach (var s in sticks) HostJoysticks.Add(s);

        int first = JoystickProbe.FirstPresentId(sticks);
        JoystickSummary = first switch
        {
            -1 => "Windows reports no joystick on any slot. Plug the controller in before starting the emulator "
                + "- SDL only enumerates sticks at start-up.",
            0 => "A stick is present on slot 0, which is the slot DOSBox binds first.",
            _ => $"The only stick is on slot {first}; slot 0 is empty. Windows never compacts these IDs, so a "
               + "controller that was replugged, or a second one, ends up here. Re-pair or replug it as the "
               + "only controller so it lands on slot 0, or bind it by hand in the emulator's mapper.",
        };

        if (configFile == null)
        {
            var emulators = DosBoxInspector.FindEmulators();
            try
            {
                if (emulators.Count > 0) configFile = DosBoxInspector.FindConfigFile(emulators[0]);
            }
            finally
            {
                foreach (var p in emulators) p.Dispose();
            }
        }

        ConfigFindings.Clear();
        foreach (var f in DosBoxInspector.CheckConfig(configFile)) ConfigFindings.Add(f);
    }

    private void BrowseGameFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select the folder holding RB.EXE",
        };
        if (dialog.ShowDialog() != true) return;
        if (!GameFolder.IsGameFolder(dialog.FolderName))
        {
            Status = $"{dialog.FolderName} does not look like a Red Baron folder (no RB.EXE / PS.EXE / VOLUME.RMF).";
            return;
        }
        GameFolderPath = dialog.FolderName;
        Status = $"Using the game folder {dialog.FolderName}.";
    }

    // ------------------------------------------------------------------ teardown

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _mem?.Dispose();
        _mem = null;
        _source = null;
        _emulator?.Dispose();
        _emulator = null;
        _game = null;
    }
}
