using System.Text.RegularExpressions;
using Pupstrap.Core;

namespace Pupstrap.Integrations;

public sealed record GameJoin(long PlaceId, string JobId, string ServerIp, DateTime JoinedAt);

public sealed partial class ActivityWatcher : IDisposable
{
    private readonly DateTime _launchedAt;
    private readonly CancellationTokenSource _cts = new();
    private GameJoin? _pending;

    public event Action<GameJoin>? Joined;
    public event Action? Left;

    public GameJoin? Current { get; private set; }

    public ActivityWatcher(DateTime launchedAt) => _launchedAt = launchedAt;

    [GeneratedRegex(@"! Joining game '([0-9a-fA-F\-]{36})' place ([0-9]+) at ([0-9\.]+)")]
    private static partial Regex JoiningRegex();

    [GeneratedRegex(@"UDMUX Address = ([0-9\.]+), Port = [0-9]+ \| RCC Server Address = ([0-9\.]+)")]
    private static partial Regex UdmuxRegex();

    [GeneratedRegex(@"Connection accepted from ([0-9\.]+)\|[0-9]+")]
    private static partial Regex AcceptedRegex();

    public void Start() => _ = Task.Run(() => RunAsync(_cts.Token));

    private async Task RunAsync(CancellationToken token)
    {
        try
        {
            var log = await FindLogAsync(token);
            if (log is null)
            {
                Logger.Warn("Activity tracking could not find a Roblox log file");
                return;
            }
            Logger.Info($"Watching {Path.GetFileName(log)}");
            await using var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            while (!token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(token);
                if (line is null)
                {
                    await Task.Delay(500, token);
                    continue;
                }
                Handle(line);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.Error("Activity watcher stopped", ex);
        }
    }

    private async Task<string?> FindLogAsync(CancellationToken token)
    {
        for (var i = 0; i < 60 && !token.IsCancellationRequested; i++)
        {
            if (Directory.Exists(Paths.RobloxLogs))
            {
                var newest = new DirectoryInfo(Paths.RobloxLogs).GetFiles("*Player*.log")
                    .Where(f => f.CreationTime >= _launchedAt.AddSeconds(-5))
                    .OrderByDescending(f => f.CreationTime)
                    .FirstOrDefault();
                if (newest is not null) return newest.FullName;
            }
            await Task.Delay(1000, token);
        }
        return null;
    }

    private void Handle(string line)
    {
        var joining = JoiningRegex().Match(line);
        if (joining.Success)
        {
            _pending = new GameJoin(long.Parse(joining.Groups[2].Value), joining.Groups[1].Value, joining.Groups[3].Value, DateTime.Now);
            return;
        }

        if (_pending is not null)
        {
            var udmux = UdmuxRegex().Match(line);
            if (udmux.Success)
            {
                _pending = _pending with { ServerIp = udmux.Groups[1].Value };
                return;
            }
            var accepted = AcceptedRegex().Match(line);
            if (accepted.Success)
            {
                Current = _pending;
                _pending = null;
                Logger.Info($"Joined place {Current.PlaceId} on {Current.ServerIp}");
                Joined?.Invoke(Current);
                return;
            }
        }

        if (Current is not null && (line.Contains("Time to disconnect replication data") || line.Contains("Disconnection Notification")))
        {
            Logger.Info($"Left place {Current.PlaceId}");
            Current = null;
            Left?.Invoke();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
