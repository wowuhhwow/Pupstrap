namespace Pupstrap.Core;

public static class Paths
{
    public static string Base { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pupstrap");
    public static string Versions => Path.Combine(Base, "Versions");
    public static string Downloads => Path.Combine(Base, "Downloads");
    public static string Mods => Path.Combine(Base, "Mods");
    public static string GeneratedMods => Path.Combine(Base, "GeneratedMods");
    public static string Logs => Path.Combine(Base, "Logs");
    public static string SettingsFile => Path.Combine(Base, "Settings.json");
    public static string StateFile => Path.Combine(Base, "State.json");
    public static string AccountsFile => Path.Combine(Base, "Accounts.json");
    public static string RobloxLogs { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "logs");

    public static string CurrentExe => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Pupstrap.exe");

    public static string VersionDir(string version) => Path.Combine(Versions, version);

    public static void EnsureCreated()
    {
        foreach (var dir in new[] { Base, Versions, Downloads, Mods, GeneratedMods, Logs })
            Directory.CreateDirectory(dir);
    }
}
