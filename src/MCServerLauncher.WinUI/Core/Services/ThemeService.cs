using Windows.UI.Xaml;
using Windows.UI.ViewManagement;

namespace MCServerLauncher.WinUI.Core.Services;

public sealed class ThemeService : IThemeService
{
    public void Apply(FrameworkElement root, string theme)
    {
        var requestedTheme = theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        root.RequestedTheme = requestedTheme;
        WinUIIslands.Application.Current.RequestedTheme = requestedTheme == ElementTheme.Dark
            ? ApplicationTheme.Dark
            : requestedTheme == ElementTheme.Light
                ? ApplicationTheme.Light
                : IsSystemDarkMode() ? ApplicationTheme.Dark : ApplicationTheme.Light;
    }

    private static bool IsSystemDarkMode()
    {
        var background = new UISettings().GetColorValue(UIColorType.Background);
        var luminance = 0.299 * background.R + 0.587 * background.G + 0.114 * background.B;
        return luminance < 128;
    }
}
