using System.Windows;
using Pupstrap.Accounts;
using Pupstrap.Core;
using Pupstrap.Launching;
using Pupstrap.UI;

namespace Pupstrap.Integrations;

/// <summary>Runs the integrations for one launched Roblox process and completes when it exits.</summary>
public sealed class RobloxSession : IDisposable
{
    private readonly LaunchResult _launch;
    private readonly ActivityWatcher? _watcher;
    private readonly DiscordRpc? _discord;

    public static bool AnyEnabled(Settings s) =>
        (s.ActivityTracking && (s.ServerLocationNotification || DiscordEnabled(s))) || LiveFlags.LiveAutoApply.Enabled(s);

    private static bool DiscordEnabled(Settings s) => s.DiscordRichPresence && s.DiscordClientId.Trim().Length > 0;

    public RobloxSession(LaunchResult launch)
    {
        _launch = launch;
        var settings = Store.Settings;
        if (!settings.ActivityTracking) return;
        _watcher = new ActivityWatcher(launch.StartedAt);
        _watcher.Joined += join => _ = OnJoinedAsync(join);
        _watcher.Left += () => _ = _discord?.SetActivityAsync(null);
        if (DiscordEnabled(settings)) _discord = new DiscordRpc(settings.DiscordClientId.Trim());
    }

    public async Task RunAsync()
    {
        _watcher?.Start();
        if (LiveFlags.LiveAutoApply.Enabled(Store.Settings)) _ = LiveFlags.LiveAutoApply.RunAsync(_launch);
        try { await _launch.Process.WaitForExitAsync(); }
        catch (InvalidOperationException) { }
        if (_discord is not null) await _discord.SetActivityAsync(null);
        Logger.Info($"Roblox pid {_launch.Process.Id} exited");
    }

    private async Task OnJoinedAsync(GameJoin join)
    {
        var settings = Store.Settings;
        if (settings.ServerLocationNotification && join.ServerIp.Length > 0)
        {
            var location = await RobloxApi.GetServerLocationAsync(join.ServerIp);
            if (location is not null)
                Application.Current.Dispatcher.Invoke(() => NotificationWindow.Show("Connected to server", $"Located in {location}"));
        }

        if (_discord is null) return;
        var game = await RobloxApi.GetGameDetailsAsync(join.PlaceId);
        var joinUrl = $"https://www.roblox.com/games/start?placeId={join.PlaceId}&gameInstanceId={join.JobId}";
        await _discord.SetActivityAsync(new
        {
            details = Truncate($"Playing {game?.Name ?? "Roblox"}"),
            state = game is null ? null : Truncate($"by {game.Creator}"),
            timestamps = new { start = new DateTimeOffset(join.JoinedAt).ToUnixTimeSeconds() },
            assets = new
            {
                large_image = string.IsNullOrEmpty(game?.IconUrl) ? null : game.IconUrl,
                large_text = game?.Name,
            },
            buttons = settings.DiscordShowJoinButton
                ? new[] { new { label = "Join server", url = joinUrl } }
                : null,
        });
    }

    private static string Truncate(string value) => value.Length <= 128 ? value : value[..125] + "...";

    public void Dispose()
    {
        _watcher?.Dispose();
        _discord?.Dispose();
    }
}
