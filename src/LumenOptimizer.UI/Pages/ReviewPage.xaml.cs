using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LumenOptimizer.Core.Models;
using LumenOptimizer.UI.ViewModels;

namespace LumenOptimizer.UI.Pages;

public sealed partial class ReviewPage : Page
{
    private bool _loading;
    private bool _hasRisky;

    public ObservableCollection<TweakItemViewModel> Items { get; } = new();

    public ReviewPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        Items.Clear();
        // Most risky first, so the important warnings are at the top.
        foreach (var i in WizardState.Selected.OrderByDescending(i => i.Tweak.Risk))
            Items.Add(i);

        _hasRisky = Items.Any(i => i.Tweak.Risk == RiskLevel.Aggressive);
        var noUndo = Items.Where(i => !i.CanRevert).ToList();

        _loading = true;
        RestorePointBox.IsChecked = WizardState.CreateRestorePoint;
        AckBox.IsChecked = false;
        AckBox.Visibility = _hasRisky ? Visibility.Visible : Visibility.Collapsed;
        _loading = false;

        RiskBar.IsOpen = _hasRisky;

        if (noUndo.Count > 0)
        {
            NoUndoBar.Title = $"{noUndo.Count} change(s) cannot be undone automatically";
            NoUndoBar.Message = "Removed apps must be reinstalled from the Microsoft Store.";
            NoUndoBar.IsOpen = true;
        }
        else
        {
            NoUndoBar.IsOpen = false;
        }

        SummaryText.Text = Items.Count == 0
            ? "Nothing is selected. Go back and tick the tweaks you want."
            : $"{Items.Count} change(s) will be made. Nothing has been changed yet.";

        UpdateNext();
    }

    private async void SaveSelectionClick(object sender, RoutedEventArgs e)
    {
        if (Items.Count == 0)
        {
            SummaryText.Text = "Nothing is selected, so there is nothing to save.";
            return;
        }

        try
        {
            var path = await FilePickers.SaveTextAsync($"LumenOptimizer-selection-{DateTime.Now:yyyyMMdd}", ".json", "Lumen Optimizer selection", WizardState.ExportSelection());
            if (path is not null) SummaryText.Text = $"{Items.Count} change(s) will be made. Selection saved to {path}.";
        }
        catch (Exception ex)
        {
            SummaryText.Text = "Could not save the selection: " + ex.Message;
        }
    }

    private void OptionsChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        WizardState.CreateRestorePoint = RestorePointBox.IsChecked == true;
        UpdateNext();
    }

    private void UpdateNext()
    {
        var ready = Items.Count > 0 && (!_hasRisky || AckBox.IsChecked == true);
        MainWindow.Current?.SetNext(Items.Count > 0 ? $"Apply {Items.Count} change(s)" : "Apply changes", ready);
    }
}
