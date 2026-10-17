using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Pupstrap.Core;
using Pupstrap.FastFlags;
using Pupstrap.LiveFlags;
using Pupstrap.UI;
using Wpf.Ui.Controls;
using DataGrid = System.Windows.Controls.DataGrid;
using TextBox = System.Windows.Controls.TextBox;

namespace Pupstrap.Pages;

public sealed class FlagRow
{
    public bool Enabled { get; set; } = true;
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class FastFlagsPage : PupPage
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    private readonly ObservableCollection<FlagRow> _rows = new();
    private readonly DataGrid _grid;
    private readonly System.Windows.Controls.TextBlock _count = Ui.Secondary("");
    private readonly TextBox _newName;
    private readonly TextBox _newValue;
    private readonly TextBox _json;
    private bool _syncing;

    public FastFlagsPage()
    {
        var s = Store.Settings;

        _grid = new DataGrid
        {
            ItemsSource = _rows,
            AutoGenerateColumns = false,
            CanUserAddRows = false,
            CanUserDeleteRows = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            MinHeight = 220,
            MaxHeight = 420,
            Margin = new Thickness(0, 4, 0, 0),
        };
        _grid.Columns.Add(new DataGridTemplateColumn { Header = "On", CellTemplate = CheckBoxTemplate(), Width = new DataGridLength(48) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Flag", Binding = new Binding(nameof(FlagRow.Name)), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new Binding(nameof(FlagRow.Value)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.RowEditEnding += (_, _) => Dispatcher.BeginInvoke(SyncFromGrid, DispatcherPriority.Background);
        _grid.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, new RoutedEventHandler((_, e) =>
        {
            if (e.OriginalSource is CheckBox) SyncFromGrid();
        }));
        _rows.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Remove) SyncFromGrid();
        };

        var master = Ui.Card("Use fast flags",
            "Turn this off to launch Roblox with none of your fast flags. Everything stays saved here, so turning it back on restores them all.",
            Ui.Toggle(s.FastFlagsEnabled, v => { s.FastFlagsEnabled = v; Commit(); }), SymbolRegular.Power24);

        var presets = new List<UIElement>();
        foreach (var choice in FlagPresets.Choices)
        {
            presets.Add(Ui.Card(choice.Title, choice.Description,
                Ui.Combo(choice.Options, choice.Read(s.FastFlags), i => { choice.Write(s.FastFlags, i); Commit(); }), SymbolRegular.Options24));
        }
        foreach (var toggle in FlagPresets.Toggles)
        {
            presets.Add(Ui.Card(toggle.Title, toggle.Description,
                Ui.Toggle(toggle.IsOn(s.FastFlags), on =>
                {
                    toggle.Set(s.FastFlags, on);
                    if (toggle.Title == "Gray sky") LiveSession.TrySet("DebugSkyGray", on ? 1 : 0);
                    Commit();
                }), SymbolRegular.ToggleRight24));
        }

        _newName = new Wpf.Ui.Controls.TextBox { PlaceholderText = "Flag name, e.g. FFlagDebugGraphicsPreferD3D11", MinWidth = 320, Margin = new Thickness(0, 0, 8, 0) };
        _newValue = new Wpf.Ui.Controls.TextBox { PlaceholderText = "Value", Width = 140, Margin = new Thickness(0, 0, 8, 0) };
        _newValue.KeyDown += (_, e) => { if (e.Key == Key.Enter) AddFlag(); };
        _newName.KeyDown += (_, e) => { if (e.Key == Key.Enter) _newValue.Focus(); };
        var addRow = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        var addButton = Ui.Button("Add", AddFlag, ControlAppearance.Primary, SymbolRegular.Add24);
        DockPanel.SetDock(addButton, Dock.Right);
        DockPanel.SetDock(_newValue, Dock.Right);
        addRow.Children.Add(addButton);
        addRow.Children.Add(_newValue);
        addRow.Children.Add(_newName);

        var editorButtons = Ui.Row(
            Ui.Button("All on", () => SetAll(true), ControlAppearance.Secondary, SymbolRegular.CheckboxChecked24),
            Ui.Button("All off", () => SetAll(false), ControlAppearance.Secondary, SymbolRegular.CheckboxUnchecked24),
            Ui.Button("Import JSON", ImportJson, ControlAppearance.Secondary, SymbolRegular.ArrowImport24),
            Ui.Button("Paste JSON", PasteJson, ControlAppearance.Secondary, SymbolRegular.ClipboardPaste24),
            Ui.Button("Copy JSON", CopyJson, ControlAppearance.Secondary, SymbolRegular.Copy24),
            Ui.Button("Clear all", () =>
            {
                if (!Ui.Confirm("Remove every fast flag?")) return;
                s.FastFlags.Clear();
                s.DisabledFastFlags.Clear();
                Commit();
            }, ControlAppearance.Danger, SymbolRegular.Delete24));

        _json = new TextBox
        {
            AcceptsReturn = true,
            AcceptsTab = true,
            TextWrapping = TextWrapping.NoWrap,
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            FontSize = 13,
            MinHeight = 200,
            MaxHeight = 420,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(0, 4, 0, 0),
        };
        var jsonButtons = Ui.Row(
            Ui.Button("Save JSON", SaveJson, ControlAppearance.Primary, SymbolRegular.Save24),
            Ui.Button("Undo edits", RefreshJson, ControlAppearance.Secondary, SymbolRegular.ArrowUndo24));

        var children = new List<UIElement>
        {
            Ui.Info("Allowlist", "Roblox only reads allowlisted flags from ClientAppSettings.json. Others are ignored silently. For anything else, use the Live Flags page."),
            master,
            Ui.Section("Presets"),
        };
        children.AddRange(presets);
        children.Add(Ui.Section("All flags"));
        children.Add(Ui.Row(_count));
        children.Add(Ui.Secondary("Add a flag: type its name and value, then press Enter or Add."));
        children.Add(addRow);
        children.Add(_grid);
        children.Add(Ui.Spacer(8));
        children.Add(editorButtons);
        children.Add(Ui.Section("Edit as JSON"));
        children.Add(Ui.Secondary("Type or paste flags here, then Save JSON. This replaces the whole list. Flags you turned off stay off."));
        children.Add(_json);
        children.Add(Ui.Spacer(8));
        children.Add(jsonButtons);

        Content = Ui.Page("Fast Flags", "Engine settings Roblox reads when it starts. Saved to ClientAppSettings.json every launch.", children.ToArray());
        RefreshGrid();
    }

    private static DataTemplate CheckBoxTemplate()
    {
        var box = new FrameworkElementFactory(typeof(CheckBox));
        box.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            new Binding(nameof(FlagRow.Enabled)) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
        box.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
        box.SetValue(MinWidthProperty, 0.0);
        return new DataTemplate { VisualTree = box };
    }

    private void Commit()
    {
        Store.SaveSettings();
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        var s = Store.Settings;
        _syncing = true;
        _rows.Clear();
        foreach (var (name, value) in s.FastFlags.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
            _rows.Add(new FlagRow { Name = name, Value = value, Enabled = s.IsFastFlagOn(name) });
        _syncing = false;
        UpdateCount();
        RefreshJson();
    }

    private void RefreshJson() => _json.Text = Store.Settings.FastFlags.Count == 0
        ? "{\n}"
        : JsonSerializer.Serialize(Store.Settings.FastFlags.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase).ToDictionary(f => f.Key, f => f.Value), Indented);

    private void UpdateCount()
    {
        var s = Store.Settings;
        var on = s.ActiveFastFlags().Count;
        var status = !s.FastFlagsEnabled ? "Fast flags are turned off, none will be used." : $"{on} on, {s.FastFlags.Count - on} off.";
        _count.Text = $"{s.FastFlags.Count} flag{(s.FastFlags.Count == 1 ? "" : "s")}. {status} Untick a flag to turn it off without deleting it. Edit cells directly, select a row and press Delete to remove it.";
    }

    private void SyncFromGrid()
    {
        if (_syncing) return;
        var flags = new Dictionary<string, string>();
        var disabled = new List<string>();
        foreach (var row in _rows)
        {
            var name = row.Name.Trim();
            if (name.Length == 0) continue;
            flags[name] = row.Value.Trim();
            if (!row.Enabled) disabled.Add(name);
        }
        Store.Settings.FastFlags = flags;
        Store.Settings.DisabledFastFlags = disabled;
        Store.SaveSettings();
        UpdateCount();
        RefreshJson();
    }

    private void AddFlag()
    {
        var name = _newName.Text.Trim();
        var value = _newValue.Text.Trim();
        if (name.Length == 0) { _newName.Focus(); return; }
        if (name.Any(char.IsWhiteSpace)) { Ui.Error("Flag names can't contain spaces."); return; }
        var s = Store.Settings;
        s.FastFlags[name] = value;
        s.DisabledFastFlags.RemoveAll(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
        Commit();
        _newName.Clear();
        _newValue.Clear();
        _newName.Focus();
    }

    private void SetAll(bool on)
    {
        var s = Store.Settings;
        s.DisabledFastFlags = on ? new() : s.FastFlags.Keys.ToList();
        Commit();
    }

    private void SaveJson()
    {
        Dictionary<string, string> parsed;
        try { parsed = ClientAppSettings.Parse(_json.Text); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            Ui.Error($"That isn't valid fast flag JSON: {ex.Message}");
            return;
        }
        var s = Store.Settings;
        s.FastFlags = parsed;
        s.DisabledFastFlags.RemoveAll(n => !parsed.Keys.Contains(n, StringComparer.OrdinalIgnoreCase));
        Commit();
        Ui.Message($"Saved {parsed.Count} flags.");
    }

    private void Merge(string json)
    {
        Dictionary<string, string> parsed;
        try { parsed = ClientAppSettings.Parse(json); }
        catch (JsonException ex)
        {
            Ui.Error($"That isn't valid fast flag JSON: {ex.Message}");
            return;
        }
        foreach (var (name, value) in parsed) Store.Settings.FastFlags[name] = value;
        Commit();
        Ui.Message($"Imported {parsed.Count} flags.");
    }

    private void ImportJson()
    {
        var path = Ui.PickFile("JSON|*.json|All files|*.*");
        if (path is not null) Merge(File.ReadAllText(path));
    }

    private void PasteJson()
    {
        if (Clipboard.ContainsText()) Merge(Clipboard.GetText());
        else Ui.Error("Copy some fast flag JSON first.");
    }

    private void CopyJson()
    {
        Clipboard.SetText(JsonSerializer.Serialize(Store.Settings.ActiveFastFlags(), Indented));
        Ui.Message("Copied the fast flags that are on to the clipboard.");
    }
}
