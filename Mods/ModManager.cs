using System.Text.Json.Nodes;
using Pupstrap.Core;

namespace Pupstrap.Mods;

public static class ModManager
{
    private const string CustomFontName = "PupstrapCustomFont";

    private static string MetaDir(string versionDir) => Path.Combine(versionDir, ".pupstrap");
    private static string OriginalsDir(string versionDir) => Path.Combine(MetaDir(versionDir), "originals");
    private static string ModdedListFile(string versionDir) => Path.Combine(MetaDir(versionDir), "modded.txt");

    public static int Apply(string versionDir, Settings settings)
    {
        RestoreOriginals(versionDir);
        BuildGeneratedMods(versionDir, settings);

        var applied = new List<string>();
        ApplyFolder(Paths.GeneratedMods, versionDir, applied);
        if (settings.UseModsFolder) ApplyFolder(Paths.Mods, versionDir, applied);

        Directory.CreateDirectory(MetaDir(versionDir));
        File.WriteAllLines(ModdedListFile(versionDir), applied.Distinct(StringComparer.OrdinalIgnoreCase));
        Logger.Info($"Applied {applied.Count} mod files");
        return applied.Count;
    }

    public static void RestoreOriginals(string versionDir)
    {
        var listFile = ModdedListFile(versionDir);
        if (!File.Exists(listFile)) return;
        var originals = OriginalsDir(versionDir);
        foreach (var relative in File.ReadAllLines(listFile).Where(l => l.Length > 0))
        {
            var target = Path.Combine(versionDir, relative);
            var backup = Path.Combine(originals, relative);
            try
            {
                if (File.Exists(backup)) File.Copy(backup, target, true);
                else if (File.Exists(target)) File.Delete(target);
            }
            catch (IOException ex)
            {
                Logger.Warn($"Could not restore {relative}: {ex.Message}");
            }
        }
        File.Delete(listFile);
    }

    private static void ApplyFolder(string sourceRoot, string versionDir, List<string> applied)
    {
        if (!Directory.Exists(sourceRoot)) return;
        var originals = OriginalsDir(versionDir);
        foreach (var source in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceRoot, source);
            if (relative.StartsWith("..") || relative.StartsWith(".pupstrap", StringComparison.OrdinalIgnoreCase)) continue;
            var target = Path.Combine(versionDir, relative);
            var backup = Path.Combine(originals, relative);
            if (File.Exists(target) && !File.Exists(backup))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                File.Copy(target, backup);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, true);
            applied.Add(relative);
        }
    }

    private static void BuildGeneratedMods(string versionDir, Settings settings)
    {
        if (Directory.Exists(Paths.GeneratedMods)) Directory.Delete(Paths.GeneratedMods, true);
        Directory.CreateDirectory(Paths.GeneratedMods);

        if (File.Exists(settings.CustomFontPath)) BuildFontMod(versionDir, settings.CustomFontPath);
        else if (settings.CustomFontPath.Length > 0) Logger.Warn($"Custom font {settings.CustomFontPath} not found");

        if (File.Exists(settings.DeathSoundPath))
            CopyGenerated(settings.DeathSoundPath, @"content\sounds\ouch.ogg");

        if (File.Exists(settings.CursorPath))
        {
            CopyGenerated(settings.CursorPath, @"content\textures\Cursors\KeyboardMouse\ArrowCursor.png");
            CopyGenerated(settings.CursorPath, @"content\textures\Cursors\KeyboardMouse\ArrowFarCursor.png");
        }
    }

    private static void CopyGenerated(string source, string relative)
    {
        var target = Path.Combine(Paths.GeneratedMods, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(source, target, true);
    }

    private static void BuildFontMod(string versionDir, string fontPath)
    {
        var extension = Path.GetExtension(fontPath).ToLowerInvariant();
        var fontRelative = $@"content\fonts\{CustomFontName}{extension}";
        CopyGenerated(fontPath, fontRelative);
        var assetId = $"rbxasset://fonts/{CustomFontName}{extension}";

        var families = Path.Combine(versionDir, "content", "fonts", "families");
        if (!Directory.Exists(families)) return;
        foreach (var file in Directory.GetFiles(families, "*.json"))
        {
            try
            {
                var node = JsonNode.Parse(File.ReadAllText(file));
                if (node?["faces"] is not JsonArray faces) continue;
                foreach (var face in faces)
                    if (face is JsonObject obj) obj["assetId"] = assetId;
                var target = Path.Combine(Paths.GeneratedMods, "content", "fonts", "families", Path.GetFileName(file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target, node.ToJsonString());
            }
            catch (System.Text.Json.JsonException ex)
            {
                Logger.Warn($"Skipping font family {Path.GetFileName(file)}: {ex.Message}");
            }
        }
    }
}
