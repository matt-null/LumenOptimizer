using System.Security.Principal;

namespace LumenOptimizer.Core.Util;

public static class AppPaths
{
    public static string DataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "LumenOptimizer");

    /// <summary>Data folder used before the app was renamed (Optimizer11). Backups are copied from here once.</summary>
    public static string LegacyDataDir { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Optimizer11");

    public static string LogDir => Path.Combine(DataDir, "logs");
}

public static class Elevation
{
    public static bool IsAdministrator()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }
}

public static class Log
{
    private static readonly object Gate = new();
    private static readonly List<string> Buffer = new();
    private const int MaxLines = 5000;

    /// <summary>Raised for every log line (any thread). The live console window subscribes to this.</summary>
    public static event Action<string>? LineWritten;

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    /// <summary>Everything logged since the app started (last 5000 lines), for a console opened late.</summary>
    public static string[] History()
    {
        lock (Gate) return Buffer.ToArray();
    }

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss} [{level}] {message}";
        try
        {
            lock (Gate)
            {
                Buffer.Add(line);
                if (Buffer.Count > MaxLines) Buffer.RemoveRange(0, Buffer.Count - MaxLines);
                Directory.CreateDirectory(AppPaths.LogDir);
                var file = Path.Combine(AppPaths.LogDir, $"{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, line + Environment.NewLine);
            }
        }
        catch
        {
            // logging must never crash the app
        }

        try { LineWritten?.Invoke(line); }
        catch { /* a broken listener must not break logging */ }
    }
}
