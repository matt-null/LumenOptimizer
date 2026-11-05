using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LumenOptimizer.Core.Tools;

namespace LumenOptimizer.UI.Pages;

public sealed partial class NetworkPage : Page
{
    private bool _busy;

    public NetworkPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        DnsBox.ItemsSource = NetworkTools.DnsProviders;
        RestoreDnsButton.IsEnabled = NetworkTools.HasDnsBackup;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        Progress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        MainWindow.Current?.SetBusy(busy);
    }

    private async Task RunAsync(string title, Func<string> work)
    {
        if (_busy) return;
        SetBusy(true);
        OutputBox.Text = title + "…";
        try
        {
            OutputBox.Text = await Task.Run(work);
        }
        catch (Exception ex)
        {
            OutputBox.Text = "";
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, title + " failed", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void RepairClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;

        string title, body; Func<string> work; var restart = false;
        switch (tag)
        {
            case "dns":
                title = "Flush the DNS cache?";
                body = "Runs: ipconfig /flushdns\nClears cached name lookups. Nothing is lost.";
                work = NetworkTools.FlushDns; break;
            case "renew":
                title = "Release and renew your IP address?";
                body = "Runs: ipconfig /release then ipconfig /renew\nYour connection drops for a few seconds. On a static IP this does nothing.";
                work = NetworkTools.ReleaseRenew; break;
            case "winsock":
                title = "Reset Winsock?";
                body = "Runs: netsh winsock reset\nRemoves third-party network filters (some VPNs and security tools add them, and may need reinstalling). A restart is needed to finish.";
                work = NetworkTools.ResetWinsock; restart = true; break;
            default:
                title = "Reset the TCP/IP stack?";
                body = "Runs: netsh int ip reset and netsh int ipv6 reset\nResets IP settings to defaults, including static addresses you set by hand. A restart is needed to finish.";
                work = NetworkTools.ResetTcpIp; restart = true; break;
        }

        if (!await Dialogs.ConfirmAsync(XamlRoot, title, body, "Run")) return;
        await RunAsync(title.TrimEnd('?'), work);
        if (restart)
            Dialogs.Show(ResultBar, InfoBarSeverity.Warning, "Restart needed", "Restart Windows to finish this reset.");
    }

    private async void CheckClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        var host = HostBox.Text.Trim();
        if (tag != "ipconfig" && !NetworkTools.IsValidHost(host))
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Warning, "Check the address", "Enter a host name like example.com or an IP address.");
            return;
        }

        ResultBar.IsOpen = false;
        switch (tag)
        {
            case "ping": await RunAsync("Ping " + host, () => NetworkTools.Ping(host)); break;
            case "trace": await RunAsync("Trace route to " + host, () => NetworkTools.Trace(host)); break;
            case "lookup": await RunAsync("DNS lookup " + host, () => NetworkTools.Lookup(host)); break;
            default: await RunAsync("IP configuration", NetworkTools.IpConfig); break;
        }
    }

    private async void ShowDnsClick(object sender, RoutedEventArgs e) =>
        await RunAsync("Current DNS", NetworkTools.DescribeCurrent);

    private async void ApplyDnsClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (DnsBox.SelectedItem is not DnsProvider provider)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Informational, "Choose a provider", "Pick Cloudflare, Google or Quad9 first.");
            return;
        }

        string current;
        try { current = await Task.Run(NetworkTools.DescribeCurrent); }
        catch (Exception ex) { Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not read DNS settings", ex.Message); return; }

        var body = $"Current DNS on active adapters:\n{current}\n\nWill become {provider.Name}: {string.Join(", ", provider.Servers)}\n{provider.Note}\n\n" +
                   "Your current settings are saved first. 'Restore original DNS' puts them back.";
        if (!await Dialogs.ConfirmAsync(XamlRoot, $"Switch DNS to {provider.Name}?", body, "Switch")) return;

        SetBusy(true);
        try
        {
            await Task.Run(() => NetworkTools.SetDns(provider));
            Dialogs.Show(ResultBar, InfoBarSeverity.Success, "DNS changed", $"Active adapters now use {provider.Name}.");
        }
        catch (Exception ex)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not change DNS", ex.Message);
        }
        finally
        {
            SetBusy(false);
            RestoreDnsButton.IsEnabled = NetworkTools.HasDnsBackup;
        }
    }

    private async void RestoreDnsClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (!await Dialogs.ConfirmAsync(XamlRoot, "Restore the original DNS?",
                "Puts back the DNS settings that were saved before the first change (automatic/DHCP where they were automatic).", "Restore")) return;

        SetBusy(true);
        try
        {
            await Task.Run(NetworkTools.RestoreDns);
            Dialogs.Show(ResultBar, InfoBarSeverity.Success, "Original DNS restored", "");
        }
        catch (Exception ex)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not restore DNS", ex.Message);
        }
        finally
        {
            SetBusy(false);
            RestoreDnsButton.IsEnabled = NetworkTools.HasDnsBackup;
        }
    }
}
