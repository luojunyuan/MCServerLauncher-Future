using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace MCServerLauncher.WinUI.Views.Components.Generic;

/// <summary>
/// OS logo (Windows / Linux / Darwin) shown on daemon cards.
/// WinUI parity of the WPF daemon card logo: the logo is selected from
/// <see cref="SystemType"/> and rendered at 24x24 via a Viewbox.
/// </summary>
public sealed partial class OsLogo : UserControl
{
    public static readonly DependencyProperty SystemTypeProperty = DependencyProperty.Register(
        nameof(SystemType),
        typeof(string),
        typeof(OsLogo),
        new PropertyMetadata("Windows", OnSystemTypeChanged));

    public OsLogo()
    {
        InitializeComponent();
        UpdateLogoVisibility();
    }

    public string SystemType
    {
        get => (string)(GetValue(SystemTypeProperty) ?? string.Empty);
        set => SetValue(SystemTypeProperty, value);
    }

    private static void OnSystemTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((OsLogo)d).UpdateLogoVisibility();
    }

    private void UpdateLogoVisibility()
    {
        var isWindows = IsSystemType("Windows");
        var isLinux = IsSystemType("Linux");
        var isDarwin = IsSystemType("Darwin") || IsSystemType("macOS");

        WindowsLogo.Visibility = isWindows ? Visibility.Visible : Visibility.Collapsed;
        LinuxLogo.Visibility = isLinux ? Visibility.Visible : Visibility.Collapsed;
        DarwinLogo.Visibility = isDarwin ? Visibility.Visible : Visibility.Collapsed;

        // WPF shows no logo when SystemType is unknown; collapse the whole slot to match.
        LogoViewbox.Visibility = isWindows || isLinux || isDarwin
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private bool IsSystemType(string expected) =>
        SystemType.StartsWith(expected, StringComparison.OrdinalIgnoreCase);
}
