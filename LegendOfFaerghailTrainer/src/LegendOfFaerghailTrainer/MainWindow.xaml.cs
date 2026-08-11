using System.Windows;
using LegendOfFaerghailTrainer.ViewModels;

namespace LegendOfFaerghailTrainer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        // Auto-attach once the window exists, so the address-space sweep does not happen before
        // there is anything on screen to show its result.
        Loaded += (_, _) => _vm.TryAutoAttach();
    }

    // The per-character quick actions are code-behind handlers rather than commands because the
    // editor is one DataTemplate reused by two tabs. The character to act on is taken from the
    // button's own DataContext — which is that template's character — so the buttons cannot act on
    // the wrong tab's selection.
    private static CharacterViewModel? CharacterOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as CharacterViewModel;

    private void OnFullHeal(object sender, RoutedEventArgs e) => CharacterOf(sender)?.FullHeal();
    private void OnMaxAttributes(object sender, RoutedEventArgs e) => CharacterOf(sender)?.MaxAttributes();
    private void OnMaxAbilities(object sender, RoutedEventArgs e) => CharacterOf(sender)?.MaxAbilities();
    private void OnAllLanguages(object sender, RoutedEventArgs e) => CharacterOf(sender)?.LearnAllLanguages();
    private void OnRestock(object sender, RoutedEventArgs e) => CharacterOf(sender)?.RestockSpellsAndRepairItems();

    private void OnClosed(object sender, EventArgs e) => _vm.Shutdown();
}
