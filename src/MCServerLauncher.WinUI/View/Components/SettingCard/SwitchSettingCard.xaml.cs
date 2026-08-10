using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace MCServerLauncher.WinUI.Views.Components.SettingCard;

/// <summary>
///    A settings card that hosts a title, description and a toggle switch.
/// </summary>
public sealed partial class SwitchSettingCard : UserControl
{
    public static readonly DependencyProperty SettingIconGlyphProperty =
        DependencyProperty.Register(nameof(SettingIconGlyph), typeof(string), typeof(SwitchSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingTitleProperty =
        DependencyProperty.Register(nameof(SettingTitle), typeof(string), typeof(SwitchSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingDescriptionProperty =
        DependencyProperty.Register(nameof(SettingDescription), typeof(string), typeof(SwitchSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingSwitchOnContentProperty =
        DependencyProperty.Register(nameof(SettingSwitchOnContent), typeof(string), typeof(SwitchSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingSwitchOffContentProperty =
        DependencyProperty.Register(nameof(SettingSwitchOffContent), typeof(string), typeof(SwitchSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingSwitchCheckedProperty =
        DependencyProperty.Register(nameof(SettingSwitchChecked), typeof(bool), typeof(SwitchSettingCard),
            new PropertyMetadata(false));

    public static readonly DependencyProperty SettingSwitchEnabledProperty =
        DependencyProperty.Register(nameof(SettingSwitchEnabled), typeof(bool), typeof(SwitchSettingCard),
            new PropertyMetadata(true));

    public SwitchSettingCard()
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

    public string SettingSwitchOnContent
    {
        get => (string)GetValue(SettingSwitchOnContentProperty);
        set => SetValue(SettingSwitchOnContentProperty, value);
    }

    public string SettingSwitchOffContent
    {
        get => (string)GetValue(SettingSwitchOffContentProperty);
        set => SetValue(SettingSwitchOffContentProperty, value);
    }

    public bool SettingSwitchChecked
    {
        get => (bool)GetValue(SettingSwitchCheckedProperty);
        set => SetValue(SettingSwitchCheckedProperty, value);
    }

    public bool SettingSwitchEnabled
    {
        get => (bool)GetValue(SettingSwitchEnabledProperty);
        set => SetValue(SettingSwitchEnabledProperty, value);
    }
}
