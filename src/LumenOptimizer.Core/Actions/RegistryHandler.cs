using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Win32;
using LumenOptimizer.Core.Models;

namespace LumenOptimizer.Core.Actions;

internal sealed class RegistryHandler : IActionHandler
{
    public string Type => "registry";

    private static RegistryKey Root(string? hive) => (hive ?? "").ToUpperInvariant() switch
    {
        "HKLM" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
        "HKCU" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64),
        _ => throw new InvalidOperationException($"Unsupported registry hive '{hive}'.")
    };

    private static RegistryValueKind ParseKind(string? kind) =>
        Enum.TryParse<RegistryValueKind>(kind ?? "DWord", true, out var k)
            ? k
            : throw new InvalidOperationException($"Unsupported registry value kind '{kind}'.");

    private static string Serialize(object value, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.Binary => Convert.ToBase64String((byte[])value),
        RegistryValueKind.MultiString => string.Join("\0", (string[])value),
        RegistryValueKind.DWord => ((int)value).ToString(CultureInfo.InvariantCulture),
        RegistryValueKind.QWord => ((long)value).ToString(CultureInfo.InvariantCulture),
        _ => (string)value
    };

    private static object Deserialize(string text, RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.Binary => Convert.FromBase64String(text),
        RegistryValueKind.MultiString => text.Split('\0'),
        // long.Parse accepts both "4294967295" and "-1"; unchecked cast gives the same DWORD bits.
        RegistryValueKind.DWord => unchecked((int)long.Parse(text, CultureInfo.InvariantCulture)),
        RegistryValueKind.QWord => long.Parse(text, CultureInfo.InvariantCulture),
        _ => text
    };

    public bool? IsApplied(TweakAction a)
    {
        using var root = Root(a.Hive);
        using var key = root.OpenSubKey(a.Path!, false);
        var name = a.Name ?? "";

        if (a.Delete)
            return key is null || key.GetValue(name) is null;

        if (key is null) return false;
        var current = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        if (current is null) return false;

        var wanted = ParseKind(a.Kind);
        var currentKind = key.GetValueKind(name);
        if (currentKind != wanted) return false;

        return Serialize(current, currentKind) == Serialize(Deserialize(a.Value ?? "", wanted), wanted);
    }

    public JsonObject? Capture(TweakAction a)
    {
        using var root = Root(a.Hive);
        using var key = root.OpenSubKey(a.Path!, false);
        var name = a.Name ?? "";
        var current = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);

        if (key is null || current is null)
            return new JsonObject { ["exists"] = false };

        var kind = key.GetValueKind(name);
        return new JsonObject
        {
            ["exists"] = true,
            ["kind"] = kind.ToString(),
            ["value"] = Serialize(current, kind)
        };
    }

    public void Apply(TweakAction a)
    {
        using var root = Root(a.Hive);
        var name = a.Name ?? "";

        if (a.Delete)
        {
            using var existing = root.OpenSubKey(a.Path!, true);
            existing?.DeleteValue(name, false);
            return;
        }

        var kind = ParseKind(a.Kind);
        using var key = root.CreateSubKey(a.Path!, true);
        key.SetValue(name, Deserialize(a.Value ?? "", kind), kind);
    }

    public bool CanRevert(TweakAction a) => true;

    public void Restore(TweakAction a, JsonObject? snapshot)
    {
        if (snapshot is null)
            throw new InvalidOperationException(
                $"No saved original value for {a.Hive}\\{a.Path}. This tweak was not applied by Lumen Optimizer, so there is nothing to restore.");

        using var root = Root(a.Hive);
        var name = a.Name ?? "";

        if (snapshot["exists"]?.GetValue<bool>() == true)
        {
            var kind = Enum.Parse<RegistryValueKind>(snapshot["kind"]!.GetValue<string>());
            var value = Deserialize(snapshot["value"]!.GetValue<string>(), kind);
            using var key = root.CreateSubKey(a.Path!, true);
            key.SetValue(name, value, kind);
        }
        else
        {
            using (var key = root.OpenSubKey(a.Path!, true))
            {
                key?.DeleteValue(name, false);
            }

            if (!string.IsNullOrEmpty(a.CleanupKey))
                root.DeleteSubKeyTree(a.CleanupKey, false);
        }
    }
}
