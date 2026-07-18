using MCServerLauncher.WinUI.Core.Localization;
using MCServerLauncher.WinUI.Core.Storage;
using Windows.Storage;

namespace MCServerLauncher.WinUI.Core.Services;

public interface IDaemonConnectionService
{
    Task<MCServerLauncher.DaemonClient.IDaemon?> GetAsync(
        Models.DaemonConfigModel config,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(Models.DaemonConfigModel config);
}

public interface INavigationService
{
    void Attach(Windows.UI.Xaml.Controls.Frame frame);
    bool Navigate(Type pageType, object? parameter = null);
    bool GoBack();
}

public enum NotificationSeverity
{
    Informational,
    Success,
    Warning,
    Error
}

public sealed record NotificationMessage(
    string Title,
    string Message,
    NotificationSeverity Severity,
    bool IsClosable = true,
    int DurationMs = 1500,
    bool ShowSystemNotification = true);

public interface INotificationService
{
    event EventHandler<NotificationMessage>? NotificationRaised;
    void Push(string title, string message, NotificationSeverity severity = NotificationSeverity.Informational,
        bool isClosable = true, int durationMs = 1500, bool showSystemNotification = true);
}

public interface IDialogService
{
    Task<bool> ConfirmAsync(
        Windows.UI.Xaml.XamlRoot root,
        string title,
        string content,
        string primaryButton,
        string closeButton,
        bool isDestructive = false);

    Task<bool> ConfirmCountdownAsync(
        Windows.UI.Xaml.XamlRoot root,
        string title,
        string content,
        string primaryButton,
        string closeButton,
        int countdownSeconds = 5,
        bool isDestructive = false);

    Task ShowErrorAsync(Windows.UI.Xaml.XamlRoot root, string title, string content);
}

public interface IThemeService
{
    void Apply(Windows.UI.Xaml.FrameworkElement root, string theme);
}

public interface IFilePickerService
{
    Task<StorageFile?> PickFileAsync(nint windowHandle, string? suggestedStartLocation = null);
    Task<IReadOnlyList<StorageFile>> PickFilesAsync(nint windowHandle);
    Task<StorageFile?> PickSaveFileAsync(nint windowHandle, string suggestedFileName);
}

public interface IClipboardService
{
    void SetText(string text);
}

public interface IAppServices
{
    StoragePaths Paths { get; }
    SettingsStore Settings { get; }
    DaemonStore Daemons { get; }
    ILocalizationService Localization { get; }
    IDaemonConnectionService DaemonConnections { get; }
    INotificationService Notifications { get; }
    IDialogService Dialogs { get; }
    IThemeService Themes { get; }
    IFilePickerService Files { get; }
    IClipboardService Clipboard { get; }
}
