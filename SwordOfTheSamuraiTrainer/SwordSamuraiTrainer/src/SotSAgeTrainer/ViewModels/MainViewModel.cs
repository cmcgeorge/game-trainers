using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SotSAgeTrainer.Core;
using SotSAgeTrainer.Infrastructure;

namespace SotSAgeTrainer.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AgeTrainer _trainer = new();
    private readonly StringBuilder _log = new();
    private readonly Action<Action> _toUi;

    public MainViewModel()
    {
        var dispatcher = Application.Current?.Dispatcher;
        _toUi = dispatcher != null ? a => dispatcher.Invoke(a) : a => a();

        _trainer.StatusChanged += s => _toUi(() => OnStatus(s));
        _trainer.Log += line => _toUi(() => AppendLog(line));

        ApplyAgeOnceCommand  = new RelayCommand(() => _trainer.SetAgeOnce(), CanEdit);
        MaxMeCommand         = new RelayCommand(() => _trainer.MaxProtagonist(), CanEdit);
        BoostArmyCommand     = new RelayCommand(() => _trainer.BoostArmy(), CanEdit);
        CrippleRivalsCommand = new RelayCommand(() => _trainer.CrippleRivals(), CanEditRivals);
        MaxRecordCommand     = new RelayCommand<int>(i => _trainer.MaxRecord(i), _ => CanEdit());
        MinRecordCommand     = new RelayCommand<int>(i => _trainer.MinRecord(i), _ => CanEdit());

        AppendLog("Trainer ready. Launch DOSBox and load Sword of the Samurai (Restore/Continue).");
        _trainer.Start();
    }

    private bool CanEdit() => IsConnected && BlockCount > 0;
    private bool CanEditRivals() => CanEdit() && RivalCount > 0;

    // ---- age freeze ---------------------------------------------------------------------------

    public IReadOnlyList<LifeStage> TargetOptions { get; } =
        new[] { LifeStage.Youth, LifeStage.YoungAdult, LifeStage.MatureAdult, LifeStage.Old };

    private LifeStage _selectedTarget = LifeStage.Youth;
    public LifeStage SelectedTarget
    {
        get => _selectedTarget;
        set { if (Set(ref _selectedTarget, value)) _trainer.Target = value; }
    }

    private bool _isFreezing;
    public bool IsFreezing
    {
        get => _isFreezing;
        set
        {
            if (Set(ref _isFreezing, value))
            {
                _trainer.Freezing = value;
                OnPropertyChanged(nameof(FreezeButtonText));
            }
        }
    }

    public string FreezeButtonText => IsFreezing ? "❄  Freezing — click to release" : "❄  Freeze age at selected stage";

    // ---- stat trainers ------------------------------------------------------------------------

    public ICommand MaxMeCommand { get; }
    public ICommand BoostArmyCommand { get; }
    public ICommand CrippleRivalsCommand { get; }
    public ICommand MaxRecordCommand { get; }
    public ICommand MinRecordCommand { get; }
    public ICommand ApplyAgeOnceCommand { get; }

    private IReadOnlyList<CharacterRecord> _roster = Array.Empty<CharacterRecord>();
    public IReadOnlyList<CharacterRecord> Roster { get => _roster; private set => Set(ref _roster, value); }

    private int _rivalCount;
    public int RivalCount { get => _rivalCount; private set => Set(ref _rivalCount, value); }

    // ---- connection / status ------------------------------------------------------------------

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set { if (Set(ref _isConnected, value)) OnPropertyChanged(nameof(StatusBrush)); }
    }

    public Brush StatusBrush => IsConnected
        ? (BlockCount > 0 ? Brushes.LimeGreen : Brushes.Goldenrod)
        : Brushes.IndianRed;

    private string _statusText = "Starting…";
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }

    private string _connectionText = "Waiting for DOSBox…";
    public string ConnectionText { get => _connectionText; private set => Set(ref _connectionText, value); }

    private int _blockCount;
    public int BlockCount
    {
        get => _blockCount;
        private set { if (Set(ref _blockCount, value)) OnPropertyChanged(nameof(StatusBrush)); }
    }

    private string _currentAgeText = "—";
    public string CurrentAgeText { get => _currentAgeText; private set => Set(ref _currentAgeText, value); }

    private string _currentArmyText = "—";
    public string CurrentArmyText { get => _currentArmyText; private set => Set(ref _currentArmyText, value); }

    private string _addressesText = "";
    public string AddressesText { get => _addressesText; private set => Set(ref _addressesText, value); }

    public string LogText => _log.ToString();

    private void OnStatus(TrainerStatus s)
    {
        IsConnected = s.Connected;
        BlockCount = s.BlockCount;
        StatusText = s.Message;
        RivalCount = s.RivalCount;
        Roster = s.Roster;
        ConnectionText = s.Connected ? $"DOSBox connected — PID {s.ProcessId}" : "Waiting for DOSBox…";

        CurrentAgeText = s.CurrentStage is byte cur ? $"{LifeStages.Label(cur)}  (byte {cur})" : "—";
        CurrentArmyText = s.CurrentArmy is int army ? $"{army} warriors" : "—";

        AddressesText = s.Blocks.Count > 0
            ? string.Join("   ", s.Blocks.Select(b => "0x" + ((long)b.BaseAddress).ToString("X")))
            : "";

        CommandManager.InvalidateRequerySuggested();
    }

    private void AppendLog(string line)
    {
        _log.Insert(0, line + Environment.NewLine);
        if (_log.Length > 8000) _log.Remove(6000, _log.Length - 6000);
        OnPropertyChanged(nameof(LogText));
    }

    public void Dispose() => _trainer.Dispose();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
