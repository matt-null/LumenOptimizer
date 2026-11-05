using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LumenOptimizer.Core.Models;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Actions;

internal static class AppxCache
{
    private static readonly object Gate = new();
    private static HashSet<string>? _names;

    /// <summary>Names of all installed Appx packages (all users), or null if the query failed.</summary>
    public static HashSet<string>? Names
    {
        get
        {
            lock (Gate)
            {
                if (_names is null)
                {
                    var (code, output) = Shell.PowerShell(
                        "Get-AppxPackage -AllUsers | Select-Object -ExpandProperty Name", logOutput: false);
                    if (code == 0)
                    {
                        _names = new HashSet<string>(
                            output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                            StringComparer.OrdinalIgnoreCase);
                    }
                }
                return _names;
            }
        }
    }

    public static void Invalidate()
    {
        lock (Gate) _names = null;
    }
}

/// <summary>Removes Microsoft Store (Appx/MSIX) apps for all users and de-provisions them for new users. Not auto-revertible.</summary>
internal sealed class AppxHandler : IActionHandler
{
    private static readonly Regex SafeName = new(@"^[A-Za-z0-9_.\-]+$", RegexOptions.Compiled);

    public string Type => "appx";

    private static string ValidName(TweakAction a)
    {
        var name = a.Name ?? "";
        if (!SafeName.IsMatch(name)) throw new InvalidOperationException($"Invalid package name '{name}'.");
        return name;
    }

    public bool? IsApplied(TweakAction a)
    {
        var names = AppxCache.Names;
        return names is null ? null : !names.Contains(ValidName(a));
    }

    public JsonObject? Capture(TweakAction a) => null;

    public void Apply(TweakAction a)
    {
        var name = ValidName(a);
        var script = $@"
$ErrorActionPreference = 'Continue'
Get-AppxPackage -AllUsers -Name '{name}' | Remove-AppxPackage -AllUsers
Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -eq '{name}' }} | Remove-AppxProvisionedPackage -Online | Out-Null
exit 0";
        var (code, output) = Shell.PowerShell(script);
        AppxCache.Invalidate();
        if (code != 0) throw new InvalidOperationException($"Removing {name} failed: {output}");
    }

    public bool CanRevert(TweakAction a) => false;

    public void Restore(TweakAction a, JsonObject? snapshot) =>
        throw new NotSupportedException("Removed apps must be reinstalled from the Microsoft Store.");
}
