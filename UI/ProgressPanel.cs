using System.Windows;
using System.Windows.Controls;
using Pupstrap.Deployment;

namespace Pupstrap.UI;

/// <summary>Inline status line plus progress bar for work started from the menu.</summary>
public sealed class ProgressPanel : StackPanel
{
    private readonly TextBlock _status;
    private readonly ProgressBar _bar = new() { Height = 4, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 6, 0, 0) };

    public ProgressPanel()
    {
        Margin = new Thickness(0, 4, 0, 10);
        Visibility = Visibility.Collapsed;
        _status = Ui.Secondary("");
        Children.Add(_status);
        Children.Add(_bar);
    }

    public IProgress<InstallProgress> Progress => new Progress<InstallProgress>(p => Report(p.Status, p.Fraction));

    public void Report(string status, double fraction)
    {
        Visibility = Visibility.Visible;
        _status.Text = status;
        _bar.Visibility = Visibility.Visible;
        _bar.IsIndeterminate = fraction <= 0 || fraction >= 1;
        if (!_bar.IsIndeterminate) _bar.Value = fraction;
    }

    public void Done(string status)
    {
        Visibility = Visibility.Visible;
        _status.Text = status;
        _bar.Visibility = Visibility.Collapsed;
    }
}
