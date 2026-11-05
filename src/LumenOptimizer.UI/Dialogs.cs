using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LumenOptimizer.UI;

/// <summary>Small helpers so every destructive action asks the same way.</summary>
public static class Dialogs
{
    /// <summary>Shows a preview of what will happen. Returns true only when the user clicks the primary button. Cancel is the default.</summary>
    public static async Task<bool> ConfirmAsync(XamlRoot root, string title, string body, string primary)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = new ScrollViewer
            {
                MaxHeight = 380,
                Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true }
            },
            PrimaryButtonText = primary,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    public static void Show(InfoBar bar, InfoBarSeverity severity, string title, string message)
    {
        bar.Severity = severity;
        bar.Title = title;
        bar.Message = message;
        bar.IsOpen = true;
    }
}
