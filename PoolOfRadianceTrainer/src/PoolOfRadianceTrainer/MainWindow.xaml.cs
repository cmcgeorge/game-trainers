using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using PoolOfRadianceTrainer.ViewModels;

namespace PoolOfRadianceTrainer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
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
    private void RandomizeIconColors_Click(object sender, RoutedEventArgs e) => Vm(sender)?.RandomizeIconColors();

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
