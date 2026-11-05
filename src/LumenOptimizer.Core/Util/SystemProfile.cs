using System.Text.Json;
using Microsoft.Win32;

namespace LumenOptimizer.Core.Util;

/// <summary>What kind of PC and Windows this is. Used to tailor presets, warnings and the splash screen.</summary>
public sealed class SystemProfile
{
    public int Build { get; init; }
    public int Ubr { get; init; }
    public string Edition { get; init; } = "";          // e.g. Professional, Core, Enterprise, EnterpriseS
    public string DisplayVersion { get; init; } = "";   // e.g. 24H2, 22H2
    public bool IsWindows11 => Build >= 22000;
    public bool IsWindows10 => Build is >= 10240 and < 22000;
    public bool IsLtsc => Edition.Contains("EnterpriseS", StringComparison.OrdinalIgnoreCase)
                          || Edition.Contains("IoTEnterpriseS", StringComparison.OrdinalIgnoreCase);

    public bool? IsLaptop { get; init; }                // null = could not tell
    public string SystemDriveType { get; init; } = "";  // SSD, NVMe SSD, HDD or ""
    public bool? IsSsd { get; init; }
    /// <summary>The Windows drive is an SD card, USB stick or eMMC: slow random access and limited write endurance.</summary>
    public bool IsFlash { get; init; }
    public List<string> Gpus { get; init; } = new();
    public double RamGb { get; init; }
    public string Cpu { get; init; } = "";

    public string WindowsName => IsWindows11 ? "Windows 11" : IsWindows10 ? "Windows 10" : "Windows";

    public string EditionName => Edition switch
    {
        "Core" => "Home",
        "CoreSingleLanguage" => "Home Single Language",
        "Professional" => "Pro",
        "ProfessionalWorkstation" => "Pro for Workstations",
        "ProfessionalEducation" => "Pro Education",
        "Education" => "Education",
        "Enterprise" => "Enterprise",
        "EnterpriseS" => "Enterprise LTSC",
        "IoTEnterpriseS" => "IoT Enterprise LTSC",
        "IoTEnterprise" => "IoT Enterprise",
        _ => Edition
    };

    /// <summary>Tags matched against Tweak.Requires / Tweak.AvoidOn.</summary>
    public HashSet<string> Tags
    {
        get
        {
            var t = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (IsLaptop == true) t.Add("laptop");
            if (IsLaptop == false) t.Add("desktop");
            if (IsFlash) t.Add("flash");
            if (IsSsd == true) t.Add("ssd");
            if (IsSsd == false) t.Add("hdd");
            foreach (var g in Gpus)
            {
                if (g.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) t.Add("nvidia");
                else if (g.Contains("AMD", StringComparison.OrdinalIgnoreCase) || g.Contains("Radeon", StringComparison.OrdinalIgnoreCase)) t.Add("amd-gpu");
                else if (g.Contains("Intel", StringComparison.OrdinalIgnoreCase)) t.Add("intel-gpu");
            }
            return t;
        }
    }

    public string Summary
    {
        get
        {
            var parts = new List<string>
            {
                $"{WindowsName} {EditionName} {DisplayVersion}".Replace("  ", " ").Trim() + $" (build {Build}.{Ubr})"
            };
            if (IsLaptop is bool l) parts.Add(l ? "laptop" : "desktop");
            if (SystemDriveType.Length > 0) parts.Add(SystemDriveType);
            if (Gpus.Count > 0) parts.Add(string.Join(" + ", Gpus.Select(ShortGpu)));
            if (RamGb > 0) parts.Add($"{Math.Round(RamGb)} GB RAM");
            return string.Join(" · ", parts);
        }
    }

    private static string ShortGpu(string g) => g.Replace("(R)", "").Replace("(TM)", "").Replace("  ", " ").Trim();

    /// <summary>
    /// A warning for the splash screen when this Windows is out of support, or null.
    /// Dates are Microsoft's published lifecycle as I know it; Windows 10 consumer Extended Security Updates ran for one year.
    /// </summary>
    public string? SupportWarning
    {
        get
        {
            if (IsWindows10)
            {
                if (IsLtsc)
                    return "This is a Windows 10 LTSC edition. LTSC follows its own support lifecycle, which is longer than regular Windows 10. Check Microsoft's lifecycle page for your exact release.";
                return "Windows 10 22H2 reached end of support on 14 October 2025. It no longer gets free security updates unless the PC is enrolled in Extended Security Updates (ESU); the consumer ESU program was a single year, to October 2026. Tweaks here help, but upgrading to Windows 11 or enrolling in ESU is the real fix.";
            }
            if (IsWindows11 && Build == 22000)
                return "Windows 11 21H2 is no longer serviced for Home and Pro. Update to a current version for security fixes.";
            if (IsWindows11 && Build is 22621 or 22631)
                return "Windows 11 22H2/23H2 are out of support for Home and Pro editions. Update to 24H2 or newer for security fixes.";
            return null;
        }
    }

    public static SystemProfile Detect()
    {
        int build = Environment.OSVersion.Version.Build, ubr = 0;
        string edition = "", display = "";
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", false);
            if (k is not null)
            {
                edition = k.GetValue("EditionID") as string ?? "";
                display = (k.GetValue("DisplayVersion") ?? k.GetValue("ReleaseId")) as string ?? "";
                if (k.GetValue("UBR") is int u) ubr = u;
            }
        }
        catch (Exception ex) { Log.Warn("Could not read Windows version: " + ex.Message); }

        bool? laptop = null, ssd = null;
        var flash = false;
        string driveType = "", cpu = "";
        double ram = 0;
        var gpus = new List<string>();

        try
        {
            const string script = @"
$out = [ordered]@{}
try { $out.chassis = @((Get-CimInstance Win32_SystemEnclosure).ChassisTypes) } catch {}
try { $out.battery = @(Get-CimInstance Win32_Battery).Count } catch {}
try { $out.gpus = @((Get-CimInstance Win32_VideoController).Name) } catch {}
try { $cs = Get-CimInstance Win32_ComputerSystem; $out.ram = [math]::Round($cs.TotalPhysicalMemory/1GB,1) } catch {}
try { $out.cpu = (Get-CimInstance Win32_Processor | Select-Object -First 1).Name } catch {}
try {
  $pd = Get-Partition -DriveLetter $env:SystemDrive.Substring(0,1) | Get-Disk | Get-PhysicalDisk | Select-Object -First 1
  $out.media = [string]$pd.MediaType
  $out.bus = [string]$pd.BusType
} catch {}
$out | ConvertTo-Json -Compress";
            var (code, output) = Shell.PowerShell(script, 60_000, logOutput: true);
            if (code == 0)
            {
                var start = output.IndexOf('{');
                using var doc = JsonDocument.Parse(start >= 0 ? output[start..] : output);
                var r = doc.RootElement;

                int[] laptopChassis = { 8, 9, 10, 11, 12, 14, 18, 21, 30, 31, 32 };
                var hasChassis = false; var chassisLaptop = false;
                if (r.TryGetProperty("chassis", out var ch))
                {
                    foreach (var c in Items(ch))
                        if (c.TryGetInt32(out var n)) { hasChassis = true; if (laptopChassis.Contains(n)) chassisLaptop = true; }
                }
                var battery = r.TryGetProperty("battery", out var b) && b.TryGetInt32(out var bc) ? bc : 0;
                if (hasChassis || battery > 0) laptop = chassisLaptop || battery > 0;

                if (r.TryGetProperty("gpus", out var g))
                    foreach (var x in Items(g))
                        if (x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString())
                            && !x.GetString()!.Contains("Basic Display", StringComparison.OrdinalIgnoreCase)
                            && !x.GetString()!.Contains("Remote Display", StringComparison.OrdinalIgnoreCase))
                            gpus.Add(x.GetString()!.Trim());

                if (r.TryGetProperty("ram", out var rm) && rm.TryGetDouble(out var rg)) ram = rg;
                if (r.TryGetProperty("cpu", out var cp) && cp.ValueKind == JsonValueKind.String) cpu = cp.GetString()!.Trim();

                var media = r.TryGetProperty("media", out var m) ? m.GetString() ?? "" : "";
                var bus = r.TryGetProperty("bus", out var bu) ? bu.GetString() ?? "" : "";
                if (bus.ToUpperInvariant() is "SD" or "MMC" || (bus.ToUpperInvariant() == "USB" && media is not ("SSD" or "HDD")))
                {
                    flash = true; driveType = bus.ToUpperInvariant() switch { "SD" => "SD card", "MMC" => "eMMC / SD", _ => "USB drive" };
                }
                else if (bus.Equals("NVMe", StringComparison.OrdinalIgnoreCase)) { ssd = true; driveType = "NVMe SSD"; }
                else if (media.Equals("SSD", StringComparison.OrdinalIgnoreCase)) { ssd = true; driveType = "SSD"; }
                else if (media.Equals("HDD", StringComparison.OrdinalIgnoreCase)) { ssd = false; driveType = "HDD"; }
            }
        }
        catch (Exception ex) { Log.Warn("Hardware detection failed: " + ex.Message); }

        var p = new SystemProfile
        {
            Build = build, Ubr = ubr, Edition = edition, DisplayVersion = display,
            IsLaptop = laptop, IsSsd = ssd, IsFlash = flash, SystemDriveType = driveType, Gpus = gpus, RamGb = ram, Cpu = cpu
        };
        Log.Info("System: " + p.Summary);
        return p;
    }

    private static IEnumerable<JsonElement> Items(JsonElement e) =>
        e.ValueKind == JsonValueKind.Array ? e.EnumerateArray().ToList() : new List<JsonElement> { e };
}
