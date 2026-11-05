using System.Text.Json.Nodes;
using System.Xml.Linq;
using LumenOptimizer.Core.Models;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Actions;

/// <summary>Enable/disable scheduled tasks via schtasks.exe. Uses /XML so detection works on any Windows language.</summary>
internal sealed class ScheduledTaskHandler : IActionHandler
{
    public string Type => "task";

    private static string ValidPath(TweakAction a)
    {
        var path = a.Path ?? "";
        if (!path.StartsWith('\\') || path.Contains('"'))
            throw new InvalidOperationException($"Invalid task path '{path}'.");
        return path;
    }

    private static (bool Exists, bool? Enabled) Query(string path)
    {
        var (code, output) = Shell.Run("schtasks.exe", $"/Query /TN \"{path}\" /XML", logOutput: false);
        if (code != 0) return (false, null);

        try
        {
            var start = output.IndexOf('<');
            var doc = XDocument.Parse(start > 0 ? output[start..] : output);
            var settings = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Settings");
            var enabled = settings?.Elements().FirstOrDefault(e => e.Name.LocalName == "Enabled");
            var isEnabled = !(enabled is not null && enabled.Value.Trim().Equals("false", StringComparison.OrdinalIgnoreCase));
            return (true, isEnabled);
        }
        catch
        {
            return (true, null);
        }
    }

    public bool? IsApplied(TweakAction a)
    {
        var (exists, enabled) = Query(ValidPath(a));
        if (!exists || enabled is null) return null;
        return !enabled.Value;
    }

    public JsonObject? Capture(TweakAction a)
    {
        var (exists, enabled) = Query(ValidPath(a));
        return new JsonObject { ["exists"] = exists, ["enabled"] = enabled };
    }

    public void Apply(TweakAction a)
    {
        var path = ValidPath(a);
        if (!Query(path).Exists) return;

        var (code, output) = Shell.Run("schtasks.exe", $"/Change /TN \"{path}\" /DISABLE");
        if (code != 0) throw new InvalidOperationException($"Could not disable task {path}: {output}");
    }

    public bool CanRevert(TweakAction a) => true;

    public void Restore(TweakAction a, JsonObject? snapshot)
    {
        if (snapshot is null)
            throw new InvalidOperationException($"No saved original state for task '{a.Path}'.");
        if (snapshot["exists"]?.GetValue<bool>() != true) return;
        if (snapshot["enabled"]?.GetValue<bool>() != true) return; // it was already disabled

        var path = ValidPath(a);
        var (code, output) = Shell.Run("schtasks.exe", $"/Change /TN \"{path}\" /ENABLE");
        if (code != 0) throw new InvalidOperationException($"Could not re-enable task {path}: {output}");
    }
}
