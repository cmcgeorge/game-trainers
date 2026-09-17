using System.Windows;
using System.Windows.Input;
using Space1889Trainer.ViewModels;

namespace Space1889Trainer;

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
    /// A click on the map. The image is drawn at exactly <see cref="MapsViewModel.Zoom"/> pixels a square with
    /// Stretch="None", so the view-model's arithmetic is a straight divide.
    /// </summary>
    private void MapImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not System.Windows.Controls.Image image) return;
        var p = e.GetPosition(image);
        _vm.Maps.Pick(p.X, p.Y);
    }

    private void MapImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not System.Windows.Controls.Image image) return;
        var p = e.GetPosition(image);
        _vm.Maps.HoverAt(p.X, p.Y);
    }
}
