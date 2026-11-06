using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

public sealed record JunkItem(string Path, bool IsDir, long Size);

/// <summary>Junk that OS and apps leave on USB sticks and SD cards. Only drives Windows reports as removable are touched.</summary>
public static class RemovableCleaner
{
    private static readonly string[] FileNames = { "Thumbs.db", "ehthumbs.db", ".DS_Store", "autorun.inf" };
    private static readonly string[] DirNames = { "$RECYCLE.BIN", ".Trashes", ".Spotlight-V100", ".fseventsd", ".TemporaryItems", ".Trash" };

    public const string Categories = "Thumbs.db / ehthumbs.db, .DS_Store and ._* Mac leftovers, ~$ Office lock files, *.tmp, autorun.inf, Recycle Bin and Mac trash/index folders, and FOUND.000 recovery folders. Your own files are not touched.";

    private static string FlagFile => Path.Combine(AppPaths.DataDir, "no-usb-prompt");
    public static bool PromptEnabled { get => !File.Exists(FlagFile); set { Directory.CreateDirectory(AppPaths.DataDir); if (value) File.Delete(FlagFile); else File.WriteAllText(FlagFile, "1"); } }

    public static List<DriveInfo> Drives() =>
        DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Removable && d.IsReady).ToList();

    private static bool IsJunkFile(string n) =>
        FileNames.Contains(n, StringComparer.OrdinalIgnoreCase) || n.StartsWith("._") || n.StartsWith("~$") || n.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
    private static bool IsJunkDir(string n) =>
        DirNames.Contains(n, StringComparer.OrdinalIgnoreCase) || System.Text.RegularExpressions.Regex.IsMatch(n, @"^FOUND\.\d{3}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public static List<JunkItem> Scan(string root)
    {
        var res = new List<JunkItem>();
        void Walk(string dir)
        {
            try
            {
                foreach (var d in Directory.EnumerateDirectories(dir))
                {
                    var n = Path.GetFileName(d);
                    if (n.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase)) continue;
                    if (IsJunkDir(n)) res.Add(new JunkItem(d, true, DirSize(d)));
                    else Walk(d);
                }
                foreach (var f in Directory.EnumerateFiles(dir))
                    if (IsJunkFile(Path.GetFileName(f)))
                        try { res.Add(new JunkItem(f, false, new FileInfo(f).Length)); } catch { }
            }
            catch { /* unreadable folder */ }
        }
        Walk(root);
        return res;
    }

    private static long DirSize(string d)
    {
        long t = 0;
        try { foreach (var f in new DirectoryInfo(d).EnumerateFiles("*", SearchOption.AllDirectories)) t += f.Length; } catch { }
        return t;
    }

    public static string Clean(string root)
    {
        if (!new DriveInfo(root).DriveType.Equals(DriveType.Removable)) throw new InvalidOperationException("That is not a removable drive.");
        var items = Scan(root);
        long freed = 0; int n = 0;
        foreach (var i in items)
        {
            try
            {
                if (i.IsDir) { foreach (var f in Directory.EnumerateFiles(i.Path, "*", SearchOption.AllDirectories)) try { File.SetAttributes(f, FileAttributes.Normal); } catch { } Directory.Delete(i.Path, true); }
                else { File.SetAttributes(i.Path, FileAttributes.Normal); File.Delete(i.Path); }
                freed += i.Size; n++;
            }
            catch (Exception ex) { Log.Warn($"  could not remove {i.Path}: {ex.Message}"); }
        }
        Log.Info($"Removable cleaner: removed {n} of {items.Count} items from {root}");
        return $"Removed {n} of {items.Count} junk items, freed {freed / 1024.0 / 1024.0:0.#} MB.";
    }
}

/// <summary>Polls for newly plugged-in removable drives (works without extra packages).</summary>
public sealed class RemovableWatcher : IDisposable
{
    private readonly Timer _timer;
    private HashSet<string> _known;
    public event Action<DriveInfo>? DriveArrived;

    public RemovableWatcher()
    {
        _known = RemovableCleaner.Drives().Select(d => d.Name).ToHashSet();
        _timer = new Timer(_ => Poll(), null, 3000, 3000);
    }

    private void Poll()
    {
        try
        {
            var now = RemovableCleaner.Drives();
            foreach (var d in now.Where(d => !_known.Contains(d.Name))) DriveArrived?.Invoke(d);
            _known = now.Select(d => d.Name).ToHashSet();
        }
        catch { }
    }

    public void Dispose() => _timer.Dispose();
}
