using System.Windows;
using SotSAgeTrainer.ViewModels;

namespace SotSAgeTrainer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        Closed += (_, _) => _vm.Dispose();
    }
}
