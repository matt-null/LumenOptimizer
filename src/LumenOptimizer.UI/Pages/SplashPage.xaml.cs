using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LumenOptimizer.Core.Engine;
using LumenOptimizer.Core.Models;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.UI.Pages;

public sealed partial class SplashPage : Page
{
    public SplashPage()
    {
        InitializeComponent();
        LogoImage.Source = AppIcons.Logo();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        AdminBar.IsOpen = !Elevation.IsAdministrator();

        if (!WizardState.Scanned)
        {
            ScanPanel.Visibility = Visibility.Visible;
            ReadyPanel.Visibility = Visibility.Collapsed;
            Ring.IsActive = true;

            await WizardState.ScanAsync((done, total) => ScanText.Text = $"Scanning your system… {done} of {total}");
        }

        ShowReady();
    }

    private async void ShowReady()
    {
        Ring.IsActive = false;
        MainWindow.Current?.SetCreditEnabled(true);
        ScanPanel.Visibility = Visibility.Collapsed;
        ReadyPanel.Visibility = Visibility.Visible;

        var total = WizardState.All.Count(i => i.State != TweakState.NotApplicable);
        var applied = WizardState.All.Count(i => i.State == TweakState.Applied);
        SummaryText.Text = $"{total} tweaks available for this PC. {applied} are already applied.";

        UndoButton.Visibility = AppServices.Backups.Ids.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        if (WizardState.Profile is { } p)
        {
            ProfileText.Text = p.Summary;
            var warning = p.SupportWarning;
            SupportBar.IsOpen = warning is not null;
            if (warning is not null)
            {
                SupportBar.Title = p.IsWindows10 ? "Windows 10 is out of support" : "Windows version";
                SupportBar.Message = warning;
            }
        }

        await AskAboutConsoleAsync();
    }

    /// <summary>Asked once per run, right after the scan: show the live console or keep it hidden.</summary>
    private async Task AskAboutConsoleAsync()
    {
        if (WizardState.ConsoleAsked) return;
        WizardState.ConsoleAsked = true;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Show the console window?",
            Content = "The console is a live feed of everything Lumen Optimizer does: each command it runs, every registry, " +
                      "service and task change, and every result or error. You can copy it or save it as a log.\n\n" +
                      "You can turn it on or off at any time with the Console button in the bottom bar.",
            PrimaryButtonText = "Show console",
            CloseButtonText = "No, keep it hidden",
            DefaultButton = ContentDialogButton.Primary
        };

        var result = await dialog.ShowAsync();
        MainWindow.Current?.SetConsoleOpen(result == ContentDialogResult.Primary);
    }

    private async void LoadSelectionClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var json = await FilePickers.OpenTextAsync(".json");
            if (json is null) return;
            var (selected, skipped) = WizardState.ImportSelection(json);
            LoadText.Text = $"{selected} tweak(s) selected from the file" +
                            (skipped > 0 ? $"; {skipped} skipped (already applied, not for this Windows, or unknown). " : ". ") +
                            "Click Get started to review them.";
        }
        catch (Exception ex)
        {
            LoadText.Text = "Could not load that file: " + ex.Message;
        }
    }

    private void ToolsClick(object sender, RoutedEventArgs e) => MainWindow.Current?.ShowTools();

    private void StartClick(object sender, RoutedEventArgs e) => MainWindow.Current?.StartWizard();

    private void UndoClick(object sender, RoutedEventArgs e) => MainWindow.Current?.ShowUndo();
}
