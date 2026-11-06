using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace LumenOptimizer.UI.Pages;

/// <summary>One page that drives every list-style tool (services, tasks, accounts, drives...). What it shows comes from a ManagerSource.</summary>
public sealed partial class ManagerPage : Page
{
    private ManagerSource? _src;
    private List<Row> _all = new();
    public ObservableCollection<Row> Rows { get; } = new();

    public ManagerPage() { InitializeComponent(); }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _src = ManagerSource.Create((string)e.Parameter);
        TitleText.Text = _src.Title;
        DescText.Text = _src.Description;
        SearchRow.Visibility = RowList.Visibility = _src.ShowList ? Visibility.Visible : Visibility.Collapsed;
        foreach (var a in _src.Actions)
        {
            var b = new Button { Content = a.Label, Tag = a };
            b.Click += ActionClick;
            ActionPanel.Children.Add(b);
        }
        await ReloadAsync();
    }

    private async Task ReloadAsync()
    {
        if (_src is null || !_src.ShowList) return;
        SetBusy(true);
        try { _all = await Task.Run(_src.Load); }
        catch (Exception ex) { Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not load the list", ex.Message); _all = new(); }
        ApplyFilter();
        if (_all.Count == 0) Dialogs.Show(ResultBar, InfoBarSeverity.Informational, "Nothing to show", _src.EmptyText);
        SetBusy(false);
    }

    private void ApplyFilter()
    {
        var q = SearchBox.Text.Trim();
        Rows.Clear();
        foreach (var r in _all.Where(r => q.Length == 0 || r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) || r.Subtitle.Contains(q, StringComparison.OrdinalIgnoreCase)))
            Rows.Add(r);
    }

    private void SearchChanged(object sender, TextChangedEventArgs e) => ApplyFilter();
    private async void RefreshClick(object sender, RoutedEventArgs e) => await ReloadAsync();

    private void SetBusy(bool busy)
    {
        Busy.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        ActionPanel.IsHitTestVisible = !busy;
        MainWindow.Current?.SetBusy(busy);
    }

    private async void ActionClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: RowAction a }) return;
        var row = RowList.SelectedItem as Row;
        if (a.NeedsRow && row is null)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Warning, "Pick one first", "Select an item in the list, then choose an action.");
            return;
        }

        string? input = null;
        if (a.Prompt.Length > 0)
        {
            input = await PromptAsync(a.Label, a.Prompt, a.Secret);
            if (input is null || (!a.Secret && input.Trim().Length == 0)) return;
        }
        if (a.Confirm.Length > 0 &&
            !await Dialogs.ConfirmAsync(XamlRoot, a.Label, a.Confirm.Replace("{name}", row?.Title ?? ""), a.Label.TrimEnd('.')))
            return;

        SetBusy(true);
        try
        {
            var msg = await Task.Run(() => a.Run(row, input));
            Dialogs.Show(ResultBar, InfoBarSeverity.Success, a.Label, msg);
        }
        catch (Exception ex) { Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not do that", ex.Message); }
        SetBusy(false);
        await ReloadAsync();
    }

    private async Task<string?> PromptAsync(string title, string label, bool secret)
    {
        FrameworkElement box = secret ? new PasswordBox { PlaceholderText = label } : new TextBox { PlaceholderText = label, MinWidth = 380 };
        var dlg = new ContentDialog
        {
            XamlRoot = XamlRoot, Title = title, Content = box,
            PrimaryButtonText = "OK", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Primary
        };
        if (await dlg.ShowAsync() != ContentDialogResult.Primary) return null;
        return box is PasswordBox pb ? pb.Password : ((TextBox)box).Text;
    }
}
