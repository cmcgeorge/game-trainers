using System.Windows;
using System.Windows.Controls;
using Civilization3ConquestsTrainer.ViewModels;

namespace Civilization3ConquestsTrainer;

/// <summary>
/// Shell window. All behaviour lives in <see cref="MainViewModel"/>; the only code-behind is the pair
/// of grid-edit hooks, which exist because "is a cell currently open for editing" is a view fact the
/// view-model has no other way to learn.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Closed += (_, _) => _vm.Dispose();
    }

    /// <summary>
    /// Pauses the poll loop's refresh while a cell is being edited. Without this the 500 ms tick
    /// raises PropertyChanged on the bound property and WPF pushes the game's current value straight
    /// into the open TextBox, wiping out whatever the user has typed. Freezes keep running.
    /// </summary>
    private void Grid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e) => _vm.SetEditing(true);

    private void Grid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e) => _vm.SetEditing(false);
}
