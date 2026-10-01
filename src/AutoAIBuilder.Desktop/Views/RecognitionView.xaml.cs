using System.Windows.Controls;
using System.Windows.Threading;

namespace AutoAIBuilder.Desktop.Views;

public partial class RecognitionView : UserControl
{
    private RecognitionCadReviewWindow? _cadReviewWindow;

    public RecognitionView()
    {
        InitializeComponent();
    }

    private void RecognitionCandidatesGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (sender is DataGrid grid)
        {
            ScrollSelectedCandidateIntoView(grid);
        }
    }

    private void CadViewport_OverlayPointSelected(
        object sender,
        System.Windows.RoutedEventArgs e) =>
        ScrollSelectedCandidateIntoView(RecognitionCandidatesGrid);

    private static void ScrollSelectedCandidateIntoView(DataGrid grid)
    {
        if (grid.SelectedItem is not { } selectedItem)
        {
            return;
        }

        grid.Dispatcher.BeginInvoke(
            () =>
            {
                grid.ScrollIntoView(selectedItem);
                grid.UpdateLayout();
                if (grid.ItemContainerGenerator.ContainerFromItem(selectedItem)
                    is DataGridRow row)
                {
                    row.BringIntoView();
                }
            },
            DispatcherPriority.Loaded);
    }

    private void CadFit_Click(
        object sender,
        System.Windows.RoutedEventArgs e) =>
        CadViewport.Fit();

    private void CadFitAll_Click(
        object sender,
        System.Windows.RoutedEventArgs e) =>
        CadViewport.FitAll();

    private void CadZoomIn_Click(
        object sender,
        System.Windows.RoutedEventArgs e) =>
        CadViewport.ZoomIn();

    private void CadZoomOut_Click(
        object sender,
        System.Windows.RoutedEventArgs e) =>
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

        _cadReviewWindow = new RecognitionCadReviewWindow
        {
            DataContext = DataContext,
            Owner = System.Windows.Window.GetWindow(this)
        };
        _cadReviewWindow.Closed += (_, _) => _cadReviewWindow = null;
        _cadReviewWindow.Show();
    }
}
