using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace MCServerLauncher.WinUI.Views.Components.SettingCard;

/// <summary>
///    A settings card that hosts a title, description and a slider with a live value label.
/// </summary>
public sealed partial class RangeSettingCard : UserControl
{
    public static readonly DependencyProperty SettingIconGlyphProperty =
        DependencyProperty.Register(nameof(SettingIconGlyph), typeof(string), typeof(RangeSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingTitleProperty =
        DependencyProperty.Register(nameof(SettingTitle), typeof(string), typeof(RangeSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingDescriptionProperty =
        DependencyProperty.Register(nameof(SettingDescription), typeof(string), typeof(RangeSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingMinimumProperty =
        DependencyProperty.Register(nameof(SettingMinimum), typeof(double), typeof(RangeSettingCard),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty SettingMaximumProperty =
        DependencyProperty.Register(nameof(SettingMaximum), typeof(double), typeof(RangeSettingCard),
            new PropertyMetadata(100d));

    public static readonly DependencyProperty SettingValueProperty =
        DependencyProperty.Register(nameof(SettingValue), typeof(double), typeof(RangeSettingCard),
            new PropertyMetadata(0d));

    public static readonly DependencyProperty SettingValueTextProperty =
        DependencyProperty.Register(nameof(SettingValueText), typeof(string), typeof(RangeSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingEnabledProperty =
        DependencyProperty.Register(nameof(SettingEnabled), typeof(bool), typeof(RangeSettingCard),
            new PropertyMetadata(true));

    public RangeSettingCard()
    {
        InitializeComponent();
    }

    public string SettingIconGlyph
    {
        get => (string)GetValue(SettingIconGlyphProperty);
        set => SetValue(SettingIconGlyphProperty, value);
    }

    public string SettingTitle
    {
        get => (string)GetValue(SettingTitleProperty);
        set => SetValue(SettingTitleProperty, value);
    }

    public string SettingDescription
    {
        get => (string)GetValue(SettingDescriptionProperty);
        set => SetValue(SettingDescriptionProperty, value);
    }

    public double SettingMinimum
    {
        get => (double)GetValue(SettingMinimumProperty);
        set => SetValue(SettingMinimumProperty, value);
    }

    public double SettingMaximum
    {
        get => (double)GetValue(SettingMaximumProperty);
        set => SetValue(SettingMaximumProperty, value);
    }

    public double SettingValue
    {
        get => (double)GetValue(SettingValueProperty);
        set => SetValue(SettingValueProperty, value);
    }

    public string SettingValueText
    {
        get => (string)GetValue(SettingValueTextProperty);
        set => SetValue(SettingValueTextProperty, value);
    }

    public bool SettingEnabled
    {
        get => (bool)GetValue(SettingEnabledProperty);
        set => SetValue(SettingEnabledProperty, value);
    }
}
