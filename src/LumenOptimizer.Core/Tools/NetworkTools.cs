using System.Text.Json;
using System.Text.RegularExpressions;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

public sealed record DnsProvider(string Name, string[] Servers, string Note);

/// <summary>Network repair and diagnostic actions. DNS changes are saved first so they can be undone.</summary>
public static class NetworkTools
{
    public static IReadOnlyList<DnsProvider> DnsProviders { get; } = new[]
    {
        new DnsProvider("Cloudflare", new[] { "1.1.1.1", "1.0.0.1" }, "Fast, privacy-focused."),
        new DnsProvider("Google", new[] { "8.8.8.8", "8.8.4.4" }, "Fast and widely used."),
        new DnsProvider("Quad9", new[] { "9.9.9.9", "149.112.112.112" }, "Blocks known malicious domains."),
    };

    private static string BackupFile => Path.Combine(AppPaths.DataDir, "dns-backup.json");

    private static readonly Regex SafeHost = new(@"^[A-Za-z0-9]([A-Za-z0-9\-.]*[A-Za-z0-9])?$|^[0-9a-fA-F:.]+$", RegexOptions.Compiled);

    public static bool IsValidHost(string host) => host.Length is > 0 and <= 253 && SafeHost.IsMatch(host);

    // ------------------------------------------------------------- repair

    public static string FlushDns() => Shell.Run("ipconfig.exe", "/flushdns").Output;
    public static string ReleaseRenew()
    {
        var a = Shell.Run("ipconfig.exe", "/release").Output;
        var b = Shell.Run("ipconfig.exe", "/renew").Output;
        return a + "\n" + b;
    }

    /// <summary>Needs a restart to finish.</summary>
    public static string ResetWinsock() => Shell.Run("netsh.exe", "winsock reset").Output;

    /// <summary>Needs a restart to finish.</summary>
    public static string ResetTcpIp() =>
        Shell.Run("netsh.exe", "int ip reset").Output + "\n" + Shell.Run("netsh.exe", "int ipv6 reset").Output;

    // -------------------------------------------------------- diagnostics

    public static string IpConfig() => Shell.Run("ipconfig.exe", "/all", logOutput: false).Output;

    public static string Ping(string host) => IsValidHost(host)
        ? Shell.Run("ping.exe", $"-n 4 {host}", 30_000).Output : "Not a valid host name or address.";

    public static string Trace(string host) => IsValidHost(host)
        ? Shell.Run("tracert.exe", $"-d -h 20 {host}", 120_000).Output : "Not a valid host name or address.";

    public static string Lookup(string host) => IsValidHost(host)
        ? Shell.Run("nslookup.exe", host, 20_000).Output : "Not a valid host name or address.";

    // ---------------------------------------------------------------- DNS

    private sealed record Adapter(int Index, string Alias, string[] Servers);

    private static List<Adapter> Current()
    {
        const string script = @"
Get-NetAdapter | Where-Object Status -eq 'Up' | ForEach-Object {
  $d = Get-DnsClientServerAddress -InterfaceIndex $_.ifIndex -AddressFamily IPv4
  [pscustomobject]@{ Index = $_.ifIndex; Alias = $_.Name; Servers = @($d.ServerAddresses) }
} | ConvertTo-Json -Compress";
        var (code, output) = Shell.PowerShell(script, 60_000, logOutput: false);
        if (code != 0) throw new InvalidOperationException("Could not read network adapters: " + output);
        var start = output.IndexOfAny(new[] { '[', '{' });
        if (start < 0) return new List<Adapter>();
        using var doc = JsonDocument.Parse(output[start..]);
        var items = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList()
            : new List<JsonElement> { doc.RootElement };
        return items.Select(e => new Adapter(
            e.GetProperty("Index").GetInt32(),
            e.GetProperty("Alias").GetString() ?? "",
            e.TryGetProperty("Servers", out var s) && s.ValueKind == JsonValueKind.Array
                ? s.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray()
                : (e.TryGetProperty("Servers", out var one) && one.ValueKind == JsonValueKind.String ? new[] { one.GetString()! } : Array.Empty<string>()))).ToList();
    }

    /// <summary>Text for the confirmation dialog: each active adapter and its current DNS.</summary>
    public static string DescribeCurrent()
    {
        var a = Current();
        return a.Count == 0 ? "No active network adapters found."
            : string.Join("\n", a.Select(x => $"{x.Alias}: {(x.Servers.Length == 0 ? "automatic (DHCP)" : string.Join(", ", x.Servers))}"));
    }

    public static bool HasDnsBackup => File.Exists(BackupFile);

    /// <summary>Saves the current DNS of every active adapter (only the first time, so the true original survives), then applies the provider.</summary>
    public static void SetDns(DnsProvider provider)
    {
        var adapters = Current();
        if (adapters.Count == 0) throw new InvalidOperationException("No active network adapters found.");

        if (!HasDnsBackup)
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.WriteAllText(BackupFile, JsonSerializer.Serialize(adapters.Select(a => new { a.Index, a.Alias, a.Servers })));
            Log.Info("DNS: saved original settings to " + BackupFile);
        }

        var list = string.Join(",", provider.Servers.Select(s => $"'{s}'"));
        foreach (var a in adapters)
        {
            var (code, output) = Shell.PowerShell($"Set-DnsClientServerAddress -InterfaceIndex {a.Index} -ServerAddresses @({list}) -ErrorAction Stop", 60_000);
            if (code != 0) throw new InvalidOperationException($"{a.Alias}: {output}");
        }
        FlushDns();
    }

    /// <summary>Puts back the DNS settings saved before the first change.</summary>
    public static void RestoreDns()
    {
        if (!HasDnsBackup) throw new InvalidOperationException("There is no saved DNS setting to restore.");
        using var doc = JsonDocument.Parse(File.ReadAllText(BackupFile));
        foreach (var e in doc.RootElement.EnumerateArray())
        {
            var index = e.GetProperty("Index").GetInt32();
            var servers = e.GetProperty("Servers").EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray();
            var script = servers.Length == 0
                ? $"Set-DnsClientServerAddress -InterfaceIndex {index} -ResetServerAddresses -ErrorAction Stop"
                : $"Set-DnsClientServerAddress -InterfaceIndex {index} -ServerAddresses @({string.Join(",", servers.Select(s => $"'{s}'"))}) -ErrorAction Stop";
            var (code, output) = Shell.PowerShell(script, 60_000);
            if (code != 0) throw new InvalidOperationException(output);
        }
        File.Delete(BackupFile);
        FlushDns();
    }
}
