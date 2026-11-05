using System.Text.Json.Nodes;
using LumenOptimizer.Core.Models;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Actions;

/// <summary>Escape hatch for things that are not just a registry value: runs PowerShell from the (trusted, embedded) catalog.</summary>
internal sealed class CommandHandler : IActionHandler
{
    public string Type => "command";

    public bool? IsApplied(TweakAction a)
    {
        if (string.IsNullOrWhiteSpace(a.DetectCommand)) return null;
        var (code, _) = Shell.PowerShell(a.DetectCommand, logOutput: false);
        return code == 0;
    }

    public JsonObject? Capture(TweakAction a) => null;

    public void Apply(TweakAction a)
    {
        if (string.IsNullOrWhiteSpace(a.ApplyCommand))
            throw new InvalidOperationException("Command action has no ApplyCommand.");
        var (code, output) = Shell.PowerShell(a.ApplyCommand);
        if (code != 0) throw new InvalidOperationException($"Command failed (exit {code}): {output}");
    }

    public bool CanRevert(TweakAction a) => !string.IsNullOrWhiteSpace(a.RevertCommand);

    public void Restore(TweakAction a, JsonObject? snapshot)
    {
        if (string.IsNullOrWhiteSpace(a.RevertCommand))
            throw new NotSupportedException("This command has no revert step.");
        var (code, output) = Shell.PowerShell(a.RevertCommand);
        if (code != 0) throw new InvalidOperationException($"Revert command failed (exit {code}): {output}");
    }
}
