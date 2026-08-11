namespace MCServerLauncher.WinUI.Core.Services;

public sealed class NotificationService : INotificationService
{
    public event EventHandler<NotificationMessage>? NotificationRaised;

    public void Push(string title, string message, NotificationSeverity severity = NotificationSeverity.Informational,
        bool isClosable = true, int durationMs = 1500, bool showSystemNotification = true)
    {
        NotificationRaised?.Invoke(this, new NotificationMessage(
            title,
            message,
            severity,
            isClosable,
            durationMs,
            showSystemNotification));
        if (showSystemNotification) SendSystemToast(title, message);
    }

    /// <summary>
    ///     Best-effort system toast. Unpackaged processes may not have a package
    ///     identity, so the WinRT toast API can throw; in that case this returns
    ///     false and the in-app InfoBar raised through <see cref="Push"/> remains
    ///     the authoritative fallback. Never throws.
    /// </summary>
    /// <returns>True when the toast was submitted successfully.</returns>
    public bool SendSystemToast(string title, string body) => SystemToastHelper.TryShow(title, body);
}
