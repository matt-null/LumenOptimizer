using System.Collections.Concurrent;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using LumenOptimizer.Core.Util;

namespace LumenOptimizer.UI;

/// <summary>
/// Live console docked at the bottom of the window. It shows every line the app logs: each command that is run,
/// every registry / service / task change, the result and any error. Fed by <see cref="Log"/> (Shell logs into it).
/// </summary>
public sealed partial class ConsolePanel : UserControl
{
    private const int MaxLines = 3000;
    private readonly ConcurrentQueue<string> _incoming = new();
    private readonly List<string> _lines = new();
    private readonly DispatcherQueueTimer _timer;
    private bool _dirty;

    /// <summary>Raised when the Hide button is pressed.</summary>
    public event Action? HideRequested;

    public ConsolePanel()
    {
        InitializeComponent();

        _lines.AddRange(Log.History().TakeLast(MaxLines));
        _dirty = true;

        Log.LineWritten += OnLine;

        _timer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(250);
        _timer.Tick += (_, _) => Flush();
        _timer.Start();
    }

    private void OnLine(string line) => _incoming.Enqueue(line);

    private void Flush()
    {
        while (_incoming.TryDequeue(out var l))
        {
            _lines.Add(l);
            _dirty = true;
        }

        if (!_dirty) return;
        _dirty = false;

        if (_lines.Count > MaxLines) _lines.RemoveRange(0, _lines.Count - MaxLines);
        if (Visibility == Visibility.Collapsed) return; // keep collecting, only draw when shown

        LogText.Text = string.Join("\n", _lines);
        CountText.Text = $"{_lines.Count} lines";
        Scroller.UpdateLayout();
        Scroller.ChangeView(null, Scroller.ScrollableHeight, null, disableAnimation: true);
    }

    /// <summary>Show or hide the panel. Content is redrawn when it becomes visible.</summary>
    public void SetOpen(bool open)
    {
        Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        _dirty = true;
        if (open) Flush();
    }

    public string AllText() => string.Join(Environment.NewLine, _lines);

    private void CopyClick(object sender, RoutedEventArgs e)
    {
        var data = new DataPackage();
        data.SetText(AllText());
        Clipboard.SetContent(data);
    }

    private void ClearClick(object sender, RoutedEventArgs e)
    {
        while (_incoming.TryDequeue(out _)) { }
        _lines.Clear();
        _dirty = true;
        Flush();
        LogText.Text = "";
        CountText.Text = "";
    }

    private void HideClick(object sender, RoutedEventArgs e) => HideRequested?.Invoke();

    private async void SaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop,
                SuggestedFileName = $"LumenOptimizer-log-{DateTime.Now:yyyyMMdd-HHmmss}"
            };
            picker.FileTypeChoices.Add("Text file", new List<string> { ".txt" });
            if (MainWindow.Current is { } w)
                WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(w));

            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            await Windows.Storage.FileIO.WriteTextAsync(file, AllText());
        }
        catch (Exception ex)
        {
            // Fall back to a fixed location if the picker is unavailable (it can fail in elevated sessions).
            try
            {
                Directory.CreateDirectory(AppPaths.LogDir);
                var path = Path.Combine(AppPaths.LogDir, $"console-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
                File.WriteAllText(path, AllText());
                Log.Warn($"Save dialog failed ({ex.Message}); wrote the log to {path}");
            }
            catch { /* nothing more we can do */ }
        }
    }
}
