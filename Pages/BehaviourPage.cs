using System.Windows.Controls;
using Pupstrap.Core;
using Pupstrap.Launching;
using Pupstrap.UI;
using Wpf.Ui.Controls;

namespace Pupstrap.Pages;

public sealed class BehaviourPage : PupPage
{
    public BehaviourPage()
    {
        var s = Store.Settings;
        void Save() => Store.SaveSettings();

        var registered = ProtocolRegistration.PointsAtPupstrap();
        var installedHere = string.Equals(Path.GetFullPath(Paths.CurrentExe), Path.GetFullPath(Installation.InstalledExe), StringComparison.OrdinalIgnoreCase);

        Content = Ui.Page("Behaviour", "What Pupstrap does when Roblox starts.",
            Ui.Card("Multi-instance", "Lets several Roblox windows run at once (needed for multiple accounts). Close every Roblox window before turning it on.",
                Ui.Toggle(s.MultiInstance, v =>
                {
                    s.MultiInstance = v;
                    Save();
                    if (!v) MultiInstance.Release();
                    else if (!MultiInstance.TryAcquire())
                        Ui.Error("A Roblox window is already open, so multi-instance starts working once you close it and launch again.");
                }), SymbolRegular.WindowMultiple16),
            Ui.Card("Close Roblox before launching", "When multi-instance is off, closes the old Roblox window instead of letting Roblox reuse it.",
                Ui.Toggle(s.CloseRobloxBeforeLaunch, v => { s.CloseRobloxBeforeLaunch = v; Save(); }), SymbolRegular.DismissSquare24),

            Ui.Section("Installation"),
            Ui.Card("Install Pupstrap", installedHere
                    ? "Pupstrap is running from its install folder."
                    : Installation.InstalledIsOutdated()
                        ? $"The copy in {Installation.InstallDir} is older than this one. Update replaces it."
                        : $"Copies Pupstrap to {Installation.InstallDir}, makes it the Roblox launcher and adds Start menu shortcuts. Reinstalling replaces the old copy.",
                Ui.Button(installedHere ? "Reinstall" : Installation.InstalledIsOutdated() ? "Update" : File.Exists(Installation.InstalledExe) ? "Reinstall" : "Install", () =>
                {
                    try
                    {
                        Installation.Install();
                        Ui.Message("Pupstrap is installed. Open \"Pupstrap\" from the Start menu or press Play on the website.");
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                    {
                        Ui.Error($"Install failed: {ex.Message}");
                    }
                }, ControlAppearance.Primary, SymbolRegular.ArrowDownload24), SymbolRegular.AppFolder24),
            Ui.Card("Website launcher", registered
                    ? "Play buttons on roblox.com open Pupstrap. Restore gives them back to Roblox's own launcher."
                    : "Play buttons on roblox.com open Roblox's own launcher.",
                Ui.Row(
                    Ui.Button("Use Pupstrap", () =>
                    {
                        ProtocolRegistration.Register(File.Exists(Installation.InstalledExe) ? Installation.InstalledExe : Paths.CurrentExe);
                        Ui.Message("Play buttons on roblox.com now open Pupstrap.");
                    }, ControlAppearance.Primary),
                    Ui.Button("Restore Roblox", () =>
                    {
                        ProtocolRegistration.Restore();
                        Ui.Message("Play buttons on roblox.com open Roblox's own launcher again.");
                    })), SymbolRegular.Globe24),
            Ui.Card("Desktop shortcut", "Adds a \"Pupstrap\" shortcut to your desktop that starts Roblox.",
                Ui.Button("Create", () =>
                {
                    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    Installation.RemoveLegacyShortcut(desktop);
                    Installation.CreateShortcut(desktop, "Pupstrap", "-player \"\"");
                    Ui.Message("Shortcut created on your desktop.");
                }), SymbolRegular.Desktop24));
    }
}

public static class Installation
{
    public static string InstallDir => Path.Combine(Paths.Base, "App");
    public static string InstalledExe => Path.Combine(InstallDir, "Pupstrap.exe");

    public static void Install()
    {
        var sourceDir = Path.GetDirectoryName(Paths.CurrentExe)!;
        if (!string.Equals(Path.GetFullPath(sourceDir), Path.GetFullPath(InstallDir), StringComparison.OrdinalIgnoreCase))
        {
            var singleFile = !File.Exists(Path.Combine(sourceDir, "Pupstrap.dll"));
            if (!singleFile && !File.Exists(Path.Combine(sourceDir, "Pupstrap.runtimeconfig.json")))
                throw new InvalidOperationException("This copy of Pupstrap is incomplete (Pupstrap.runtimeconfig.json is missing), so the installed copy wouldn't start. Install from publish\\Pupstrap.exe instead.");

            ClearInstallDir();
            Directory.CreateDirectory(InstallDir);
            if (singleFile)
            {
                File.Copy(Paths.CurrentExe, InstalledExe, true);
            }
            else
            {
                foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    var target = Path.Combine(InstallDir, Path.GetRelativePath(sourceDir, file));
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(file, target, true);
                }
            }
        }

        WriteIcon();
        ProtocolRegistration.Register(InstalledExe);
        var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Pupstrap");
        RemoveLegacyShortcut(startMenu);
        RemoveLegacyShortcut(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        CreateShortcut(startMenu, "Pupstrap", "-player \"\"", InstalledExe);
        CreateShortcut(startMenu, "Pupstrap Settings", "", InstalledExe);
        RefreshShellIcons();
        Logger.Info($"Installed Pupstrap to {InstallDir}");
    }

    /// <summary>Separate .ico next to the exe. Explorer caches exe icons by path, so a replaced exe keeps showing the old one.</summary>
    public static string InstalledIcon => Path.Combine(InstallDir, "pupstrap.ico");

    private static void WriteIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/Assets/pupstrap.ico"));
        if (resource is null) return;
        using var source = resource.Stream;
        using var file = File.Create(InstalledIcon);
        source.CopyTo(file);
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

    /// <summary>Tells Explorer and the Start menu that icons changed.</summary>
    private static void RefreshShellIcons() => SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);

    /// <summary>True when an installed copy exists and is older than the exe that's running now.</summary>
    public static bool InstalledIsOutdated() =>
        File.Exists(InstalledExe) && File.GetLastWriteTimeUtc(InstalledExe) < File.GetLastWriteTimeUtc(Paths.CurrentExe);

    private static void ClearInstallDir()
    {
        if (!Directory.Exists(InstallDir)) return;
        foreach (var file in Directory.EnumerateFiles(InstallDir, "*", SearchOption.AllDirectories))
        {
            try { File.Delete(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new IOException($"Couldn't remove the old {Path.GetFileName(file)}. Close any Pupstrap or Roblox started from the installed copy and try again.", ex);
            }
        }
        foreach (var dir in Directory.EnumerateDirectories(InstallDir).ToList()) Directory.Delete(dir, true);
    }

    /// <summary>Deletes the "Roblox (Pupstrap)" shortcut that older versions created.</summary>
    public static void RemoveLegacyShortcut(string folder)
    {
        var legacy = Path.Combine(folder, "Roblox (Pupstrap).lnk");
        try { if (File.Exists(legacy)) File.Delete(legacy); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Logger.Warn($"Couldn't remove {legacy}: {ex.Message}"); }
    }

    public static void CreateShortcut(string folder, string name, string arguments, string? target = null)
    {
        target ??= File.Exists(InstalledExe) ? InstalledExe : Paths.CurrentExe;
        Directory.CreateDirectory(folder);
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ?? throw new InvalidOperationException("Windows Script Host is unavailable.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        var link = shell.CreateShortcut(Path.Combine(folder, name + ".lnk"));
        link.TargetPath = target;
        link.Arguments = arguments;
        link.WorkingDirectory = Path.GetDirectoryName(target);
        link.IconLocation = File.Exists(InstalledIcon) ? InstalledIcon + ",0" : target + ",0";
        link.Save();
    }
}
