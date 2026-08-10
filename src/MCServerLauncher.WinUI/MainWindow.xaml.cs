using System.Runtime.InteropServices;

namespace MCServerLauncher.WinUI;

public sealed partial class MainWindow : WinUIIslands.Window
{
    private const int MinimumWidth = 480;
    private const int MinimumHeight = 600;
    private const uint WmSetIcon = 0x0080;
    private const uint ImageIcon = 1;
    private const uint LoadFromFile = 0x00000010;

    public MainWindow(Exception? startupError = null)
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SystemBackdrop = new WinUIIslands.MicaBackdrop();
        RootPage = new MainPage(startupError);
        WindowRoot.Children.Add(RootPage);
        SetTitleBar(RootPage.TitleBarElement);
        Title = RootPage.ProductName;
        Activated += (_, _) => ApplyWindowIcon(this);
        if (AppWindow is { } appWindow)
        {
            var dpi = GetDpiForSystem();
            var scale = dpi / 96.0;
            appWindow.Resize(new Windows.Graphics.SizeInt32(
                (int)Math.Round(1138 * scale),
                (int)Math.Round(750 * scale)));
            if (appWindow.Presenter is WinUIIslands.Windowing.OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = (int)Math.Round(MinimumWidth * scale);
                presenter.PreferredMinimumHeight = (int)Math.Round(MinimumHeight * scale);
            }
        }
    }

    public MainPage RootPage { get; }

    /// <summary>
    ///     Applies the application icon to the window's taskbar and title bar via Win32.
    /// </summary>
    public static unsafe void ApplyWindowIcon(WinUIIslands.Window window)
    {
        var hwnd = WinUIIslands.Windowing.WindowNative.GetWindowHandle(window);
        if (hwnd == IntPtr.Zero) return;

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "MCServerLauncherFuture.ico");
        if (!File.Exists(iconPath)) return;

        fixed (char* path = iconPath)
        {
            var hIcon = LoadImageW(IntPtr.Zero, (IntPtr)path, ImageIcon, 0, 0, LoadFromFile);
            if (hIcon != IntPtr.Zero)
            {
                SendMessage(hwnd, WmSetIcon, new IntPtr(1), hIcon); // ICON_SMALL (title bar / taskbar small)
                SendMessage(hwnd, WmSetIcon, new IntPtr(0), hIcon); // ICON_BIG (alt-tab)
            }
        }
    }

    [DllImport("user32.dll", EntryPoint = "LoadImageW")]
    private static extern IntPtr LoadImageW(IntPtr hinst, IntPtr name, uint type, int cx, int cy, uint fuLoad);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForSystem();
}
