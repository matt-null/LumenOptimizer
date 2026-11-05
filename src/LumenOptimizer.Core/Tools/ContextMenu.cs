using Microsoft.Win32;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

public sealed class ContextMenuItem
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";

    /// <summary>Key under HKCU\Software\Classes, for example "Directory\Background\shell\LumenPowerShell".</summary>
    public string KeyPath { get; init; } = "";
    public string Command { get; init; } = "";
    public string Icon { get; init; } = "";
    public bool Elevate { get; init; }
    public bool Installed { get; set; }
}

/// <summary>
/// Adds and removes right-click menu entries for the current user only (HKCU\Software\Classes), so nothing
/// system-wide is touched and removing an entry fully undoes it. On Windows 11 they appear under "Show more options".
/// </summary>
public static class ContextMenu
{
    private const string Root = @"Software\Classes\";

    public static List<ContextMenuItem> Items()
    {
        var list = new List<ContextMenuItem>
        {
            new() { Id = "ps-here", Title = "Open PowerShell here", Description = "Right-click inside a folder to open PowerShell in it.",
                    KeyPath = @"Directory\Background\shell\LumenPowerShell", Icon = "powershell.exe",
                    Command = "powershell.exe -NoExit -Command \"Set-Location -LiteralPath '%V'\"" },
            new() { Id = "cmd-here", Title = "Open Command Prompt here", Description = "Right-click inside a folder to open Command Prompt in it.",
                    KeyPath = @"Directory\Background\shell\LumenCmd", Icon = "cmd.exe",
                    Command = "cmd.exe /k cd /d \"%V\"" },
            new() { Id = "restart-explorer", Title = "Restart Explorer", Description = "Desktop right-click: restarts the taskbar and desktop if they freeze.",
                    KeyPath = @"DesktopBackground\Shell\LumenRestartExplorer", Icon = "explorer.exe",
                    Command = "cmd.exe /c taskkill /f /im explorer.exe & start explorer.exe" },
            new() { Id = "empty-bin", Title = "Empty Recycle Bin", Description = "Desktop right-click: empties the Recycle Bin without opening it. It does not ask first.",
                    KeyPath = @"DesktopBackground\Shell\LumenEmptyBin", Icon = "shell32.dll,-254",
                    Command = "powershell.exe -NoProfile -WindowStyle Hidden -Command \"Clear-RecycleBin -Force\"" },
            new() { Id = "notepad", Title = "Open with Notepad", Description = "Adds 'Open with Notepad' to every file.",
                    KeyPath = @"*\shell\LumenNotepad", Icon = "notepad.exe",
                    Command = "notepad.exe \"%1\"" },
            new() { Id = "ps1-admin", Title = "Run .ps1 scripts as administrator", Description = "Adds 'Run as administrator' to PowerShell script files.",
                    KeyPath = @"SystemFileAssociations\.ps1\shell\runas", Icon = "powershell.exe", Elevate = true,
                    Command = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%1\"" },
        };

        foreach (var i in list) i.Installed = IsInstalled(i);
        return list;
    }

    public static bool IsInstalled(ContextMenuItem i)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Root + i.KeyPath + @"\command", false);
            return k?.GetValue("") is string s && s.Length > 0;
        }
        catch { return false; }
    }

    public static void Install(ContextMenuItem i)
    {
        Log.Info($"Context menu: add \"{i.Title}\" -> HKCU\\{Root}{i.KeyPath}");
        using var k = Registry.CurrentUser.CreateSubKey(Root + i.KeyPath, true)
            ?? throw new InvalidOperationException("Could not create the registry key.");
        k.SetValue("", i.Title);
        k.SetValue("MUIVerb", i.Title);
        if (i.Icon.Length > 0) k.SetValue("Icon", i.Icon);
        if (i.Elevate) k.SetValue("HasLUAShield", "");
        using var c = k.CreateSubKey("command", true)!;
        c.SetValue("", i.Command);
        i.Installed = true;
    }

    public static void Remove(ContextMenuItem i)
    {
        Log.Info($"Context menu: remove \"{i.Title}\"");
        Registry.CurrentUser.DeleteSubKeyTree(Root + i.KeyPath, throwOnMissingSubKey: false);
        i.Installed = false;
    }
}
