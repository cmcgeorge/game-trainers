using System.Windows.Input;
using JumpmanLivesTrainer.Game;
using JumpmanLivesTrainer.Memory;

namespace JumpmanLivesTrainer.ViewModels;

/// <summary>
/// The located game state: editable fields bound two-way, and a live read-only mirror refreshed by
/// the poll loop. The editable buffer is a shadow of what the trainer last wrote; the live mirror
/// shows what the game currently holds, so a background refresh never fights a half-typed value.
/// </summary>
public sealed class PlayerViewModel : ObservableObject
{
    private readonly IGameHost _host;
    private readonly byte[] _globals;
    private readonly byte[] _player;
    private readonly byte[] _livePlayer = new byte[GameLayout.PlayerRecordSize];
    private readonly byte[] _liveGlobals = new byte[GameLayout.GlobalWindowLength];

    /// <summary>Live address of DGROUP:0000 in the attached process.</summary>
    public nuint DgroupAddress { get; }

    /// <summary>How the data segment was found.</summary>
    public string LocateMethod { get; }

    /// <summary>How many corroborating patterns matched.</summary>
    public int ValidatorsMatched { get; }

    /// <summary>Current player index (1–4).</summary>
    public int PlayerIndex { get; }

    public PlayerViewModel(IGameHost host, LocateResult located, int playerIndex)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!located.Found) throw new ArgumentException("Locate result holds no game state.", nameof(located));

        _host = host;
        DgroupAddress = located.DgroupAddress;
        LocateMethod = located.Method;
        ValidatorsMatched = located.ValidatorsMatched;
        PlayerIndex = playerIndex;

        _globals = new byte[GameLayout.GlobalWindowLength];
        Array.Copy(located.Globals, _globals, Math.Min(located.Globals.Length, _globals.Length));

        _player = new byte[GameLayout.PlayerRecordSize];

        MaxLivesCommand = new RelayCommand(() =>
        { Lives = GameLayout.MaxLives; _host.ReportStatus($"Lives set to {GameLayout.MaxLives}."); });

        MaxBonusCommand = new RelayCommand(() =>
        { Bonus = GameLayout.DefaultBonus; _host.ReportStatus($"Time bonus refilled to {GameLayout.DefaultBonus}."); });

        MaxScoreCommand = new RelayCommand(() =>
        { Score = 999999; _host.ReportStatus("Score set to 999,999."); });

        MaxEverythingCommand = new RelayCommand(() =>
        {
            Lives = GameLayout.MaxLives;
            Bonus = GameLayout.DefaultBonus;
            EnableTrainerMode();
            _host.ReportStatus("Lives, bonus, and trainer mode all maxed.");
        });

        EnableTrainerCommand = new RelayCommand(() =>
        { EnableTrainerMode(); _host.ReportStatus("Trainer mode enabled (21 lives on next game)."); });

        ReloadCommand = new RelayCommand(() =>
        { SyncFromLive(); _host.ReportStatus("Editable fields reloaded from the game."); });
    }

    // --- commands ------------------------------------------------------------

    public ICommand MaxLivesCommand { get; }
    public ICommand MaxBonusCommand { get; }
    public ICommand MaxScoreCommand { get; }
    public ICommand MaxEverythingCommand { get; }
    public ICommand EnableTrainerCommand { get; }
    public ICommand ReloadCommand { get; }

    // --- editable fields -----------------------------------------------------

    /// <summary>Remaining lives.</summary>
    public int Lives
    {
        get => GameLayout.ReadI8(_player, GameLayout.PlayerLives);
        set => EditPlayer(GameLayout.PlayerLives, value, 0, GameLayout.MaxLives,
            () => Lives, v => WritePlayerByte(GameLayout.PlayerLives, (sbyte)v),
            () => _pinnedLives = value, nameof(Lives));
    }

    /// <summary>Player score.</summary>
    public int Score
    {
        get => GameLayout.ReadI32(_player, GameLayout.PlayerScore);
        set => EditPlayer(GameLayout.PlayerScore, value, 0, 999999,
            () => Score, v => WritePlayerI32(GameLayout.PlayerScore, v),
            null, nameof(Score));
    }

    /// <summary>Time bonus for the current level.</summary>
    public int Bonus
    {
        get => GameLayout.ReadI32(_globals, GameLayout.OffBonus - GameLayout.GlobalWindowStart);
        set => EditGlobal(GameLayout.OffBonus, value, 0, GameLayout.DefaultBonus,
            () => Bonus, v => WriteGlobalI32(GameLayout.OffBonus, v),
            () => _pinnedBonus = value, nameof(Bonus));
    }

    /// <summary>Current level number (1–45).</summary>
    public int CurrentLevel
    {
        get => _globals[GameLayout.OffCurrentLevel - GameLayout.GlobalWindowStart];
        set => EditGlobal(GameLayout.OffCurrentLevel, value, 1, GameLayout.MaxLevel,
            () => CurrentLevel, v => WriteGlobalByte(GameLayout.OffCurrentLevel, (byte)v),
            null, nameof(CurrentLevel));
    }

    /// <summary>Game speed (1–8).</summary>
    public int Speed
    {
        get => _player[GameLayout.PlayerSpeed];
        set => EditPlayer(GameLayout.PlayerSpeed, value, GameLayout.MinSpeed, GameLayout.MaxSpeed,
            () => Speed, v => WritePlayerByte(GameLayout.PlayerSpeed, unchecked((sbyte)v)),
            null, nameof(Speed));
    }

    /// <summary>Player X position.</summary>
    public int PlayerX
    {
        get => GameLayout.ReadI16(_player, GameLayout.PlayerX);
        set => EditPlayer(GameLayout.PlayerX, value, -5, 309,
            () => PlayerX, v => WritePlayerI16(GameLayout.PlayerX, (short)v),
            null, nameof(PlayerX));
    }

    /// <summary>Player Y position.</summary>
    public int PlayerY
    {
        get => GameLayout.ReadI16(_player, GameLayout.PlayerY);
        set => EditPlayer(GameLayout.PlayerY, value, 0, 172,
            () => PlayerY, v => WritePlayerI16(GameLayout.PlayerY, (short)v),
            null, nameof(PlayerY));
    }

    /// <summary>Whether trainer mode is active.</summary>
    public bool TrainerMode
    {
        get => _globals[GameLayout.OffTrainer - GameLayout.GlobalWindowStart] != 0;
        set
        {
            WriteGlobalByte(GameLayout.OffTrainer, (byte)(value ? 1 : 0));
            OnPropertyChanged(nameof(TrainerMode));
        }
    }

    /// <summary>Enables trainer mode by writing 1 to the trainer flag.</summary>
    private void EnableTrainerMode() => TrainerMode = true;

    // --- freeze toggles ------------------------------------------------------

    private bool _freezeLives;
    private bool _freezeBonus;
    private int _pinnedLives;
    private int _pinnedBonus;

    public bool FreezeLives
    {
        get => _freezeLives;
        set { if (SetField(ref _freezeLives, value) && value)
            _pinnedLives = GameLayout.ReadI8(_livePlayer, GameLayout.PlayerLives); }
    }

    public bool FreezeBonus
    {
        get => _freezeBonus;
        set { if (SetField(ref _freezeBonus, value) && value)
            _pinnedBonus = GameLayout.ReadI32(_liveGlobals, GameLayout.OffBonus - GameLayout.GlobalWindowStart); }
    }

    // --- live mirror ---------------------------------------------------------

    /// <summary>The globals buffer the poll loop refreshes.</summary>
    public byte[] LiveGlobals => _liveGlobals;

    /// <summary>The player buffer the poll loop refreshes.</summary>
    public byte[] LivePlayer => _livePlayer;

    /// <summary>One-line summary of the live game state.</summary>
    public string LiveSummary =>
        $"Lives {GameLayout.ReadI8(_livePlayer, GameLayout.PlayerLives)}   " +
        $"Score {GameLayout.ReadI32(_livePlayer, GameLayout.PlayerScore):N0}   " +
        $"Level {_liveGlobals[GameLayout.OffCurrentLevel - GameLayout.GlobalWindowStart]}   " +
        $"Bonus {GameLayout.ReadI32(_liveGlobals, GameLayout.OffBonus - GameLayout.GlobalWindowStart):N0}   " +
        $"Speed {_livePlayer[GameLayout.PlayerSpeed]}   " +
        $"Pos ({GameLayout.ReadI16(_livePlayer, GameLayout.PlayerX)}, {GameLayout.ReadI16(_livePlayer, GameLayout.PlayerY)})";

    /// <summary>Called by the poll loop after refreshing the live buffers.</summary>
    public void OnPolled()
    {
        ApplyFreezes();
        OnPropertyChanged(nameof(LiveSummary));
    }

    private void ApplyFreezes()
    {
        if (_freezeLives)
        {
            int liveLives = GameLayout.ReadI8(_livePlayer, GameLayout.PlayerLives);
            if (liveLives != _pinnedLives)
                WritePlayerByte(GameLayout.PlayerLives, (sbyte)_pinnedLives);
        }

        if (_freezeBonus)
        {
            int liveBonus = GameLayout.ReadI32(_liveGlobals, GameLayout.OffBonus - GameLayout.GlobalWindowStart);
            if (liveBonus != _pinnedBonus)
                WriteGlobalI32(GameLayout.OffBonus, _pinnedBonus);
        }
    }

    /// <summary>Copies the live values back into the editable fields.</summary>
    public void SyncFromLive()
    {
        Array.Copy(_liveGlobals, _globals, Math.Min(_liveGlobals.Length, _globals.Length));
        Array.Copy(_livePlayer, _player, Math.Min(_livePlayer.Length, _player.Length));
        OnPropertyChanged(nameof(Lives));
        OnPropertyChanged(nameof(Score));
        OnPropertyChanged(nameof(Bonus));
        OnPropertyChanged(nameof(CurrentLevel));
        OnPropertyChanged(nameof(Speed));
        OnPropertyChanged(nameof(PlayerX));
        OnPropertyChanged(nameof(PlayerY));
        OnPropertyChanged(nameof(TrainerMode));
    }

    // --- plumbing ------------------------------------------------------------

    private void EditPlayer(int off, int value, int min, int max,
        Func<int> get, Action<int> set, Action? pin, string propName)
    {
        int clamped = Math.Clamp(value, min, max);
        if (clamped != value)
            _host.ReportStatus($"{propName} clamped from {value} to {clamped}.");

        if (get() == clamped)
        {
            if (clamped != value) OnPropertyChanged(propName);
            return;
        }
        set(clamped);
        pin?.Invoke();
        OnPropertyChanged(propName);
    }

    private void EditGlobal(int dgroupOff, int value, int min, int max,
        Func<int> get, Action<int> set, Action? pin, string propName)
    {
        int clamped = Math.Clamp(value, min, max);
        if (clamped != value)
            _host.ReportStatus($"{propName} clamped from {value} to {clamped}.");

        if (get() == clamped)
        {
            if (clamped != value) OnPropertyChanged(propName);
            return;
        }
        set(clamped);
        pin?.Invoke();
        OnPropertyChanged(propName);
    }

    private void WritePlayerByte(int off, sbyte v)
    {
        int addr = GameLayout.PlayerArrayOffset + (PlayerIndex - 1) * GameLayout.PlayerRecordSize + off;
        if (_host.WriteBytes(addr, new byte[] { (byte)v }))
            _player[off] = (byte)v;
        else
            _host.ReportStatus("Write failed — the game may have exited.");
    }

    private void WritePlayerI16(int off, short v)
    {
        var bytes = new byte[2];
        bytes[0] = (byte)v;
        bytes[1] = (byte)(v >> 8);
        int addr = GameLayout.PlayerArrayOffset + (PlayerIndex - 1) * GameLayout.PlayerRecordSize + off;
        if (_host.WriteBytes(addr, bytes))
            GameLayout.WriteI16(_player, off, v);
        else
            _host.ReportStatus("Write failed — the game may have exited.");
    }

    private void WritePlayerI32(int off, int v)
    {
        var bytes = new byte[4];
        bytes[0] = (byte)v;
        bytes[1] = (byte)(v >> 8);
        bytes[2] = (byte)(v >> 16);
        bytes[3] = (byte)(v >> 24);
        int addr = GameLayout.PlayerArrayOffset + (PlayerIndex - 1) * GameLayout.PlayerRecordSize + off;
        if (_host.WriteBytes(addr, bytes))
            GameLayout.WriteI32(_player, off, v);
        else
            _host.ReportStatus("Write failed — the game may have exited.");
    }

    private void WriteGlobalByte(int dgroupOff, byte v)
    {
        if (_host.WriteBytes(dgroupOff, new byte[] { v }))
        {
            int rel = dgroupOff - GameLayout.GlobalWindowStart;
            _globals[rel] = v;
        }
        else
            _host.ReportStatus("Write failed — the game may have exited.");
    }

    private void WriteGlobalI32(int dgroupOff, int v)
    {
        var bytes = new byte[4];
        bytes[0] = (byte)v;
        bytes[1] = (byte)(v >> 8);
        bytes[2] = (byte)(v >> 16);
        bytes[3] = (byte)(v >> 24);
        if (_host.WriteBytes(dgroupOff, bytes))
        {
            int rel = dgroupOff - GameLayout.GlobalWindowStart;
            GameLayout.WriteI32(_globals, rel, v);
        }
        else
            _host.ReportStatus("Write failed — the game may have exited.");
    }
}
