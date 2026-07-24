using System.Windows;
using AutoAIBuilder.Desktop.Composition;

namespace AutoAIBuilder.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = DesktopCompositionRoot.CreateMainWindowViewModel();
    }
}
