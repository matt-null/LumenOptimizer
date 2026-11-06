using System.Text.Json;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

public sealed record ServiceRow(string Name, string Display, string State, string StartMode, string Description, bool Critical);

/// <summary>Services manager. The original start type is saved before the first change so it can always be restored.</summary>
public static class ServicesTool
{
    private static readonly HashSet<string> Critical = new(StringComparer.OrdinalIgnoreCase)
    {
        "RpcSs","RpcEptMapper","DcomLaunch","LSM","SamSs","PlugPlay","Power","ProfSvc","BrokerInfrastructure","CoreMessaging",
        "gpsvc","nsi","BFE","Schedule","SystemEventsBroker","UserManager","StateRepository","CryptSvc","Winmgmt","EventLog","TrustedInstaller"
    };

    private static string BackupFile => Path.Combine(AppPaths.DataDir, "service-backups.json");

    public static List<ServiceRow> List() =>
        Ps.Json("ConvertTo-Json -InputObject @(Get-CimInstance Win32_Service | Select-Object Name,DisplayName,State,StartMode,Description) -Compress", 60_000)
          .Select(e => new ServiceRow(e.S("Name"), e.S("DisplayName"), e.S("State"), e.S("StartMode"), e.S("Description"), Critical.Contains(e.S("Name"))))
          .OrderBy(s => s.Display).ToList();

    private static Dictionary<string, string> Backups()
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(BackupFile)) ?? new(); }
        catch { return new(); }
    }

    private static void Guard(string name, string what)
    {
        if (Critical.Contains(name)) throw new InvalidOperationException($"{name} is a core Windows service. {what} could stop Windows working, so it is blocked.");
        if (!List().Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("No such service.");
    }

    public static string SetStartMode(string name, string mode)
    {
        if (mode == "Disabled") Guard(name, "Disabling it");
        var current = List().FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("No such service.");
        var b = Backups();
        if (!b.ContainsKey(name))
        {
            b[name] = current.StartMode;
            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(BackupFile, JsonSerializer.Serialize(b));
        }
        return Apply(name, mode);
    }

    public static string Restore(string name)
    {
        var b = Backups();
        if (!b.TryGetValue(name, out var mode)) return "Nothing to restore: its start type has not been changed by this app.";
        var msg = Apply(name, mode);
        b.Remove(name);
        File.WriteAllText(BackupFile, JsonSerializer.Serialize(b));
        return msg;
    }

    private static string Apply(string name, string mode)
    {
        var sc = mode switch { "Auto" => "auto", "Manual" => "demand", "Disabled" => "disabled", _ => throw new InvalidOperationException("Cannot set " + mode) };
        var r = Shell.Run("sc.exe", $"config \"{name}\" start= {sc}");
        if (r.ExitCode != 0) throw new InvalidOperationException(r.Output);
        return $"{name} start type is now {mode}.";
    }

    public static string StartStop(string name, bool start)
    {
        if (!start) Guard(name, "Stopping it");
        var r = Shell.Run("sc.exe", $"{(start ? "start" : "stop")} \"{name}\"", 60_000);
        if (r.ExitCode != 0 && !r.Output.Contains("1056") && !r.Output.Contains("1062")) throw new InvalidOperationException(r.Output);
        return $"{name} {(start ? "start" : "stop")} requested.";
    }
}
