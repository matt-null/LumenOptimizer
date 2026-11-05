using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LumenOptimizer.Core.Tools;
using LumenOptimizer.UI.ViewModels;

namespace LumenOptimizer.UI.Pages;

public sealed partial class CleanerPage : Page
{
    private bool _busy;

    public ObservableCollection<CleanItemViewModel> Items { get; } = new();

    public CleanerPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (Items.Count == 0)
            foreach (var t in Cleaner.Targets()) Items.Add(new CleanItemViewModel(t));
        await ScanAsync();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Ring.IsActive = busy;
        RescanButton.IsEnabled = !busy;
        CleanButton.IsEnabled = !busy;
        MainWindow.Current?.SetBusy(busy);
    }

    private async Task ScanAsync()
    {
        if (_busy) return;
        SetBusy(true);
        TotalText.Text = "Scanning…";
        try
        {
            foreach (var item in Items)
            {
                item.SizeText = "scanning…";
                var t = item.Target;
                await Task.Run(() => Cleaner.Scan(t));
                item.Refresh();
            }
            var total = Items.Sum(i => i.Target.Bytes);
            TotalText.Text = $"Up to {Cleaner.FormatBytes(total)} can be cleaned.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void RescanClick(object sender, RoutedEventArgs e)
    {
        ResultBar.IsOpen = false;
        await ScanAsync();
    }

    private async void CleanClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var chosen = Items.Where(i => i.IsChecked).ToList();
        if (chosen.Count == 0)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Informational, "Nothing selected", "Tick the items you want to clean.");
            return;
        }

        var size = chosen.Sum(i => i.Target.Bytes);
        var lines = chosen.Select(i => $"• {i.Name}  ({(i.Target.Special == "dns" ? "—" : Cleaner.FormatBytes(i.Target.Bytes))})");
        var body = "These will be deleted permanently:\n\n" + string.Join("\n", lines) +
                   $"\n\nAbout {Cleaner.FormatBytes(size)} will be freed. Files that are in use are skipped. " +
                   "This cannot be undone, but it only removes temporary files and caches that Windows and your apps recreate.";
        if (chosen.Any(i => i.Target.Special == "recyclebin"))
            body += "\n\nThe Recycle Bin will be emptied: anything in it is gone for good.";

        if (!await Dialogs.ConfirmAsync(XamlRoot, $"Clean {chosen.Count} item(s)?", body, "Clean")) return;

        SetBusy(true);
        TotalText.Text = "Cleaning…";
        try
        {
            var targets = chosen.Select(i => i.Target).ToList();
            var report = await Task.Run(() => Cleaner.Clean(targets));
            var message = $"Freed {Cleaner.FormatBytes(report.FreedBytes)} ({report.Deleted:N0} file(s) removed, {report.Skipped:N0} skipped because they were in use).";
            if (report.Notes.Count > 0) message += "\n" + string.Join("\n", report.Notes);
            Dialogs.Show(ResultBar, InfoBarSeverity.Success, "Done", message);
        }
        catch (Exception ex)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Cleaning failed", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }

        await ScanAsync();
    }
}
