using System.Windows;
using Questron2Trainer.ViewModels;

namespace Questron2Trainer;

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

    private static CharacterViewModel? Vm(object sender) =>
        (sender as FrameworkElement)?.DataContext as CharacterViewModel;

    private void FullHeal_Click(object sender, RoutedEventArgs e) => Vm(sender)?.FullHeal();
    private void MaxAttributes_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxAttributes();
    private void MaxGold_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxGold();
    private void MaxSpells_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxSpells();
    private void MaxEverything_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxEverything();
}
