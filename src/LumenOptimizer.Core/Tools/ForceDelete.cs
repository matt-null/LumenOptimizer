using System.Diagnostics;
using System.Runtime.InteropServices;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

/// <summary>Removes files and folders that will not go: takes ownership, grants access, ends the programs using them,
/// then falls back to SYSTEM and finally to removal at the next restart. Core Windows and this app are protected.</summary>
public static class ForceDelete
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string existing, string? target, int flags);

    public static string? Protected(string full)
    {
        string N(string p) => Path.GetFullPath(p).TrimEnd('\\');
        var path = N(full);
        var root = N(Path.GetPathRoot(path) ?? "C:\\");
        if (path.Equals(root, StringComparison.OrdinalIgnoreCase) || path.Length <= 3) return "That is a whole drive.";
        var win = N(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        if (path.Equals(win, StringComparison.OrdinalIgnoreCase) || path.StartsWith(win + "\\", StringComparison.OrdinalIgnoreCase))
            return "Files inside the Windows folder are protected so the PC keeps booting.";
        var keep = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? "C:\\Users",
            AppContext.BaseDirectory, AppPaths.DataDir
        };
        foreach (var k in keep.Where(k => !string.IsNullOrEmpty(k)).Select(N))
            if (k.Equals(path, StringComparison.OrdinalIgnoreCase) || k.StartsWith(path + "\\", StringComparison.OrdinalIgnoreCase))
                return "That location holds Windows, your user profile or this app, so it is protected.";
        return null;
    }

    public static string Delete(string input)
    {
        var path = Path.GetFullPath(input.Trim().Trim('"'));
        var block = Protected(path);
        if (block != null) throw new InvalidOperationException(block);
        var isDir = Directory.Exists(path);
        if (!isDir && !File.Exists(path)) throw new FileNotFoundException("Nothing exists at " + path);
        Log.Info("Force delete: " + path);

        if (TryRemove(path, isDir)) return "Deleted.";

        // 2: ownership + permissions, then end programs running from inside it
        Shell.Run("takeown.exe", $"/f \"{path}\"" + (isDir ? " /r /d y" : ""), 300_000, logOutput: false);
        Shell.Run("icacls.exe", $"\"{path}\" /grant *S-1-5-32-544:F" + (isDir ? " /t" : "") + " /c /q", 300_000, logOutput: false);
        EndProcessesIn(path, isDir);
        if (TryRemove(path, isDir)) return "Deleted after taking ownership.";

        // 3: SYSTEM
        var cmd = isDir ? $"rd /s /q \"{path}\"" : $"del /f /q \"{path}\"";
        SystemRun.Run($"takeown /f \"{path}\"{(isDir ? " /r /d y" : "")}\r\nicacls \"{path}\" /grant *S-1-5-32-544:F{(isDir ? " /t" : "")} /c /q\r\nattrib -r -s -h \"{path}\"{(isDir ? " /s /d" : "")}\r\n{cmd}");
        if (!Exists(path, isDir)) return "Deleted by the SYSTEM account.";

        // 4: next restart
        int queued = 0;
        var items = isDir ? Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length).ToList() : new List<string>();
        items.Add(path);
        foreach (var i in items) if (MoveFileEx(i, null, 4)) queued++;
        return queued > 0 ? $"It is in use by Windows. {queued} item(s) will be removed the next time you restart the PC."
                          : throw new IOException("Could not remove it, even at restart.");
    }

    private static bool Exists(string p, bool dir) => dir ? Directory.Exists(p) : File.Exists(p);

    private static bool TryRemove(string path, bool isDir)
    {
        try
        {
            if (isDir)
            {
                foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    try { File.SetAttributes(f, FileAttributes.Normal); } catch { }
                Directory.Delete(path, true);
            }
            else { File.SetAttributes(path, FileAttributes.Normal); File.Delete(path); }
        }
        catch (Exception ex) { Log.Warn("  normal delete failed: " + ex.Message); }
        return !Exists(path, isDir);
    }

    private static void EndProcessesIn(string path, bool isDir)
    {
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var exe = p.MainModule?.FileName;
                if (exe == null || p.Id == Environment.ProcessId) continue;
                var hit = isDir ? exe.StartsWith(path + "\\", StringComparison.OrdinalIgnoreCase) : exe.Equals(path, StringComparison.OrdinalIgnoreCase);
                if (!hit) continue;
                Log.Info($"  ending {p.ProcessName} (it was using the target)");
                p.Kill(true); p.WaitForExit(3000);
            }
            catch { /* protected or already gone */ }
        }
    }
}
