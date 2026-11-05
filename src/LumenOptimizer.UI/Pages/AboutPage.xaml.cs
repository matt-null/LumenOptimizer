using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace LumenOptimizer.UI.Pages;

public sealed partial class AboutPage : Page
{
    public IReadOnlyList<StudioProject> Services => StudioInfo.Services;
    public IReadOnlyList<StudioProject> Specialties => StudioInfo.Specialties;
    public IReadOnlyList<CreditItem> Credits => StudioInfo.Credits;

    public AboutPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        NameText.Text = StudioInfo.Name;
        TaglineText.Text = StudioInfo.Tagline;
        AboutText.Text = StudioInfo.About;
        FooterText.Text = $"Lumen Optimizer {StudioInfo.Version}  ·  Built with ❤️ by {StudioInfo.Name}";

        if (Uri.TryCreate(StudioInfo.Website, UriKind.Absolute, out var site))
        {
            WebsiteLink.Content = StudioInfo.Website;
            WebsiteLink.NavigateUri = site;
            WebsiteLink.Visibility = Visibility.Visible;
        }

        if (!string.IsNullOrWhiteSpace(StudioInfo.Email))
        {
            EmailLink.Content = StudioInfo.Email;
            EmailLink.NavigateUri = new Uri("mailto:" + StudioInfo.Email);
            EmailLink.Visibility = Visibility.Visible;
        }

        ContactPanel.Visibility = WebsiteLink.Visibility == Visibility.Visible || EmailLink.Visibility == Visibility.Visible
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BackClick(object sender, RoutedEventArgs e) => MainWindow.Current?.CloseAbout();
}
