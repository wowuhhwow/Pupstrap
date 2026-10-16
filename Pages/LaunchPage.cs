using System.Windows;
using System.Windows.Controls;
using Pupstrap.Accounts;
using Pupstrap.Core;
using Pupstrap.Deployment;
using Pupstrap.Launching;
using Pupstrap.UI;
using Wpf.Ui.Controls;

namespace Pupstrap.Pages;

public sealed class LaunchPage : PupPage
{
    private readonly ProgressPanel _progress = new();
    private readonly System.Windows.Controls.TextBlock _latest = Ui.Secondary("Checking...");
    private readonly System.Windows.Controls.TextBlock _running = Ui.Secondary("");

    public LaunchPage()
    {
        var settings = Store.Settings;
        var installed = Store.State.InstalledVersion;

        var launch = Ui.AsyncButton("Launch Roblox", () => LaunchAsync(null), ControlAppearance.Secondary, SymbolRegular.Play24);
        launch.Padding = new Thickness(26, 12, 26, 12);
        launch.FontSize = 16;
        launch.FontWeight = FontWeights.SemiBold;
        launch.VerticalAlignment = VerticalAlignment.Center;

        var placeBox = Ui.Text(settings.LastPlaceInput, v => { settings.LastPlaceInput = v; Store.SaveSettings(); }, "Place ID or game link", 300);
        var join = Ui.AsyncButton("Join", async () =>
        {
            settings.LastPlaceInput = placeBox.Text;
            Store.SaveSettings();
            var target = RobloxApi.ParseJoinTarget(placeBox.Text, null)
                         ?? throw new InvalidOperationException("Enter a place ID or a roblox.com/games/... link.");
            var uri = $"roblox://experiences/start?placeId={target.PlaceId}";
            if (target.JobId is not null) uri += $"&gameInstanceId={target.JobId}";
            if (target.PrivateServerLinkCode is not null) uri += $"&linkCode={target.PrivateServerLinkCode}";
            await LaunchAsync(uri);
        }, ControlAppearance.Secondary, SymbolRegular.ArrowEnterLeft24);

        UIElement registration = ProtocolRegistration.IsRegisteredTo(Paths.CurrentExe)
            ? Ui.Secondary("Pupstrap opens when you press Play on roblox.com.")
            : Ui.Button("Make default", () =>
            {
                ProtocolRegistration.Register(Paths.CurrentExe);
                Ui.Message("Pupstrap is now the Roblox launcher. Pressing Play on the website will open Pupstrap.");
            }, ControlAppearance.Primary);

        var heroSubtitle = new System.Windows.Controls.TextBlock
        {
            Text = "Opens Roblox with your Pupstrap mods, fast flags and live profile.",
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 255, 255, 255)),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 16, 0),
        };

        var installedText = Ui.Primary(installed.Length == 0 ? "Not installed" : installed, 13, FontWeights.SemiBold);
        _latest.FontSize = 13;
        _running.VerticalAlignment = VerticalAlignment.Center;
        var closeAll = Ui.Button("Close all", CloseAll, ControlAppearance.Danger);
        closeAll.MinWidth = 0;
        closeAll.Padding = new Thickness(10, 3, 10, 4);

        Content = Ui.Page("Home", "A cozy Roblox bootstrapper with mods, fast flags and multi-account launching.",
            Ui.Hero("Ready to play", heroSubtitle, launch),
            _progress,
            Ui.Tiles(
                Ui.Tile(SymbolRegular.Box24, "Installed", installedText),
                Ui.Tile(SymbolRegular.CloudArrowDown24, "Latest", _latest),
                Ui.Tile(SymbolRegular.Window24, "Roblox windows", Ui.Row(_running, closeAll))),
            Ui.Section("Join"),
            Ui.Card("Quick join", "Joins a game with the account signed in to the Roblox app. Use Accounts to pick a specific account.",
                Ui.Row(placeBox, join), SymbolRegular.Games24),
            Ui.Card("Website launcher", "Whether the Play button on roblox.com goes through Pupstrap.", registration, SymbolRegular.Globe24));

        Loaded += async (_, _) =>
        {
            RefreshRunning();
            try
            {
                var latest = await RobloxVersions.GetLatestAsync(settings.Channel);
                _latest.Text = latest.VersionGuid == installed ? "Up to date" : $"{latest.VersionGuid} ({latest.Version})";
            }
            catch (InvalidOperationException ex)
            {
                _latest.Text = ex.Message;
            }
        };
    }

    private async Task LaunchAsync(string? argument)
    {
        _progress.Report("Preparing", 0);
        try
        {
            await App.LaunchFromMenuAsync(argument, _progress.Progress, CancellationToken.None);
            _progress.Done("Roblox started.");
        }
        catch
        {
            _progress.Done("Launch failed.");
            throw;
        }
        await Task.Delay(1500);
        RefreshRunning();
    }

    private void RefreshRunning()
    {
        var count = Bootstrapper.RunningRoblox().Length;
        _running.Text = count == 0 ? "None open  " : $"{count} open  ";
    }

    private void CloseAll()
    {
        foreach (var process in Bootstrapper.RunningRoblox())
        {
            try { process.Kill(); }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }
        RefreshRunning();
    }
}
