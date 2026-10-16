using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Pupstrap.Core;
using Pupstrap.LiveFlags;
using Pupstrap.UI;
using Wpf.Ui.Controls;
using ListBox = System.Windows.Controls.ListBox;
using TextBlock = System.Windows.Controls.TextBlock;
using WpfTextBox = Wpf.Ui.Controls.TextBox;

namespace Pupstrap.Pages;

public sealed class LiveFlagsPage : PupPage
{
    private const int MaxResults = 2000;

    private readonly TextBlock _dumpInfo = Ui.Secondary("");
    private readonly TextBlock _attachInfo = Ui.Secondary("");
    private readonly TextBlock _restoredInfo = new() { Margin = new Thickness(4, 0, 0, 8), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    private readonly WpfTextBox _search = new() { PlaceholderText = "Search flags (Ctrl+F)", Icon = new SymbolIcon(SymbolRegular.Search24) };
    private readonly ListBox _results = new() { Height = 330, DisplayMemberPath = nameof(FlagEntry.Name) };
    private readonly TextBlock _resultCount = Ui.Secondary("");
    private readonly TextBlock _selectedName = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _selectedInfo = Ui.Secondary("Pick a flag on the left.");
    private readonly WpfTextBox _value = new() { PlaceholderText = "Int32 value", MinWidth = 140 };
    private readonly StackPanel _changes = new();
    private readonly DispatcherTimer _searchDelay = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private ComboBox _autoProfile = new();

    public LiveFlagsPage()
    {
        var s = Store.Settings;
        LiveSession.TryLoadConfiguredDump();

        var dumpBox = Ui.Text(s.LiveDumpPath, v => { s.LiveDumpPath = v.Trim().Trim('"'); Store.SaveSettings(); }, @"...\FFlags.hpp", 0);
        dumpBox.MaxWidth = double.PositiveInfinity;
        var dumpCard = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 6),
            Children =
            {
                Ui.Card("Offset dump", "FFlags.hpp or FFlags.json from your dumper. It must match the Roblox version you run.",
                    Ui.Row(
                        Ui.Button("Browse", () =>
                        {
                            var path = Ui.PickFile("FFlag dump|*.hpp;*.h;*.json|All files|*.*");
                            if (path is null) return;
                            dumpBox.Text = path;
                            s.LiveDumpPath = path;
                            Store.SaveSettings();
                            LoadDump();
                        }),
                        Ui.Button("Load", LoadDump, ControlAppearance.Primary)),
                    SymbolRegular.DocumentData24),
                dumpBox,
                _dumpInfo,
            },
        };
        _dumpInfo.Margin = new Thickness(4, 4, 0, 0);

        var keepCard = Ui.Card("Keep applied",
            "Roblox resets some flags when you join a new game. This checks five times a second and puts your values back, so you don't have to reapply. Pupstrap stays running in the background until Roblox closes.",
            Ui.Toggle(s.LiveKeepApplied, v =>
            {
                s.LiveKeepApplied = v;
                Store.SaveSettings();
                LiveSession.UpdateGuard();
            }), SymbolRegular.ShieldCheckmark24);

        var attachCard = Ui.Card("Roblox", "Writes go straight into the running client. Graphics flags are mostly read when a place loads, so set them on the home screen and then join.",
            Ui.Row(_attachInfo,
                Ui.Button("Attach", Attach, ControlAppearance.Primary, SymbolRegular.PlugConnected24),
                Ui.Button("Detach", () => LiveSession.Detach())),
            SymbolRegular.PlugConnected24);

        _autoProfile = BuildAutoProfileCombo();
        var profilesCard = Ui.Card("Profiles", "Loading a profile first reverts anything an earlier profile set, so they never stack.",
            Ui.Row(
                Ui.Button("Load profile", LoadProfile, ControlAppearance.Primary, SymbolRegular.FolderOpen24),
                Ui.Button("Save profile", SaveProfile, ControlAppearance.Secondary, SymbolRegular.Save24),
                Ui.Button("Open folder", () => Ui.OpenFolder(FlagProfile.Folder))),
            SymbolRegular.DocumentCopy24);
        var autoCard = Ui.Card("Apply on launch",
            "Every time Pupstrap starts Roblox it waits for the client and writes this profile automatically, but only when the dump matches that exact version.",
            Ui.Row(_autoProfile,
                Ui.Text(s.LiveApplyDelaySeconds.ToString(CultureInfo.InvariantCulture), v =>
                {
                    if (int.TryParse(v, out var n)) { s.LiveApplyDelaySeconds = Math.Clamp(n, 0, 30); Store.SaveSettings(); }
                }, "Delay s", 60),
                new TextBlock { Text = "s delay", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) }),
            SymbolRegular.Flash24);

        Content = Ui.Page("Live Flags", "Edit any fast flag in a running Roblox, including the ones ClientAppSettings.json ignores.",
            Ui.Info("Use with care", "This writes raw 4-byte values into Roblox's memory at the dump's offsets. Use a dump for the exact version you run. Values are lost when Roblox closes.",
                InfoBarSeverity.Warning),
            dumpCard, attachCard, keepCard, _restoredInfo, profilesCard, autoCard,
            Ui.Section("Flags"),
            BuildBrowser(),
            Ui.Section("Changed this session"),
            Ui.Row(Ui.Button("Revert all", () => Try(LiveSession.RevertAll), ControlAppearance.Danger, SymbolRegular.ArrowUndo24)),
            Ui.Spacer(6),
            _changes);

        _searchDelay.Tick += (_, _) => { _searchDelay.Stop(); RunSearch(); };
        _search.TextChanged += (_, _) => { _searchDelay.Stop(); _searchDelay.Start(); };
        _results.SelectionChanged += (_, _) => ShowSelected();
        _value.KeyDown += (_, e) => { if (e.Key == Key.Enter) ApplyValue(); };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.Control) { _search.Focus(); e.Handled = true; }
        };

        LiveSession.Changed += Refresh;
        LiveSession.Restored += OnRestored;
        Unloaded += (_, _) =>
        {
            LiveSession.Changed -= Refresh;
            LiveSession.Restored -= OnRestored;
        };
        Refresh();
        RunSearch();
    }

    private UIElement BuildBrowser()
    {
        var left = new StackPanel();
        left.Children.Add(_search);
        left.Children.Add(Ui.Spacer(6));
        left.Children.Add(_results);
        left.Children.Add(_resultCount);

        var right = new StackPanel { Margin = new Thickness(18, 0, 0, 0) };
        right.Children.Add(_selectedName);
        right.Children.Add(_selectedInfo);
        right.Children.Add(Ui.Spacer(10));
        right.Children.Add(Ui.Row(_value, Ui.Button("Apply", ApplyValue, ControlAppearance.Primary)));
        right.Children.Add(Ui.Spacer(8));
        right.Children.Add(Ui.Row(
            Ui.Button("Off (0)", () => SetAndApply(0)),
            Ui.Button("On (1)", () => SetAndApply(1)),
            Ui.Button("Revert", RevertSelected)));
        right.Children.Add(Ui.Spacer(10));
        right.Children.Add(Ui.Secondary("True/false flags are stored as 0 or 1. Float flags show raw bits here, so small integers do nothing useful for them."));

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);

        var card = new Border { Padding = new Thickness(16), CornerRadius = new CornerRadius(6), Child = grid };
        card.SetResourceReference(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        return card;
    }

    private ComboBox BuildAutoProfileCombo()
    {
        var files = FlagProfile.Available().ToList();
        var names = new List<string> { "Off" };
        names.AddRange(files.Select(f => Path.GetFileNameWithoutExtension(f)));
        var current = files.FindIndex(f => f.Equals(Store.Settings.LiveProfileOnLaunch, StringComparison.OrdinalIgnoreCase));
        return Ui.Combo(names, current + 1, i =>
        {
            Store.Settings.LiveProfileOnLaunch = i == 0 ? "" : files[i - 1];
            Store.SaveSettings();
        }, 170);
    }

    private FlagEntry? Selected => _results.SelectedItem as FlagEntry;

    private void OnRestored(IReadOnlyList<string> names)
    {
        var list = string.Join(", ", names.Take(6)) + (names.Count > 6 ? $" and {names.Count - 6} more" : "");
        _restoredInfo.Text = $"{DateTime.Now:HH:mm:ss}  Roblox reset {names.Count} flag(s), put back: {list}";
        _restoredInfo.Visibility = Visibility.Visible;
    }

    private void LoadDump() => Try(() =>
    {
        LiveSession.LoadDump(Store.Settings.LiveDumpPath);
        RunSearch();
    });

    private void Attach() => Try(() => LiveSession.Attach());

    private void RunSearch()
    {
        var dump = LiveSession.Dump;
        if (dump is null)
        {
            _results.ItemsSource = null;
            _resultCount.Text = "Load a dump to browse flags.";
            return;
        }
        var query = _search.Text.Trim();
        var matches = query.Length == 0
            ? dump.Flags
            : dump.Flags.Where(f => f.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        _results.ItemsSource = matches.Take(MaxResults).ToList();
        _resultCount.Text = matches.Count > MaxResults
            ? $"Showing {MaxResults} of {matches.Count} matches. Type more to narrow it down."
            : $"{matches.Count} match{(matches.Count == 1 ? "" : "es")}";
    }

    private void ShowSelected()
    {
        if (Selected is not { } flag)
        {
            _selectedName.Text = "";
            _selectedInfo.Text = "Pick a flag on the left.";
            return;
        }
        _selectedName.Text = flag.Name;
        var change = LiveSession.Changes.FirstOrDefault(c => c.Name == flag.Name);
        var info = $"Offset 0x{flag.Rva:x}";
        if (LiveSession.IsAttached)
        {
            try
            {
                var live = LiveSession.Read(flag);
                info += $"   Live {live}";
                if (Math.Abs(live) > 1_000_000) info += " (huge, probably not an integer flag)";
                _value.Text = live.ToString(CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                info += $"   {ex.Message}";
            }
        }
        else
        {
            info += "   Attach to read the live value.";
        }
        if (change is not null) info += $"   Original {change.Original}";
        _selectedInfo.Text = info;
    }

    private void SetAndApply(int value)
    {
        _value.Text = value.ToString(CultureInfo.InvariantCulture);
        ApplyValue();
    }

    private void ApplyValue()
    {
        if (Selected is not { } flag) return;
        if (!int.TryParse(_value.Text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            Ui.Error("Enter a whole number between -2147483648 and 2147483647.");
            return;
        }
        Try(() => LiveSession.Write(flag, value));
        ShowSelected();
    }

    private void RevertSelected()
    {
        if (Selected is not { } flag) return;
        var change = LiveSession.Changes.FirstOrDefault(c => c.Name == flag.Name);
        if (change is not null) Try(() => LiveSession.Revert(change));
        ShowSelected();
    }

    private void LoadProfile()
    {
        Directory.CreateDirectory(FlagProfile.Folder);
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Flag profile|*.flags|All files|*.*", InitialDirectory = FlagProfile.Folder };
        if (dialog.ShowDialog() != true) return;
        Try(() =>
        {
            var profile = FlagProfile.Load(dialog.FileName);
            var result = LiveSession.ApplyProfile(profile);
            var message = $"Applied {result.Applied} flags from {profile.Name}";
            if (result.Reverted > 0) message += $", reverted {result.Reverted} from the previous profile";
            if (result.Skipped > 0) message += $", {result.Skipped} not in this dump";
            if (result.Failed > 0) message += $", {result.Failed} failed (see log)";
            if (profile.RobloxVersion.Length > 0 && LiveSession.Dump is { } dump && profile.RobloxVersion != dump.RobloxVersion)
                message += $". The profile was made for {profile.RobloxVersion}";
            Ui.Message(message + ".\n\nRejoin the game for graphics flags to take effect.");
        });
    }

    private void SaveProfile()
    {
        if (LiveSession.Changes.Count == 0)
        {
            Ui.Error("Change a flag before saving a profile.");
            return;
        }
        Directory.CreateDirectory(FlagProfile.Folder);
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "Flag profile|*.flags", DefaultExt = "flags", InitialDirectory = FlagProfile.Folder };
        if (dialog.ShowDialog() != true) return;
        Try(() => LiveSession.SnapshotProfile(Path.GetFileNameWithoutExtension(dialog.FileName)).Save(dialog.FileName));
    }

    private void Refresh()
    {
        var dump = LiveSession.Dump;
        _dumpInfo.Text = dump is null
            ? "No dump loaded."
            : $"{dump.RobloxVersion}  ·  {dump.Flags.Count:N0} flags  ·  dumped {dump.DumpedAt}";

        var memory = LiveSession.Memory;
        _attachInfo.Text = memory is { IsAlive: true }
            ? $"Attached to pid {memory.ProcessId} ({(memory.Version.Length > 0 ? memory.Version : "unknown version")})  "
            : "Not attached  ";

        _changes.Children.Clear();
        if (LiveSession.Changes.Count == 0) _changes.Children.Add(Ui.Secondary("Nothing changed yet."));
        foreach (var change in LiveSession.Changes)
        {
            var c = change;
            _changes.Children.Add(Ui.Card(c.Name, $"{c.Original} -> {c.Current}",
                Ui.Button("Revert", () => Try(() => LiveSession.Revert(c)))));
        }
    }

    private static void Try(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Logger.Warn(ex.Message);
            Ui.Error(ex.Message);
        }
    }
}
