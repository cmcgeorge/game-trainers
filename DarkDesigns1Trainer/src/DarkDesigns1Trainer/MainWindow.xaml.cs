using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DarkDesigns1Trainer.Game;
using DarkDesigns1Trainer.ViewModels;

namespace DarkDesigns1Trainer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e) => _vm.Dispose();

    // The per-character action buttons live in a panel whose DataContext is the selected
    // CharacterViewModel, so the sender's DataContext is that view-model.
    private static CharacterViewModel? Vm(object sender) =>
        (sender as FrameworkElement)?.DataContext as CharacterViewModel;

    private void FullHeal_Click(object sender, RoutedEventArgs e) => Vm(sender)?.FullHeal();
    private void MaxAttributes_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxAttributes();
    private void MaxMoney_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxMoney();
    private void MaxEverything_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxEverything();
    private void ClearPack_Click(object sender, RoutedEventArgs e) => Vm(sender)?.ClearPack();

    // Maps: clicking a square aims the teleport at it. Y is not flipped — Dark Designs numbers
    // rows from the north edge down, so the grid draws in the same order it is stored.
    private void Map_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement map || map.DataContext is not MapsViewModel vm) return;

        var p = e.GetPosition(map);
        int x = (int)Math.Floor(p.X / MapScaleConverter.CellSize);
        int y = (int)Math.Floor(p.Y / MapScaleConverter.CellSize);
        vm.TargetX = Math.Clamp(x, 0, MapFormat.GridSize - 1);
        vm.TargetY = Math.Clamp(y, 0, MapFormat.GridSize - 1);
    }

    // Save editor: mark the save file modified when a field loses focus
    private void SaveField_LostFocus(object sender, RoutedEventArgs e)
    {
        _vm.SaveFile?.MarkModified();
        var selected = _vm.SelectedSaveCharacter;
        if (selected != null)
        {
            int idx = _vm.SaveCharacters.IndexOf(selected);
            if (idx >= 0)
            {
                _vm.SaveCharacters.RemoveAt(idx);
                _vm.SaveCharacters.Insert(idx, selected);
                _vm.SelectedSaveCharacter = selected;
            }
        }
    }
}
