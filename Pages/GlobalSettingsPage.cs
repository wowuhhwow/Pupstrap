using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Pupstrap.Core;
using Pupstrap.Launching;
using Pupstrap.UI;
using Wpf.Ui.Controls;

namespace Pupstrap.Pages;

/// <summary>Roblox's own in-game settings, edited in GlobalBasicSettings_13.xml.</summary>
public sealed class GlobalSettingsPage : PupPage
{
    private static readonly string[] QualityLabels = ["Automatic", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10"];

    private readonly Dictionary<string, string> _values = RobloxGlobalSettings.Read();
    private readonly DispatcherTimer _save = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly InfoBar _status = Ui.Info("", "");

    public GlobalSettingsPage()
    {
        const string subtitle = "Roblox's own settings, the same ones as the in-game Settings menu.";
        if (!RobloxGlobalSettings.Exists)
        {
            Content = Ui.Page("Global Settings", subtitle,
                Ui.Info("No Roblox settings yet", "Roblox creates its settings file the first time it runs. Launch Roblox once, then come back.", InfoBarSeverity.Warning));
            return;
        }

        _save.Tick += (_, _) => Save();
        Unloaded += (_, _) => { if (_save.IsEnabled) Save(); };
        ShowStatus(null);

        Content = Ui.Page("Global Settings", subtitle,
            _status,
            Ui.Section("Performance"),
            Ui.Card("Framerate limit",
                "Roblox's own frame rate cap. This is the one that actually limits FPS. Going above 240 is not recommended. Turn off VSync in your GPU settings to go above your refresh rate.",
                IntBox("FramerateCap", 1, 9999), SymbolRegular.TopSpeed24),
            Ui.Card("Graphics quality", "The in-game graphics slider. Automatic lets Roblox pick.",
                Choice("SavedQualityLevel", QualityLabels, [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10]), SymbolRegular.Options24),
            Ui.Card("Graphics mode", "Performance favours frame rate, Quality favours visuals.",
                Choice("GraphicsOptimizationMode", ["Performance", "Balanced", "Quality"], [0, 1, 2]), SymbolRegular.Options24),
            Ui.Card("Fullscreen", "Start Roblox in fullscreen.", Toggle("Fullscreen"), SymbolRegular.FullScreenMaximize24),
            Ui.Card("Performance stats", "Show the frame rate, ping and memory overlay.", Toggle("PerformanceStatsVisible"), SymbolRegular.DataUsage24),
            Ui.Card("Microprofiler", "Show the on-screen Ctrl+F6 profiler.", Toggle("OnScreenProfilerEnabled"), SymbolRegular.Pulse24),

            Ui.Section("Camera and controls"),
            Ui.Card("Mouse sensitivity", "Camera sensitivity, in Roblox's units. The in-game slider goes from 0.1 to 4.",
                NumberBox("MouseSensitivity", 0.0001, 10), SymbolRegular.CursorHover24),
            Ui.Card("Gamepad camera sensitivity", "Camera sensitivity when using a controller.",
                NumberBox("GamepadCameraSensitivity", 0.01, 10), SymbolRegular.Games24),
            Ui.Card("Invert camera", "Flip the vertical camera direction.", Toggle("CameraYInverted"), SymbolRegular.ArrowSwap24),
            Ui.Card("Camera mode", "How the camera follows you on a computer. Games can override this.",
                Choice("ComputerCameraMovementMode", ["Default", "Classic", "Follow", "Orbital", "Camera toggle"], [0, 1, 2, 3, 4]), SymbolRegular.Video24),
            Ui.Card("Movement mode", "How you move your character on a computer. Games can override this.",
                Choice("ComputerMovementMode", ["Default", "Keyboard and mouse", "Click to move"], [0, 1, 2]), SymbolRegular.ArrowMove24),
            Ui.Card("Shift lock", "Classic camera vs Shift to mouse-lock.",
                Choice("ControlMode", ["Classic", "Mouse lock switch"], [0, 1]), SymbolRegular.LockClosed24),
            Ui.Card("UI navigation key", "Let the \\ key toggle UI navigation.", Toggle("UiNavigationKeyBindEnabled"), SymbolRegular.Keyboard24),

            Ui.Section("Audio"),
            Ui.Card("Master volume", "Overall game volume.", Percent("MasterVolume"), SymbolRegular.Speaker224),
            Ui.Card("Voice chat volume", "Volume of other players' voice chat.", Percent("VoiceChatVolume"), SymbolRegular.Mic24),
            Ui.Card("Party voice volume", "Volume of Roblox party voice.", Percent("PartyVoiceVolume"), SymbolRegular.Chat24),

            Ui.Section("Interface"),
            Ui.Card("Background transparency", "How see-through Roblox's menus are. 100% is the default.", Percent("PreferredTransparency"), SymbolRegular.Eye24),
            Ui.Card("Text size", "UI text size in Roblox menus.",
                Choice("PreferredTextSize", ["Default", "Larger", "Largest"], [1, 2, 3]), SymbolRegular.TextFont24),
            Ui.Card("Reduced motion", "Cut down on UI animations and camera effects.", Toggle("ReducedMotion"), SymbolRegular.Accessibility24),
            Ui.Card("Chat", "Show the chat window.", Toggle("ChatVisible"), SymbolRegular.Chat24),
            Ui.Card("Chat translation", "Translate chat messages into your language.", Toggle("ChatTranslationEnabled"), SymbolRegular.Translate24),
            Ui.Card("Player list", "Show the leaderboard / player list.", Toggle("PlayerListVisible"), SymbolRegular.Person24),
            Ui.Card("Player names", "Show names above other players.", Toggle("PlayerNamesEnabled"), SymbolRegular.Person24),
            Ui.Card("Badge notifications", "Show badge earned popups.", Toggle("BadgeVisible"), SymbolRegular.Ribbon24),
            Ui.Card("Read aloud", "Screen reader style read-aloud for UI.", Toggle("ReadAloud"), SymbolRegular.Speaker224),
            Ui.Card("VR", "Allow Roblox to use a VR headset.", Toggle("VREnabled"), SymbolRegular.Video24),
            Ui.Spacer(4),
            Ui.Row(Ui.Button("Open Roblox settings folder", () => Ui.OpenFolder(Path.GetDirectoryName(RobloxGlobalSettings.FilePath)!),
                ControlAppearance.Secondary, SymbolRegular.FolderOpen24)));
    }

    private string? Value(string name) => _values.TryGetValue(name, out var value) ? value : null;

    private void Change(string name, string value)
    {
        if (Value(name) == value) return;
        _values[name] = value;
        RobloxGlobalSettings.Set(name, value);
        if (name == "MouseSensitivity")
        {
            RobloxGlobalSettings.Set("MouseSensitivityFirstPerson", value);
            RobloxGlobalSettings.Set("MouseSensitivityThirdPerson", value);
        }
        Store.SaveSettings();
        _save.Stop();
        _save.Start();
    }

    private void Save()
    {
        _save.Stop();
        try { ShowStatus(RobloxGlobalSettings.ApplyPending()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Xml.XmlException)
        {
            Logger.Error("Couldn't write Roblox settings", ex);
            _status.Title = "Couldn't save";
            _status.Message = ex.Message;
            _status.Severity = InfoBarSeverity.Error;
        }
    }

    private void ShowStatus(bool? final)
    {
        if (Bootstrapper.RunningRoblox().Length > 0)
        {
            _status.Title = "Roblox is running";
            _status.Message = "Roblox overwrites this file when it closes, so Pupstrap writes your changes again the next time you launch Roblox through it.";
            _status.Severity = InfoBarSeverity.Warning;
        }
        else if (final == true)
        {
            _status.Title = "Saved";
            _status.Message = "Roblox will use these the next time it starts.";
            _status.Severity = InfoBarSeverity.Success;
        }
        else
        {
            _status.Title = "Roblox settings";
            _status.Message = "Changes save straight to Roblox's settings file. Close Roblox first, or they're applied on your next launch through Pupstrap.";
            _status.Severity = InfoBarSeverity.Informational;
        }
    }

    private UIElement Toggle(string name) =>
        Ui.Toggle(Value(name) == "true", on => Change(name, on ? "true" : "false"));

    private UIElement Choice(string name, string[] labels, int[] values)
    {
        var index = int.TryParse(Value(name), out var current) ? Array.IndexOf(values, current) : -1;
        return Ui.Combo(labels, index, i => Change(name, values[i].ToString()));
    }

    private UIElement IntBox(string name, int min, int max) => Box(name, 120, text =>
        int.TryParse(text, out var n) && n >= min && n <= max ? n.ToString() : null);

    private UIElement NumberBox(string name, double min, double max) => Box(name, 120, text =>
        RobloxGlobalSettings.TryNumber(text, out var n) && n >= min && n <= max ? RobloxGlobalSettings.Number(n) : null);

    private UIElement Box(string name, double width, Func<string, string?> parse)
    {
        var box = new Wpf.Ui.Controls.TextBox { Text = Value(name) is { } v && RobloxGlobalSettings.TryNumber(v, out var n) ? RobloxGlobalSettings.Number(n) : "", MinWidth = width };
        void Commit()
        {
            if (parse(box.Text.Trim()) is { } value) Change(name, value);
            else box.Text = Value(name) is { } old && RobloxGlobalSettings.TryNumber(old, out var n) ? RobloxGlobalSettings.Number(n) : "";
        }
        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) => { if (e.Key == Key.Enter) Commit(); };
        return box;
    }

    private UIElement Percent(string name)
    {
        var start = RobloxGlobalSettings.TryNumber(Value(name), out var v) ? Math.Round(Math.Clamp(v, 0, 1) * 100) : 100;
        var label = Ui.Secondary($"{start}%");
        label.Width = 44;
        label.VerticalAlignment = VerticalAlignment.Center;
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = start, Width = 200, IsSnapToTickEnabled = true, TickFrequency = 1, VerticalAlignment = VerticalAlignment.Center };
        slider.ValueChanged += (_, e) =>
        {
            label.Text = $"{e.NewValue:0}%";
            Change(name, RobloxGlobalSettings.Number(e.NewValue / 100));
        };
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(slider);
        row.Children.Add(label);
        return row;
    }
}
