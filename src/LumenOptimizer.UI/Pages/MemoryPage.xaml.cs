using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LumenOptimizer.Core.Tools;
using LumenOptimizer.UI.ViewModels;

namespace LumenOptimizer.UI.Pages;

public sealed partial class MemoryPage : Page
{
    private bool _busy;

    public ObservableCollection<ProcessMemoryViewModel> Processes { get; } = new();

    public MemoryPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        FlashBar.IsOpen = WizardState.Profile?.IsFlash == true;
        await RefreshAsync();
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Ring.IsActive = busy;
        StandbyButton.IsEnabled = !busy;
        ModifiedButton.IsEnabled = !busy;
        TrimButton.IsEnabled = !busy;
        MainWindow.Current?.SetBusy(busy);
    }

    private async Task RefreshAsync()
    {
        var status = await Task.Run(Memory.Status);
        var top = await Task.Run(() => Memory.TopProcesses(15));

        TotalText.Text = $"Installed: {Cleaner.FormatBytes(status.TotalBytes)}";
        InUseText.Text = $"In use: {Cleaner.FormatBytes(status.InUseBytes)}";
        AvailableText.Text = $"Available: {Cleaner.FormatBytes(status.AvailableBytes)}";
        CachedText.Text = $"Cached (standby): {Cleaner.FormatBytes(status.CachedBytes)}";
        CommitText.Text = $"Committed: {Cleaner.FormatBytes(status.CommitTotalBytes)} of {Cleaner.FormatBytes(status.CommitLimitBytes)}";
        ProcessText.Text = $"Processes: {status.ProcessCount}";

        LoadBar.Value = status.LoadPercent;
        LoadText.Text = $"{status.LoadPercent}% of physical memory in use. Cached data counts as available, because Windows hands it back on demand.";

        Processes.Clear();
        foreach (var p in top) Processes.Add(new ProcessMemoryViewModel(p));
    }

    private async void RefreshClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        ResultBar.IsOpen = false;
        SetBusy(true);
        try { await RefreshAsync(); } finally { SetBusy(false); }
    }

    private async void ActionClick(object sender, RoutedEventArgs e)
    {
        if (_busy || sender is not Button { Tag: string tag }) return;

        string title, body, primary;
        Func<long> work;

        switch (tag)
        {
            case "standby":
                title = "Empty the standby list?";
                body = "Windows is keeping recently used file data in otherwise idle RAM so it can be reused without touching the disk. " +
                       "This throws that cache away.\n\nThe \"cached\" and \"available\" figures will change straight away. Nothing is lost " +
                       "and no program is affected, but files that were cached have to be read from disk again, so the PC may feel slower " +
                       "for a short while.\n\nWindows rebuilds the cache as you use the PC.";
                primary = "Empty standby list";
                work = Memory.PurgeStandbyList;
                break;

            case "modified":
                title = "Flush modified pages to disk?";
                body = "Modified pages are changes waiting to be written out. This writes them now so the memory can be reclaimed.\n\n" +
                       "It causes disk writes, and Windows would do this by itself anyway. Nothing is lost.";
                primary = "Flush";
                work = Memory.FlushModifiedList;
                break;

            default:
                title = "Trim every program's working set?";
                body = "This pushes the memory pages of every running program out of RAM. Programs keep running, but the moment they " +
                       "need those pages back they have to be read from disk.\n\nThe \"in use\" figure drops, which looks good and is " +
                       "mostly an illusion: the work comes straight back as you use your apps, and they will stutter while it does. " +
                       "This is what most \"RAM cleaner\" tools do, and it is the one action here with the clearest downside.\n\n" +
                       "Nothing is lost and no restart is needed.";
                primary = "Trim anyway";
                work = Memory.TrimAllWorkingSets;
                break;
        }

        if (WizardState.Profile?.IsFlash == true)
            body += "\n\nOn this PC Windows runs from an SD card, USB drive or eMMC, so everything dropped has to be read back from " +
                    "that slow drive. This is likely to make the PC feel worse, not better.";

        if (!await Dialogs.ConfirmAsync(XamlRoot, title, body, primary)) return;

        SetBusy(true);
        try
        {
            var freed = await Task.Run(work);
            await RefreshAsync();
            Dialogs.Show(ResultBar, InfoBarSeverity.Success, "Done",
                freed > 0
                    ? $"Available memory went up by about {Cleaner.FormatBytes(freed)}. Remember that this is cache Windows will rebuild."
                    : "The request went through. The available figure barely moved, which usually means there was nothing worth reclaiming.");
        }
        catch (Exception ex)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not do that", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }
}
