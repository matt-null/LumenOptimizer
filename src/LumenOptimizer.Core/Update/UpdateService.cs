using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Update;

public sealed record UpdateInfo(Version Latest, string Tag, string Notes, string AssetName, string AssetUrl, long AssetSize, string PageUrl);

public sealed record UpdateCheck(bool Configured, bool Failed, UpdateInfo? Update, string Message);

/// <summary>Checks the GitHub "latest release" and can download + apply the portable zip. Nothing happens without the user agreeing.</summary>
public static class UpdateService
{
    private static readonly HttpClient Http = Make();

    private static HttpClient Make()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("LumenOptimizer-Updater");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    public static Version Parse(string v)
    {
        v = (v ?? "").Trim().TrimStart('v', 'V');
        var dash = v.IndexOfAny(new[] { '-', '+' });
        if (dash >= 0) v = v[..dash];
        return Version.TryParse(v, out var r) ? r : new Version(0, 0);
    }

    public static async Task<UpdateCheck> CheckAsync(string owner, string repo, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
            return new UpdateCheck(false, false, null, "Updates are not set up yet.");
        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            using var resp = await Http.GetAsync(url);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return new UpdateCheck(true, false, null, "No release has been published yet.");
            resp.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            var latest = Parse(tag);
            var notes = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";
            var page = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";

            string aName = "", aUrl = ""; long aSize = 0;
            if (root.TryGetProperty("assets", out var assets))
                foreach (var a in assets.EnumerateArray())
                {
                    var n = a.GetProperty("name").GetString() ?? "";
                    if (n.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && n.Contains("portable", StringComparison.OrdinalIgnoreCase))
                    {
                        aName = n; aUrl = a.GetProperty("browser_download_url").GetString() ?? "";
                        aSize = a.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                        break;
                    }
                }

            if (latest <= Parse(currentVersion))
                return new UpdateCheck(true, false, null, $"You are up to date (version {currentVersion}).");
            var info = new UpdateInfo(latest, tag, notes, aName, aUrl, aSize, page);
            return new UpdateCheck(true, false, info, $"Version {latest} is available.");
        }
        catch (Exception ex)
        {
            Log.Warn("Update check failed: " + ex.Message);
            return new UpdateCheck(true, true, null, "Could not check for updates (are you online?).");
        }
    }

    /// <summary>Downloads and unpacks the zip, then writes the swap script. Returns the script path; call Launch to apply it.</summary>
    public static async Task<string> PrepareAsync(UpdateInfo info, string owner, string repo, IProgress<double>? progress = null)
    {
        var prefix = $"https://github.com/{owner}/{repo}/releases/download/";
        if (string.IsNullOrEmpty(info.AssetUrl) || !info.AssetUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This release has no downloadable portable zip.");

        var work = Path.Combine(Path.GetTempPath(), "LumenOptimizer-update");
        if (Directory.Exists(work)) Directory.Delete(work, true);
        Directory.CreateDirectory(work);
        var zip = Path.Combine(work, "update.zip");

        using (var dl = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
        {
            dl.DefaultRequestHeaders.UserAgent.ParseAdd("LumenOptimizer-Updater");
            using var resp = await dl.GetAsync(info.AssetUrl, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? info.AssetSize;
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = File.Create(zip);
            var buf = new byte[81920]; long done = 0; int n;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n));
                done += n;
                if (total > 0) progress?.Report((double)done / total);
            }
        }

        if (info.AssetSize > 0 && new FileInfo(zip).Length != info.AssetSize)
            throw new InvalidOperationException("The download is incomplete. Try again.");

        var extract = Path.Combine(work, "new");
        ZipFile.ExtractToDirectory(zip, extract);
        // Zips made by build.ps1 have files at the top; tolerate a single wrapping folder too.
        var dirs = Directory.GetDirectories(extract); var files = Directory.GetFiles(extract);
        if (files.Length == 0 && dirs.Length == 1) extract = dirs[0];
        var exeName = Path.GetFileName(Environment.ProcessPath ?? "LumenOptimizer.exe");
        if (!File.Exists(Path.Combine(extract, exeName)))
            throw new InvalidOperationException($"The update zip does not contain {exeName}.");

        var appDir = AppContext.BaseDirectory.TrimEnd('\\');
        var pid = Environment.ProcessId;
        var script = Path.Combine(work, "apply.cmd");
        File.WriteAllText(script,
$@"@echo off
:wait
tasklist /FI ""PID eq {pid}"" 2>nul | find ""{pid}"" >nul
if not errorlevel 1 (
  timeout /t 1 /nobreak >nul
  goto wait
)
xcopy ""{extract}\*"" ""{appDir}\"" /E /Y /I /Q >nul
start """" ""{Path.Combine(appDir, exeName)}""
");
        Log.Info($"Update {info.Tag} downloaded and ready.");
        return script;
    }

    public static void Launch(string script)
    {
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { CreateNoWindow = true, UseShellExecute = false });
    }
}
