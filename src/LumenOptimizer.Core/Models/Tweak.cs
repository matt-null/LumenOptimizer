namespace LumenOptimizer.Core.Models;

public enum RiskLevel
{
    Safe,
    Moderate,
    Aggressive
}

public enum TweakState
{
    Unknown,
    NotApplied,
    Applied,
    Partial,
    NotApplicable
}

/// <summary>One user-visible tweak made of one or more low-level actions.</summary>
public sealed class Tweak
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public RiskLevel Risk { get; set; } = RiskLevel.Safe;

    /// <summary>Shown in the confirmation dialog before the tweak is applied.</summary>
    public string? Warning { get; set; }

    public bool RequiresReboot { get; set; }

    /// <summary>Minimum Windows build number (e.g. 22000 = Windows 11). 0 = any.</summary>
    public int MinBuild { get; set; }

    /// <summary>Highest Windows build this tweak applies to (e.g. 21999 = Windows 10 only). 0 = no limit.</summary>
    public int MaxBuild { get; set; }

    /// <summary>
    /// Hardware tags the PC must have, or the tweak is hidden as not applicable.
    /// Tags: laptop, desktop, ssd, hdd, flash (SD / USB / eMMC system drive), nvidia, amd-gpu, intel-gpu.
    /// </summary>
    public List<string> Requires { get; set; } = new();

    /// <summary>Hardware tags where this tweak is a bad idea. It stays selectable but is never preselected and shows <see cref="AvoidNote"/>.</summary>
    public List<string> AvoidOn { get; set; } = new();

    public string? AvoidNote { get; set; }

    /// <summary>true = the registry value or policy is documented but could not be confirmed on every build. Shown to the user.</summary>
    public bool Unverified { get; set; }

    /// <summary>Hardware tags where this tweak helps most (same tags as Requires, plus flash). Shown as a hint; never preselects anything.</summary>
    public List<string> FitsOn { get; set; } = new();

    public string? FitNote { get; set; }

    public List<TweakAction> Actions { get; set; } = new();
}

/// <summary>
/// One low-level change. Which fields matter depends on <see cref="Type"/>:
/// registry | service | task | appx | powerplan | command.
/// </summary>
public sealed class TweakAction
{
    public string Type { get; set; } = "";

    // registry: Hive (HKLM/HKCU), Path, Name ("" = default value), Kind, Value, Delete, CleanupKey
    public string? Hive { get; set; }
    public string? Path { get; set; }
    public string? Name { get; set; }
    public string? Kind { get; set; }
    public string? Value { get; set; }

    /// <summary>registry: apply = delete the value instead of setting it.</summary>
    public bool Delete { get; set; }

    /// <summary>registry: key tree (under Hive) to delete on revert when the value did not exist originally.</summary>
    public string? CleanupKey { get; set; }

    // service: Name + StartType (Disabled | Manual | Automatic)
    public string? StartType { get; set; }

    // task: Path = full task path, e.g. \Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser

    // appx: Name = package name, e.g. Microsoft.BingNews

    // powerplan: Guid = scheme to activate, Name = its display name (used for detection)
    public string? Guid { get; set; }

    // command: PowerShell snippets. DetectCommand exits 0 when the tweak is applied.
    public string? ApplyCommand { get; set; }
    public string? RevertCommand { get; set; }
    public string? DetectCommand { get; set; }
}

public sealed class CatalogFile
{
    public int Version { get; set; }
    public List<Tweak> Tweaks { get; set; } = new();
}

public sealed class TweakResult
{
    public List<string> Errors { get; } = new();
    public bool Success => Errors.Count == 0;
}
