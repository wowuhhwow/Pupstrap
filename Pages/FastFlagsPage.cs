using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Pupstrap.Core;
using Pupstrap.FastFlags;
using Pupstrap.UI;
using Wpf.Ui.Controls;
using DataGrid = System.Windows.Controls.DataGrid;

namespace Pupstrap.Pages;

public sealed class FlagRow
{
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed class FastFlagsPage : PupPage
{
    private readonly ObservableCollection<FlagRow> _rows = new();
    private readonly DataGrid _grid;
    private readonly System.Windows.Controls.TextBlock _count = Ui.Secondary("");
    private bool _syncing;

    public FastFlagsPage()
    {
        var s = Store.Settings;

        _grid = new DataGrid
        {
            ItemsSource = _rows,
            AutoGenerateColumns = false,
            CanUserAddRows = true,
            CanUserDeleteRows = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            MinHeight = 220,
            MaxHeight = 420,
            Margin = new Thickness(0, 4, 0, 0),
        };
        _grid.Columns.Add(new DataGridTextColumn { Header = "Flag", Binding = new System.Windows.Data.Binding(nameof(FlagRow.Name)), Width = new DataGridLength(3, DataGridLengthUnitType.Star) });
        _grid.Columns.Add(new DataGridTextColumn { Header = "Value", Binding = new System.Windows.Data.Binding(nameof(FlagRow.Value)), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        _grid.RowEditEnding += (_, _) => Dispatcher.BeginInvoke(SyncFromGrid, DispatcherPriority.Background);
        _rows.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Remove) SyncFromGrid();
        };

        var presets = new List<UIElement>();
        var fps = Ui.Text(FlagPresets.ReadFpsCap(s.FastFlags) is var cap && cap > 0 ? cap.ToString() : "", v =>
        {
            FlagPresets.WriteFpsCap(s.FastFlags, int.TryParse(v, out var n) ? n : 0);
            Commit();
        }, "Default (60)", 120);
        presets.Add(Ui.Card("FPS limit", "Frame rate cap. Leave empty for Roblox's default. Turn off VSync in-game to go above your refresh rate.",
            fps, SymbolRegular.TopSpeed24));

        foreach (var choice in FlagPresets.Choices)
        {
            presets.Add(Ui.Card(choice.Title, choice.Description,
                Ui.Combo(choice.Options, choice.Read(s.FastFlags), i => { choice.Write(s.FastFlags, i); Commit(); }), SymbolRegular.Options24));
        }
        foreach (var toggle in FlagPresets.Toggles)
        {
            presets.Add(Ui.Card(toggle.Title, toggle.Description,
                Ui.Toggle(toggle.IsOn(s.FastFlags), on => { toggle.Set(s.FastFlags, on); Commit(); }), SymbolRegular.ToggleRight24));
        }

        var editorButtons = Ui.Row(
            Ui.Button("Import JSON", ImportJson, ControlAppearance.Secondary, SymbolRegular.ArrowImport24),
            Ui.Button("Paste JSON", PasteJson, ControlAppearance.Secondary, SymbolRegular.ClipboardPaste24),
            Ui.Button("Copy JSON", CopyJson, ControlAppearance.Secondary, SymbolRegular.Copy24),
            Ui.Button("Clear all", () =>
            {
                if (!Ui.Confirm("Remove every fast flag?")) return;
                s.FastFlags.Clear();
                Commit();
            }, ControlAppearance.Danger, SymbolRegular.Delete24));

        var children = new List<UIElement>
        {
            Ui.Info("Allowlist", "Roblox only reads allowlisted flags from ClientAppSettings.json. Others are ignored silently. For anything else, use the Live Flags page."),
            Ui.Section("Presets"),
        };
        children.AddRange(presets);
        children.Add(Ui.Section("All flags"));
        children.Add(Ui.Row(_count));
        children.Add(_grid);
        children.Add(Ui.Spacer(8));
        children.Add(editorButtons);

        Content = Ui.Page("Fast Flags", "Engine settings Roblox reads when it starts. Saved to ClientAppSettings.json every launch.", children.ToArray());
        RefreshGrid();
    }

    private void Commit()
    {
        Store.SaveSettings();
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        _syncing = true;
        _rows.Clear();
        foreach (var (name, value) in Store.Settings.FastFlags.OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase))
            _rows.Add(new FlagRow { Name = name, Value = value });
        _syncing = false;
        _count.Text = $"{_rows.Count} flag{(_rows.Count == 1 ? "" : "s")}. Edit cells directly, select a row and press Delete to remove it.";
    }

    private void SyncFromGrid()
    {
        if (_syncing) return;
        var flags = new Dictionary<string, string>();
        foreach (var row in _rows)
        {
            var name = row.Name.Trim();
            if (name.Length > 0) flags[name] = row.Value.Trim();
        }
        Store.Settings.FastFlags = flags;
        Store.SaveSettings();
        _count.Text = $"{flags.Count} flag{(flags.Count == 1 ? "" : "s")}. Edit cells directly, select a row and press Delete to remove it.";
    }

    private void Merge(string json)
    {
        Dictionary<string, string> parsed;
        try { parsed = ClientAppSettings.Parse(json); }
        catch (System.Text.Json.JsonException ex)
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
        var json = System.Text.Json.JsonSerializer.Serialize(Store.Settings.FastFlags, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        Clipboard.SetText(json);
        Ui.Message("Copied fast flags to the clipboard.");
    }
}
