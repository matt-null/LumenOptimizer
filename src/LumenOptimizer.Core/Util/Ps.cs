using System.Text.Json;

namespace LumenOptimizer.Core.Util;

public static class Ps
{
    /// <summary>Runs a script that ends with: ConvertTo-Json -InputObject @(...) -Compress. Always returns an array.</summary>
    public static List<JsonElement> Json(string script, int timeoutMs = 120_000)
    {
        var (code, output) = Shell.PowerShell(script, timeoutMs, logOutput: false);
        var list = new List<JsonElement>();
        if (code != 0 || string.IsNullOrWhiteSpace(output)) return list;
        try
        {
            var start = output.IndexOfAny(new[] { '[', '{' });
            using var doc = JsonDocument.Parse(output[start..]);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                foreach (var e in doc.RootElement.EnumerateArray()) list.Add(e.Clone());
            else list.Add(doc.RootElement.Clone());
        }
        catch (Exception ex) { Log.Warn("Could not read PowerShell output: " + ex.Message); }
        return list;
    }

    public static string S(this JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "";

    public static string Q(string s) => s.Replace("'", "''");
}
