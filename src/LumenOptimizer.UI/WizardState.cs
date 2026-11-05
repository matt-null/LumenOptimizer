using LumenOptimizer.Core.Models;
using LumenOptimizer.Core.Util;
using LumenOptimizer.UI.ViewModels;

namespace LumenOptimizer.UI;

public sealed record ApplyOutcome(int Applied, int Failed, bool NeedsRestart, IReadOnlyList<string> Errors);

/// <summary>Everything the wizard pages share: the tweak list, what is selected, and the result of applying.</summary>
public static class WizardState
{
    /// <summary>Wizard steps, in order, between the preset page and the review page.</summary>
    public static IReadOnlyList<string> Categories { get; } =
        new[] { "Privacy", "Edge", "Performance", "Debloat", "Services", "Appearance", "Advanced" };

    public static List<TweakItemViewModel> All { get; } = new();

    public static bool Scanned { get; set; }

    /// <summary>Windows version, edition, laptop/desktop, drive type and GPU. Set during the scan.</summary>
    public static SystemProfile? Profile { get; private set; }

    /// <summary>The "show the console?" question is asked once per run.</summary>
    public static bool ConsoleAsked { get; set; }
    public static bool CreateRestorePoint { get; set; } = true;
    public static ApplyOutcome? LastOutcome { get; set; }

    public static IEnumerable<TweakItemViewModel> Selected => All.Where(i => i.IsSelected && i.CanSelect);

    /// <summary>Detects the current state of every tweak. Call from the UI thread.</summary>
    public static async Task ScanAsync(Action<int, int>? progress = null)
    {
        if (Profile is null)
        {
            Profile = await Task.Run(SystemProfile.Detect);
            AppServices.Engine.Profile = Profile;
        }

        if (All.Count == 0)
        {
            foreach (var t in AppServices.Catalog)
                All.Add(new TweakItemViewModel(t));
        }

        var total = All.Count;
        var done = 0;
        foreach (var item in All)
        {
            var tweak = item.Tweak;
            item.State = await Task.Run(() => AppServices.Engine.GetState(tweak));
            item.IsSelected = false;
            progress?.Invoke(++done, total);
        }

        Scanned = true;
        LastOutcome = null;
    }

    /// <summary>The selection as a small JSON file so it can be reused on another PC.</summary>
    public static string ExportSelection() =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            app = "LumenOptimizer",
            version = 1,
            saved = DateTime.Now.ToString("s"),
            tweaks = Selected.Select(i => i.Tweak.Id).ToArray()
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

    /// <summary>Selects the tweaks named in a saved file. Returns how many were selected and how many were skipped (not applicable here, already applied, or unknown).</summary>
    public static (int Selected, int Skipped) ImportSelection(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("tweaks", out var arr) || arr.ValueKind != System.Text.Json.JsonValueKind.Array)
            throw new InvalidOperationException("This is not a Lumen Optimizer selection file.");

        var ids = new HashSet<string>(arr.EnumerateArray().Select(e => e.GetString() ?? ""), StringComparer.OrdinalIgnoreCase);
        int chosen = 0, skipped = 0;
        foreach (var i in All)
            i.IsSelected = false;
        foreach (var id in ids)
        {
            var item = All.FirstOrDefault(i => i.Tweak.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (item is null || !item.CanSelect) { skipped++; continue; }
            item.IsSelected = true;
            chosen++;
        }
        return (chosen, skipped);
    }
}
