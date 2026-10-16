using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Pupstrap.Accounts;
using Pupstrap.Core;
using Pupstrap.Deployment;
using Pupstrap.Integrations;
using Pupstrap.Launching;
using Pupstrap.UI;

namespace Pupstrap;

public partial class App : Application
{
    public static string Version { get; } =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    private static int _activeSessions;
    private static bool _menuOpen;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandled;
        UI.Ui.InstallWheelForwarding();

        Store.Load();
        LiveFlags.FlagProfile.InstallBuiltIns(false);
        var updateOnly = e.Args.Any(a => a.Equals("-update", StringComparison.OrdinalIgnoreCase));
        var launchArgument = ParseLaunchArgument(e.Args);
        Logger.Initialize(updateOnly ? "Update" : launchArgument is null ? "Menu" : "Player");
        ThemeService.Apply(Store.Settings);

        if (updateOnly)
        {
            _ = RunUpdateAsync();
            return;
        }

        if (launchArgument is not null)
        {
            _ = RunPlayerAsync(launchArgument);
            return;
        }

        AccountStore.Load();
        if (Store.Settings.MultiInstance) MultiInstance.TryAcquire();
        var pageIndex = Array.FindIndex(e.Args, a => a.Equals("-page", StringComparison.OrdinalIgnoreCase));
        OpenMenu(pageIndex >= 0 && pageIndex + 1 < e.Args.Length ? e.Args[pageIndex + 1] : null);
    }

    private static string? ParseLaunchArgument(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("-player", StringComparison.OrdinalIgnoreCase))
                return i + 1 < args.Length ? args[i + 1] : "";
            if (args[i].StartsWith("roblox", StringComparison.OrdinalIgnoreCase) && args[i].Contains(':'))
                return args[i];
        }
        return null;
    }

    private void OpenMenu(string? page)
    {
        _menuOpen = true;
        var window = new MainWindow(page);
        MainWindow = window;
        window.Closed += async (_, _) =>
        {
            _menuOpen = false;
            if (_activeSessions > 0) return;
            await WhileGuardingAsync();
            if (_activeSessions == 0 && !_menuOpen) Shutdown();
        };
        window.Show();
    }

    /// <summary>Keeps Pupstrap running in the background while live flags are being kept applied.</summary>
    private static async Task WhileGuardingAsync()
    {
        if (LiveFlags.LiveSession.IsGuarding) Logger.Info("Menu closed, keeping live flags applied until Roblox exits");
        while (LiveFlags.LiveSession.IsGuarding) await Task.Delay(2000);
    }

    /// <summary>Full bootstrap flow used when Pupstrap is started by a roblox-player link.</summary>
    private async Task RunPlayerAsync(string launchArgument)
    {
        var settings = Store.Settings;
        var dialog = LaunchDialog.Create(settings);
        dialog?.Show();
        var progress = new Progress<InstallProgress>(p => dialog?.Report(p.Status, p.Fraction));
        try
        {
            if (settings.MultiInstance) MultiInstance.TryAcquire();
            var versionDir = await Bootstrapper.PrepareAsync(progress, dialog?.Cancellation.Token ?? CancellationToken.None);
            dialog?.Report("Starting Roblox", 0);
            var launch = Bootstrapper.Start(versionDir, launchArgument);
            if (dialog is not null && settings.DialogWaitForRobloxWindow) await WaitForWindowAsync(launch.Process, dialog);
            else await Task.Delay(1200);
            dialog?.Close();
            dialog = null;

            if (RobloxSession.AnyEnabled(settings) || MultiInstance.IsHeld)
            {
                using var session = new RobloxSession(launch);
                await session.RunAsync();
            }
        }
        catch (OperationCanceledException)
        {
            Logger.Info("Launch cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error("Launch failed", ex);
            dialog?.Hide();
            Ui.Error($"Roblox could not be launched.\n\n{ex.Message}\n\nLog: {Logger.CurrentFile}");
        }
        finally
        {
            dialog?.Close();
            MultiInstance.Release();
            Shutdown();
        }
    }

    private static async Task WaitForWindowAsync(System.Diagnostics.Process process, ILaunchDialog dialog)
    {
        dialog.Report("Waiting for Roblox", 0);
        for (var i = 0; i < 120 && !dialog.Cancellation.IsCancellationRequested; i++)
        {
            process.Refresh();
            if (process.HasExited || process.MainWindowHandle != IntPtr.Zero) break;
            await Task.Delay(250);
        }
    }

    /// <summary>Installs or updates Roblox and exits without launching it.</summary>
    private async Task RunUpdateAsync()
    {
        var settings = Store.Settings;
        var dialog = LaunchDialog.Create(settings);
        dialog?.Show();
        var progress = new Progress<InstallProgress>(p => dialog?.Report(p.Status, p.Fraction));
        try
        {
            await Bootstrapper.PrepareAsync(progress, dialog?.Cancellation.Token ?? CancellationToken.None);
            Logger.Info($"Update finished, {Store.State.InstalledVersion} is ready");
        }
        catch (OperationCanceledException)
        {
            Logger.Info("Update cancelled");
        }
        catch (Exception ex)
        {
            Logger.Error("Update failed", ex);
            dialog?.Hide();
            Ui.Error($"Roblox could not be updated.\n\n{ex.Message}\n\nLog: {Logger.CurrentFile}");
        }
        finally
        {
            dialog?.Close();
            Shutdown();
        }
    }

    /// <summary>Launches from inside the menu (Launch and Accounts pages) and keeps integrations alive in the background.</summary>
    public static async Task<LaunchResult> LaunchFromMenuAsync(string? launchArgument, IProgress<InstallProgress> progress, CancellationToken token)
    {
        if (Store.Settings.MultiInstance && !MultiInstance.TryAcquire() && Bootstrapper.RunningRoblox().Length > 0)
            Logger.Warn("Multi-instance is on but Roblox started before Pupstrap; the next window may replace the old one");
        var versionDir = await Bootstrapper.PrepareAsync(progress, token);
        var launch = Bootstrapper.Start(versionDir, launchArgument);
        TrackSession(launch);
        return launch;
    }

    private static void TrackSession(LaunchResult launch)
    {
        _activeSessions++;
        var session = new RobloxSession(launch);
        _ = Current.Dispatcher.InvokeAsync(async () =>
        {
            try { await session.RunAsync(); }
            finally
            {
                session.Dispose();
                _activeSessions--;
                if (_activeSessions == 0 && !_menuOpen) await WhileGuardingAsync();
                if (_activeSessions == 0 && !_menuOpen)
                {
                    MultiInstance.Release();
                    Current.Shutdown();
                }
            }
        });
    }

    private static void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Error("Unhandled exception", e.Exception);
        Ui.Error($"Something went wrong: {e.Exception.Message}\n\nLog: {Logger.CurrentFile}");
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        MultiInstance.Release();
        base.OnExit(e);
    }
}
