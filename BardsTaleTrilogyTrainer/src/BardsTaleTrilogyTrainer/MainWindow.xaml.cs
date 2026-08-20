using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BardsTaleTrilogyTrainer.ViewModels;

namespace BardsTaleTrilogyTrainer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Closed += (_, _) => _vm.Dispose();
    }

    private void WriteAll_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.WriteAll();

    private void LearnAllSpells_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.LearnAllSpells();

    private void SetInfiniteItems_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.SetInfiniteItems();

    private void FullHeal_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.FullHeal();

    private void MaxAttributes_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.MaxAttributes();

    private void ChangeClass_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.ChangeClass();

    private void WriteClassScores_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.WriteClassScores();

    private void MaxClassScores_Click(object sender, RoutedEventArgs e) =>
        _vm.SelectedCharacter?.MaxClassScores();

    /// <summary>
    /// A click on the map. The image is drawn unscaled, so the position within it is already
    /// in the renderer's pixel coordinates and converts straight to a map square.
    /// </summary>
    private void MapImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Image image || image.Source == null) return;
        var p = e.GetPosition(image);
        _vm.Maps.OnMapClicked(p.X, p.Y);
    }
}
