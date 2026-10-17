using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Pupstrap.Launching;

namespace Pupstrap.Core;

/// <summary>
/// Roblox's own settings (the in-game Settings menu), stored in GlobalBasicSettings_13.xml.
/// Roblox reads the file when it starts and rewrites it when it closes, so edits made while it runs are kept
/// in <see cref="Settings.PendingGlobalSettings"/> and written again at the next launch.
/// </summary>
public static class RobloxGlobalSettings
{
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "GlobalBasicSettings_13.xml");

    public static bool Exists => File.Exists(FilePath);

    /// <summary>Current values from the file with pending edits on top. Vector2 values are read as their X.</summary>
    public static Dictionary<string, string> Read()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (Load() is { } loaded)
        {
            foreach (var element in loaded.Properties.Elements())
            {
                if (element.Attribute("name")?.Value is not { } name) continue;
                values[name] = element.Name.LocalName == "Vector2" ? element.Element("X")?.Value ?? "" : element.Value;
            }
        }
        foreach (var (name, value) in Store.Settings.PendingGlobalSettings) values[name] = value;
        return values;
    }

    public static void Set(string name, string value) => Store.Settings.PendingGlobalSettings[name] = value;

    public static string Number(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    public static bool TryNumber(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    /// <summary>Writes pending edits to the file. They stay pending while Roblox runs, because it overwrites the file on exit.</summary>
    /// <returns>True when the edits are final, false when Roblox is running and they'll be written again next launch.</returns>
    public static bool ApplyPending()
    {
        var settings = Store.Settings;
        if (settings.PendingGlobalSettings.Count == 0) return true;

        var loaded = Load() ?? throw new InvalidOperationException("Roblox hasn't created its settings file yet. Launch Roblox once, then try again.");
        foreach (var (name, value) in settings.PendingGlobalSettings)
        {
            var element = loaded.Properties.Elements().FirstOrDefault(e => e.Attribute("name")?.Value == name);
            if (element is null)
            {
                Logger.Warn($"Roblox setting {name} isn't in GlobalBasicSettings, skipping it");
                continue;
            }
            if (element.Name.LocalName == "Vector2")
            {
                element.SetElementValue("X", value);
                element.SetElementValue("Y", value);
            }
            else element.Value = value;
        }

        var xml = new XmlWriterSettings
        {
            OmitXmlDeclaration = true,
            Indent = true,
            IndentChars = "\t",
            NewLineChars = "\r\n",
            NewLineHandling = NewLineHandling.Replace,
        };
        using (var writer = XmlWriter.Create(FilePath, xml))
            loaded.Document.Save(writer);
        Logger.Info($"Wrote {settings.PendingGlobalSettings.Count} Roblox setting(s) to {Path.GetFileName(FilePath)}");

        if (Bootstrapper.RunningRoblox().Length > 0) return false;
        settings.PendingGlobalSettings.Clear();
        Store.SaveSettings();
        return true;
    }

    private static (XDocument Document, XElement Properties)? Load()
    {
        if (!File.Exists(FilePath)) return null;
        var document = XDocument.Load(FilePath);
        var properties = document.Root?.Elements("Item")
            .FirstOrDefault(e => e.Attribute("class")?.Value == "UserGameSettings")?.Element("Properties");
        return properties is null ? null : (document, properties);
    }
}
