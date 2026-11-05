using System.Diagnostics;
using System.Runtime.InteropServices;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

public sealed record MemoryStatus(
    long TotalBytes, long AvailableBytes, long CachedBytes,
    long CommitTotalBytes, long CommitLimitBytes, int ProcessCount, int LoadPercent)
{
    public long InUseBytes => Math.Max(0, TotalBytes - AvailableBytes);
}

public sealed record ProcessMemory(int Pid, string Name, long WorkingSetBytes);

/// <summary>
/// Reads real memory figures and performs the three memory operations Windows actually exposes.
/// None of this is a magic speed-up: Windows manages RAM well, and "free" RAM is wasted RAM.
/// Each action says plainly what it does and when it is worth doing, so the UI can warn instead of promise.
/// </summary>
public static class Memory
{
    // ------------------------------------------------------------- reading

    public static MemoryStatus Status()
    {
        var ms = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        long total = 0, avail = 0; var load = 0;
        if (GlobalMemoryStatusEx(ref ms))
        {
            total = (long)ms.ullTotalPhys;
            avail = (long)ms.ullAvailPhys;
            load = (int)ms.dwMemoryLoad;
        }

        long cached = 0, commit = 0, limit = 0; var procs = 0;
        var pi = new PERFORMANCE_INFORMATION { cb = (uint)Marshal.SizeOf<PERFORMANCE_INFORMATION>() };
        if (GetPerformanceInfo(out pi, pi.cb))
        {
            var page = (long)(ulong)pi.PageSize;
            cached = (long)(ulong)pi.SystemCache * page;
            commit = (long)(ulong)pi.CommitTotal * page;
            limit = (long)(ulong)pi.CommitLimit * page;
            procs = (int)pi.ProcessCount;
        }

        return new MemoryStatus(total, avail, cached, commit, limit, procs, load);
    }

    /// <summary>The biggest memory users, so the user can decide what to close themselves.</summary>
    public static List<ProcessMemory> TopProcesses(int count = 15)
    {
        var list = new List<ProcessMemory>();
        foreach (var p in Process.GetProcesses())
        {
            try { list.Add(new ProcessMemory(p.Id, p.ProcessName, p.WorkingSet64)); }
            catch { /* exited or no access */ }
            finally { p.Dispose(); }
        }
        return list.OrderByDescending(p => p.WorkingSetBytes).Take(count).ToList();
    }

    // ------------------------------------------------------------- actions

    /// <summary>
    /// Drops the standby list: file data Windows is keeping cached in otherwise unused RAM.
    /// Frees the "cached" figure, and the next read of those files comes from disk again.
    /// </summary>
    public static long PurgeStandbyList()
    {
        var before = Status();
        Run(MemoryPurgeStandbyList, "purge standby list", "SeProfileSingleProcessPrivilege");
        return Freed(before);
    }

    /// <summary>
    /// Writes modified (dirty) pages out so they become reclaimable. This causes disk writes.
    /// </summary>
    public static long FlushModifiedList()
    {
        var before = Status();
        Run(MemoryFlushModifiedList, "flush modified page list", "SeProfileSingleProcessPrivilege");
        return Freed(before);
    }

    /// <summary>
    /// Trims every process's working set. Apps keep running but must re-read their pages from disk
    /// as soon as they are used again, so this usually costs performance rather than gaining it.
    /// </summary>
    public static long TrimAllWorkingSets()
    {
        var before = Status();
        Run(MemoryEmptyWorkingSets, "trim all working sets", "SeIncreaseQuotaPrivilege");
        return Freed(before);
    }

    private static long Freed(MemoryStatus before)
    {
        System.Threading.Thread.Sleep(400); // the figures settle a moment after the call
        var after = Status();
        var freed = after.AvailableBytes - before.AvailableBytes;
        Log.Info($"  available RAM {Cleaner.FormatBytes(before.AvailableBytes)} -> {Cleaner.FormatBytes(after.AvailableBytes)}");
        return Math.Max(0, freed);
    }

    private static void Run(int command, string what, string privilege)
    {
        Log.Info($"Memory: {what}");
        if (!EnablePrivilege(privilege))
            Log.Warn($"  could not enable {privilege}; the call may fail");

        var value = command;
        var status = NtSetSystemInformation(SystemMemoryListInformation, ref value, sizeof(int));
        if (status != 0)
            throw new InvalidOperationException(
                $"Windows refused the request (NTSTATUS 0x{status:X8}). This needs administrator rights, and some builds block it.");
    }

    // -------------------------------------------------------- native calls

    private const int SystemMemoryListInformation = 0x0050;
    private const int MemoryEmptyWorkingSets = 2;
    private const int MemoryFlushModifiedList = 3;
    private const int MemoryPurgeStandbyList = 4;

    private const int SE_PRIVILEGE_ENABLED = 0x0002;
    private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    private const uint TOKEN_QUERY = 0x0008;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PERFORMANCE_INFORMATION
    {
        public uint cb;
        public nuint CommitTotal;
        public nuint CommitLimit;
        public nuint CommitPeak;
        public nuint PhysicalTotal;
        public nuint PhysicalAvailable;
        public nuint SystemCache;
        public nuint KernelTotal;
        public nuint KernelPaged;
        public nuint KernelNonpaged;
        public nuint PageSize;
        public uint HandleCount;
        public uint ProcessCount;
        public uint ThreadCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct TOKEN_PRIVILEGES { public int PrivilegeCount; public LUID Luid; public int Attributes; }

    private static bool EnablePrivilege(string name)
    {
        var token = IntPtr.Zero;
        try
        {
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token)) return false;
            if (!LookupPrivilegeValue(null, name, out var luid)) return false;
            var tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Luid = luid, Attributes = SE_PRIVILEGE_ENABLED };
            if (!AdjustTokenPrivileges(token, false, ref tp, Marshal.SizeOf<TOKEN_PRIVILEGES>(), IntPtr.Zero, IntPtr.Zero))
                return false;
            return Marshal.GetLastWin32Error() == 0;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (token != IntPtr.Zero) CloseHandle(token);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, uint cb);

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(int systemInformationClass, ref int systemInformation, int systemInformationLength);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LookupPrivilegeValue(string? systemName, string name, out LUID luid);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges,
        ref TOKEN_PRIVILEGES newState, int bufferLength, IntPtr previousState, IntPtr returnLength);
}
