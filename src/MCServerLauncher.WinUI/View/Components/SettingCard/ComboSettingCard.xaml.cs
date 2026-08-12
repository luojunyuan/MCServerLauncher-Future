using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace MCServerLauncher.WinUI.Views.Components.SettingCard;

/// <summary>
///    A settings card that hosts a title, description and a combo box.
/// </summary>
public sealed partial class ComboSettingCard : UserControl
{
    public static readonly DependencyProperty SettingIconGlyphProperty =
        DependencyProperty.Register(nameof(SettingIconGlyph), typeof(string), typeof(ComboSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingTitleProperty =
        DependencyProperty.Register(nameof(SettingTitle), typeof(string), typeof(ComboSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingDescriptionProperty =
        DependencyProperty.Register(nameof(SettingDescription), typeof(string), typeof(ComboSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingItemsSourceProperty =
        DependencyProperty.Register(nameof(SettingItemsSource), typeof(object), typeof(ComboSettingCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SettingSelectedIndexProperty =
        DependencyProperty.Register(nameof(SettingSelectedIndex), typeof(int), typeof(ComboSettingCard),
            new PropertyMetadata(0));

    public static readonly DependencyProperty SettingEnabledProperty =
        DependencyProperty.Register(nameof(SettingEnabled), typeof(bool), typeof(ComboSettingCard),
            new PropertyMetadata(true));

    public ComboSettingCard()
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

    public object SettingItemsSource
    {
        get => GetValue(SettingItemsSourceProperty);
        set => SetValue(SettingItemsSourceProperty, value);
    }

    public int SettingSelectedIndex
    {
        get => (int)GetValue(SettingSelectedIndexProperty);
        set => SetValue(SettingSelectedIndexProperty, value);
    }

    public bool SettingEnabled
    {
        get => (bool)GetValue(SettingEnabledProperty);
        set => SetValue(SettingEnabledProperty, value);
    }
}
