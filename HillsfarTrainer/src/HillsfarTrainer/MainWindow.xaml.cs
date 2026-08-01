using System.Windows;
using HillsfarTrainer.ViewModels;

namespace HillsfarTrainer;

/// <summary>The shell window. All behaviour lives in <see cref="MainViewModel"/>.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    /// <summary>Builds the window and binds the root view-model.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Closed += (_, _) => _vm.Dispose();
    }
}
