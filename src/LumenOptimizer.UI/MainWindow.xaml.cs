using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using LumenOptimizer.UI.Pages;
using Windows.Graphics;

namespace LumenOptimizer.UI;

/// <summary>Hosts the wizard: title bar, the page frame, and the Back / Next footer.</summary>
public sealed partial class MainWindow : Window
{
    private readonly List<(Type Page, object? Param)> _flow = new();
    private int _index = -1;
    private bool _standalone;
    private Action? _backOverride;

    private LumenOptimizer.Core.Tools.RemovableWatcher? _usbWatcher;
    public static new MainWindow? Current { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        Current = this;
        Title = "Lumen Optimizer";

        // Unpackaged apps get no icon unless it is set explicitly; this covers the taskbar, Alt+Tab and the title bar.
        try
        {
            var icon = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (System.IO.File.Exists(icon))
            {
                AppWindow.SetIcon(icon);
                SetWin32Icon(icon);   // second route; the taskbar button honours this one most reliably
            }
            else
            {
                LumenOptimizer.Core.Util.Log.Warn("Icon file not found: " + icon);
            }
        }
        catch (Exception ex) { LumenOptimizer.Core.Util.Log.Warn("Could not set the window icon: " + ex.Message); }

        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        CenterAndResize(1100, 780);

        // Wizard order: one page per category -> review. Nothing is preselected.
        foreach (var category in WizardState.Categories)
            _flow.Add((typeof(TweaksPage), category));
        _flow.Add((typeof(ReviewPage), null));

        TitleIcon.Source = AppIcons.Logo();
        ConsoleDock.HideRequested += () => SetConsoleOpen(false);

        ShowSplash();

        // Offer to clean a USB stick / SD card when one is plugged in.
        _usbWatcher = new LumenOptimizer.Core.Tools.RemovableWatcher();
        _usbWatcher.DriveArrived += d => DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (!LumenOptimizer.Core.Tools.RemovableCleaner.PromptEnabled || !BackButton.IsEnabled || Content is not FrameworkElement root) return;
                if (await Dialogs.ConfirmAsync(root.XamlRoot, "Drive plugged in",
                        $"{d.Name} {d.VolumeLabel} was just connected. Scan it for junk files?", "Open the drive cleaner"))
                    ShowTool(typeof(ManagerPage), "USB drive cleaner", "UsbClean");
            }
            catch { }
        });

        if (Content is FrameworkElement fe)
            fe.Loaded += async (_, _) =>
            {
                try { await Task.Delay(1500); await UpdateFlow.StartupCheckAsync(fe.XamlRoot); }
                catch (Exception ex) { LumenOptimizer.Core.Util.Log.Warn("Startup update check: " + ex.Message); }
            };
    }

    // -------------------------------------------------------------- console

    public bool ConsoleOpen => ConsoleDock.Visibility == Visibility.Visible;

    public void SetConsoleOpen(bool open)
    {
        ConsoleDock.SetOpen(open);
        ConsoleToggle.IsChecked = open;
    }

    private void ConsoleToggleClick(object sender, RoutedEventArgs e) => SetConsoleOpen(ConsoleToggle.IsChecked == true);

    // ---- window icon via Win32 (WM_SETICON), in case AppWindow.SetIcon alone is not picked up by the taskbar
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private void SetWin32Icon(string icoPath)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        const uint WM_SETICON = 0x0080, IMAGE_ICON = 1, LR_LOADFROMFILE = 0x10;
        var big = LoadImage(IntPtr.Zero, icoPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
        var small = LoadImage(IntPtr.Zero, icoPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
        if (big != IntPtr.Zero) SendMessage(hwnd, WM_SETICON, (IntPtr)1, big);
        if (small != IntPtr.Zero) SendMessage(hwnd, WM_SETICON, IntPtr.Zero, small);
    }

    private void CenterAndResize(int width, int height)
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var work = area.WorkArea;
        width = Math.Min(width, work.Width);
        height = Math.Min(height, work.Height);
        AppWindow.MoveAndResize(new RectInt32(
            work.X + (work.Width - width) / 2,
            work.Y + (work.Height - height) / 2,
            width, height));
    }

    // ------------------------------------------------------------ navigation

    public void ShowSplash()
    {
        _index = -1;
        _standalone = false;
        _backOverride = null;
        HideNav();
        SetCreditEnabled(false); // enabled again once the scan has finished
        ContentFrame.Navigate(typeof(SplashPage), null, new EntranceNavigationTransitionInfo());
    }

    public void StartWizard() => GoToFlow(0, forward: true);

    public void ShowUndo()
    {
        _standalone = true;
        _index = -1;
        _backOverride = null;
        SetCreditEnabled(true);
        BackButton.Visibility = Visibility.Visible;
        NextButton.Visibility = Visibility.Collapsed;
        StepText.Text = "";
        ContentFrame.Navigate(typeof(UndoPage), null, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    /// <summary>The tools hub (Cleaner, Startup, Uninstaller, Hosts, Integrator, Network). Back returns to the splash screen.</summary>
    public void ShowTools()
    {
        _standalone = true;
        _index = -1;
        _backOverride = null;
        SetCreditEnabled(true);
        BackButton.Visibility = Visibility.Visible;
        NextButton.Visibility = Visibility.Collapsed;
        StepText.Text = "Tools";
        ContentFrame.Navigate(typeof(ToolsPage), null, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    /// <summary>Opens one tool page. Back returns to the tools hub.</summary>
    public void ShowTool(Type page, string title, object? param = null)
    {
        _standalone = true;
        _index = -1;
        _backOverride = ShowTools;
        SetCreditEnabled(true);
        BackButton.Visibility = Visibility.Visible;
        NextButton.Visibility = Visibility.Collapsed;
        StepText.Text = title;
        ContentFrame.Navigate(page, param, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    /// <summary>Tool pages call this while an action runs so the user cannot navigate away mid-change.</summary>
    public void SetBusy(bool busy)
    {
        BackButton.IsEnabled = !busy;
        SetCreditEnabled(!busy);
    }

    public void ShowApply()
    {
        HideNav();
        SetCreditEnabled(false); // leaving the page mid-apply would restart it
        ContentFrame.Navigate(typeof(ApplyPage), null, new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromRight });
    }

    public void ShowDone()
    {
        HideNav();
        SetCreditEnabled(true);
        ContentFrame.Navigate(typeof(DonePage), null, new EntranceNavigationTransitionInfo());
    }

    private void GoToFlow(int index, bool forward)
    {
        _index = index;
        _standalone = false;
        SetCreditEnabled(true);
        UpdateFooter();

        var (page, param) = _flow[index];
        ContentFrame.Navigate(page, param, new SlideNavigationTransitionInfo
        {
            Effect = forward ? SlideNavigationTransitionEffect.FromRight : SlideNavigationTransitionEffect.FromLeft
        });
    }

    // ---------------------------------------------------------------- about

    private (Visibility Back, Visibility Next, string Step, object? NextText, bool NextEnabled)? _footerBeforeAbout;

    public void SetCreditEnabled(bool enabled) => CreditLink.IsEnabled = enabled;

    private void CreditClick(object sender, RoutedEventArgs e) => ShowAbout();

    public void ShowAbout()
    {
        if (ContentFrame.Content is AboutPage) return;

        _footerBeforeAbout = (BackButton.Visibility, NextButton.Visibility, StepText.Text, NextButton.Content, NextButton.IsEnabled);
        HideNav();
        ContentFrame.Navigate(typeof(AboutPage), null,
            new SlideNavigationTransitionInfo { Effect = SlideNavigationTransitionEffect.FromBottom });
    }

    public void CloseAbout()
    {
        // Put the footer back first; the page we return to may then adjust it (for example the Review page's Next button).
        if (_footerBeforeAbout is { } f)
        {
            BackButton.Visibility = f.Back;
            NextButton.Visibility = f.Next;
            StepText.Text = f.Step;
            NextButton.Content = f.NextText;
            NextButton.IsEnabled = f.NextEnabled;
            _footerBeforeAbout = null;
        }

        if (ContentFrame.CanGoBack) ContentFrame.GoBack();
    }

    // --------------------------------------------------------------- footer

    private void HideNav()
    {
        BackButton.Visibility = Visibility.Collapsed;
        NextButton.Visibility = Visibility.Collapsed;
        StepText.Text = "";
    }

    private void UpdateFooter()
    {
        BackButton.Visibility = Visibility.Visible;
        NextButton.Visibility = Visibility.Visible;
        NextButton.IsEnabled = true;
        NextButton.Content = _index == _flow.Count - 1 ? "Apply changes" : "Next";
        StepText.Text = $"Step {_index + 1} of {_flow.Count}";
    }

    /// <summary>Pages call this to change the Next button (for example "Apply 12 changes").</summary>
    public void SetNext(string text, bool enabled)
    {
        NextButton.Content = text;
        NextButton.IsEnabled = enabled;
    }

    private void BackClick(object sender, RoutedEventArgs e)
    {
        if (_standalone && _backOverride is { } back) back();
        else if (_standalone || _index <= 0) ShowSplash();
        else GoToFlow(_index - 1, forward: false);
    }

    private void NextClick(object sender, RoutedEventArgs e)
    {
        if (_index < 0) return;
        if (_index >= _flow.Count - 1) ShowApply();
        else GoToFlow(_index + 1, forward: true);
    }
}
