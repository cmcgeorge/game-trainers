using WastelandRemasteredTrainer.ViewModels;

namespace WastelandRemasteredTrainer;

public partial class MainWindow : System.Windows.Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // Releases the poll timer and the process handle deterministically; without this the
        // 400 ms timer keeps running against the game until the trainer's process exits.
        Closed += (_, _) => _vm.Dispose();
    }
}
