using System.Security;
using Serilog;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

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
        if (showSystemNotification) TryShowSystemNotification(title, message);
    }

    private static void TryShowSystemNotification(string title, string message)
    {
        try
        {
            // Unpackaged processes may not have a package identity. The WinRT
            // toast API throws in that case; the in-app notification remains the
            // authoritative fallback and must not be affected.
            var document = new XmlDocument();
            document.LoadXml($"<toast><visual><binding template=\"ToastGeneric\"><text>{SecurityElement.Escape(title)}</text><text>{SecurityElement.Escape(message)}</text></binding></visual></toast>");
            ToastNotificationManager.CreateToastNotifier().Show(new ToastNotification(document));
        }
        catch (Exception exception)
        {
            Log.Debug(exception, "[WinUI] System notification unavailable for unpackaged process");
        }
    }
}
