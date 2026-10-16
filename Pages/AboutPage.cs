using System.Windows.Controls;
using Pupstrap.Core;
using Pupstrap.UI;
using Wpf.Ui.Controls;

namespace Pupstrap.Pages;

public sealed class AboutPage : PupPage
{
    public AboutPage()
    {
        Content = Ui.Page("About", "Pupstrap is a Roblox bootstrapper inspired by Bloxstrap and Fishstrap. It is not affiliated with Roblox.",
            Ui.Card("Version", App.Version, null, SymbolRegular.AnimalDog24),
            Ui.Card("Data folder", Paths.Base, Ui.Button("Open", () => Ui.OpenFolder(Paths.Base)), SymbolRegular.Folder24),
            Ui.Card("Logs", "Attach the newest log when something breaks.", Ui.Button("Open", () => Ui.OpenFolder(Paths.Logs)), SymbolRegular.DocumentText24),
            Ui.Card("Roblox logs", Paths.RobloxLogs, Ui.Button("Open", () => Ui.OpenFolder(Paths.RobloxLogs)), SymbolRegular.DocumentSearch24),
            Ui.Section("Thanks"),
            Ui.Card("Bloxstrap and Fishstrap", "For showing how a good bootstrapper should work.", null, SymbolRegular.Heart24),
            Ui.Card("WPF UI", "The Fluent controls Pupstrap is built with.", null, SymbolRegular.PaintBrush24));
    }
}
