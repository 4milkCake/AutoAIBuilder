using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace AutoAIBuilder.Desktop.Views;

public partial class RecognitionCadReviewWindow : Window
{
    public RecognitionCadReviewWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            CadViewport.CenterSelectedPoint();
            ScrollSelectedCandidateIntoView();
        };
    }

    private void RecognitionCandidatesGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        ScrollSelectedCandidateIntoView();

    private void CadViewport_OverlayPointSelected(
        object sender,
        RoutedEventArgs e) =>
        ScrollSelectedCandidateIntoView();

    private void ScrollSelectedCandidateIntoView()
    {
        if (RecognitionCandidatesGrid.SelectedItem is not { } selectedItem)
        {
            return;
        }

        RecognitionCandidatesGrid.Dispatcher.BeginInvoke(
            () =>
            {
                RecognitionCandidatesGrid.ScrollIntoView(selectedItem);
                RecognitionCandidatesGrid.UpdateLayout();
                if (RecognitionCandidatesGrid.ItemContainerGenerator
                        .ContainerFromItem(selectedItem)
                    is DataGridRow row)
                {
                    row.BringIntoView();
                }
            },
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
