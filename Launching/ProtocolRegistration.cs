using Microsoft.Win32;
using Pupstrap.Core;

namespace Pupstrap.Launching;

public static class ProtocolRegistration
{
    private static readonly string[] Schemes = { "roblox-player", "roblox" };

    private static string CommandFor(string exe) => $"\"{exe}\" -player \"%1\"";

    public static string? ReadCommand(string scheme)
    {
        using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{scheme}\shell\open\command");
        return key?.GetValue("") as string;
    }

    public static bool IsRegisteredTo(string exe) =>
        Schemes.All(s => string.Equals(ReadCommand(s), CommandFor(exe), StringComparison.OrdinalIgnoreCase));

    public static bool PointsAtPupstrap() =>
        Schemes.Any(s => ReadCommand(s)?.Contains("Pupstrap", StringComparison.OrdinalIgnoreCase) == true);

    public static void Register(string exe)
    {
        var state = Store.State;
        if (!PointsAtPupstrap())
        {
            state.PreviousRobloxPlayerCommand = ReadCommand("roblox-player") ?? "";
            state.PreviousRobloxCommand = ReadCommand("roblox") ?? "";
        }

        foreach (var scheme in Schemes)
        {
            using var root = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{scheme}");
            root.SetValue("", $"URL: {scheme} Protocol");
            root.SetValue("URL Protocol", "");
            var iconFile = Path.Combine(Path.GetDirectoryName(exe)!, "pupstrap.ico");
            using (var icon = root.CreateSubKey("DefaultIcon")) icon.SetValue("", File.Exists(iconFile) ? $"\"{iconFile}\",0" : $"\"{exe}\",0");
            using var command = root.CreateSubKey(@"shell\open\command");
            command.SetValue("", CommandFor(exe));
        }

        state.ProtocolRegistered = true;
        Store.SaveState();
        Logger.Info($"Registered roblox protocols to {exe}");
    }

    public static void Restore()
    {
        var state = Store.State;
        Restore("roblox-player", state.PreviousRobloxPlayerCommand);
        Restore("roblox", state.PreviousRobloxCommand);
        state.ProtocolRegistered = false;
        Store.SaveState();
        Logger.Info("Restored the original roblox protocol handlers");
    }

    private static void Restore(string scheme, string previous)
    {
        if (string.IsNullOrEmpty(previous))
        {
            Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{scheme}", false);
            return;
        }
        using var command = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{scheme}\shell\open\command");
        command.SetValue("", previous);
    }
}
