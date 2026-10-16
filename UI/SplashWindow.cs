using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Pupstrap.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;
using TextBlock = System.Windows.Controls.TextBlock;
using WpfButton = Wpf.Ui.Controls.Button;

namespace Pupstrap.UI;

/// <summary>The Pupstrap, Minimal and Classic launch dialogs.</summary>
public sealed class SplashWindow : Window, ILaunchDialog
{
    private readonly TextBlock _status = new() { FontSize = 13, TextTrimming = TextTrimming.CharacterEllipsis };
    private readonly SimpleBar _bar;
    private readonly bool _showPercent;

    public CancellationTokenSource Cancellation { get; } = new();

    public SplashWindow(Settings s)
    {
        Title = string.IsNullOrWhiteSpace(s.DialogTitle) ? "Pupstrap" : s.DialogTitle;
        Icon = LaunchDialog.AppIcon;
        ResizeMode = ResizeMode.NoResize;
        Topmost = s.DialogTopmost;
        _showPercent = s.DialogShowPercent;
        _status.Text = "Starting";

        switch (s.DialogStyle)
        {
            case DialogStyle.Minimal:
                _bar = new SimpleBar(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), AccentBrush(), 3);
                BuildMinimal(s);
                break;
            case DialogStyle.Classic:
                _bar = new SimpleBar(new SolidColorBrush(Color.FromRgb(0xE1, 0xE1, 0xE1)), new SolidColorBrush(Color.FromRgb(0x00, 0x74, 0xBD)), 14);
                BuildClassic(s);
                break;
            default:
                _bar = new SimpleBar(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), Brushes.White, 6);
                BuildPupstrap(s);
                break;
        }

        LaunchDialog.Place(this, s.DialogPosition);
        Closing += (_, _) => Cancellation.Cancel();
    }

    public void Report(string status, double fraction)
    {
        _status.Text = LaunchDialog.Format(status, fraction, _showPercent);
        if (fraction <= 0 || fraction >= 1) _bar.SetIndeterminate();
        else _bar.SetProgress(fraction);
    }

    private void BuildPupstrap(Settings s)
    {
        MakeBorderless(520, 236);

        var heading = new TextBlock { Text = Title, FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
        _status.Foreground = new SolidColorBrush(Color.FromArgb(225, 255, 255, 255));
        _status.Margin = new Thickness(0, 4, 0, 0);

        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
        text.Children.Add(heading);
        text.Children.Add(_status);

        var top = new DockPanel();
        var icon = LaunchDialog.Icon(s.DialogIconPath, 76);
        icon.Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 2, Opacity = 0.35 };
        DockPanel.SetDock(icon, Dock.Left);
        top.Children.Add(icon);
        top.Children.Add(text);

        var root = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        _bar.Margin = new Thickness(0, 24, 0, 0);
        DockPanel.SetDock(_bar, Dock.Top);
        root.Children.Add(_bar);
        if (s.DialogShowCancel)
        {
            var cancel = CancelButton(Brushes.White);
            cancel.HorizontalAlignment = HorizontalAlignment.Right;
            cancel.Margin = new Thickness(0, 14, 0, 0);
            DockPanel.SetDock(cancel, Dock.Bottom);
            root.Children.Add(cancel);
        }

        Brush background = Ui.AccentGradient();
        var layers = new Grid();
        if (TryLoadImage(s.DialogBackgroundPath, out var image))
        {
            background = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
            layers.Children.Add(new Border { Background = new LinearGradientBrush(Color.FromArgb(200, 0, 0, 0), Color.FromArgb(60, 0, 0, 0), 0) });
        }
        layers.Children.Add(new Border { Padding = new Thickness(28, 26, 28, 22), Child = root });

        Content = Card(background, new CornerRadius(16), layers);
    }

    private void BuildMinimal(Settings s)
    {
        MakeBorderless(380, 92);

        _status.Foreground = Brushes.White;
        _status.VerticalAlignment = VerticalAlignment.Center;

        var row = new DockPanel();
        var icon = LaunchDialog.Icon(s.DialogIconPath, 30);
        icon.Margin = new Thickness(0, 0, 12, 0);
        DockPanel.SetDock(icon, Dock.Left);
        row.Children.Add(icon);
        if (s.DialogShowCancel)
        {
            var close = CancelButton(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), "\u2715");
            close.MinWidth = 0;
            close.Padding = new Thickness(8, 2, 8, 3);
            close.Margin = new Thickness(8, 0, 0, 0);
            DockPanel.SetDock(close, Dock.Right);
            row.Children.Add(close);
        }
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(_status);
        _bar.Margin = new Thickness(0, 7, 0, 0);
        text.Children.Add(_bar);
        row.Children.Add(text);

        Content = Card(new SolidColorBrush(Color.FromArgb(0xF2, 0x1C, 0x1C, 0x20)), new CornerRadius(34),
            new Border { Padding = new Thickness(18, 0, 18, 0), Child = row });
    }

    private void BuildClassic(Settings s)
    {
        Width = 420;
        SizeToContent = SizeToContent.Height;
        Loaded += (_, _) => LaunchDialog.Place(this, s.DialogPosition);
        WindowStyle = WindowStyle.SingleBorderWindow;
        Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
        Resources.MergedDictionaries.Add(new ThemesDictionary { Theme = ApplicationTheme.Light });

        _status.Foreground = Brushes.Black;
        _status.FontSize = 12;
        _status.VerticalAlignment = VerticalAlignment.Center;

        var top = new DockPanel();
        var icon = LaunchDialog.Icon(s.DialogIconPath, 44);
        icon.Margin = new Thickness(0, 0, 14, 0);
        DockPanel.SetDock(icon, Dock.Left);
        top.Children.Add(icon);
        top.Children.Add(_status);

        var root = new DockPanel { Margin = new Thickness(18, 16, 18, 14), LastChildFill = false };
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);
        _bar.Margin = new Thickness(0, 14, 0, 0);
        DockPanel.SetDock(_bar, Dock.Top);
        root.Children.Add(_bar);
        if (s.DialogShowCancel)
        {
            var cancel = new System.Windows.Controls.Button { Content = "Cancel", MinWidth = 86, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            cancel.Click += (_, _) => { Cancellation.Cancel(); cancel.IsEnabled = false; };
            DockPanel.SetDock(cancel, Dock.Bottom);
            root.Children.Add(cancel);
        }
        Content = root;
    }

    private void MakeBorderless(double width, double height)
    {
        const double shadow = 16;
        Width = width + shadow * 2;
        Height = height + shadow * 2;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed) DragMove();
        };
    }

    private static Grid Card(Brush background, CornerRadius radius, UIElement child)
    {
        var shadow = new Border
        {
            Background = Brushes.Black,
            CornerRadius = radius,
            Effect = new DropShadowEffect { BlurRadius = 18, ShadowDepth = 3, Opacity = 0.45 },
        };
        var surface = new Border
        {
            Background = background,
            CornerRadius = radius,
            BorderBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            BorderThickness = new Thickness(1),
            Child = child,
        };
        surface.SizeChanged += (_, e) =>
            surface.Clip = new RectangleGeometry(new Rect(e.NewSize), radius.TopLeft, radius.TopLeft);
        return new Grid { Margin = new Thickness(16), Children = { shadow, surface } };
    }

    private WpfButton CancelButton(Brush foreground, string text = "Cancel")
    {
        var button = new WpfButton
        {
            Content = text,
            Appearance = Wpf.Ui.Controls.ControlAppearance.Transparent,
            Foreground = foreground,
            MinWidth = 80,
        };
        button.Click += (_, _) => { Cancellation.Cancel(); button.IsEnabled = false; };
        return button;
    }

    private static Brush AccentBrush()
    {
        var brush = new SolidColorBrush();
        if (ThemeService.TryParseColor(Store.Settings.Accent, out var color)) brush.Color = color;
        else brush.Color = SystemParameters.WindowGlassColor;
        return brush;
    }

    private static bool TryLoadImage(string path, out BitmapImage image)
    {
        image = null!;
        if (!File.Exists(path)) return false;
        try
        {
            image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(path);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            return true;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or UriFormatException)
        {
            return false;
        }
    }
}
