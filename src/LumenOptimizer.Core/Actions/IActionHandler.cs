using System.Text.Json.Nodes;
using LumenOptimizer.Core.Models;

namespace LumenOptimizer.Core.Actions;

/// <summary>Knows how to detect, capture, apply and restore one kind of action.</summary>
internal interface IActionHandler
{
    string Type { get; }

    /// <summary>true = applied, false = not applied, null = cannot tell / not present on this PC.</summary>
    bool? IsApplied(TweakAction action);

    /// <summary>Snapshot of the current (original) state, stored before applying.</summary>
    JsonObject? Capture(TweakAction action);

    void Apply(TweakAction action);

    bool CanRevert(TweakAction action);

    void Restore(TweakAction action, JsonObject? snapshot);
}
