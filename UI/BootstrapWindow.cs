using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pupstrap.Core;
using Wpf.Ui.Controls;
using Image = System.Windows.Controls.Image;
using TextBlock = System.Windows.Controls.TextBlock;
using WpfButton = Wpf.Ui.Controls.Button;

namespace Pupstrap.UI;

/// <summary>The Fluent and Compact launch dialogs.</summary>
public sealed class BootstrapWindow : FluentWindow, ILaunchDialog
{
    private readonly TextBlock _status = new() { FontSize = 14, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly ProgressBar _progress = new() { Height = 4, Minimum = 0, Maximum = 1, IsIndeterminate = true };
    private readonly bool _showPercent;
    public CancellationTokenSource Cancellation { get; } = new();

    public BootstrapWindow(Settings s)
    {
        Title = string.IsNullOrWhiteSpace(s.DialogTitle) ? "Pupstrap" : s.DialogTitle;
        Icon = LaunchDialog.AppIcon;
        ResizeMode = ResizeMode.NoResize;
        ExtendsContentIntoTitleBar = true;
        WindowBackdropType = WindowBackdropType.Mica;
        WindowCornerPreference = WindowCornerPreference.Round;
        var compact = s.DialogStyle == DialogStyle.Compact;
        Topmost = compact || s.DialogTopmost;
        _showPercent = s.DialogShowPercent;

        MinWidth = 0;
        MinHeight = 0;
        Width = compact ? 340 : 520;
        Height = compact ? 110 : 230;
        LaunchDialog.Place(this, s.DialogPosition);

        var icon = LaunchDialog.Icon(s.DialogIconPath, compact ? 32 : 56);
        var heading = Ui.Primary(Title, compact ? 14 : 20, FontWeights.SemiBold);
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");

        var cancel = new WpfButton { Content = "Cancel", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 90 };
        cancel.Click += (_, _) =>
        {
            Cancellation.Cancel();
            cancel.IsEnabled = false;
        };

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        text.Children.Add(heading);
        text.Children.Add(_status);

        var top = new DockPanel();
        DockPanel.SetDock(icon, Dock.Left);
        top.Children.Add(icon);
        top.Children.Add(text);

        var root = new DockPanel { Margin = compact ? new Thickness(16, 14, 16, 14) : new Thickness(26, 40, 26, 22), LastChildFill = false };
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        _progress.Margin = new Thickness(0, compact ? 12 : 22, 0, 0);
        DockPanel.SetDock(_progress, Dock.Top);
        root.Children.Add(_progress);
        if (!compact && s.DialogShowCancel)
        {
            DockPanel.SetDock(cancel, Dock.Bottom);
            root.Children.Add(cancel);
        }

        var grid = new Grid();
        if (!compact) grid.Children.Add(new TitleBar { Title = "", ShowMaximize = false, ShowMinimize = true, CanMaximize = false });
        grid.Children.Add(root);
        Content = grid;

        Closing += (_, _) => Cancellation.Cancel();
    }

    public void Report(string status, double fraction)
    {
        _status.Text = LaunchDialog.Format(status, fraction, _showPercent);
        if (fraction <= 0 || fraction >= 1)
        {
            _progress.IsIndeterminate = true;
        }
        else
        {
            _progress.IsIndeterminate = false;
            _progress.Value = fraction;
        }
    }
}
