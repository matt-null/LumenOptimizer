using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LumenOptimizer.Core.Tools;
using LumenOptimizer.UI.ViewModels;

namespace LumenOptimizer.UI.Pages;

public sealed partial class StartupPage : Page
{
    private bool _reverting;

    public ObservableCollection<StartupItemViewModel> Items { get; } = new();

    public StartupPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var list = await Task.Run(Startup.List);
        Items.Clear();
        foreach (var s in list) Items.Add(new StartupItemViewModel(s));
        if (Items.Count == 0)
            Dialogs.Show(ResultBar, InfoBarSeverity.Informational, "Nothing found", "No startup items were found in the registry or Startup folders.");
    }

    private void ToggleChanged(object sender, RoutedEventArgs e)
    {
        if (_reverting || sender is not ToggleSwitch { Tag: StartupItemViewModel item } sw) return;
        if (sw.IsOn == item.Entry.Enabled) return; // initial binding, not a user change

        try
        {
            Startup.SetEnabled(item.Entry, sw.IsOn);
            Dialogs.Show(ResultBar, InfoBarSeverity.Success, sw.IsOn ? "Switched on" : "Switched off",
                $"{item.Name} {(sw.IsOn ? "will" : "will no longer")} start with Windows.");
        }
        catch (Exception ex)
        {
            _reverting = true;
            sw.IsOn = item.Entry.Enabled;
            _reverting = false;
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not change it", ex.Message);
        }
    }
}
