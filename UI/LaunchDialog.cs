using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Pupstrap.Core;
using Image = System.Windows.Controls.Image;

namespace Pupstrap.UI;

/// <summary>Progress window shown while Roblox is updated and started.</summary>
public interface ILaunchDialog
{
    CancellationTokenSource Cancellation { get; }
    void Report(string status, double fraction);
    void Show();
    void Hide();
    void Close();
}

public static class LaunchDialog
{
    public static readonly (DialogStyle Style, string Name, string Description)[] Styles =
    {
        (DialogStyle.Pupstrap, "Pupstrap", "Accent-coloured card with the paw icon. Supports a custom background image."),
        (DialogStyle.Fluent, "Fluent", "Windows 11 style window with Mica."),
        (DialogStyle.Compact, "Compact", "Small always-on-top bar."),
        (DialogStyle.Minimal, "Minimal", "A floating pill with just the status."),
        (DialogStyle.Classic, "Classic", "Plain light window like the old Roblox launcher."),
        (DialogStyle.Hidden, "Hidden", "No window at all."),
    };

    public static readonly (DialogPosition Position, string Name)[] Positions =
    {
        (DialogPosition.Center, "Centre"),
        (DialogPosition.BottomRight, "Bottom right"),
        (DialogPosition.BottomCenter, "Bottom centre"),
        (DialogPosition.TopCenter, "Top centre"),
    };

    public static ILaunchDialog? Create(Settings s) => s.DialogStyle switch
    {
        DialogStyle.Hidden => null,
        DialogStyle.Fluent or DialogStyle.Compact => new BootstrapWindow(s),
        _ => new SplashWindow(s),
    };

    public static ImageSource AppIcon { get; } = LoadAppIcon();

    private static ImageSource LoadAppIcon()
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri("pack://application:,,,/Assets/pupstrap.png");
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static Image Icon(string customPath, double size)
    {
        ImageSource source = AppIcon;
        if (File.Exists(customPath))
        {
            try { source = new BitmapImage(new Uri(customPath)); }
            catch (Exception ex) when (ex is NotSupportedException or IOException or UriFormatException) { }
        }
        var image = new Image { Source = source, Width = size, Height = size };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        return image;
    }

    public static string Format(string status, double fraction, bool showPercent) =>
        showPercent && fraction > 0 && fraction < 1 ? $"{status}  {fraction:P0}" : status;

    /// <summary>Positions a window using its fixed size, or its measured size once it has loaded.</summary>
    public static void Place(Window window, DialogPosition position)
    {
        var area = SystemParameters.WorkArea;
        var width = double.IsNaN(window.Width) ? window.ActualWidth : window.Width;
        var height = double.IsNaN(window.Height) ? window.ActualHeight : window.Height;
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        var centreX = area.Left + (area.Width - width) / 2;
        (window.Left, window.Top) = position switch
        {
            DialogPosition.BottomRight => (area.Right - width - 12, area.Bottom - height - 12),
            DialogPosition.BottomCenter => (centreX, area.Bottom - height - 24),
            DialogPosition.TopCenter => (centreX, area.Top + 24),
            _ => (centreX, area.Top + (area.Height - height) / 2),
        };
    }
}

/// <summary>Flat progress bar that looks the same in every dialog style and theme.</summary>
public sealed class SimpleBar : Grid
{
    private readonly Border _fill = new() { HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TranslateTransform _slide = new();
    private bool _indeterminate;

    public SimpleBar(Brush track, Brush fill, double height = 4)
    {
        Height = height;
        ClipToBounds = true;
        var radius = new CornerRadius(height / 2);
        Children.Add(new Border { Background = track, CornerRadius = radius });
        _fill.Background = fill;
        _fill.CornerRadius = radius;
        _fill.RenderTransform = _slide;
        Children.Add(_fill);
        SizeChanged += (_, _) => Update(_lastValue);
        SetIndeterminate();
    }

    private double _lastValue;

    public void SetProgress(double fraction)
    {
        _lastValue = Math.Clamp(fraction, 0, 1);
        if (_indeterminate)
        {
            _indeterminate = false;
            _slide.BeginAnimation(TranslateTransform.XProperty, null);
            _slide.X = 0;
        }
        Update(_lastValue);
    }

    public void SetIndeterminate()
    {
        _indeterminate = true;
        Update(0);
    }

    private void Update(double fraction)
    {
        var width = ActualWidth;
        if (width <= 0) return;
        if (_indeterminate)
        {
            _fill.Width = width * 0.3;
            _slide.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(-width * 0.3, width, TimeSpan.FromSeconds(1.3))
            {
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            });
        }
        else
        {
            _fill.Width = width * fraction;
        }
    }
}
