using System.Windows.Controls;
using Pupstrap.Core;
using Pupstrap.UI;
using Wpf.Ui.Controls;

namespace Pupstrap.Pages;

public sealed class IntegrationsPage : PupPage
{
    public IntegrationsPage()
    {
        var s = Store.Settings;
        void Save() => Store.SaveSettings();

        Content = Ui.Page("Integrations", "Extra features that run alongside Roblox while you play.",
            Ui.Card("Activity tracking", "Reads Roblox's log to know which game and server you're in. The options below need this.",
                Ui.Toggle(s.ActivityTracking, v => { s.ActivityTracking = v; Save(); }), SymbolRegular.Pulse24),
            Ui.Card("Server location", "Shows a small popup with the server's city and country when you join.",
                Ui.Toggle(s.ServerLocationNotification, v => { s.ServerLocationNotification = v; Save(); }), SymbolRegular.Location24),

            Ui.Section("Discord Rich Presence"),
            Ui.Card("Show the game on Discord", "Displays the game name, icon and play time on your Discord profile.",
                Ui.Toggle(s.DiscordRichPresence, v => { s.DiscordRichPresence = v; Save(); }), SymbolRegular.Chat24),
            Ui.Card("Discord application ID",
                "Create an application at discord.com/developers, name it what you want shown (for example \"Roblox\") and paste its Application ID here.",
                Ui.Row(
                    Ui.Text(s.DiscordClientId, v => { s.DiscordClientId = v.Trim(); Save(); }, "Application ID", 200),
                    Ui.Button("Open portal", () => Ui.OpenUrl("https://discord.com/developers/applications"))),
                SymbolRegular.Key24),
            Ui.Card("Join button", "Adds a button to your presence so friends can join your server.",
                Ui.Toggle(s.DiscordShowJoinButton, v => { s.DiscordShowJoinButton = v; Save(); }), SymbolRegular.PersonArrowRight24));
    }
}
