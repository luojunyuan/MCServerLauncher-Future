using System.Windows.Input;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace MCServerLauncher.WinUI.Views.Components.Generic;

/// <summary>
/// StopTipLayer.xaml 的交互逻辑 (WinUI parity of WPF View/Components/Generic/StopTipLayer).
/// </summary>
public sealed partial class StopTipLayer : UserControl
{
    public StopTipLayer()
    {
        InitializeComponent();
    }

    public static readonly DependencyProperty SymbolProperty =
        DependencyProperty.Register(nameof(Symbol), typeof(string), typeof(StopTipLayer), new PropertyMetadata(null));

    public string Symbol
    {
        get => (string)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public static readonly DependencyProperty StopTipProperty =
        DependencyProperty.Register(nameof(StopTip), typeof(string), typeof(StopTipLayer), new PropertyMetadata(null));

    public string StopTip
    {
        get => (string)GetValue(StopTipProperty);
        set => SetValue(StopTipProperty, value);
    }

    public static readonly DependencyProperty StopDescriptionProperty =
        DependencyProperty.Register(nameof(StopDescription), typeof(string), typeof(StopTipLayer), new PropertyMetadata(null));

    public string StopDescription
    {
        get => (string)GetValue(StopDescriptionProperty);
        set => SetValue(StopDescriptionProperty, value);
    }

    public static readonly DependencyProperty ButtonTextProperty =
        DependencyProperty.Register(nameof(ButtonText), typeof(string), typeof(StopTipLayer), new PropertyMetadata(null));

    public string ButtonText
    {
        get => (string)GetValue(ButtonTextProperty);
        set => SetValue(ButtonTextProperty, value);
    }

    public static readonly DependencyProperty ButtonCommandProperty =
        DependencyProperty.Register(nameof(ButtonCommand), typeof(ICommand), typeof(StopTipLayer), new PropertyMetadata(null));

    public ICommand? ButtonCommand
    {
        get => (ICommand?)GetValue(ButtonCommandProperty);
        set => SetValue(ButtonCommandProperty, value);
    }

    public static readonly DependencyProperty ButtonCommandParameterProperty =
        DependencyProperty.Register(nameof(ButtonCommandParameter), typeof(object), typeof(StopTipLayer), new PropertyMetadata(null));

    public object ButtonCommandParameter
    {
        get => GetValue(ButtonCommandParameterProperty);
        set => SetValue(ButtonCommandParameterProperty, value);
    }

    /// <summary>
    /// x:Bind helper: collapses the action button while <paramref name="s"/> is null/empty.
    /// </summary>
    public static Visibility TextToVisibility(string s) =>
        string.IsNullOrEmpty(s) ? Visibility.Collapsed : Visibility.Visible;
}
