using LumenOptimizer.Core.Actions;
using LumenOptimizer.Core.Models;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Engine;

public sealed class TweakEngine
{
    private readonly BackupStore _backups;
    private readonly Dictionary<string, IActionHandler> _handlers;

    public TweakEngine(BackupStore backups)
    {
        _backups = backups;
        _handlers = new IActionHandler[]
        {
            new RegistryHandler(),
            new ServiceHandler(),
            new ScheduledTaskHandler(),
            new AppxHandler(),
            new PowerPlanHandler(),
            new CommandHandler()
        }.ToDictionary(h => h.Type, StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyCollection<string> KnownActionTypes { get; } =
        new[] { "registry", "service", "task", "appx", "powerplan", "command" };

    public static int WindowsBuild => Environment.OSVersion.Version.Build;

    /// <summary>Hardware and Windows details. Detect() once at startup; null until then (hardware rules are skipped).</summary>
    public SystemProfile? Profile { get; set; }

    private IActionHandler Handler(TweakAction a) =>
        _handlers.TryGetValue(a.Type, out var h) ? h : throw new InvalidOperationException($"Unknown action type '{a.Type}'.");

    public bool IsApplicable(Tweak t)
    {
        if (t.MinBuild > WindowsBuild) return false;
        if (t.MaxBuild > 0 && WindowsBuild > t.MaxBuild) return false;
        if (t.Requires.Count > 0 && Profile is { } p)
        {
            var tags = p.Tags;
            if (!t.Requires.All(tags.Contains)) return false;
        }
        return true;
    }

    /// <summary>Why this tweak is a bad idea on this PC (from AvoidOn), or null.</summary>
    public string? HardwareWarning(Tweak t)
    {
        if (t.AvoidOn.Count == 0 || Profile is null) return null;
        var tags = Profile.Tags;
        var hit = t.AvoidOn.FirstOrDefault(tags.Contains);
        if (hit is null) return null;
        return t.AvoidNote ?? $"Not recommended on this PC ({hit}).";
    }

    /// <summary>Why this tweak suits this PC (from FitsOn), or null.</summary>
    public string? FitHint(Tweak t)
    {
        if (t.FitsOn.Count == 0 || Profile is null) return null;
        var tags = Profile.Tags;
        return t.FitsOn.Any(tags.Contains) ? (t.FitNote ?? "Suits this PC.") : null;
    }

    private static string Describe(TweakAction a) => a.Type.ToLowerInvariant() switch
    {
        "registry" => a.Delete
            ? $"registry delete {a.Hive}\\{a.Path}\\{a.Name}"
            : $"registry set {a.Hive}\\{a.Path}\\{a.Name} = {a.Value} ({a.Kind})",
        "service" => $"service {a.Name} start type -> {a.StartType}",
        "task" => $"scheduled task disable {a.Path}",
        "appx" => $"remove app {a.Name}",
        "powerplan" => $"power plan -> {a.Name ?? a.Guid}",
        "command" => "run PowerShell command",
        _ => a.Type
    };

    public bool CanRevert(Tweak t)
    {
        try { return t.Actions.All(a => Handler(a).CanRevert(a)); }
        catch { return false; }
    }

    /// <summary>Re-detects the current state of a tweak from the live system.</summary>
    public TweakState GetState(Tweak t)
    {
        if (!IsApplicable(t)) return TweakState.NotApplicable;

        int on = 0, off = 0;
        foreach (var a in t.Actions)
        {
            bool? applied;
            try { applied = Handler(a).IsApplied(a); }
            catch (Exception ex)
            {
                Log.Warn($"Detect failed for {t.Id}: {ex.Message}");
                applied = null;
            }

            if (applied == true) on++;
            else if (applied == false) off++;
        }

        if (on == 0 && off == 0) return TweakState.Unknown;
        if (off == 0) return TweakState.Applied;
        if (on == 0) return TweakState.NotApplied;
        return TweakState.Partial;
    }

    public TweakResult Apply(Tweak t)
    {
        var result = new TweakResult();
        Log.Info($"Applying \"{t.Name}\" ({t.Id})");

        // Snapshot originals once. Re-applying must never overwrite the true original with an already-tweaked value.
        try
        {
            if (_backups.Get(t.Id) is null)
            {
                var snapshots = new List<System.Text.Json.Nodes.JsonObject?>();
                foreach (var a in t.Actions)
                {
                    try { snapshots.Add(Handler(a).Capture(a)); }
                    catch (Exception ex)
                    {
                        Log.Warn($"Capture failed for {t.Id}: {ex.Message}");
                        snapshots.Add(null);
                    }
                }
                _backups.Set(t.Id, new TweakBackup { AppliedUtc = DateTime.UtcNow, Actions = snapshots });
            }
        }
        catch (Exception ex)
        {
            // Without a backup we refuse to change anything.
            result.Errors.Add($"Could not save a backup, nothing was changed: {ex.Message}");
            return result;
        }

        foreach (var a in t.Actions)
        {
            try { Log.Info("  " + Describe(a)); Handler(a).Apply(a); }
            catch (Exception ex)
            {
                Log.Error($"Apply failed for {t.Id}: {ex.Message}");
                result.Errors.Add(ex.Message);
            }
        }

        return result;
    }

    public TweakResult Revert(Tweak t)
    {
        var result = new TweakResult();
        Log.Info($"Reverting \"{t.Name}\" ({t.Id})");

        if (!CanRevert(t))
        {
            result.Errors.Add("This tweak cannot be undone automatically.");
            return result;
        }

        var backup = _backups.Get(t.Id);
        for (var i = 0; i < t.Actions.Count; i++)
        {
            var a = t.Actions[i];
            var snapshot = backup is not null && i < backup.Actions.Count ? backup.Actions[i] : null;
            try { Log.Info("  restore: " + Describe(a)); Handler(a).Restore(a, snapshot); }
            catch (Exception ex)
            {
                Log.Error($"Revert failed for {t.Id}: {ex.Message}");
                result.Errors.Add(ex.Message);
            }
        }

        if (result.Success) _backups.Remove(t.Id);
        return result;
    }
}
