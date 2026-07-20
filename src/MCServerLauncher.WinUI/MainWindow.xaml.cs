namespace MCServerLauncher.WinUI;

public sealed partial class MainWindow : WinUIIslands.Window
{
    private const int MinimumWidth = 480;
    private const int MinimumHeight = 600;

    public MainWindow(Exception? startupError = null)
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new WinUIIslands.MicaBackdrop();
        RootPage = new MainPage(startupError);
        WindowRoot.Children.Add(RootPage);
        SetTitleBar(RootPage.TitleBarElement);
        Title = RootPage.ProductName;
        if (AppWindow is { } appWindow)
        {
            appWindow.Resize(new Windows.Graphics.SizeInt32(1138, 750));
            if (appWindow.Presenter is WinUIIslands.Windowing.OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = MinimumWidth;
                presenter.PreferredMinimumHeight = MinimumHeight;
            }
        }
    }

    public MainPage RootPage { get; }
}
