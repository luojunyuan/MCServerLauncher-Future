using MCServerLauncher.WinUI.Models;

namespace MCServerLauncher.WinUI.InstanceConsole;

public sealed class InstanceConsoleWindow : WinUIIslands.Window
{
    private const int MinimumWidth = 330;
    private const int MinimumHeight = 600;

    public InstanceConsoleWindow()
        : this(new DaemonConfigModel(), Guid.Empty, isDebugMode: true)
    {
    }

    public InstanceConsoleWindow(DaemonConfigModel daemonConfig, Guid instanceId)
        : this(daemonConfig, instanceId, isDebugMode: false)
    {
    }

    private InstanceConsoleWindow(DaemonConfigModel daemonConfig, Guid instanceId, bool isDebugMode)
    {
        var view = new InstanceConsoleView(this, daemonConfig, instanceId, isDebugMode);
        Content = view;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(view.TitleBarElement);
        SystemBackdrop = new WinUIIslands.MicaBackdrop();
        Title = Core.AppInfo.ProductName;
        if (AppWindow is { } appWindow)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(1000, 700));
            if (appWindow.Presenter is WinUIIslands.Windowing.OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = MinimumWidth;
                presenter.PreferredMinimumHeight = MinimumHeight;
            }
        }
    }
}
