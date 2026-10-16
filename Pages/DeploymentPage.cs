using System.Windows;
using System.Windows.Controls;
using Pupstrap.Core;
using Pupstrap.Deployment;
using Pupstrap.Launching;
using Pupstrap.UI;
using Wpf.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Pupstrap.Pages;

public sealed class DeploymentPage : PupPage
{
    private readonly TextBlock _installed = Ui.Secondary("");
    private readonly TextBlock _latest = Ui.Secondary("Not checked yet");
    private readonly StackPanel _versions = new();
    private readonly ProgressPanel _progress = new();

    public DeploymentPage()
    {
        var s = Store.Settings;
        void Save() => Store.SaveSettings();

        Content = Ui.Page("Deployment", "Which Roblox version Pupstrap installs and how it keeps it up to date.",
            Ui.Card("Installed", "", _installed, SymbolRegular.Box24),
            Ui.Card("Latest on channel", "", Ui.Row(_latest, Ui.AsyncButton("Check", CheckAsync, ControlAppearance.Secondary, SymbolRegular.ArrowSync24)),
                SymbolRegular.CloudArrowDown24),
            Ui.Card("Update now", "Installs the target version without launching Roblox.",
                Ui.Row(
                    Ui.AsyncButton("Update", () => InstallAsync(false), ControlAppearance.Primary, SymbolRegular.ArrowDownload24),
                    Ui.AsyncButton("Reinstall", () => InstallAsync(true), ControlAppearance.Secondary, SymbolRegular.ArrowClockwise24)),
                SymbolRegular.Rocket24),
            _progress,

            Ui.Section("Version"),
            Ui.Card("Channel", "LIVE is the normal release. Other channels are Roblox's test builds and may need you signed in.",
                Ui.Text(s.Channel, v =>
                {
                    s.Channel = RobloxVersions.NormalizeChannel(v);
                    Save();
                }, "LIVE", 160), SymbolRegular.BranchFork24),
            Ui.Card("Pin a version",
                "Keeps Roblox on one version-hash instead of updating, so an offset dump stays valid. Roblox eventually refuses old versions. Leave empty to always use the latest.",
                Ui.Text(s.PinnedVersion, v =>
                {
                    v = v.Trim();
                    if (v.Length > 0 && !RobloxVersions.IsValidVersionGuid(v))
                    {
                        Ui.Error("A version looks like version-11f155f3450f4c8e.");
                        return;
                    }
                    s.PinnedVersion = v;
                    Save();
                }, "version-xxxxxxxxxxxxxxxx", 230), SymbolRegular.Pin24),
            Ui.Card("Check for updates when launching", "Turn off to always launch the installed version straight away.",
                Ui.Toggle(s.CheckUpdatesOnLaunch, v => { s.CheckUpdatesOnLaunch = v; Save(); }), SymbolRegular.ArrowSyncCircle24),
            Ui.Card("Remove old versions", "Deletes previous Roblox versions after an update to save disk space.",
                Ui.Toggle(s.CleanupOldVersions, v => { s.CleanupOldVersions = v; Save(); }), SymbolRegular.Broom24),

            Ui.Section("Storage"),
            Ui.Card("Installed versions", "", _versions, SymbolRegular.Folder24),
            Ui.Card("Folders", Paths.Base, Ui.Row(
                Ui.Button("Versions", () => Ui.OpenFolder(Paths.Versions)),
                Ui.Button("Remove old", () =>
                {
                    var removed = Installer.CleanupOldVersions(Store.State.InstalledVersion, Store.Settings.PinnedVersion);
                    Ui.Message($"Removed {removed} old version{(removed == 1 ? "" : "s")}.");
                    Refresh();
                }),
                Ui.Button("Clear download cache", () =>
                {
                    Installer.ClearDownloadCache();
                    Ui.Message("Download cache cleared.");
                })), SymbolRegular.Storage24));

        Refresh();
    }

    private void Refresh()
    {
        var installed = Store.State.InstalledVersion;
        _installed.Text = installed.Length == 0 ? "Nothing yet" : installed + (Store.Settings.PinnedVersion.Length > 0 ? "  (pinned)" : "");
        _versions.Children.Clear();
        var versions = RobloxVersions.InstalledVersions().ToList();
        if (versions.Count == 0) _versions.Children.Add(Ui.Secondary("None"));
        foreach (var version in versions)
            _versions.Children.Add(Ui.Secondary(version == installed ? $"{version}  (current)" : version));
    }

    private async Task CheckAsync()
    {
        _latest.Text = "Checking...";
        var latest = await RobloxVersions.GetLatestAsync(Store.Settings.Channel);
        _latest.Text = latest.VersionGuid == Store.State.InstalledVersion
            ? $"{latest.VersionGuid} (up to date) "
            : $"{latest.VersionGuid} ({latest.Version}) ";
    }

    private async Task InstallAsync(bool force)
    {
        try
        {
            await Bootstrapper.PrepareAsync(_progress.Progress, CancellationToken.None, force);
            _progress.Done($"Ready: {Store.State.InstalledVersion}");
        }
        catch
        {
            _progress.Done("Update failed.");
            throw;
        }
        finally
        {
            Refresh();
        }
    }
}
