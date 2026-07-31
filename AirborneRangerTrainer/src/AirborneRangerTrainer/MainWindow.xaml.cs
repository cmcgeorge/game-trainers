using System.Windows;
using AirborneRangerTrainer.Game;
using AirborneRangerTrainer.ViewModels;
using Microsoft.Win32;

namespace AirborneRangerTrainer;

/// <summary>The trainer's only window.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    /// <summary>Builds the window and wires up the view-model.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Closed += (_, _) => _vm.Dispose();
    }

    private void OnOpenRoster(object sender, RoutedEventArgs e)
    {
        // Opening another file replaces the in-memory roster outright, and unsaved edits have no
        // .bak to fall back on — the backup is only taken when a save happens.
        if (_vm.Roster.WouldDiscardEdits &&
            MessageBox.Show(this,
                "There are unsaved roster edits. Opening another file will discard them.\n\nContinue?",
                "Airborne Ranger Trainer", MessageBoxButton.OKCancel, MessageBoxImage.Warning)
            != MessageBoxResult.OK)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Open the Airborne Ranger career file",
            FileName = RosterFormat.FileName,
            Filter = $"Ranger roster ({RosterFormat.FileName})|{RosterFormat.FileName}|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (dialog.ShowDialog(this) == true) _vm.Roster.Load(dialog.FileName);
    }
}
