using LumenOptimizer.Core.Util;

namespace LumenOptimizer.Core.Tools;

public sealed record DiskVolume(string Letter, string Label, string Fs, double SizeGb, double FreeGb, string Media, string Bus, string Type)
{
    public bool IsFlash => Type == "Removable" || Bus is "USB" or "SD" or "MMC" || Media == "Unspecified" && Bus != "";
    public bool IsSsd => Media == "SSD";
}

public static class DiskOptimizer
{
    public static List<DiskVolume> List() => Ps.Json(@"
$out=@()
Get-Volume | Where-Object { $_.DriveLetter -and ($_.DriveType -eq 'Fixed' -or $_.DriveType -eq 'Removable') } | ForEach-Object {
  $mt='Unknown'; $bus=''
  try { $d = Get-Partition -DriveLetter $_.DriveLetter -ErrorAction Stop | Get-Disk | Get-PhysicalDisk -ErrorAction Stop; $mt=[string]$d.MediaType; $bus=[string]$d.BusType } catch {}
  $out += [pscustomobject]@{Letter=[string]$_.DriveLetter;Label=$_.FileSystemLabel;FS=$_.FileSystem;SizeGB=[math]::Round($_.Size/1GB,1);FreeGB=[math]::Round($_.SizeRemaining/1GB,1);Media=$mt;Bus=$bus;Type=[string]$_.DriveType}
}
ConvertTo-Json -InputObject $out -Compress", 90_000)
        .Select(e => new DiskVolume(e.S("Letter"), e.S("Label"), e.S("FS"), double.Parse(e.S("SizeGB"), System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(e.S("FreeGB"), System.Globalization.CultureInfo.InvariantCulture), e.S("Media"), e.S("Bus"), e.S("Type")))
        .ToList();

    public static string Analyze(DiskVolume v)
    {
        var r = Shell.Run("defrag.exe", $"{v.Letter}: /A /V", 600_000);
        return r.Output.Length > 600 ? r.Output[^600..] : r.Output;
    }

    /// <summary>Windows picks the right job: defragment a hard disk, TRIM an SSD. USB sticks and SD cards are never defragmented.</summary>
    public static string Optimize(DiskVolume v)
    {
        if (v.IsFlash)
            throw new InvalidOperationException("This is a USB or SD flash drive. Defragmenting it only wears it out and helps nothing, so it is skipped.");
        var r = Shell.Run("defrag.exe", $"{v.Letter}: /O /U /V", 4 * 3600_000);
        if (r.ExitCode != 0) throw new InvalidOperationException(r.Output);
        return v.IsSsd ? "TRIM finished (SSD)." : "Optimization finished.";
    }

    public static string CheckDisk(DiskVolume v)
    {
        var r = Shell.Run("chkdsk.exe", $"{v.Letter}:", 3600_000);
        return r.Output.Length > 500 ? r.Output[^500..] : r.Output;
    }
}
