using System.Reflection;
using Windows.ApplicationModel.Activation;
using MCServerLauncher.WinUI.Core.Services;
using Serilog;

namespace MCServerLauncher.WinUI;

public sealed partial class App : WinUIIslands.Application
{
    private static Mutex? _instanceMutex;
    private static readonly List<WinUIIslands.Window> SecondaryWindows = [];
    private static bool _mainWindowClosed;
    private static int _servicesDisposed;

    public static MainWindow Window { get; private set; } = null!;
    public static Windows.System.DispatcherQueue DispatcherQueue { get; private set; } = null!;
    public static AppServices Services { get; private set; } = null!;
    public static Version AppVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 1);
    public static nint WindowHandle => WinUIIslands.Windowing.WindowNative.GetWindowHandle(Window);

    public App()
    {
        InitializeComponent();
        UnhandledException += OnXamlUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    internal static void RegisterSecondaryWindow(WinUIIslands.Window window)
    {
        lock (SecondaryWindows)
        {
            if (!SecondaryWindows.Contains(window)) SecondaryWindows.Add(window);
        }
    }

    internal static void UnregisterSecondaryWindow(WinUIIslands.Window window)
    {
        lock (SecondaryWindows) SecondaryWindows.Remove(window);
        _ = DisposeServicesIfReadyAsync();
    }

    protected override async void OnIslandLaunched(LaunchActivatedEventArgs e)
    {
        DispatcherQueue = global::Windows.System.DispatcherQueue.GetForCurrentThread();
        var servicesCreated = false;

        try
        {
            Services = AppServices.Create();
            servicesCreated = true;
            _instanceMutex = new Mutex(true, "MCServerLauncher.Future.WinUI", out var createdNew);
            if (!createdNew)
            {
                await Services.DisposeAsync();
                _instanceMutex.Dispose();
                _instanceMutex = null;
                return;
            }

            await Services.InitializeAsync();
            ActivateMainWindow();
            GC.KeepAlive(_instanceMutex);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "[WinUI] Application startup failed");
            if (servicesCreated) ActivateMainWindow(ex);
        }
    }

    private static void ActivateMainWindow(Exception? startupError = null)
    {
        Window = new MainWindow(startupError);
        Window.Closed += (_, _) =>
        {
            lock (SecondaryWindows) _mainWindowClosed = true;
            _ = DisposeServicesIfReadyAsync();
        };
        Window.Activate();
        MCServerLauncher.WinUI.MainWindow.ApplyWindowSetup(App.Window);
    }

    private static void OnUnhandledException(object sender, System.UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception)
        {
            Log.Fatal(exception, "[WinUI] Unhandled exception");
            ShowExceptionWindow(exception);
        }
    }

    private static void OnXamlUnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs args)
    {
        args.Handled = true;
        Log.Error(args.Exception, "[WinUI] Unhandled UI exception");
        ShowExceptionWindow(args.Exception);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        args.SetObserved();
        Log.Error(args.Exception, "[WinUI] Unobserved task exception");
        ShowExceptionWindow(args.Exception);
    }

    private static void ShowExceptionWindow(Exception exception)
    {
        if (DispatcherQueue is null || Services is null || Volatile.Read(ref _servicesDisposed) != 0) return;
        DispatcherQueue.TryEnqueue(() =>
        {
            var window = new ExceptionWindow(exception);
            RegisterSecondaryWindow(window);
            window.Activate();
        });
    }

    private static async Task DisposeServicesIfReadyAsync()
    {
        lock (SecondaryWindows)
        {
            if (!_mainWindowClosed || SecondaryWindows.Count > 0) return;
        }

        if (Interlocked.Exchange(ref _servicesDisposed, 1) != 0) return;
        try
        {
            await Services.DisposeAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[WinUI] Failed to dispose application services");
        }
        finally
        {
            _instanceMutex?.Dispose();
            _instanceMutex = null;
        }
    }
}
