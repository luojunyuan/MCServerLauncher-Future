using Windows.UI.Xaml.Controls;

namespace MCServerLauncher.WinUI.Core.Services;

public sealed class NavigationService : INavigationService
{
    private Frame? _frame;

    public void Attach(Frame frame) => _frame = frame;

    public bool Navigate(Type pageType, object? parameter = null)
    {
        if (_frame is null) return false;
        if (_frame.Content?.GetType() == pageType && parameter is null) return true;

        var navigated = _frame.Navigate(pageType, parameter);
        if (!navigated) return false;

        if (_frame.Content is Page page)
            page.NavigationCacheMode = Windows.UI.Xaml.Navigation.NavigationCacheMode.Required;
        _frame.BackStack.Clear();
        return true;
    }

    public bool GoBack()
    {
        if (_frame?.CanGoBack != true) return false;
        _frame.GoBack();
        return true;
    }
}
