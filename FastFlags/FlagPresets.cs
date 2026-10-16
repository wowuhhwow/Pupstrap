using System.Text.Json;
using Pupstrap.Core;

namespace Pupstrap.FastFlags;

public sealed record ChoicePreset(string Title, string Description, string[] Options, Func<IDictionary<string, string>, int> Read, Action<IDictionary<string, string>, int> Write);

public sealed record TogglePreset(string Title, string Description, IReadOnlyDictionary<string, string> Flags)
{
    public bool IsOn(IDictionary<string, string> flags) =>
        Flags.All(f => flags.TryGetValue(f.Key, out var v) && string.Equals(v, f.Value, StringComparison.OrdinalIgnoreCase));

    public void Set(IDictionary<string, string> flags, bool on)
    {
        foreach (var (key, value) in Flags)
        {
            if (on) flags[key] = value;
            else flags.Remove(key);
        }
    }
}

public static class FlagPresets
{
    private static readonly string[] RendererFlags =
    {
        "FFlagDebugGraphicsPreferD3D11", "FFlagDebugGraphicsPreferVulkan", "FFlagDebugGraphicsPreferOpenGL", "FFlagDebugGraphicsPreferD3D11FL10",
    };

    private static readonly string[] LightingFlags =
    {
        "DFFlagDebugRenderForceTechnologyVoxel", "FFlagDebugForceFutureIsBrightPhase2", "FFlagDebugForceFutureIsBrightPhase3",
    };

    private static int SelectedFlag(IDictionary<string, string> flags, string[] names)
    {
        for (var i = 0; i < names.Length; i++)
            if (flags.TryGetValue(names[i], out var v) && v.Equals("True", StringComparison.OrdinalIgnoreCase)) return i + 1;
        return 0;
    }

    private static void SelectFlag(IDictionary<string, string> flags, string[] names, int index)
    {
        foreach (var name in names) flags.Remove(name);
        if (index > 0) flags[names[index - 1]] = "True";
    }

    private static int ReadValue(IDictionary<string, string> flags, string name, string[] values)
    {
        if (!flags.TryGetValue(name, out var v)) return 0;
        var i = Array.IndexOf(values, v);
        return i < 0 ? 0 : i + 1;
    }

    private static void WriteValue(IDictionary<string, string> flags, string name, string[] values, int index)
    {
        if (index <= 0) flags.Remove(name);
        else flags[name] = values[index - 1];
    }

    private static readonly string[] MsaaValues = { "1", "2", "4", "8" };
    private static readonly string[] TextureValues = { "0", "1", "2", "3" };
    private static readonly string[] QualityValues = Enumerable.Range(1, 21).Select(i => i.ToString()).ToArray();

    public static IReadOnlyList<ChoicePreset> Choices { get; } = new List<ChoicePreset>
    {
        new("Rendering API", "Which graphics backend Roblox prefers.",
            new[] { "Automatic", "Direct3D 11", "Vulkan", "OpenGL", "Direct3D 10" },
            f => SelectedFlag(f, RendererFlags), (f, i) => SelectFlag(f, RendererFlags, i)),
        new("Lighting technology", "Forces a lighting mode regardless of what the game picks.",
            new[] { "Chosen by game", "Voxel", "Shadowmap", "Future" },
            f => SelectedFlag(f, LightingFlags), (f, i) => SelectFlag(f, LightingFlags, i)),
        new("Anti-aliasing (MSAA)", "Edge smoothing samples. Off is fastest.",
            new[] { "Automatic", "Off", "2x", "4x", "8x" },
            f => ReadValue(f, "FIntDebugForceMSAASamples", MsaaValues), (f, i) => WriteValue(f, "FIntDebugForceMSAASamples", MsaaValues, i)),
        new("Texture quality", "Overrides texture resolution.",
            new[] { "Automatic", "Lowest", "Low", "Medium", "High" },
            f => ReadValue(f, "DFIntTextureQualityOverride", TextureValues),
            (f, i) =>
            {
                WriteValue(f, "DFIntTextureQualityOverride", TextureValues, i);
                if (i > 0) f["DFFlagTextureQualityOverrideEnabled"] = "True";
                else f.Remove("DFFlagTextureQualityOverrideEnabled");
            }),
        new("Graphics quality level", "Locks the graphics slider level (1 lowest, 21 highest).",
            new[] { "Automatic" }.Concat(QualityValues).ToArray(),
            f => ReadValue(f, "DFIntDebugFRMQualityLevelOverride", QualityValues), (f, i) => WriteValue(f, "DFIntDebugFRMQualityLevelOverride", QualityValues, i)),
    };

    public static IReadOnlyList<TogglePreset> Toggles { get; } = new List<TogglePreset>
    {
        new("Disable post-processing", "Removes bloom, blur, sun rays, colour correction and depth of field.",
            new Dictionary<string, string> { ["FFlagDisablePostFx"] = "True" }),
        new("Remove grass", "Stops terrain grass from rendering.",
            new Dictionary<string, string> { ["FIntFRMMinGrassDistance"] = "0", ["FIntFRMMaxGrassDistance"] = "0", ["FIntRenderGrassDetailStrands"] = "0" }),
        new("Disable shadows", "Turns off shadow intensity.",
            new Dictionary<string, string> { ["FIntRenderShadowIntensity"] = "0" }),
        new("Low-poly CSG", "Unions and negates use their lowest detail at every distance.",
            new Dictionary<string, string>
            {
                ["DFIntCSGLevelOfDetailSwitchingDistance"] = "0", ["DFIntCSGLevelOfDetailSwitchingDistanceL12"] = "0",
                ["DFIntCSGLevelOfDetailSwitchingDistanceL23"] = "0", ["DFIntCSGLevelOfDetailSwitchingDistanceL34"] = "0",
            }),
        new("Gray sky", "Replaces the skybox with flat gray.",
            new Dictionary<string, string> { ["FFlagDebugSkyGray"] = "True" }),
        new("Alt+Enter fullscreen fix", "Lets Alt+Enter toggle exclusive fullscreen.",
            new Dictionary<string, string> { ["FFlagHandleAltEnterFullscreenManually"] = "False" }),
    };

    public static int ReadFpsCap(IDictionary<string, string> flags) =>
        flags.TryGetValue("DFIntTaskSchedulerTargetFps", out var v) && int.TryParse(v, out var fps) ? fps : 0;

    public static void WriteFpsCap(IDictionary<string, string> flags, int fps)
    {
        if (fps <= 0)
        {
            flags.Remove("DFIntTaskSchedulerTargetFps");
            flags.Remove("FFlagTaskSchedulerLimitTargetFpsTo2402");
            return;
        }
        flags["DFIntTaskSchedulerTargetFps"] = fps.ToString();
        if (fps > 240) flags["FFlagTaskSchedulerLimitTargetFpsTo2402"] = "False";
        else flags.Remove("FFlagTaskSchedulerLimitTargetFpsTo2402");
    }
}

public static class ClientAppSettings
{
    public static void Write(string versionDir, IReadOnlyDictionary<string, string> flags)
    {
        var dir = Path.Combine(versionDir, "ClientSettings");
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(flags.OrderBy(f => f.Key, StringComparer.Ordinal).ToDictionary(f => f.Key, f => f.Value),
            new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(dir, "ClientAppSettings.json"), json);
        Logger.Info($"Wrote {flags.Count} fast flags");
    }

    public static Dictionary<string, string> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var result = new Dictionary<string, string>();
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? "",
                JsonValueKind.True => "True",
                JsonValueKind.False => "False",
                _ => property.Value.GetRawText(),
            };
        }
        return result;
    }
}
