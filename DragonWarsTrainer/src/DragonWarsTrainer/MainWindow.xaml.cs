using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DragonWarsTrainer.Game;
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

    // The per-slot buttons and picker live inside the inventory item template, so their
    // DataContext is the ItemSlotViewModel for that row.
    private static ItemSlotViewModel? Item(object sender) =>
        (sender as FrameworkElement)?.DataContext as ItemSlotViewModel;

    private void ClearItem_Click(object sender, RoutedEventArgs e) => Item(sender)?.Clear();
    private void InfiniteCharges_Click(object sender, RoutedEventArgs e) => Item(sender)?.SetInfiniteCharges();
    private void DuplicateItem_Click(object sender, RoutedEventArgs e) => Item(sender)?.Duplicate();

    // The editable combo raises SelectionChanged both when the user picks from the drop-down and
    // when its Text binding syncs to a matching catalog entry (on load, or after a rename). Applying
    // a template on that automatic sync would re-stamp the header and clobber an existing item's
    // charges/equip flags. We treat it as a genuine pick only when the drop-down is open (an explicit
    // user selection) or the chosen item's name differs from the slot's current name (which the load
    // sync never does, since it always matches Text). This still applies when a user re-picks the
    // item the slot already holds — restoring its stock header/flags — which the old name-equality
    // guard silently dropped.
    private void ItemTemplate_Selected(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0) return;
        if (sender is not ComboBox combo) return;
        if (Item(combo) is not { } vm || e.AddedItems[0] is not ItemTemplate template) return;
        if (!combo.IsDropDownOpen && template.Name == vm.Name) return;
        vm.ApplyTemplate(template);
    }

    // Each map square is drawn at this many pixels (matches the DrawingBrush viewport and the
    // MapScale converter's Cell). Clicking the schematic sets the teleport target to that square;
    // the Y axis is drawn bottom-up (origin bottom-left), so the row is flipped back to a grid Y.
    private const double MapCell = 15;

    private void Map_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement map || map.DataContext is not MapsViewModel vm) return;
        if (vm.SelectedArea is null) return;

        var p = e.GetPosition(map);
        int x = (int)Math.Floor(p.X / MapCell);
        int y = vm.GridHeight - 1 - (int)Math.Floor(p.Y / MapCell);
        vm.TargetX = Math.Clamp(x, 0, vm.GridWidth - 1);
        vm.TargetY = Math.Clamp(y, 0, vm.GridHeight - 1);
    }
}
