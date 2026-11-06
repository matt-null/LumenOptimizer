using LumenOptimizer.Core.Update;
using LumenOptimizer.Core.Util;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LumenOptimizer.UI;

/// <summary>Shared by the startup check and the About page button. Always asks before downloading or restarting.</summary>
public static class UpdateFlow
{
    public static Task<UpdateCheck> CheckAsync() =>
        UpdateService.CheckAsync(StudioInfo.GitHubOwner, StudioInfo.GitHubRepo, StudioInfo.Version);

    public static async Task OfferAsync(XamlRoot root, UpdateInfo info)
    {
        var notes = string.IsNullOrWhiteSpace(info.Notes) ? "" : "\n\nWhat's new:\n" + info.Notes.Trim();
        var body = $"Lumen Optimizer {info.Latest} is available (you have {StudioInfo.Version}).{notes}\n\nThe app will download the update, close, replace its files and reopen. Your backups and saved data are kept.";
        if (!await Dialogs.ConfirmAsync(root, "Update available", body, "Download and install")) return;

        var bar = new ProgressBar { Minimum = 0, Maximum = 1, Width = 360 };
        var dlg = new ContentDialog
        {
            XamlRoot = root, Title = "Downloading update",
            Content = bar, CloseButtonText = null
        };
        var show = dlg.ShowAsync();
        try
        {
            var script = await UpdateService.PrepareAsync(info, StudioInfo.GitHubOwner, StudioInfo.GitHubRepo,
                new Progress<double>(v => bar.Value = v));
            dlg.Hide();
            UpdateService.Launch(script);
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            dlg.Hide();
            Log.Warn("Update failed: " + ex.Message);
            await Dialogs.ConfirmAsync(root, "Update failed", ex.Message + "\n\nYou can download it manually from the releases page.", "OK");
        }
    }

    /// <summary>Quiet check at startup: only speaks up when a newer version exists.</summary>
    public static async Task StartupCheckAsync(XamlRoot root)
    {
        var r = await CheckAsync();
        if (r.Update is not null) await OfferAsync(root, r.Update);
    }
}
