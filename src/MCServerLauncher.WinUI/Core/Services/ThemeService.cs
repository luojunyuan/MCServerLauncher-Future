using System;
using System.Runtime.CompilerServices;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace MCServerLauncher.WinUI.Core.Services;

/// <summary>
///     Applies the selected theme to the root element. Popups (Flyout / MenuFlyout /
///     ComboBox drop-down) render in a separate popup root and do not inherit a runtime
///     RequestedTheme change on the root (microsoft-ui-xaml#6622), and the WinUI
///     Islands host does not propagate Application.RequestedTheme to them either.
///     Instead, every popup source (ComboBox, Flyout/MenuFlyout, ContentDialog) is
///     hooked once and the open popups are re-themed synchronously at the instant a
///     popup opens — before it is painted — so popups always open in the selected theme
///     and follow theme hot-switches with no timers or per-frame work.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private FrameworkElement? _root;
    private ElementTheme _currentTheme = ElementTheme.Default;
    // Weak keys: a ConditionalWeakTable does not pin its keys, so dismissed
    // ContentDialogs / closed popups can be collected instead of leaking their
    // visual trees for the lifetime of the process (unlike a HashSet).
    private readonly ConditionalWeakTable<object, object?> _hooked = new();

    public void Apply(FrameworkElement root, string theme)
    {
        _root = root;
        _currentTheme = theme switch
        {
            "light" => ElementTheme.Light,
            "dark" => ElementTheme.Dark,
            _ => ElementTheme.Default
        };

        root.RequestedTheme = _currentTheme;
        WinUIIslands.Application.Current.RequestedTheme = _currentTheme == ElementTheme.Dark
            ? ApplicationTheme.Dark
            : _currentTheme == ElementTheme.Light
                ? ApplicationTheme.Light
                : IsSystemDarkMode() ? ApplicationTheme.Dark : ApplicationTheme.Light;

        // Re-walk on every navigation so controls on newly loaded pages are hooked too.
        if (FindFrame(root) is { } frame)
        {
            frame.Navigated -= OnFrameNavigated;
            frame.Navigated += OnFrameNavigated;
        }

        HookPopupSources(root);
        ThemeOpenPopups();
    }

    private void OnFrameNavigated(object sender, NavigationEventArgs e)
    {
        if (e.Content is DependencyObject content)
            HookPopupSources(content);
        ThemeOpenPopups();
    }

    private void HookPopupSources(DependencyObject node)
    {
        if (node is ComboBox comboBox)
        {
            if (_hooked.TryAdd(comboBox, null))
                comboBox.DropDownOpened += (_, _) => ThemeOpenPopups();
        }

        if (node is ContentDialog dialog)
        {
            if (_hooked.TryAdd(dialog, null))
                dialog.Opened += (_, _) => ThemeOpenPopups();
        }

        if (node is FrameworkElement fe)
        {
            HookFlyout(fe.ContextFlyout);
            HookFlyout(fe is Button button ? button.Flyout : null);
            HookFlyout(FlyoutBase.GetAttachedFlyout(fe));
        }

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
            HookPopupSources(VisualTreeHelper.GetChild(node, i));
    }

    private void HookFlyout(FlyoutBase? flyout)
    {
        if (flyout is null || !_hooked.TryAdd(flyout, null))
            return;
        flyout.Opened += (_, _) => ThemeOpenPopups();
    }

    private void ThemeOpenPopups()
    {
        if (_root?.XamlRoot is not { } xamlRoot) return;
        try
        {
            var themed = 0;
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
            {
                // Self-healing: once a popup is seen, hook its Opened so future opens are
                // themed at the instant they open even if no source control was hooked.
                if (_hooked.TryAdd(popup, null))
                    popup.Opened += (_, _) => ThemeOpenPopups();

                if (popup.Child is FrameworkElement child && child.RequestedTheme != _currentTheme)
                {
                    child.RequestedTheme = _currentTheme;
                    themed++;
                }
            }

            if (themed > 0)
                Serilog.Log.Debug("[WinUI] Popup themed at open: {Count} to {Theme}", themed, _currentTheme);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[WinUI] Popup theme walk failed");
        }
    }

    private static Frame? FindFrame(DependencyObject node)
    {
        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(node, i);
            if (child is Frame frame)
                return frame;
            if (FindFrame(child) is { } nested)
                return nested;
        }
        return null;
    }

    private static bool IsSystemDarkMode()
    {
        var background = new UISettings().GetColorValue(UIColorType.Background);
        var luminance = 0.299 * background.R + 0.587 * background.G + 0.114 * background.B;
        return luminance < 128;
    }
}
