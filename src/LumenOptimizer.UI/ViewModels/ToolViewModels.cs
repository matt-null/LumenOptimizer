using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using LumenOptimizer.Core.Tools;

namespace LumenOptimizer.UI.ViewModels;

public partial class CleanItemViewModel : ObservableObject
{
    public CleanItemViewModel(CleanTarget target)
    {
        Target = target;
        _isChecked = target.DefaultOn;
    }

    public CleanTarget Target { get; }
    public string Name => Target.Name;
    public string Description => Target.Description;

    [ObservableProperty]
    private bool _isChecked;

    [ObservableProperty]
    private string _sizeText = "scanning…";

    public void Refresh()
    {
        SizeText = Target.Special == "dns" ? "—"
            : Target.Bytes == 0 && Target.Files == 0 ? "nothing to clean"
            : $"{Cleaner.FormatBytes(Target.Bytes)}  ·  {Target.Files:N0} file(s)";
    }
}

public sealed class StartupItemViewModel
{
    public StartupItemViewModel(StartupEntry entry) => Entry = entry;
    public StartupEntry Entry { get; }
    public string Name => Entry.Name;
    public string Command => Entry.Command;
    public string Source => Entry.Source;
    public bool IsOn => Entry.Enabled;
}

public sealed class ProgramViewModel
{
    public ProgramViewModel(InstalledProgram p) => Program = p;
    public InstalledProgram Program { get; }
    public string Name => Program.Name;

    public string Details
    {
        get
        {
            var parts = new List<string>();
            if (Program.Publisher.Length > 0) parts.Add(Program.Publisher);
            if (Program.Version.Length > 0) parts.Add("v" + Program.Version);
            if (Program.SizeText.Length > 0) parts.Add(Program.SizeText);
            if (Program.InstallDate.Length == 8) parts.Add($"installed {Program.InstallDate[..4]}-{Program.InstallDate.Substring(4, 2)}-{Program.InstallDate.Substring(6, 2)}");
            return string.Join("  ·  ", parts);
        }
    }
}

public sealed class ContextItemViewModel
{
    public ContextItemViewModel(ContextMenuItem item) => Item = item;
    public ContextMenuItem Item { get; }
    public string Title => Item.Title;
    public string Description => Item.Description;
    public bool IsOn => Item.Installed;
}

public partial class GhostDeviceViewModel : ObservableObject
{
    public GhostDeviceViewModel(GhostDevice device) => Device = device;

    public GhostDevice Device { get; }
    public string Name => Device.Name;
    public string Class => Device.Class.Length > 0 ? Device.Class : "Unknown type";
    public string InstanceId => Device.InstanceId;

    public string Note => Device.IsRisky ? "Part of the PC itself - leave this one alone" : "";
    public Visibility NoteVisibility => Device.IsRisky ? Visibility.Visible : Visibility.Collapsed;

    [ObservableProperty]
    private bool _isChecked;
}

public partial class StaleRecordViewModel : ObservableObject
{
    public StaleRecordViewModel(StaleRecord record) => Record = record;

    public StaleRecord Record { get; }
    public string Description => Record.Description;
    public string Where => Record.ValueName is null
        ? $"{Record.Hive}\\{Record.KeyPath}"
        : $"{Record.Hive}\\{Record.KeyPath} -> {Record.ValueName}";

    [ObservableProperty]
    private bool _isChecked = true;
}

public sealed class ProcessMemoryViewModel
{
    public ProcessMemoryViewModel(ProcessMemory p) => Process = p;

    public ProcessMemory Process { get; }
    public string Name => Process.Name;
    public string Details => $"PID {Process.Pid}  -  {Cleaner.FormatBytes(Process.WorkingSetBytes)}";
}
