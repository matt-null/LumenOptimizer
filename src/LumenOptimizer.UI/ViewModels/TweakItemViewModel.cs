using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using LumenOptimizer.Core.Models;

namespace LumenOptimizer.UI.ViewModels;

public partial class TweakItemViewModel : ObservableObject
{
    public TweakItemViewModel(Tweak tweak)
    {
        Tweak = tweak;
        CanRevert = AppServices.Engine.CanRevert(tweak);
        RiskBrush = new SolidColorBrush(tweak.Risk switch
        {
            RiskLevel.Safe => Colors.SeaGreen,
            RiskLevel.Moderate => Colors.DarkOrange,
            _ => Colors.IndianRed
        });
    }

    public Tweak Tweak { get; }
    public bool CanRevert { get; }
    public Brush RiskBrush { get; }

    public string Name => Tweak.Name;
    public string Description => Tweak.Description;
    /// <summary>Why this tweak is a bad idea on this specific PC (laptop, hard drive...), or "".</summary>
    public string HardwareNote => AppServices.Engine.HardwareWarning(Tweak) ?? "";

    /// <summary>Why this tweak suits this PC (for example it reduces writes on an SD card), or "".</summary>
    public string FitNote => AppServices.Engine.FitHint(Tweak) ?? "";
    public Visibility FitVisibility => FitNote.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    public bool Fits => FitNote.Length > 0;
    public string FitText => FitNote.Length == 0 ? "" : "Good fit for this PC: " + FitNote;

    public string Warning
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Tweak.Warning)) parts.Add(Tweak.Warning!);
            if (HardwareNote.Length > 0) parts.Add("Not recommended on this PC: " + HardwareNote);
            return string.Join("\n", parts);
        }
    }

    public Visibility WarningVisibility => Warning.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    public string RiskText => Tweak.Risk.ToString();

    public string Notes
    {
        get
        {
            var notes = new List<string>();
            if (Tweak.RequiresReboot) notes.Add("restart or sign-out needed");
            if (!CanRevert) notes.Add("no automatic undo");
            if (Tweak.MinBuild >= 26100) notes.Add("Windows 11 24H2 or newer");
            else if (Tweak.MinBuild >= 22000) notes.Add("Windows 11");
            if (Tweak.MaxBuild is > 0 and < 22000) notes.Add("Windows 10");
            if (Tweak.Unverified) notes.Add("not verified on every build");
            return string.Join(" · ", notes);
        }
    }

    /// <summary>Selected for applying in the wizard.</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Selected for undoing on the Undo page.</summary>
    [ObservableProperty]
    private bool _isUndoSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(CanSelect))]
    private TweakState _state = TweakState.Unknown;

    /// <summary>Tweaks that are already applied, or do not fit this Windows version, cannot be selected.</summary>
    public bool CanSelect => State != TweakState.NotApplicable && State != TweakState.Applied;

    public string StatusText => State switch
    {
        TweakState.Applied => "Already applied",
        TweakState.NotApplied => "Not applied",
        TweakState.Partial => "Partly applied",
        TweakState.NotApplicable => "Not for this Windows",
        _ => "Not detected"
    };
}
