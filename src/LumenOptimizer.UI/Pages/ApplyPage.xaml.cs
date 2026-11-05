using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LumenOptimizer.Core.Engine;
using LumenOptimizer.Core.Models;
using LumenOptimizer.UI.ViewModels;

namespace LumenOptimizer.UI.Pages;

public sealed partial class ApplyPage : Page
{
    private bool _started;

    public ObservableCollection<string> Log { get; } = new();

    public ApplyPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _started = false;
        Log.Clear();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_started) return;
        _started = true;

        var items = WizardState.Selected.ToList();
        var errors = new List<string>();
        var applied = 0;
        var needsRestart = false;

        try
        {
            if (WizardState.CreateRestorePoint)
            {
                StatusText.Text = "Creating a restore point…";
                var rp = await Task.Run(() => RestorePointService.Create("Lumen Optimizer - before applying tweaks"));
                if (rp.Success)
                {
                    Log.Add("✔  Restore point created");
                }
                else
                {
                    Log.Add("⚠  Restore point failed: " + rp.Message);
                    var proceed = await AskContinueAsync(rp.Message);
                    if (!proceed)
                    {
                        MainWindow.Current?.StartWizard();
                        return;
                    }
                }
            }

            Bar.IsIndeterminate = false;
            Bar.Maximum = Math.Max(1, items.Count);
            Bar.Value = 0;

            var done = 0;
            foreach (var item in items)
            {
                StatusText.Text = $"Applying {item.Name}  ({done + 1} of {items.Count})";
                var tweak = item.Tweak;
                var result = await Task.Run(() => AppServices.Engine.Apply(tweak));
                item.State = await Task.Run(() => AppServices.Engine.GetState(tweak));
                item.IsSelected = false;

                if (result.Success)
                {
                    applied++;
                    if (tweak.RequiresReboot) needsRestart = true;
                    Log.Add("✔  " + item.Name);
                }
                else
                {
                    foreach (var err in result.Errors) errors.Add($"{item.Name}: {err}");
                    Log.Add("✖  " + item.Name + " - " + result.Errors.FirstOrDefault());
                }

                Bar.Value = ++done;
                if (Log.Count > 0) LogList.ScrollIntoView(Log[^1]);
            }
        }
        catch (Exception ex)
        {
            errors.Add("Unexpected error: " + ex.Message);
            LumenOptimizer.Core.Util.Log.Error("Apply page: " + ex);
        }

        StatusText.Text = "Finished";
        WizardState.LastOutcome = new ApplyOutcome(applied, errors.Count, needsRestart, errors);

        await Task.Delay(900);
        MainWindow.Current?.ShowDone();
    }

    private async Task<bool> AskContinueAsync(string reason)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Restore point failed",
            Content = new TextBlock
            {
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                Text = reason + "\n\nContinue without a restore point? Lumen Optimizer still saves the original value of every setting it changes, so you can undo changes from the start screen."
            },
            PrimaryButtonText = "Continue anyway",
            CloseButtonText = "Go back",
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
