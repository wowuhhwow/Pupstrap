namespace Pupstrap.Core;

public static class Logger
{
    private static readonly object Gate = new();
    private static string? _file;

    public static string? CurrentFile => _file;

    public static void Initialize(string mode)
    {
        Directory.CreateDirectory(Paths.Logs);
        _file = Path.Combine(Paths.Logs, $"Pupstrap_{DateTime.Now:yyyyMMdd_HHmmss}_{mode}.log");
        foreach (var old in new DirectoryInfo(Paths.Logs).GetFiles("*.log").OrderByDescending(f => f.CreationTimeUtc).Skip(20))
        {
            try { old.Delete(); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        Info($"Pupstrap {App.Version} started ({mode})");
    }

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}";
        System.Diagnostics.Debug.WriteLine(line);
        if (_file is null) return;
        lock (Gate)
        {
            try { File.AppendAllText(_file, line + Environment.NewLine); }
            catch (IOException) { }
        }
    }
}
