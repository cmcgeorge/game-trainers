using System.Windows;
using Wizardry1Trainer.ViewModels;

namespace Wizardry1Trainer;

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

    private void FullHeal_Click(object sender, RoutedEventArgs e) => _vm.SelectedCharacter?.FullHeal();
    private void MaxAttributes_Click(object sender, RoutedEventArgs e) => _vm.SelectedCharacter?.MaxAttributes();
    private void MaxHp_Click(object sender, RoutedEventArgs e) => _vm.SelectedCharacter?.MaxHp();
    private void LearnSpells_Click(object sender, RoutedEventArgs e) => _vm.SelectedCharacter?.LearnAllSpells();
    private void MaxGold_Click(object sender, RoutedEventArgs e) => _vm.SelectedCharacter?.MaxGold();
    private void MaxExperience_Click(object sender, RoutedEventArgs e) => _vm.SelectedCharacter?.MaxExperience();
    private void MaxEverything_Click(object sender, RoutedEventArgs e) => _vm.SelectedCharacter?.MaxEverything();
}
