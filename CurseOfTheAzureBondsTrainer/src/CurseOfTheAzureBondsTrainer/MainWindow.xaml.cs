using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using CurseOfTheAzureBondsTrainer.ViewModels;

namespace CurseOfTheAzureBondsTrainer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        // The view-model asks before anything irreversible; give it a real dialog to ask with.
        _vm.Confirm = message => MessageBox.Show(this, message, "Curse of the Azure Bonds Trainer",
            MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.OK;
        _vm.SaveEditor.Confirm = _vm.Confirm;
        _vm.PartyGen.Confirm = _vm.Confirm;
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        _vm.InitHotkeys(hwnd);
    }

    private void OnClosed(object? sender, EventArgs e) => _vm.Dispose();

    // The per-character quick-action buttons live in a panel whose DataContext is the
    // selected CharacterViewModel, so the sender's DataContext is that view-model.
    private static CharacterViewModel? Vm(object sender) =>
        (sender as FrameworkElement)?.DataContext as CharacterViewModel;

    private void MaxStats_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxStats();
    private void FullHeal_Click(object sender, RoutedEventArgs e) => Vm(sender)?.FullHeal();
    private void MaxMoney_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxMoney();
    private void MaxEverything_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxEverything();
    private void RestoreDrained_Click(object sender, RoutedEventArgs e) => Vm(sender)?.RestoreDrainedStats();
    private void RandomizeIconColors_Click(object sender, RoutedEventArgs e) => Vm(sender)?.RandomizeIconColors();

    // The class-change button needs a confirm dialog, because the change is irreversible and the
    // preview it shows (what it will do, what it won't, and any warnings) should be in front of the
    // user before they click OK.
    private void ChangeClass_Click(object sender, RoutedEventArgs e)
    {
        if (Vm(sender) is not { } character) return;
        if (!_vm.Confirm($"Change {character.Name}'s class?\n\n{character.ClassChangePreview}\n\n" +
                         "This cannot be undone."))
        {
            _vm.Status = "Class change cancelled.";
            return;
        }
        _vm.Status = character.ApplyClassChange();
    }

    // The Combat tab's monster fields are refreshed from live memory every poll tick, which would
    // wipe out a half-typed value. GotKeyboardFocus/LostKeyboardFocus bubble from the boxes to the
    // panel, and both fire after focus has moved, so IsKeyboardFocusWithin is the answer to "is the
    // user typing in here right now?".
    private void EnemyEditor_FocusChanged(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
    {
        if (sender is UIElement panel) _vm.EnemyEditorFocused = panel.IsKeyboardFocusWithin;
    }

    // Cell size (px) of the Maps schematic — must match the MapScale/MapFlipY converters' Cell in
    // App.xaml. Clicking a square sets the teleport target to that grid cell; the schematic is drawn
    // with (0,0) at the top-left (Y increasing down), matching the game's own coordinates.
    private const double MapCell = 18;

    private void Map_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement map || map.DataContext is not MapsViewModel vm) return;
        if (vm.SelectedArea is null) return;

        var p = e.GetPosition(map);
        int x = (int)System.Math.Floor(p.X / MapCell);
        int y = (int)System.Math.Floor(p.Y / MapCell);
        vm.TargetX = System.Math.Clamp(x, 0, vm.GridWidth - 1);
        vm.TargetY = System.Math.Clamp(y, 0, vm.GridHeight - 1);
    }

    // Folder picker for the offline save-editor tab.
    private void BrowseSaveFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select the Gold Box save folder (with CHRDATAn.SAV files)" };
        if (Directory.Exists(_vm.SaveEditor.SaveFolder)) dlg.InitialDirectory = _vm.SaveEditor.SaveFolder;
        if (dlg.ShowDialog() == true) _vm.SaveEditor.SaveFolder = dlg.FolderName;
    }
}
