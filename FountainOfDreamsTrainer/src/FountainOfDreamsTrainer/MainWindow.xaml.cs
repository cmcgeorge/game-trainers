using System.Windows;
using System.Windows.Controls;
using FountainOfDreamsTrainer.ViewModels;

namespace FountainOfDreamsTrainer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Closed += (_, _) => _vm.Dispose();
    }

    private void FullHeal_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.FullHeal();

    private void MaxAttributes_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.MaxAttributes();

    private void MaxMoney_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.MaxMoney();

    private void MaxEverything_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.MaxEverything();

    private void ItemClear_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ItemRowViewModel row)
            row.Clear();
    }
}
