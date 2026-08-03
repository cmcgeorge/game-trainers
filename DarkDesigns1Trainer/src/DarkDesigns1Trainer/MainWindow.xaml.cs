using System.Windows;
using System.Windows.Controls;
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
