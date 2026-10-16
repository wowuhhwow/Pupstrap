using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui.Controls;
using WpfButton = Wpf.Ui.Controls.Button;
using WpfTextBox = Wpf.Ui.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace Pupstrap.UI;

/// <summary>Builders for the card-based settings layout used by every page.</summary>
public static class Ui
{
    /// <summary>Page body. NavigationView already scrolls page content, so this must not add another ScrollViewer.</summary>
    public static StackPanel Page(string title, string subtitle, params UIElement[] children)
    {
        var stack = new StackPanel { Margin = new Thickness(32, 20, 32, 32), MaxWidth = 1100 };
        stack.Children.Add(Primary(title, 30, FontWeights.SemiBold));
        var sub = Secondary(subtitle);
        sub.FontSize = 14;
        sub.Margin = new Thickness(0, 2, 0, 18);
        stack.Children.Add(sub);
        foreach (var child in children) stack.Children.Add(child);
        return stack;
    }

    /// <summary>Lets the mouse wheel reach the page when a nested list or grid has nothing left to scroll that way.</summary>
    public static void InstallWheelForwarding() =>
        EventManager.RegisterClassHandler(typeof(ScrollViewer), UIElement.PreviewMouseWheelEvent, new System.Windows.Input.MouseWheelEventHandler((sender, e) =>
        {
            var viewer = (ScrollViewer)sender;
            if (e.Handled || NearestScrollViewer(e.OriginalSource as DependencyObject) != viewer) return;
            var atLimit = e.Delta > 0 ? viewer.VerticalOffset <= 0 : viewer.VerticalOffset >= viewer.ScrollableHeight;
            if (!atLimit || VisualTreeHelper.GetParent(viewer) is not UIElement parent) return;
            e.Handled = true;
            parent.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = UIElement.MouseWheelEvent, Source = viewer });
        }));

    private static ScrollViewer? NearestScrollViewer(DependencyObject? node)
    {
        while (node is not null)
        {
            if (node is ScrollViewer viewer) return viewer;
            node = node is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
        }
        return null;
    }

    public static TextBlock Primary(string text, double size = 14, FontWeight? weight = null)
    {
        var block = new TextBlock { Text = text, FontSize = size, FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        return block;
    }

    public static TextBlock Secondary(string text)
    {
        var block = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 13 };
        block.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorSecondaryBrush");
        return block;
    }

    public static TextBlock Section(string text)
    {
        var block = Primary(text, 15, FontWeights.SemiBold);
        block.Margin = new Thickness(2, 22, 0, 8);
        return block;
    }

    public static CardControl Card(string title, string description, UIElement? control, SymbolRegular icon = SymbolRegular.Empty)
    {
        var header = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(Primary(title, 14, FontWeights.SemiBold));
        if (description.Length > 0) header.Children.Add(Secondary(description));
        var card = new CardControl { Header = header, Content = control, Margin = new Thickness(0, 0, 0, 4), Padding = new Thickness(18, 14, 18, 14) };
        if (icon != SymbolRegular.Empty) card.Icon = new SymbolIcon(icon) { FontSize = 22 };
        return card;
    }

    public static LinearGradientBrush AccentGradient()
    {
        var accent = ThemeService.TryParseColor(Core.Store.Settings.Accent, out var c) ? c : SystemParameters.WindowGlassColor;
        var dark = Color.FromRgb((byte)(accent.R * 0.45), (byte)(accent.G * 0.45), (byte)(accent.B * 0.45));
        return new LinearGradientBrush(accent, dark, new Point(0, 0), new Point(1, 1));
    }

    /// <summary>Accent-gradient banner for the top of a page, with the app icon on the left.</summary>
    public static Border Hero(string title, UIElement subtitle, UIElement? action)
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var badge = LaunchDialog.Icon("", 68);
        badge.Margin = new Thickness(0, 0, 18, 0);
        badge.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 12, ShadowDepth = 2, Opacity = 0.3 };
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
        text.Children.Add(subtitle);
        Grid.SetColumn(text, 1);
        grid.Children.Add(badge);
        grid.Children.Add(text);
        if (action is not null)
        {
            Grid.SetColumn(action, 2);
            grid.Children.Add(action);
        }

        return new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(24, 22, 24, 22),
            Margin = new Thickness(0, 0, 0, 8),
            Background = AccentGradient(),
            Child = grid,
        };
    }

    /// <summary>Small stat box with an icon, a label and a value underneath.</summary>
    public static Border Tile(SymbolRegular icon, string label, UIElement value)
    {
        var stack = new StackPanel();
        var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        var symbol = new SymbolIcon(icon) { FontSize = 18, Margin = new Thickness(0, 0, 8, 0) };
        symbol.SetResourceReference(Control.ForegroundProperty, "AccentTextFillColorPrimaryBrush");
        head.Children.Add(symbol);
        head.Children.Add(Secondary(label));
        stack.Children.Add(head);
        stack.Children.Add(value);
        var tile = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 14, 16, 14), Margin = new Thickness(0, 0, 4, 4), Child = stack };
        tile.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        tile.SetResourceReference(Border.BorderBrushProperty, "CardStrokeColorDefaultBrush");
        tile.BorderThickness = new Thickness(1);
        return tile;
    }

    public static System.Windows.Controls.Primitives.UniformGrid Tiles(params UIElement[] tiles)
    {
        var grid = new System.Windows.Controls.Primitives.UniformGrid { Columns = tiles.Length, Rows = 1 };
        foreach (var tile in tiles) grid.Children.Add(tile);
        return grid;
    }

    public static ToggleSwitch Toggle(bool value, Action<bool> changed)
    {
        var toggle = new ToggleSwitch { IsChecked = value };
        toggle.Checked += (_, _) => changed(true);
        toggle.Unchecked += (_, _) => changed(false);
        return toggle;
    }

    public static ComboBox Combo(IEnumerable<string> items, int selected, Action<int> changed, double width = 200)
    {
        var combo = new ComboBox { MinWidth = width };
        foreach (var item in items) combo.Items.Add(item);
        combo.SelectedIndex = selected;
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedIndex >= 0) changed(combo.SelectedIndex);
        };
        return combo;
    }

    public static WpfTextBox Text(string value, Action<string> changed, string placeholder = "", double width = 260)
    {
        var box = new WpfTextBox { Text = value, PlaceholderText = placeholder, MinWidth = width, MaxWidth = 420 };
        box.LostFocus += (_, _) => changed(box.Text);
        return box;
    }

    public static WpfButton Button(string text, Action click, ControlAppearance appearance = ControlAppearance.Secondary,
        SymbolRegular icon = SymbolRegular.Empty)
    {
        var button = new WpfButton { Content = text, Appearance = appearance, Margin = new Thickness(6, 0, 0, 0), MinWidth = 90 };
        if (icon != SymbolRegular.Empty) button.Icon = new SymbolIcon(icon);
        button.Click += (_, _) => click();
        return button;
    }

    public static WpfButton AsyncButton(string text, Func<Task> click, ControlAppearance appearance = ControlAppearance.Secondary,
        SymbolRegular icon = SymbolRegular.Empty)
    {
        WpfButton? button = null;
        button = Button(text, async () =>
        {
            button!.IsEnabled = false;
            try { await click(); }
            catch (Exception ex)
            {
                Core.Logger.Error($"{text} failed", ex);
                Error(ex.Message);
            }
            finally { button.IsEnabled = true; }
        }, appearance, icon);
        return button;
    }

    public static StackPanel Row(params UIElement[] children)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        foreach (var child in children) row.Children.Add(child);
        return row;
    }

    public static InfoBar Info(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Informational) =>
        new() { Title = title, Message = message, IsOpen = true, IsClosable = false, Severity = severity, Margin = new Thickness(0, 0, 0, 8) };

    public static Border Spacer(double height) => new() { Height = height, Background = Brushes.Transparent };

    public static void Error(string message) =>
        System.Windows.MessageBox.Show(message, "Pupstrap", MessageBoxButton.OK, MessageBoxImage.Warning);

    public static void Message(string message) =>
        System.Windows.MessageBox.Show(message, "Pupstrap", MessageBoxButton.OK, MessageBoxImage.Information);

    public static bool Confirm(string message) =>
        System.Windows.MessageBox.Show(message, "Pupstrap", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public static string? PickFile(string filter)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = filter };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
    }

    public static void OpenUrl(string url) =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
}
