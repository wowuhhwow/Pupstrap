using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Pupstrap.LiveFlags;

/// <summary>Reads and writes 4-byte flag values in a running RobloxPlayerBeta at module base + RVA.</summary>
public sealed partial class RobloxMemory : IDisposable
{
    private const uint ProcessVmRead = 0x0010, ProcessVmWrite = 0x0020, ProcessVmOperation = 0x0008, ProcessQueryInformation = 0x0400;
    private const uint SnapModule = 0x00000008, SnapModule32 = 0x00000010;
    private const uint StillActive = 259;

    private IntPtr _handle;

    public int ProcessId { get; }
    public ulong ModuleBase { get; }
    public uint ModuleSize { get; }
    public string ExePath { get; }

    /// <summary>The version-xxxxxxxx folder the process runs from, or "" when the path has none.</summary>
    public string Version => VersionRegex().Match(ExePath) is { Success: true } m ? m.Value : "";

    private RobloxMemory(int pid, IntPtr handle, ulong moduleBase, uint moduleSize, string exePath)
    {
        ProcessId = pid;
        _handle = handle;
        ModuleBase = moduleBase;
        ModuleSize = moduleSize;
        ExePath = exePath;
    }

    [GeneratedRegex(@"version-[0-9a-fA-F]{16}")]
    private static partial Regex VersionRegex();

    public static RobloxMemory Attach(int? pid = null)
    {
        var target = pid ?? System.Diagnostics.Process.GetProcessesByName("RobloxPlayerBeta").OrderByDescending(p => p.StartTime).FirstOrDefault()?.Id
                     ?? throw new InvalidOperationException("Roblox isn't running.");

        if (!TryFindModule(target, out var moduleBase, out var moduleSize, out var exePath))
            throw new InvalidOperationException("Couldn't read Roblox's module list. Try again once the window has opened, or run Pupstrap as administrator.");

        var handle = OpenProcess(ProcessVmRead | ProcessVmWrite | ProcessVmOperation | ProcessQueryInformation, false, (uint)target);
        if (handle == IntPtr.Zero)
            throw new InvalidOperationException($"Windows refused access to Roblox (error {Marshal.GetLastWin32Error()}). Run Pupstrap as administrator.");

        return new RobloxMemory(target, handle, moduleBase, moduleSize, exePath);
    }

    public static bool TryFindModule(int pid, out ulong moduleBase, out uint moduleSize, out string exePath)
    {
        moduleBase = 0;
        moduleSize = 0;
        exePath = "";
        var snap = CreateToolhelp32Snapshot(SnapModule | SnapModule32, (uint)pid);
        if (snap == new IntPtr(-1)) return false;
        try
        {
            var entry = new ModuleEntry32 { dwSize = (uint)Marshal.SizeOf<ModuleEntry32>() };
            if (!Module32FirstW(snap, ref entry)) return false;
            do
            {
                if (!entry.szModule.Equals("RobloxPlayerBeta.exe", StringComparison.OrdinalIgnoreCase)) continue;
                moduleBase = (ulong)entry.modBaseAddr;
                moduleSize = entry.modBaseSize;
                exePath = entry.szExePath;
                return true;
            } while (Module32NextW(snap, ref entry));
            return false;
        }
        finally
        {
            CloseHandle(snap);
        }
    }

    public bool IsAlive => _handle != IntPtr.Zero && GetExitCodeProcess(_handle, out var code) && code == StillActive;

    private IntPtr Address(uint rva)
    {
        if (rva == 0) throw new ArgumentException("Flag has no offset.");
        if (ModuleSize != 0 && (ulong)rva + 4 > ModuleSize) throw new ArgumentException($"Offset 0x{rva:x} is outside the Roblox module. The dump is for another version.");
        return (IntPtr)(long)(ModuleBase + rva);
    }

    public int ReadInt32(uint rva)
    {
        var buffer = new byte[4];
        if (!ReadProcessMemory(_handle, Address(rva), buffer, 4, out var read) || read != 4)
            throw new InvalidOperationException($"Couldn't read 0x{rva:x} (error {Marshal.GetLastWin32Error()}).");
        return BitConverter.ToInt32(buffer);
    }

    public void WriteInt32(uint rva, int value)
    {
        var buffer = BitConverter.GetBytes(value);
        if (!WriteProcessMemory(_handle, Address(rva), buffer, 4, out var written) || written != 4)
            throw new InvalidOperationException($"Couldn't write 0x{rva:x} (error {Marshal.GetLastWin32Error()}).");
    }

    public void Dispose()
    {
        if (_handle == IntPtr.Zero) return;
        CloseHandle(_handle);
        _handle = IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ModuleEntry32
    {
        public uint dwSize;
        public uint th32ModuleID;
        public uint th32ProcessID;
        public uint GlblcntUsage;
        public uint ProccntUsage;
        public IntPtr modBaseAddr;
        public uint modBaseSize;
        public IntPtr hModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExePath;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr handle, out uint code);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr handle, IntPtr address, byte[] buffer, int size, out IntPtr read);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr handle, IntPtr address, byte[] buffer, int size, out IntPtr written);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32FirstW(IntPtr snap, ref ModuleEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool Module32NextW(IntPtr snap, ref ModuleEntry32 entry);
}
