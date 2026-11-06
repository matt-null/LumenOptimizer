using LumenOptimizer.Core.Tools;

namespace LumenOptimizer.UI;

public sealed class Row
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public string Status { get; set; } = "";
    public object? Data { get; set; }

    public Row() { }
    public Row(string id, string title, string subtitle, string status, object? data = null)
    { Id = id; Title = title; Subtitle = subtitle; Status = status; Data = data; }
}

/// <param name="Run">Receives the selected row (null if none needed) and the typed text (if Prompt is set). Returns the message to show; throws on failure.</param>
/// <param name="Confirm">Shown before running. {name} is replaced by the row title. Empty = no confirmation.</param>
public sealed record RowAction(string Label, Func<Row?, string?, string> Run, string Confirm = "", bool NeedsRow = true,
    string Prompt = "", bool Secret = false);

public abstract class ManagerSource
{
    public abstract string Title { get; }
    public abstract string Description { get; }
    public virtual bool ShowList => true;
    public virtual string EmptyText => "Nothing found.";
    public abstract List<Row> Load();
    public abstract IReadOnlyList<RowAction> Actions { get; }

    public static ManagerSource Create(string key) => key switch
    {
        "Services" => new ServicesSource(),
        "Tasks" => new TasksSource(),
        "Accounts" => new AccountsSource(),
        "ForceDelete" => new ForceDeleteSource(),
        "Disks" => new DisksSource(),
        "UsbClean" => new UsbSource(),
        _ => throw new ArgumentException(key)
    };
}

public sealed class ServicesSource : ManagerSource
{
    public override string Title => "Services";
    public override string Description =>
        "Background services Windows runs. Pick one, then change how it starts. The original start type is saved the first time you change a service, and 'Restore original' puts it back. Core Windows services cannot be disabled or stopped.";
    public override List<Row> Load() => ServicesTool.List()
        .Select(s => new Row(s.Name, s.Display, s.Name + (string.IsNullOrWhiteSpace(s.Description) ? "" : "  -  " + s.Description),
            $"{s.State} / {s.StartMode}" + (s.Critical ? " / core" : ""))).ToList();
    public override IReadOnlyList<RowAction> Actions { get; } = new RowAction[]
    {
        new("Automatic", (r, _) => ServicesTool.SetStartMode(r!.Id, "Auto")),
        new("Manual", (r, _) => ServicesTool.SetStartMode(r!.Id, "Manual")),
        new("Disable", (r, _) => ServicesTool.SetStartMode(r!.Id, "Disabled"), "Disable {name}? It will not start with Windows. You can restore it at any time."),
        new("Start", (r, _) => ServicesTool.StartStop(r!.Id, true)),
        new("Stop", (r, _) => ServicesTool.StartStop(r!.Id, false), "Stop {name} now?"),
        new("Restore original", (r, _) => ServicesTool.Restore(r!.Id)),
    };
}

public sealed class TasksSource : ManagerSource
{
    public override string Title => "Scheduled tasks";
    public override string Description =>
        "Tasks Windows and programs run on a schedule (telemetry, updaters, maintenance). Disabling is reversible. Built-in Windows tasks cannot be deleted, only disabled.";
    public override List<Row> Load() => TaskSchedulerTool.List()
        .Select(t => new Row(t.Id, t.Name, t.Path + (string.IsNullOrWhiteSpace(t.Author) ? "" : "  -  " + t.Author), t.State, t)).ToList();
    public override IReadOnlyList<RowAction> Actions { get; } = new RowAction[]
    {
        new("Disable", (r, _) => TaskSchedulerTool.Disable((TaskRow)r!.Data!), "Disable {name}? It stops running until you enable it again."),
        new("Enable", (r, _) => TaskSchedulerTool.Enable((TaskRow)r!.Data!)),
        new("Run now", (r, _) => TaskSchedulerTool.RunNow((TaskRow)r!.Data!)),
        new("Delete", (r, _) => TaskSchedulerTool.Delete((TaskRow)r!.Data!), "Permanently delete {name}? This cannot be undone."),
    };
}

public sealed class AccountsSource : ManagerSource
{
    public override string Title => "Local accounts";
    public override string Description =>
        "Reset a forgotten local password, unlock a locked-out account, enable or disable it, or change administrator rights. This works on this PC's local accounts while you are signed in as an administrator. Microsoft-account sign-ins are reset at account.live.com instead.";
    public override List<Row> Load() => Accounts.List()
        .Select(a => new Row(a.Name, a.Name, a.Sid, string.Join(" / ", new[] { a.Disabled ? "disabled" : "active", a.Locked ? "LOCKED" : null, a.IsAdmin ? "administrator" : null }.Where(x => x != null)))).ToList();
    public override IReadOnlyList<RowAction> Actions { get; } = new RowAction[]
    {
        new("Set new password", (r, p) => Accounts.SetPassword(r!.Id, p ?? ""), "Set a new password for {name}? Encrypted (EFS) files and saved credentials for that account may stop working.", true, "New password", true),
        new("Clear password", (r, _) => Accounts.SetPassword(r!.Id, ""), "Remove the password from {name}? Anyone could sign in to it."),
        new("Unlock", (r, _) => Accounts.Unlock(r!.Id)),
        new("Enable", (r, _) => Accounts.SetEnabled(r!.Id, true)),
        new("Disable", (r, _) => Accounts.SetEnabled(r!.Id, false), "Disable {name}?"),
        new("Make administrator", (r, _) => Accounts.SetAdmin(r!.Id, true), "Give {name} administrator rights?"),
        new("Remove administrator", (r, _) => Accounts.SetAdmin(r!.Id, false), "Remove administrator rights from {name}?"),
    };
}

public sealed class ForceDeleteSource : ManagerSource
{
    public override string Title => "Force delete";
    public override string Description =>
        "Removes a file or folder that will not delete. It takes ownership, grants access, ends programs running from inside it, retries as the SYSTEM account, and as a last resort removes it at the next restart. The Windows folder, whole drives, your user profile and this app are protected. Deleted items do NOT go to the Recycle Bin.";
    public override bool ShowList => false;
    public override List<Row> Load() => new();
    public override IReadOnlyList<RowAction> Actions { get; } = new RowAction[]
    {
        new("Force delete a file or folder...", (_, p) => ForceDelete.Delete(p ?? ""),
            "Permanently delete this path? It cannot be undone.", false, "Full path, e.g. C:\\Temp\\stuck-folder"),
    };
}

public sealed class DisksSource : ManagerSource
{
    public override string Title => "Optimize drives";
    public override string Description =>
        "Defragment hard disks and TRIM SSDs using Windows' own optimizer. USB sticks and SD cards are never defragmented because it only wears them out. A hard-disk optimize can take a long time; leave the app open.";
    public override List<Row> Load() => DiskOptimizer.List()
        .Select(v => new Row(v.Letter, $"{v.Letter}:  {v.Label}", $"{v.Fs}  -  {v.FreeGb} GB free of {v.SizeGb} GB",
            $"{(v.IsFlash ? "USB/SD flash" : v.Media)}", v)).ToList();
    public override IReadOnlyList<RowAction> Actions { get; } = new RowAction[]
    {
        new("Analyze", (r, _) => DiskOptimizer.Analyze((DiskVolume)r!.Data!)),
        new("Optimize (defrag / TRIM)", (r, _) => DiskOptimizer.Optimize((DiskVolume)r!.Data!), "Optimize drive {name}? On a hard disk this can take a long time."),
        new("Check for errors (read-only)", (r, _) => DiskOptimizer.CheckDisk((DiskVolume)r!.Data!)),
    };
}

public sealed class UsbSource : ManagerSource
{
    public override string Title => "USB / SD drive cleaner";
    public override string Description =>
        "Removes junk that Windows, Macs and apps leave on USB sticks and SD cards. It only touches drives Windows reports as removable. You are also asked automatically when a new drive is plugged in (turn that off below).";
    public override string EmptyText => "No removable drives are plugged in.";
    public override List<Row> Load() => RemovableCleaner.Drives()
        .Select(d => new Row(d.Name, $"{d.Name}  {d.VolumeLabel}", $"{d.DriveFormat}  -  {d.AvailableFreeSpace / 1073741824.0:0.#} GB free of {d.TotalSize / 1073741824.0:0.#} GB", "removable")).ToList();
    public override IReadOnlyList<RowAction> Actions { get; } = new RowAction[]
    {
        new("Scan for junk", (r, _) =>
        {
            var items = RemovableCleaner.Scan(r!.Id);
            var mb = items.Sum(i => i.Size) / 1048576.0;
            return items.Count == 0 ? "No junk found." : $"Found {items.Count} junk items ({mb:0.#} MB): " + string.Join(", ", items.Take(6).Select(i => Path.GetFileName(i.Path))) + (items.Count > 6 ? ", ..." : "");
        }),
        new("Clean junk", (r, _) => RemovableCleaner.Clean(r!.Id), "Remove junk from {name}?\n\n" + RemovableCleaner.Categories),
        new("Toggle plug-in prompt", (_, _) => { RemovableCleaner.PromptEnabled = !RemovableCleaner.PromptEnabled;
            return RemovableCleaner.PromptEnabled ? "You will be asked when a USB drive is plugged in." : "Plug-in prompts are off."; }, "", false),
    };
}
