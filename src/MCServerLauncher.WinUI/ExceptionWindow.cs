using MCServerLauncher.WinUI.Views.Pages;

namespace MCServerLauncher.WinUI;

public sealed class ExceptionWindow : CoreIsland.Window
{
    private const int WindowWidth = 650;
    private const int WindowHeight = 385;

    public ExceptionWindow(Exception exception)
    {
        Content = new StartupErrorPage(exception, Close);
        SystemBackdrop = new CoreIsland.MicaBackdrop();
        Title = App.Services.Localization.Get("ErrorDialogTitle");
        if (AppWindow is { } appWindow)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(WindowWidth, WindowHeight));
            if (appWindow.Presenter is CoreIsland.Windowing.OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = WindowWidth;
                presenter.PreferredMinimumHeight = WindowHeight;
            }
        }
        Closed += (_, _) => App.UnregisterSecondaryWindow(this);
    }
}
