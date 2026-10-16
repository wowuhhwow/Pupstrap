using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Pupstrap.UI;

/// <summary>A small toast in the bottom-right corner that never steals focus from Roblox.</summary>
public sealed class NotificationWindow : Window
{
    private NotificationWindow(string title, string message)
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock { Text = "Pupstrap", FontSize = 11, Opacity = 0.7, Foreground = Brushes.White });
        stack.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 2, 0, 2) });
        stack.Children.Add(new TextBlock { Text = message, FontSize = 13, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, MaxWidth = 320 });

        var icon = LaunchDialog.Icon("", 36);
        icon.Margin = new Thickness(0, 2, 12, 0);
        icon.VerticalAlignment = VerticalAlignment.Top;
        var row = new DockPanel();
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        row.Children.Add(stack);

        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF0, 0x20, 0x20, 0x24)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 18, 12),
            Child = row,
            MinWidth = 260,
        };

        Loaded += (_, _) =>
        {
            var area = SystemParameters.WorkArea;
            Left = area.Right - ActualWidth - 16;
            Top = area.Bottom - ActualHeight - 16;
            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180)));
        };
    }

    public static void Show(string title, string message, int seconds = 6)
    {
        var window = new NotificationWindow(title, message);
        window.Show();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(250));
            fade.Completed += (_, _) => window.Close();
            window.BeginAnimation(OpacityProperty, fade);
        };
        timer.Start();
    }
}
