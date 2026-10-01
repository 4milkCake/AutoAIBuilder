using System.Windows.Controls;
using System.Windows.Threading;

namespace AutoAIBuilder.Desktop.Views;

public partial class SemanticReviewView : UserControl
{
    private CadReviewWindow? _cadReviewWindow;

    public SemanticReviewView()
    {
        InitializeComponent();
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

    private void CadFit_Click(object sender, System.Windows.RoutedEventArgs e) =>
        CadViewport.Fit();

    private void CadFitAll_Click(object sender, System.Windows.RoutedEventArgs e) =>
        CadViewport.FitAll();

    private void CadZoomIn_Click(object sender, System.Windows.RoutedEventArgs e) =>
        CadViewport.ZoomIn();

    private void CadZoomOut_Click(object sender, System.Windows.RoutedEventArgs e) =>
        CadViewport.ZoomOut();

    private void CadExpand_Click(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (_cadReviewWindow?.IsVisible == true)
        {
            _cadReviewWindow.Activate();
            return;
        }

        _cadReviewWindow = new CadReviewWindow
        {
            DataContext = DataContext,
            Owner = System.Windows.Window.GetWindow(this)
        };
        _cadReviewWindow.Closed += (_, _) => _cadReviewWindow = null;
        _cadReviewWindow.Show();
    }
}
