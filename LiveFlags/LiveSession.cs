using Pupstrap.Core;

namespace Pupstrap.LiveFlags;

public sealed class FlagChange
{
    public required string Name { get; init; }
    public required uint Rva { get; init; }
    public required int Original { get; init; }
    public int Current { get; set; }
}

public sealed record ProfileResult(int Applied, int Skipped, int Failed, int Reverted);

/// <summary>The dump, the attached Roblox and every flag written this session, shared by the Live Flags page and auto-apply.</summary>
public static class LiveSession
{
    private static readonly List<FlagChange> ChangeList = new();

    public static FlagDump? Dump { get; private set; }
    public static RobloxMemory? Memory { get; private set; }
    public static IReadOnlyList<FlagChange> Changes => ChangeList;

    public static event Action? Changed;

    /// <summary>Raised on the UI thread after the guard puts back flags Roblox reset.</summary>
    public static event Action<IReadOnlyList<string>>? Restored;

    private static FlagGuard? _guard;

    public static bool IsAttached => Memory is { IsAlive: true };

    public static bool IsGuarding => _guard is not null && ChangeList.Count > 0 && IsAttached;

    /// <summary>Starts or stops rewriting flags Roblox resets, following the Keep applied setting.</summary>
    public static void UpdateGuard()
    {
        var wanted = Store.Settings.LiveKeepApplied && Memory is { IsAlive: true };
        if (!wanted)
        {
            _guard?.Dispose();
            _guard = null;
            return;
        }
        if (_guard is not null) return;
        _guard = new FlagGuard(Memory!);
        _guard.Restored += names => System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => Restored?.Invoke(names));
        foreach (var change in ChangeList) _guard.Set(change.Name, change.Rva, change.Current);
    }

    public static FlagDump LoadDump(string path)
    {
        Dump = FlagDump.Load(path);
        Logger.Info($"Loaded {Dump.Flags.Count} flags for {Dump.RobloxVersion} from {path}");
        Changed?.Invoke();
        return Dump;
    }

    public static FlagDump? TryLoadConfiguredDump()
    {
        if (Dump is not null) return Dump;
        var path = Store.Settings.LiveDumpPath;
        if (!File.Exists(path)) return null;
        try { return LoadDump(path); }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            Logger.Warn($"Could not load dump {path}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Attaches to Roblox. Refuses when the running client isn't the version the dump was made for.</summary>
    public static RobloxMemory Attach(int? pid = null)
    {
        Detach();
        var memory = RobloxMemory.Attach(pid);
        if (Dump is not null && memory.Version.Length > 0 && Dump.RobloxVersion.Length > 0 &&
            !memory.Version.Equals(Dump.RobloxVersion, StringComparison.OrdinalIgnoreCase))
        {
            memory.Dispose();
            throw new InvalidOperationException(
                $"Roblox is running {memory.Version} but the dump is for {Dump.RobloxVersion}. Writing would hit the wrong memory, so Pupstrap won't attach. Dump the new version or pin {Dump.RobloxVersion} in Deployment.");
        }
        Memory = memory;
        Logger.Info($"Attached to Roblox pid {memory.ProcessId} ({memory.Version})");
        UpdateGuard();
        Changed?.Invoke();
        return memory;
    }

    public static void Detach()
    {
        _guard?.Dispose();
        _guard = null;
        Memory?.Dispose();
        Memory = null;
        ChangeList.Clear();
        Changed?.Invoke();
    }

    private static RobloxMemory RequireMemory()
    {
        if (Memory is null || !Memory.IsAlive)
        {
            _guard?.Dispose();
            _guard = null;
            Memory?.Dispose();
            Memory = null;
            ChangeList.Clear();
            throw new InvalidOperationException("Attach to Roblox first.");
        }
        return Memory;
    }

    public static int Read(FlagEntry flag) => RequireMemory().ReadInt32(flag.Rva);

    public static void Write(FlagEntry flag, int value)
    {
        var memory = RequireMemory();
        var existing = ChangeList.FirstOrDefault(c => c.Name == flag.Name);
        var before = memory.ReadInt32(flag.Rva);
        if (before != value) memory.WriteInt32(flag.Rva, value);
        if (existing is null)
        {
            if (before != value) ChangeList.Add(new FlagChange { Name = flag.Name, Rva = flag.Rva, Original = before, Current = value });
        }
        else if (existing.Original == value)
        {
            ChangeList.Remove(existing);
        }
        else
        {
            existing.Current = value;
        }
        if (ChangeList.Any(c => c.Name == flag.Name)) _guard?.Set(flag.Name, flag.Rva, value);
        else _guard?.Remove(flag.Name);
        Changed?.Invoke();
    }

    public static void Revert(FlagChange change)
    {
        _guard?.Remove(change.Name);
        RequireMemory().WriteInt32(change.Rva, change.Original);
        ChangeList.Remove(change);
        Changed?.Invoke();
    }

    public static void RevertAll()
    {
        foreach (var change in ChangeList.ToList()) Revert(change);
    }

    /// <summary>Applies a profile, first reverting earlier edits the profile doesn't set so profiles never stack.</summary>
    public static ProfileResult ApplyProfile(FlagProfile profile)
    {
        var dump = Dump ?? throw new InvalidOperationException("Load an offset dump first.");
        RequireMemory();
        var names = new HashSet<string>(profile.Values.Select(v => v.Key), StringComparer.OrdinalIgnoreCase);
        var reverted = 0;
        foreach (var change in ChangeList.Where(c => !names.Contains(c.Name)).ToList())
        {
            Revert(change);
            reverted++;
        }

        int applied = 0, skipped = 0, failed = 0;
        foreach (var (name, value) in profile.Values)
        {
            var flag = dump.Find(name);
            if (flag is null) { skipped++; continue; }
            try
            {
                Write(flag, value);
                applied++;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                failed++;
                Logger.Warn($"{name}: {ex.Message}");
            }
        }
        Logger.Info($"Profile {profile.Name}: applied {applied}, skipped {skipped}, failed {failed}, reverted {reverted}");
        return new ProfileResult(applied, skipped, failed, reverted);
    }

    public static FlagProfile SnapshotProfile(string name)
    {
        var profile = new FlagProfile { Name = name, RobloxVersion = Dump?.RobloxVersion ?? "" };
        profile.Values.AddRange(ChangeList.Select(c => new KeyValuePair<string, int>(c.Name, c.Current)));
        return profile;
    }
}
