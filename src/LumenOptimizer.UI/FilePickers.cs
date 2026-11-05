using Windows.Storage;
using Windows.Storage.Pickers;

namespace LumenOptimizer.UI;

/// <summary>Save/open dialogs for an unpackaged WinUI 3 app (the picker needs the window handle).</summary>
public static class FilePickers
{
    private static void Init(object picker)
    {
        if (MainWindow.Current is { } w)
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(w));
    }

    /// <returns>The path saved to, or null if cancelled.</returns>
    public static async Task<string?> SaveTextAsync(string suggestedName, string extension, string description, string content)
    {
        var picker = new FileSavePicker { SuggestedStartLocation = PickerLocationId.Desktop, SuggestedFileName = suggestedName };
        picker.FileTypeChoices.Add(description, new List<string> { extension });
        Init(picker);
        var file = await picker.PickSaveFileAsync();
        if (file is null) return null;
        await FileIO.WriteTextAsync(file, content);
        return file.Path;
    }

    public static async Task<string?> OpenTextAsync(string extension)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Desktop };
        picker.FileTypeFilter.Add(extension);
        Init(picker);
        var file = await picker.PickSingleFileAsync();
        return file is null ? null : await FileIO.ReadTextAsync(file);
    }
}
