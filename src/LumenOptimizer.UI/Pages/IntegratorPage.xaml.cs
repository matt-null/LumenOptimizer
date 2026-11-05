using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LumenOptimizer.Core.Tools;
using LumenOptimizer.UI.ViewModels;

namespace LumenOptimizer.UI.Pages;

public sealed partial class IntegratorPage : Page
{
    private bool _reverting;

    public ObservableCollection<ContextItemViewModel> Items { get; } = new();

    public IntegratorPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Items.Clear();
        foreach (var i in ContextMenu.Items()) Items.Add(new ContextItemViewModel(i));
    }

    private async void ToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_reverting || sender is not ToggleSwitch { Tag: ContextItemViewModel vm } sw) return;
        if (sw.IsOn == vm.Item.Installed) return; // initial binding, not a user change

        var adding = sw.IsOn;
        var body = adding
            ? $"This adds \"{vm.Title}\" to the right-click menu.\n\nCommand that the entry will run:\n{vm.Item.Command}\n\nRegistry key (current user only):\nHKCU\\Software\\Classes\\{vm.Item.KeyPath}"
            : $"This removes \"{vm.Title}\" from the right-click menu by deleting:\nHKCU\\Software\\Classes\\{vm.Item.KeyPath}";

        if (!await Dialogs.ConfirmAsync(XamlRoot, adding ? "Add to the right-click menu?" : "Remove from the right-click menu?", body, adding ? "Add" : "Remove"))
        {
            _reverting = true;
            sw.IsOn = vm.Item.Installed;
            _reverting = false;
            return;
        }

        try
        {
            if (adding) ContextMenu.Install(vm.Item); else ContextMenu.Remove(vm.Item);
            Dialogs.Show(ResultBar, InfoBarSeverity.Success, adding ? "Added" : "Removed", vm.Title);
        }
        catch (Exception ex)
        {
            _reverting = true;
            sw.IsOn = vm.Item.Installed;
            _reverting = false;
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not change it", ex.Message);
        }
    }
}
