using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

public sealed record TaskRow(string Path, string Name, string State, string Author, string Description)
{
    public string Id => Path + Name;
    public bool IsMicrosoft => Path.StartsWith("\\Microsoft\\", StringComparison.OrdinalIgnoreCase);
}

public static class TaskSchedulerTool
{
    public static List<TaskRow> List() =>
        Ps.Json("ConvertTo-Json -InputObject @(Get-ScheduledTask | ForEach-Object { [pscustomobject]@{Path=$_.TaskPath;Name=$_.TaskName;State=[string]$_.State;Author=[string]$_.Author;Description=[string]$_.Description} }) -Compress", 90_000)
          .Select(e => new TaskRow(e.S("Path"), e.S("Name"), e.S("State"), e.S("Author"), e.S("Description")))
          .OrderBy(t => t.Path).ThenBy(t => t.Name).ToList();

    private static void Do(TaskRow t, string cmdlet)
    {
        var (c, o) = Shell.PowerShell($"{cmdlet} -TaskPath '{Ps.Q(t.Path)}' -TaskName '{Ps.Q(t.Name)}' | Out-Null");
        if (c != 0) throw new InvalidOperationException(o);
    }

    public static string Enable(TaskRow t) { Do(t, "Enable-ScheduledTask"); return "Enabled."; }
    public static string Disable(TaskRow t) { Do(t, "Disable-ScheduledTask"); return "Disabled. You can switch it back on any time."; }
    public static string RunNow(TaskRow t) { Do(t, "Start-ScheduledTask"); return "Started."; }

    public static string Delete(TaskRow t)
    {
        if (t.IsMicrosoft) throw new InvalidOperationException("Built-in Windows tasks are not deleted. Disable it instead; that is reversible.");
        var (c, o) = Shell.PowerShell($"Unregister-ScheduledTask -TaskPath '{Ps.Q(t.Path)}' -TaskName '{Ps.Q(t.Name)}' -Confirm:$false");
        if (c != 0) throw new InvalidOperationException(o);
        return "Deleted.";
    }
}
