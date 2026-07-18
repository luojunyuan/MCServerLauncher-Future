using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using MCServerLauncher.WinUI.Core.Localization;

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
        if (isDestructive
            && Application.Current.Resources.TryGetValue("AccentButtonStyle", out var style)
            && style is Style accentButtonStyle)
        {
            dialog.PrimaryButtonStyle = accentButtonStyle;
        }

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
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
        await dialog.ShowAsync();
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
        if (isDestructive
            && Application.Current.Resources.TryGetValue("AccentButtonStyle", out var style)
            && style is Style accentButtonStyle)
        {
            dialog.PrimaryButtonStyle = accentButtonStyle;
        }

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
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
        finally
        {
            timer.Stop();
        }
    }
}
