using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KeefTrainer.UI;

namespace KeefTrainer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;
        Closed += (_, _) => _viewModel.Dispose();

        // Diagnostic: KeefTrainer.exe --screenshot <path.png> renders the window to a
        // PNG shortly after startup and exits.
        string[] args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, "--screenshot");
        if (i >= 0 && i + 1 < args.Length)
        {
            string path = args[i + 1];
            ContentRendered += async (_, _) =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(1500); // let attach + first poll happen
                    SaveScreenshot(path);
                }
                catch
                {
                    // diagnostic-only path — never let it crash the app (async void handler)
                }
                finally
                {
                    Close();
                }
            };
        }
    }

    private void SaveScreenshot(string path)
    {
        if (Content is not FrameworkElement root || root.ActualWidth < 1) return;
        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(
            (int)root.ActualWidth, (int)root.ActualHeight, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Background, null, new Rect(0, 0, root.ActualWidth, root.ActualHeight));
            dc.DrawRectangle(new System.Windows.Media.VisualBrush(root), null,
                new Rect(0, 0, root.ActualWidth, root.ActualHeight));
        }
        bmp.Render(visual);
        var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
        enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
        using var fs = System.IO.File.Create(path);
        enc.Save(fs);
    }

    private static StatRowViewModel? RowOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as StatRowViewModel;

    private void ValueBox_GotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (RowOf(sender) is { } row) row.IsEditing = true;
        if (sender is TextBox tb) tb.SelectAll();
    }

    private void ValueBox_LostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (RowOf(sender) is { } row) row.IsEditing = false; // commits the edit
    }

    private void ValueBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (e.Key == Key.Enter)
        {
            // Move focus away; LostKeyboardFocus commits the value.
            Keyboard.ClearFocus();
            FocusManager.SetFocusedElement(this, this);
            if (RowOf(sender) is { } row) row.IsEditing = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (RowOf(sender) is { } row)
            {
                row.EditText = row.Value.ToString();
                row.IsEditing = false;
            }
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }
}
