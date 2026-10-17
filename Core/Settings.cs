using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pupstrap.Core;

public enum DialogStyle { Fluent, Compact, Hidden, Pupstrap, Minimal, Classic }

public enum DialogPosition { Center, BottomRight, BottomCenter, TopCenter }

public sealed class Settings
{
    public string Channel { get; set; } = "LIVE";
    public string PinnedVersion { get; set; } = "";
    public bool CheckUpdatesOnLaunch { get; set; } = true;
    public bool CleanupOldVersions { get; set; } = true;

    public bool MultiInstance { get; set; }
    public bool CloseRobloxBeforeLaunch { get; set; }

    public string Theme { get; set; } = "Dark";
    public string Accent { get; set; } = "#FF9E5E";
    public DialogStyle DialogStyle { get; set; } = DialogStyle.Pupstrap;
    public string DialogTitle { get; set; } = "Pupstrap";
    public string DialogIconPath { get; set; } = "";
    public string DialogBackgroundPath { get; set; } = "";
    public DialogPosition DialogPosition { get; set; } = DialogPosition.Center;
    public bool DialogTopmost { get; set; }
    public bool DialogShowCancel { get; set; } = true;
    public bool DialogShowPercent { get; set; } = true;
    public bool DialogWaitForRobloxWindow { get; set; } = true;

    public bool ActivityTracking { get; set; } = true;
    public bool ServerLocationNotification { get; set; } = true;
    public bool DiscordRichPresence { get; set; }
    public string DiscordClientId { get; set; } = "";
    public bool DiscordShowJoinButton { get; set; } = true;

    public string CustomFontPath { get; set; } = "";
    public string DeathSoundPath { get; set; } = "";
    public string CursorPath { get; set; } = "";
    public bool UseModsFolder { get; set; } = true;

    public Dictionary<string, string> FastFlags { get; set; } = new();
    public bool FastFlagsEnabled { get; set; } = true;
    public List<string> DisabledFastFlags { get; set; } = new();

    /// <summary>The flags that actually get written to ClientAppSettings.json.</summary>
    public Dictionary<string, string> ActiveFastFlags()
    {
        if (!FastFlagsEnabled) return new();
        var off = new HashSet<string>(DisabledFastFlags, StringComparer.OrdinalIgnoreCase);
        return FastFlags.Where(f => !off.Contains(f.Key)).ToDictionary(f => f.Key, f => f.Value);
    }

    public bool IsFastFlagOn(string name) => !DisabledFastFlags.Contains(name, StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> PendingGlobalSettings { get; set; } = new();
    public string LiveDumpPath { get; set; } = "";
    public string LiveProfileOnLaunch { get; set; } = "";
    public int LiveApplyDelaySeconds { get; set; } = 2;
    public bool LiveKeepApplied { get; set; } = true;

    public string LastPlaceInput { get; set; } = "";
}

public sealed class State
{
    public string InstalledVersion { get; set; } = "";
    public string PreviousRobloxPlayerCommand { get; set; } = "";
    public string PreviousRobloxCommand { get; set; } = "";
    public bool ProtocolRegistered { get; set; }
}

public static class Store
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Settings Settings { get; private set; } = new();
    public static State State { get; private set; } = new();

    public static event Action? SettingsChanged;

    public static void Load()
    {
        Paths.EnsureCreated();
        Settings = Read<Settings>(Paths.SettingsFile) ?? new Settings();
        if (Settings.FastFlags.Remove("DFIntTaskSchedulerTargetFps") | Settings.FastFlags.Remove("FFlagTaskSchedulerLimitTargetFpsTo2402"))
            SaveSettings();
        State = Read<State>(Paths.StateFile) ?? new State();
    }

    public static void SaveSettings()
    {
        Write(Paths.SettingsFile, Settings);
        SettingsChanged?.Invoke();
    }

    public static void SaveState() => Write(Paths.StateFile, State);

    private static T? Read<T>(string path) where T : class
    {
        if (!File.Exists(path)) return null;
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options); }
        catch (JsonException ex)
        {
            Logger.Error($"Could not read {path}, using defaults", ex);
            return null;
        }
    }

    private static void Write<T>(string path, T value)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, true);
    }
}
