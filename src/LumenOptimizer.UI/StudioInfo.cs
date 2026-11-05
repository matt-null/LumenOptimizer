using Microsoft.UI.Xaml;

namespace LumenOptimizer.UI;

/// <summary>Everything shown on the About page. Edit the text here; no XAML changes needed.</summary>
public static class StudioInfo
{
    public const string Name = "Alright Dev";
    public const string Version = "0.4.3";

    public const string Tagline = "Software, smart home and business development.";

    public const string About =
        "Alright Dev is a development company specializing in software, smart home, and business development.";

    /// <summary>Fill these in to show contact links on the About page. Leave empty to hide them.</summary>
    public const string Website = "https://alrightdev.com";
    public const string Email = "";

    public static IReadOnlyList<StudioProject> Services { get; } = new StudioProject[]
    {
        new("Software", "Windows utilities, web apps and tools built to be fast and dependable."),
        new("Smart home", "Connected-home setups and automation."),
        new("Business development", "Systems and software that help businesses run and grow.")
    };

    public static IReadOnlyList<StudioProject> Specialties { get; } = new StudioProject[]
    {
        new("Security Installations", "Installation of security and surveillance systems."),
        new("Windows Apps", "Custom Windows software and utilities."),
        new("Business Systems", "Software and systems that help businesses run day to day."),
        new("Game Servers", "Setup and management of game servers.")
    };

    public static IReadOnlyList<CreditItem> Credits { get; } = new CreditItem[]
    {
        new("Created by", "Alright Dev", "https://alrightdev.com"),
        new("Engineering assistance", "Claude, by Anthropic", "https://www.anthropic.com/claude"),
        new("Inspired by", "Optimizer by hellzerg (feature ideas only; no code was used)", "https://github.com/hellzerg/optimizer"),
        new("Built with", ".NET 8 and Windows App SDK (WinUI 3)"),
        new("Icon", "Feather by Cole Bemis (MIT) and Lucide (ISC)", "https://lucide.dev"),
        new("UI toolkit", "CommunityToolkit.Mvvm"),
        new("Installer", "Inno Setup")
    };
}

public sealed record StudioProject(string Name, string Description);

public sealed record CreditItem(string Role, string Name, string Url = "")
{
    public Visibility LinkVisibility => string.IsNullOrEmpty(Url) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility PlainVisibility => string.IsNullOrEmpty(Url) ? Visibility.Visible : Visibility.Collapsed;
    public Uri? LinkUri => Uri.TryCreate(Url, UriKind.Absolute, out var u) ? u : null;
}
