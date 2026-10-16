using System.Windows;
using System.Windows.Controls;
using Pupstrap.Core;
using Pupstrap.UI;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Pupstrap.Pages;

public sealed class AppearancePage : PupPage
{
    private static readonly string[] Themes = { "Dark", "Light", "System" };

    public AppearancePage()
    {
        var s = Store.Settings;
        void Save() => Store.SaveSettings();

        var accentIndex = Array.FindIndex(ThemeService.Accents, a => a.Hex.Equals(s.Accent, StringComparison.OrdinalIgnoreCase));
        var customAccent = Ui.Text(accentIndex < 0 ? s.Accent : "", v =>
        {
            if (!ThemeService.TryParseColor(v, out _)) return;
            s.Accent = v.Trim();
            Save();
            ThemeService.Apply(s);
        }, "#RRGGBB", 110);

        var iconLabel = Ui.Secondary(s.DialogIconPath.Length == 0 ? "Pup" : Path.GetFileName(s.DialogIconPath));
        iconLabel.VerticalAlignment = VerticalAlignment.Center;
        iconLabel.Margin = new Thickness(0, 0, 8, 0);
        var backgroundLabel = Ui.Secondary(s.DialogBackgroundPath.Length == 0 ? "Accent gradient" : Path.GetFileName(s.DialogBackgroundPath));
        backgroundLabel.VerticalAlignment = VerticalAlignment.Center;
        backgroundLabel.Margin = new Thickness(0, 0, 8, 0);

        Content = Ui.Page("Appearance", "How Pupstrap and its launch window look.",
            Ui.Card("Theme", "", Ui.Combo(Themes, Math.Max(0, Array.IndexOf(Themes, s.Theme)), i =>
            {
                s.Theme = Themes[i];
                Save();
                ThemeService.Apply(s);
            }), SymbolRegular.DarkTheme24),
            Ui.Card("Accent colour", "Pick a preset or type your own hex colour.", Ui.Row(
                Ui.Combo(ThemeService.Accents.Select(a => a.Name), accentIndex, i =>
                {
                    s.Accent = ThemeService.Accents[i].Hex;
                    customAccent.Text = "";
                    Save();
                    ThemeService.Apply(s);
                }, 170),
                Ui.Spacer(0), customAccent), SymbolRegular.Color24),

            Ui.Section("Launch window"),
            StyleCard(),
            Ui.Card("Position", "Where the launch window appears on screen.",
                Ui.Combo(LaunchDialog.Positions.Select(p => p.Name),
                    Math.Max(0, Array.FindIndex(LaunchDialog.Positions, p => p.Position == s.DialogPosition)),
                    i => { s.DialogPosition = LaunchDialog.Positions[i].Position; Save(); }, 170),
                SymbolRegular.Location24),
            Ui.Card("Title", "Text shown at the top of the launch window.",
                Ui.Text(s.DialogTitle, v => { s.DialogTitle = v; Save(); }, "Pupstrap", 200), SymbolRegular.TextT24),
            Ui.Card("Icon", "A PNG or ICO shown in the launch window.", Ui.Row(iconLabel,
                Ui.Button("Choose", () =>
                {
                    var path = Ui.PickFile("Images|*.png;*.ico;*.jpg");
                    if (path is null) return;
                    s.DialogIconPath = path;
                    iconLabel.Text = Path.GetFileName(path);
                    Save();
                }),
                Ui.Button("Reset", () =>
                {
                    s.DialogIconPath = "";
                    iconLabel.Text = "Pup";
                    Save();
                })), SymbolRegular.Image24),
            Ui.Card("Background image", "Pupstrap style only. Darkened a little so the text stays readable.", Ui.Row(backgroundLabel,
                Ui.Button("Choose", () =>
                {
                    var path = Ui.PickFile("Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif");
                    if (path is null) return;
                    s.DialogBackgroundPath = path;
                    backgroundLabel.Text = Path.GetFileName(path);
                    Save();
                }),
                Ui.Button("Reset", () =>
                {
                    s.DialogBackgroundPath = "";
                    backgroundLabel.Text = "Accent gradient";
                    Save();
                })), SymbolRegular.ImageMultiple24),
            Ui.Card("Show percentage", "Adds the download percentage next to the status.",
                Ui.Toggle(s.DialogShowPercent, v => { s.DialogShowPercent = v; Save(); }), SymbolRegular.DataBarHorizontal24),
            Ui.Card("Cancel button", "Lets you stop an update or launch from the window.",
                Ui.Toggle(s.DialogShowCancel, v => { s.DialogShowCancel = v; Save(); }), SymbolRegular.DismissCircle24),
            Ui.Card("Always on top", "Keeps the launch window above other windows. Compact is always on top.",
                Ui.Toggle(s.DialogTopmost, v => { s.DialogTopmost = v; Save(); }), SymbolRegular.Pin24),
            Ui.Card("Stay open until Roblox appears", "Closes the window once Roblox's own window shows up instead of right after starting it.",
                Ui.Toggle(s.DialogWaitForRobloxWindow, v => { s.DialogWaitForRobloxWindow = v; Save(); }), SymbolRegular.Timer24),
            Ui.Card("Preview", "Shows the launch window for a few seconds.", Ui.AsyncButton("Preview", PreviewAsync, ControlAppearance.Primary, SymbolRegular.Eye24),
                SymbolRegular.Play24));
    }

    private static CardControl StyleCard()
    {
        var s = Store.Settings;
        var styles = LaunchDialog.Styles;
        var index = Math.Max(0, Array.FindIndex(styles, x => x.Style == s.DialogStyle));
        var card = Ui.Card("Style", styles[index].Description, null, SymbolRegular.Window24);
        card.Content = Ui.Combo(styles.Select(x => x.Name), index, i =>
        {
            s.DialogStyle = styles[i].Style;
            Store.SaveSettings();
            if (card.Header is StackPanel { Children.Count: > 1 } header && header.Children[1] is TextBlock description)
                description.Text = styles[i].Description;
        }, 170);
        return card;
    }

    private static async Task PreviewAsync()
    {
        var s = Store.Settings;
        if (s.DialogStyle == DialogStyle.Hidden)
        {
            Ui.Message("The Hidden style doesn't show a window.");
            return;
        }
        var window = LaunchDialog.Create(s)!;
        window.Show();
        string[] steps = { "Checking for updates", "Downloading Roblox (40 / 120 MB)", "Downloading Roblox (95 / 120 MB)", "Applying mods", "Starting Roblox" };
        for (var i = 0; i < steps.Length && !window.Cancellation.IsCancellationRequested; i++)
        {
            window.Report(steps[i], i is 1 or 2 ? i * 0.35 : 0);
            await Task.Delay(900);
        }
        window.Close();
    }
}
