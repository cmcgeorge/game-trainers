using System.Windows;
using RailroadTycoonTrainer.ViewModels;

namespace RailroadTycoonTrainer;

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
