using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LumenOptimizer.Core.Tools;
using LumenOptimizer.UI.ViewModels;

namespace LumenOptimizer.UI.Pages;

public sealed partial class UninstallerPage : Page
{
    private List<ProgramViewModel> _all = new();
    private bool _busy;

    public ObservableCollection<ProgramViewModel> Items { get; } = new();

    public UninstallerPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        var list = await Task.Run(Uninstaller.List);
        _all = list.Select(p => new ProgramViewModel(p)).ToList();
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var q = SearchBox.Text?.Trim() ?? "";
        Items.Clear();
        foreach (var p in _all.Where(p => q.Length == 0
                     || p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                     || p.Program.Publisher.Contains(q, StringComparison.OrdinalIgnoreCase)))
            Items.Add(p);
    }

    private void SearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => ApplyFilter();

    private async void UninstallClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        if (ProgramList.SelectedItem is not ProgramViewModel vm)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Informational, "Nothing selected", "Click a program in the list first.");
            return;
        }

        var quiet = QuietBox.IsChecked == true && !string.IsNullOrWhiteSpace(vm.Program.QuietUninstallString);
        var cmd = Uninstaller.CommandFor(vm.Program, quiet);
        var body = $"{vm.Name}\n{vm.Details}\n\nThis command will be run:\n{cmd}\n\n" +
                   "The program's own uninstaller does the work, so it may show its own windows. Uninstalling cannot be undone; " +
                   "to get the program back you have to install it again.";
        if (!await Dialogs.ConfirmAsync(XamlRoot, $"Uninstall {vm.Name}?", body, "Uninstall")) return;

        _busy = true;
        UninstallButton.IsEnabled = false;
        MainWindow.Current?.SetBusy(true);
        try
        {
            var program = vm.Program;
            var code = await Task.Run(() => Uninstaller.Run(program, quiet));
            Dialogs.Show(ResultBar,
                code == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning,
                code == 0 ? "Finished" : "The uninstaller reported a problem",
                code == 0 ? $"{vm.Name} was uninstalled." : $"Exit code {code}. It may have been cancelled or need a restart. The list was refreshed.");
        }
        catch (Exception ex)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not start the uninstaller", ex.Message);
        }
        finally
        {
            _busy = false;
            UninstallButton.IsEnabled = true;
            MainWindow.Current?.SetBusy(false);
        }

        await ReloadAsync();
    }
}
