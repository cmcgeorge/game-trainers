using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using KeefTrainer.Core;

namespace KeefTrainer.UI;

public sealed class StatGroup(string title, params StatRowViewModel[] rows)
{
    public string Title { get; } = title;
    public ObservableCollection<StatRowViewModel> Rows { get; } = new(rows);
}

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly TrainerEngine _engine = new();
    private readonly DispatcherTimer _pollTimer;
    private readonly DispatcherTimer _attachTimer;
    private string _statusText = "Searching for DOSBox…";
    private bool _isAttached;
    private bool _inGame;

    public ObservableCollection<StatGroup> Groups { get; } = new();

    public RelayCommand MaxStatsCommand { get; }
    public RelayCommand RefillCommand { get; }
    public RelayCommand MaxGoldCommand { get; }
    public RelayCommand ThiefKitCommand { get; }
    public RelayCommand GodModeCommand { get; }
    public RelayCommand ReattachCommand { get; }

    private readonly Dictionary<KeefField, StatRowViewModel> _rows = new();

    public MainViewModel()
    {
        StatRowViewModel Row(KeefField f, string name, string? hint = null)
        {
            var row = new StatRowViewModel(_engine, f, name, hint);
            _rows[f] = row;
            return row;
        }

        Groups.Add(new StatGroup("Attributes",
            Row(KeefField.Strength, "Strength"),
            Row(KeefField.Speed, "Speed"),
            Row(KeefField.Constitution, "Constitution", "also Max HP"),
            Row(KeefField.Wisdom, "Wisdom", "also Max MP"),
            Row(KeefField.Luck, "Luck"),
            Row(KeefField.Charisma, "Charisma"),
            Row(KeefField.HiddenStat, "Hidden stat", "rolled but never shown")));

        Groups.Add(new StatGroup("Thief Skills",
            Row(KeefField.Disarming, "Disarming"),
            Row(KeefField.Stealing, "Stealing"),
            Row(KeefField.Unlocking, "Unlocking")));

        Groups.Add(new StatGroup("Survival",
            Row(KeefField.Nutrition, "Nutrition", "0 = starvation"),
            Row(KeefField.Sobriety, "Sobriety"),
            Row(KeefField.Sleep, "Sleep", "0 = exhaustion")));

        Groups.Add(new StatGroup("Resources",
            Row(KeefField.Gold, "Gold", "cap 9999"),
            Row(KeefField.HitPoints, "Hit Points", "cap 999"),
            Row(KeefField.MagicPoints, "Magic Points"),
            Row(KeefField.Experience, "Experience", "level-up on next kill"),
            Row(KeefField.Level, "Level", "cap 24")));

        Groups.Add(new StatGroup("Equipment (equipped item)",
            Row(KeefField.WeaponStrength, "Weapon Strength", "reset on re-equip"),
            Row(KeefField.WeaponSpeed, "Weapon Speed"),
            Row(KeefField.WeaponRange, "Weapon Range", "shown as ×6 ft"),
            Row(KeefField.ArmorStrength, "Armor Strength"),
            Row(KeefField.ArmorSpeed, "Armor Speed")));

        Groups.Add(new StatGroup("Thief Consumables",
            Row(KeefField.LockPicks, "Lock Picks"),
            Row(KeefField.Flints, "Flints")));

        MaxStatsCommand = new RelayCommand(MaxStats, () => IsAttached);
        RefillCommand = new RelayCommand(Refill, () => IsAttached);
        MaxGoldCommand = new RelayCommand(() => _rows[KeefField.Gold].Apply(9999), () => IsAttached);
        ThiefKitCommand = new RelayCommand(ThiefKit, () => IsAttached);
        GodModeCommand = new RelayCommand(GodMode, () => IsAttached);
        ReattachCommand = new RelayCommand(() => { _engine.Detach(); TryAttach(); });

        _engine.AttachmentChanged += (_, _) => OnAttachmentChanged();

        _pollTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };
        _pollTimer.Tick += (_, _) => Poll();
        _pollTimer.Start();

        _attachTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        _attachTimer.Tick += (_, _) => { if (!_engine.IsAttached) TryAttach(); };
        _attachTimer.Start();

        TryAttach();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged([CallerMemberName] string? p = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public string StatusText
    {
        get => _statusText;
        private set { if (_statusText != value) { _statusText = value; OnChanged(); } }
    }

    public bool IsAttached
    {
        get => _isAttached;
        private set
        {
            if (_isAttached == value) return;
            _isAttached = value;
            OnChanged();
            foreach (var c in new[] { MaxStatsCommand, RefillCommand, MaxGoldCommand, ThiefKitCommand, GodModeCommand })
                c.RaiseCanExecuteChanged();
        }
    }

    /// <summary>False when attached but the table looks like the title screen.</summary>
    public bool InGame
    {
        get => _inGame;
        private set { if (_inGame != value) { _inGame = value; OnChanged(); } }
    }

    private void TryAttach()
    {
        _engine.TryAttach();
        OnAttachmentChanged();
    }

    private void OnAttachmentChanged()
    {
        IsAttached = _engine.IsAttached;
        StatusText = _engine.StatusText;
    }

    private void Poll()
    {
        if (!_engine.IsAttached) return;
        var snap = _engine.Tick();
        StatusText = _engine.StatusText;
        IsAttached = _engine.IsAttached;
        if (snap is null) return;
        InGame = snap.LooksInGame;
        foreach (var row in _rows.Values)
            row.UpdateFromSnapshot(snap);
    }

    // ------------------------------------------------------------- presets

    private void MaxStats()
    {
        foreach (var f in new[]
                 {
                     KeefField.HiddenStat, KeefField.Strength, KeefField.Speed, KeefField.Constitution,
                     KeefField.Wisdom, KeefField.Luck, KeefField.Charisma,
                     KeefField.Disarming, KeefField.Stealing, KeefField.Unlocking,
                 })
            _rows[f].Apply(100);
    }

    private void Refill()
    {
        // HP refills to Constitution, MP to Wisdom (their in-game maximums).
        _rows[KeefField.HitPoints].Apply(_rows[KeefField.Constitution].Value);
        _rows[KeefField.MagicPoints].Apply(_rows[KeefField.Wisdom].Value);
        _rows[KeefField.Nutrition].Apply(100);
        _rows[KeefField.Sobriety].Apply(100);
        _rows[KeefField.Sleep].Apply(100);
    }

    private void ThiefKit()
    {
        _rows[KeefField.LockPicks].Apply(99);
        _rows[KeefField.Flints].Apply(99);
    }

    private void GodMode()
    {
        foreach (var (f, v) in new[]
                 {
                     (KeefField.HitPoints, 999),
                     (KeefField.MagicPoints, 999),
                     (KeefField.Nutrition, 100),
                     (KeefField.Sobriety, 100),
                     (KeefField.Sleep, 100),
                 })
        {
            _rows[f].Apply(v);
            _rows[f].IsFrozen = true;
        }
    }

    public void Dispose()
    {
        _pollTimer.Stop();
        _attachTimer.Stop();
        _engine.Dispose();
    }
}
