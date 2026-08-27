using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TheQuestTrainer.ViewModels;

namespace TheQuestTrainer;

/// <summary>
/// Shell for the session view model.
///
/// The only logic here is the focus probe. The refresh must not overwrite a box the user is halfway
/// through typing into, and the reliable way to know that is to ask <c>FocusManager</c> for the
/// window's logical focus at the moment the question is asked. A flag tracked from
/// GotFocus/LostFocus events gets this wrong twice over: it latches on forever when the focused
/// editor is destroyed rather than blurred (rebuilding the skill grid does exactly that), and
/// clearing it when keyboard focus leaves the application throws away a half-typed value on
/// alt-tab.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainViewModel vm)
        {
            vm.EditorFocusProbe = IsEditorFocused;
            Closed += (_, _) => vm.Dispose();
        }
    }

    private bool IsEditorFocused()
    {
        var focused = FocusManager.GetFocusedElement(this);
        return focused is TextBox or DataGridCell;
    }

    /// <summary>
    /// Picks the folder the game is installed in, and lists its adventures straight away.
    ///
    /// A dialog rather than a bound text box alone because the install path is long and easy to
    /// mistype; the box stays editable so a second install can be typed in without browsing.
    /// </summary>
    private void BrowseGameFolder(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (Pick("Where is The Quest installed?", vm.Book.GameFolder) is not { } folder) return;

        vm.Book.GameFolder = folder;
        vm.Book.Find();
    }

    /// <summary>Picks where the cluebooks are written.</summary>
    private void BrowseOutputFolder(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (Pick("Where should the cluebooks go?", vm.Book.OutputFolder) is { } folder)
            vm.Book.OutputFolder = folder;
    }

    private static string? Pick(string title, string startAt)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = title, Multiselect = false };
        if (startAt.Length > 0 && System.IO.Directory.Exists(startAt)) dialog.InitialDirectory = startAt;
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
