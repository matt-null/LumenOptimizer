using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

public sealed class InstalledProgram
{
    public string Name { get; init; } = "";
    public string Version { get; init; } = "";
    public string Publisher { get; init; } = "";
    public string InstallDate { get; init; } = "";
    public long SizeKb { get; init; }
    public string UninstallString { get; init; } = "";
    public string? QuietUninstallString { get; init; }
    public string RegistryKeyPath { get; init; } = "";
    public bool Is32Bit { get; init; }

    public string SizeText => SizeKb <= 0 ? "" : Cleaner.FormatBytes(SizeKb * 1024);
}

/// <summary>Lists installed desktop programs and starts their own uninstaller. Uninstalls cannot be undone.</summary>
public static class Uninstaller
{
    public static List<InstalledProgram> List()
    {
        var all = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);
        Read(all, Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false);
        Read(all, Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", true);
        Read(all, Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Uninstall", false);
        return all.Values.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void Read(Dictionary<string, InstalledProgram> into, RegistryKey hive, string path, bool is32)
    {
        try
        {
            using var root = hive.OpenSubKey(path, false);
            if (root is null) return;
            foreach (var sub in root.GetSubKeyNames())
            {
                try
                {
                    using var k = root.OpenSubKey(sub, false);
                    if (k is null) continue;
                    var name = k.GetValue("DisplayName") as string;
                    var uninstall = k.GetValue("UninstallString") as string;
                    if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uninstall)) continue;
                    if (k.GetValue("SystemComponent") is int sc && sc == 1) continue;       // hidden system entries
                    if (!string.IsNullOrEmpty(k.GetValue("ParentKeyName") as string)) continue; // updates and patches
                    if (Regex.IsMatch(name, @"^(Update for|Security Update|Hotfix for)\b", RegexOptions.IgnoreCase)) continue;

                    var size = k.GetValue("EstimatedSize") is int s ? s : 0;
                    var p = new InstalledProgram
                    {
                        Name = name.Trim(),
                        Version = k.GetValue("DisplayVersion") as string ?? "",
                        Publisher = k.GetValue("Publisher") as string ?? "",
                        InstallDate = k.GetValue("InstallDate") as string ?? "",
                        SizeKb = size,
                        UninstallString = uninstall.Trim(),
                        QuietUninstallString = k.GetValue("QuietUninstallString") as string,
                        RegistryKeyPath = $"{hive.Name}\\{path}\\{sub}",
                        Is32Bit = is32
                    };
                    into.TryAdd(p.Name + "|" + p.Version, p);
                }
                catch { /* skip unreadable entry */ }
            }
        }
        catch (Exception ex) { Log.Warn("Uninstaller list: " + ex.Message); }
    }

    /// <summary>The exact command that would run, shown to the user before they confirm.</summary>
    public static string CommandFor(InstalledProgram p, bool quiet)
    {
        var cmd = quiet && !string.IsNullOrWhiteSpace(p.QuietUninstallString) ? p.QuietUninstallString! : p.UninstallString;
        // MSI entries often say "MsiExec.exe /I{GUID}" (modify); we want remove.
        if (Regex.IsMatch(cmd, @"msiexec(\.exe)?\s+/I\s*\{", RegexOptions.IgnoreCase))
            cmd = Regex.Replace(cmd, @"/I\s*\{", "/X{", RegexOptions.IgnoreCase);
        return cmd;
    }

    /// <summary>Starts the program's own uninstaller and waits for it. Returns the exit code.</summary>
    public static int Run(InstalledProgram p, bool quiet)
    {
        var cmd = CommandFor(p, quiet);
        Log.Info($"Uninstall \"{p.Name}\": {cmd}");
        var psi = new ProcessStartInfo("cmd.exe", "/c \"" + cmd + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Could not start the uninstaller.");
        proc.WaitForExit();
        Log.Info($"  uninstaller exit code {proc.ExitCode}");
        return proc.ExitCode;
    }
}
