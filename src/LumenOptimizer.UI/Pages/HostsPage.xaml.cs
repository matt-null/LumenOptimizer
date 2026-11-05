using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using LumenOptimizer.Core.Tools;

namespace LumenOptimizer.UI.Pages;

public sealed partial class HostsPage : Page
{
    private string _original = "";

    public HostsPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Reload();
    }

    private void Reload()
    {
        try
        {
            _original = HostsFile.Read();
            Editor.Text = _original;
            BackupBox.ItemsSource = HostsFile.Backups();
            ResultBar.IsOpen = false;
        }
        catch (Exception ex)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not read the hosts file", ex.Message);
        }
    }

    private void ReloadClick(object sender, RoutedEventArgs e) => Reload();

    private async void AddTelemetryClick(object sender, RoutedEventArgs e)
    {
        var body = "This adds the following host names to the editor, pointing at 0.0.0.0, so those addresses stop working:\n\n" +
                   string.Join("\n", HostsFile.TelemetryHosts) +
                   "\n\nThis is a common community block list, and Microsoft can change the names it uses at any time. " +
                   "It does not stop Windows telemetry by itself (use the Privacy page for that). Windows Defender sometimes reports hosts-file " +
                   "changes as a warning. Nothing is saved yet; you will see the full list of changes before saving.";
        if (!await Dialogs.ConfirmAsync(XamlRoot, "Add telemetry block list to the editor?", body, "Add to editor")) return;

        Editor.Text = HostsFile.AddEntries(Editor.Text, HostsFile.TelemetryHosts);
        Dialogs.Show(ResultBar, InfoBarSeverity.Informational, "Added to the editor", "Click Save… to review and write the file.");
    }

    private void LoadBackupClick(object sender, RoutedEventArgs e)
    {
        if (BackupBox.SelectedItem is not string path)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Informational, "No backup chosen", "Pick a backup from the list first.");
            return;
        }

        try
        {
            Editor.Text = File.ReadAllText(path);
            Dialogs.Show(ResultBar, InfoBarSeverity.Informational, "Backup loaded into the editor", "Click Save… to put it back as the hosts file.");
        }
        catch (Exception ex)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not read the backup", ex.Message);
        }
    }

    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        var text = Editor.Text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n");

        var problems = HostsFile.Validate(text);
        if (problems.Count > 0)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Fix these lines first", string.Join("\n", problems.Take(6)));
            return;
        }

        var (added, removed) = HostsFile.Diff(_original, text);
        if (added.Count == 0 && removed.Count == 0)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Informational, "No changes", "The editor matches the hosts file on disk.");
            return;
        }

        var body = (added.Count > 0 ? "Lines added:\n" + string.Join("\n", added.Take(40).Select(l => "+ " + l)) + (added.Count > 40 ? $"\n… and {added.Count - 40} more" : "") + "\n\n" : "")
                 + (removed.Count > 0 ? "Lines removed:\n" + string.Join("\n", removed.Take(40).Select(l => "- " + l)) + (removed.Count > 40 ? $"\n… and {removed.Count - 40} more" : "") + "\n\n" : "")
                 + "The current file is backed up first, so you can load it back from the Backups list.";
        if (!await Dialogs.ConfirmAsync(XamlRoot, "Save the hosts file?", body, "Save")) return;

        try
        {
            var backup = await Task.Run(() => HostsFile.Save(text));
            Dialogs.Show(ResultBar, InfoBarSeverity.Success, "Saved", $"The previous file was backed up to {backup}");
            Reload();
        }
        catch (Exception ex)
        {
            Dialogs.Show(ResultBar, InfoBarSeverity.Error, "Could not save", ex.Message + "\n(Security software can block changes to the hosts file.)");
        }
    }
}
