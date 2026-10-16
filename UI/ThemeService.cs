using System.Windows.Media;
using Pupstrap.Core;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Pupstrap.UI;

public static class ThemeService
{
    public static readonly (string Name, string Hex)[] Accents =
    {
        ("Pup orange", "#FF9E5E"),
        ("Bubblegum", "#FF6FAE"),
        ("Lavender", "#A78BFA"),
        ("Sky", "#4FA8FF"),
        ("Mint", "#3DDC97"),
        ("Lemon", "#F5C542"),
        ("Cherry", "#F0525E"),
        ("Windows accent", ""),
    };

    public static void Apply(Settings settings)
    {
        ApplicationTheme theme;
        if (settings.Theme == "System")
        {
            ApplicationThemeManager.ApplySystemTheme(false);
            theme = ApplicationThemeManager.GetAppTheme();
        }
        else
        {
            theme = settings.Theme == "Light" ? ApplicationTheme.Light : ApplicationTheme.Dark;
        }
        ApplicationThemeManager.Apply(theme, WindowBackdropType.Mica, false);

        if (TryParseColor(settings.Accent, out var accent))
            ApplicationAccentColorManager.Apply(accent, theme, false);
        else
            ApplicationAccentColorManager.ApplySystemAccent();
    }

    public static bool TryParseColor(string hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        try
        {
            color = (Color)ColorConverter.ConvertFromString(hex);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
