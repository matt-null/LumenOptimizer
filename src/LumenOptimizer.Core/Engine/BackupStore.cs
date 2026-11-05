using System.Text.Json;
using System.Text.Json.Nodes;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Engine;

/// <summary>What the machine looked like before a tweak was applied (one snapshot per action, same order).</summary>
public sealed class TweakBackup
{
    public DateTime AppliedUtc { get; set; }
    public List<JsonObject?> Actions { get; set; } = new();
}

/// <summary>Persists original values to %ProgramData%\LumenOptimizer\backups.json so every tweak can be undone.</summary>
public sealed class BackupStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly object _gate = new();
    private readonly string _file;
    private Dictionary<string, TweakBackup> _data;

    public BackupStore(string? file = null)
    {
        _file = file ?? Path.Combine(AppPaths.DataDir, "backups.json");
        MigrateLegacyBackups();
        _data = Load();
    }

    /// <summary>Ids of tweaks that Lumen Optimizer has applied and can undo.</summary>
    public IReadOnlyList<string> Ids
    {
        get { lock (_gate) return _data.Keys.ToList(); }
    }

    public TweakBackup? Get(string tweakId)
    {
        lock (_gate) return _data.TryGetValue(tweakId, out var b) ? b : null;
    }

    public void Set(string tweakId, TweakBackup backup)
    {
        lock (_gate)
        {
            _data[tweakId] = backup;
            Save();
        }
    }

    public void Remove(string tweakId)
    {
        lock (_gate)
        {
            if (_data.Remove(tweakId)) Save();
        }
    }

    /// <summary>One-time copy of backups made under the old app name, so earlier changes can still be undone.</summary>
    private void MigrateLegacyBackups()
    {
        try
        {
            var legacy = Path.Combine(AppPaths.LegacyDataDir, "backups.json");
            if (File.Exists(_file) || !File.Exists(legacy)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            File.Copy(legacy, _file);
            Log.Info("Copied backups from the previous app name.");
        }
        catch (Exception ex)
        {
            Log.Warn("Could not migrate old backups: " + ex.Message);
        }
    }

    private Dictionary<string, TweakBackup> Load()
    {
        try
        {
            if (!File.Exists(_file)) return new();
            var json = File.ReadAllText(_file);
            return JsonSerializer.Deserialize<Dictionary<string, TweakBackup>>(json, Options) ?? new();
        }
        catch (Exception ex)
        {
            Log.Error($"Backup file unreadable, starting fresh: {ex.Message}");
            try { File.Move(_file, _file + ".corrupt-" + DateTime.Now.ToString("yyyyMMddHHmmss")); } catch { /* ignore */ }
            return new();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
        var tmp = _file + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_data, Options));
        File.Move(tmp, _file, overwrite: true);
    }
}
