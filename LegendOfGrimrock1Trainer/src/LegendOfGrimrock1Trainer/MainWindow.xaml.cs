using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using LegendOfGrimrock1Trainer.Game;
using LegendOfGrimrock1Trainer.ViewModels;

namespace LegendOfGrimrock1Trainer;

/// <summary>Shell window: owns the session view-model and releases it on close.</summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    /// <summary>Builds the window and binds the session.</summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // Static reference data; no binding to the session, so it is filled once here.
        SpellGrid.ItemsSource = GameTables.Spells
            .OrderBy(s => s.Skill, StringComparer.Ordinal)
            .ThenBy(s => s.SkillLevel)
            .ToList();

        // Tell the session when the user is mid-edit. Grimrock drains food and counts condition
        // timers down continuously, so without this the four-times-a-second refresh would replace
        // the text in whichever box is being typed into — LostFocus defers the write *out* of a
        // control, but nothing stops a source PropertyChanged pushing a new value back *in*.
        //
        // Asked as a question each time rather than tracked as a flag: see SetEditorProbe. The window
        // is the right thing to ask because FocusManager gives *logical* focus, which one control
        // holds at a time, survives the application being deactivated, and cannot outlive the element
        // that holds it. That covers every editor without per-control wiring — the standalone text
        // boxes, the ones inside the per-champion tab template, and the TextBox a DataGrid builds
        // when a text cell enters edit mode. Check-box cells commit instantly and have nothing to
        // clobber.
        _vm.SetEditorProbe(IsEditorFocused);

        Closed += (_, _) =>
        {
            _vm.SetEditorProbe(null);
            _vm.Dispose();
        };
    }

    /// <summary>Whether a text editor currently holds logical focus inside this window.</summary>
    private bool IsEditorFocused() => FocusManager.GetFocusedElement(this) is TextBoxBase;
}
