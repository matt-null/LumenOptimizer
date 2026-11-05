using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

/// <summary>A device Windows still remembers but that is not plugged in ("ghost" device in Device Manager).</summary>
public sealed class GhostDevice
{
    public string InstanceId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Class { get; init; } = "";

    /// <summary>USB, storage and other removable-hardware classes: the ones people mean by "old USBs still showing up".</summary>
    public bool IsRemovableKind => Class is "USB" or "USBDevice" or "DiskDrive" or "Volume" or "WPD" or "SCSIAdapter"
        or "Bluetooth" or "Image" or "PrintQueue" or "Ports" or "HIDClass" or "Mouse" or "Keyboard" or "Media" or "Net";

    /// <summary>Classes that are part of the machine itself. Removing these records gains nothing, so they are not offered by default.</summary>
    public bool IsRisky => Class is "System" or "Computer" or "Processor" or "SoftwareDevice"
        or "SoftwareComponent" or "SecurityDevices" or "Display";
}

/// <summary>One leftover registry record for a drive or device that is no longer attached.</summary>
public sealed class StaleRecord
{
    public string Kind { get; init; } = "";        // explorer-mountpoint | portable-device | drive-letter
    public string Description { get; init; } = "";
    public string Hive { get; init; } = "";        // HKCU | HKLM
    public string KeyPath { get; init; } = "";
    /// <summary>Set when a single value is removed instead of a whole key.</summary>
    public string? ValueName { get; init; }
}

/// <summary>
/// Removes records Windows keeps for hardware that is gone: ghost devices in Device Manager, old USB drives
/// in Explorer and Portable Devices, and drive letters still reserved for drives that are not attached.
/// Registry removals are backed up to a file first, so they can be put back.
/// </summary>
public static class DeviceCleanup
{
    private static readonly Regex SafeInstanceId = new(@"^[A-Za-z0-9\\&#_\-.{}+]{3,400}$", RegexOptions.Compiled);

    public static string BackupDir => Path.Combine(AppPaths.DataDir, "device-backups");

    // ------------------------------------------------------- ghost devices

    /// <summary>Devices Windows has a driver record for but which are not connected. Locale-independent (uses CIM properties, not text).</summary>
    public static List<GhostDevice> GhostDevices()
    {
        const string script = @"
Get-PnpDevice -ErrorAction SilentlyContinue |
  Where-Object { -not $_.Present } |
  Select-Object InstanceId, FriendlyName, Class |
  ConvertTo-Json -Compress";

        var (code, output) = Shell.PowerShell(script, 120_000, logOutput: false);
        if (code != 0)
            throw new InvalidOperationException("Could not list devices: " + output);

        var list = new List<GhostDevice>();
        var start = output.IndexOfAny(new[] { '[', '{' });
        if (start < 0) return list;

        using var doc = JsonDocument.Parse(output[start..]);
        var items = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement.EnumerateArray().ToList()
            : new List<JsonElement> { doc.RootElement };

        foreach (var e in items)
        {
            var id = Str(e, "InstanceId");
            if (id.Length == 0) continue;
            var name = Str(e, "FriendlyName");
            list.Add(new GhostDevice
            {
                InstanceId = id,
                Name = name.Length > 0 ? name : id,
                Class = Str(e, "Class")
            });
        }

        Log.Info($"Devices: {list.Count} not-present device record(s) found");
        return list.OrderBy(d => d.Class, StringComparer.OrdinalIgnoreCase)
                   .ThenBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? (v.GetString() ?? "").Trim() : "";

    /// <summary>The exact command used to remove one device record, shown to the user before they confirm.</summary>
    public static string RemoveCommandFor(GhostDevice d) => $"pnputil /remove-device \"{d.InstanceId}\"";

    /// <summary>
    /// Removes the stored record for devices that are not connected. There is no undo, but plugging the device
    /// back in makes Windows set it up again. Returns one result line per device.
    /// </summary>
    public static (int Removed, List<string> Problems) RemoveGhostDevices(IEnumerable<GhostDevice> devices)
    {
        var problems = new List<string>();
        var removed = 0;

        foreach (var d in devices)
        {
            if (!SafeInstanceId.IsMatch(d.InstanceId))
            {
                problems.Add($"{d.Name}: skipped, its instance ID has unexpected characters.");
                continue;
            }

            var (code, output) = Shell.Run("pnputil.exe", $"/remove-device \"{d.InstanceId}\"", 60_000);
            if (code == 0)
            {
                removed++;
            }
            else if (output.Contains("/remove-device", StringComparison.OrdinalIgnoreCase)
                     && output.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add("This Windows build's pnputil has no /remove-device. Use Device Manager: View > Show hidden devices.");
                break;
            }
            else
            {
                problems.Add($"{d.Name}: {(output.Length > 0 ? output.Split('\n')[0].Trim() : $"exit code {code}")}");
            }
        }

        Log.Info($"Devices: removed {removed} device record(s), {problems.Count} problem(s)");
        return (removed, problems);
    }

    /// <summary>Opens Device Manager with non-present ("hidden") devices already shown.</summary>
    public static void OpenDeviceManagerWithHidden()
    {
        Log.Info("Devices: opening Device Manager with hidden devices shown");
        var psi = new ProcessStartInfo("mmc.exe", "devmgmt.msc") { UseShellExecute = false };
        psi.Environment["DEVMGR_SHOW_NONPRESENT_DEVICES"] = "1";
        Process.Start(psi);
    }

    // --------------------------------------------------- stale drive records

    private const string MountPoints2 = @"Software\Microsoft\Windows\CurrentVersion\Explorer\MountPoints2";
    private const string PortableDevices = @"SOFTWARE\Microsoft\Windows Portable Devices\Devices";
    private const string MountedDevices = @"SYSTEM\MountedDevices";

    /// <summary>Leftover records for drives that are not attached right now.</summary>
    public static List<StaleRecord> StaleDriveRecords()
    {
        var records = new List<StaleRecord>();
        var present = PresentLetters();

        // 1. Explorer's own list of volumes it has seen. Per-user, and rebuilt automatically.
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(MountPoints2, false);
            if (k is not null)
                foreach (var name in k.GetSubKeyNames())
                {
                    // A single letter means a mapped drive letter; a {GUID} is a volume Explorer has seen before.
                    if (name.Length == 1 && char.IsLetter(name[0]) && present.Contains(char.ToUpperInvariant(name[0]))) continue;
                    records.Add(new StaleRecord
                    {
                        Kind = "explorer-mountpoint",
                        Description = $"Explorer remembers volume \"{name}\"",
                        Hive = "HKCU",
                        KeyPath = MountPoints2 + "\\" + name
                    });
                }
        }
        catch (Exception ex) { Log.Warn("MountPoints2: " + ex.Message); }

        // 2. Portable Devices: the friendly names Windows shows for drives and phones it has seen.
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(PortableDevices, false);
            if (k is not null)
                foreach (var name in k.GetSubKeyNames())
                {
                    string friendly = "";
                    try
                    {
                        using var sub = k.OpenSubKey(name, false);
                        friendly = sub?.GetValue("FriendlyName") as string ?? "";
                    }
                    catch { /* unreadable */ }

                    // Keep entries whose drive letter is currently in use.
                    var letter = Regex.Match(name, @"(?:^|#|\\)([A-Za-z]):").Groups[1].Value;
                    if (letter.Length == 1 && present.Contains(char.ToUpperInvariant(letter[0]))) continue;

                    records.Add(new StaleRecord
                    {
                        Kind = "portable-device",
                        Description = friendly.Length > 0 ? $"Saved device name \"{friendly}\"" : $"Saved device record \"{Shorten(name)}\"",
                        Hive = "HKLM",
                        KeyPath = PortableDevices + "\\" + name
                    });
                }
        }
        catch (Exception ex) { Log.Warn("Portable Devices: " + ex.Message); }

        // 3. Drive letters still reserved for drives that are not attached.
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(MountedDevices, false);
            if (k is not null)
                foreach (var value in k.GetValueNames())
                {
                    var m = Regex.Match(value, @"^\\DosDevices\\([A-Za-z]):$");
                    if (!m.Success) continue;
                    var letter = char.ToUpperInvariant(m.Groups[1].Value[0]);
                    if (present.Contains(letter)) continue;
                    if (letter is 'C' or 'A' or 'B') continue;                 // never the system drive or floppies
                    if (letter == SystemDriveLetter()) continue;

                    records.Add(new StaleRecord
                    {
                        Kind = "drive-letter",
                        Description = $"Drive letter {letter}: is reserved for a drive that is not attached",
                        Hive = "HKLM",
                        KeyPath = MountedDevices,
                        ValueName = value
                    });
                }
        }
        catch (Exception ex) { Log.Warn("MountedDevices: " + ex.Message); }

        Log.Info($"Devices: {records.Count} leftover drive record(s) found");
        return records;
    }

    private static string Shorten(string s) => s.Length <= 60 ? s : s[..57] + "…";

    private static HashSet<char> PresentLetters()
    {
        var set = new HashSet<char>();
        try
        {
            foreach (var d in DriveInfo.GetDrives())
                if (d.Name.Length > 0) set.Add(char.ToUpperInvariant(d.Name[0]));
        }
        catch { /* ignore */ }
        return set;
    }

    private static char SystemDriveLetter()
    {
        var s = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
        return s.Length > 0 ? char.ToUpperInvariant(s[0]) : 'C';
    }

    // ----------------------------------------------------- remove + restore

    /// <summary>Backs the records up to a file, then removes them. Returns the backup path.</summary>
    public static string RemoveStaleDriveRecords(IEnumerable<StaleRecord> records)
    {
        var list = records.ToList();
        var backup = new JsonArray();

        foreach (var r in list)
        {
            try { backup.Add(Capture(r)); }
            catch (Exception ex) { Log.Warn($"Could not back up {r.KeyPath}: {ex.Message}"); }
        }

        Directory.CreateDirectory(BackupDir);
        var path = Path.Combine(BackupDir, $"devices-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        File.WriteAllText(path, backup.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        Log.Info($"Devices: backed up {backup.Count} record(s) to {path}");

        foreach (var r in list)
        {
            var root = Root(r.Hive);
            Log.Info($"  remove {r.Hive}\\{r.KeyPath}" + (r.ValueName is null ? " (key)" : $" value {r.ValueName}"));
            if (r.ValueName is null)
            {
                root.DeleteSubKeyTree(r.KeyPath, throwOnMissingSubKey: false);
            }
            else
            {
                using var k = root.OpenSubKey(r.KeyPath, writable: true);
                k?.DeleteValue(r.ValueName, throwOnMissingValue: false);
            }
        }

        return path;
    }

    private static RegistryKey Root(string hive) => hive.Equals("HKCU", StringComparison.OrdinalIgnoreCase)
        ? Registry.CurrentUser : Registry.LocalMachine;

    private static JsonObject Capture(StaleRecord r)
    {
        var o = new JsonObject
        {
            ["hive"] = r.Hive,
            ["keyPath"] = r.KeyPath,
            ["kind"] = r.Kind,
            ["description"] = r.Description
        };

        if (r.ValueName is not null)
        {
            using var k = Root(r.Hive).OpenSubKey(r.KeyPath, false);
            o["valueName"] = r.ValueName;
            o["value"] = Serialize(k, r.ValueName);
        }
        else
        {
            using var k = Root(r.Hive).OpenSubKey(r.KeyPath, false);
            o["key"] = k is null ? null : CaptureKey(k, depth: 0);
        }

        return o;
    }

    private static JsonObject? CaptureKey(RegistryKey key, int depth)
    {
        if (depth > 4) return null;

        var values = new JsonArray();
        foreach (var name in key.GetValueNames())
            values.Add(new JsonObject { ["name"] = name, ["data"] = Serialize(key, name) });

        var subs = new JsonObject();
        foreach (var name in key.GetSubKeyNames())
        {
            try
            {
                using var sub = key.OpenSubKey(name, false);
                if (sub is not null && CaptureKey(sub, depth + 1) is { } captured) subs[name] = captured;
            }
            catch { /* unreadable subkey */ }
        }

        return new JsonObject { ["values"] = values, ["subKeys"] = subs };
    }

    private static JsonObject? Serialize(RegistryKey? key, string name)
    {
        if (key is null) return null;
        try
        {
            var kind = key.GetValueKind(name);
            var raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (raw is null) return null;

            var text = kind switch
            {
                RegistryValueKind.Binary => Convert.ToBase64String((byte[])raw),
                RegistryValueKind.MultiString => string.Join("\0", (string[])raw),
                _ => Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture) ?? ""
            };
            return new JsonObject { ["kind"] = kind.ToString(), ["text"] = text };
        }
        catch
        {
            return null;
        }
    }

    public static List<string> Backups() =>
        Directory.Exists(BackupDir)
            ? Directory.GetFiles(BackupDir, "devices-*.json").OrderByDescending(f => f).ToList()
            : new List<string>();

    /// <summary>Puts back everything in a backup file. Returns how many records were restored.</summary>
    public static int RestoreBackup(string path)
    {
        Log.Info("Devices: restoring " + path);
        var node = JsonNode.Parse(File.ReadAllText(path)) as JsonArray
            ?? throw new InvalidOperationException("That file is not a Lumen Optimizer device backup.");

        var restored = 0;
        foreach (var item in node)
        {
            if (item is not JsonObject o) continue;
            var hive = o["hive"]?.GetValue<string>() ?? "HKLM";
            var keyPath = o["keyPath"]?.GetValue<string>() ?? "";
            if (keyPath.Length == 0) continue;

            try
            {
                if (o["valueName"]?.GetValue<string>() is { } valueName)
                {
                    using var k = Root(hive).CreateSubKey(keyPath, true);
                    if (k is not null && Write(k, valueName, o["value"] as JsonObject)) restored++;
                }
                else if (o["key"] is JsonObject captured)
                {
                    using var k = Root(hive).CreateSubKey(keyPath, true);
                    if (k is not null) { RestoreKey(k, captured); restored++; }
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"  could not restore {keyPath}: {ex.Message}");
            }
        }

        Log.Info($"  restored {restored} record(s)");
        return restored;
    }

    private static void RestoreKey(RegistryKey key, JsonObject captured)
    {
        if (captured["values"] is JsonArray values)
            foreach (var v in values)
                if (v is JsonObject vo && vo["name"]?.GetValue<string>() is { } name)
                    Write(key, name, vo["data"] as JsonObject);

        if (captured["subKeys"] is JsonObject subs)
            foreach (var (name, sub) in subs)
                if (sub is JsonObject so)
                {
                    using var child = key.CreateSubKey(name, true);
                    if (child is not null) RestoreKey(child, so);
                }
    }

    private static bool Write(RegistryKey key, string name, JsonObject? data)
    {
        if (data?["kind"]?.GetValue<string>() is not { } kindText) return false;
        if (!Enum.TryParse<RegistryValueKind>(kindText, out var kind)) return false;
        var text = data["text"]?.GetValue<string>() ?? "";

        object value = kind switch
        {
            RegistryValueKind.Binary => Convert.FromBase64String(text),
            RegistryValueKind.MultiString => text.Split('\0'),
            RegistryValueKind.DWord => unchecked((int)long.Parse(text, System.Globalization.CultureInfo.InvariantCulture)),
            RegistryValueKind.QWord => long.Parse(text, System.Globalization.CultureInfo.InvariantCulture),
            _ => text
        };

        key.SetValue(name, value, kind);
        return true;
    }

    // ------------------------------------------------------- USB history

    /// <summary>
    /// Every USB storage device this PC has ever had plugged in, from the driver database. Read-only:
    /// clearing this key needs TrustedInstaller rights, which this app does not take.
    /// </summary>
    public static List<string> UsbStorageHistory()
    {
        var list = new List<string>();
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USBSTOR", false);
            if (k is not null) list.AddRange(k.GetSubKeyNames());
        }
        catch (Exception ex) { Log.Warn("USBSTOR history: " + ex.Message); }

        return list
            .Select(n => n.Replace("Disk&", "").Replace("Ven_", "").Replace("Prod_", " ").Replace("Rev_", " rev ").Replace("_", " ").Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
