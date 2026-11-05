using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using LumenOptimizer.Core.Models;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Actions;

/// <summary>Service start type. Reads the registry, changes via sc.exe (goes through the Service Control Manager).</summary>
internal sealed class ServiceHandler : IActionHandler
{
    private static readonly Regex SafeName = new(@"^[A-Za-z0-9_.\-]+$", RegexOptions.Compiled);

    public string Type => "service";

    private static string ValidName(TweakAction a)
    {
        var name = a.Name ?? "";
        if (!SafeName.IsMatch(name)) throw new InvalidOperationException($"Invalid service name '{name}'.");
        return name;
    }

    private static int? ReadStart(string name)
    {
        using var key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{name}", false);
        return key?.GetValue("Start") is int i ? i : null;
    }

    private static int Target(TweakAction a) => (a.StartType ?? "").ToLowerInvariant() switch
    {
        "disabled" => 4,
        "manual" => 3,
        "automatic" => 2,
        _ => throw new InvalidOperationException($"Unsupported StartType '{a.StartType}'.")
    };

    private static string ScArg(int start) => start switch
    {
        2 => "auto",
        3 => "demand",
        4 => "disabled",
        _ => throw new InvalidOperationException($"Start type {start} cannot be restored with sc.exe.")
    };

    private static void SetStart(string name, int start)
    {
        var (code, output) = Shell.Run("sc.exe", $"config {name} start= {ScArg(start)}");
        if (code != 0) throw new InvalidOperationException($"sc config {name} failed: {output}");
    }

    public bool? IsApplied(TweakAction a)
    {
        var current = ReadStart(ValidName(a));
        return current is null ? null : current == Target(a);
    }

    public JsonObject? Capture(TweakAction a)
    {
        var current = ReadStart(ValidName(a));
        return new JsonObject { ["exists"] = current is not null, ["start"] = current };
    }

    public void Apply(TweakAction a)
    {
        var name = ValidName(a);
        if (ReadStart(name) is null) return; // service not present on this PC

        var target = Target(a);
        SetStart(name, target);
        if (target == 4) Shell.Run("sc.exe", $"stop {name}"); // best effort
    }

    public bool CanRevert(TweakAction a) => true;

    public void Restore(TweakAction a, JsonObject? snapshot)
    {
        if (snapshot is null)
            throw new InvalidOperationException($"No saved original start type for service '{a.Name}'.");
        if (snapshot["exists"]?.GetValue<bool>() != true) return;

        var name = ValidName(a);
        var original = snapshot["start"]!.GetValue<int>();
        SetStart(name, original);
        if (original == 2) Shell.Run("sc.exe", $"start {name}"); // best effort
    }
}
