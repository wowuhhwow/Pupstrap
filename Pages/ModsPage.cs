using System.Windows;
using System.Windows.Controls;
using Pupstrap.Core;
using Pupstrap.UI;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Pupstrap.Pages;

public sealed class ModsPage : PupPage
{
    public ModsPage()
    {
        var s = Store.Settings;
        void Save() => Store.SaveSettings();

        var count = Directory.Exists(Paths.Mods) ? Directory.EnumerateFiles(Paths.Mods, "*", SearchOption.AllDirectories).Count() : 0;

        Content = Ui.Page("Mods", "Replace Roblox's files with your own. Changes apply the next time Roblox launches, and the originals come back when you remove a mod.",
            Ui.Card("Mods folder", $"Files here overwrite the matching path in the Roblox folder, e.g. Mods\\content\\sounds\\ouch.ogg. {count} file{(count == 1 ? "" : "s")} right now.",
                Ui.Row(
                    Ui.Toggle(s.UseModsFolder, v => { s.UseModsFolder = v; Save(); }),
                    Ui.Button("Open folder", () => Ui.OpenFolder(Paths.Mods), ControlAppearance.Secondary, SymbolRegular.Folder24)),
                SymbolRegular.FolderOpen24),

            Ui.Section("Built-in mods"),
            FileCard("Custom font", "Replaces every in-game font with one font file.", "Font files|*.ttf;*.otf",
                () => s.CustomFontPath, v => { s.CustomFontPath = v; Save(); }, SymbolRegular.TextFont24),
            FileCard("Death sound", "Replaces the character death sound. Must be an .ogg file.", "Ogg audio|*.ogg",
                () => s.DeathSoundPath, v => { s.DeathSoundPath = v; Save(); }, SymbolRegular.Speaker224),
            FileCard("Mouse cursor", "Replaces the arrow cursor with a PNG (64x64 works best).", "PNG image|*.png",
                () => s.CursorPath, v => { s.CursorPath = v; Save(); }, SymbolRegular.CursorHover24));
    }

    private static UIElement FileCard(string title, string description, string filter, Func<string> get, Action<string> set, SymbolRegular icon)
    {
        var label = Ui.Secondary(Describe(get()));
        label.MaxWidth = 220;
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        label.Margin = new Thickness(0, 0, 8, 0);
        label.VerticalAlignment = VerticalAlignment.Center;

        var choose = Ui.Button("Choose", () =>
        {
            var path = Ui.PickFile(filter);
            if (path is null) return;
            set(path);
            label.Text = Describe(path);
        });
        var clear = Ui.Button("Reset", () =>
        {
            set("");
            label.Text = Describe("");
        });
        return Ui.Card(title, description, Ui.Row(label, choose, clear), icon);
    }

    private static string Describe(string path) => path.Length == 0 ? "Default" : Path.GetFileName(path);
}
