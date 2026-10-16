using Pupstrap.Core;

namespace Pupstrap.Launching;

public static class MultiInstance
{
    private const string SingletonMutexName = "ROBLOX_singletonMutex";
    private static Mutex? _mutex;

    public static bool IsHeld => _mutex is not null;

    /// <summary>Must be called from a thread that stays alive (the UI thread), since a mutex belongs to its owning thread.</summary>
    public static bool TryAcquire()
    {
        if (_mutex is not null) return true;
        var mutex = new Mutex(true, SingletonMutexName, out var createdNew);
        if (!createdNew)
        {
            mutex.Dispose();
            Logger.Warn("Roblox already owns the singleton mutex; multi-instance needs every Roblox window closed first");
            return false;
        }
        _mutex = mutex;
        Logger.Info("Holding the Roblox singleton mutex for multi-instance");
        return true;
    }

    public static void Release()
    {
        if (_mutex is null) return;
        try { _mutex.ReleaseMutex(); } catch (ApplicationException) { }
        _mutex.Dispose();
        _mutex = null;
    }
}
