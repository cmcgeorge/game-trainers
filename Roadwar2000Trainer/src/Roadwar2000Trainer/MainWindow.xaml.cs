using System.Windows;
using System.Windows.Input;
using Roadwar2000Trainer.ViewModels;

namespace Roadwar2000Trainer;

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
    /// Turns a click on the map schematic into a map square. The image is drawn at exactly
    /// <see cref="MapViewModel.Cell"/> pixels a square with Stretch="None", so the arithmetic is
    /// a straight divide -- no scaling factor to get wrong.
    /// </summary>
    private void MapImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Image image) return;
        var p = e.GetPosition(image);
        var (x, y) = _vm.Map.SquareAt(p.X, p.Y);
        _vm.Map.Pick(x, y);
    }
}
