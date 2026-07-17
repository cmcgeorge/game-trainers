using System.Windows;
using System.Windows.Controls;
using WastelandTrainer.Game;
using WastelandTrainer.ViewModels;

namespace WastelandTrainer;

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
    private void MaxMoney_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxMoney();
    private void MaxEverything_Click(object sender, RoutedEventArgs e) => Vm(sender)?.MaxEverything();

    // The Clear button lives inside the inventory item template, so its DataContext is the
    // ItemRowViewModel for that row.
    private void ClearItem_Click(object sender, RoutedEventArgs e) =>
        ((sender as FrameworkElement)?.DataContext as ItemRowViewModel)?.Clear();

    // The editable item combo raises SelectionChanged both when the user picks from the drop-down and
    // when its Text binding re-syncs to a matching catalog entry (on load, or after a compaction moved
    // an item into this row). Treat it as a real pick only when the drop-down is open, or the picked
    // item differs from what the slot already holds — so the automatic re-sync never re-applies an id.
    private void ItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        if (sender is not ComboBox combo) return;
        if (combo.DataContext is not ItemRowViewModel vm) return;
        if (e.AddedItems[0] is not ItemInfo option) return;
        if (!combo.IsDropDownOpen && option.Id == vm.Id) return;
        vm.Apply(option);
    }
}
