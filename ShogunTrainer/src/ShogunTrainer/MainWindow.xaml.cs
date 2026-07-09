using System.Windows;
using System.Windows.Threading;
using ShogunTrainer.Game;

namespace ShogunTrainer;

public partial class MainWindow : Window
{
    private readonly TrainerEngine _engine = new();
    private readonly DispatcherTimer _timer;

    public MainWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += OnTimerTick;
        Closed += (_, _) => { _timer.Stop(); _engine.Dispose(); };
        SetCheatsEnabled(false);
        Log("Ready. Start Shogun in DOSBox-X, get in-game, then click Attach.");
    }

    // ---- attach / status ----

    private void OnAttachClick(object sender, RoutedEventArgs e)
    {
        if (_engine.Attached)
        {
            _timer.Stop();
            _engine.Detach();
            SetCheatsEnabled(false);
            AttachButton.Content = "Attach";
            StatusText.Text = "Detached.";
            Log("Detached.");
            return;
        }

        string msg = _engine.Attach();
        Log(msg);
        StatusText.Text = msg;
        if (_engine.Attached)
        {
            AttachButton.Content = "Detach";
            SetCheatsEnabled(true);
            _timer.Start();
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        // Push freezes and pull a fresh snapshot on the UI thread.
        _engine.FreezeCash = FreezeCash.IsChecked == true;
        _engine.FreezeHp = FreezeHp.IsChecked == true;
        _engine.FrozenCash = ParseOr(CashBox.Text, 255);
        _engine.FrozenHp = ParseOr(HpBox.Text, 254);
        _engine.FreezeTimer = FreezeTime.IsChecked == true;
        _engine.ForceFollowing = ForceFollow.IsChecked == true;

        PlayerState s = _engine.Tick();
        if (!s.TableValid)
        {
            CashNow.Text = HpNow.Text = "";
            FollowersNow.Text = "Followers: —";
            StatusText.Text = "Lost the entity table (reloading / not in-game). Retrying…";
            return;
        }
        CashNow.Text = $"now: {s.Cash}";
        HpNow.Text = $"now: {s.Hp}";
        FollowersNow.Text = $"Followers: {s.Followers}" + (s.Followers > 19 ? "  (contest open!)" : "");
        StatusText.Text = $"Attached — pid {_engine.ProcessId}, table 0x{_engine.TableAddress:X}";

        // Time & contest panel (only meaningful if the globals segment mapped).
        if (_engine.GlobalsValid())
        {
            ContestGroup.IsEnabled = true;
            TimerNow.Text = _engine.ReadTimer().ToString();
            int tally = _engine.ReadFollowingTally();
            TallyNow.Text = $"{tally} / 20" + (tally > 19 ? "  ✓" : "");
        }
        else
        {
            ContestGroup.IsEnabled = false;
            TimerNow.Text = TallyNow.Text = "n/a";
        }
    }

    // ---- cheat handlers ----

    private void OnSetCash(object sender, RoutedEventArgs e)
    {
        int v = ParseOr(CashBox.Text, 255);
        Log(_engine.SetCash(v) ? $"Set cash = {v}." : "Set cash failed.");
    }

    private void OnSetHp(object sender, RoutedEventArgs e)
    {
        int v = ParseOr(HpBox.Text, 254);
        Log(_engine.SetHp(v) ? $"Set health = {v}." : "Set health failed.");
    }

    private void OnFreezeCash(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) Log(FreezeCash.IsChecked == true ? "Cash frozen." : "Cash unfrozen.");
    }

    private void OnFreezeHp(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) Log(FreezeHp.IsChecked == true ? "Health frozen." : "Health unfrozen.");
    }

    private void OnFreezeTime(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) Log(FreezeTime.IsChecked == true
            ? "Fail timer frozen — take all the time you want."
            : "Fail timer unfrozen.");
    }

    private void OnForceFollow(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) Log(ForceFollow.IsChecked == true
            ? "Forcing your following past 19. Pick up any object to open the become-Shōgun contest."
            : "Stopped forcing following.");
    }

    private void OnPlaceRelics(object sender, RoutedEventArgs e)
    {
        Log(_engine.PlaceRelicsAtContest());
    }

    private void OnRoute(object sender, RoutedEventArgs e)
    {
        int target = int.TryParse(AreaTargetBox.Text?.Trim(), out int v) ? v : -1;
        RouteResult.Text = _engine.RouteTo(target);
        Log(RouteResult.Text);
    }

    private void OnTeleport(object sender, RoutedEventArgs e)
    {
        int target = int.TryParse(AreaTargetBox.Text?.Trim(), out int v) ? v : -1;
        Log(_engine.Teleport(target)
            ? $"Teleported to area {target}. If the screen looks off, step one screen over and back to redraw it."
            : "Teleport failed (need a valid area 0–254 and the globals mapped).");
    }

    private void OnRecruitAll(object sender, RoutedEventArgs e)
    {
        int n = _engine.Recruit(ShogunGame.EntityCount);
        Log($"Recruited {n} NPC(s) to your following.");
    }

    private void OnRecruitN(object sender, RoutedEventArgs e)
    {
        int want = ParseOr(RecruitCount.Text, 20);
        int n = _engine.Recruit(want);
        Log($"Recruited {n} NPC(s) (asked for {want}).");
    }

    private void OnMakeFriendly(object sender, RoutedEventArgs e)
    {
        int n = _engine.MakeFriendly();
        Log($"Made {n} NPC(s) friendly (max disposition; gifts always accepted, easy to recruit).");
    }

    private void OnMaxStats(object sender, RoutedEventArgs e)
    {
        Log(_engine.MaxStats(ShogunGame.PlayerIndex) ? "Maxed Blackthorne's personality stats." : "Max stats failed.");
    }

    private void OnGiveRelics(object sender, RoutedEventArgs e)
    {
        Log(_engine.GiveRelics()
            ? "Put Buddha/Scroll/Mirror in your pockets. NOTE: the become-Shogun check reads the " +
              "world-object table, not your inventory — these won't win by themselves."
            : "Give relics failed.");
    }

    // ---- helpers ----

    private void SetCheatsEnabled(bool on)
    {
        PlayerGroup.IsEnabled = on;
        FollowGroup.IsEnabled = on;
        ExtrasGroup.IsEnabled = on;
        ContestGroup.IsEnabled = on;
    }

    private static int ParseOr(string text, int fallback) =>
        int.TryParse(text?.Trim(), out int v) ? Math.Clamp(v, 0, 255) : fallback;

    private void Log(string message)
    {
        LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\r\n");
        LogBox.ScrollToEnd();
    }
}
