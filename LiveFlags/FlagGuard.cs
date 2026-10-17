using System.Collections.Concurrent;
using Pupstrap.Core;

namespace Pupstrap.LiveFlags;

/// <summary>Rewrites flags Roblox changes back, which it does for some flags whenever you join a new game.</summary>
public sealed class FlagGuard : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

    private readonly RobloxMemory _memory;
    private readonly ConcurrentDictionary<string, (uint Rva, int Value)> _targets = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _stop = new();

    /// <summary>Raised on a background thread with the names of the flags that were put back.</summary>
    public event Action<IReadOnlyList<string>>? Restored;

    /// <summary>Raised on a background thread when Roblox has exited.</summary>
    public event Action? Died;

    private int _burst;

    public FlagGuard(RobloxMemory memory)
    {
        _memory = memory;
        _ = Task.Run(RunAsync);
    }

    public void Set(string name, uint rva, int value) => _targets[name] = (rva, value);
    public void Remove(string name) => _targets.TryRemove(name, out _);

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                if (!_memory.IsAlive)
                {
                    if (!_stop.IsCancellationRequested) Died?.Invoke();
                    return;
                }
                List<string>? restored = null;
                var force = _burst > 0;
                if (force) _burst--;
                foreach (var (name, target) in _targets)
                {
                    if (!force && _memory.ReadInt32(target.Rva) == target.Value) continue;
                    if (_memory.ReadInt32(target.Rva) != target.Value) (restored ??= new()).Add(name);
                    _memory.WriteInt32(target.Rva, target.Value);
                }
                if (restored is not null) _burst = 40;
                if (restored is null) continue;
                Logger.Info($"Roblox reset {restored.Count} flag(s), rewrote them: {string.Join(", ", restored)}");
                Restored?.Invoke(restored);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            if (!_stop.IsCancellationRequested) Logger.Warn($"Keep-applied stopped: {ex.Message}");
        }
    }

    public void Dispose() => _stop.Cancel();
}
