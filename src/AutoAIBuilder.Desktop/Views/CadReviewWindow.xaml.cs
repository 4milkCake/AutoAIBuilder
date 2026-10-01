using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AutoAIBuilder.Desktop.Views;

public partial class CadReviewWindow : Window
{
    public CadReviewWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => CadViewport.CenterSelectedPoint();
    }

    private void SemanticPointsGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (sender is not DataGrid { SelectedItem: { } selectedItem } grid)
        {
            return;
        }

        grid.Dispatcher.BeginInvoke(
            () => grid.ScrollIntoView(selectedItem),
            DispatcherPriority.Loaded);
    }

    private void CadFit_Click(object sender, RoutedEventArgs e) =>
        CadViewport.Fit();

    private void CadFitAll_Click(object sender, RoutedEventArgs e) =>
        CadViewport.FitAll();

    private void CadZoomIn_Click(object sender, RoutedEventArgs e) =>
        CadViewport.ZoomIn();

    private void CadZoomOut_Click(object sender, RoutedEventArgs e) =>
        CadViewport.ZoomOut();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        Close();
        e.Handled = true;
    }
}
