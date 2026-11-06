namespace LumenOptimizer.Core.Util;

/// <summary>Runs a batch script as the SYSTEM account through a one-off scheduled task. This is how locked or protected files get removed.</summary>
public static class SystemRun
{
    public static bool Run(string batchBody, int timeoutSec = 120)
    {
        var dir = Path.Combine(AppPaths.DataDir, "system");
        Directory.CreateDirectory(dir);
        var id = Guid.NewGuid().ToString("N")[..8];
        var bat = Path.Combine(dir, id + ".cmd");
        var res = Path.Combine(dir, id + ".res");
        var task = "LumenSystem_" + id;
        File.WriteAllText(bat, "@echo off\r\n" + batchBody + "\r\necho done> \"" + res + "\"\r\n");
        try
        {
            Log.Info("Running a step as SYSTEM...");
            var c = Shell.Run("schtasks.exe", $"/create /tn {task} /tr \"\\\"{bat}\\\"\" /sc once /st 00:00 /ru SYSTEM /rl HIGHEST /f", 30_000, logOutput: false);
            if (c.ExitCode != 0) return false;
            Shell.Run("schtasks.exe", $"/run /tn {task}", 30_000, logOutput: false);
            var end = DateTime.UtcNow.AddSeconds(timeoutSec);
            while (DateTime.UtcNow < end && !File.Exists(res)) Thread.Sleep(300);
            return File.Exists(res);
        }
        finally
        {
            Shell.Run("schtasks.exe", $"/delete /tn {task} /f", 30_000, logOutput: false);
            try { File.Delete(bat); File.Delete(res); } catch { }
        }
    }
}
