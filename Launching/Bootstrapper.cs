using System.Diagnostics;
using System.Xml;
using Pupstrap.Core;
using Pupstrap.Deployment;
using Pupstrap.FastFlags;
using Pupstrap.Mods;

namespace Pupstrap.Launching;

public sealed record LaunchResult(Process Process, string Version, DateTime StartedAt);

public static class Bootstrapper
{
    private static readonly SemaphoreSlim PrepareGate = new(1, 1);

    public static Process[] RunningRoblox() => Process.GetProcessesByName("RobloxPlayerBeta");

    /// <summary>True when a running Roblox was started from this folder, or when its path can't be read.</summary>
    public static bool IsVersionInUse(string versionDir)
    {
        var root = Path.GetFullPath(versionDir).TrimEnd('\\') + "\\";
        foreach (var process in RunningRoblox())
        {
            try
            {
                var path = process.MainModule?.FileName;
                if (path is null || path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Makes sure the right version is installed, applies mods and fast flags. Returns the version folder.</summary>
    public static async Task<string> PrepareAsync(IProgress<InstallProgress> progress, CancellationToken token, bool forceReinstall = false)
    {
        await PrepareGate.WaitAsync(token);
        try
        {
            var settings = Store.Settings;
            var state = Store.State;
            var version = await ResolveVersionAsync(settings, state, progress, token);

            if (forceReinstall || !Installer.IsInstalled(version))
            {
                if (Installer.IsInstalled(version) && IsVersionInUse(Paths.VersionDir(version)))
                    throw new InvalidOperationException("Close Roblox before reinstalling the version it is running from.");
                await Installer.InstallAsync(version, RobloxVersions.NormalizeChannel(settings.Channel), progress, token);
            }

            state.InstalledVersion = version;
            Store.SaveState();

            var versionDir = Paths.VersionDir(version);
            progress.Report(new InstallProgress("Applying mods", 1));
            if (!IsVersionInUse(versionDir)) ModManager.Apply(versionDir, settings);
            else Logger.Info("Roblox is running from this version, leaving mod files alone this launch");

            ClientAppSettings.Write(versionDir, settings.ActiveFastFlags());

            try { RobloxGlobalSettings.ApplyPending(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or XmlException)
            {
                Logger.Warn($"Couldn't write Roblox settings: {ex.Message}");
            }

            if (settings.CleanupOldVersions)
                Installer.CleanupOldVersions(version, settings.PinnedVersion);

            return versionDir;
        }
        finally
        {
            PrepareGate.Release();
        }
    }

    private static async Task<string> ResolveVersionAsync(Settings settings, State state, IProgress<InstallProgress> progress, CancellationToken token)
    {
        var pinned = settings.PinnedVersion.Trim();
        if (pinned.Length > 0)
        {
            if (!RobloxVersions.IsValidVersionGuid(pinned))
                throw new InvalidOperationException($"Pinned version \"{pinned}\" is not a version-xxxxxxxxxxxxxxxx hash.");
            Logger.Info($"Using pinned version {pinned}");
            return pinned;
        }

        var installed = state.InstalledVersion;
        if (!settings.CheckUpdatesOnLaunch && installed.Length > 0 && Installer.IsInstalled(installed))
            return installed;

        progress.Report(new InstallProgress("Checking for updates", 0));
        try
        {
            var latest = await RobloxVersions.GetLatestAsync(settings.Channel, token);
            Logger.Info($"Latest {latest.Channel} version is {latest.VersionGuid} ({latest.Version})");
            return latest.VersionGuid;
        }
        catch (InvalidOperationException ex) when (installed.Length > 0 && Installer.IsInstalled(installed))
        {
            Logger.Warn($"Update check failed, launching installed {installed}: {ex.Message}");
            return installed;
        }
    }

    public static LaunchResult Start(string versionDir, string? launchArgument)
    {
        var settings = Store.Settings;

        if (settings.MultiInstance) MultiInstance.TryAcquire();
        else if (settings.CloseRobloxBeforeLaunch)
        {
            foreach (var process in RunningRoblox())
            {
                try { process.Kill(); process.WaitForExit(3000); }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception) { }
            }
        }

        var info = new ProcessStartInfo(Path.Combine(versionDir, "RobloxPlayerBeta.exe"))
        {
            WorkingDirectory = versionDir,
            UseShellExecute = false,
        };
        if (string.IsNullOrWhiteSpace(launchArgument)) info.ArgumentList.Add("--app");
        else info.ArgumentList.Add(launchArgument);

        var startedAt = DateTime.Now;
        var started = Process.Start(info) ?? throw new InvalidOperationException("Roblox did not start.");
        Logger.Info($"Started Roblox pid {started.Id} from {versionDir}");
        return new LaunchResult(started, Path.GetFileName(versionDir), startedAt);
    }
}
