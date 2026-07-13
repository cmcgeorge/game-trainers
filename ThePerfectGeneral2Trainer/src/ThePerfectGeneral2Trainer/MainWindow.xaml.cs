using System.Windows;
using ThePerfectGeneral2Trainer.ViewModels;

namespace ThePerfectGeneral2Trainer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Closed += (_, _) => _vm.Dispose();
    }
}
