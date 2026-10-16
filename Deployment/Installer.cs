using System.IO.Compression;
using System.Security.Cryptography;
using Pupstrap.Core;

namespace Pupstrap.Deployment;

public sealed record PackageEntry(string Name, string Md5, long PackedSize, long Size);

public sealed record InstallProgress(string Status, double Fraction);

public static class Installer
{
    private static readonly string[] Mirrors =
    {
        "https://setup.rbxcdn.com",
        "https://setup-aws.rbxcdn.com",
        "https://setup-ak.rbxcdn.com",
        "https://roblox-setup.cachefly.net",
        "https://s3.amazonaws.com/setup.roblox.com",
    };

    private static readonly Dictionary<string, string> PackageDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["RobloxApp.zip"] = "",
        ["redist.zip"] = "",
        ["shaders.zip"] = "shaders",
        ["ssl.zip"] = "ssl",
        ["WebView2.zip"] = "",
        ["WebView2RuntimeInstaller.zip"] = "WebView2RuntimeInstaller",
        ["content-avatar.zip"] = @"content\avatar",
        ["content-configs.zip"] = @"content\configs",
        ["content-fonts.zip"] = @"content\fonts",
        ["content-sky.zip"] = @"content\sky",
        ["content-sounds.zip"] = @"content\sounds",
        ["content-textures2.zip"] = @"content\textures",
        ["content-models.zip"] = @"content\models",
        ["content-platform-fonts.zip"] = @"PlatformContent\pc\fonts",
        ["content-platform-dictionaries.zip"] = @"PlatformContent\pc\shared_compression_dictionaries",
        ["content-terrain.zip"] = @"PlatformContent\pc\terrain",
        ["content-textures3.zip"] = @"PlatformContent\pc\textures",
        ["extracontent-luapackages.zip"] = @"ExtraContent\LuaPackages",
        ["extracontent-translations.zip"] = @"ExtraContent\translations",
        ["extracontent-models.zip"] = @"ExtraContent\models",
        ["extracontent-textures.zip"] = @"ExtraContent\textures",
        ["extracontent-places.zip"] = @"ExtraContent\places",
    };

    private const string AppSettingsXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Settings>\r\n\t<ContentFolder>content</ContentFolder>\r\n\t<BaseUrl>http://www.roblox.com</BaseUrl>\r\n</Settings>\r\n";

    public static bool IsInstalled(string version) =>
        File.Exists(Path.Combine(Paths.VersionDir(version), "RobloxPlayerBeta.exe"));

    private static string ChannelPath(string channel) =>
        RobloxVersions.IsLive(channel) ? "" : $"/channel/{channel.ToLowerInvariant()}";

    public static async Task InstallAsync(string version, string channel, IProgress<InstallProgress> progress, CancellationToken token)
    {
        progress.Report(new InstallProgress("Fetching package list", 0));
        var (manifest, mirror) = await FetchManifestAsync(version, channel, token);
        Logger.Info($"Installing {version} from {mirror} ({manifest.Count} packages)");

        var stagingDir = Paths.VersionDir(version) + ".staging";
        if (Directory.Exists(stagingDir)) Directory.Delete(stagingDir, true);
        Directory.CreateDirectory(stagingDir);

        long totalBytes = Math.Max(1, manifest.Sum(p => p.PackedSize));
        long doneBytes = 0;
        void Advance(long bytes)
        {
            var now = Interlocked.Add(ref doneBytes, bytes);
            progress.Report(new InstallProgress($"Downloading Roblox ({now / 1048576} / {totalBytes / 1048576} MB)", 0.9 * now / totalBytes));
        }

        using var gate = new SemaphoreSlim(4);
        var downloads = manifest.Select(async package =>
        {
            await gate.WaitAsync(token);
            try { return await DownloadPackageAsync(package, version, channel, mirror, Advance, token); }
            finally { gate.Release(); }
        }).ToList();
        var files = await Task.WhenAll(downloads);

        for (var i = 0; i < manifest.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var package = manifest[i];
            progress.Report(new InstallProgress($"Extracting {package.Name}", 0.9 + 0.1 * i / manifest.Count));
            if (!PackageDirectories.TryGetValue(package.Name, out var relative))
            {
                Logger.Warn($"Unknown package {package.Name}, extracting to the version root");
                relative = "";
            }
            var destination = Path.Combine(stagingDir, relative);
            Directory.CreateDirectory(destination);
            ExtractSafely(files[i], destination);
        }

        await File.WriteAllTextAsync(Path.Combine(stagingDir, "AppSettings.xml"), AppSettingsXml, token);

        var finalDir = Paths.VersionDir(version);
        if (Directory.Exists(finalDir)) Directory.Delete(finalDir, true);
        Directory.Move(stagingDir, finalDir);
        progress.Report(new InstallProgress("Installed", 1));
        Logger.Info($"Installed {version}");
    }

    private static async Task<(List<PackageEntry>, string)> FetchManifestAsync(string version, string channel, CancellationToken token)
    {
        Exception? last = null;
        foreach (var mirror in Mirrors)
        {
            try
            {
                var text = await Http.GetStringAsync($"{mirror}{ChannelPath(channel)}/{version}-rbxPkgManifest.txt", token);
                return (ParseManifest(text), mirror);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !token.IsCancellationRequested)
            {
                last = ex;
                Logger.Warn($"Manifest from {mirror} failed: {ex.Message}");
            }
        }
        throw new InvalidOperationException($"No mirror has {version}. It may be too old or the channel may be wrong.", last);
    }

    public static List<PackageEntry> ParseManifest(string text)
    {
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
        if (lines.Length == 0 || lines[0] != "v0") throw new InvalidDataException("Unexpected package manifest format.");
        var result = new List<PackageEntry>();
        for (var i = 1; i + 3 < lines.Length; i += 4)
        {
            if (!lines[i].EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(new PackageEntry(lines[i], lines[i + 1].ToLowerInvariant(), long.Parse(lines[i + 2]), long.Parse(lines[i + 3])));
        }
        return result;
    }

    private static async Task<string> DownloadPackageAsync(PackageEntry package, string version, string channel, string preferredMirror,
        Action<long> advance, CancellationToken token)
    {
        var cached = Path.Combine(Paths.Downloads, package.Md5);
        if (File.Exists(cached) && await Md5Async(cached, token) == package.Md5)
        {
            advance(package.PackedSize);
            return cached;
        }

        foreach (var mirror in Mirrors.OrderBy(m => m == preferredMirror ? 0 : 1))
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                long counted = 0;
                try
                {
                    var url = $"{mirror}{ChannelPath(channel)}/{version}-{package.Name}";
                    using var response = await Http.Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
                    response.EnsureSuccessStatusCode();
                    var temp = cached + ".part";
                    await using (var input = await response.Content.ReadAsStreamAsync(token))
                    await using (var output = File.Create(temp))
                    {
                        var buffer = new byte[81920];
                        int read;
                        while ((read = await input.ReadAsync(buffer, token)) > 0)
                        {
                            await output.WriteAsync(buffer.AsMemory(0, read), token);
                            counted += read;
                            advance(read);
                        }
                    }
                    if (await Md5Async(temp, token) != package.Md5)
                        throw new InvalidDataException($"{package.Name} failed its checksum.");
                    File.Move(temp, cached, true);
                    return cached;
                }
                catch (Exception ex) when (!token.IsCancellationRequested && ex is HttpRequestException or IOException or InvalidDataException or TaskCanceledException)
                {
                    advance(-counted);
                    Logger.Warn($"{package.Name} from {mirror} failed (attempt {attempt + 1}): {ex.Message}");
                }
            }
        }
        throw new InvalidOperationException($"Could not download {package.Name} from any mirror.");
    }

    private static async Task<string> Md5Async(string path, CancellationToken token)
    {
        await using var stream = File.OpenRead(path);
        var hash = await MD5.HashDataAsync(stream, token);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ExtractSafely(string zipPath, string destination)
    {
        var root = Path.GetFullPath(destination);
        if (!root.EndsWith(Path.DirectorySeparatorChar)) root += Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var relative = entry.FullName.Replace('/', '\\').TrimStart('\\');
            if (relative.Length == 0) continue;
            var target = Path.GetFullPath(Path.Combine(root, relative));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Package entry {entry.FullName} escapes the install folder.");
            if (relative.EndsWith('\\'))
            {
                Directory.CreateDirectory(target);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);
        }
    }

    public static int CleanupOldVersions(params string[] keep)
    {
        var removed = 0;
        foreach (var dir in Directory.GetDirectories(Paths.Versions))
        {
            var name = Path.GetFileName(dir);
            if (keep.Any(k => k.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;
            try
            {
                Directory.Delete(dir, true);
                removed++;
                Logger.Info($"Removed old version {name}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Logger.Warn($"Could not remove {name}, it may still be running: {ex.Message}");
            }
        }
        return removed;
    }

    public static void ClearDownloadCache()
    {
        foreach (var file in Directory.GetFiles(Paths.Downloads))
        {
            try { File.Delete(file); } catch (IOException) { }
        }
    }
}
