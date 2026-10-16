using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Pupstrap.Core;

namespace Pupstrap.Accounts;

public sealed record RobloxUser(long Id, string Name, string DisplayName, string AvatarUrl);

public sealed record JoinTarget(long PlaceId, string? JobId, string? PrivateServerLinkCode);

public sealed record GameDetails(long UniverseId, string Name, string Creator, string IconUrl);

public static partial class RobloxApi
{
    private static HttpRequestMessage WithCookie(HttpMethod method, string url, string cookie)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("Cookie", $".ROBLOSECURITY={cookie}");
        return request;
    }

    public static async Task<RobloxUser> GetUserAsync(string cookie, CancellationToken token = default)
    {
        using var request = WithCookie(HttpMethod.Get, "https://users.roblox.com/v1/users/authenticated", cookie);
        using var response = await Http.Client.SendAsync(request, token);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("Roblox rejected that cookie. It may be expired or copied incompletely.");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        var root = doc.RootElement;
        var id = root.GetProperty("id").GetInt64();
        return new RobloxUser(id, root.GetProperty("name").GetString() ?? "", root.GetProperty("displayName").GetString() ?? "",
            await GetHeadshotAsync(id, token));
    }

    public static async Task<string> GetHeadshotAsync(long userId, CancellationToken token = default)
    {
        try
        {
            using var doc = await Http.GetJsonAsync(
                $"https://thumbnails.roblox.com/v1/users/avatar-headshot?userIds={userId}&size=150x150&format=Png&isCircular=false", token);
            return doc.RootElement.GetProperty("data")[0].GetProperty("imageUrl").GetString() ?? "";
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            Logger.Warn($"No headshot for {userId}: {ex.Message}");
            return "";
        }
    }

    public static async Task<string> GetAuthTicketAsync(string cookie, CancellationToken token = default)
    {
        string? csrf = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var request = WithCookie(HttpMethod.Post, "https://auth.roblox.com/v1/authentication-ticket", cookie);
            request.Headers.Referrer = new Uri("https://www.roblox.com/");
            request.Headers.Add("Origin", "https://www.roblox.com");
            if (csrf is not null) request.Headers.Add("x-csrf-token", csrf);
            request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

            using var response = await Http.Client.SendAsync(request, token);
            if (response.Headers.TryGetValues("rbx-authentication-ticket", out var tickets))
                return tickets.First();
            if (response.StatusCode == HttpStatusCode.Forbidden && response.Headers.TryGetValues("x-csrf-token", out var tokens))
            {
                csrf = tokens.First();
                continue;
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("This account's cookie has expired. Remove it and add it again.");
            throw new InvalidOperationException($"Roblox did not issue a launch ticket ({(int)response.StatusCode}).");
        }
        throw new InvalidOperationException("Roblox did not issue a launch ticket.");
    }

    public static string BuildLaunchUri(string ticket, JoinTarget target)
    {
        var browserTrackerId = Random.Shared.NextInt64(100_000_000_000, 999_999_999_999);
        var launchTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        string placeLauncher;
        if (!string.IsNullOrWhiteSpace(target.PrivateServerLinkCode))
            placeLauncher = $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestPrivateGame&placeId={target.PlaceId}&linkCode={target.PrivateServerLinkCode}";
        else if (!string.IsNullOrWhiteSpace(target.JobId))
            placeLauncher = $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGameJob&browserTrackerId={browserTrackerId}&placeId={target.PlaceId}&gameId={target.JobId}&isPlayTogetherGame=false";
        else
            placeLauncher = $"https://assetgame.roblox.com/game/PlaceLauncher.ashx?request=RequestGame&browserTrackerId={browserTrackerId}&placeId={target.PlaceId}&isPlayTogetherGame=false";
        placeLauncher += $"&joinAttemptId={Guid.NewGuid()}&joinAttemptOrigin=PlayButton";

        return "roblox-player:1+launchmode:play" +
               $"+gameinfo:{ticket}" +
               $"+launchtime:{launchTime}" +
               $"+placelauncherurl:{Uri.EscapeDataString(placeLauncher)}" +
               $"+browsertrackerid:{browserTrackerId}" +
               "+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
    }

    public static string BuildAppUri(string ticket)
    {
        var launchTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return $"roblox-player:1+launchmode:app+gameinfo:{ticket}+launchtime:{launchTime}+robloxLocale:en_us+gameLocale:en_us+channel:+LaunchExp:InApp";
    }

    [GeneratedRegex(@"games/(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex GameUrlRegex();

    [GeneratedRegex(@"[?&]privateServerLinkCode=([^&#]+)", RegexOptions.IgnoreCase)]
    private static partial Regex LinkCodeRegex();

    [GeneratedRegex(@"[?&](?:gameInstanceId|jobId)=([0-9a-fA-F\-]{36})", RegexOptions.IgnoreCase)]
    private static partial Regex JobIdRegex();

    public static JoinTarget? ParseJoinTarget(string input, string? jobId)
    {
        input = input.Trim();
        long placeId;
        string? linkCode = null;
        if (long.TryParse(input, out placeId))
        {
        }
        else
        {
            var game = GameUrlRegex().Match(input);
            if (!game.Success || !long.TryParse(game.Groups[1].Value, out placeId)) return null;
            var code = LinkCodeRegex().Match(input);
            if (code.Success) linkCode = Uri.UnescapeDataString(code.Groups[1].Value);
            var job = JobIdRegex().Match(input);
            if (job.Success && string.IsNullOrWhiteSpace(jobId)) jobId = job.Groups[1].Value;
        }
        return new JoinTarget(placeId, string.IsNullOrWhiteSpace(jobId) ? null : jobId.Trim(), linkCode);
    }

    public static async Task<GameDetails?> GetGameDetailsAsync(long placeId, CancellationToken token = default)
    {
        try
        {
            long universeId;
            using (var doc = await Http.GetJsonAsync($"https://apis.roblox.com/universes/v1/places/{placeId}/universe", token))
                universeId = doc.RootElement.GetProperty("universeId").GetInt64();

            string name, creator;
            using (var doc = await Http.GetJsonAsync($"https://games.roblox.com/v1/games?universeIds={universeId}", token))
            {
                var game = doc.RootElement.GetProperty("data")[0];
                name = game.GetProperty("name").GetString() ?? "Roblox";
                creator = game.GetProperty("creator").GetProperty("name").GetString() ?? "";
            }

            var icon = "";
            using (var doc = await Http.GetJsonAsync(
                       $"https://thumbnails.roblox.com/v1/games/icons?universeIds={universeId}&returnPolicy=PlaceHolder&size=512x512&format=Png&isCircular=false", token))
                icon = doc.RootElement.GetProperty("data")[0].GetProperty("imageUrl").GetString() ?? "";

            return new GameDetails(universeId, name, creator, icon);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            Logger.Warn($"Could not look up place {placeId}: {ex.Message}");
            return null;
        }
    }

    public static async Task<string?> GetServerLocationAsync(string ip, CancellationToken token = default)
    {
        try
        {
            using var doc = await Http.GetJsonAsync($"https://ipinfo.io/{ip}/json", token);
            var root = doc.RootElement;
            string Part(string name) => root.TryGetProperty(name, out var v) ? v.GetString() ?? "" : "";
            var parts = new[] { Part("city"), Part("region"), Part("country") }.Where(p => p.Length > 0).Distinct();
            var text = string.Join(", ", parts);
            return text.Length == 0 ? null : text;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            Logger.Warn($"Server location lookup failed: {ex.Message}");
            return null;
        }
    }
}
