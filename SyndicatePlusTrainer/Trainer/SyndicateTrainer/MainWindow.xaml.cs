using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SyndicateTrainer;

public partial class MainWindow : Window
{
    private readonly GameConnection _game = new();
    private readonly DispatcherTimer _timer;
    private bool _freeze;
    private int _freezeValue;
    private bool _godMode;
    private bool _infiniteAmmo;
    private bool _noRecoil;
    private TextBlock[] _hpLabels = Array.Empty<TextBlock>();

    public MainWindow()
    {
        InitializeComponent();

        _hpLabels = new[] { Hp1, Hp2, Hp3, Hp4 };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += Timer_Tick;
        _timer.Start();

        Loaded += (_, _) => PopulateProcesses();
        Closed += (_, _) => { _timer.Stop(); _game.Dispose(); };
    }

    // ---- Process list ----

    private void PopulateProcesses()
    {
        ProcessCombo.Items.Clear();
        foreach (var p in GameConnection.FindDosBoxProcesses())
            ProcessCombo.Items.Add(new ProcessItem(p));

        if (ProcessCombo.Items.Count > 0)
        {
            ProcessCombo.SelectedIndex = 0;
            Log($"Found {ProcessCombo.Items.Count} DOSBox process(es). Select one and click Attach.");
        }
        else
        {
            Log("No DOSBox / DOSBox-X process found. Start Syndicate Plus first, then click Refresh.");
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => PopulateProcesses();

    // ---- Attach ----

    private async void AttachButton_Click(object sender, RoutedEventArgs e)
    {
        if (ProcessCombo.SelectedItem is not ProcessItem item)
        {
            Log("Select a DOSBox process first.");
            return;
        }

        AttachButton.IsEnabled = false;
        SetStatus(false, "Scanning...");
        Log($"Attaching to {item.Process.ProcessName} (PID {item.Process.Id})...");

        bool ok = false;
        try
        {
            // Scan on a background thread so the UI stays responsive.
            ok = await Task.Run(() => _game.Attach(item.Process, msg =>
                Dispatcher.Invoke(() => Log(msg))));
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
        }

        if (ok)
        {
            SetStatus(true, $"Attached • base 0x{_game.GameBase:X} • money @ 0x{_game.MoneyAddress:X}");
            EnableCheats(true);
        }
        else
        {
            SetStatus(false, "Not attached");
            EnableCheats(false);
        }
        AttachButton.IsEnabled = true;
    }

    private void EnableCheats(bool on)
    {
        SetButton.IsEnabled = on;
        Add100kButton.IsEnabled = on;
        MaxButton.IsEnabled = on;
        FreezeCheck.IsEnabled = on;
        HealButton.IsEnabled = on;
        GodModeCheck.IsEnabled = on;
        InfiniteAmmoCheck.IsEnabled = on;
        NoRecoilCheck.IsEnabled = on;
        if (!on)
        {
            FreezeCheck.IsChecked = false;
            _freeze = false;
            GodModeCheck.IsChecked = false;
            _godMode = false;
            InfiniteAmmoCheck.IsChecked = false;
            _infiniteAmmo = false;
            NoRecoilCheck.IsChecked = false;
            _noRecoil = false;
            AmmoStatus.Text = "";
            RecoilStatus.Text = "";
            foreach (var lbl in _hpLabels) lbl.Text = "—";
        }
    }

    // ---- Agent health actions ----

    private void HealButton_Click(object sender, RoutedEventArgs e)
    {
        int n = _game.HealAliveAgents(GameConnection.HealthFull);
        Log(n > 0 ? $"Healed {n} agent(s) to {GameConnection.HealthFull}."
                  : "No alive agents to heal (are you in a mission?).");
    }

    private void GodModeCheck_Changed(object sender, RoutedEventArgs e)
    {
        _godMode = GodModeCheck.IsChecked == true;
        Log(_godMode ? "God mode ON — alive agents held at full health." : "God mode OFF.");
    }

    private void InfiniteAmmoCheck_Changed(object sender, RoutedEventArgs e)
    {
        _infiniteAmmo = InfiniteAmmoCheck.IsChecked == true;
        if (_infiniteAmmo)
        {
            bool found = _game.LocateWeaponArray();
            Log(found
                ? $"Infinite ammo ON — {_game.WeaponCount} weapon(s) will be kept topped up."
                : "Infinite ammo ON, but no weapons found yet (start/enter a mission first).");
        }
        else
        {
            Log("Infinite ammo OFF.");
            AmmoStatus.Text = "";
        }
    }

    private void NoRecoilCheck_Changed(object sender, RoutedEventArgs e)
    {
        _noRecoil = NoRecoilCheck.IsChecked == true;
        if (_noRecoil)
        {
            Log("No recoil ON — hit-reaction fields reset each tick so shots don't knock agents back.");
        }
        else
        {
            Log("No recoil OFF.");
            RecoilStatus.Text = "";
        }
    }

    // ---- Money actions ----

    private void SetButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetInput(out int value)) return;
        ApplyMoney(value);
    }

    private void Add100kButton_Click(object sender, RoutedEventArgs e)
    {
        if (_game.TryGetMoney(out int cur))
            ApplyMoney(SafeAdd(cur, 100_000));
    }

    private void MaxButton_Click(object sender, RoutedEventArgs e)
    {
        const int max = 999_999_999;
        MoneyInput.Text = max.ToString(CultureInfo.InvariantCulture);
        ApplyMoney(max);
    }

    private void ApplyMoney(int value)
    {
        if (_game.SetMoney(value))
        {
            _freezeValue = value;
            if (_freeze) Log($"Money set to {value:N0} (frozen).");
            else Log($"Money set to {value:N0}.");
        }
        else
        {
            Log("Write failed — the game may have closed. Re-attach.");
            HandleLostConnection();
        }
    }

    private void FreezeCheck_Changed(object sender, RoutedEventArgs e)
    {
        _freeze = FreezeCheck.IsChecked == true;
        if (_freeze)
        {
            // Freeze at the typed value if valid, else the current in-game value.
            if (TryGetInput(out int v)) _freezeValue = v;
            else if (_game.TryGetMoney(out int cur)) _freezeValue = cur;
            _game.SetMoney(_freezeValue);
            Log($"Freeze ON at {_freezeValue:N0}.");
        }
        else
        {
            Log("Freeze OFF.");
        }
    }

    // ---- Poll loop ----

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_game.IsAttached) return;

        if (!_game.TryGetMoney(out int money))
        {
            HandleLostConnection();
            return;
        }

        if (_freeze && money != _freezeValue)
        {
            _game.SetMoney(_freezeValue);
            money = _freezeValue;
        }

        MoneyText.Text = money.ToString("N0", CultureInfo.InvariantCulture);

        if (_game.TryGetDate(out int day, out int year))
            DateText.Text = $"in-game day {day}, yr {year}";

        // Agent health: display + optional freeze (god mode).
        for (int i = 0; i < GameConnection.ActiveAgentCount; i++)
        {
            if (_game.TryGetAgentHealth(i, out int hp) && hp > 0 && hp <= 0x2000)
            {
                _hpLabels[i].Text = hp.ToString(CultureInfo.InvariantCulture);
                _hpLabels[i].Foreground = (Brush)FindResource(
                    hp >= GameConnection.HealthFull ? "GoodBrush" : "AccentBrush");
            }
            else
            {
                _hpLabels[i].Text = hp == 0 ? "dead/—" : "—";
                _hpLabels[i].Foreground = (Brush)FindResource("SubtleBrush");
            }
        }

        if (_godMode)
            _game.HealAliveAgents(GameConnection.HealthFull);

        if (_infiniteAmmo)
        {
            _game.FreezeAmmo(GameConnection.AmmoFreezeTarget);
            int wc = _game.WeaponCount;
            AmmoStatus.Text = wc > 0 ? $"topping up {wc} weapon(s)" : "no weapons located";
        }

        if (_noRecoil)
        {
            int n = _game.SuppressRecoilAliveAgents();
            RecoilStatus.Text = n > 0 ? $"cancelled recoil on {n} agent(s)" : "active";
        }
    }

    private void HandleLostConnection()
    {
        SetStatus(false, "Connection lost — re-attach");
        EnableCheats(false);
        MoneyText.Text = "—";
        DateText.Text = "";
        _game.Detach();
    }

    // ---- Helpers ----

    private bool TryGetInput(out int value)
    {
        var raw = MoneyInput.Text.Replace(",", "").Replace("_", "").Trim();
        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0)
            return true;
        Log($"'{MoneyInput.Text}' is not a valid amount (0 .. 2,147,483,647).");
        value = 0;
        return false;
    }

    private static int SafeAdd(int a, int b)
    {
        long sum = (long)a + b;
        return sum > int.MaxValue ? int.MaxValue : (int)sum;
    }

    private void SetStatus(bool good, string text)
    {
        StatusDot.Fill = (Brush)(good
            ? FindResource("GoodBrush")
            : FindResource("BadBrush"));
        StatusText.Text = text;
    }

    private void Log(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}\n";
        LogText.Text += line;
        LogScroll.ScrollToEnd();
    }

    private sealed record ProcessItem(Process Process)
    {
        public override string ToString()
        {
            try { return $"{Process.ProcessName}  (PID {Process.Id})"; }
            catch { return $"PID {Process.Id}"; }
        }
    }
}
