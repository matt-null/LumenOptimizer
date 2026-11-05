using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LumenOptimizer.Core.Models;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Actions;

internal sealed class PowerPlanHandler : IActionHandler
{
    private const string GuidPattern = @"[0-9a-fA-F]{8}-(?:[0-9a-fA-F]{4}-){3}[0-9a-fA-F]{12}";
    private static readonly Regex GuidOnly = new($"^{GuidPattern}$", RegexOptions.Compiled);
    private static readonly Regex AnyGuid = new(GuidPattern, RegexOptions.Compiled);

    public string Type => "powerplan";

    private static string ValidGuid(TweakAction a)
    {
        var guid = a.Guid ?? "";
        if (!GuidOnly.IsMatch(guid)) throw new InvalidOperationException($"Invalid power scheme GUID '{guid}'.");
        return guid;
    }

    private static (string? Guid, string Line) Active()
    {
        var (code, output) = Shell.Run("powercfg.exe", "/getactivescheme", logOutput: false);
        if (code != 0) return (null, output);
        var m = AnyGuid.Match(output);
        return (m.Success ? m.Value : null, output);
    }

    public bool? IsApplied(TweakAction a)
    {
        var (guid, line) = Active();
        if (guid is null) return null;
        if (guid.Equals(ValidGuid(a), StringComparison.OrdinalIgnoreCase)) return true;
        // A duplicated scheme (e.g. Ultimate Performance) gets a new GUID, so also compare the display name.
        return !string.IsNullOrEmpty(a.Name) && line.Contains($"({a.Name})", StringComparison.OrdinalIgnoreCase);
    }

    public JsonObject? Capture(TweakAction a)
    {
        var (guid, _) = Active();
        return new JsonObject { ["guid"] = guid };
    }

    private static void SetActive(string guid)
    {
        var (code, output) = Shell.Run("powercfg.exe", $"/setactive {guid}");
        if (code != 0) throw new InvalidOperationException($"powercfg /setactive failed: {output}");
    }

    public void Apply(TweakAction a)
    {
        var guid = ValidGuid(a);

        // 1) scheme already installed under its own GUID?
        var (code, _) = Shell.Run("powercfg.exe", $"/setactive {guid}");
        if (code == 0) return;

        // 2) an earlier duplicate with the same display name exists?
        var (_, list) = Shell.Run("powercfg.exe", "/list", logOutput: false);
        if (!string.IsNullOrEmpty(a.Name))
        {
            foreach (var line in list.Split('\n'))
            {
                if (line.Contains($"({a.Name})", StringComparison.OrdinalIgnoreCase))
                {
                    var existing = AnyGuid.Match(line);
                    if (existing.Success) { SetActive(existing.Value); return; }
                }
            }
        }

        // 3) create it from the built-in template
        var (dupCode, dupOut) = Shell.Run("powercfg.exe", $"/duplicatescheme {guid}");
        var created = AnyGuid.Match(dupOut);
        if (dupCode != 0 || !created.Success)
            throw new InvalidOperationException($"This power plan is not available on this PC: {dupOut}");
        SetActive(created.Value);
    }

    public bool CanRevert(TweakAction a) => true;

    public void Restore(TweakAction a, JsonObject? snapshot)
    {
        var original = snapshot?["guid"]?.GetValue<string>();
        if (string.IsNullOrEmpty(original))
            throw new InvalidOperationException("No saved original power plan.");
        SetActive(original);
    }
}
