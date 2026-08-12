using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace MCServerLauncher.WinUI.Views.Components.SettingCard;

/// <summary>
///    A settings card that hosts a title, description and an action button.
/// </summary>
public sealed partial class ActionSettingCard : UserControl
{
    public static readonly DependencyProperty SettingIconGlyphProperty =
        DependencyProperty.Register(nameof(SettingIconGlyph), typeof(string), typeof(ActionSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingTitleProperty =
        DependencyProperty.Register(nameof(SettingTitle), typeof(string), typeof(ActionSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingDescriptionProperty =
        DependencyProperty.Register(nameof(SettingDescription), typeof(string), typeof(ActionSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingButtonTextProperty =
        DependencyProperty.Register(nameof(SettingButtonText), typeof(string), typeof(ActionSettingCard),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SettingButtonCommandProperty =
        DependencyProperty.Register(nameof(SettingButtonCommand), typeof(ICommand), typeof(ActionSettingCard),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SettingButtonIsEnabledProperty =
        DependencyProperty.Register(nameof(SettingButtonIsEnabled), typeof(bool), typeof(ActionSettingCard),
            new PropertyMetadata(true));

    public static readonly DependencyProperty SettingButtonIsAccentProperty =
        DependencyProperty.Register(nameof(SettingButtonIsAccent), typeof(bool), typeof(ActionSettingCard),
            new PropertyMetadata(true, OnSettingButtonIsAccentChanged));

    public ActionSettingCard()
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

    public string SettingButtonText
    {
        get => (string)GetValue(SettingButtonTextProperty);
        set => SetValue(SettingButtonTextProperty, value);
    }

    public ICommand? SettingButtonCommand
    {
        get => (ICommand?)GetValue(SettingButtonCommandProperty);
        set => SetValue(SettingButtonCommandProperty, value);
    }

    public bool SettingButtonIsEnabled
    {
        get => (bool)GetValue(SettingButtonIsEnabledProperty);
        set => SetValue(SettingButtonIsEnabledProperty, value);
    }

    public bool SettingButtonIsAccent
    {
        get => (bool)GetValue(SettingButtonIsAccentProperty);
        set => SetValue(SettingButtonIsAccentProperty, value);
    }

    private static void OnSettingButtonIsAccentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ActionSettingCard control) return;
        if (e.NewValue is not bool isAccent) return;
        if (isAccent
            && Application.Current.Resources.TryGetValue("AccentButtonStyle", out var value)
            && value is Style accentStyle)
        {
            control.SettingButton.Style = accentStyle;
        }
        else
        {
            control.SettingButton.Style = null;
        }
    }
}
