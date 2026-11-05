using System.Runtime.InteropServices;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

/// <summary>One group of junk files, such as "User temp files".</summary>
public sealed class CleanTarget
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";

    /// <summary>Folders whose contents are removed (the folder itself is kept).</summary>
    public List<string> Folders { get; init; } = new();

    /// <summary>Individual files (supports * wildcards in the file name) inside <see cref="Folders"/>-style roots.</summary>
    public List<(string Folder, string Pattern)> FilePatterns { get; init; } = new();

    /// <summary>Special handling instead of deleting files.</summary>
    public string? Special { get; init; }   // "recyclebin" | "dns"

    public bool DefaultOn { get; init; } = true;
    public bool NeedsAdmin { get; init; }

    // filled by Scan
    public long Bytes { get; set; }
    public int Files { get; set; }
    public bool Scanned { get; set; }
}

public sealed record CleanReport(long FreedBytes, int Deleted, int Skipped, IReadOnlyList<string> Notes);

/// <summary>
/// Finds and removes temporary and cache files. Scan first, show the list, then Clean.
/// Only well-known cache locations are touched, never documents, downloads or desktop files.
/// Deleted files are gone for good (they are not sent to the Recycle Bin), which is why every run is previewed.
/// </summary>
public static class Cleaner
{
    private static string Env(string name) => Environment.GetEnvironmentVariable(name) ?? "";
    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string Win => Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    private static string WindowsOld => Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\", "Windows.old");
    private static string ProgData => Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    public static List<CleanTarget> Targets()
    {
        var list = new List<CleanTarget>
        {
            new() { Id = "user-temp", Name = "User temporary files", Description = "Files apps left in your temp folder.",
                    Folders = { Path.GetTempPath() } },
            new() { Id = "win-temp", Name = "Windows temporary files", Description = "Temporary files in the Windows folder.",
                    Folders = { Path.Combine(Win, "Temp") }, NeedsAdmin = true },
            new() { Id = "wu-cache", Name = "Windows Update download cache", Description = "Already-installed update downloads. Windows re-downloads what it needs.",
                    Folders = { Path.Combine(Win, "SoftwareDistribution", "Download") }, NeedsAdmin = true },
            new() { Id = "do-cache", Name = "Delivery Optimization cache", Description = "Update files cached for sharing with other PCs.",
                    Folders = { Path.Combine(Win, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization", "Cache") }, NeedsAdmin = true },
            new() { Id = "thumbs", Name = "Thumbnail and icon cache", Description = "Explorer rebuilds these the next time you open a folder.",
                    FilePatterns = { (Path.Combine(Local, "Microsoft", "Windows", "Explorer"), "thumbcache_*.db"),
                                     (Path.Combine(Local, "Microsoft", "Windows", "Explorer"), "iconcache_*.db") } },
            new() { Id = "wer", Name = "Error reports and crash dumps", Description = "Windows Error Reporting files and application crash dumps.",
                    Folders = { Path.Combine(ProgData, "Microsoft", "Windows", "WER", "ReportArchive"),
                                Path.Combine(ProgData, "Microsoft", "Windows", "WER", "ReportQueue"),
                                Path.Combine(Local, "CrashDumps"), Path.Combine(Win, "Minidump") },
                    FilePatterns = { (Win, "MEMORY.DMP") }, NeedsAdmin = true },
            new() { Id = "logs", Name = "Windows log files", Description = "Old servicing (CBS) logs.",
                    FilePatterns = { (Path.Combine(Win, "Logs", "CBS"), "*.log"), (Path.Combine(Win, "Logs", "CBS"), "*.cab") }, NeedsAdmin = true },
            new() { Id = "shader", Name = "DirectX shader cache", Description = "Games and the GPU driver rebuild it on demand. First launches may be slightly slower.",
                    Folders = { Path.Combine(Local, "D3DSCache") }, DefaultOn = false },
            new() { Id = "edge", Name = "Microsoft Edge cache", Description = "Cached web pages and code. Close Edge first. Passwords and history are not touched.",
                    Folders = { Path.Combine(Local, "Microsoft", "Edge", "User Data", "Default", "Cache"),
                                Path.Combine(Local, "Microsoft", "Edge", "User Data", "Default", "Code Cache"),
                                Path.Combine(Local, "Microsoft", "Edge", "User Data", "Default", "GPUCache") } },
            new() { Id = "chrome", Name = "Google Chrome cache", Description = "Cached web pages and code. Close Chrome first. Passwords and history are not touched.",
                    Folders = { Path.Combine(Local, "Google", "Chrome", "User Data", "Default", "Cache"),
                                Path.Combine(Local, "Google", "Chrome", "User Data", "Default", "Code Cache"),
                                Path.Combine(Local, "Google", "Chrome", "User Data", "Default", "GPUCache") } },
            new() { Id = "brave", Name = "Brave cache", Description = "Cached web pages and code. Close Brave first.",
                    Folders = { Path.Combine(Local, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache"),
                                Path.Combine(Local, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Code Cache") } },
            new() { Id = "firefox", Name = "Firefox cache", Description = "Cached web pages. Close Firefox first.",
                    Folders = FirefoxCaches() },
            new() { Id = "store-cache", Name = "Microsoft Store cache", Description = "Cached Store listings and images. The Store rebuilds them.",
                    Folders = { Path.Combine(Local, "Packages", "Microsoft.WindowsStore_8wekyb3d8bbwe", "LocalCache") } },
            new() { Id = "upgrade-logs", Name = "Windows setup and upgrade logs", Description = "Logs left behind by Windows installs and version upgrades.",
                    Folders = { Path.Combine(Win, "Panther") }, NeedsAdmin = true },
            new() { Id = "prefetch", Name = "Prefetch data", Description = "Windows rebuilds it over the next few launches, which are slower until it does. Not worth it on a hard drive.",
                    FilePatterns = { (Path.Combine(Win, "Prefetch"), "*.pf") }, DefaultOn = false, NeedsAdmin = true },
            new() { Id = "recent", Name = "Recent files and jump lists", Description = "Clears the recent-files list and taskbar jump lists for your account. Your actual files are untouched.",
                    Folders = { Path.Combine(Roaming, "Microsoft", "Windows", "Recent") }, DefaultOn = false },
            new() { Id = "recycle", Name = "Recycle Bin", Description = "Permanently deletes everything in the Recycle Bin on all drives.",
                    Special = "recyclebin", DefaultOn = false },
            new() { Id = "dns", Name = "DNS cache", Description = "Clears the resolver cache. Nothing is lost; it refills as you browse.",
                    Special = "dns" },
            new() { Id = "eventlogs", Name = "Windows event logs", Description = "Clears the event logs. Useful for privacy, but you lose the history used to diagnose problems.",
                    Special = "eventlogs", DefaultOn = false, NeedsAdmin = true },
            new() { Id = "componentstore", Name = "Superseded update files (component store)", Description = "Runs DISM to remove update files replaced by newer ones. Often frees several GB, and can take 10 to 30 minutes.",
                    Special = "componentstore", DefaultOn = false, NeedsAdmin = true },
            new() { Id = "windowsold", Name = "Previous Windows installation (Windows.old)", Description = "The old Windows kept after a version upgrade. Often 10 to 30 GB.",
                    Special = "windowsold", DefaultOn = false, NeedsAdmin = true },
        };

        // Chrome/Edge keep a cache per profile; "Default" covers the main profile.
        return list;
    }

    private static List<string> FirefoxCaches()
    {
        var result = new List<string>();
        try
        {
            var root = Path.Combine(Local, "Mozilla", "Firefox", "Profiles");
            if (Directory.Exists(root))
                foreach (var d in Directory.GetDirectories(root))
                    result.Add(Path.Combine(d, "cache2"));
        }
        catch { /* ignore */ }
        return result;
    }

    // ------------------------------------------------------------- safety

    /// <summary>Only paths below these roots may ever be deleted.</summary>
    private static IEnumerable<string> AllowedRoots()
    {
        yield return Path.GetFullPath(Path.GetTempPath());
        yield return Path.GetFullPath(Path.Combine(Win, "Temp"));
        yield return Path.GetFullPath(Path.Combine(Win, "SoftwareDistribution", "Download"));
        yield return Path.GetFullPath(Path.Combine(Win, "ServiceProfiles", "NetworkService", "AppData", "Local", "Microsoft", "Windows", "DeliveryOptimization"));
        yield return Path.GetFullPath(Path.Combine(Win, "Logs", "CBS"));
        yield return Path.GetFullPath(Path.Combine(Win, "Prefetch"));
        yield return Path.GetFullPath(Path.Combine(Win, "Panther"));
        yield return Path.GetFullPath(Path.Combine(Roaming, "Microsoft", "Windows", "Recent"));
        yield return Path.GetFullPath(Path.Combine(Win, "Minidump"));
        yield return Path.GetFullPath(Path.Combine(Win, "MEMORY.DMP"));
        yield return Path.GetFullPath(Path.Combine(ProgData, "Microsoft", "Windows", "WER"));
        yield return Path.GetFullPath(Local);
    }

    private static bool IsAllowed(string path)
    {
        var full = Path.GetFullPath(path);
        // The user's temp folder is a subfolder of LocalAppData, so "Local" is only allowed for the known cache sub-paths below.
        var local = Path.GetFullPath(Local) + Path.DirectorySeparatorChar;
        if (full.StartsWith(local, StringComparison.OrdinalIgnoreCase))
        {
            var rel = full[local.Length..];
            string[] okPrefixes =
            {
                "Temp", "CrashDumps", "D3DSCache", @"Microsoft\Windows\Explorer", @"Microsoft\Edge\User Data",
                @"Packages\Microsoft.WindowsStore_8wekyb3d8bbwe\LocalCache",
                @"Google\Chrome\User Data", @"BraveSoftware\Brave-Browser\User Data", @"Mozilla\Firefox\Profiles"
            };
            return okPrefixes.Any(p => rel.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        return AllowedRoots().Where(r => !r.Equals(Path.GetFullPath(Local), StringComparison.OrdinalIgnoreCase))
            .Any(r => full.StartsWith(r, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsReparsePoint(FileSystemInfo fi) => (fi.Attributes & FileAttributes.ReparsePoint) != 0;

    // --------------------------------------------------------------- scan

    public static void Scan(CleanTarget t)
    {
        long bytes = 0; var files = 0;
        foreach (var f in EnumerateFiles(t))
        {
            try { bytes += new FileInfo(f).Length; files++; } catch { /* vanished or locked */ }
        }

        if (t.Special == "windowsold" && Directory.Exists(WindowsOld))
            (bytes, files) = FolderSize(WindowsOld);

        if (t.Special == "recyclebin")
        {
            var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
            if (SHQueryRecycleBin(null, ref info) == 0) { bytes = info.i64Size; files = (int)Math.Min(int.MaxValue, info.i64NumItems); }
        }

        t.Bytes = bytes; t.Files = files; t.Scanned = true;
        Log.Info($"Cleaner scan: {t.Name}: {files} file(s), {bytes / 1024.0 / 1024.0:F1} MB");
    }

    /// <summary>Best-effort size of a folder tree, skipping anything unreadable.</summary>
    public static (long Bytes, int Files) FolderSize(string root)
    {
        long bytes = 0; var files = 0;
        foreach (var f in Walk(root))
        {
            try { bytes += new FileInfo(f).Length; files++; } catch { /* locked or gone */ }
        }
        return (bytes, files);
    }

    private static IEnumerable<string> EnumerateFiles(CleanTarget t)
    {
        foreach (var folder in t.Folders)
        {
            if (!Directory.Exists(folder) || !IsAllowed(folder)) continue;
            foreach (var f in Walk(folder)) yield return f;
        }

        foreach (var (folder, pattern) in t.FilePatterns)
        {
            if (!Directory.Exists(folder)) continue;
            string[] found;
            try { found = Directory.GetFiles(folder, pattern); } catch { continue; }
            foreach (var f in found.Where(IsAllowed)) yield return f;
        }
    }

    private static IEnumerable<string> Walk(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] files = Array.Empty<string>(), dirs = Array.Empty<string>();
            try { files = Directory.GetFiles(dir); } catch { }
            try { dirs = Directory.GetDirectories(dir); } catch { }
            foreach (var f in files) yield return f;
            foreach (var d in dirs)
            {
                try { if (IsReparsePoint(new DirectoryInfo(d))) continue; } catch { continue; } // never follow links
                stack.Push(d);
            }
        }
    }

    // -------------------------------------------------------------- clean

    public static CleanReport Clean(IEnumerable<CleanTarget> targets, Action<string>? progress = null)
    {
        long freed = 0; int deleted = 0, skipped = 0;
        var notes = new List<string>();

        foreach (var t in targets)
        {
            progress?.Invoke(t.Name);
            Log.Info($"Cleaning: {t.Name}");

            if (t.Special == "dns")
            {
                var (code, _) = Shell.Run("ipconfig.exe", "/flushdns");
                if (code != 0) notes.Add("Could not flush the DNS cache.");
                continue;
            }
            if (t.Special == "eventlogs")
            {
                var (code, output) = Shell.PowerShell(
                    "$cleared = 0; $failed = 0; " +
                    "wevtutil el | ForEach-Object { try { wevtutil cl \"$_\" 2>$null; $cleared++ } catch { $failed++ } }; " +
                    "Write-Output \"cleared=$cleared failed=$failed\"", 300_000);
                notes.Add(code == 0 ? "Event logs: " + output.Trim() : "Could not clear the event logs.");
                continue;
            }

            if (t.Special == "componentstore")
            {
                var (code, output) = Shell.Run("Dism.exe", "/Online /Cleanup-Image /StartComponentCleanup /Quiet", 1_800_000);
                notes.Add(code == 0
                    ? "Component store cleaned. The space it freed is not counted in the figure above."
                    : $"DISM component cleanup failed (exit {code}): {Head(output)}");
                continue;
            }

            if (t.Special == "windowsold")
            {
                var old = WindowsOld;
                if (!Directory.Exists(old)) { notes.Add("There is no Windows.old folder."); continue; }

                var size = t.Bytes;
                var script =
                    $"$p = '{old}'; " +
                    "takeown /f $p /r /d Y | Out-Null; " +
                    "icacls $p /grant *S-1-5-32-544:F /t /c /q | Out-Null; " +
                    "Remove-Item -LiteralPath $p -Recurse -Force -ErrorAction SilentlyContinue; " +
                    "if (Test-Path $p) { Write-Output 'partial' } else { Write-Output 'gone' }";
                var (code, output) = Shell.PowerShell(script, 1_800_000);
                if (code == 0 && output.Contains("gone"))
                {
                    freed += size;
                    notes.Add("Windows.old removed. Rolling back to the previous Windows version is no longer possible.");
                }
                else
                {
                    notes.Add("Windows.old was only partly removed; some of it is protected by Windows. Disk Cleanup's \"Previous Windows installation(s)\" option can finish the job.");
                }
                continue;
            }

            if (t.Special == "recyclebin")
            {
                var hr = SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4); // no confirmation, no progress UI, no sound
                if (hr == 0) { freed += t.Bytes; deleted += t.Files; Log.Info("  Recycle Bin emptied"); }
                else notes.Add("Recycle Bin: nothing emptied (it may already be empty).");
                continue;
            }

            foreach (var f in EnumerateFiles(t).ToList())
            {
                try
                {
                    if (!IsAllowed(f)) { skipped++; continue; }
                    var fi = new FileInfo(f);
                    var len = fi.Length;
                    if (fi.IsReadOnly) fi.IsReadOnly = false;
                    fi.Delete();
                    freed += len; deleted++;
                }
                catch
                {
                    skipped++; // in use or no permission; normal for temp files
                }
            }

            foreach (var folder in t.Folders) RemoveEmptyDirs(folder, keepRoot: true);
        }

        Log.Info($"Cleaner finished: {deleted} file(s) deleted, {freed / 1024.0 / 1024.0:F1} MB freed, {skipped} skipped");
        return new CleanReport(freed, deleted, skipped, notes);
    }

    private static void RemoveEmptyDirs(string root, bool keepRoot)
    {
        if (!Directory.Exists(root) || !IsAllowed(root)) return;
        try
        {
            foreach (var d in Directory.GetDirectories(root))
            {
                try { if (IsReparsePoint(new DirectoryInfo(d))) continue; } catch { continue; }
                RemoveEmptyDirs(d, keepRoot: false);
            }
            if (!keepRoot && !Directory.EnumerateFileSystemEntries(root).Any())
                Directory.Delete(root);
        }
        catch { /* in use */ }
    }

    private static string Head(string s) => s.Length == 0 ? "" : s.Split('\n')[0].Trim();

    public static string FormatBytes(long b)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" };
        double v = b; var i = 0;
        while (v >= 1024 && i < u.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{v:F0} {u[i]}" : $"{v:F1} {u[i]}";
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);
}
