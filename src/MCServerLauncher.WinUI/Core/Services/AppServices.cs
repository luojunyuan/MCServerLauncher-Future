using Microsoft.Extensions.DependencyInjection;
using Serilog;
using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Storage;

namespace MCServerLauncher.WinUI.Core.Services;

public sealed class AppServices : IAppServices, IAsyncDisposable
{
    private readonly ServiceProvider _provider;

    private AppServices(
        ServiceProvider provider,
        StoragePaths paths,
        SettingsStore settings,
        DaemonStore daemons,
        ILocalizationService localization,
        IDaemonConnectionService daemonConnections,
        INotificationService notifications,
        IDialogService dialogs,
        IThemeService themes,
        IFilePickerService files,
        IClipboardService clipboard)
    {
        _provider = provider;
        Paths = paths;
        Settings = settings;
        Daemons = daemons;
        Localization = localization;
        DaemonConnections = daemonConnections;
        Notifications = notifications;
        Dialogs = dialogs;
        Themes = themes;
        Files = files;
        Clipboard = clipboard;
    }

    public StoragePaths Paths { get; }
    public SettingsStore Settings { get; }
    public DaemonStore Daemons { get; }
    public ILocalizationService Localization { get; }
    public IDaemonConnectionService DaemonConnections { get; }
    public INotificationService Notifications { get; }
    public IDialogService Dialogs { get; }
    public IThemeService Themes { get; }
    public IFilePickerService Files { get; }
    public IClipboardService Clipboard { get; }

    public static AppServices Create()
    {
        var paths = StoragePaths.Initialize();
        ConfigureLogging(paths);

        var collection = new ServiceCollection();
        collection.AddSingleton<ILocalizationService, LocalizationService>();
        collection.AddSingleton<IDaemonConnectionService, DaemonConnectionService>();
        collection.AddSingleton<INotificationService, NotificationService>();
        collection.AddSingleton<IDialogService, DialogService>();
        collection.AddSingleton<IThemeService, ThemeService>();
        collection.AddSingleton<IFilePickerService, FilePickerService>();
        collection.AddSingleton<IClipboardService, ClipboardService>();
        collection.AddSingleton<INavigationService, NavigationService>();

        var provider = collection.BuildServiceProvider();
        return new AppServices(
            provider,
            paths,
            new SettingsStore(paths),
            new DaemonStore(paths),
            provider.GetRequiredService<ILocalizationService>(),
            provider.GetRequiredService<IDaemonConnectionService>(),
            provider.GetRequiredService<INotificationService>(),
            provider.GetRequiredService<IDialogService>(),
            provider.GetRequiredService<IThemeService>(),
            provider.GetRequiredService<IFilePickerService>(),
            provider.GetRequiredService<IClipboardService>());
    }

    public async Task InitializeAsync()
    {
        Localization.ChangeLanguage(Settings.Current.App.Language);
        if (Settings.Current.App.IsFirstSetupFinished)
            await ConnectConfiguredDaemonsAsync();
    }

    public Task ConnectConfiguredDaemonsAsync() => ConnectConfiguredDaemonsCoreAsync();

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _provider.DisposeAsync();
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    private async Task ConnectConfiguredDaemonsCoreAsync()
    {
        foreach (var config in Daemons.Items)
        {
            try { await DaemonConnections.GetAsync(config); } catch { }
        }
    }

    private static void ConfigureLogging(StoragePaths paths)
    {
        Directory.CreateDirectory(paths.LogsRoot);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Async(writer => writer.File(
                Path.Combine(paths.LogsRoot, "WinUILog-.txt"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"))
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
