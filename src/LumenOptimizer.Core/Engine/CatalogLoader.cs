using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using LumenOptimizer.Core.Models;

namespace LumenOptimizer.Core.Engine;

public static class CatalogLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly string[] KnownTags = { "laptop", "desktop", "ssd", "hdd", "flash", "nvidia", "amd-gpu", "intel-gpu" };

    public static IReadOnlyList<Tweak> LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("tweaks.json")
            ?? throw new InvalidOperationException("Embedded tweak catalog 'tweaks.json' is missing.");
        return Parse(stream);
    }

    public static IReadOnlyList<Tweak> Parse(Stream stream)
    {
        var file = JsonSerializer.Deserialize<CatalogFile>(stream, Options)
            ?? throw new InvalidOperationException("Tweak catalog is empty.");
        return file.Tweaks;
    }

    /// <summary>Returns a list of problems in the catalog (empty = valid).</summary>
    public static List<string> Validate(IEnumerable<Tweak> tweaks)
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in tweaks)
        {
            if (string.IsNullOrWhiteSpace(t.Id)) { errors.Add("A tweak has no Id."); continue; }
            if (!ids.Add(t.Id)) errors.Add($"{t.Id}: duplicate Id.");
            if (string.IsNullOrWhiteSpace(t.Name)) errors.Add($"{t.Id}: missing Name.");
            if (string.IsNullOrWhiteSpace(t.Category)) errors.Add($"{t.Id}: missing Category.");
            if (t.Actions.Count == 0) errors.Add($"{t.Id}: no actions.");
            if (t.Risk == RiskLevel.Aggressive && string.IsNullOrWhiteSpace(t.Warning))
                errors.Add($"{t.Id}: aggressive tweaks must have a Warning.");

            if (t.MaxBuild > 0 && t.MaxBuild < t.MinBuild) errors.Add($"{t.Id}: MaxBuild is lower than MinBuild.");
            foreach (var tag in t.Requires.Concat(t.AvoidOn).Concat(t.FitsOn))
                if (!KnownTags.Contains(tag, StringComparer.OrdinalIgnoreCase)) errors.Add($"{t.Id}: unknown hardware tag '{tag}'.");
            if (t.AvoidOn.Count > 0 && string.IsNullOrWhiteSpace(t.AvoidNote)) errors.Add($"{t.Id}: AvoidOn needs an AvoidNote.");

            foreach (var a in t.Actions)
            {
                if (!TweakEngine.KnownActionTypes.Contains(a.Type, StringComparer.OrdinalIgnoreCase))
                {
                    errors.Add($"{t.Id}: unknown action type '{a.Type}'.");
                    continue;
                }

                switch (a.Type.ToLowerInvariant())
                {
                    case "registry":
                        if (string.IsNullOrEmpty(a.Hive) || string.IsNullOrEmpty(a.Path)) errors.Add($"{t.Id}: registry action needs Hive and Path.");
                        if (!a.Delete && a.Value is null) errors.Add($"{t.Id}: registry action needs Value (or Delete).");
                        break;
                    case "service":
                        if (string.IsNullOrEmpty(a.Name) || string.IsNullOrEmpty(a.StartType)) errors.Add($"{t.Id}: service action needs Name and StartType.");
                        break;
                    case "task":
                        if (string.IsNullOrEmpty(a.Path)) errors.Add($"{t.Id}: task action needs Path.");
                        break;
                    case "appx":
                        if (string.IsNullOrEmpty(a.Name)) errors.Add($"{t.Id}: appx action needs Name.");
                        break;
                    case "powerplan":
                        if (string.IsNullOrEmpty(a.Guid)) errors.Add($"{t.Id}: powerplan action needs Guid.");
                        break;
                    case "command":
                        if (string.IsNullOrEmpty(a.ApplyCommand)) errors.Add($"{t.Id}: command action needs ApplyCommand.");
                        break;
                }
            }
        }

        return errors;
    }
}
