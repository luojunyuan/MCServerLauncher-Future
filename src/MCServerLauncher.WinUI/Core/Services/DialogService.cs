using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core.Localization;
using Serilog;

namespace MCServerLauncher.WinUI.Core.Services;

public sealed class DialogService : IDialogService
{
    private readonly ILocalizationService _localization;

    public DialogService(ILocalizationService localization) => _localization = localization;

    public async Task<bool> ConfirmAsync(
        XamlRoot root,
        string title,
        string content,
        string primaryButton,
        string closeButton,
        bool isDestructive = false)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = content,
            PrimaryButtonText = primaryButton,
            CloseButtonText = closeButton,
            DefaultButton = ContentDialogButton.Primary
        };
        ApplyAccentPrimaryStyle(dialog, isDestructive);

        try
        {
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception ex)
        {
            // ShowAsync can fail when the app is closing, the XamlRoot is
            // disconnected, or another dialog is already open. Never let that
            // crash an async-void caller: fall back to the safe default.
            Log.Warning(ex, "[WinUI] ContentDialog ShowAsync failed (Confirm)");
            return false;
        }
    }

    public async Task ShowErrorAsync(XamlRoot root, string title, string content)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = content,
            CloseButtonText = _localization.Get("OK"),
            DefaultButton = ContentDialogButton.Close
        };
        try
        {
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WinUI] ContentDialog ShowAsync failed (Error)");
        }
    }

    public async Task<bool> ConfirmCountdownAsync(
        XamlRoot root,
        string title,
        string content,
        string primaryButton,
        string closeButton,
        int countdownSeconds = 5,
        bool isDestructive = false)
    {
        var remaining = Math.Max(1, countdownSeconds);
        var dialog = new ContentDialog
        {
            XamlRoot = root,
            Title = title,
            Content = content,
            PrimaryButtonText = $"{primaryButton} ({remaining}s)",
            CloseButtonText = closeButton,
            DefaultButton = ContentDialogButton.Close,
            IsPrimaryButtonEnabled = false
        };
        ApplyAccentPrimaryStyle(dialog, isDestructive);

        var dismissed = false;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            // A tick can be queued just before the dialog is dismissed; ignore it
            // instead of mutating a dialog that is already closing.
            if (dismissed || remaining <= 0)
            {
                timer.Stop();
                return;
            }

            remaining--;
            if (remaining > 0)
            {
                dialog.PrimaryButtonText = $"{primaryButton} ({remaining}s)";
                return;
            }

            timer.Stop();
            dialog.PrimaryButtonText = primaryButton;
            dialog.IsPrimaryButtonEnabled = true;
        };

        timer.Start();
        try
        {
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WinUI] ContentDialog ShowAsync failed (Countdown)");
            return false;
        }
        finally
        {
            dismissed = true;
            timer.Stop();
        }
    }

    private static void ApplyAccentPrimaryStyle(ContentDialog dialog, bool isDestructive)
    {
        if (isDestructive
            && Application.Current.Resources.TryGetValue("AccentButtonStyle", out var style)
            && style is Style accentButtonStyle)
        {
            dialog.PrimaryButtonStyle = accentButtonStyle;
        }
    }
}
