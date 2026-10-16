using Pupstrap.Core;
using Pupstrap.Launching;
using Pupstrap.UI;

namespace Pupstrap.LiveFlags;

/// <summary>Writes the chosen live profile into a Roblox that Pupstrap just launched, before any game loads.</summary>
public static class LiveAutoApply
{
    public static bool Enabled(Settings s) => s.LiveProfileOnLaunch.Length > 0 && File.Exists(s.LiveProfileOnLaunch) && File.Exists(s.LiveDumpPath);

    public static async Task RunAsync(LaunchResult launch)
    {
        var settings = Store.Settings;
        var dump = LiveSession.TryLoadConfiguredDump();
        if (dump is null) return;
        if (dump.RobloxVersion.Length > 0 && !dump.RobloxVersion.Equals(launch.Version, StringComparison.OrdinalIgnoreCase))
        {
            Logger.Warn($"Live profile skipped: dump is for {dump.RobloxVersion}, launched {launch.Version}");
            NotificationWindow.Show("Live profile skipped", $"Your dump is for {dump.RobloxVersion} but Roblox updated to {launch.Version}. Dump it again or pin the old version.", 10);
            return;
        }

        FlagProfile profile;
        try { profile = FlagProfile.Load(settings.LiveProfileOnLaunch); }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            Logger.Warn($"Live profile unreadable: {ex.Message}");
            return;
        }

        var pid = launch.Process.Id;
        var found = false;
        for (var i = 0; i < 120 && !launch.Process.HasExited; i++)
        {
            if (RobloxMemory.TryFindModule(pid, out _, out _, out _)) { found = true; break; }
            await Task.Delay(250);
        }
        if (!found) return;
        await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(settings.LiveApplyDelaySeconds, 0, 30)));
        if (launch.Process.HasExited) return;

        try
        {
            LiveSession.Attach(pid);
            var result = LiveSession.ApplyProfile(profile);
            Logger.Info($"Auto-applied {profile.Name} to pid {pid}");
            NotificationWindow.Show("Live profile applied", $"{profile.Name}: {result.Applied} flags written.");
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            Logger.Error("Live auto-apply failed", ex);
            NotificationWindow.Show("Live profile failed", ex.Message, 10);
        }
    }
}
