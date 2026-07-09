using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MightAndMagic1Trainer.Memory;
using MightAndMagic1Trainer.ViewModels;
using Microsoft.Win32;

namespace MightAndMagic1Trainer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private GlobalHotkeys? _hotkeys;
    private string? _lastRosterDir;
    private string? _lastDumpDir;
    private bool _closePending;
    private bool _readyToClose;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    // Global hotkeys need a window handle, which only exists from here on.
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        const uint vkF1 = 0x70, vkF2 = 0x71, vkF3 = 0x72;
        _hotkeys = new GlobalHotkeys(this);
        bool ok = _hotkeys.RegisterCtrl(vkF1, _vm.ToggleGodModeHotkey);
        ok &= _hotkeys.RegisterCtrl(vkF2, _vm.HealPartyHotkey);
        ok &= _hotkeys.RegisterCtrl(vkF3, _vm.MaxEverythingHotkey);
        if (!ok)
            _vm.Status = "Some global hotkeys (Ctrl+F1/F2/F3) are taken by another app and won't fire.";
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotkeys?.Dispose();
        base.OnClosed(e);
    }

    // Closing mid-dump would kill the pool thread between writes and strand the dump's temp
    // files; instead cancel it and let the close finish once its cleanup has run.
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_readyToClose || _vm.MemoryDump.DumpTask is not { IsCompleted: false } dump) return;
        e.Cancel = true;
        if (_closePending) return;
        _closePending = true;
        _vm.MemoryDump.CancelDump();
        CloseWhenDumpStops(dump);
    }

    private async void CloseWhenDumpStops(Task dump)
    {
        await dump;   // never faults: the dump's own handler reports every outcome
        _readyToClose = true;
        Close();
    }

    private void OnLoadRosterClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Browse for ROSTER.DTA",
            Filter = "Roster file (*.dta)|*.dta|All files (*.*)|*.*",
            FileName = "Roster.dta",
            CheckFileExists = true,
            InitialDirectory = _lastRosterDir ?? DefaultRosterDir()
        };
        if (dlg.ShowDialog(this) == true)
        {
            var dir = Path.GetDirectoryName(dlg.FileName);
            if (dir != null) _lastRosterDir = dir;   // null only for drive-root paths
            _vm.LoadRosterFileCommand.Execute(dlg.FileName);
        }
    }

    private void OnDumpMemoryClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Dump process memory to…",
            Filter = "Memory dump (*.bin)|*.bin|All files (*.*)|*.*",
            DefaultExt = ".bin",
            FileName = $"memdump-{DateTime.Now:yyyyMMdd-HHmmss}.bin",
            // Remember where the last dump went: the tab's own workflow (dump twice, diff)
            // means landing in the same folder back-to-back is the normal case.
            InitialDirectory = _lastDumpDir ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dlg.ShowDialog(this) == true)
        {
            var dir = Path.GetDirectoryName(dlg.FileName);
            if (dir != null) _lastDumpDir = dir;   // null only for drive-root paths
            _vm.MemoryDump.Start(dlg.FileName);
        }
    }

    private void OnBrowseOldDumpClicked(object sender, RoutedEventArgs e)
    {
        if (BrowseForDump("Pick the BEFORE (older) dump") is { } path) _vm.DumpDiff.OldPath = path;
    }

    private void OnBrowseNewDumpClicked(object sender, RoutedEventArgs e)
    {
        if (BrowseForDump("Pick the AFTER (newer) dump") is { } path) _vm.DumpDiff.NewPath = path;
    }

    private string? BrowseForDump(string title)
    {
        var dlg = new OpenFileDialog
        {
            Title = title,
            Filter = "Memory dump (*.bin)|*.bin|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = _lastDumpDir ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dlg.ShowDialog(this) != true) return null;
        var dir = Path.GetDirectoryName(dlg.FileName);
        if (dir != null) _lastDumpDir = dir;   // null only for drive-root paths
        return dlg.FileName;
    }

    // Max EVERYTHING for the whole party clobbers every character irreversibly (and writes
    // straight to the live game when attached), so confirm before firing. A dialog belongs in
    // code-behind, not the view model.
    private void OnMaxEverythingPartyClicked(object sender, RoutedEventArgs e)
    {
        if (!_vm.HasParty) return;
        var answer = MessageBox.Show(
            this,
            $"Max HP, SP, stats, resistances, gold, gems and food for all {_vm.Characters.Count} character(s)?\n\n" +
            "This overwrites their current values and can't be undone (take a 📸 Snapshot first if unsure).",
            "Max EVERYTHING — whole party",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer == MessageBoxResult.Yes)
            _vm.MaxEverythingAllCommand.Execute(null);
    }

    private void OnSaveSnapshotClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save party snapshot to…",
            Filter = "Party snapshot / roster (*.dta)|*.dta|All files (*.*)|*.*",
            DefaultExt = ".dta",
            FileName = $"party-snapshot-{DateTime.Now:yyyyMMdd-HHmmss}.dta",
            InitialDirectory = _lastSnapshotDir ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dlg.ShowDialog(this) == true)
        {
            RememberSnapshotDir(dlg.FileName);
            _vm.SaveSnapshotCommand.Execute(dlg.FileName);
        }
    }

    private void OnLoadSnapshotClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Restore party snapshot from…",
            Filter = "Party snapshot / roster (*.dta)|*.dta|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = _lastSnapshotDir ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dlg.ShowDialog(this) == true)
        {
            RememberSnapshotDir(dlg.FileName);
            _vm.LoadSnapshotCommand.Execute(dlg.FileName);
        }
    }

    private string? _lastSnapshotDir;

    private void RememberSnapshotDir(string fileName)
    {
        var dir = Path.GetDirectoryName(fileName);
        if (dir != null) _lastSnapshotDir = dir;   // null only for drive-root paths
    }

    // Forward map clicks to the Maps view model in the overlay grid's coordinate space —
    // the image is unscaled (Stretch=None), so these are the same coordinates the
    // calibration anchors were recorded in.
    private void OnMapImageClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var pos = e.GetPosition((IInputElement)sender);
        _vm.MapReference.OnMapClicked(pos.X, pos.Y);
    }

    // The drawn map's board grid is sized 1:1 to its render (cell = 30px), so a click's
    // position relative to the grid is already in board-pixel coordinates.
    private void OnDrawnMapClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var pos = e.GetPosition((IInputElement)sender);
        _vm.DrawnMap.OnBoardClicked(pos.X, pos.Y);
    }

    private void OnLoadMazedataClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Browse for Mazedata.dta",
            Filter = "Maze data (*.dta)|*.dta|All files (*.*)|*.*",
            FileName = "Mazedata.dta",
            CheckFileExists = true,
            InitialDirectory = _lastRosterDir ?? DefaultRosterDir()
        };
        if (dlg.ShowDialog(this) == true)
        {
            var dir = Path.GetDirectoryName(dlg.FileName);
            if (dir != null) _lastRosterDir = dir;
            _vm.DrawnMap.LoadFrom(dlg.FileName);
        }
    }

    // The X/Y search grid tells its view model when a cell edit is open, so the tab's
    // auto-refresh poll won't overwrite the value the user is currently typing.
    private void OnXyGridBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PairSearchViewModel vm) vm.BeginGridEdit();
    }

    private void OnXyGridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PairSearchViewModel vm) vm.EndGridEdit();
    }

    // Start the browse dialog somewhere useful: the user's game folder if present,
    // then the bundled sample roster under docs\, otherwise the app's own folder.
    private static string DefaultRosterDir()
    {
        const string gameDir = @"C:\Temp\Games\MM1";
        if (Directory.Exists(gameDir)) return gameDir;

        var docs = Path.Combine(AppContext.BaseDirectory, "docs");
        return Directory.Exists(docs) ? docs : AppContext.BaseDirectory;
    }
}
