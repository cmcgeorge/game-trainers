using System.Windows;
using BardsTaleTrilogyTrainer.ViewModels;

namespace BardsTaleTrilogyTrainer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private void WriteAll_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.WriteAll();

    private void LearnAllSpells_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.LearnAllSpells();

    private void SetInfiniteItems_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.SetInfiniteItems();

    private void FullHeal_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.FullHeal();

    private void MaxAttributes_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.MaxAttributes();
}
