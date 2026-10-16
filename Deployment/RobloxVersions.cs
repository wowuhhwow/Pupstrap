using Pupstrap.Core;

namespace Pupstrap.Deployment;

public sealed record ClientVersion(string Version, string VersionGuid, string Channel);

public static class RobloxVersions
{
    private static readonly string[] ClientSettingsHosts =
    {
        "https://clientsettingscdn.roblox.com",
        "https://clientsettings.roblox.com",
    };

    public static string NormalizeChannel(string? channel) =>
        string.IsNullOrWhiteSpace(channel) ? "LIVE" : channel.Trim();

    public static bool IsLive(string channel) => channel.Equals("LIVE", StringComparison.OrdinalIgnoreCase);

    public static async Task<ClientVersion> GetLatestAsync(string channel, CancellationToken token = default)
    {
        channel = NormalizeChannel(channel);
        Exception? last = null;
        foreach (var host in ClientSettingsHosts)
        {
            try
            {
                using var doc = await Http.GetJsonAsync($"{host}/v2/client-version/WindowsPlayer/channel/{Uri.EscapeDataString(channel)}", token);
                var root = doc.RootElement;
                return new ClientVersion(
                    root.GetProperty("version").GetString() ?? "",
                    root.GetProperty("clientVersionUpload").GetString() ?? "",
                    channel);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException or KeyNotFoundException)
            {
                last = ex;
                Logger.Warn($"Version check against {host} failed: {ex.Message}");
            }
        }
        throw new InvalidOperationException($"Could not get the latest Roblox version for channel {channel}.", last);
    }

    public static bool IsValidVersionGuid(string value) =>
        value.StartsWith("version-", StringComparison.OrdinalIgnoreCase) && value.Length > 12 &&
        value[8..].All(Uri.IsHexDigit);

    public static IEnumerable<string> InstalledVersions()
    {
        if (!Directory.Exists(Paths.Versions)) yield break;
        foreach (var dir in Directory.GetDirectories(Paths.Versions))
        {
            var name = Path.GetFileName(dir);
            if (IsValidVersionGuid(name) && File.Exists(Path.Combine(dir, "RobloxPlayerBeta.exe")))
                yield return name;
        }
    }
}
