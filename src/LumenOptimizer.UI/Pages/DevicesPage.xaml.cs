using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LumenOptimizer.Core.Tools;
using LumenOptimizer.UI.ViewModels;

namespace LumenOptimizer.UI.Pages;

public sealed partial class DevicesPage : Page
{
    private List<GhostDeviceViewModel> _allGhosts = new();
    private bool _busy;

    public ObservableCollection<GhostDeviceViewModel> Ghosts { get; } = new();
    public ObservableCollection<StaleRecordViewModel> Records { get; } = new();

    public DevicesPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadBackups();
        LoadHistory();
        await ScanDevicesAsync();
        await ScanRecordsAsync();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        MainWindow.Current?.SetBusy(busy);
        RemoveDevicesButton.IsEnabled = !busy;
        RemoveRecordsButton.IsEnabled = !busy;
        ScanDevicesButton.IsEnabled = !busy;
        ScanRecordsButton.IsEnabled = !busy;
    }

    // ------------------------------------------------------- ghost devices

    private async Task ScanDevicesAsync()
    {
        if (_busy) return;
        SetBusy(true);
        DeviceRing.IsActive = true;
        DeviceCountText.Text = "Looking for devices that are not plugged in…";
        try
        {
            var found = await Task.Run(DeviceCleanup.GhostDevices);
            _allGhosts = found.Select(d => new GhostDeviceViewModel(d)).ToList();
            ShowGhosts();
        }
        catch (Exception ex)
        {
            _allGhosts = new List<GhostDeviceViewModel>();
            ShowGhosts();
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not list devices", ex.Message);
        }
        finally
        {
            DeviceRing.IsActive = false;
            SetBusy(false);
        }
    }

    private void ShowGhosts()
    {
        var usbOnly = UsbOnlyBox.IsChecked == true;
        var shown = _allGhosts.Where(g => !usbOnly || g.Device.IsRemovableKind).ToList();

        Ghosts.Clear();
        foreach (var g in shown) Ghosts.Add(g);

        DeviceCountText.Text = _allGhosts.Count == 0
            ? "No leftover device records found. Nothing to clean up here."
            : $"{shown.Count} shown of {_allGhosts.Count} leftover record(s)." +
              (usbOnly ? " Untick the box above to see the rest." : "");
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        if (_allGhosts.Count == 0 && Ghosts.Count == 0) return;
        ShowGhosts();
    }

    private void SelectAllDevicesClick(object sender, RoutedEventArgs e)
    {
        // Entries that are part of the PC itself are left alone even by "select all".
        foreach (var g in Ghosts.Where(g => !g.Device.IsRisky)) g.IsChecked = true;
    }

    private void SelectNoDevicesClick(object sender, RoutedEventArgs e)
    {
        foreach (var g in Ghosts) g.IsChecked = false;
    }

    private async void ScanDevicesClick(object sender, RoutedEventArgs e)
    {
        ResultBar.IsOpen = false;
        await ScanDevicesAsync();
    }

    private void OpenDeviceManagerClick(object sender, RoutedEventArgs e)
    {
        try
        {
            DeviceCleanup.OpenDeviceManagerWithHidden();
        }
        catch (Exception ex)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not open Device Manager", ex.Message);
        }
    }

    private async void RemoveDevicesClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var chosen = Ghosts.Where(g => g.IsChecked).ToList();
        if (chosen.Count == 0)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Informational, "Nothing selected", "Tick the device records you want to remove.");
            return;
        }

        var lines = chosen.Take(30).Select(g => $"• {g.Name}  ({g.Class})");
        var body = "The stored record for these devices will be deleted:\n\n" + string.Join("\n", lines) +
                   (chosen.Count > 30 ? $"\n… and {chosen.Count - 30} more" : "") +
                   "\n\nEach one runs:\npnputil /remove-device \"<device id>\"\n\n" +
                   "No hardware is affected and nothing that is plugged in right now is touched. Plug a device back in and " +
                   "Windows installs it again. This cannot be undone from here, because there is nothing left to put back.";

        if (chosen.Any(g => g.Device.IsRisky))
            body += "\n\nWarning: some of these belong to the PC itself rather than to plug-in hardware. Removing those gains " +
                    "nothing, and Windows will simply detect them again on the next boot.";

        if (!await Dialogs.ConfirmAsync(XamlRoot, $"Remove {chosen.Count} device record(s)?", body, "Remove")) return;

        SetBusy(true);
        DeviceRing.IsActive = true;
        try
        {
            var devices = chosen.Select(g => g.Device).ToList();
            var (removed, problems) = await Task.Run(() => DeviceCleanup.RemoveGhostDevices(devices));

            if (problems.Count == 0)
                Dialogs.Show(ResultBar, InfoBarSeverity.Success, "Done", $"{removed} device record(s) removed.");
            else
                Dialogs.Show(ResultBar, InfoBarSeverity.Warning, "Finished with problems",
                    $"{removed} removed.\n" + string.Join("\n", problems.Take(6)));
        }
        catch (Exception ex)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not remove them", ex.Message);
        }
        finally
        {
            DeviceRing.IsActive = false;
            SetBusy(false);
        }

        await ScanDevicesAsync();
    }

    // -------------------------------------------------------- drive records

    private async Task ScanRecordsAsync()
    {
        if (_busy) return;
        SetBusy(true);
        RecordRing.IsActive = true;
        try
        {
            var found = await Task.Run(DeviceCleanup.StaleDriveRecords);
            Records.Clear();
            foreach (var r in found) Records.Add(new StaleRecordViewModel(r));
            RecordCountText.Text = found.Count == 0
                ? "No leftover drive records found."
                : $"{found.Count} leftover record(s).";
        }
        catch (Exception ex)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not read the drive records", ex.Message);
        }
        finally
        {
            RecordRing.IsActive = false;
            SetBusy(false);
        }
    }

    private async void ScanRecordsClick(object sender, RoutedEventArgs e)
    {
        ResultBar.IsOpen = false;
        await ScanRecordsAsync();
    }

    private void SelectAllRecordsClick(object sender, RoutedEventArgs e)
    {
        foreach (var r in Records) r.IsChecked = true;
    }

    private void SelectNoRecordsClick(object sender, RoutedEventArgs e)
    {
        foreach (var r in Records) r.IsChecked = false;
    }

    private async void RemoveRecordsClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var chosen = Records.Where(r => r.IsChecked).ToList();
        if (chosen.Count == 0)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Informational, "Nothing selected", "Tick the records you want to remove.");
            return;
        }

        var lines = chosen.Take(30).Select(r => $"• {r.Description}\n    {r.Where}");
        var body = "These registry records will be removed:\n\n" + string.Join("\n", lines) +
                   (chosen.Count > 30 ? $"\n… and {chosen.Count - 30} more" : "") +
                   "\n\nAll of them are saved to a backup file first, and the Restore button puts them back.\n\n" +
                   "Drives that are plugged in now are not affected. Freed drive letters may be handed to a different drive " +
                   "next time, and Explorer forgets the custom name or icon you gave an old drive.";

        if (!await Dialogs.ConfirmAsync(XamlRoot, $"Remove {chosen.Count} drive record(s)?", body, "Remove")) return;

        SetBusy(true);
        RecordRing.IsActive = true;
        try
        {
            var records = chosen.Select(r => r.Record).ToList();
            var backup = await Task.Run(() => DeviceCleanup.RemoveStaleDriveRecords(records));
            Dialogs.Show(ResultBar, InfoBarSeverity.Success, "Done",
                $"{records.Count} record(s) removed. Backup saved to {backup}\n" +
                "Sign out and back in, or restart Explorer, for the sidebar to catch up.");
            LoadBackups();
        }
        catch (Exception ex)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not remove them", ex.Message);
        }
        finally
        {
            RecordRing.IsActive = false;
            SetBusy(false);
        }

        await ScanRecordsAsync();
    }

    private void LoadBackups() => BackupBox.ItemsSource = DeviceCleanup.Backups();

    private async void RestoreBackupClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (BackupBox.SelectedItem is not string path)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Informational, "No backup chosen", "Pick a backup from the list first.");
            return;
        }

        if (!await Dialogs.ConfirmAsync(XamlRoot, "Restore this backup?",
                $"Everything saved in\n{path}\nwill be written back to the registry.", "Restore")) return;

        SetBusy(true);
        try
        {
            var restored = await Task.Run(() => DeviceCleanup.RestoreBackup(path));
            Dialogs.Show(ResultBar, InfoBarSeverity.Success, "Restored", $"{restored} record(s) put back.");
        }
        catch (Exception ex)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not restore the backup", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }

        await ScanRecordsAsync();
    }

    // --------------------------------------------------------- usb history

    private void LoadHistory()
    {
        try
        {
            var history = DeviceCleanup.UsbStorageHistory();
            HistoryText.Text = history.Count == 0
                ? "No USB storage history found, or it could not be read."
                : string.Join("\n", history.Select(h => "• " + h));
        }
        catch (Exception ex)
        {
            HistoryText.Text = "Could not read the history: " + ex.Message;
        }
    }
}
