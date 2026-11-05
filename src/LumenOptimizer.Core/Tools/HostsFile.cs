using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

/// <summary>Reads and edits the Windows hosts file. Every save makes a timestamped backup first.</summary>
public static class HostsFile
{
    public static string Path { get; } =
        System.IO.Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");

    public static string BackupDir => System.IO.Path.Combine(AppPaths.DataDir, "hosts-backups");

    /// <summary>Well-known Microsoft telemetry host names used by common block lists. Optional; see the warning in the UI.</summary>
    public static IReadOnlyList<string> TelemetryHosts { get; } = new[]
    {
        "vortex.data.microsoft.com", "vortex-win.data.microsoft.com", "telecommand.telemetry.microsoft.com",
        "oca.telemetry.microsoft.com", "sqm.telemetry.microsoft.com", "watson.telemetry.microsoft.com",
        "redir.metaservices.microsoft.com", "choice.microsoft.com", "df.telemetry.microsoft.com",
        "reports.wes.df.telemetry.microsoft.com", "services.wes.df.telemetry.microsoft.com",
        "sqm.df.telemetry.microsoft.com", "telemetry.microsoft.com", "settings-sandbox.data.microsoft.com"
    };

    public static string Read() => File.Exists(Path) ? File.ReadAllText(Path) : "";

    private static readonly Regex Hostname = new(@"^[A-Za-z0-9]([A-Za-z0-9\-_.]*[A-Za-z0-9])?$", RegexOptions.Compiled);

    /// <summary>Returns a list of problems, one per bad line. Empty = fine.</summary>
    public static List<string> Validate(string text)
    {
        var problems = new List<string>();
        var n = 0;
        foreach (var raw in text.Split('\n'))
        {
            n++;
            var line = raw.Trim('\r', ' ', '\t');
            var hash = line.IndexOf('#');
            if (hash >= 0) line = line[..hash].Trim();
            if (line.Length == 0) continue;

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) { problems.Add($"Line {n}: needs an IP address and at least one host name."); continue; }
            if (!IPAddress.TryParse(parts[0], out _)) problems.Add($"Line {n}: '{parts[0]}' is not a valid IP address.");
            foreach (var h in parts.Skip(1))
                if (!Hostname.IsMatch(h)) problems.Add($"Line {n}: '{h}' is not a valid host name.");
        }
        return problems;
    }

    /// <summary>Line-based diff for the confirmation step.</summary>
    public static (List<string> Added, List<string> Removed) Diff(string before, string after)
    {
        static List<string> Lines(string s) => s.Replace("\r", "").Split('\n').ToList();
        var a = Lines(before); var b = Lines(after);
        var removed = a.Except(b).Where(l => l.Trim().Length > 0).ToList();
        var added = b.Except(a).Where(l => l.Trim().Length > 0).ToList();
        return (added, removed);
    }

    public static string AddEntries(string text, IEnumerable<string> hosts, string ip = "0.0.0.0", string marker = "# Lumen Optimizer")
    {
        var existing = new HashSet<string>(
            text.Replace("\r", "").Split('\n')
                .Select(l => l.Split('#')[0].Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                .Where(p => p.Length >= 2).SelectMany(p => p.Skip(1)), StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder(text.TrimEnd('\r', '\n'));
        var any = false;
        foreach (var h in hosts.Where(h => !existing.Contains(h)))
        {
            if (!any) { sb.Append("\r\n\r\n").Append(marker).Append(" - blocked hosts"); any = true; }
            sb.Append("\r\n").Append(ip).Append(' ').Append(h);
        }
        return sb.Append("\r\n").ToString();
    }

    /// <summary>Backs up the current file, then writes the new text. Returns the backup path.</summary>
    public static string Save(string text)
    {
        var problems = Validate(text);
        if (problems.Count > 0) throw new InvalidOperationException(string.Join("\n", problems.Take(5)));

        Directory.CreateDirectory(BackupDir);
        var backup = System.IO.Path.Combine(BackupDir, $"hosts-{DateTime.Now:yyyyMMdd-HHmmss}.bak");
        if (File.Exists(Path)) File.Copy(Path, backup, overwrite: true);

        Log.Info($"Hosts: saving {Path} (backup {backup})");
        var attrs = File.Exists(Path) ? File.GetAttributes(Path) : FileAttributes.Normal;
        if ((attrs & FileAttributes.ReadOnly) != 0) File.SetAttributes(Path, attrs & ~FileAttributes.ReadOnly);
        File.WriteAllText(Path, text, new UTF8Encoding(false));
        Shell.Run("ipconfig.exe", "/flushdns");
        return backup;
    }

    public static List<string> Backups() =>
        Directory.Exists(BackupDir)
            ? Directory.GetFiles(BackupDir, "hosts-*.bak").OrderByDescending(f => f).ToList()
            : new List<string>();
}
