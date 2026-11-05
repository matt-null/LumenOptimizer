using Microsoft.Win32;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

public sealed class StartupEntry
{
    public string Name { get; init; } = "";
    public string Command { get; init; } = "";
    public string Source { get; init; } = "";      // e.g. "Registry (current user)"
    public bool Machine { get; init; }             // true = all users
    public bool Enabled { get; set; }

    // How to flip it (same mechanism as Task Manager's Startup tab):
    internal string ApprovedSubKey { get; init; } = "";   // under ...\Explorer\StartupApproved\
    internal string ApprovedValueName { get; init; } = "";
}

/// <summary>
/// Lists programs that start with Windows and switches them on/off the way Task Manager does, using the
/// StartupApproved registry keys. Nothing is deleted, so every change is reversible by switching it back on.
/// </summary>
public static class Startup
{
    private const string ApprovedRoot = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\";

    public static List<StartupEntry> List()
    {
        var list = new List<StartupEntry>();

        AddRun(list, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "Registry (current user)", false, "Run");
        AddRun(list, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", "Registry (all users)", true, "Run");
        AddRun(list, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "Registry (all users, 32-bit)", true, "Run32");

        AddFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Startup folder (current user)", false);
        AddFolder(list, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Startup folder (all users)", true);

        return list.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void AddRun(List<StartupEntry> list, RegistryKey hive, string path, string source, bool machine, string approvedKey)
    {
        try
        {
            using var key = hive.OpenSubKey(path, false);
            if (key is null) return;
            foreach (var name in key.GetValueNames())
            {
                var command = key.GetValue(name)?.ToString() ?? "";
                list.Add(new StartupEntry
                {
                    Name = name, Command = command, Source = source, Machine = machine,
                    ApprovedSubKey = approvedKey, ApprovedValueName = name,
                    Enabled = IsEnabled(machine, approvedKey, name)
                });
            }
        }
        catch (Exception ex) { Log.Warn($"Startup list ({source}): {ex.Message}"); }
    }

    private static void AddFolder(List<StartupEntry> list, string folder, string source, bool machine)
    {
        try
        {
            if (!Directory.Exists(folder)) return;
            foreach (var f in Directory.GetFiles(folder))
            {
                var name = Path.GetFileName(f);
                if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                list.Add(new StartupEntry
                {
                    Name = Path.GetFileNameWithoutExtension(f), Command = f, Source = source, Machine = machine,
                    ApprovedSubKey = "StartupFolder", ApprovedValueName = name,
                    Enabled = IsEnabled(machine, "StartupFolder", name)
                });
            }
        }
        catch (Exception ex) { Log.Warn($"Startup list ({source}): {ex.Message}"); }
    }

    private static RegistryKey Hive(bool machine) => machine ? Registry.LocalMachine : Registry.CurrentUser;

    private static bool IsEnabled(bool machine, string approvedKey, string valueName)
    {
        try
        {
            using var k = Hive(machine).OpenSubKey(ApprovedRoot + approvedKey, false);
            if (k?.GetValue(valueName) is byte[] data && data.Length > 0)
                return (data[0] & 1) == 0; // 02/06 = enabled, 03 = disabled (low bit set)
        }
        catch { /* treat as enabled */ }
        return true;
    }

    public static void SetEnabled(StartupEntry e, bool enabled)
    {
        Log.Info($"Startup: {(enabled ? "enable" : "disable")} {e.Name} ({e.Source})");
        using var k = Hive(e.Machine).CreateSubKey(ApprovedRoot + e.ApprovedSubKey, true)
            ?? throw new InvalidOperationException("Could not open the StartupApproved key.");

        var data = new byte[12];
        if (enabled)
        {
            data[0] = 0x02;
        }
        else
        {
            data[0] = 0x03;
            BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
        }

        k.SetValue(e.ApprovedValueName, data, RegistryValueKind.Binary);
        e.Enabled = enabled;
    }
}
