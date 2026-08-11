using System.Windows;
using RedBaronTrainer.ViewModels;

namespace RedBaronTrainer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _model;

    public MainWindow()
    {
        InitializeComponent();
        _model = new MainViewModel();
        DataContext = _model;
    }

    protected override void OnClosed(EventArgs e)
    {
        _model.Dispose();
        base.OnClosed(e);
    }
}
