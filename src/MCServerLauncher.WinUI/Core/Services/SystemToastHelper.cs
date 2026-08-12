using System.Security;
using Serilog;
using Windows.ApplicationModel;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace MCServerLauncher.WinUI.Core.Services;

/// <summary>
///     Best-effort system toast helper for an unpackaged WinUI Islands app.
///     The WinRT toast API requires a package identity or a registered
///     AppUserModelId; unpackaged processes may have neither, so every call is
///     guarded and failure returns false so callers keep their in-app fallback.
///     Non-blocking: toast submission is fire-and-forget on the WinRT side.
/// </summary>
public static class SystemToastHelper
{
    /// <summary>AppUserModelId used when the app does not declare a package identity.</summary>
    public const string AppUserModelId = "MCServerLauncher.Future";

    /// <summary>
    ///     Attempts to show a system toast. Never throws.
    /// </summary>
    /// <returns>True when the toast was submitted; false when the platform rejected it.</returns>
    public static bool TryShow(string title, string body)
    {
        try
        {
            var aumid = ResolveAumid();
            var document = new XmlDocument();
            document.LoadXml(
                $"<toast><visual><binding template=\"ToastGeneric\">" +
                $"<text>{SecurityElement.Escape(title)}</text>" +
                $"<text>{SecurityElement.Escape(body)}</text>" +
                $"</binding></visual></toast>");
            ToastNotificationManager.CreateToastNotifier(aumid)
                .Show(new ToastNotification(document));
            return true;
        }
        catch (Exception ex)
        {
            // ResolveAumid never throws, so this log line is safe even here.
            Log.Debug(ex, "[WinUI] System notification unavailable (AUMID: {Aumid})", ResolveAumid());
            return false;
        }
    }

    private static string ResolveAumid()
    {
        try
        {
            // Packaged builds expose a package identity; derive the AppUserModelId
            // from it. Unpackaged (WindowsPackageType=None) throws, so fall back to
            // the constant AUMID used for shortcut-based toast registration.
            if (Package.Current.Id is { } id && !string.IsNullOrEmpty(id.FamilyName))
                return $"{id.FamilyName}!App";
        }
        catch
        {
            // Unpackaged process: no package identity.
        }

        return AppUserModelId;
    }
}
