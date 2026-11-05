using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;

namespace LumenOptimizer.UI.Pages;

public sealed partial class DonePage : Page
{
    public DonePage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        var outcome = WizardState.LastOutcome ?? new ApplyOutcome(0, 0, false, Array.Empty<string>());

        if (outcome.Failed == 0)
        {
            TitleText.Text = "All done";
            SummaryText.Text = $"{outcome.Applied} change(s) applied. You can undo any of them later from the start screen.";
        }
        else
        {
            IconBorder.Background = new SolidColorBrush(Colors.DarkOrange);
            IconGlyph.Glyph = ""; // warning
            TitleText.Text = "Finished with problems";
            SummaryText.Text = $"{outcome.Applied} change(s) applied, {outcome.Failed} problem(s).";

            var lines = outcome.Errors.Take(8).ToList();
            if (outcome.Errors.Count > 8) lines.Add($"…and {outcome.Errors.Count - 8} more. See %ProgramData%\\LumenOptimizer\\logs.");
            ErrorsText.Text = string.Join("\n", lines);
            ErrorsText.Visibility = Visibility.Visible;
        }

        RestartBar.IsOpen = outcome.NeedsRestart;
        RestartButton.Visibility = outcome.NeedsRestart ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void RestartClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = "Restart now?",
            Content = "Windows will restart in 15 seconds. Save your work in other apps first.",
            PrimaryButtonText = "Restart",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

        Process.Start(new ProcessStartInfo("shutdown.exe", "/r /t 15 /c \"Lumen Optimizer: restarting to finish applying changes\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true
        });
    }

    private void StartOverClick(object sender, RoutedEventArgs e)
    {
        WizardState.Scanned = false; // re-detect everything so the new state is shown
        MainWindow.Current?.ShowSplash();
    }

    private void CloseClick(object sender, RoutedEventArgs e) => Application.Current.Exit();
}
