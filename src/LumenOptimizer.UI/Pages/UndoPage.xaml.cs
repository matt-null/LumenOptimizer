using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LumenOptimizer.UI.ViewModels;

namespace LumenOptimizer.UI.Pages;

public sealed partial class UndoPage : Page
{
    private bool _busy;

    public ObservableCollection<TweakItemViewModel> Items { get; } = new();

    public UndoPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LoadItems();
    }

    private void LoadItems()
    {
        var ids = new HashSet<string>(AppServices.Backups.Ids, StringComparer.OrdinalIgnoreCase);
        Items.Clear();
        foreach (var item in WizardState.All.Where(i => ids.Contains(i.Tweak.Id) && i.CanRevert))
        {
            item.IsUndoSelected = false;
            Items.Add(item);
        }

        if (Items.Count == 0)
            Show(InfoBarSeverity.Informational, "Nothing to undo", "There are no saved changes that can be undone.");
    }

    private void Show(InfoBarSeverity severity, string title, string message)
    {
        ResultBar.Severity = severity;
        ResultBar.Title = title;
        ResultBar.Message = message;
        ResultBar.IsOpen = true;
    }

    private void SelectAllClick(object sender, RoutedEventArgs e)
    {
        foreach (var i in Items) i.IsUndoSelected = true;
    }

    private void SelectNoneClick(object sender, RoutedEventArgs e)
    {
        foreach (var i in Items) i.IsUndoSelected = false;
    }

    private async void RevertClick(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var chosen = Items.Where(i => i.IsUndoSelected).ToList();
        if (chosen.Count == 0)
        {
            Show(InfoBarSeverity.Informational, "Nothing selected", "Tick the changes you want to undo.");
            return;
        }

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Undo {chosen.Count} change(s)?",
            Content = new ScrollViewer
            {
                MaxHeight = 360,
                Content = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    Text = string.Join("\n", chosen.Select(i => "• " + i.Name))
                }
            },
            PrimaryButtonText = "Undo",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        _busy = true;
        RevertButton.IsEnabled = false;
        Progress.Visibility = Visibility.Visible;
        Progress.Maximum = chosen.Count;
        Progress.Value = 0;

        var errors = new List<string>();
        var needsRestart = false;
        var ok = 0;
        var done = 0;
        try
        {
            foreach (var item in chosen)
            {
                var tweak = item.Tweak;
                var result = await Task.Run(() => AppServices.Engine.Revert(tweak));
                item.State = await Task.Run(() => AppServices.Engine.GetState(tweak));
                if (result.Success)
                {
                    ok++;
                    item.IsUndoSelected = false;
                    if (tweak.RequiresReboot) needsRestart = true;
                }
                else
                {
                    errors.AddRange(result.Errors.Select(err => $"{tweak.Name}: {err}"));
                }
                Progress.Value = ++done;
            }
        }
        finally
        {
            _busy = false;
            RevertButton.IsEnabled = true;
            Progress.Visibility = Visibility.Collapsed;
        }

        LoadItemsKeepingMessage();

        if (errors.Count == 0)
            Show(InfoBarSeverity.Success, "Done", $"{ok} change(s) undone." + (needsRestart ? " Restart or sign out to finish." : ""));
        else
            Show(InfoBarSeverity.Warning, "Finished with problems", $"{ok} undone.\n" + string.Join("\n", errors.Take(6)));
    }

    private void LoadItemsKeepingMessage()
    {
        // Reverted tweaks lose their backup, so they drop off the list.
        var ids = new HashSet<string>(AppServices.Backups.Ids, StringComparer.OrdinalIgnoreCase);
        foreach (var gone in Items.Where(i => !ids.Contains(i.Tweak.Id)).ToList())
            Items.Remove(gone);
    }
}
