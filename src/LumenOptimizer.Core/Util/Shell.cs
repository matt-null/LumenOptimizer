using System.Diagnostics;
using System.Text;

namespace LumenOptimizer.Core.Util;

public static class Shell
{
    /// <param name="display">What to show in the console instead of the raw command line.</param>
    /// <param name="logOutput">false for noisy read-only queries; failures are always logged.</param>
    public static (int ExitCode, string Output) Run(string fileName, string arguments, int timeoutMs = 120_000,
        string? display = null, bool logOutput = true)
    {
        Log.Info("> " + (display ?? $"{fileName} {arguments}"));

        var psi = new ProcessStartInfo(fileName, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {fileName}");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();

        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(true); } catch { /* already gone */ }
            Log.Error("  timed out");
            return (-1, "Timed out");
        }

        p.WaitForExit();
        var output = (stdout.Result + stderr.Result).Trim();
        LogResult(p.ExitCode, output, logOutput);
        return (p.ExitCode, output);
    }

    private static void LogResult(int code, string output, bool logOutput)
    {
        var text = code == 0 ? "  exit 0" : "  exit " + code;
        if (code == 0) Log.Info(text); else Log.Warn(text);

        if (output.Length == 0 || (code == 0 && !logOutput)) return;
        var shown = output.Length > 1500 ? output[..1500] + " …(truncated)" : output;
        foreach (var l in shown.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(40))
            Log.Info("  " + l);
    }

    /// <summary>Runs a script in Windows PowerShell 5.1 using -EncodedCommand (no quoting problems).</summary>
    public static (int ExitCode, string Output) PowerShell(string script, int timeoutMs = 180_000, bool logOutput = true)
    {
        var full = "$ProgressPreference='SilentlyContinue'\n" + script;
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(full));
        var preview = script.Trim().Replace("\r", "");
        if (preview.Length > 300) preview = preview[..300] + " …";
        return Run("powershell.exe",
            $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}", timeoutMs,
            "powershell: " + preview.Replace("\n", " ; "), logOutput);
    }
}
