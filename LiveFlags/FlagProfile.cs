using System.Globalization;
using System.Reflection;
using Pupstrap.Core;

namespace Pupstrap.LiveFlags;

/// <summary>A .flags file: "Name=int32" lines, "#" comments and an optional roblox_version= line.</summary>
public sealed class FlagProfile
{
    public string Name { get; init; } = "";
    public string RobloxVersion { get; set; } = "";
    public List<KeyValuePair<string, int>> Values { get; } = new();

    public static string Folder => System.IO.Path.Combine(Paths.Base, "Profiles");

    public static FlagProfile Load(string path)
    {
        var profile = new FlagProfile { Name = System.IO.Path.GetFileNameWithoutExtension(path) };
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line[0] == '#') continue;
            var eq = line.IndexOf('=');
            if (eq <= 0) continue;
            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (key == "roblox_version") profile.RobloxVersion = value;
            else if (int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
                profile.Values.Add(new(key, number));
        }
        if (profile.Values.Count == 0) throw new InvalidDataException("That profile has no flag values.");
        return profile;
    }

    public void Save(string path)
    {
        var lines = new List<string> { "# wraith-fflag-profile 1", $"roblox_version={RobloxVersion}" };
        lines.AddRange(Values.Select(v => $"{v.Key}={v.Value}"));
        File.WriteAllLines(path, lines);
    }

    public static IEnumerable<string> Available() =>
        Directory.Exists(Folder) ? Directory.GetFiles(Folder, "*.flags").OrderBy(f => f) : Enumerable.Empty<string>();

    /// <summary>Writes the bundled ultra-low and ultra-high profiles, overwriting them only when asked.</summary>
    public static void InstallBuiltIns(bool overwrite)
    {
        Directory.CreateDirectory(Folder);
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var resource in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".flags")))
        {
            var fileName = resource.Split('.')[^2] + ".flags";
            var target = System.IO.Path.Combine(Folder, fileName);
            if (File.Exists(target) && !overwrite) continue;
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var file = File.Create(target);
            stream.CopyTo(file);
        }
    }
}
