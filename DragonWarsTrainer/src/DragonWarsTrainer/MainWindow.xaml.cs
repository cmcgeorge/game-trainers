using System.Windows;
using System.Windows.Controls;
using DragonWarsTrainer.ViewModels;

namespace DragonWarsTrainer;

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
    private void MaxSkills_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxSkills();
    private void LearnSpells_Click(object sender, RoutedEventArgs e) => Vm(sender)?.LearnAllSpells();
    private void MaxMoney_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxMoney();
    private void MaxEverything_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxEverything();
}
