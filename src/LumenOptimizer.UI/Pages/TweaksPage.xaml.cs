using System.Collections.ObjectModel;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LumenOptimizer.Core.Models;
using LumenOptimizer.UI.ViewModels;

namespace LumenOptimizer.UI.Pages;

/// <summary>One wizard step: the tweaks of a single category with checkboxes.</summary>
public sealed partial class TweaksPage : Page
{
    private static readonly Dictionary<string, (string Title, string Subtitle)> Info = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Privacy"] = ("Privacy", "Telemetry, tracking, advertising and data-collection settings in Windows."),
        ["Edge"] = ("Microsoft Edge", "Telemetry, sidebar and nag settings for Edge. Edge will show \"managed by your organization\" because these use policies."),
        ["Performance"] = ("Performance", "Power, startup, input and responsiveness tweaks."),
        ["Debloat"] = ("Debloat", "Remove pre-installed apps. Removed apps cannot be restored automatically; reinstall them from the Microsoft Store if you want them back."),
        ["Services"] = ("Services", "Background services you may not need. Read each warning: some features depend on them."),
        ["Appearance"] = ("Appearance", "File Explorer, taskbar and Start menu behaviour."),
        ["Advanced"] = ("Advanced (risky)", "Aggressive tweaks for experienced users.")
    };

    private List<TweakItemViewModel> _source = new();
    private bool _ready;

    public ObservableCollection<TweakItemViewModel> Items { get; } = new();

    public TweaksPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var category = e.Parameter as string ?? "";
        var (title, subtitle) = Info.TryGetValue(category, out var info) ? info : (category, "");
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        AdvancedBar.Visibility = category.Equals("Advanced", StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible : Visibility.Collapsed;

        _source = WizardState.All
            .Where(i => i.Tweak.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (var item in _source) item.PropertyChanged += OnItemChanged;

        _ready = true;
        ApplyFilter();
    }

    /// <summary>Search + filter. Tweaks that suit this PC are listed first; the order is otherwise unchanged.</summary>
    private void ApplyFilter()
    {
        if (!_ready) return;
        var q = SearchBox.Text?.Trim() ?? "";
        var filter = (FilterBox.SelectedItem as ComboBoxItem)?.Content as string ?? "All";

        bool Match(TweakItemViewModel i) => filter switch
        {
            "Fits this PC" => i.Fits,
            "Not applied" => i.State is TweakState.NotApplied or TweakState.Partial,
            "Already applied" => i.State == TweakState.Applied,
            "Needs restart" => i.Tweak.RequiresReboot,
            "Safe" => i.Tweak.Risk == RiskLevel.Safe,
            "Moderate" => i.Tweak.Risk == RiskLevel.Moderate,
            "Aggressive" => i.Tweak.Risk == RiskLevel.Aggressive,
            _ => true
        };

        var shown = _source
            .Where(i => Match(i) && (q.Length == 0
                || i.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || i.Description.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .Select((i, n) => (i, n))
            .OrderByDescending(x => x.i.Fits).ThenBy(x => x.n)
            .Select(x => x.i).ToList();

        Items.Clear();
        foreach (var i in shown) Items.Add(i);
        UpdateCount();
    }

    private void SearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => ApplyFilter();
    private void FilterChanged(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        foreach (var item in _source) item.PropertyChanged -= OnItemChanged;
        _ready = false;
        base.OnNavigatedFrom(e);
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TweakItemViewModel.IsSelected)) UpdateCount();
    }

    private void UpdateCount()
    {
        var selectable = _source.Count(i => i.CanSelect);
        var selected = _source.Count(i => i.IsSelected && i.CanSelect);
        var applied = _source.Count(i => i.State == TweakState.Applied);
        CountText.Text = $"{selected} of {selectable} selected" + (applied > 0 ? $"  ·  {applied} already applied" : "");
    }

    /// <summary>Ticks the safe and moderate tweaks that suit this PC and have no hardware warning (shown items only).</summary>
    private void SelectFitsClick(object sender, RoutedEventArgs e)
    {
        foreach (var i in Items.Where(i => i.Fits && i.CanSelect && i.Tweak.Risk != RiskLevel.Aggressive && i.HardwareNote.Length == 0))
            i.IsSelected = true;
    }

    private void SelectNoneClick(object sender, RoutedEventArgs e)
    {
        foreach (var i in Items) i.IsSelected = false; // only what is shown
    }
}
