using Microsoft.Win32;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Engine;

public static class RestorePointService
{
    private const string KeyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
    private const string FrequencyValue = "SystemRestorePointCreationFrequency";

    /// <summary>
    /// Creates a System Restore point. Windows normally allows only one per 24 hours, so the throttle
    /// value is temporarily set to 0 and put back afterwards.
    /// </summary>
    public static (bool Success, string Message) Create(string description)
    {
        object? previous = null;
        try
        {
            using (var key = Registry.LocalMachine.CreateSubKey(KeyPath, true))
            {
                previous = key.GetValue(FrequencyValue);
                key.SetValue(FrequencyValue, 0, RegistryValueKind.DWord);
            }

            var safe = description.Replace("'", "''");
            var script = $@"
$ErrorActionPreference = 'Stop'
Enable-ComputerRestore -Drive ""$env:SystemDrive\""
Checkpoint-Computer -Description '{safe}' -RestorePointType MODIFY_SETTINGS";
            var (code, output) = Shell.PowerShell(script, 300_000);
            return code == 0
                ? (true, "Restore point created.")
                : (false, string.IsNullOrWhiteSpace(output) ? "Restore point failed." : output);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
        finally
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(KeyPath, true);
                if (previous is null) key.DeleteValue(FrequencyValue, false);
                else key.SetValue(FrequencyValue, previous, RegistryValueKind.DWord);
            }
            catch { /* best effort */ }
        }
    }
}
