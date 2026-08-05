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
}
