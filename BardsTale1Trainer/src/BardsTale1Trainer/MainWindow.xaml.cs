using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BardsTale1Trainer.Memory;
using BardsTale1Trainer.ViewModels;
using Microsoft.Win32;

namespace BardsTale1Trainer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();
    private GlobalHotkeys? _hotkeys;
    private string? _lastTpwDir;
    private string? _lastDumpDir;
    private string? _lastSnapshotDir;
    private bool _closePending;
    private bool _readyToClose;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // Pause the periodic auto re-read while a TextBox *in the character editor* has
        // keyboard focus, so it can't overwrite a value the user is part-way through typing.
        // Scoped to the editor subtree (CharacterEditor) so focusing unrelated TextBoxes —
        // the Memory/X-Y search boxes, the item search, etc. — doesn't needlessly halt the
        // party read. These window-level handlers fire for any focus change in the window;
        // LostKeyboardFocus runs before GotKeyboardFocus, so moving between two editor
        // TextBoxes keeps the flag set.
        AddHandler(GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) =>
        {
            if (e.NewFocus is TextBox tb && CharacterEditor.IsAncestorOf(tb)) _vm.EditorHasFocus = true;
        }), handledEventsToo: true);
        AddHandler(LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) =>
        {
            if (e.OldFocus is TextBox tb && CharacterEditor.IsAncestorOf(tb)) _vm.EditorHasFocus = false;
        }), handledEventsToo: true);
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

    // Closing mid-dump would strand the dump's temp files; cancel it and finish the close
    // once its cleanup has run.
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
        await dump;
        _readyToClose = true;
        Close();
    }

    private void OnLoadTpwClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Load a .TPW character file",
            Filter = "Bard's Tale character (*.TPW)|*.TPW|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = _lastTpwDir ?? DefaultGameDir()
        };
        if (dlg.ShowDialog(this) == true)
        {
            var dir = Path.GetDirectoryName(dlg.FileName);
            if (dir != null) _lastTpwDir = dir;
            _vm.LoadTpwFileCommand.Execute(dlg.FileName);
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
            InitialDirectory = _lastDumpDir ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dlg.ShowDialog(this) == true)
        {
            var dir = Path.GetDirectoryName(dlg.FileName);
            if (dir != null) _lastDumpDir = dir;
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

    private void OnSaveSnapshotClicked(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Save party snapshot to…",
            Filter = "Party snapshot (*.party)|*.party|All files (*.*)|*.*",
            DefaultExt = ".party",
            FileName = $"party-snapshot-{DateTime.Now:yyyyMMdd-HHmmss}.party",
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
            Filter = "Party snapshot (*.party)|*.party|All files (*.*)|*.*",
            CheckFileExists = true,
            InitialDirectory = _lastSnapshotDir ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };
        if (dlg.ShowDialog(this) == true)
        {
            RememberSnapshotDir(dlg.FileName);
            _vm.LoadSnapshotCommand.Execute(dlg.FileName);
        }
    }

    private void RememberSnapshotDir(string fileName)
    {
        var dir = Path.GetDirectoryName(fileName);
        if (dir != null) _lastSnapshotDir = dir;   // null only for drive-root paths
    }

    // Forward map clicks to the Maps view model in the overlay grid's coordinate space —
    // the image is unscaled (Stretch=None), so these are the same coordinates the
    // calibration anchors were recorded in.
    private void OnMapImageClicked(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition((IInputElement)sender);
        _vm.MapReference.OnMapClicked(pos.X, pos.Y);
    }

    private void OnXyGridBeginningEdit(object sender, DataGridBeginningEditEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PairSearchViewModel vm) vm.BeginGridEdit();
    }

    private void OnXyGridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PairSearchViewModel vm) vm.EndGridEdit();
    }

    private static string DefaultGameDir()
    {
        const string gameDir = @"C:\Temp\Games\BTALE";
        if (Directory.Exists(gameDir)) return gameDir;
        var docs = Path.Combine(AppContext.BaseDirectory, "docs");
        return Directory.Exists(docs) ? docs : AppContext.BaseDirectory;
    }
}
