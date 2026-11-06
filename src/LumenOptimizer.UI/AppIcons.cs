using Microsoft.UI.Xaml.Media.Imaging;

namespace LumenOptimizer.UI;

/// <summary>Loads the logo straight from the app folder. Unpackaged apps cannot rely on ms-appx:/// paths.</summary>
public static class AppIcons
{
    /// <summary>Alright Dev wordmark. Black text for light theme, white text for dark theme.</summary>
    public static BitmapImage? StudioLogo(bool dark)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", dark ? "AlrightDev-forDark.png" : "AlrightDev-forLight.png");
            return File.Exists(path) ? new BitmapImage(new Uri(path)) : null;
        }
        catch { return null; }
    }

    public static BitmapImage? Logo()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.png");
            if (!File.Exists(path))
            {
                LumenOptimizer.Core.Util.Log.Warn("Logo file not found: " + path);
                return null;
            }
            return new BitmapImage(new Uri(path));
        }
        catch (Exception ex)
        {
            LumenOptimizer.Core.Util.Log.Warn("Could not load the logo: " + ex.Message);
            return null;
        }
    }
}
