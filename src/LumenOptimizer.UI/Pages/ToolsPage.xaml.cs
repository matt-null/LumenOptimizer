using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LumenOptimizer.UI.Pages;

public sealed partial class ToolsPage : Page
{
    public ToolsPage()
    {
        InitializeComponent();
    }

    private void ToolClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag }) return;
        var main = MainWindow.Current;
        if (main is null) return;

        switch (tag)
        {
            case "Cleaner": main.ShowTool(typeof(CleanerPage), "Cleaner"); break;
            case "Devices": main.ShowTool(typeof(DevicesPage), "Devices and USB"); break;
            case "Memory": main.ShowTool(typeof(MemoryPage), "Memory"); break;
            case "Startup": main.ShowTool(typeof(StartupPage), "Startup manager"); break;
            case "Uninstaller": main.ShowTool(typeof(UninstallerPage), "Uninstaller"); break;
            case "Hosts": main.ShowTool(typeof(HostsPage), "Hosts editor"); break;
            case "Integrator": main.ShowTool(typeof(IntegratorPage), "Integrator"); break;
            case "Network": main.ShowTool(typeof(NetworkPage), "Network tools"); break;
        }
    }
}
