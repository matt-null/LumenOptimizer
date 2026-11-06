using System.Runtime.InteropServices;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

public sealed record LocalAccount(string Name, bool Disabled, bool Locked, string Sid, bool IsAdmin);

/// <summary>Local Windows accounts: reset a forgotten password, unlock, enable/disable, admin rights. Passwords never reach the log.</summary>
public static class Accounts
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct USER_INFO_1003 { public string usri1003_password; }

    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int NetUserSetInfo(string? server, string user, int level, ref USER_INFO_1003 buf, out int err);

    public static List<LocalAccount> List()
    {
        var admins = Ps.Json("ConvertTo-Json -InputObject @(Get-LocalGroupMember -SID S-1-5-32-544 -EA SilentlyContinue | Select-Object -ExpandProperty Name) -Compress")
            .Select(e => e.ToString().Split('\\').Last()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Ps.Json("ConvertTo-Json -InputObject @(Get-CimInstance Win32_UserAccount -Filter 'LocalAccount=True' | Select-Object Name,Disabled,Lockout,SID) -Compress")
            .Select(e => new LocalAccount(e.S("Name"), e.S("Disabled") == "True", e.S("Lockout") == "True", e.S("SID"), admins.Contains(e.S("Name"))))
            .OrderBy(a => a.Name).ToList();
    }

    private static void Check(string name)
    {
        if (!List().Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("No local account called " + name);
    }

    public static string SetPassword(string name, string password)
    {
        Check(name);
        var info = new USER_INFO_1003 { usri1003_password = password };
        var rc = NetUserSetInfo(null, name, 1003, ref info, out _);
        if (rc != 0) throw new InvalidOperationException($"Windows refused the password (code {rc}). It may not meet the password policy.");
        Log.Info($"Password changed for local account '{name}'.");
        return $"Password for {name} changed. Files encrypted with EFS or saved web/Windows credentials for this account may no longer open.";
    }

    public static string Unlock(string name)
    {
        Check(name);
        Shell.PowerShell($"$u=[ADSI]'WinNT://./{Ps.Q(name)},user'; $u.IsAccountLocked=$false; $u.SetInfo()");
        return $"{name} unlocked.";
    }

    public static string SetEnabled(string name, bool enabled)
    {
        Check(name);
        if (!enabled && name.Equals(Environment.UserName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("You are signed in as this account; disabling it would lock you out.");
        var (c, o) = Shell.PowerShell($"{(enabled ? "Enable" : "Disable")}-LocalUser -Name '{Ps.Q(name)}'");
        if (c != 0) throw new InvalidOperationException(o);
        return $"{name} {(enabled ? "enabled" : "disabled")}.";
    }

    public static string SetAdmin(string name, bool admin)
    {
        Check(name);
        if (!admin && List().Count(a => a.IsAdmin && !a.Disabled) <= 1)
            throw new InvalidOperationException("That is the only active administrator. Keep at least one.");
        var verb = admin ? "Add" : "Remove";
        var (c, o) = Shell.PowerShell($"{verb}-LocalGroupMember -SID S-1-5-32-544 -Member '{Ps.Q(name)}'");
        if (c != 0) throw new InvalidOperationException(o);
        return $"{name} {(admin ? "is now an administrator" : "is no longer an administrator")}.";
    }
}
