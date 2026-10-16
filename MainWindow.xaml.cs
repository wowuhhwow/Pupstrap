using Pupstrap.Pages;
using Wpf.Ui.Controls;

namespace Pupstrap;

public partial class MainWindow : FluentWindow
{
    public MainWindow(string? initialPage = null)
    {
        InitializeComponent();
        var page = initialPage is null ? null : Type.GetType($"Pupstrap.Pages.{initialPage.Replace(" ", "")}Page", false, true);
        Loaded += (_, _) => RootNavigation.Navigate(page ?? typeof(LaunchPage));
    }
}
