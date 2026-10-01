using System.Windows;
using System.Windows.Controls;

namespace AutoAIBuilder.Desktop.Views;

public partial class AutomationPreviewView : UserControl
{
    public AutomationPreviewView()
    {
        InitializeComponent();
    }

    private void Fit_Click(object sender, RoutedEventArgs e)
    {
        BeforeViewport.Fit();
        AfterViewport.Fit();
    }

    private void FitAll_Click(object sender, RoutedEventArgs e)
    {
        BeforeViewport.FitAll();
        AfterViewport.FitAll();
    }
}
